using Cale.Api.Services;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Services;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Cale.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace Cale.UnitTests.Identity;

public sealed class CommercialDirectoryServiceTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 11, 4, 15, 0, 0, DateTimeKind.Utc);

    private readonly CaleDbContext _db;
    private readonly FakeClock _clock = new(T0);
    private readonly CommercialDirectoryService _directory;

    public CommercialDirectoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<CaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CaleDbContext(options, new MappingAssemblies(typeof(UserConfiguration).Assembly));
        _db.Database.EnsureCreated();
        _directory = new CommercialDirectoryService(_db, _clock, new SchoolCommercialPresenceGate());
    }

    [Fact]
    public async Task Lists_only_gate_eligible_schools()
    {
        var eligible = await SeedSchoolAsync(
            userId: 1,
            name: "CEA Alfa",
            plan: SchoolPlans.Monthly,
            publish: true,
            paymentConfirmed: true,
            active: true);

        await SeedSchoolAsync(
            userId: 2,
            name: "CEA FreeAccess",
            plan: SchoolPlans.Monthly,
            publish: true,
            paymentConfirmed: false,
            active: false);

        await SeedSchoolAsync(
            userId: 3,
            name: "CEA Sin publicar",
            plan: SchoolPlans.Annual,
            publish: false,
            paymentConfirmed: true,
            active: true);

        await SeedSchoolAsync(
            userId: 4,
            name: "CEA Semestral",
            plan: SchoolPlans.Semestral,
            publish: true,
            paymentConfirmed: true,
            active: true);

        var rows = await _directory.ListAsync(20);

        Assert.Single(rows);
        Assert.Equal(eligible.UserId, rows[0].Id);
        Assert.Equal("CEA Alfa", rows[0].Name);
        Assert.Equal("A2, B1", rows[0].LicenseCategories);
        Assert.StartsWith("/escuelas/", rows[0].DetailPath);
    }

    [Fact]
    public async Task GetBySlug_returns_detail_when_eligible()
    {
        await SeedSchoolAsync(
            userId: 50,
            name: "CEA Ficha",
            plan: SchoolPlans.Annual,
            publish: true,
            paymentConfirmed: true,
            active: true);

        var detail = await _directory.GetBySlugAsync("cea-50");

        Assert.Equal("CEA Ficha", detail.Name);
        Assert.Equal("cea-50", detail.Slug);
        Assert.Equal("/escuelas/cea-50", detail.DetailPath);
        Assert.StartsWith("https://www.google.com/maps/search/?api=1&query=", detail.MapsUrl);
        Assert.Contains("Calle", detail.MapsUrl);
        Assert.Equal("A2, B1", detail.LicenseCategories);
    }

    [Fact]
    public async Task GetBySlug_returns_404_when_unpublished_or_unknown()
    {
        await SeedSchoolAsync(
            userId: 51,
            name: "CEA Oculta",
            plan: SchoolPlans.Monthly,
            publish: false,
            paymentConfirmed: true,
            active: true);

        var hidden = await Assert.ThrowsAsync<Cale.BuildingBlocks.Domain.Exceptions.NotFoundException>(
            () => _directory.GetBySlugAsync("cea-51"));
        Assert.Equal("commercial_school_not_found", hidden.ErrorCode);

        var missing = await Assert.ThrowsAsync<Cale.BuildingBlocks.Domain.Exceptions.NotFoundException>(
            () => _directory.GetBySlugAsync("no-existe"));
        Assert.Equal("commercial_school_not_found", missing.ErrorCode);
    }

    [Fact]
    public async Task Courtesy_school_is_excluded_even_when_published()
    {
        var previous = Cale.BuildingBlocks.Domain.Access.FreeAccessPolicy.Enabled;
        try
        {
            Cale.BuildingBlocks.Domain.Access.FreeAccessPolicy.Enabled = false;
            var school = SchoolProfile.Create(
                20,
                "CEA Cortesía Dir",
                "900200300",
                "c@test.com",
                "3002003000",
                "Calle 20",
                "Barranquilla",
                "Atlántico",
                SchoolPlans.Find(SchoolPlans.Monthly)!,
                T0);
            school.GrantCourtesy(T0.AddDays(30), "Puente", T0);
            _db.Add(school);

            var commercial = SchoolCommercialProfile.Create(20, T0);
            commercial.UpdateSheet(
                "Descripción comercial suficientemente larga para cumplir el mínimo de la ficha.",
                "B1",
                Guid.NewGuid(),
                null,
                T0);
            commercial.SetSlug("cea-cortesia-dir", T0);
            commercial.Publish(T0);
            _db.Add(commercial);
            await _db.SaveChangesAsync();

            var rows = await _directory.ListAsync(10);
            Assert.Empty(rows);
        }
        finally
        {
            Cale.BuildingBlocks.Domain.Access.FreeAccessPolicy.Enabled = previous;
        }
    }

    private async Task<SchoolProfile> SeedSchoolAsync(
        int userId,
        string name,
        string plan,
        bool publish,
        bool paymentConfirmed,
        bool active)
    {
        var planInfo = SchoolPlans.Find(plan)!;
        var school = SchoolProfile.Create(
            userId,
            name,
            $"900{userId:000000}",
            $"school{userId}@test.com",
            $"300{userId:0000000}"[..10],
            $"Calle {userId}",
            "Bogotá",
            "Cundinamarca",
            planInfo,
            T0);

        if (active)
        {
            school.AdminAssignSubscription(
                planInfo,
                planInfo.PriceCop,
                T0.AddDays(-2),
                T0.AddMonths(planInfo.DurationMonths),
                paymentConfirmed ? PaymentAdminStatuses.Confirmed : PaymentAdminStatuses.Pending,
                activateNow: paymentConfirmed,
                T0);
        }
        else if (paymentConfirmed)
        {
            school.SetPaymentAdminStatus(PaymentAdminStatuses.Confirmed, T0);
        }

        _db.Add(school);

        var commercial = SchoolCommercialProfile.Create(userId, T0);
        commercial.UpdateSheet(
            "Descripción comercial suficientemente larga para cumplir el mínimo de la ficha.",
            "A2, B1",
            Guid.NewGuid(),
            null,
            T0);
        commercial.SetSlug($"cea-{userId}", T0);
        if (publish)
        {
            commercial.Publish(T0);
        }

        _db.Add(commercial);
        await _db.SaveChangesAsync();
        return school;
    }

    public void Dispose() => _db.Dispose();
}
