using Microsoft.Extensions.Options;

namespace Cale.Modules.TheoreticalTraining.Application.WhatsApp;

/// <summary>
/// Never performs HTTP to Meta Graph. Used while SchedulingReports:WhatsApp is off.
/// </summary>
public sealed class DisabledWhatsAppCloudSender : IWhatsAppCloudSender
{
    private readonly SchedulingReportsOptions _options;

    public DisabledWhatsAppCloudSender(IOptions<SchedulingReportsOptions> options) =>
        _options = options.Value;

    public Task<WhatsAppSendResult> SendDocumentAsync(
        WhatsAppDocumentSendRequest request,
        CancellationToken ct = default)
    {
        _ = request;
        _ = ct;

        if (!_options.WhatsApp.IsConfigured || !_options.AllowManualWhatsAppSend || !_options.Enabled)
        {
            return Task.FromResult(new WhatsAppSendResult(
                false,
                "NotConfigured",
                null,
                "WhatsApp Cloud API no está activado. Pendiente de activación por Luz Verde."));
        }

        // Even if flags flip on accidentally without a real client, refuse rather than call Graph.
        return Task.FromResult(new WhatsAppSendResult(
            false,
            "NotConfigured",
            null,
            "El cliente de WhatsApp Cloud API aún no está implementado. No se realizó ninguna llamada a Meta."));
    }
}
