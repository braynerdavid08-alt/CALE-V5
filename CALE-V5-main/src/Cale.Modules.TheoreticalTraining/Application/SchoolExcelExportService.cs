using System.Globalization;
using ClosedXML.Excel;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed class SchoolExcelExportService
{
    private readonly ApprenticeRegistryService _registry;
    private readonly TheoryTrainingService _theory;
    private readonly PracticalTrainingService _practical;
    private readonly CaleDbContext _db;
    private readonly IUserStore _users;

    public SchoolExcelExportService(
        ApprenticeRegistryService registry,
        TheoryTrainingService theory,
        PracticalTrainingService practical,
        CaleDbContext db,
        IUserStore users)
    {
        _registry = registry;
        _theory = theory;
        _practical = practical;
        _db = db;
        _users = users;
    }

    public async Task<(byte[] Bytes, string FileName)> ExportApprenticesAsync(
        int schoolUserId,
        string? search,
        string? month,
        bool? withBalance,
        CancellationToken ct)
    {
        var apprentices = await _registry.ListAsync(schoolUserId, search, month, withBalance, ct);
        var enrollments = await _theory.ListEnrollmentsAsync(schoolUserId, ct);
        var eligibilityByStudent = enrollments
            .Where(e => e.PracticalEligibility is not null)
            .GroupBy(e => e.StudentUserId)
            .ToDictionary(g => g.Key, g => g.First().PracticalEligibility!);

        using var wb = new XLWorkbook();
        var sheet = wb.Worksheets.Add("Aprendices");
        var headers = new[]
        {
            "NOMBRES",
            "TIPO DOCUMENTO",
            "NUMERO DOCUMENTO",
            "CORREO",
            "CELULAR",
            "MES MATRICULA",
            "CATEGORIA",
            "ENROLADO",
            "VALOR A PAGAR",
            "VALOR PAGADO",
            "SALDO PENDIENTE",
            "HORAS TEORIA",
            "HORAS TALLER",
            "EXAMEN TEORICO",
            "AUTH EXAMEN",
            "AUTH MANEJO",
            "CLASES MANEJO",
            "ESTADO INSCRIPCION"
        };

        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
            sheet.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var a in apprentices.OrderBy(x => x.StudentName, StringComparer.OrdinalIgnoreCase))
        {
            eligibilityByStudent.TryGetValue(a.StudentUserId, out var pe);
            ApprenticePracticalSummaryDto? practical = null;
            try
            {
                practical = await _practical.GetApprenticePracticalSummaryAsync(
                    schoolUserId,
                    a.StudentUserId,
                    ct);
            }
            catch
            {
                // Practical tables may be unavailable; export still useful without lesson counts.
            }

            sheet.Cell(row, 1).Value = a.StudentName;
            sheet.Cell(row, 2).Value = a.DocumentType ?? "";
            sheet.Cell(row, 3).Value = a.DocumentNumber ?? "";
            sheet.Cell(row, 4).Value = a.StudentEmail ?? a.ContactEmail ?? "";
            sheet.Cell(row, 5).Value = a.Phone ?? "";
            sheet.Cell(row, 6).Value = a.EnrollmentMonth ?? "";
            sheet.Cell(row, 7).Value = a.LicenseCategories ?? "";
            sheet.Cell(row, 8).Value = a.IsEnrolled ? "SI" : "NO";
            sheet.Cell(row, 9).Value = a.AmountDue;
            sheet.Cell(row, 10).Value = a.AmountPaid;
            sheet.Cell(row, 11).Value = a.BalanceDue;
            sheet.Cell(row, 12).Value = pe is null
                ? ""
                : $"{pe.TheoryHoursCompleted}/{pe.TheoryHoursRequired}";
            sheet.Cell(row, 13).Value = pe is null
                ? ""
                : $"{pe.WorkshopHoursCompleted}/{pe.WorkshopHoursRequired}";
            sheet.Cell(row, 14).Value = pe is null
                ? ""
                : pe.TheoryExamPassed
                    ? "APROBADO"
                    : "PENDIENTE";
            sheet.Cell(row, 15).Value = a.TheoryExamAuthorized || (pe?.TheoryExamAuthorized ?? false)
                ? "SI"
                : "NO";
            sheet.Cell(row, 16).Value = a.PracticalAuthorized || (pe?.PracticalAuthorized ?? false)
                ? "SI"
                : "NO";
            sheet.Cell(row, 17).Value = practical is null
                ? ""
                : $"{practical.CompletedLessons}/{practical.RequiredLessons}";
            sheet.Cell(row, 18).Value = a.EnrollmentStatus;
            row++;
        }

        sheet.Columns().AdjustToContents();
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return (ToBytes(wb), $"cale-aprendices-{stamp}.xlsx");
    }

    public async Task<(byte[] Bytes, string FileName)> ExportPaymentsAsync(
        int schoolUserId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(-3));
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

        if (end < start)
        {
            throw new DomainException("La fecha final debe ser mayor o igual a la inicial.", 400, "invalid_date_range");
        }

        var abonos = await _db.Set<ApprenticePaymentAbono>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.PaymentDate >= start
                && x.PaymentDate <= end)
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var studentIds = abonos.Select(x => x.StudentUserId).Distinct().ToList();
        var names = new Dictionary<int, string>();
        foreach (var id in studentIds)
        {
            names[id] = (await _users.GetByIdAsync(id, ct))?.Name ?? $"Estudiante {id}";
        }

        var apprentices = await _registry.ListAsync(schoolUserId, null, null, withBalance: true, ct);

        using var wb = new XLWorkbook();
        var ledger = wb.Worksheets.Add("Abonos");
        var ledgerHeaders = new[]
        {
            "FECHA",
            "ESTUDIANTE",
            "MONTO",
            "METODO",
            "RECIBO",
            "TIPO",
            "NOTAS",
            "REGISTRADO"
        };
        for (var c = 0; c < ledgerHeaders.Length; c++)
        {
            ledger.Cell(1, c + 1).Value = ledgerHeaders[c];
            ledger.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var a in abonos)
        {
            ledger.Cell(row, 1).Value = a.PaymentDate.ToString("yyyy-MM-dd");
            ledger.Cell(row, 2).Value = names.GetValueOrDefault(a.StudentUserId, $"Estudiante {a.StudentUserId}");
            ledger.Cell(row, 3).Value = a.Amount;
            ledger.Cell(row, 4).Value = a.PaymentMethod ?? "";
            ledger.Cell(row, 5).Value = a.ReceiptNumber ?? "";
            ledger.Cell(row, 6).Value = a.Kind;
            ledger.Cell(row, 7).Value = a.Notes ?? "";
            ledger.Cell(row, 8).Value = a.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            row++;
        }

        ledger.Columns().AdjustToContents();

        var balances = wb.Worksheets.Add("Saldos pendientes");
        var balanceHeaders = new[]
        {
            "ESTUDIANTE",
            "DOCUMENTO",
            "VALOR A PAGAR",
            "PAGADO",
            "SALDO",
            "CORREO",
            "CELULAR"
        };
        for (var c = 0; c < balanceHeaders.Length; c++)
        {
            balances.Cell(1, c + 1).Value = balanceHeaders[c];
            balances.Cell(1, c + 1).Style.Font.Bold = true;
        }

        row = 2;
        foreach (var a in apprentices
            .Where(x => x.BalanceDue > 0)
            .OrderByDescending(x => x.BalanceDue))
        {
            balances.Cell(row, 1).Value = a.StudentName;
            balances.Cell(row, 2).Value = a.DocumentNumber ?? "";
            balances.Cell(row, 3).Value = a.AmountDue;
            balances.Cell(row, 4).Value = a.AmountPaid;
            balances.Cell(row, 5).Value = a.BalanceDue;
            balances.Cell(row, 6).Value = a.StudentEmail ?? "";
            balances.Cell(row, 7).Value = a.Phone ?? "";
            row++;
        }

        balances.Columns().AdjustToContents();

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return (ToBytes(wb), $"cale-cartera-{stamp}.xlsx");
    }

    private static byte[] ToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
