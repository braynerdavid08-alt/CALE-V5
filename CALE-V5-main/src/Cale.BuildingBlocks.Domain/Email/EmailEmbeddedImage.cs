namespace Cale.BuildingBlocks.Domain.Email;

/// <summary>Inline image for HTML emails (CID / LinkedResource / Hostinger attachment).</summary>
public sealed record EmailEmbeddedImage(
    string ContentId,
    string FileName,
    string ContentType,
    byte[] Content);
