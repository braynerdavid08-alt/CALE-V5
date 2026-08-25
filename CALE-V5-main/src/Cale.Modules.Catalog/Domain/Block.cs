namespace Cale.Modules.Catalog.Domain;

public sealed class Block
{
    public int Id { get; private set; }
    public string Name { get; private set; } = "";

    private Block()
    {
    }
}
