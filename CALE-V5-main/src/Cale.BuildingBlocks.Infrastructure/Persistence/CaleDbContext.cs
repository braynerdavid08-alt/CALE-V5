using Microsoft.EntityFrameworkCore;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

public sealed class CaleDbContext : DbContext
{
    private readonly MappingAssemblies _mappings;

    public CaleDbContext(
        DbContextOptions<CaleDbContext> options,
        MappingAssemblies mappings)
        : base(options)
    {
        _mappings = mappings;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var assembly in _mappings.Assemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }
}
