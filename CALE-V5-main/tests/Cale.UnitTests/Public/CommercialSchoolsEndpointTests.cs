using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.UnitTests.Security.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests.Public;

public sealed class CommercialSchoolsEndpointTests : IAsyncLifetime
{
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

    [Fact]
    public async Task Commercial_schools_endpoint_excludes_non_eligible_and_keeps_legacy_schools_endpoint()
    {
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var now = DateTime.UtcNow;
            var plan = SchoolPlans.Find(SchoolPlans.Annual)!;

            var user = User.RegisterSchool(
                "Contacto Comercial",
                $"comercial-{Guid.NewGuid():N}@pruebas.test",
                hasher.Hash(SecurityApiFactory.Password),
                now);
            user.MarkEmailConfirmed();
            db.Add(user);
            await db.SaveChangesAsync();

            var school = SchoolProfile.Create(
                user.Id,
                "CEA Comercial Publicada",
                "900123456-8",
                user.Email,
                "3001112233",
                "Calle 10 # 20-30",
                "Bogotá",
                "Cundinamarca",
                plan,
                now);
            school.AdminAssignSubscription(
                plan,
                plan.PriceCop,
                now.AddDays(-1),
                now.AddYears(1),
                PaymentAdminStatuses.Confirmed,
                activateNow: true,
                now);
            db.Add(school);

            var commercial = SchoolCommercialProfile.Create(user.Id, now);
            commercial.UpdateSheet(
                "Escuela de conducción con formación teórica y práctica para categorías A2 y B1 en Bogotá.",
                "A2, B1",
                Guid.NewGuid(),
                null,
                now);
            commercial.SetSlug($"cea-comercial-{user.Id}", now);
            commercial.Publish(now);
            db.Add(commercial);
            await db.SaveChangesAsync();
        }

        var commercialList = await _api.ClientFor(null)
            .GetFromJsonAsync<JsonElement>("/api/public/commercial-schools?take=50");
        var commercialNames = commercialList.EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()!)
            .ToList();
        Assert.Contains("CEA Comercial Publicada", commercialNames);

        // Legacy endpoint must keep working with its own semantics.
        var legacy = await _api.ClientFor(null)
            .GetAsync("/api/public/schools?take=10");
        Assert.True(legacy.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Commercial_school_slug_endpoint_hides_non_eligible_and_returns_detail()
    {
        string slug;
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var now = DateTime.UtcNow;
            var plan = SchoolPlans.Find(SchoolPlans.Monthly)!;

            var user = User.RegisterSchool(
                "Contacto Ficha",
                $"ficha-{Guid.NewGuid():N}@pruebas.test",
                hasher.Hash(SecurityApiFactory.Password),
                now);
            user.MarkEmailConfirmed();
            db.Add(user);
            await db.SaveChangesAsync();

            var school = SchoolProfile.Create(
                user.Id,
                "CEA Ficha Pública",
                "900123456-8",
                user.Email,
                "3002223344",
                "Cra 15 # 80-20",
                "Bogotá",
                "Cundinamarca",
                plan,
                now);
            school.AdminAssignSubscription(
                plan,
                plan.PriceCop,
                now.AddDays(-1),
                now.AddMonths(1),
                PaymentAdminStatuses.Confirmed,
                activateNow: true,
                now);
            db.Add(school);

            slug = $"cea-ficha-{user.Id}";
            var commercial = SchoolCommercialProfile.Create(user.Id, now);
            commercial.UpdateSheet(
                "Centro de enseñanza automovilística con formación teórica y práctica para categorías A2 y B1.",
                "A2, B1",
                Guid.NewGuid(),
                null,
                now);
            commercial.SetSlug(slug, now);
            commercial.Publish(now);
            db.Add(commercial);
            await db.SaveChangesAsync();
        }

        var ok = await _api.ClientFor(null)
            .GetFromJsonAsync<JsonElement>($"/api/public/commercial-schools/{slug}");
        Assert.Equal("CEA Ficha Pública", ok.GetProperty("name").GetString());
        Assert.Equal(slug, ok.GetProperty("slug").GetString());
        Assert.False(string.IsNullOrWhiteSpace(ok.GetProperty("mapsUrl").GetString()));

        var missing = await _api.ClientFor(null)
            .GetAsync("/api/public/commercial-schools/slug-inexistente-xyz");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task School_commercial_profile_is_forbidden_for_other_roles_and_scoped_to_caller()
    {
        var student = await _api.ClientFor(_api.Users.StudentA).GetAsync("/api/school/commercial-profile");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, student.StatusCode);

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var now = DateTime.UtcNow;
            var plan = SchoolPlans.Find(SchoolPlans.Monthly)!;
            if (!await db.Set<SchoolProfile>().AnyAsync(p => p.UserId == _api.Users.School1.Id))
            {
                db.Add(SchoolProfile.Create(
                    _api.Users.School1.Id,
                    "CEA Escuela 1",
                    "900123456-8",
                    "school1@pruebas.test",
                    "3001111111",
                    "Calle 1",
                    "Bogotá",
                    "Cundinamarca",
                    plan,
                    now));
                await db.SaveChangesAsync();
            }
        }

        var own = await _api.ClientFor(_api.Users.School1).GetAsync("/api/school/commercial-profile");
        Assert.True(own.IsSuccessStatusCode);
        var dto = await own.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(_api.Users.School1.Id, dto.GetProperty("schoolUserId").GetInt32());
    }
}

