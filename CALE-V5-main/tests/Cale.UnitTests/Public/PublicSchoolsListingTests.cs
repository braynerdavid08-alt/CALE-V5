using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Domain.Security;
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
    public async Task Public_directory_hides_empty_unpaid_schools_and_lists_those_with_members()
    {
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var now = DateTime.UtcNow;
            var plan = SchoolPlans.Find(SchoolPlans.Monthly)!;

            var emptyUser = User.RegisterSchool(
                "Contacto Vacio",
                $"vacia-{Guid.NewGuid():N}@pruebas.test",
                hasher.Hash(SecurityApiFactory.Password),
                now);
            emptyUser.MarkEmailConfirmed();
            db.Add(emptyUser);
            await db.SaveChangesAsync();
            db.Add(SchoolProfile.Create(
                emptyUser.Id, "CEA Vacia Sin Miembros", "900123456-8",
                emptyUser.Email, "3000000001", "Calle 1", "Barranquilla", "Atlántico", plan, now));

            // Paid model (FreeForAll=false): public listing requires CanOperateProduct.
            // Members alone no longer unlock visibility — seed an operable school instead.
            var operable = await db.Set<SchoolProfile>().FirstOrDefaultAsync(p => p.UserId == _api.Users.School2.Id);
            if (operable is null)
            {
                operable = SchoolProfile.Create(
                    _api.Users.School2.Id, "CEA Con Plan Activo", "901578798-8",
                    "school2@pruebas.test", "3000000002", "Calle 2", "Barranquilla", "Atlántico", plan, now);
                db.Add(operable);
            }
            else
            {
                operable.UpdateBilling(
                    "CEA Con Plan Activo", "901578798-8", "school2@pruebas.test",
                    "3000000002", "Calle 2", "Barranquilla", "Atlántico");
            }

            if (!operable.CanOperateProduct(now))
            {
                operable.GrantCourtesy(now.AddDays(30), "public-listing-fixture", now);
            }

            await db.SaveChangesAsync();
        }

        var list = await _api.ClientFor(null).GetFromJsonAsync<JsonElement>("/api/public/schools?take=50");
        var names = list.EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToList();
        Assert.Contains("CEA Con Plan Activo", names);
        Assert.DoesNotContain("CEA Vacia Sin Miembros", names);
    }
}
