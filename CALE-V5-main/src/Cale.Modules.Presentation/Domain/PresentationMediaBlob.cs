namespace Cale.Modules.Presentation.Domain;

/// <summary>Presentation image/video bytes stored in PostgreSQL (survives Render redeploys without paid disk).</summary>
public sealed class PresentationMediaBlob
{
    public Guid Id { get; private set; }
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "application/octet-stream";
    public byte[] Data { get; private set; } = [];
    public int? OwnerId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PresentationMediaBlob()
    {
    }

    public static PresentationMediaBlob Create(
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

        return new PresentationMediaBlob
        {
            Id = id,
            FileName = string.IsNullOrWhiteSpace(fileName) ? $"{id:N}" : fileName.Trim(),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            Data = data,
            OwnerId = ownerId,
            CreatedAt = utcNow
        };
    }
}
