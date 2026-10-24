using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Engagement.Domain;
using Cale.Modules.Identity.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cale.Api.Services.Shop;

public sealed class ShopService
{
    private readonly CaleDbContext _db;
    private readonly SchoolPaymentOptions _payment;
    private readonly IClock _clock;

    public ShopService(
        CaleDbContext db,
        IOptions<SchoolPaymentOptions> payment,
        IClock clock)
    {
        _db = db;
        _payment = payment.Value;
        _clock = clock;
    }

    public async Task<PublicShopDto> GetPublicAsync(CancellationToken ct = default)
    {
        var settings = await EnsureSettingsAsync(ct);
        var products = await _db.Set<ShopProduct>()
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);

        var whatsApp = ResolveWhatsApp(settings);
        return new PublicShopDto(
            settings.PageTitle,
            settings.PageSubtitle,
            settings.HeroImageUrl,
            settings.CtaLabel,
            whatsApp,
            !string.IsNullOrWhiteSpace(whatsApp),
            products.Select(ToPublicProduct).ToList());
    }

    public async Task<PublicShopProductDto?> GetPublicBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = (slug ?? "").Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return null;
        }

        var product = await _db.Set<ShopProduct>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsActive && p.Slug == normalized, ct);

        return product is null ? null : ToPublicProduct(product);
    }

    public async Task<AdminShopDto> GetAdminAsync(CancellationToken ct = default)
    {
        var settings = await EnsureSettingsAsync(ct);
        var products = await _db.Set<ShopProduct>()
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);

        return new AdminShopDto(
            ToSettingsDto(settings),
            _payment.WhatsApp?.Trim(),
            products.Select(ToAdminProduct).ToList());
    }

    public async Task<AdminShopDto> SaveSettingsAsync(UpdateShopSettingsRequest body, CancellationToken ct = default)
    {
        var settings = await EnsureSettingsAsync(ct);
        settings.Update(
            body.PageTitle,
            body.PageSubtitle,
            body.HeroImageUrl,
            body.WhatsApp,
            body.CtaLabel,
            _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return await GetAdminAsync(ct);
    }

    public async Task<AdminShopProductDto> CreateProductAsync(
        UpsertShopProductRequest body,
        CancellationToken ct = default)
    {
        await EnsureSettingsAsync(ct);
        var count = await _db.Set<ShopProduct>().CountAsync(ct);
        if (count >= 24)
        {
            throw new DomainException(
                "Puedes tener hasta 24 productos en el catálogo. Archiva o elimina alguno antes de añadir más.",
                400,
                "shop_limit");
        }

        var product = ShopProduct.Create(
            body.Name,
            body.ShortDescription,
            body.Description,
            ResolveImageUrls(body),
            body.PriceCop,
            body.CompareAtPriceCop,
            body.PromoLabel,
            body.Category,
            body.Sizes,
            body.Stock,
            body.IsActive,
            body.IsFeatured,
            body.SortOrder,
            _clock.UtcNow,
            body.Brand,
            body.Reference,
            body.Colors,
            body.ProductType);

        await EnsureUniqueSlugAsync(product, ct);
        _db.Set<ShopProduct>().Add(product);
        await _db.SaveChangesAsync(ct);
        return ToAdminProduct(product);
    }

    public async Task<AdminShopProductDto> UpdateProductAsync(
        int id,
        UpsertShopProductRequest body,
        CancellationToken ct = default)
    {
        var product = await _db.Set<ShopProduct>().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new DomainException("Producto no encontrado.", 404, "product_not_found");

        product.Update(
            body.Name,
            body.ShortDescription,
            body.Description,
            ResolveImageUrls(body),
            body.PriceCop,
            body.CompareAtPriceCop,
            body.PromoLabel,
            body.Category,
            body.Sizes,
            body.Stock,
            body.IsActive,
            body.IsFeatured,
            body.SortOrder,
            _clock.UtcNow,
            body.Brand,
            body.Reference,
            body.Colors,
            body.ProductType);

        await EnsureUniqueSlugAsync(product, ct);
        await _db.SaveChangesAsync(ct);
        return ToAdminProduct(product);
    }

    public async Task DeleteProductAsync(int id, CancellationToken ct = default)
    {
        var product = await _db.Set<ShopProduct>().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return;
        }

        _db.Set<ShopProduct>().Remove(product);
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueSlugAsync(ShopProduct product, CancellationToken ct)
    {
        var baseSlug = product.Slug;
        var slug = baseSlug;
        var n = 2;
        while (await _db.Set<ShopProduct>()
                   .AnyAsync(p => p.Slug == slug && p.Id != product.Id, ct))
        {
            slug = $"{baseSlug}-{n}";
            n++;
            if (n > 50)
            {
                throw new DomainException(
                    "No se pudo generar una URL única para este producto.",
                    400,
                    "duplicate_slug");
            }
        }

        if (slug != product.Slug)
        {
            product.MakeSlugUnique(slug);
        }
    }

    private async Task<ShopSettings> EnsureSettingsAsync(CancellationToken ct)
    {
        var settings = await _db.Set<ShopSettings>()
            .FirstOrDefaultAsync(s => s.Id == ShopSettings.SingletonId, ct);
        if (settings is not null)
        {
            return settings;
        }

        settings = ShopSettings.CreateDefault(_clock.UtcNow);
        _db.Set<ShopSettings>().Add(settings);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.Entry(settings).State = EntityState.Detached;
            settings = await _db.Set<ShopSettings>()
                .FirstAsync(s => s.Id == ShopSettings.SingletonId, ct);
        }

        return settings;
    }

    private string? ResolveWhatsApp(ShopSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.WhatsApp))
        {
            return settings.WhatsApp;
        }

        var fallback = _payment.WhatsApp?.Trim();
        if (string.IsNullOrWhiteSpace(fallback))
        {
            return null;
        }

        var digits = new string(fallback.Where(char.IsDigit).ToArray());
        return digits.Length is >= 10 and <= 15 ? digits : null;
    }

    private static IReadOnlyList<string> ResolveImageUrls(UpsertShopProductRequest body)
    {
        if (body.ImageUrls is { Count: > 0 })
        {
            return body.ImageUrls;
        }

        return string.IsNullOrWhiteSpace(body.ImageUrl) ? [] : [body.ImageUrl];
    }

    private static PublicShopProductDto ToPublicProduct(ShopProduct p) =>
        new(
            p.Id,
            p.Name,
            p.Slug,
            p.ShortDescription,
            p.Description,
            p.ImageUrl,
            p.ImageUrls,
            p.PriceCop,
            p.CompareAtPriceCop,
            p.PromoLabel,
            p.DiscountPercent,
            p.Category,
            p.ProductType,
            p.Brand,
            p.Reference,
            p.Sizes,
            p.Colors,
            p.Stock,
            p.IsFeatured,
            p.HasPromo,
            p.CreatedAt);

    private static AdminShopProductDto ToAdminProduct(ShopProduct p) =>
        new(
            p.Id,
            p.Name,
            p.Slug,
            p.ShortDescription,
            p.Description,
            p.ImageUrl,
            p.ImageUrls,
            p.PriceCop,
            p.CompareAtPriceCop,
            p.PromoLabel,
            p.Category,
            p.ProductType,
            p.Brand,
            p.Reference,
            p.Sizes,
            p.Colors,
            p.Stock,
            p.IsActive,
            p.IsFeatured,
            p.SortOrder,
            p.CreatedAt,
            p.UpdatedAt);

    private static ShopSettingsDto ToSettingsDto(ShopSettings s) =>
        new(s.PageTitle, s.PageSubtitle, s.HeroImageUrl, s.WhatsApp, s.CtaLabel, s.UpdatedAt);
}

