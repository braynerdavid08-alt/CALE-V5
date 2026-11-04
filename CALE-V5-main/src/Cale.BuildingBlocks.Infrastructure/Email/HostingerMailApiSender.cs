using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Privacy;
using Cale.BuildingBlocks.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cale.BuildingBlocks.Infrastructure.Email;

/// <summary>
/// Sends mail via Hostinger Mail API over HTTPS (works on Render Free; SMTP ports blocked).
/// Contract: POST /api/v1/mailboxes/{mailboxResourceId}/send → 204 on success.
/// </summary>
public sealed class HostingerMailApiSender : IEmailSender
{
    public const string HttpClientName = "hostinger-mail";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmailOptions _options;
    private readonly ILogger<HostingerMailApiSender> _logger;

    public HostingerMailApiSender(
        IHttpClientFactory httpClientFactory,
        IOptions<EmailOptions> options,
        ILogger<HostingerMailApiSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        _options.Enabled && _options.HostingerMail.IsComplete;

    public async Task SendAsync(
        string toEmail,
        string subject,
        string plainTextBody,
        CancellationToken ct = default,
        string? htmlBody = null,
        IReadOnlyList<EmailEmbeddedImage>? embeddedImages = null)
    {
        if (!IsConfigured)
        {
            throw new DomainException(
                "Hostinger Mail API is not configured.",
                503,
                "email_delivery_misconfigured");
        }

        var mail = _options.HostingerMail;
        var path = $"api/v1/mailboxes/{Uri.EscapeDataString(mail.MailboxResourceId.Trim())}/send";
        var html = string.IsNullOrWhiteSpace(htmlBody) ? null : htmlBody;
        var attachments = MapAttachments(embeddedImages);
        var payload = new HostingerSendRequest(
            To: [toEmail],
            DisplayName: string.IsNullOrWhiteSpace(_options.FromName) ? null : _options.FromName.Trim(),
            Subject: subject,
            Text: plainTextBody,
            Html: html,
            Attachments: attachments);

        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mail.ApiToken.Trim());
        request.Content = JsonContent.Create(payload, options: JsonOpts);

        var approxBytes = System.Text.Encoding.UTF8.GetByteCount(subject)
            + System.Text.Encoding.UTF8.GetByteCount(plainTextBody ?? "")
            + System.Text.Encoding.UTF8.GetByteCount(html ?? "")
            + (embeddedImages?.Sum(i => i.Content.Length) ?? 0);

        var http = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode)
            {
                OutboundTraffic.Record("hostinger-mail", approxBytes, 0);
                _logger.LogInformation(
                    "Hostinger Mail API sent email to={To} status={Status}",
                    PersonalData.MaskEmail(toEmail),
                    (int)response.StatusCode);
                return;
            }

            OutboundTraffic.Record("hostinger-mail", approxBytes, 0, failed: true);
            _logger.LogError(
                "Hostinger Mail API failed to={To} status={Status}",
                PersonalData.MaskEmail(toEmail),
                (int)response.StatusCode);

            throw new DomainException(
                "No pudimos enviar el correo de verificación. Intenta reenviar el código en unos minutos.",
                503,
                "email_send_failed");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            OutboundTraffic.Record("hostinger-mail", approxBytes, 0, failed: true);
            _logger.LogError(ex, "Hostinger Mail API timeout to={To}", PersonalData.MaskEmail(toEmail));
            throw new DomainException(
                "El envío de correo tardó demasiado. Intenta reenviar el código.",
                503,
                "email_send_failed");
        }
        catch (HttpRequestException ex)
        {
            OutboundTraffic.Record("hostinger-mail", approxBytes, 0, failed: true);
            _logger.LogError(ex, "Hostinger Mail API network error to={To}", PersonalData.MaskEmail(toEmail));
            throw new DomainException(
                "No pudimos enviar el correo de verificación. Intenta reenviar el código en unos minutos.",
                503,
                "email_send_failed");
        }
    }

    private static IReadOnlyList<HostingerAttachment>? MapAttachments(
        IReadOnlyList<EmailEmbeddedImage>? embeddedImages)
    {
        if (embeddedImages is null || embeddedImages.Count == 0)
        {
            return null;
        }

        return embeddedImages
            .Select(img => new HostingerAttachment(
                Filename: img.FileName,
                Content: Convert.ToBase64String(img.Content),
                ContentType: img.ContentType,
                Cid: img.ContentId,
                Encoding: "base64"))
            .ToArray();
    }

    private sealed record HostingerSendRequest(
        [property: JsonPropertyName("to")] IReadOnlyList<string> To,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("html")] string? Html,
        [property: JsonPropertyName("attachments")] IReadOnlyList<HostingerAttachment>? Attachments);

    private sealed record HostingerAttachment(
        [property: JsonPropertyName("filename")] string Filename,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("contentType")] string ContentType,
        [property: JsonPropertyName("cid")] string Cid,
        [property: JsonPropertyName("encoding")] string Encoding);
}
