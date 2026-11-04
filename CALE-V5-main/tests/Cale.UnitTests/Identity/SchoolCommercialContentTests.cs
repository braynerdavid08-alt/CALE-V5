using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Services;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Cale.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace Cale.UnitTests.Identity;

public sealed class SchoolCommercialContentTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 11, 4, 18, 0, 0, DateTimeKind.Utc);

    private readonly CaleDbContext _db;
    private readonly FakeClock _clock = new(T0);
    private readonly SchoolCommercialContentService _content;
    private readonly SchoolCommercialProfileService _profiles;

    public SchoolCommercialContentTests()
    {
        var options = new DbContextOptionsBuilder<CaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CaleDbContext(options, new MappingAssemblies(typeof(UserConfiguration).Assembly));
        _db.Database.EnsureCreated();
        var store = new SchoolProfileStore(_db);
        var gate = new SchoolCommercialPresenceGate();
        var access = new OwnerOnlySchoolCommercialAccessPolicy();
        _content = new SchoolCommercialContentService(_db, _clock, gate, access);
        _profiles = new SchoolCommercialProfileService(_db, store, _clock, gate, access);
    }

    [Fact]
    public async Task Admin_suspension_hides_commercial_presence()
    {
        await SeedEligibleAsync(9);
        await _profiles.UpdateOwnAsync(
            9,
            new UpdateSchoolCommercialProfileRequest(
                "Formación vial con flota propia, instructores certificados y acompañamiento en ruta.",
                "A2, B1",
                Guid.NewGuid(),
                null));
        await _profiles.SetPublicationAsync(9, true);

        await _profiles.SuspendByAdminAsync(9, "Contenido engañoso", default);
        var view = await _profiles.GetAdminViewAsync(9);
        Assert.True(view.IsCommerciallySuspended);
        Assert.False(view.PresenceEligible);
        Assert.Equal(SchoolCommercialPresenceDenyReasons.CommerciallySuspended, view.PresenceDenyReason);
    }

    [Fact]
    public async Task Posts_and_promotions_are_school_scoped()
    {
        await SeedEligibleAsync(11);
        var post = await _content.UpsertPostAsync(
            11,
            null,
            new UpsertSchoolCommercialPostRequest("Abrimos inscripción para A2 este mes.", null));
        Assert.Equal(SchoolCommercialContentStatuses.Draft, post.Status);

        var published = await _content.SetPostStatusAsync(11, post.Id, true);
        Assert.Equal(SchoolCommercialContentStatuses.Published, published.Status);

        var foreign = await Assert.ThrowsAsync<NotFoundException>(() =>
            _content.SetPostStatusAsync(12, post.Id, false));
        Assert.Equal("commercial_post_not_found", foreign.ErrorCode);
    }

    private async Task SeedEligibleAsync(int userId)
    {
        var plan = SchoolPlans.Find(SchoolPlans.Monthly)!;
        var school = SchoolProfile.Create(
            userId,
            $"CEA {userId}",
            "900123456-8",
            $"s{userId}@test.com",
            "3001234567",
            "Calle 1",
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
}
