using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class SchoolJoinRequestStore : ISchoolJoinRequestStore
{
    private readonly CaleDbContext _db;

    public SchoolJoinRequestStore(CaleDbContext db) => _db = db;

    public async Task AddAsync(SchoolJoinRequest request, CancellationToken ct = default) =>
        await _db.Set<SchoolJoinRequest>().AddAsync(request, ct);

    public Task<SchoolJoinRequest?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Set<SchoolJoinRequest>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<SchoolJoinRequest?> FindPendingAsync(
        int teacherUserId,
        int schoolUserId,
        CancellationToken ct = default) =>
        _db.Set<SchoolJoinRequest>().FirstOrDefaultAsync(
            x => x.TeacherUserId == teacherUserId
                && x.SchoolUserId == schoolUserId
                && x.Status == SchoolJoinRequestStatuses.Pending,
            ct);

    public async Task<IReadOnlyList<SchoolJoinRequest>> ListPendingBySchoolAsync(
        int schoolUserId,
        CancellationToken ct = default) =>
        await _db.Set<SchoolJoinRequest>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.Status == SchoolJoinRequestStatuses.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SchoolJoinRequest>> ListByTeacherAsync(
        int teacherUserId,
        CancellationToken ct = default) =>
        await _db.Set<SchoolJoinRequest>()
            .Where(x => x.TeacherUserId == teacherUserId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(30)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);
}

public sealed class SchoolJoinRequestConfiguration : IEntityTypeConfiguration<SchoolJoinRequest>
{
    public void Configure(EntityTypeBuilder<SchoolJoinRequest> builder)
    {
        builder.ToTable("SchoolJoinRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(500);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.HasIndex(x => new { x.SchoolUserId, x.Status });
        builder.HasIndex(x => new { x.TeacherUserId, x.Status });
    }
}
