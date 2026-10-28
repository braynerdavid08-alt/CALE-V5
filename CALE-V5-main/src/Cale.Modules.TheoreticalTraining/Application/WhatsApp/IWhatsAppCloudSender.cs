namespace Cale.Modules.TheoreticalTraining.Application.WhatsApp;

public sealed record WhatsAppDocumentSendRequest(
    string ToE164,
    string FileName,
    byte[] DocumentBytes,
    string? Caption);

public sealed record WhatsAppSendResult(
    bool Success,
    string Status,
    string? MessageId,
    string? Error);

/// <summary>
/// Meta WhatsApp Cloud API sender. Phase 1 ships only a disabled stub that never calls Graph.
/// </summary>
public interface IWhatsAppCloudSender
{
    Task<WhatsAppSendResult> SendDocumentAsync(
        WhatsAppDocumentSendRequest request,
        CancellationToken ct = default);
}
