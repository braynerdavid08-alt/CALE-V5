using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Catalog.Domain;

public sealed class Block
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "";

    private Block()
    {
    }

    public static Block Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Block name is required.", 400, "invalid_name");
        }

        return new Block { Name = name.Trim() };
    }
}
