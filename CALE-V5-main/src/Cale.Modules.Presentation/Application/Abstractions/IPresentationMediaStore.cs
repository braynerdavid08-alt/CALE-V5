namespace Cale.Modules.Presentation.Application.Abstractions;

public interface IPresentationMediaStore
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

    Task<(byte[] Data, string ContentType, string FileName)?> TryReadLegacyDiskAsync(
        string fileName,
        CancellationToken ct = default);
}
