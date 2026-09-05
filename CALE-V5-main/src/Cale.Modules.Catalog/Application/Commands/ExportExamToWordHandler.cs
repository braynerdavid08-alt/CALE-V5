using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Catalog.Application.Abstractions;

namespace Cale.Modules.Catalog.Application.Commands;

public sealed class ExportExamToWordHandler
{
    private readonly ICatalogStore _store;

    public ExportExamToWordHandler(ICatalogStore store) => _store = store;

    public async Task<(byte[] Bytes, string FileName)> HandleAsync(
        int examId,
        int userId,
        bool isAdmin,
        CancellationToken ct)
    {
        var exam = await _store.GetExamAsync(examId, ct)
            ?? throw new DomainException("Examen no encontrado.", 404, "exam_not_found");

        if (!isAdmin && exam.CreatedById != userId)
        {
            throw new DomainException("No puedes exportar este examen.", 403, "forbidden");
        }

        if (exam.BankId is null)
        {
            throw new DomainException(
                "Este examen no tiene banco de preguntas para exportar. Impórtalo desde Word o asígnale un banco.",
                400,
                "exam_without_bank");
        }

        var questions = await _store.ListActiveQuestionsInBankAsync(exam.BankId.Value, ct);
        if (questions.Count == 0)
        {
            throw new DomainException(
                "El banco del examen no tiene preguntas activas.",
                400,
                "exam_empty_bank");
        }

        var exportQs = questions
            .OrderBy(q => q.Id)
            .Select((q, i) =>
            {
                var opts = q.Options
                    .OrderBy(o => o.Id)
                    .Select((o, oi) => new ExamWordExportOption(
                        (char)('A' + oi),
                        o.Text,
                        o.IsCorrect))
                    .ToList();
                return new ExamWordExportQuestion(i + 1, q.Text, opts);
            })
            .ToList();

        var bytes = ExamWordImportParser.BuildExportDocx(exam.Name, exportQs);
        var safe = string.Join("_", exam.Name.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = $"examen-{exam.Id}";
        }

        return (bytes, $"{safe}.docx");
    }
}
