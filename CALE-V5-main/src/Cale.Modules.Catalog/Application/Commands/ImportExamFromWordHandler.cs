using Cale.BuildingBlocks.Domain.Catalog;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Application.DTOs;
using Cale.Modules.Catalog.Domain;

namespace Cale.Modules.Catalog.Application.Commands;

public sealed class ImportExamFromWordHandler
{
    private readonly ICatalogStore _store;
    private readonly IClock _clock;

    public ImportExamFromWordHandler(ICatalogStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<ImportExamResultDto> HandleAsync(
        Stream file,
        string? title,
        int userId,
        CancellationToken ct)
    {
        ParsedExamDocument parsed;
        try
        {
            parsed = ExamWordImportParser.Parse(file);
        }
        catch (Exception ex)
        {
            throw new DomainException(
                $"No se pudo leer el Word: {ex.Message}",
                400,
                "invalid_exam_word");
        }

        if (parsed.Questions.Count == 0)
        {
            throw new DomainException(
                "No se encontraron preguntas con opciones A–D. Usa la plantilla Word de Mi CALE.",
                400,
                "empty_exam_import");
        }

        var bankName = string.IsNullOrWhiteSpace(title)
            ? $"Importado {DateTime.Now:yyyy-MM-dd HH:mm}"
            : title.Trim();
        if (bankName.Length > 180)
        {
            bankName = bankName[..180];
        }

        var bank = Bank.Create(
            bankName,
            "Banco creado al importar un examen Word desde el perfil del instructor.",
            _clock.UtcNow);
        await _store.AddBankAsync(bank, ct);
        await _store.SaveChangesAsync(ct);

        var block = await _store.GetBlockByNameAsync("Importado Word", ct);
        if (block is null)
        {
            block = Block.Create("Importado Word");
            await _store.AddBlockAsync(block, ct);
            await _store.SaveChangesAsync(ct);
        }

        var imported = 0;
        var reviewNeeded = 0;
        foreach (var item in parsed.Questions)
        {
            var options = item.Options
                .Select(o => QuestionOption.Create(o.Text, o.IsCorrect, null))
                .ToList();
            var question = Question.Create(
                bank.Id,
                block.Id,
                userId,
                item.Text,
                QuestionTypes.MultipleChoice,
                topic: $"Pregunta {item.Number}",
                imageUrl: null,
                explanation: item.NeedsCorrectReview
                    ? ExamImportMarkers.NeedsReviewExplanation
                    : null,
                options,
                _clock.UtcNow);
            await _store.AddQuestionAsync(question, ct);
            imported++;
            if (item.NeedsCorrectReview)
            {
                reviewNeeded++;
            }
        }

        await _store.SaveChangesAsync(ct);

        var exam = Exam.Create(
            bankName,
            reviewNeeded > 0
                ? $"Importado desde Word. {reviewNeeded} pregunta(s) sin clave (*letra o RESPUESTAS): revisa antes de publicar."
                : "Importado desde Word.",
            bank.Id,
            imported,
            timeMinutes: Math.Clamp(imported, 20, 90),
            allowedAttempts: 1,
            randomize: true,
            userId,
            startsAt: null,
            endsAt: null,
            _clock.UtcNow);
        await _store.AddExamAsync(exam, ct);
        await _store.SaveChangesAsync(ct);

        return new ImportExamResultDto(
            exam.Id,
            bank.Id,
            bankName,
            imported,
            reviewNeeded,
            parsed.Skipped.Count,
            parsed.Skipped.Take(12).ToList());
    }
}
