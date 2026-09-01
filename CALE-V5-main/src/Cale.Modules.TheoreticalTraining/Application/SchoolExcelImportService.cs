using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed class SchoolExcelImportPreviewCache
{
    private readonly ConcurrentDictionary<Guid, CachedExcelImport> _items = new();

    public void Put(CachedExcelImport preview) => _items[preview.PreviewId] = preview;

    public CachedExcelImport? Take(Guid previewId, int schoolUserId)
    {
        if (!_items.TryGetValue(previewId, out var item))
        {
            return null;
        }

        if (item.SchoolUserId != schoolUserId || item.ExpiresAtUtc < DateTime.UtcNow)
        {
            _items.TryRemove(previewId, out _);
            return null;
        }

        return item;
    }

    public void Remove(Guid previewId) => _items.TryRemove(previewId, out _);
}

public sealed record CachedExcelImport(
    Guid PreviewId,
    int SchoolUserId,
    string FileName,
    string ImportType,
    DateTime ExpiresAtUtc,
    IReadOnlyList<ParsedExcelRow> Rows);

public sealed record ParsedExcelRow(
    int LineNumber,
    string Label,
    string Action,
    string Severity,
    string? Message,
    ApprenticeImportPayload? Apprentice,
    TheoryExamImportPayload? Exam);

public sealed record ApprenticeImportPayload(
    string Name,
    string Email,
    string? DocumentType,
    string? DocumentNumber,
    string? Phone,
    string? Address,
    string? ContactEmail,
    DateOnly? EnrollmentDate,
    string? EnrollmentMonth,
    int? OrderNumber,
    string? LicenseCategory,
    string? AttendanceDayType,
    string? ScheduleSlot,
    string? ReceiptNumber,
    decimal AmountDue,
    decimal AmountPaid,
    decimal BalanceDue,
    string? PaymentMethod,
    decimal? BalancePaymentAmount,
    decimal AccountsReceivable,
    DateOnly? BalancePaymentDate,
    string? BalancePaymentMethod,
    string? BalanceReceiptNumber,
    string? EnrollmentPin,
    bool RuntRegistered,
    bool IsEnrolled);

public sealed record TheoryExamImportPayload(
    DateOnly ExamDate,
    TimeOnly SlotTime,
    string StudentLabel);

public sealed class SchoolExcelImportService
{
    public const int MaxRows = 3000;

    private readonly CaleDbContext _db;
    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _schoolProfiles;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly SchoolExcelImportPreviewCache _cache;
    private readonly TheoryTrainingService _theory;
    private readonly INotificationPublisher _notifications;

    public SchoolExcelImportService(
        CaleDbContext db,
        IUserStore users,
        ISchoolProfileStore schoolProfiles,
        IPasswordHasher hasher,
        IClock clock,
        SchoolExcelImportPreviewCache cache,
        TheoryTrainingService theory,
        INotificationPublisher notifications)
    {
        _db = db;
        _users = users;
        _schoolProfiles = schoolProfiles;
        _hasher = hasher;
        _clock = clock;
        _cache = cache;
        _theory = theory;
        _notifications = notifications;
    }

    public async Task<ExcelImportPreviewDto> PreviewAsync(
        int schoolUserId,
        string importType,
        string fileName,
        Stream stream,
        CancellationToken ct)
    {
        var schoolDomain = await ResolveSchoolEmailDomainAsync(schoolUserId, ct);
        var rows = importType switch
        {
            "apprentices" => ParseApprenticesWorkbook(stream, schoolDomain),
            "theory-exams" => ParseTheoryExamsWorkbook(stream),
            _ => throw new DomainException("Tipo de importación no soportado.", 400, "invalid_import_type")
        };

        if (rows.Count == 0)
        {
            throw new DomainException("El archivo no tiene filas para importar.", 400, "empty_import");
        }

        if (rows.Count > MaxRows)
        {
            throw new DomainException($"Máximo {MaxRows} filas por archivo.", 400, "import_too_large");
        }

        var resolved = new List<ParsedExcelRow>();
        foreach (var row in rows)
        {
            if (row.Action == "error")
            {
                resolved.Add(row);
                continue;
            }

            if (row.Apprentice is not null)
            {
                resolved.Add(await ClassifyApprenticeRowAsync(schoolUserId, row, ct));
            }
            else if (row.Exam is not null)
            {
                resolved.Add(await ClassifyTheoryExamRowAsync(schoolUserId, row, ct));
            }
            else
            {
                resolved.Add(row);
            }
        }

        var previewId = Guid.NewGuid();
        _cache.Put(new CachedExcelImport(
            previewId,
            schoolUserId,
            fileName,
            importType,
            _clock.UtcNow.AddMinutes(30),
            resolved));

        return BuildPreview(previewId, fileName, importType, resolved);
    }