public sealed record PublicShopDto(
    string PageTitle,
    string PageSubtitle,
    string? HeroImageUrl,
    string CtaLabel,
    string? WhatsApp,
    bool CanOrder,
    IReadOnlyList<PublicShopProductDto> Products);

public sealed record PublicShopProductDto(
    int Id,
    string Name,
    string Slug,
    string ShortDescription,
    string? Description,
    string? ImageUrl,
    IReadOnlyList<string> ImageUrls,
    decimal PriceCop,
    decimal? CompareAtPriceCop,
    string? PromoLabel,
    int? DiscountPercent,
    string Category,
    string? ProductType,
    string? Brand,
    string? Reference,
    string? Sizes,
    string? Colors,
    int Stock,
    bool IsFeatured,
    bool HasPromo,
    DateTime CreatedAt);

public sealed record AdminShopDto(
    ShopSettingsDto Settings,
    string? PlatformWhatsAppFallback,
    IReadOnlyList<AdminShopProductDto> Products);

public sealed record ShopSettingsDto(
    string PageTitle,
    string PageSubtitle,
    string? HeroImageUrl,
    string? WhatsApp,
    string CtaLabel,
    DateTime UpdatedAt);

public sealed record AdminShopProductDto(
    int Id,
    string Name,
    string Slug,
    string ShortDescription,
    string? Description,
    string? ImageUrl,
    IReadOnlyList<string> ImageUrls,
    decimal PriceCop,
    decimal? CompareAtPriceCop,
    string? PromoLabel,
    string Category,
    string? ProductType,
    string? Brand,
    string? Reference,
    string? Sizes,
    string? Colors,
    int Stock,
    bool IsActive,
    bool IsFeatured,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record UpdateShopSettingsRequest(
    string PageTitle,
    string PageSubtitle,
    string? HeroImageUrl,
    string? WhatsApp,
    string CtaLabel);

public sealed record UpsertShopProductRequest(
    string Name,
    string ShortDescription,
    string? Description,
    string? ImageUrl,
    IReadOnlyList<string>? ImageUrls,
    decimal PriceCop,
    decimal? CompareAtPriceCop,
    string? PromoLabel,
    string? Category,
    string? ProductType,
    string? Brand,
    string? Reference,
    string? Sizes,
    string? Colors,
    int Stock,
    bool IsActive,
    bool IsFeatured,
    int SortOrder);
