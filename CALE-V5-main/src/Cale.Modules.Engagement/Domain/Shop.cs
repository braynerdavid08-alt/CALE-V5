using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Engagement.Domain;

/// <summary>Storefront settings for the public helmet/equipment shop (WhatsApp checkout).</summary>
public sealed class ShopSettings
{
    public const int SingletonId = 1;

    public int Id { get; private set; } = SingletonId;
    public string PageTitle { get; private set; } = "Cascos y equipo";
    public string PageSubtitle { get; private set; } =
        "Protección homologada para motociclistas. Elige tu casco y pide por WhatsApp.";
    public string? HeroImageUrl { get; private set; }
    public string? WhatsApp { get; private set; }
    public string CtaLabel { get; private set; } = "Pedir por WhatsApp";
    public DateTime UpdatedAt { get; private set; }

    private ShopSettings()
    {
    }

    public static ShopSettings CreateDefault(DateTime utcNow) =>
        new() { UpdatedAt = utcNow };

    public void Update(
        string? pageTitle,
        string? pageSubtitle,
        string? heroImageUrl,
        string? whatsApp,
        string? ctaLabel,
        DateTime utcNow)
    {
        PageTitle = Required(pageTitle, 120, "Escribe el título de la tienda.", "invalid_shop_title");
        PageSubtitle = Required(pageSubtitle, 500, "Escribe una descripción corta.", "invalid_shop_subtitle");
        HeroImageUrl = Optional(heroImageUrl, 500);
        WhatsApp = NormalizePhone(whatsApp);
        CtaLabel = Required(ctaLabel, 80, "Escribe el texto del botón.", "invalid_shop_cta");
        UpdatedAt = utcNow;
    }

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 15)
        {
            throw new DomainException(
                "El WhatsApp debe tener entre 10 y 15 dígitos (con indicativo de país, ej. 573001234567).",
                400,
                "invalid_whatsapp");
        }

        return digits;
    }

    private static string Required(string? value, int max, string message, string code)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException(message, 400, code);
        }

        if (trimmed.Length > max)
        {
            throw new DomainException($"El texto es muy largo (máximo {max}).", 400, code);
        }

        return trimmed;
    }

    private static string? Optional(string? value, int max)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > max)
        {
            throw new DomainException($"El texto es muy largo (máximo {max}).", 400, "invalid_shop_image");
        }

        return trimmed;
    }
}

