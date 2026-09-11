namespace Cale.Modules.Catalog.Application.Abstractions;

public interface ICatalogMediaStore
{
    string BuildPublicUrl(Guid id);

    Task<string> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        int? ownerId,
        CancellationToken ct = default);

    Task<(byte[] Data, string ContentType, string FileName)?> ReadAsync(
        Guid id,
        CancellationToken ct = default);

    /// <summary>Serve pre-DB uploads still present on disk under /uploads/.</summary>
    Task<(byte[] Data, string ContentType, string FileName)?> TryReadLegacyDiskAsync(
        string fileName,
        CancellationToken ct = default);
}
