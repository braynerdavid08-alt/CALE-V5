using System.Reflection;

namespace Cale.BuildingBlocks.Infrastructure.Persistence;

public sealed class MappingAssemblies
{
    public MappingAssemblies(params Assembly[] assemblies)
    {
        Assemblies = assemblies;
    }

    public Assembly[] Assemblies { get; }
}
