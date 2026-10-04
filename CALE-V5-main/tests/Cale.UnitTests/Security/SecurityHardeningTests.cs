using System.Reflection;
using System.Security.Claims;
using System.Text;
using Cale.Api.Controllers;
using Cale.Api.Infrastructure;
using Cale.Api.Middleware;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Security;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cale.UnitTests.Security;

public sealed class UploadSniffingTests
{
    [Fact]
    public void Real_png_and_jpeg_are_recognised_as_images()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        var jpg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 };

        Assert.Equal("image/png", MediaSniffer.Detect(png)?.ContentType);
        Assert.Equal("image/jpeg", MediaSniffer.Detect(jpg)?.ContentType);
    }

    [Theory]
    [InlineData("<html><script>alert(document.cookie)</script></html>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"></svg>")]
    [InlineData("<?xml version=\"1.0\"?><svg></svg>")]
    [InlineData("MZ\u0090\u0000 executable")]
    public void Html_svg_and_executables_disguised_as_images_are_rejected(string content)
    {
        Assert.Null(MediaSniffer.Detect(Encoding.UTF8.GetBytes(content)));
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("image/svg+xml")]
    [InlineData("application/javascript")]
    [InlineData(null)]
    public void Stored_active_content_is_served_as_a_download_never_inline(string? storedType)
    {
        var context = new DefaultHttpContext();

        var served = SafeMediaResponse.Prepare(context.Response, storedType);

        Assert.Equal("application/octet-stream", served);
        Assert.Equal("attachment", context.Response.Headers.ContentDisposition.ToString());
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions.ToString());
        Assert.Contains("sandbox", context.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public void Images_are_served_inline_but_still_sandboxed()
    {
        var context = new DefaultHttpContext();

        var served = SafeMediaResponse.Prepare(context.Response, "image/png");

        Assert.Equal("image/png", served);
        Assert.Contains("sandbox", context.Response.Headers.ContentSecurityPolicy.ToString());
    }
}

public sealed class AccountTakeoverTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void School_manages_credentials_only_of_accounts_it_created()
    {
        var created = User.RegisterStudent("Ana", "ana@test.co", "hash", Now, schoolId: 5);
        created.MarkCreatedBySchool(5);

        Assert.True(created.CredentialsManagedBy(5));
        Assert.False(created.CredentialsManagedBy(6));
    }

    [Fact]
    public void Attached_existing_account_cannot_be_taken_over_by_the_school()
    {
        var attached = User.RegisterStudent("Luis", "luis@test.co", "hash", Now);
        attached.AssignSchool(5);

        Assert.False(attached.CredentialsManagedBy(5));
    }

    [Fact]
    public void Former_school_loses_credential_control_after_the_member_leaves()
    {
        var user = User.RegisterStudent("Eva", "eva@test.co", "hash", Now, schoolId: 5);
        user.MarkCreatedBySchool(5);
        user.LeaveSchool();

        Assert.False(user.CredentialsManagedBy(5));
    }
}

public sealed class PaymentProofTests
{
    [Theory]
    [InlineData("/uploads/receipts/0123456789abcdef0123456789abcdef.png")]
    [InlineData("/uploads/receipts/0123456789abcdef0123456789abcdef.pdf")]
    public void Receipts_issued_by_our_upload_endpoint_are_accepted(string url)
    {
        Assert.True(SchoolProfile.IsIssuedReceiptPath(url));
    }

    [Theory]
    [InlineData("https://evil.example/receipt.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/uploads/receipts/../../appsettings.json")]
    [InlineData("/uploads/receipts/0123456789abcdef0123456789abcdef.html")]
    [InlineData("/uploads/receipts/0123456789abcdef0123456789abcdef.svg")]
    [InlineData("/uploads/courses/0123456789abcdef0123456789abcdef.png")]
    [InlineData("")]
    public void Foreign_or_crafted_receipt_urls_are_rejected(string url)
    {
        Assert.False(SchoolProfile.IsIssuedReceiptPath(url));
    }
}

public sealed class PrivateBankVisibilityTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Private_bank_is_hidden_from_other_teachers()
    {
        var bank = Bank.Create("Banco privado", null, Now, createdById: 10);

        Assert.True(bank.IsVisibleTo(10, isAdmin: false));
        Assert.False(bank.IsVisibleTo(11, isAdmin: false));
        Assert.True(bank.IsVisibleTo(11, isAdmin: true));
    }

    [Fact]
    public void Official_bank_is_visible_to_everyone()
    {
        var bank = Bank.Create("Banco oficial", null, Now);

        Assert.True(bank.IsVisibleTo(99, isAdmin: false));
    }
}

