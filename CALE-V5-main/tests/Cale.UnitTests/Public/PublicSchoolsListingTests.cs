using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.UnitTests.Security.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cale.UnitTests.Public;

public sealed class PublicSchoolsListingTests : IAsyncLifetime
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
    public async Task Public_directory_hides_pending_payment_and_lists_active()
    {
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var now = DateTime.UtcNow;
            var plan = SchoolPlans.Find(SchoolPlans.Monthly)!;

            // PendingPayment school (approved, not active) — must stay hidden.
            var unpaid = await db.Set<SchoolProfile>().FirstOrDefaultAsync(p => p.UserId == _api.Users.School1.Id);
            if (unpaid is null)
            {
                unpaid = SchoolProfile.Create(
                    _api.Users.School1.Id, "CEA Pendiente Pago", "900123456-8",
                    "school1@pruebas.test", "3000000001", "Calle 1", "Barranquilla", "Atlántico", plan, now);
                db.Add(unpaid);
            }
            else
            {
                unpaid.UpdateBilling(
                    "CEA Pendiente Pago", "900123456-8", "school1@pruebas.test",
                    "3000000001", "Calle 1", "Barranquilla", "Atlántico");
            }

            // Active school — must appear.
            var active = await db.Set<SchoolProfile>().FirstOrDefaultAsync(p => p.UserId == _api.Users.School2.Id);
            if (active is null)
            {
                active = SchoolProfile.Create(
                    _api.Users.School2.Id, "CEA Activa Publica", "901578798-8",
                    "school2@pruebas.test", "3000000002", "Calle 2", "Barranquilla", "Atlántico", plan, now);
                db.Add(active);
                await db.SaveChangesAsync();
            }

            active.UpdateBilling(
                "CEA Activa Publica", "901578798-8", "school2@pruebas.test",
                "3000000002", "Calle 2", "Barranquilla", "Atlántico");
            active.ActivateOrRenew(plan, now);
            await db.SaveChangesAsync();
        }

        var list = await _api.ClientFor(null).GetFromJsonAsync<JsonElement>("/api/public/schools?take=50");
        var names = list.EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToList();
        Assert.Contains("CEA Activa Publica", names);
        Assert.DoesNotContain("CEA Pendiente Pago", names);
    }
}
