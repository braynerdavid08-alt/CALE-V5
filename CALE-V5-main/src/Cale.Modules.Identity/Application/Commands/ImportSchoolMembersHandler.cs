using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Domain.Validation;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class SchoolMemberImportPreviewCache
{
    private readonly ConcurrentDictionary<Guid, CachedPreview> _items = new();

    public void Put(CachedPreview preview) => _items[preview.PreviewId] = preview;

    public CachedPreview? Take(Guid previewId, int schoolUserId)
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

public sealed record CachedPreview(
    Guid PreviewId,
    int SchoolUserId,
    string FileName,
    DateTime ExpiresAtUtc,
    IReadOnlyList<ParsedImportRow> Rows);

public sealed record ParsedImportRow(
    int LineNumber,
    string Name,
    string Email,
    string Role,
    string Action,
    string Severity,
    string? Code,
    string? Message);

public sealed class ImportSchoolMembersHandler
{
    public const int MaxRows = 2000;
    public const string TemplateCsv =
        "nombre,email,rol\n" +
        "Ana Pérez,ana.perez@escuela.edu,Student\n" +
        "Carlos Ruiz,carlos.ruiz@escuela.edu,Teacher\n";

    private readonly IUserStore _users;
    private readonly ISchoolProfileStore _profiles;
    private readonly IMembershipEventStore _events;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly SchoolMemberImportPreviewCache _cache;

    public ImportSchoolMembersHandler(
        IUserStore users,
        ISchoolProfileStore profiles,
        IMembershipEventStore events,
        IPasswordHasher hasher,
        IClock clock,
        SchoolMemberImportPreviewCache cache)
    {
        _users = users;
        _profiles = profiles;
        _events = events;
        _hasher = hasher;
        _clock = clock;
        _cache = cache;
    }

    public async Task<ImportPreviewDto> PreviewAsync(
        int schoolUserId,
        string fileName,
        Stream csvStream,
        CancellationToken ct)
    {
        await EnsureMembershipAsync(schoolUserId, ct);

        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(ct);
        var parsed = ParseCsv(text);
        if (parsed.Count == 0)
        {
            throw new DomainException(
                "El CSV no tiene filas de datos. Descarga la plantilla e inténtalo de nuevo.",
                400,
                "empty_import");
        }

        if (parsed.Count > MaxRows)
        {
            throw new DomainException(
                $"El archivo supera el máximo de {MaxRows} filas.",
                400,
                "import_too_large");
        }

        var profile = await _profiles.GetTrackedByUserIdAsync(schoolUserId, ct)
            ?? throw new NotFoundException("Perfil de escuela no encontrado.", "school_not_found");
        profile.RefreshStatus(_clock.UtcNow);
        var plan = SchoolPlans.Find(profile.PlanCode)
            ?? throw new DomainException("Plan de escuela inválido.", 400, "invalid_plan");

        var teachersUsed = await _users.CountBySchoolAndRoleAsync(schoolUserId, Roles.Teacher, ct);
        var studentsUsed = await _users.CountBySchoolAndRoleAsync(schoolUserId, Roles.Student, ct);
        var teachersBudget = profile.EffectiveMaxTeachers(plan) - teachersUsed;
        var studentsBudget = profile.EffectiveMaxStudents(plan) - studentsUsed;

        var seenEmails = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<ParsedImportRow>(parsed.Count);

        foreach (var raw in parsed)
        {
            rows.Add(await ClassifyRowAsync(
                schoolUserId,
                raw,
                seenEmails,
                () => teachersBudget,
                () => studentsBudget,
                n => teachersBudget -= n,
                n => studentsBudget -= n,
                ct));
        }

        var create = rows.Count(r => r.Action == ImportActions.Create);
        var attach = rows.Count(r => r.Action == ImportActions.Attach);
        var skip = rows.Count(r => r.Action == ImportActions.Skip);
        var error = rows.Count(r => r.Action == ImportActions.Error);
        var canCommit = create + attach > 0 && error == 0;
        string? blocking = null;
        if (error > 0)
        {
            blocking = "Corrige las filas con error o elimina esas líneas del CSV antes de confirmar.";
        }
        else if (create + attach == 0)
        {
            blocking = "No hay filas nuevas para importar (todas ya pertenecen a tu escuela).";
        }

        var previewId = Guid.NewGuid();
        _cache.Put(new CachedPreview(
            previewId,
            schoolUserId,
            string.IsNullOrWhiteSpace(fileName) ? "import.csv" : fileName.Trim(),
            DateTime.UtcNow.AddMinutes(30),
            rows));

        return new ImportPreviewDto(
            previewId,
            string.IsNullOrWhiteSpace(fileName) ? "import.csv" : fileName.Trim(),
            rows.Count,
            create,
            attach,
            skip,
            error,
            canCommit,
            blocking,
            rows.Select(ToDto).ToList());
    }

    public async Task<ImportCommitResultDto> CommitAsync(
        int schoolUserId,
        Guid previewId,
        CancellationToken ct)
    {
        await EnsureMembershipAsync(schoolUserId, ct);
        var cached = _cache.Take(previewId, schoolUserId)
            ?? throw new DomainException(
                "La vista previa expiró o no existe. Vuelve a subir el CSV.",
                400,
                "preview_expired");

        if (cached.Rows.Any(r => r.Action == ImportActions.Error))
        {
            throw new DomainException(
                "Hay errores en la vista previa. No se puede confirmar.",
                400,
                "import_has_errors");
        }

        var actionable = cached.Rows
            .Where(r => r.Action is ImportActions.Create or ImportActions.Attach)
            .ToList();
        if (actionable.Count == 0)
        {
            throw new DomainException(
                "No hay filas para importar.",
                400,
                "empty_import");
        }

        // Re-check seats against live counts
        foreach (var role in new[] { Roles.Teacher, Roles.Student })
        {
            var adds = actionable.Count(r =>
                r.Role == role && r.Action is ImportActions.Create or ImportActions.Attach);
            if (adds == 0)
            {
                continue;
            }

            await SchoolSeatGuard.EnsureCanAddAsync(
                _users, _profiles, _clock, schoolUserId, role, ct);
            var used = await _users.CountBySchoolAndRoleAsync(schoolUserId, role, ct);
            var profile = await _profiles.GetTrackedByUserIdAsync(schoolUserId, ct);
            var plan = SchoolPlans.Find(profile!.PlanCode)!;
            var max = role == Roles.Teacher
                ? profile.EffectiveMaxTeachers(plan)
                : profile.EffectiveMaxStudents(plan);
            if (used + adds > max)
            {
                throw new DomainException(
                    role == Roles.Teacher
                        ? $"El lote supera el límite de docentes ({max})."
                        : $"El lote supera el límite de estudiantes ({max}).",
                    400,
                    "seat_limit_reached");
            }
        }

        var credentials = new List<ImportCredentialDto>();
        var results = new List<ParsedImportRow>();
        var created = 0;
        var attached = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var row in cached.Rows)
        {
            if (row.Action == ImportActions.Skip)
            {
                skipped++;
                results.Add(row);
                continue;
            }

            try
            {
                if (row.Action == ImportActions.Create)
                {
                    var temp = GenerateTemporaryPassword();
                    var user = row.Role == Roles.Teacher
                        ? User.CreateTeacher(
                            row.Name,
                            row.Email,
                            _hasher.Hash(temp),
                            _clock.UtcNow,
                            schoolUserId)
                        : User.RegisterStudent(
                            row.Name,
                            row.Email,
                            _hasher.Hash(temp),
                            _clock.UtcNow,
                            schoolUserId);
                    user.RequirePasswordChange();
                    await _users.AddAsync(user, ct);
                    credentials.Add(new ImportCredentialDto(row.Name, row.Email, row.Role, temp));
                    created++;
                    results.Add(row with
                    {
                        Message = "Cuenta creada. Debe cambiar la contraseña al ingresar."
                    });
                }
                else if (row.Action == ImportActions.Attach)
                {
                    var user = await _users.FindByEmailAsync(row.Email, ct)
                        ?? throw new NotFoundException("Usuario no encontrado.", "user_not_found");
                    if (user.SchoolId is not null && user.SchoolId != schoolUserId)
                    {
                        throw new ConflictException(
                            "Esa cuenta ya está vinculada a otra escuela.",
                            "already_in_other_school");
                    }

                    if (user.SchoolId != schoolUserId)
                    {
                        user.AssignSchool(schoolUserId);
                    }

                    attached++;
                    results.Add(row with { Message = "Cuenta vinculada a la escuela." });
                }
            }
            catch (Exception ex)
            {
                failed++;
                results.Add(row with
                {
                    Action = ImportActions.Error,
                    Severity = "error",
                    Code = "commit_failed",
                    Message = ex is DomainException de ? de.Message : "No se pudo importar esta fila."
                });
            }
        }

        await _users.SaveChangesAsync(ct);

        if (created > 0 || attached > 0)
        {
            await _events.AddAsync(
                MembershipEvent.Create(
                    schoolUserId,
                    MembershipEventTypes.MemberImported,
                    null,
                    null,
                    schoolUserId,
                    $"Importación CSV: {created} creados, {attached} vinculados, {skipped} omitidos, {failed} fallidos ({cached.FileName})",
                    _clock.UtcNow),
                ct);
            await _profiles.SaveChangesAsync(ct);
        }

        _cache.Remove(previewId);

        var credentialsCsv = BuildCredentialsCsv(credentials);
        return new ImportCommitResultDto(
            previewId,
            created,
            attached,
            skipped,
            failed,
            credentials,
            results.Select(ToDto).ToList(),
            credentialsCsv);
    }

    private async Task EnsureMembershipAsync(int schoolUserId, CancellationToken ct)
    {
        var profile = await _profiles.GetTrackedByUserIdAsync(schoolUserId, ct);
        if (profile is null)
        {
            throw new DomainException(
                "Completa el perfil de escuela antes de importar.",
                400,
                "school_profile_required");
        }

        profile.RefreshStatus(_clock.UtcNow);
        if (!profile.CanOperateProduct(_clock.UtcNow))
        {
            throw new DomainException(
                "Tu membresía no está activa. No puedes importar usuarios.",
                400,
                "membership_inactive");
        }
    }

    private async Task<ParsedImportRow> ClassifyRowAsync(
        int schoolUserId,
        RawCsvRow raw,
        HashSet<string> seenEmails,
        Func<int> teachersBudget,
        Func<int> studentsBudget,
        Action<int> consumeTeacher,
        Action<int> consumeStudent,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw.Name))
        {
            return Error(raw, "invalid_name", "El nombre es obligatorio.");
        }

        if (raw.Name.Trim().Length > 200)
        {
            return Error(raw, "invalid_name", "El nombre es demasiado largo.");
        }

        string email;
        try
        {
            email = EmailAddress.Normalize(raw.Email);
        }
        catch (DomainException)
        {
            return Error(raw, "invalid_email", "Correo inválido.");
        }

        if (!seenEmails.Add(email))
        {
            return Error(raw, "duplicate_in_file", "Este correo está repetido en el archivo.");
        }

        string role;
        try
        {
            role = SchoolSeatGuard.ParseMemberRole(raw.Role);
        }
        catch (DomainException)
        {
            return Error(raw, "invalid_role", "Rol inválido. Usa Student o Teacher.");
        }

        var existing = await _users.FindByEmailAsync(email, ct);
        if (existing is null)
        {
            if (role == Roles.Teacher)
            {
                if (teachersBudget() <= 0)
                {
                    return Error(raw, "seat_limit_reached", "Sin cupos de docentes para esta fila.");
                }

                consumeTeacher(1);
            }
            else
            {
                if (studentsBudget() <= 0)
                {
                    return Error(raw, "seat_limit_reached", "Sin cupos de estudiantes para esta fila.");
                }

                consumeStudent(1);
            }

            return new ParsedImportRow(
                raw.LineNumber,
                raw.Name.Trim(),
                email,
                role,
                ImportActions.Create,
                "ok",
                null,
                "Se creará una cuenta nueva con contraseña temporal.");
        }

        var existingRole = Roles.Normalize(existing.Role);
        if (existingRole is not (Roles.Teacher or Roles.Student))
        {
            return Error(raw, "invalid_role", "Ese correo pertenece a una cuenta que no se puede importar.");
        }

        if (existingRole != role)
        {
            return Error(
                raw,
                "role_mismatch",
                existingRole == Roles.Teacher
                    ? "Esa cuenta ya es docente; cambia el rol en el CSV."
                    : "Esa cuenta ya es estudiante; cambia el rol en el CSV.");
        }

        if (existing.SchoolId == schoolUserId)
        {
            return new ParsedImportRow(
                raw.LineNumber,
                raw.Name.Trim(),
                email,
                role,
                ImportActions.Skip,
                "warning",
                "already_member",
                "Ya pertenece a tu escuela. Se omitirá.");
        }

        if (existing.SchoolId is not null)
        {
            return Error(raw, "already_in_other_school", "Ese correo ya está vinculado a otra escuela.");
        }

        if (!existing.IsActive)
        {
            return Error(raw, "user_inactive", "Esa cuenta está desactivada.");
        }

        if (role == Roles.Teacher)
        {
            if (teachersBudget() <= 0)
            {
                return Error(raw, "seat_limit_reached", "Sin cupos de docentes para vincular esta fila.");
            }

            consumeTeacher(1);
        }
        else
        {
            if (studentsBudget() <= 0)
            {
                return Error(raw, "seat_limit_reached", "Sin cupos de estudiantes para vincular esta fila.");
            }

            consumeStudent(1);
        }

        return new ParsedImportRow(
            raw.LineNumber,
            raw.Name.Trim(),
            email,
            role,
            ImportActions.Attach,
            "ok",
            null,
            "Se vinculará la cuenta existente a tu escuela.");
    }

    private static ParsedImportRow Error(RawCsvRow raw, string code, string message) =>
        new(
            raw.LineNumber,
            raw.Name?.Trim() ?? "",
            raw.Email?.Trim().ToLowerInvariant() ?? "",
            raw.Role?.Trim() ?? "",
            ImportActions.Error,
            "error",
            code,
            message);

    private static ImportRowPreviewDto ToDto(ParsedImportRow row) =>
        new(row.LineNumber, row.Name, row.Email, row.Role, row.Action, row.Severity, row.Code, row.Message);

    private static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        Span<char> chars = stackalloc char[12];
        chars[0] = 'A';
        chars[1] = 'a';
        chars[2] = '2';
        chars[3] = '!';
        for (var i = 4; i < chars.Length; i++)
        {
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        // shuffle tail
        for (var i = chars.Length - 1; i > 3; i--)
        {
            var j = RandomNumberGenerator.GetInt32(4, i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static string BuildCredentialsCsv(IReadOnlyList<ImportCredentialDto> credentials)
    {
        var sb = new StringBuilder();
        sb.AppendLine("nombre,email,rol,password_temporal");
        foreach (var c in credentials)
        {
            sb.Append(Escape(c.Name)).Append(',')
                .Append(Escape(c.Email)).Append(',')
                .Append(Escape(c.Role)).Append(',')
                .Append(Escape(c.TemporaryPassword)).AppendLine();
        }

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static List<RawCsvRow> ParseCsv(string text)
    {
        var lines = text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return [];
        }

        var header = SplitCsvLine(lines[0]).Select(NormalizeHeader).ToList();
        var nameIdx = IndexOf(header, "nombre", "name");
        var emailIdx = IndexOf(header, "email", "correo", "mail");
        var roleIdx = IndexOf(header, "rol", "role", "tipo");
        if (nameIdx < 0 || emailIdx < 0 || roleIdx < 0)
        {
            throw new DomainException(
                "Cabeceras inválidas. Usa: nombre,email,rol",
                400,
                "invalid_csv_headers");
        }

        var rows = new List<RawCsvRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var cols = SplitCsvLine(line);
            rows.Add(new RawCsvRow(
                i + 1,
                GetCol(cols, nameIdx),
                GetCol(cols, emailIdx),
                GetCol(cols, roleIdx)));
        }

        return rows;
    }

    private static string NormalizeHeader(string value) =>
        value.Trim().Trim('"').ToLowerInvariant();

    private static int IndexOf(IReadOnlyList<string> headers, params string[] aliases)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            if (aliases.Contains(headers[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetCol(IReadOnlyList<string> cols, int index) =>
        index >= 0 && index < cols.Count ? cols[index].Trim().Trim('"') : "";

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        result.Add(sb.ToString());
        return result;
    }

    private sealed record RawCsvRow(int LineNumber, string Name, string Email, string Role);
}

public static class ImportActions
{
    public const string Create = "create";
    public const string Attach = "attach";
    public const string Skip = "skip";
    public const string Error = "error";
}