public sealed class CsvInjectionTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\",\"x\")")]
    [InlineData("+cmd|' /C calc'!A0")]
    [InlineData("-2+3")]
    [InlineData("@SUM(1+1)")]
    public void Formula_like_cells_are_neutralised(string value)
    {
        var cell = CsvCell.Escape(value);

        Assert.StartsWith("\"'", cell);
    }

    [Fact]
    public void Plain_text_is_only_quoted_and_quotes_are_doubled()
    {
        Assert.Equal("\"Ana \"\"la\"\" piloto\"", CsvCell.Escape("Ana \"la\" piloto"));
    }

    [Fact]
    public void Exported_formula_cells_round_trip_on_import()
    {
        Assert.Equal("=1+1", CsvCell.Unescape("'=1+1"));
        Assert.Equal("'normal", CsvCell.Unescape("'normal"));
    }
}

public sealed class ReceiptGateTests
{
    [Fact]
    public async Task Anonymous_user_cannot_download_payment_receipts()
    {
        var (status, reached) = await RunAsync(role: null);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(reached);
    }

    [Theory]
    [InlineData(Roles.Student)]
    [InlineData(Roles.Teacher)]
    public void Students_and_teachers_cannot_download_payment_receipts(string role)
    {
        var (status, reached) = RunAsync(role).GetAwaiter().GetResult();

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.False(reached);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.School)]
    public async Task Admin_and_school_can_download_payment_receipts(string role)
    {
        var (_, reached) = await RunAsync(role);

        Assert.True(reached);
    }

    private static async Task<(int Status, bool Reached)> RunAsync(string? role)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new FakeAuth(role))
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/uploads/receipts/0123456789abcdef0123456789abcdef.png";

        var reached = false;
        var middleware = new CourseVideoGateMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context);
        return (context.Response.StatusCode, reached);
    }

    private sealed class FakeAuth(string? role) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(role is null
                ? AuthenticateResult.NoResult()
                : AuthenticateResult.Success(new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, role)], "test")),
                    "test")));

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}

public sealed class SessionRevocationTests : IDisposable
{
    private readonly IdentityTestFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    [Fact]
    public async Task Token_of_a_deactivated_user_is_rejected()
    {
        var user = await AddUserAsync(Roles.Teacher);
        user.Deactivate();
        await _fx.Users.SaveChangesAsync(default);

        var (status, reached) = await RunAsync(user.Id, Roles.Teacher);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(reached);
    }

    [Fact]
    public async Task Token_issued_before_a_role_change_is_rejected()
    {
        var user = await AddUserAsync(Roles.Teacher);
        user.ChangeRole(Roles.Student);
        await _fx.Users.SaveChangesAsync(default);

        var (status, reached) = await RunAsync(user.Id, Roles.Teacher);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(reached);
    }

    [Fact]
    public async Task Token_of_a_deleted_user_is_rejected()
    {
        var (status, reached) = await RunAsync(9999, Roles.Admin);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(reached);
    }

    [Fact]
    public async Task Valid_active_user_passes()
    {
        var user = await AddUserAsync(Roles.Teacher);

        var (_, reached) = await RunAsync(user.Id, Roles.Teacher);

        Assert.True(reached);
    }

    [Fact]
    public async Task Forced_password_change_blocks_the_api()
    {
        var user = await AddUserAsync(Roles.Student);
        user.RequirePasswordChange();
        await _fx.Users.SaveChangesAsync(default);

        var (status, reached) = await RunAsync(user.Id, Roles.Student);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.False(reached);
    }