    public async Task<ExcelImportCommitResultDto> CommitAsync(
        int schoolUserId,
        Guid previewId,
        CancellationToken ct)
    {
        var cached = _cache.Take(previewId, schoolUserId)
            ?? throw new NotFoundException("La vista previa expiró. Vuelve a subir el archivo.", "preview_expired");

        if (cached.Rows.Any(r => r.Action == "error"))
        {
            throw new DomainException("Corrige los errores antes de importar.", 400, "import_has_errors");
        }

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;
        var credentials = new List<ExcelImportCredentialDto>();
        var results = new List<ParsedExcelRow>();

        foreach (var row in cached.Rows)
        {
            try
            {
                if (row.Action == "skip")
                {
                    skipped++;
                    results.Add(row);
                    continue;
                }

                if (cached.ImportType == "apprentices" && row.Apprentice is not null)
                {
                    var isCreate = row.Action == "create";
                    var credential = await UpsertApprenticeAsync(schoolUserId, row.Apprentice, ct);
                    if (credential is not null)
                    {
                        credentials.Add(credential);
                    }

                    if (isCreate)
                    {
                        created++;
                    }
                    else
                    {
                        updated++;
                    }
                }
                else if (cached.ImportType == "theory-exams" && row.Exam is not null)
                {
                    await UpsertExamSlotAsync(schoolUserId, row.Exam, ct);
                    updated++;
                }
                else
                {
                    skipped++;
                }

                results.Add(row);
            }
            catch (Exception ex)
            {
                failed++;
                results.Add(row with
                {
                    Action = "error",
                    Severity = "error",
                    Message = ex is DomainException de ? de.Message : "No se pudo importar esta fila."
                });
            }
        }

        _cache.Remove(previewId);
        await _db.SaveChangesAsync(ct);

        return new ExcelImportCommitResultDto(
            previewId,
            created,
            updated,
            skipped,
            failed,
            credentials,
            results.Select(ToDto).ToList(),
            BuildCredentialsCsv(credentials));
    }

    private async Task<ParsedExcelRow> ClassifyApprenticeRowAsync(
        int schoolUserId,
        ParsedExcelRow row,
        CancellationToken ct)
    {
        var payload = row.Apprentice!;
        User? user = null;
        if (!string.IsNullOrWhiteSpace(payload.DocumentNumber))
        {
            var profile = await _db.Set<SchoolApprenticeProfile>()
                .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                    && x.DocumentNumber == payload.DocumentNumber, ct);
            if (profile is not null)
            {
                user = await _users.GetByIdAsync(profile.StudentUserId, ct);
            }
        }

        user ??= await _users.FindByEmailAsync(payload.Email, ct);
        if (user is null)
        {
            return row with { Action = "create", Severity = "ok", Message = "Nuevo aprendiz" };
        }

        if (user.SchoolId == schoolUserId && user.Role == Roles.Student)
        {
            var balance = Math.Max(0, payload.AmountDue - payload.AmountPaid);
            if (balance > 0)
            {
                return row with
                {
                    Action = "update",
                    Severity = "warning",
                    Message = $"Actualizar expediente · Saldo pendiente: {balance:N0}"
                };
            }

            return row with { Action = "update", Severity = "ok", Message = "Actualizar expediente" };
        }

        if (user.SchoolId == schoolUserId)
        {
            return row with
            {
                Action = "error",
                Severity = "error",
                Message = "El correo pertenece a un usuario que no es estudiante."
            };
        }

