using Cale.Modules.Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Engagement.Infrastructure.Persistence;

public sealed class PushSubscriptionConfiguration
    : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("SuscripcionesPush");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Endpoint).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.P256dh).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Auth).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UserAgent).HasMaxLength(300);
        builder.HasIndex(x => x.Endpoint).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

public sealed class PushVapidKeysConfiguration
    : IEntityTypeConfiguration<PushVapidKeys>
{
    public void Configure(EntityTypeBuilder<PushVapidKeys> builder)
    {
        builder.ToTable("ClavesPush");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PublicKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PrivateKey).HasMaxLength(200).IsRequired();
    }
}
