using Cale.BuildingBlocks.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cale.UnitTests.Email;

public sealed class ConfigurableEmailSenderTests
{
    [Fact]
    public void Auto_with_hostinger_complete_uses_hostinger()
    {
        var hostinger = CreateHostingerProbe();
        var smtp = CreateSmtp();
        var logging = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance);
        var options = Options.Create(CompleteHostinger(EmailProviders.Auto));

        var sut = new ConfigurableEmailSender(
            options, hostinger, smtp, logging, NullLogger<ConfigurableEmailSender>.Instance);

        Assert.True(sut.IsConfigured);
    }

    [Fact]
    public void Auto_without_hostinger_uses_smtp_when_ready()
    {
        var hostinger = CreateHostingerProbe(complete: false);
        var smtp = CreateSmtp(ready: true);
        var logging = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance);
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            Provider = EmailProviders.Auto,
            Smtp = new SmtpOptions { Host = "smtp.example.com" },
            HostingerMail = new HostingerMailOptions()
        });

        var sut = new ConfigurableEmailSender(
            options, hostinger, smtp, logging, NullLogger<ConfigurableEmailSender>.Instance);

        Assert.True(sut.IsConfigured);
    }

    [Fact]
    public void Auto_disabled_falls_back_to_logging()
    {
        var options = Options.Create(new EmailOptions
        {
            Enabled = false,
            Provider = EmailProviders.Auto
        });
        var sut = new ConfigurableEmailSender(
            options,
            CreateHostingerProbe(complete: false),
            CreateSmtp(ready: false),
            new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance),
            NullLogger<ConfigurableEmailSender>.Instance);

        Assert.False(sut.IsConfigured);
    }

    [Fact]
    public void HostingerMail_incomplete_throws_at_construction()
    {
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            Provider = EmailProviders.HostingerMail,
            HostingerMail = new HostingerMailOptions { ApiToken = "", MailboxResourceId = "" }
        });

        Assert.Throws<InvalidOperationException>(() =>
            new ConfigurableEmailSender(
                options,
                CreateHostingerProbe(complete: false),
                CreateSmtp(ready: false),
                new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance),
                NullLogger<ConfigurableEmailSender>.Instance));
    }

    [Fact]
    public void Smtp_provider_without_host_throws()
    {
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            Provider = EmailProviders.Smtp,
            Smtp = new SmtpOptions { Host = "" }
        });

        Assert.Throws<InvalidOperationException>(() =>
            new ConfigurableEmailSender(
                options,
                CreateHostingerProbe(complete: false),
                CreateSmtp(ready: false),
                new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance),
                NullLogger<ConfigurableEmailSender>.Instance));
    }

    [Fact]
    public void Invalid_provider_throws()
    {
        var options = Options.Create(new EmailOptions
        {
            Enabled = false,
            Provider = "SendGrid"
        });

        Assert.Throws<InvalidOperationException>(() =>
            new ConfigurableEmailSender(
                options,
                CreateHostingerProbe(complete: false),
                CreateSmtp(ready: false),
                new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance),
                NullLogger<ConfigurableEmailSender>.Instance));
    }

    [Fact]
    public void Auto_enabled_without_any_provider_throws()
    {
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            Provider = EmailProviders.Auto
        });

        Assert.Throws<InvalidOperationException>(() =>
            new ConfigurableEmailSender(
                options,
                CreateHostingerProbe(complete: false),
                CreateSmtp(ready: false),
                new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance),
                NullLogger<ConfigurableEmailSender>.Instance));
    }

    private static EmailOptions CompleteHostinger(string provider) => new()
    {
        Enabled = true,
        Provider = provider,
        FromName = "Luz Verde",
        HostingerMail = new HostingerMailOptions
        {
            ApiToken = "token",
            MailboxResourceId = "ACxxx",
            BaseUrl = "https://api.mail.hostinger.com"
        }
    };

    private static HostingerMailApiSender CreateHostingerProbe(bool complete = true)
    {
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            HostingerMail = new HostingerMailOptions
            {
                ApiToken = complete ? "token" : "",
                MailboxResourceId = complete ? "ACxxx" : "",
                BaseUrl = "https://api.mail.hostinger.com"
            }
        });
        return new HostingerMailApiSender(
            new NoHttpFactory(),
            options,
            NullLogger<HostingerMailApiSender>.Instance);
    }

    private static SmtpEmailSender CreateSmtp(bool ready = true)
    {
        var options = Options.Create(new EmailOptions
        {
            Enabled = ready,
            Smtp = new SmtpOptions { Host = ready ? "smtp.example.com" : "" }
        });
        return new SmtpEmailSender(options, NullLogger<SmtpEmailSender>.Instance);
    }

    private sealed class NoHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
