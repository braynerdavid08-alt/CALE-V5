using System.Net;
using System.Net.Http.Json;

namespace Cale.UnitTests.Security.Integration;

public sealed class ShopApiTests(SecurityApiFixture fixture) : IClassFixture<SecurityApiFixture>
{
    private readonly SecurityApiFactory _api = fixture.Factory;

    [Fact]
    public async Task Public_shop_starts_empty_and_admin_can_manage_catalog()
    {
        using var anon = _api.ClientFor(null);
        using var asAdmin = _api.ClientFor(_api.Users.Admin);
        using var asStudent = _api.ClientFor(_api.Users.StudentA);

        var publicEmpty = await anon.GetFromJsonAsync<PublicShop>("/api/public/shop");
        Assert.NotNull(publicEmpty);
        Assert.Empty(publicEmpty.Products);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await asStudent.GetAsync("/api/admin/shop")).StatusCode);

        var created = await asAdmin.PostAsJsonAsync("/api/admin/shop/products", new
        {
            name = "Casco Integral A2",
            shortDescription = "Casco homologado para categoría A2.",
            description = "Visor transparente y calota ABS.",
            imageUrl = (string?)null,
            imageUrls = new[] { "/uploads/a.webp", "/uploads/b.webp" },
            priceCop = 189000,
            compareAtPriceCop = 230000,
            promoLabel = "Nuevo",
            category = "Cascos",
            sizes = "M, L, XL",
            stock = 6,
            isActive = true,
            isFeatured = true,
            sortOrder = 1
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var product = await created.Content.ReadFromJsonAsync<AdminProduct>();
        Assert.NotNull(product);
        Assert.True(product.Id > 0);

        (await asAdmin.PutAsJsonAsync("/api/admin/shop/settings", new
        {
            pageTitle = "Cascos Luz Verde",
            pageSubtitle = "Pide por WhatsApp y te confirmamos disponibilidad.",
            heroImageUrl = (string?)null,
            whatsApp = "573001112233",
            ctaLabel = "Pedir por WhatsApp"
        })).EnsureSuccessStatusCode();

        var publicShop = await anon.GetFromJsonAsync<PublicShop>("/api/public/shop");
        Assert.NotNull(publicShop);
        Assert.Equal("Cascos Luz Verde", publicShop.PageTitle);
        Assert.True(publicShop.CanOrder);
        Assert.Equal("573001112233", publicShop.WhatsApp);
        Assert.Single(publicShop.Products);
        Assert.Equal("Casco Integral A2", publicShop.Products[0].Name);
        Assert.True(publicShop.Products[0].HasPromo);
        Assert.Equal(2, publicShop.Products[0].ImageUrls.Count);
        Assert.Equal("/uploads/a.webp", publicShop.Products[0].ImageUrl);

        (await asAdmin.PutAsJsonAsync($"/api/admin/shop/products/{product.Id}", new
        {
            name = product.Name,
            shortDescription = product.ShortDescription,
            description = product.Description,
            imageUrl = product.ImageUrl,
            imageUrls = product.ImageUrls,
            priceCop = product.PriceCop,
            compareAtPriceCop = product.CompareAtPriceCop,
            promoLabel = product.PromoLabel,
            category = product.Category,
            sizes = product.Sizes,
            stock = product.Stock,
            isActive = false,
            isFeatured = product.IsFeatured,
            sortOrder = product.SortOrder
        })).EnsureSuccessStatusCode();

        var hidden = await anon.GetFromJsonAsync<PublicShop>("/api/public/shop");
        Assert.NotNull(hidden);
        Assert.Empty(hidden.Products);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await asAdmin.DeleteAsync($"/api/admin/shop/products/{product.Id}")).StatusCode);
    }

    private sealed record PublicShop(
        string PageTitle,
        string PageSubtitle,
        string? HeroImageUrl,
        string CtaLabel,
        string? WhatsApp,
        bool CanOrder,
        IReadOnlyList<PublicProduct> Products);

    private sealed record PublicProduct(
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
        string? Sizes,
        int Stock,
        bool IsFeatured,
        bool HasPromo);

    private sealed record AdminProduct(
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
        string? Sizes,
        int Stock,
        bool IsActive,
        bool IsFeatured,
        int SortOrder,
        DateTime UpdatedAt);
}