    private async Task<User> AddUserAsync(string role)
    {
        var user = role == Roles.Teacher
            ? User.CreateTeacher("Profe", $"{Guid.NewGuid():N}@test.co", "hash", _fx.Clock.UtcNow, emailConfirmed: true)
            : User.RegisterStudent("Alumno", $"{Guid.NewGuid():N}@test.co", "hash", _fx.Clock.UtcNow);
        await _fx.Users.AddAsync(user, default);
        await _fx.Users.SaveChangesAsync(default);
        return user;
    }

    private async Task<(int Status, bool Reached)> RunAsync(int userId, string tokenRole)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/exams";
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, tokenRole)],
            "test"));

        var reached = false;
        var middleware = new MustChangePasswordMiddleware(
            _ =>
            {
                reached = true;
                return Task.CompletedTask;
            },
            NullLogger<MustChangePasswordMiddleware>.Instance);
        await middleware.InvokeAsync(context, _fx.Users);
        return (context.Response.StatusCode, reached);
    }
}

public sealed class ErrorDisclosureTests
{
    [Fact]
    public async Task Production_errors_do_not_leak_internal_messages()
    {
        var body = await RunAsync(Environments.Production);

        Assert.DoesNotContain("Server=db.internal", body);
        Assert.Contains("internal_error", body);
    }

    [Fact]
    public async Task Development_errors_keep_details_for_debugging()
    {
        var body = await RunAsync(Environments.Development);

        Assert.Contains("Server=db.internal", body);
    }

    private static async Task<string> RunAsync(string environment)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/exams";
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Connection failed: Server=db.internal;Password=secret"),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            new FakeEnv(environment));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }
}

public sealed class SecurityHeadersTests
{
    [Fact]
    public async Task Responses_carry_browser_hardening_headers_and_a_strict_csp()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("luzverde.test");
        var config = new ConfigurationBuilder().Build();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask, config, new FakeEnv(Environments.Production));

        await middleware.InvokeAsync(context);

        var h = context.Response.Headers;
        Assert.Equal("nosniff", h.XContentTypeOptions.ToString());
        Assert.Equal("SAMEORIGIN", h.XFrameOptions.ToString());
        var csp = h.ContentSecurityPolicy.ToString();
        Assert.Contains("script-src 'self'", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("frame-ancestors 'self'", csp);
    }
}

public sealed class AuthorizationSurfaceTests
{
    private static readonly Assembly Api = typeof(UsersController).Assembly;

    [Fact]
    public void Every_admin_route_requires_the_admin_role()
    {
        var adminControllers = Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<RouteAttribute>()?.Template.StartsWith("api/admin", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.NotEmpty(adminControllers);
        foreach (var controller in adminControllers)
        {
            var auth = controller.GetCustomAttributes<AuthorizeAttribute>().ToList();
            Assert.True(
                auth.Any(a => a.Policy == "AdminOnly" || a.Roles == Roles.Admin),
                $"{controller.Name} must require the admin role");
            Assert.DoesNotContain(
                controller.GetMethods(),
                m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
        }
    }

    [Fact]
    public void Every_endpoint_declares_an_explicit_access_decision()
    {
        var missing = Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<AuthorizeAttribute>() is null && t.GetCustomAttribute<AllowAnonymousAttribute>() is null)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute))
                .Where(m => m.GetCustomAttribute<AuthorizeAttribute>() is null && m.GetCustomAttribute<AllowAnonymousAttribute>() is null)
                .Select(m => $"{t.Name}.{m.Name}"))
            .ToList();

        Assert.True(missing.Count == 0, "Endpoints without [Authorize]/[AllowAnonymous]: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData(nameof(GameShowController.PutGlobalSettings))]
    [InlineData(nameof(GameShowController.RestoreGlobalSettings))]
    public void Global_game_show_settings_are_admin_only(string action)
    {
        var method = typeof(GameShowController).GetMethod(action)!;

        Assert.Equal("AdminOnly", method.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }
}

internal sealed class FakeEnv(string name) : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "Cale.Api";
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
