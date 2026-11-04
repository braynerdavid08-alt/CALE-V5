using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Services;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Cale.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace Cale.UnitTests.Identity;

public sealed class SchoolCommercialProfileServiceTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 11, 4, 16, 0, 0, DateTimeKind.Utc);

    private readonly CaleDbContext _db;
    private readonly SchoolProfileStore _profiles;
    private readonly FakeClock _clock = new(T0);
    private readonly SchoolCommercialProfileService _service;

    public SchoolCommercialProfileServiceTests()
    {
        var options = new DbContextOptionsBuilder<CaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CaleDbContext(options, new MappingAssemblies(typeof(UserConfiguration).Assembly));
        _db.Database.EnsureCreated();
        _profiles = new SchoolProfileStore(_db);
        _service = new SchoolCommercialProfileService(
            _db,
            _profiles,
            _clock,
            new SchoolCommercialPresenceGate(),
            new OwnerOnlySchoolCommercialAccessPolicy());
    }

    [Fact]
    public async Task School_cannot_manage_another_schools_commercial_profile()
    {
        await SeedEligibleSchoolAsync(1, "CEA Uno");
        await SeedEligibleSchoolAsync(2, "CEA Dos");

        var policy = new OwnerOnlySchoolCommercialAccessPolicy();
        Assert.False(await policy.CanManageAsync(1, 2));

        // Service always uses actor as school id; forged other-id is rejected by access policy.
        var foreign = new SpyAccessPolicy(allow: false);
        var locked = new SchoolCommercialProfileService(
            _db,
            _profiles,
            _clock,
            new SchoolCommercialPresenceGate(),
            foreign);

        var ex = await Assert.ThrowsAsync<DomainException>(() => locked.GetOwnAsync(1));
        Assert.Equal("commercial_forbidden", ex.ErrorCode);
    }

    [Fact]
    public async Task Update_and_publish_roundtrip_for_own_school()
    {
        await SeedEligibleSchoolAsync(7, "CEA Propia");

        var updated = await _service.UpdateOwnAsync(
            7,
            new UpdateSchoolCommercialProfileRequest(
                "Formación vial con flota propia, instructores certificados y acompañamiento en ruta.",
                "A2, B1, C1",
                Guid.NewGuid(),
                null));

        Assert.False(updated.IsPublished);
        Assert.True(updated.IsSheetComplete);

        var published = await _service.SetPublicationAsync(7, publish: true);
        Assert.True(published.IsPublished);
        Assert.True(published.PresenceEligible);
    }

    [Fact]
    public async Task Publish_denied_without_confirmed_paid_coverage()
    {
        var plan = SchoolPlans.Find(SchoolPlans.Monthly)!;
        var school = SchoolProfile.Create(
            8,
            "CEA Sin Pago",
            "900888777",
            "nopay@test.com",
            "3008887777",
            "Calle 8",
            "Bogotá",
            "Cundinamarca",
            plan,
            T0);
        _db.Add(school);
        await _db.SaveChangesAsync();

        await _service.UpdateOwnAsync(
            8,
            new UpdateSchoolCommercialProfileRequest(
                "Formación vial con flota propia, instructores certificados y acompañamiento en ruta.",
                "A2",
                Guid.NewGuid(),
                null));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.SetPublicationAsync(8, publish: true));
        Assert.Equal(SchoolCommercialPresenceDenyReasons.PaymentNotConfirmed, ex.ErrorCode);
    }

    private async Task SeedEligibleSchoolAsync(int userId, string name)
    {
        var plan = SchoolPlans.Find(SchoolPlans.Monthly)!;
        var school = SchoolProfile.Create(
            userId,
            name,
            $"901{userId:000000}",
            $"s{userId}@test.com",
            $"301{userId:0000000}"[..10],
            $"Calle {userId}",
            "Bogotá",
            "Cundinamarca",
            plan,
            T0);
        school.AdminAssignSubscription(
            plan,
            plan.PriceCop,
            T0.AddDays(-1),
            T0.AddMonths(1),
            PaymentAdminStatuses.Confirmed,
            activateNow: true,
            T0);
        _db.Add(school);
        await _db.SaveChangesAsync();
    }

    public void Dispose() => _db.Dispose();

    private sealed class SpyAccessPolicy(bool allow) : ISchoolCommercialAccessPolicy
    {
        public Task<bool> CanManageAsync(int actorUserId, int schoolUserId, CancellationToken ct = default) =>
            Task.FromResult(allow);
    }
}
