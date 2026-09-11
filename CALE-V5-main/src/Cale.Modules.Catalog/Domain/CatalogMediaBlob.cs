namespace Cale.Modules.Catalog.Domain;

/// <summary>
/// Question/exam image bytes in the database so they survive Render redeploys
/// (no paid persistent disk required).
/// </summary>
public sealed class CatalogMediaBlob
{
    public Guid Id { get; private set; }
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "application/octet-stream";
    public byte[] Data { get; private set; } = [];
    public int? OwnerId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private CatalogMediaBlob()
    {
    }

    public static CatalogMediaBlob Create(
        Guid id,
        string fileName,
        string contentType,
        byte[] data,
        int? ownerId,
        DateTime utcNow)
    {
        if (data.Length == 0)
        {
            throw new ArgumentException("Empty media payload.", nameof(data));
        }

        return new CatalogMediaBlob
        {
            Id = id,
            FileName = string.IsNullOrWhiteSpace(fileName) ? $"{id:N}" : fileName.Trim(),
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType.Trim(),
            Data = data,
            OwnerId = ownerId,
            CreatedAt = utcNow
        };
    }
}
