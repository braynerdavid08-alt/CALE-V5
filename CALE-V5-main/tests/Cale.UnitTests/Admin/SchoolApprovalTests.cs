using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.UnitTests.Security.Integration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cale.UnitTests.Admin;

public sealed class ColombianTaxIdTests
{
    [Theory]
    [InlineData("901578798-8", "901578798-8")]
    [InlineData("901.578.798-8", "901578798-8")]
    [InlineData(" 900123456 - 8 ", "900123456-8")]
    public void Valid_nits_are_normalized(string raw, string expected)
    {
        Assert.True(ColombianTaxId.TryNormalize(raw, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("000000000-0")]
    [InlineData("111111111-1")]
    [InlineData("900123456-7")]
    [InlineData("900123456")]
    [InlineData("9001234568")]
    [InlineData("PENDIENTE")]
    [InlineData("12345-6")]
    [InlineData("")]
    public void Placeholder_or_wrong_check_digit_is_rejected(string raw)
    {
        Assert.False(ColombianTaxId.TryNormalize(raw, out _));
        var ex = Assert.Throws<DomainException>(() => ColombianTaxId.Normalize(raw));
        Assert.Equal("invalid_tax_id", ex.ErrorCode);
    }

    [Fact]
    public void Comparison_key_ignores_legacy_formatting()
    {
        Assert.Equal(ColombianTaxId.ComparisonKey("901.578.798-8"), ColombianTaxId.ComparisonKey("9015787988"));
    }
}

public sealed class SchoolApprovalTests : IAsyncLifetime
{
    private const string Nit = "901578798-8";
    private readonly SecurityApiFactory _api = new();

    public async Task InitializeAsync()
    {
        _ = _api.Server;
        await _api.SeedAsync();
    }

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    private static object Registration(string email, string taxId) => new
    {
        contactName = "Contacto Prueba",
        email,
        password = SecurityApiFactory.Password,
        legalName = "CEA Prueba Aprobación",
        taxId,
        billingEmail = email,
        phone = "3001234567",
        address = "Calle 1 # 2-3",
        city = "Barranquilla",
        department = "Atlántico",
        planCode = SchoolPlans.Monthly,
        consent = new { acceptTerms = true, acknowledgePrivacy = true, authorizeDataProcessing = true, acceptSchoolTerms = true }
    };

    private async Task<int> RegisterAsync(string email = "luzverde.cea.prueba@gmail.com", string taxId = Nit)
    {
        var response = await _api.ClientFor(null).PostAsJsonAsync("/api/auth/register-school", Registration(email, taxId));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("userId").GetInt32();
    }

    private async Task<JsonElement> AdminSummaryAsync(int schoolId)
    {
        var list = await _api.ClientFor(_api.Users.Admin).GetFromJsonAsync<JsonElement>("/api/admin/schools");
        return list.EnumerateArray().Single(s => s.GetProperty("userId").GetInt32() == schoolId);
    }

    private async Task<bool> IsPubliclyListedAsync() =>
        (await _api.ClientFor(null).GetStringAsync("/api/public/schools")).Contains("CEA Prueba Aprobación");

    [Fact]
    public async Task Self_registered_school_waits_for_admin_approval()
    {
        var schoolId = await RegisterAsync();

        Assert.True((await AdminSummaryAsync(schoolId)).GetProperty("pendingApproval").GetBoolean());
        Assert.False(await IsPubliclyListedAsync());

        var student = _api.ClientFor(_api.Users.LoneRequestingStudent);
        var blocked = await student.PostAsJsonAsync("/api/me/school-membership/requests", new { schoolQuery = Nit });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("school_pending_approval", await blocked.Content.ReadAsStringAsync());

        var approved = await _api.ClientFor(_api.Users.Admin).PostAsync($"/api/admin/schools/{schoolId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        Assert.False((await AdminSummaryAsync(schoolId)).GetProperty("pendingApproval").GetBoolean());
        Assert.False(await IsPubliclyListedAsync()); // still PendingPayment until activated

        var joined = await student.PostAsJsonAsync("/api/me/school-membership/requests", new { schoolQuery = Nit });
        Assert.True(joined.IsSuccessStatusCode, await joined.Content.ReadAsStringAsync());

        var activated = await _api.ClientFor(_api.Users.Admin).PostAsJsonAsync(
            $"/api/admin/memberships/{schoolId}/activate",
            new { forceWithoutProof = true });
        Assert.True(activated.IsSuccessStatusCode, await activated.Content.ReadAsStringAsync());
        Assert.True(await IsPubliclyListedAsync());

        var again = await _api.ClientFor(_api.Users.Admin).PostAsync($"/api/admin/schools/{schoolId}/approve", null);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Pending_school_cannot_invite_members()
    {
        var schoolId = await RegisterAsync();
        var school = _api.ClientFor(await SchoolUserAsync(schoolId));

        var invite = await school.PostAsJsonAsync("/api/school/invitations", new { email = _api.Users.LoneTeacher.Email, role = "Teacher" });

        Assert.Equal(HttpStatusCode.Forbidden, invite.StatusCode);
        Assert.Contains("school_pending_approval", await invite.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_or_duplicate_nit_is_rejected_at_registration()
    {
        var anon = _api.ClientFor(null);

        var invalid = await anon.PostAsJsonAsync("/api/auth/register-school", Registration("luzverde.cea.otra@gmail.com", "000000000-0"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("invalid_tax_id", await invalid.Content.ReadAsStringAsync());

        await RegisterAsync();
        var duplicate = await anon.PostAsJsonAsync("/api/auth/register-school", Registration("luzverde.cea.otra@gmail.com", "901.578.798-8"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("tax_id_taken", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Existing_schools_stay_approved()
    {
        var summary = await AdminSummaryAsync(_api.Users.School1.Id);
        Assert.False(summary.GetProperty("pendingApproval").GetBoolean());
    }

    private async Task<User> SchoolUserAsync(int schoolId)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        return (await db.Set<User>().FindAsync(schoolId))!;
    }
}
