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
    private readonly ICatalogMediaStore _media;
    private readonly IClock _clock;

    public ImportExamFromWordHandler(
        ICatalogStore store,
        ICatalogMediaStore media,
        IClock clock)
    {
        _store = store;
        _media = media;
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
            _clock.UtcNow,
            userId);
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
        var imagesAttached = 0;
        foreach (var item in parsed.Questions)
        {
            var options = new List<QuestionOption>();
            foreach (var o in item.Options)
            {
                string? optionUrl = null;
                if (o.Image is not null)
                {
                    optionUrl = await SaveImageAsync(o.Image, userId, ct);
                    imagesAttached++;
                }

                options.Add(QuestionOption.Create(o.Text, o.IsCorrect, optionUrl));
            }

            string? questionUrl = null;
            if (item.Image is not null)
            {
                questionUrl = await SaveImageAsync(item.Image, userId, ct);
                imagesAttached++;
            }

            var question = Question.Create(
                bank.Id,
                block.Id,
                userId,
                item.Text,
                QuestionTypes.MultipleChoice,
                topic: $"Pregunta {item.Number}",
                imageUrl: questionUrl,
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
            parsed.Skipped.Take(80).ToList(),
            imagesAttached);
    }

    private async Task<string> SaveImageAsync(
        ParsedExamImage image,
        int userId,
        CancellationToken ct)
    {
        await using var stream = new MemoryStream(image.Data, writable: false);
        return await _media.SaveAsync(
            stream,
            image.FileName,
            image.ContentType,
            userId,
            ct);
    }
}