/// <summary>A helmet or motorcycle gear item sold via WhatsApp.</summary>
public sealed class ShopProduct
{
    public const int NameMax = 120;
    public const int DescriptionMax = 2000;
    public const int PromoMax = 40;
    public const int SizesMax = 120;
    public const int CategoryMax = 60;
    public const int BrandMax = 80;
    public const int ReferenceMax = 60;
    public const int ColorsMax = 120;
    public const int ProductTypeMax = 60;
    public const int MaxImages = 8;

    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public string ShortDescription { get; private set; } = "";
    public string? Description { get; private set; }
    /// <summary>Cover image (first of <see cref="ImageUrls"/>).</summary>
    public string? ImageUrl { get; private set; }
    /// <summary>JSON array of image URLs (angles / gallery). Kept in sync with <see cref="ImageUrl"/>.</summary>
    public string? ImageUrlsJson { get; private set; }
    public decimal PriceCop { get; private set; }
    public decimal? CompareAtPriceCop { get; private set; }
    public string? PromoLabel { get; private set; }
    public string Category { get; private set; } = "Cascos";
    /// <summary>Optional subtype (e.g. Integral, Abatible) within the category.</summary>
    public string? ProductType { get; private set; }
    public string? Brand { get; private set; }
    public string? Reference { get; private set; }
    public string? Sizes { get; private set; }
    public string? Colors { get; private set; }
    public int Stock { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsFeatured { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ShopProduct()
    {
    }

    public IReadOnlyList<string> ImageUrls => ParseImageUrls(ImageUrlsJson, ImageUrl);

    public static ShopProduct Create(
        string name,
        string? shortDescription,
        string? description,
        IEnumerable<string?>? imageUrls,
        decimal priceCop,
        decimal? compareAtPriceCop,
        string? promoLabel,
        string? category,
        string? sizes,
        int stock,
        bool isActive,
        bool isFeatured,
        int sortOrder,
        DateTime utcNow,
        string? brand = null,
        string? reference = null,
        string? colors = null,
        string? productType = null)
    {
        var product = new ShopProduct { CreatedAt = utcNow };
        product.Apply(
            name, shortDescription, description, imageUrls, priceCop, compareAtPriceCop,
            promoLabel, category, sizes, stock, isActive, isFeatured, sortOrder, utcNow,
            brand, reference, colors, productType);
        return product;
    }

    public void Update(
        string name,
        string? shortDescription,
        string? description,
        IEnumerable<string?>? imageUrls,
        decimal priceCop,
        decimal? compareAtPriceCop,
        string? promoLabel,
        string? category,
        string? sizes,
        int stock,
        bool isActive,
        bool isFeatured,
        int sortOrder,
        DateTime utcNow,
        string? brand = null,
        string? reference = null,
        string? colors = null,
        string? productType = null) =>
        Apply(
            name, shortDescription, description, imageUrls, priceCop, compareAtPriceCop,
            promoLabel, category, sizes, stock, isActive, isFeatured, sortOrder, utcNow,
            brand, reference, colors, productType);

    public bool HasPromo =>
        !string.IsNullOrWhiteSpace(PromoLabel)
        || (CompareAtPriceCop is { } compare && compare > PriceCop);

    public int? DiscountPercent
    {
        get
        {
            if (CompareAtPriceCop is not { } compare || compare <= PriceCop || compare <= 0)
            {
                return null;
            }

            return (int)Math.Round((1 - (PriceCop / compare)) * 100m);
        }
    }

    private void Apply(
        string name,
        string? shortDescription,
        string? description,
        IEnumerable<string?>? imageUrls,
        decimal priceCop,
        decimal? compareAtPriceCop,
        string? promoLabel,
        string? category,
        string? sizes,
        int stock,
        bool isActive,
        bool isFeatured,
        int sortOrder,
        DateTime utcNow,
        string? brand = null,
        string? reference = null,
        string? colors = null,
        string? productType = null)
    {
        Name = Required(name, NameMax, "Escribe el nombre del producto.", "invalid_product_name");
        ShortDescription = Required(
            shortDescription, 280, "Escribe una descripción corta.", "invalid_product_summary");
        Description = Optional(description, DescriptionMax);
        SetImages(imageUrls);
        if (priceCop is < 1000 or > 50_000_000)
        {
            throw new DomainException("El precio debe estar entre $1.000 y $50.000.000 COP.", 400, "invalid_price");
        }

        if (compareAtPriceCop is { } compare && (compare < priceCop || compare > 50_000_000))
        {
            throw new DomainException(
                "El precio tachado debe ser mayor que el precio de venta.",
                400,
                "invalid_compare_price");
        }

        PriceCop = decimal.Round(priceCop, 0);
        CompareAtPriceCop = compareAtPriceCop is null ? null : decimal.Round(compareAtPriceCop.Value, 0);
        PromoLabel = Optional(promoLabel, PromoMax);
        Category = string.IsNullOrWhiteSpace(category)
            ? "Cascos"
            : Required(category, CategoryMax, "Categoría inválida.", "invalid_category");
        ProductType = Optional(productType, ProductTypeMax);
        Brand = Optional(brand, BrandMax);
        Reference = Optional(reference, ReferenceMax);
        Sizes = Optional(sizes, SizesMax);
        Colors = Optional(colors, ColorsMax);
        if (stock is < 0 or > 10_000)
        {
            throw new DomainException("El stock debe estar entre 0 y 10.000.", 400, "invalid_stock");
        }

        Stock = stock;
        IsActive = isActive;
        IsFeatured = isFeatured;
        SortOrder = Math.Clamp(sortOrder, 0, 10_000);
        Slug = Slugify(Name);
        UpdatedAt = utcNow;
    }

    /// <summary>Used when the auto slug collides with another product.</summary>
    public void MakeSlugUnique(string candidate)
    {
        var cleaned = Slugify(candidate);
        if (cleaned.Length == 0)
        {
            cleaned = Slugify(Name);
        }

        Slug = Required(cleaned, 140, "Slug inválido.", "invalid_slug");
    }

    private void SetImages(IEnumerable<string?>? imageUrls)
    {
        var cleaned = (imageUrls ?? Array.Empty<string?>())
            .Select(u => (u ?? "").Trim())
            .Where(u => u.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxImages)
            .ToList();

        foreach (var url in cleaned)
        {
            if (url.Length > 500)
            {
                throw new DomainException($"La URL de imagen es muy larga (máximo 500).", 400, "invalid_shop_image");
            }
        }

        if (cleaned.Count == 0)
        {
            ImageUrl = null;
            ImageUrlsJson = null;
            return;
        }

        ImageUrl = cleaned[0];
        ImageUrlsJson = System.Text.Json.JsonSerializer.Serialize(cleaned);
    }

    public static IReadOnlyList<string> ParseImageUrls(string? json, string? cover)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (list is { Count: > 0 })
                {
                    return list
                        .Select(u => (u ?? "").Trim())
                        .Where(u => u.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .Take(MaxImages)
                        .ToList();
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Fall through to cover.
            }
        }

        var single = (cover ?? "").Trim();
        return single.Length == 0 ? Array.Empty<string>() : [single];
    }

    private static string Slugify(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var chars = normalized.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private static string Required(string? value, int max, string message, string code)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException(message, 400, code);
        }

        if (trimmed.Length > max)
        {
            throw new DomainException($"El texto es muy largo (máximo {max}).", 400, code);
        }

        return trimmed;
    }

    private static string? Optional(string? value, int max)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > max)
        {
            throw new DomainException($"El texto es muy largo (máximo {max}).", 400, "invalid_product_text");
        }

        return trimmed;
    }
}