        return row with
        {
            Action = "error",
            Severity = "error",
            Message = "El correo ya está registrado en otra escuela."
        };
    }

    private async Task<ExcelImportCredentialDto?> UpsertApprenticeAsync(
        int schoolUserId,
        ApprenticeImportPayload payload,
        CancellationToken ct)
    {
        var (user, tempPassword) = await ResolveOrCreateStudentAsync(schoolUserId, payload, ct);
        var now = _clock.UtcNow;

        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == user.Id, ct);
        if (enrollment is null)
        {
            enrollment = new SchoolStudentEnrollment
            {
                SchoolUserId = schoolUserId,
                StudentUserId = user.Id,
                Status = StudentEnrollmentStatuses.Active,
                CreatedAt = now,
                UpdatedAt = now,
                AcceptedAt = now
            };
            await _db.Set<SchoolStudentEnrollment>().AddAsync(enrollment, ct);
        }

        if (!string.IsNullOrWhiteSpace(payload.LicenseCategory))
        {
            enrollment.LicenseCategories = payload.LicenseCategory.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(payload.AttendanceDayType))
        {
            enrollment.AttendanceDayType = payload.AttendanceDayType;
        }

        if (enrollment.Status is StudentEnrollmentStatuses.Pending or StudentEnrollmentStatuses.Accepted)
        {
            enrollment.Status = StudentEnrollmentStatuses.Active;
            enrollment.AcceptedAt ??= now;
        }

        enrollment.UpdatedAt = now;

        var profile = await _db.Set<SchoolApprenticeProfile>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == user.Id, ct);
        if (profile is null)
        {
            profile = new SchoolApprenticeProfile
            {
                SchoolUserId = schoolUserId,
                StudentUserId = user.Id,
                CreatedAt = now
            };
            await _db.Set<SchoolApprenticeProfile>().AddAsync(profile, ct);
        }

        ApplyProfile(profile, payload, now);

        return string.IsNullOrWhiteSpace(tempPassword)
            ? null
            : new ExcelImportCredentialDto(user.Name, user.Email, tempPassword);
    }

    private async Task<(User User, string? TempPassword)> ResolveOrCreateStudentAsync(
        int schoolUserId,
        ApprenticeImportPayload payload,
        CancellationToken ct)
    {
        User? user = null;
        if (!string.IsNullOrWhiteSpace(payload.DocumentNumber))
        {
            var existingProfile = await _db.Set<SchoolApprenticeProfile>()
                .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                    && x.DocumentNumber == payload.DocumentNumber, ct);
            if (existingProfile is not null)
            {
                user = await _users.GetByIdAsync(existingProfile.StudentUserId, ct);
            }
        }

        user ??= await _users.FindByEmailAsync(payload.Email, ct);
        if (user is not null)
        {
            if (user.SchoolId != schoolUserId || user.Role != Roles.Student)
            {
                throw new DomainException("No se puede vincular este usuario.", 400, "invalid_student");
            }

            if (!string.Equals(user.Name, payload.Name, StringComparison.OrdinalIgnoreCase))
            {
                user.UpdateProfile(payload.Name.Trim(), user.Email);
                await _users.SaveChangesAsync(ct);
            }

            return (user, null);
        }

        var tempPassword = GenerateTempPassword();
        var hash = _hasher.Hash(tempPassword);
        var now = _clock.UtcNow;
        user = User.RegisterStudent(payload.Name.Trim(), payload.Email.Trim(), hash, now, schoolUserId);
        await _users.AddAsync(user, ct);
        return (user, tempPassword);
    }

    private static void ApplyProfile(SchoolApprenticeProfile profile, ApprenticeImportPayload payload, DateTime now)
    {
        profile.DocumentType = payload.DocumentType;
        profile.DocumentNumber = payload.DocumentNumber;
        profile.Phone = payload.Phone;
        profile.Address = payload.Address;
        profile.ContactEmail = payload.ContactEmail;
        profile.EnrollmentDate = payload.EnrollmentDate;
        profile.EnrollmentMonth = payload.EnrollmentMonth;
        profile.OrderNumber = payload.OrderNumber;
        profile.ScheduleSlot = payload.ScheduleSlot;
        profile.ReceiptNumber = payload.ReceiptNumber;
        profile.AmountDue = payload.AmountDue;
        profile.AmountPaid = payload.AmountPaid;
        profile.BalanceDue = Math.Max(0, payload.AmountDue - payload.AmountPaid);
        profile.PaymentMethod = payload.PaymentMethod;
        profile.BalancePaymentAmount = payload.BalancePaymentAmount;
        profile.AccountsReceivable = payload.AccountsReceivable;
        profile.BalancePaymentDate = payload.BalancePaymentDate;
        profile.BalancePaymentMethod = payload.BalancePaymentMethod;
        profile.BalanceReceiptNumber = payload.BalanceReceiptNumber;
        profile.EnrollmentPin = payload.EnrollmentPin;
        profile.RuntRegistered = payload.RuntRegistered;
        profile.IsEnrolled = payload.IsEnrolled;
        profile.UpdatedAt = now;
    }

    private async Task UpsertExamSlotAsync(
        int schoolUserId,
        TheoryExamImportPayload payload,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var studentUserId = await TryMatchStudentByNameAsync(schoolUserId, payload.StudentLabel, ct);

        if (studentUserId is int studentId)
        {
            await ValidateExamSlotStudentAsync(schoolUserId, studentId, ct);
        }

        var existing = await _db.Set<TheoryExamAppointment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.ExamDate == payload.ExamDate
                && x.SlotTime == payload.SlotTime, ct);

        var previousStudentId = existing?.StudentUserId;
        TheoryExamAppointment entity;

        if (existing is null)
        {
            entity = new TheoryExamAppointment
            {
                SchoolUserId = schoolUserId,
                ExamDate = payload.ExamDate,
                SlotTime = payload.SlotTime,
                StudentUserId = studentUserId,
                StudentLabel = payload.StudentLabel,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _db.Set<TheoryExamAppointment>().AddAsync(entity, ct);
        }
        else
        {
            entity = existing;
            entity.StudentUserId = studentUserId;
            entity.StudentLabel = payload.StudentLabel;
            entity.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        if (entity.StudentUserId is int assignedId && assignedId != previousStudentId)
        {
            await _notifications.NotifyUsersAsync(
                [assignedId],
                new NotificationDraft(
                    "Cita de examen teórico",
                    $"Tu examen teórico está programado para el {entity.ExamDate:dd/MM/yyyy} a las {entity.SlotTime:HH:mm}.",
                    NotificationTypes.TheoryClass,
                    RelatedEntity: "theory_exam_appointment",
                    RelatedId: entity.Id,
                    Link: "/student/training"),
                ct);
        }
    }

    private async Task ValidateExamSlotStudentAsync(
        int schoolUserId,
        int studentId,
        CancellationToken ct)
    {
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentId, ct)
            ?? throw new DomainException(
                "El estudiante no está inscrito en la escuela.",
                400,
                "student_not_enrolled");

        if (!StudentEnrollmentStatuses.CanReserveStatuses.Contains(enrollment.Status))
        {
            throw new DomainException(
                "El estudiante debe estar activo en Programación.",
                400,
                "student_not_authorized");
        }

        if (!enrollment.TheoryExamAuthorized)
        {
            throw new DomainException(
                "El estudiante no está autorizado para examen teórico.",
                400,
                "theory_exam_not_authorized");
        }

        var eligibility = await _theory.GetPracticalEligibilityAsync(schoolUserId, studentId, ct);
        if (eligibility.TheoryExamPassed)
        {
            throw new DomainException(
                "El estudiante ya aprobó el examen teórico.",
                400,
                "theory_exam_already_passed");
        }

        if (!eligibility.TheoryHoursComplete || !eligibility.WorkshopHoursComplete)
        {
            throw new DomainException(
                "El estudiante debe completar las horas de teoría y taller.",
                400,
                "theory_hours_incomplete");
        }
    }

    private async Task<ParsedExcelRow> ClassifyTheoryExamRowAsync(
        int schoolUserId,
        ParsedExcelRow row,
        CancellationToken ct)
    {
        var payload = row.Exam!;
        var studentUserId = await TryMatchStudentByNameAsync(schoolUserId, payload.StudentLabel, ct);
        if (studentUserId is null)
        {
            return row with
            {
                Severity = "warning",
                Message = "Estudiante no encontrado — se guardará solo la etiqueta."
            };
        }

        try
        {
            await ValidateExamSlotStudentAsync(schoolUserId, studentUserId.Value, ct);
            return row with
            {
                Severity = "ok",
                Message = $"Asignar cita a {payload.StudentLabel}"
            };
        }
        catch (DomainException ex)
        {
            return row with
            {
                Action = "error",
                Severity = "error",
                Message = $"{payload.StudentLabel}: {ex.Message}"
            };
        }
    }

    private async Task<int?> TryMatchStudentByNameAsync(
        int schoolUserId,
        string label,
        CancellationToken ct)
    {
        var students = (await _users.ListBySchoolAsync(schoolUserId, ct))
            .Where(x => x.Role == Roles.Student)
            .ToList();
        var normalized = NormalizeName(label);
        var match = students.FirstOrDefault(s => NormalizeName(s.Name).Contains(normalized, StringComparison.Ordinal)
            || normalized.Contains(NormalizeName(s.Name), StringComparison.Ordinal));
        return match?.Id;
    }

    private static string NormalizeName(string value) =>
        new string(value.Trim().ToUpperInvariant().Where(c => !char.IsPunctuation(c)).ToArray())
            .Replace("  ", " ");

    private async Task<string> ResolveSchoolEmailDomainAsync(int schoolUserId, CancellationToken ct)
    {
        var profile = await _schoolProfiles.GetByUserIdAsync(schoolUserId, ct);
        var schoolName = profile?.LegalName;
        if (string.IsNullOrWhiteSpace(schoolName))
        {
            var schoolUser = await _users.GetByIdAsync(schoolUserId, ct);
            schoolName = schoolUser?.Name;
        }

        var slug = SlugifySchoolDomain(schoolName ?? $"escuela{schoolUserId}");
        return $"{slug}.com";
    }

    private List<ParsedExcelRow> ParseApprenticesWorkbook(Stream stream, string schoolDomain)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault(w =>
            w.Name.Contains("RELACION", StringComparison.OrdinalIgnoreCase)
            || w.Name.Contains("PAGOS", StringComparison.OrdinalIgnoreCase)
            || w.Name.Contains("MATRICULA", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.First();

        var headerRow = sheet.Row(1);
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var key = NormalizeHeader(cell.GetString());
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (headers.ContainsKey(key))
            {
                key = $"{key}_{cell.Address.ColumnNumber}";
            }

            headers[key] = cell.Address.ColumnNumber;
        }

        var rows = new List<ParsedExcelRow>();
        var usedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var line = 2; line <= lastRow; line++)
        {
            var name = GetCell(sheet, line, headers, "NOMBRES", "NOMBRE");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            try
            {
                var doc = CleanDoc(GetCell(sheet, line, headers, "NUMERO DOCUMENTO", "DOCUMENTO"));
                var email = GetCell(sheet, line, headers, "CORREO", "EMAIL");
                if (string.IsNullOrWhiteSpace(email))
                {
                    email = BuildDefaultEmail(name, doc, schoolDomain, line, usedEmails);
                }

                var horario = GetCell(sheet, line, headers, "HORARIO");
                var (dayType, slot) = MapScheduleSlot(horario);
                var amountDue = ParseDecimal(GetCell(sheet, line, headers, "VALOR A PAGAR", "VALOR PAGAR"));
                var amountPaid = ParseDecimal(GetCell(sheet, line, headers, "VALOR PAGADO"));
                var balance = ParseDecimal(GetCell(sheet, line, headers, "SALDO PENDIENTE"));
                if (balance == 0 && amountDue > amountPaid)
                {
                    balance = amountDue - amountPaid;
                }

                var payload = new ApprenticeImportPayload(
                    name.Trim(),
                    email.Trim().ToLowerInvariant(),
                    NullIfEmpty(GetCell(sheet, line, headers, "TIPO DOC.", "TIPO DOC", "TIPO DOCUMENTO")),
                    doc,
                    NullIfEmpty(GetCell(sheet, line, headers, "CELULAR", "TELEFONO")),
                    NullIfEmpty(GetCell(sheet, line, headers, "DIRECCION", "DIRECCIÓN")),
                    NullIfEmpty(email),
                    ParseDate(GetCell(sheet, line, headers, "FECHA MATRICULA", "FECHA MATRÍCULA")),
                    NullIfEmpty(GetCell(sheet, line, headers, "MES")),
                    ParseInt(GetCell(sheet, line, headers, "ORDEN")),
                    NullIfEmpty(GetCell(sheet, line, headers, "CATEGORIA", "CATEGORÍA"))?.ToUpperInvariant(),
                    dayType,
                    slot,
                    NullIfEmpty(GetCell(sheet, line, headers, "RECIBO DE CAJA", "RECIBO")),
                    amountDue,
                    amountPaid,
                    balance,
                    NullIfEmpty(GetCell(sheet, line, headers, "METODO DE PAGO", "MÉTODO DE PAGO")),
                    ParseNullableDecimal(GetCell(sheet, line, headers, "ABONO SALDO PENDIENTE")),
                    ParseDecimal(GetCell(sheet, line, headers, "CARTERA")),
                    ParseDate(GetCell(sheet, line, headers, "FECHA PAGO")),
                    NullIfEmpty(GetCellByContains(headers, sheet, line, "METODO DE PAGO_")),
                    NullIfEmpty(GetCellByContains(headers, sheet, line, "RECIBO DE CAJA_")),
                    NullIfEmpty(GetCell(sheet, line, headers, "NUMERO PIN PYMET", "PIN")),
                    ParseBool(GetCell(sheet, line, headers, "INSCRITO EN RUNT", "RUNT")),
                    ParseBool(GetCell(sheet, line, headers, "ENROLADO")));

                rows.Add(new ParsedExcelRow(
                    line,
                    $"{name} · {doc}",
                    "pending",
                    "info",
                    null,
                    payload,
                    null));
            }
            catch (Exception ex)
            {
                rows.Add(new ParsedExcelRow(
                    line,
                    name,
                    "error",
                    "error",
                    ex.Message,
                    null,
                    null));
            }
        }

        return rows;
    }

    private static string BuildDefaultEmail(
        string name,
        string? documentNumber,
        string schoolDomain,
        int line,
        HashSet<string> usedEmails)
    {
        var firstName = ExtractFirstName(name);
        var last3 = ExtractLast3Digits(documentNumber, line);
        var localPart = string.IsNullOrWhiteSpace(firstName)
            ? $"aprendiz{line}{last3}"
            : $"{firstName}{last3}";

        var email = $"{localPart}@{schoolDomain}";
        var suffix = 1;
        while (!usedEmails.Add(email))
        {
            email = $"{localPart}{suffix}@{schoolDomain}";
            suffix++;
        }

        return email;
    }

    private static string ExtractFirstName(string name)
    {
        var normalized = RemoveDiacritics(name.Trim().ToLowerInvariant());
        var first = normalized
            .Split([' ', '.', ',', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(first))
        {
            return "";
        }

        return Regex.Replace(first, @"[^a-z0-9]", "");
    }

    private static string SlugifySchoolDomain(string value)
    {
        var normalized = RemoveDiacritics(value.Trim().ToLowerInvariant());
        var slug = Regex.Replace(normalized, @"[^a-z0-9]", "");
        return string.IsNullOrWhiteSpace(slug) ? "escuela" : slug;
    }

    private static string ExtractLast3Digits(string? documentNumber, int lineFallback)
    {
        if (!string.IsNullOrWhiteSpace(documentNumber))
        {
            var digits = new string(documentNumber.Where(char.IsDigit).ToArray());
            if (digits.Length >= 3)
            {
                return digits[^3..];
            }

            if (digits.Length > 0)
            {
                return digits.PadLeft(3, '0');
            }
        }

        var lineDigits = Math.Abs(lineFallback).ToString(CultureInfo.InvariantCulture);
        return lineDigits.Length >= 3 ? lineDigits[^3..] : lineDigits.PadLeft(3, '0');
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static List<ParsedExcelRow> ParseTheoryExamsWorkbook(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault(w =>
            w.Name.Contains("EXAMEN", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.First();

        var header = sheet.Row(1);
        var slotColumns = new List<(int Column, TimeOnly Time)>();
        foreach (var cell in header.CellsUsed())
        {
            if (cell.Address.ColumnNumber <= 3)
            {
                continue;
            }

            var time = ParseExamSlotHeader(cell.GetString());
            if (time is not null)
            {
                slotColumns.Add((cell.Address.ColumnNumber, time.Value));
            }
        }

        var rows = new List<ParsedExcelRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var line = 2; line <= lastRow; line++)
        {
            var examDate = ParseDate(sheet.Cell(line, 2).GetString())
                ?? ParseDate(sheet.Cell(line, 3).GetString());
            if (examDate is null)
            {
                continue;
            }

            foreach (var (column, slotTime) in slotColumns)
            {
                var label = sheet.Cell(line, column).GetString().Trim();
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                rows.Add(new ParsedExcelRow(
                    line,
                    $"{examDate:yyyy-MM-dd} {slotTime:HH:mm} · {label}",
                    "update",
                    "ok",
                    null,
                    null,
                    new TheoryExamImportPayload(examDate.Value, slotTime, label)));
            }
        }

        return rows;
    }

    private static ExcelImportPreviewDto BuildPreview(
        Guid previewId,
        string fileName,
        string importType,
        IReadOnlyList<ParsedExcelRow> rows)
    {
        var create = rows.Count(r => r.Action == "create");
        var update = rows.Count(r => r.Action == "update" || r.Action == "pending");
        var skip = rows.Count(r => r.Action == "skip");
        var error = rows.Count(r => r.Action == "error");
        var canCommit = error == 0 && rows.Any(r => r.Action is "create" or "update" or "pending");
        return new ExcelImportPreviewDto(
            previewId,
            fileName,
            importType,
            rows.Count,
            create,
            update,
            skip,
            error,
            canCommit,
            error > 0 ? "Corrige las filas con error antes de importar." : null,
            rows.Select(ToDto).ToList());
    }

    private static ExcelImportRowPreviewDto ToDto(ParsedExcelRow row) =>
        new(row.LineNumber, row.Label, row.Action, row.Severity, row.Message);

    private static (string? DayType, string? Slot) MapScheduleSlot(string? horario)
    {
        if (string.IsNullOrWhiteSpace(horario))
        {
            return (null, null);
        }

        var value = horario.Trim().ToUpperInvariant();
        if (value.Contains("SABADO"))
        {
            return (StudentAttendanceDayTypes.Saturday, "SABADOS");
        }

        return (StudentAttendanceDayTypes.Weekday, horario.Trim());
    }

    private static string NormalizeHeader(string value) =>
        value.Trim().ToUpperInvariant().Replace("Á", "A").Replace("É", "E").Replace("Í", "I")
            .Replace("Ó", "O").Replace("Ú", "U");

    private static string GetCell(IXLWorksheet sheet, int row, Dictionary<string, int> headers, params string[] keys)
    {
        foreach (var key in keys)
        {
            var normalized = NormalizeHeader(key);
            if (headers.TryGetValue(normalized, out var col))
            {
                return sheet.Cell(row, col).GetString().Trim();
            }
        }

        return string.Empty;
    }

    private static string GetCellByContains(
        Dictionary<string, int> headers,
        IXLWorksheet sheet,
        int row,
        string prefix)
    {
        foreach (var pair in headers)
        {
            if (pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return sheet.Cell(row, pair.Value).GetString().Trim();
            }
        }

        return string.Empty;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? CleanDoc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.EndsWith(".0", StringComparison.Ordinal))
        {
            text = text[..^2];
        }

        return text;
    }

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        if (decimal.TryParse(value, NumberStyles.Any, new CultureInfo("es-CO"), out result))
        {
            return result;
        }

        return 0;
    }

    private static decimal? ParseNullableDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ParseDecimal(value);
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value.Split('.')[0], out var result))
        {
            return result;
        }

        return null;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        if (DateTime.TryParse(value, new CultureInfo("es-CO"), DateTimeStyles.None, out var dt))
        {
            return DateOnly.FromDateTime(dt);
        }

        return null;
    }

    private static bool ParseBool(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Trim().StartsWith("S", StringComparison.OrdinalIgnoreCase);

    private static TimeOnly? ParseExamSlotHeader(string header)
    {
        var text = header.ToUpperInvariant().Replace(" ", "");
        if (text.Contains("9AM") || text.StartsWith("9"))
        {
            return new TimeOnly(9, 0);
        }

        if (text.Contains("10"))
        {
            return new TimeOnly(10, 0);
        }

        if (text.Contains("11"))
        {
            return new TimeOnly(11, 0);
        }

        if (text.Contains("12"))
        {
            return new TimeOnly(12, 0);
        }

        if (text.Contains("1PM") || text == "1")
        {
            return new TimeOnly(13, 0);
        }

        if (text.Contains("2PM") || text.StartsWith("2"))
        {
            return new TimeOnly(14, 0);
        }

        if (text.Contains("3PM") || text.StartsWith("3"))
        {
            return new TimeOnly(15, 0);
        }

        if (text.Contains("4"))
        {
            return new TimeOnly(16, 0);
        }

        return null;
    }

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(10);
        var sb = new StringBuilder(10);
        foreach (var b in bytes)
        {
            sb.Append(chars[b % chars.Length]);
        }

        return sb.ToString();
    }

    private static string BuildCredentialsCsv(IReadOnlyList<ExcelImportCredentialDto> credentials)
    {
        var sb = new StringBuilder();
        sb.AppendLine("nombre,email,password_temporal");
        foreach (var c in credentials)
        {
            sb.Append(EscapeCsv(c.Name)).Append(',')
                .Append(EscapeCsv(c.Email)).Append(',')
                .Append(EscapeCsv(c.TemporaryPassword)).AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
