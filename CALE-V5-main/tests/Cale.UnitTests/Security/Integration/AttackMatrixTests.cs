using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Cale.UnitTests.Security.Integration;

/// <summary>
/// Controlled attack matrix against the real HTTP pipeline. Each test is one row of
/// SECURITY_TEST_MATRIX.md: an attacker with a legitimate account (or none) crafts the request by hand.
/// </summary>
[Collection(SecurityApiCollection.Name)]
public sealed class AttackMatrixTests(SecurityApiFixture fixture)
{
    private readonly SecurityApiFactory _api = fixture.Factory;
    private SecurityApiFactory.Accounts U => _api.Users;
    private SecurityApiFactory.Fixtures D => _api.Data;

    // ---------- Vertical privilege escalation ----------

    [Fact]
    public async Task Anonymous_to_admin_endpoint_is_401()
    {
        var res = await _api.ClientFor(null).GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData("student")]
    [InlineData("teacher")]
    [InlineData("school")]
    public async Task Non_admin_to_admin_endpoint_is_403(string who)
    {
        var res = await _api.ClientFor(Pick(who)).GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData("student")]
    [InlineData("teacher")]
    public async Task Student_or_teacher_to_school_panel_is_403(string who)
    {
        var res = await _api.ClientFor(Pick(who)).GetAsync("/api/school/members");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Student_to_teacher_endpoint_is_403()
    {
        var res = await _api.ClientFor(U.StudentA).PostAsJsonAsync("/api/groups", new { name = "Hack", isActive = true });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Student_cannot_read_the_question_bank()
    {
        var res = await _api.ClientFor(U.StudentA).GetAsync($"/api/questions/{D.PrivateQuestionId}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Admin_can_reach_admin_endpoint()
    {
        var res = await _api.ClientFor(U.Admin).GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---------- JWT tampering ----------

    [Fact]
    public async Task Jwt_with_role_edited_to_admin_is_401()
    {
        var token = _api.TokenFor(U.StudentA);
        var parts = token.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]))
            .Replace("\"Student\"", "\"Admin\"", StringComparison.Ordinal);
        var forged = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";

        var res = await Send(HttpMethod.Get, "/api/admin/users", forged);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Jwt_signed_with_another_key_is_401()
    {
        var res = await Send(HttpMethod.Get, "/api/admin/users", Forge(U.Admin, "attacker-controlled-key-0123456789-abcdefghijkl", DateTime.UtcNow.AddMinutes(30)));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Expired_jwt_is_401()
    {
        var res = await Send(HttpMethod.Get, "/api/auth/me", Forge(U.StudentA, SecurityApiFactory.JwtKey, DateTime.UtcNow.AddMinutes(-5)));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Unsigned_alg_none_jwt_is_401()
    {
        var header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var body = Base64UrlEncoder.Encode(
            $"{{\"{ClaimTypes.NameIdentifier}\":\"{U.Admin.Id}\",\"{ClaimTypes.Role}\":\"Admin\",\"iss\":\"Cale.Api\",\"aud\":\"Cale.Frontend\",\"exp\":{exp}}}");

        var res = await Send(HttpMethod.Get, "/api/admin/users", $"{header}.{body}.");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Jwt_for_another_audience_is_401()
    {
        var res = await Send(HttpMethod.Get, "/api/auth/me", Forge(U.StudentA, SecurityApiFactory.JwtKey, DateTime.UtcNow.AddMinutes(30), audience: "other-app"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---------- Multi-tenant isolation (School A vs School B) ----------

    [Fact]
    public async Task School_member_list_never_includes_other_school()
    {
        var body = await _api.ClientFor(U.School1).GetStringAsync("/api/school/members");
        Assert.Contains(U.StudentA.Email, body);
        Assert.DoesNotContain(U.StudentC.Email, body);
        Assert.DoesNotContain(U.Teacher2.Email, body);
    }

    [Fact]
    public async Task School_A_cannot_edit_or_reset_password_of_school_B_student()
    {
        var res = await _api.ClientFor(U.School1).PutAsJsonAsync(
            $"/api/school/members/{U.StudentC.Id}",
            new { name = "Hackeado", email = U.StudentC.Email, newPassword = "Robada-12345!" });

        Assert.True(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"got {(int)res.StatusCode}");
        var stored = await Reload(U.StudentC.Id);
        Assert.Equal("Student_C_School_2", stored.Name);
        Assert.Equal(U.StudentC.PasswordHash, stored.PasswordHash);
    }

    [Fact]
    public async Task School_cannot_activate_its_own_plan_with_forged_fields()
    {
        var res = await _api.ClientFor(U.School1).PostAsJsonAsync(
            "/api/school/plan/activate",
            new { plan = "premium", isPaid = true, status = "Active", expiresAt = "2099-01-01" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var put = await _api.ClientFor(U.School1).PutAsJsonAsync("/api/school/plan", new { plan = "premium", isPaid = true });
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    // ---------- Teacher A vs resources of school B ----------

    [Fact]
    public async Task Teacher_cannot_read_private_question_of_another_school()
    {
        var res = await _api.ClientFor(U.Teacher1).GetAsync($"/api/questions/{D.PrivateQuestionId}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Teacher_question_listing_hides_private_banks_of_others()
    {
        var body = await _api.ClientFor(U.Teacher1).GetStringAsync($"/api/questions?bankId={D.PrivateBankId}&pageSize=100");
        Assert.DoesNotContain("Pregunta privada de la escuela 2", body);
        Assert.DoesNotContain("Solucionario privado", body);
    }

    [Fact]
    public async Task Owner_teacher_still_sees_own_private_question()
    {
        var res = await _api.ClientFor(U.Teacher2).GetAsync($"/api/questions/{D.PrivateQuestionId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Teacher_cannot_open_group_of_another_teacher()
    {
        var res = await _api.ClientFor(U.Teacher1).GetAsync($"/api/groups/{D.Teacher2GroupId}");
        Assert.True(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"got {(int)res.StatusCode}");

        var members = await _api.ClientFor(U.Teacher1).GetAsync($"/api/groups/{D.Teacher2GroupId}/members");
        Assert.True(members.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"got {(int)members.StatusCode}");
    }

    [Fact]
    public async Task Teacher_cannot_add_students_to_group_of_another_teacher()
    {
        var res = await _api.ClientFor(U.Teacher1).PostAsJsonAsync(
            $"/api/groups/{D.Teacher2GroupId}/members",
            new { email = U.StudentA.Email });
        Assert.True(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"got {(int)res.StatusCode}");
    }

    [Fact]
    public async Task Teacher_cannot_pull_student_of_another_school_into_own_group()
    {
        var create = await _api.ClientFor(U.Teacher1).PostAsJsonAsync("/api/groups", new { name = "Grupo T1", isActive = true });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var groupId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var res = await _api.ClientFor(U.Teacher1).PostAsJsonAsync(
            $"/api/groups/{groupId}/members",
            new { email = U.StudentC.Email });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ---------- Student A vs Student B (attempts) ----------

    [Fact]
    public async Task Student_B_cannot_review_attempt_of_student_A()
    {
        var res = await _api.ClientFor(U.StudentB).GetAsync($"/api/exams/{D.StudentAAttemptId}/review");
        Assert.True(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"got {(int)res.StatusCode}");
    }

    [Fact]
    public async Task Student_B_cannot_answer_or_finish_attempt_of_student_A()
    {
        var answer = await _api.ClientFor(U.StudentB).PostAsJsonAsync(
            $"/api/exams/{D.StudentAAttemptId}/answer",
            new { questionId = D.PrivateQuestionId, optionId = 1 });
        Assert.True(answer.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"got {(int)answer.StatusCode}");

        var finish = await _api.ClientFor(U.StudentB).PostAsync($"/api/exams/{D.StudentAAttemptId}/finish", null);
        Assert.True(finish.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"got {(int)finish.StatusCode}");
    }

    [Fact]
    public async Task Forged_score_in_finish_body_is_ignored_and_server_grades()
    {
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var attempt = Cale.Modules.Assessment.Domain.Attempt.Start(U.StudentB.Id, D.PrivateBankId, null, "practice", 10, 30, DateTime.UtcNow);
            db.Add(attempt);
            await db.SaveChangesAsync();

            var res = await _api.ClientFor(U.StudentB).PostAsJsonAsync(
                $"/api/exams/{attempt.Id}/finish",
                new { score = 100, percent = 100, passed = true, approved = true, correctCount = 10, attempts = 0 });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var result = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(result.GetProperty("passed").GetBoolean());
            Assert.Equal(0m, result.GetProperty("percent").GetDecimal());
        }
    }

    // ---------- Mass assignment ----------

    [Fact]
    public async Task Extra_role_and_school_fields_in_profile_update_are_ignored()
    {
        var res = await _api.ClientFor(U.StudentB).PutAsJsonAsync(
            "/api/auth/me",
            new { name = "Student_B_School_1", role = "Admin", isAdmin = true, schoolId = U.School2.Id, isActive = true, isPaid = true });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var stored = await Reload(U.StudentB.Id);
        Assert.Equal(Roles.Student, Roles.Normalize(stored.Role));
        Assert.Equal(U.School1.Id, stored.SchoolId);
    }

    // ---------- Safe responses ----------

    [Fact]
    public async Task Unknown_api_route_returns_404_not_the_spa()
    {
        var res = await _api.ClientFor(U.StudentA).GetAsync("/api/esto-no-existe/26");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.NotEqual("text/html", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Private_api_responses_are_not_cacheable()
    {
        var res = await _api.ClientFor(U.StudentA).GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.CacheControl?.NoStore == true, "Cache-Control must be no-store");
    }

    [Fact]
    public async Task Plain_json_is_returned_without_the_wire_header_so_encryption_is_not_a_control()
    {
        var res = await _api.ClientFor(U.StudentA).GetAsync("/api/auth/me");
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains(U.StudentA.Email, body);
    }

    [Fact]
    public async Task Payment_receipts_require_admin_or_school()
    {
        var res = await _api.ClientFor(U.StudentA).GetAsync("/uploads/receipts/0123456789abcdef0123456789abcdef.png");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ---------- helpers ----------

    private User Pick(string who) => who switch
    {
        "student" => U.StudentA,
        "teacher" => U.Teacher1,
        "school" => U.School1,
        _ => U.Admin
    };

    private async Task<HttpResponseMessage> Send(HttpMethod method, string url, string token)
    {
        var client = _api.ClientFor(null);
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(req);
    }

    private static string Forge(User user, string key, DateTime expires, string audience = "Cale.Frontend")
    {
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "Cale.Api",
            audience: audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, Roles.Normalize(user.Role))
            ],
            notBefore: expires.AddHours(-2),
            expires: expires,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<User> Reload(int id)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        return await db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == id);
    }
}
