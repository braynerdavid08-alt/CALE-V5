using System.Net;
using System.Text.Json;
using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cale.UnitTests.Email;

public sealed class HostingerMailApiSenderTests
{
    [Fact]
    public void IsConfigured_requires_enabled_token_and_mailbox()
    {
        var sender = CreateSender(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent)), complete: false);
        Assert.False(sender.IsConfigured);

        var ok = CreateSender(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent)), complete: true);
        Assert.True(ok.IsConfigured);
    }

    [Fact]
    public async Task SendAsync_posts_bearer_json_and_accepts_204()
    {
        var handler = new RecordingHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
            Assert.Equal("test-token-secret", req.Headers.Authorization?.Parameter);
            Assert.Equal("application/json", req.Content?.Headers.ContentType?.MediaType);
            Assert.Contains("api/v1/mailboxes/ACtestmailbox/send", req.RequestUri!.ToString(), StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        const string subject = "Código de verificación — Luz Verde";
        const string text = "Tu código es: 123456";
        var sender = CreateSender(handler, complete: true);
        await sender.SendAsync("user@example.com", subject, text, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(handler.LastBody));
        // Parse JSON (avoids brittle Unicode/em-dash substring matches) and assert Hostinger V1.Send.Request fields.
        using var doc = JsonDocument.Parse(handler.LastBody!);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.GetProperty("to").ValueKind);
        Assert.Equal("user@example.com", root.GetProperty("to")[0].GetString());
        Assert.Equal("Luz Verde", root.GetProperty("displayName").GetString());
        Assert.Equal(subject, root.GetProperty("subject").GetString());
        Assert.Equal(text, root.GetProperty("text").GetString());
        Assert.False(root.TryGetProperty("html", out _));
        Assert.DoesNotContain("test-token-secret", handler.LastBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_includes_html_when_provided()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sender = CreateSender(handler, complete: true);
        const string html = "<p>Tu código es <strong>123456</strong></p>";

        await sender.SendAsync(
            "user@example.com",
            "Código de verificación – Luz Verde",
            "Tu código es: 123456",
            CancellationToken.None,
            html);

        using var doc = JsonDocument.Parse(handler.LastBody!);
        var root = doc.RootElement;
        Assert.Equal("Tu código es: 123456", root.GetProperty("text").GetString());
        Assert.Equal(html, root.GetProperty("html").GetString());
    }

    [Fact]
    public async Task SendAsync_includes_cid_logo_attachment()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sender = CreateSender(handler, complete: true);
        var logo = new EmailEmbeddedImage(
            "luzverde-logo",
            "luz-verde-logo.png",
            "image/png",
            [1, 2, 3, 4]);

        await sender.SendAsync(
            "user@example.com",
            "Asunto",
            "texto",
            CancellationToken.None,
            "<img src=\"cid:luzverde-logo\" />",
            [logo]);

        using var doc = JsonDocument.Parse(handler.LastBody!);
        var att = doc.RootElement.GetProperty("attachments")[0];
        Assert.Equal("luz-verde-logo.png", att.GetProperty("filename").GetString());
        Assert.Equal("luzverde-logo", att.GetProperty("cid").GetString());
        Assert.Equal("image/png", att.GetProperty("contentType").GetString());
        Assert.Equal("base64", att.GetProperty("encoding").GetString());
        Assert.Equal(Convert.ToBase64String([1, 2, 3, 4]), att.GetProperty("content").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_non_success_throws_email_send_failed(HttpStatusCode status)
    {
        var sender = CreateSender(new RecordingHandler(_ => new HttpResponseMessage(status)), complete: true);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            sender.SendAsync("user@example.com", "Asunto", "cuerpo", CancellationToken.None));
        Assert.Equal("email_send_failed", ex.ErrorCode);
        Assert.Equal(503, ex.StatusCode);
    }

    [Fact]
    public async Task SendAsync_network_error_maps_to_email_send_failed()
    {
        var sender = CreateSender(new RecordingHandler(_ => throw new HttpRequestException("network down")), complete: true);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            sender.SendAsync("user@example.com", "Asunto", "cuerpo", CancellationToken.None));
        Assert.Equal("email_send_failed", ex.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_respects_cancellation()
    {
        var sender = CreateSender(
            new RecordingHandler(async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }),
            complete: true);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sender.SendAsync("user@example.com", "Asunto", "cuerpo", cts.Token));
    }

    [Fact]
    public async Task SendAsync_when_incomplete_throws_misconfigured()
    {
        var sender = CreateSender(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent)), complete: false);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            sender.SendAsync("user@example.com", "Asunto", "cuerpo", CancellationToken.None));
        Assert.Equal("email_delivery_misconfigured", ex.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_logs_do_not_include_token_or_verification_body()
    {
        var logger = new CapturingLogger();
        var sender = CreateSender(
            new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent)),
            complete: true,
            logger);

        await sender.SendAsync("user@example.com", "Código", "Tu código es: 654321", CancellationToken.None);

        var joined = string.Join('\n', logger.Messages);
        Assert.DoesNotContain("test-token-secret", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("654321", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("Tu código es", joined, StringComparison.Ordinal);
    }

    private static HostingerMailApiSender CreateSender(
        HttpMessageHandler handler,
        bool complete,
        ILogger<HostingerMailApiSender>? logger = null)
    {
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            FromName = "Luz Verde",
            HostingerMail = new HostingerMailOptions
            {
                ApiToken = complete ? "test-token-secret" : "",
                MailboxResourceId = complete ? "ACtestmailbox" : "",
                BaseUrl = "https://api.mail.hostinger.com",
                TimeoutSeconds = 15
            }
        });

        var factory = new FixedHttpClientFactory(handler, "https://api.mail.hostinger.com/");
        return new HostingerMailApiSender(
            factory,
            options,
            logger ?? NullLogger<HostingerMailApiSender>.Instance);
    }

    private sealed class FixedHttpClientFactory(HttpMessageHandler handler, string baseAddress) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri(baseAddress)
            };
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            : this((req, _) => Task.FromResult(responder(req)))
        {
        }

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) =>
            _responder = responder;

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return await _responder(request, cancellationToken);
        }
    }

    private sealed class CapturingLogger : ILogger<HostingerMailApiSender>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
