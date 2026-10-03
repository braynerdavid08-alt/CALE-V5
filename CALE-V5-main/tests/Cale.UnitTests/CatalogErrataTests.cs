using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Catalog.Infrastructure;
using Cale.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cale.UnitTests;

public sealed class CatalogErrataTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cale-errata-{Guid.NewGuid():N}.db");

    private CaleDbContext NewDb() => new(
        new DbContextOptionsBuilder<CaleDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=False")
            .ReplaceService<IModelCacheKeyFactory, CatalogOnlyModelKey>()
            .Options,
        new MappingAssemblies(typeof(QuestionOptionConfiguration).Assembly));

    private sealed class CatalogOnlyModelKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => (context.GetType(), nameof(CatalogOnlyModelKey), designTime);
    }

    [Fact]
    public async Task Errata_fixes_the_urban_speed_limit_in_stored_questions()
    {
        var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        await using (var db = NewDb())
        {
            await db.Database.EnsureCreatedAsync();
            var bank = Bank.Create("Normas", null, now);
            var block = Block.Create("Normas");
            db.AddRange(bank, block);
            await db.SaveChangesAsync();

            db.Add(Question.Create(bank.Id, block.Id, null,
                "Si no hay señalización específica, ¿cuál es el límite general de velocidad en vías urbanas para automóviles?",
                "Seleccion multiple", "Velocidad", null,
                "En zona urbana el límite general para automóviles es 60 km/h, salvo señalización distinta.",
                [QuestionOption.Create("100 km/h si la vía es amplia.", false, null), QuestionOption.Create("60 km/h.", true, null)],
                now));
            db.Add(Question.Create(bank.Id, block.Id, null,
                "En zona escolar, cuando hay señalización de límite reducido, usted debe:",
                "Seleccion multiple", "Velocidad", null, null,
                [QuestionOption.Create("Mantener 60 km/h porque “es el máximo urbano”.", false, null), QuestionOption.Create("Respetar el límite indicado y extremar precaución.", true, null)],
                now));
            await db.SaveChangesAsync();
        }

        await using (var db = NewDb())
        {
            await CatalogSeed.ApplyErrataAsync(db, NullLogger.Instance);
            await CatalogSeed.ApplyErrataAsync(db, NullLogger.Instance);
        }

        await using (var db = NewDb())
        {
            var options = await db.Set<QuestionOption>().Select(o => o.Text).ToListAsync();
            Assert.Contains("50 km/h.", options);
            Assert.Contains("Mantener 50 km/h porque “es el máximo urbano”.", options);
            Assert.DoesNotContain(options, o => o.Contains("60 km/h"));
            var explanation = await db.Set<Question>().Where(q => q.Topic == "Velocidad" && q.Explanation != null).Select(q => q.Explanation).SingleAsync();
            Assert.Contains("50 km/h", explanation);
        }
    }

    public void Dispose()
    {
        try { File.Delete(_path); } catch (IOException) { }
    }
}
