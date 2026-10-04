using Cale.BuildingBlocks.Domain.Email;
using Cale.BuildingBlocks.Infrastructure.Email;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.BuildingBlocks.Infrastructure.Security;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.Services;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Cale.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cale.UnitTests;

public sealed class IdentityTestFixture : IDisposable
{
    public IdentityTestFixture()
    {
        var options = new DbContextOptionsBuilder<CaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Db = new CaleDbContext(
            options,
            new MappingAssemblies(typeof(UserConfiguration).Assembly));
        Db.Database.EnsureCreated();

        Hasher = new PasswordHasher();
        Clock = new FakeClock(
            new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc));
        Tokens = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                Key = "CALE-LOCAL-DEV-KEY-CHANGE-ME-32CHARS-MIN",
                Issuer = "Cale.Api",
                Audience = "Cale.Frontend",
                ExpirationHours = 12
            }),
            Clock);
        Users = new UserStore(Db);
        Profiles = new SchoolProfileStore(Db);
        AccountStatus = new AccountStatusStore(Db);
        EmailOptions = Options.Create(new EmailOptions
        {
            Enabled = false,
            CodeLength = 6,
            CodeExpiresMinutes = 15
        });
        EmailConfirmation = new EmailConfirmationService(
            Users,
            new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance),
            Clock,
            EmailOptions);
    }

    public CaleDbContext Db { get; }
    public UserStore Users { get; }
    public SchoolProfileStore Profiles { get; }
    public AccountStatusStore AccountStatus { get; }
    public PasswordHasher Hasher { get; }
    public FakeClock Clock { get; }
    public JwtTokenService Tokens { get; }
    public IOptions<EmailOptions> EmailOptions { get; }
    public EmailConfirmationService EmailConfirmation { get; }

    public RegisterUserHandler CreateRegister() =>
        new(Users, Hasher, Tokens, Clock, EmailConfirmation);

    public ConfirmEmailHandler CreateConfirmEmail() =>
        new(Users, Tokens, Clock, EmailConfirmation);

    public LoginUserHandler CreateLogin() =>
        new(Users, Hasher, Tokens, Clock, AccountStatus, NullLogger<LoginUserHandler>.Instance);

    public SetUserActiveHandler CreateSetActive() =>
        new(Users, new NoopRefreshTokenStore(), AccountStatus, Clock, NullLogger<SetUserActiveHandler>.Instance);

    private sealed class NoopRefreshTokenStore : Cale.Modules.Identity.Application.Abstractions.IRefreshTokenStore
    {
        public Task<string> IssueAsync(int userId, DateTime expiresAtUtc, CancellationToken ct = default) =>
            Task.FromResult("token");
        public Task<int?> ConsumeAsync(string rawToken, CancellationToken ct = default) =>
            Task.FromResult<int?>(null);
        public Task RevokeAsync(string rawToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task RevokeAllForUserAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
    }

    public void Dispose() => Db.Dispose();
}
