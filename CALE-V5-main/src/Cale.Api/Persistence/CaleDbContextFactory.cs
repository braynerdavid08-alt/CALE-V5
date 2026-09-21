using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Infrastructure.Persistence;
using Cale.Modules.Catalog.Infrastructure.Persistence;
using Cale.Modules.Classroom.Infrastructure.Persistence;
using Cale.Modules.Engagement.Infrastructure.Persistence;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Cale.Modules.LiveClassroom.Infrastructure.Persistence;
using Cale.Modules.Presentation.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cale.Api.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef migrations</c>.
/// Authoritative provider is PostgreSQL (production).
/// </summary>
public sealed class CaleDbContextFactory : IDesignTimeDbContextFactory<CaleDbContext>
{
    public CaleDbContext CreateDbContext(string[] args)
    {
        var mappings = new MappingAssemblies(
            typeof(UserConfiguration).Assembly,
            typeof(BankConfiguration).Assembly,
            typeof(AttemptConfiguration).Assembly,
            typeof(GroupConfiguration).Assembly,
            typeof(NotificationConfiguration).Assembly,
            typeof(PresentationDeckConfiguration).Assembly,
            typeof(LiveSessionConfiguration).Assembly,
            typeof(TheoryTopicConfiguration).Assembly);

        var options = new DbContextOptionsBuilder<CaleDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Database=cale_design;Username=postgres;Password=postgres",
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(CaleDbContextFactory).Assembly.GetName().Name);
                    npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
            .Options;

        return new CaleDbContext(options, mappings);
    }
}
