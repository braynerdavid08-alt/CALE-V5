using System.Text.Json;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.Catalog.Infrastructure;

public static class CatalogSeed
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static async Task EnsureOfficialBanksAsync(
        CaleDbContext db,
        string? seedDirectory,
        IClock clock,
        int? createdById,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(seedDirectory) || !Directory.Exists(seedDirectory))
        {
            logger.LogWarning("Catalog seed data folder not found; skipping bank import.");
            return;
        }

        await ImportBankFileAsync(
            db,
            clock,
            logger,
            createdById,
            Path.Combine(seedDirectory, "banco-normas-transito.json"),
            ct);

        await ImportBankFileAsync(
            db,
            clock,
            logger,
            createdById,
            Path.Combine(seedDirectory, "banco-senales-reconocimiento.json"),
            ct);

        await ImportBankFileAsync(
            db,
            clock,
            logger,
            createdById,
            Path.Combine(seedDirectory, "banco-senales-accion.json"),
            ct);
    }

    private static async Task ImportBankFileAsync(
        CaleDbContext db,
        IClock clock,
        ILogger logger,
        int? createdById,
        string path,
        CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            logger.LogWarning("Seed file missing: {Path}", path);
            return;
        }

        await using var stream = File.OpenRead(path);
        var payload = await JsonSerializer.DeserializeAsync<SeedBankFile>(stream, JsonOptions, ct);
        if (payload is null ||
            string.IsNullOrWhiteSpace(payload.BankName) ||
            payload.Questions.Count == 0)
        {
            logger.LogWarning("Invalid seed file: {Path}", path);
            return;
        }

        var bank = await db.Set<Bank>()
            .FirstOrDefaultAsync(x => x.Name == payload.BankName, ct);

        if (bank is null)
        {
            bank = Bank.Create(payload.BankName, payload.Description, clock.UtcNow);
            await db.Set<Bank>().AddAsync(bank, ct);
            await db.SaveChangesAsync(ct);
        }
        else if (payload.ReplaceExisting)
        {
            var old = await db.Set<Question>()
                .Where(x => x.BankId == bank.Id)
                .ToListAsync(ct);
            if (old.Count > 0)
            {
                logger.LogInformation(
                    "Replacing bank '{Name}' seed ({Count} old questions → {New}).",
                    bank.Name,
                    old.Count,
                    payload.Questions.Count);
                db.Set<Question>().RemoveRange(old);
                await db.SaveChangesAsync(ct);
            }

            if (!string.IsNullOrWhiteSpace(payload.Description))
            {
                bank.Update(payload.BankName, payload.Description);
            }

            bank.SetActive(true);
        }
        else if (bank.SeedCompleted)
        {
            var existing = await db.Set<Question>().CountAsync(x => x.BankId == bank.Id, ct);
            if (existing >= payload.Questions.Count)
            {
                logger.LogInformation(
                    "Bank '{Name}' already seeded ({Count} questions).",
                    bank.Name,
                    existing);
                return;
            }
        }

        var blockName = string.IsNullOrWhiteSpace(payload.BlockName)
            ? payload.BankName
            : payload.BlockName.Trim();
        var block = await db.Set<Block>()
            .FirstOrDefaultAsync(x => x.Name == blockName, ct);
        if (block is null)
        {
            block = Block.Create(blockName);
            await db.Set<Block>().AddAsync(block, ct);
            await db.SaveChangesAsync(ct);
        }

        var already = await db.Set<Question>().CountAsync(x => x.BankId == bank.Id, ct);
        if (already > 0 && already < payload.Questions.Count)
        {
            logger.LogWarning(
                "Bank '{Name}' has partial seed ({Have}/{Need}). Rebuilding questions.",
                bank.Name,
                already,
                payload.Questions.Count);
            var old = await db.Set<Question>()
                .Where(x => x.BankId == bank.Id)
                .ToListAsync(ct);
            db.Set<Question>().RemoveRange(old);
            await db.SaveChangesAsync(ct);
        }
        else if (already >= payload.Questions.Count)
        {
            if (!bank.SeedCompleted)
            {
                bank.MarkSeedCompleted();
                await db.SaveChangesAsync(ct);
            }

            return;
        }

        var now = clock.UtcNow;
        var batch = 0;
        foreach (var item in payload.Questions)
        {
            var options = item.Options
                .Select(o => QuestionOption.Create(o.Text, o.IsCorrect, o.ImageUrl))
                .ToList();

            var question = Question.Create(
                bank.Id,
                block.Id,
                createdById,
                item.Text,
                item.Type,
                item.Topic,
                item.ImageUrl,
                item.Explanation,
                options,
                now);
            question.SetCatalogMeta(
                item.Subject,
                item.Topic,
                item.Subtopic,
                item.Difficulty,
                item.Source);
            question.SetActive(true);
            await db.Set<Question>().AddAsync(question, ct);
            batch++;
            if (batch % 100 == 0)
            {
                await db.SaveChangesAsync(ct);
            }
        }

        bank.MarkSeedCompleted();
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seeded bank '{Name}' with {Count} questions.",
            bank.Name,
            payload.Questions.Count);
    }

    private sealed class SeedBankFile
    {
        public string BankName { get; set; } = "";
        public string? Description { get; set; }
        public string? BlockName { get; set; }
        public bool ReplaceExisting { get; set; }
        public List<SeedQuestion> Questions { get; set; } = [];
    }

    private sealed class SeedQuestion
    {
        public string Text { get; set; } = "";
        public string Type { get; set; } = "Seleccion multiple";
        public string? Subject { get; set; }
        public string? Topic { get; set; }
        public string? Subtopic { get; set; }
        public string? Difficulty { get; set; }
        public string? ImageUrl { get; set; }
        public string? Explanation { get; set; }
        public string? Source { get; set; }
        public List<SeedOption> Options { get; set; } = [];
    }

    private sealed class SeedOption
    {
        public string Text { get; set; } = "";
        public bool IsCorrect { get; set; }
        public string? ImageUrl { get; set; }
    }
}
