using Cale.Modules.Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Engagement.Infrastructure.Persistence;

public sealed class ShopSettingsConfiguration : IEntityTypeConfiguration<ShopSettings>
{
    public void Configure(EntityTypeBuilder<ShopSettings> builder)
    {
        builder.ToTable("ShopSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PageTitle).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PageSubtitle).HasMaxLength(500).IsRequired();
        builder.Property(x => x.HeroImageUrl).HasMaxLength(500);
        builder.Property(x => x.WhatsApp).HasMaxLength(20);
        builder.Property(x => x.CtaLabel).HasMaxLength(80).IsRequired();
    }
}

public sealed class ShopProductConfiguration : IEntityTypeConfiguration<ShopProduct>
{
    public void Configure(EntityTypeBuilder<ShopProduct> builder)
    {
        builder.ToTable("ShopProducts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(ShopProduct.NameMax).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(140).IsRequired();
        builder.Property(x => x.ShortDescription).HasMaxLength(280).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(ShopProduct.DescriptionMax);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.ImageUrlsJson).HasColumnType("text");
        builder.Property(x => x.PriceCop).HasPrecision(18, 2);
        builder.Property(x => x.CompareAtPriceCop).HasPrecision(18, 2);
        builder.Property(x => x.PromoLabel).HasMaxLength(ShopProduct.PromoMax);
        builder.Property(x => x.Category).HasMaxLength(ShopProduct.CategoryMax).IsRequired();
        builder.Property(x => x.ProductType).HasMaxLength(ShopProduct.ProductTypeMax);
        builder.Property(x => x.Brand).HasMaxLength(ShopProduct.BrandMax);
        builder.Property(x => x.Reference).HasMaxLength(ShopProduct.ReferenceMax);
        builder.Property(x => x.Sizes).HasMaxLength(ShopProduct.SizesMax);
        builder.Property(x => x.Colors).HasMaxLength(ShopProduct.ColorsMax);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.SortOrder);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Ignore(x => x.HasPromo);
        builder.Ignore(x => x.DiscountPercent);
        builder.Ignore(x => x.ImageUrls);
    }
}
