using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Engagement.Domain;

namespace Cale.UnitTests.Shop;

public sealed class ShopProductTests
{
    [Fact]
    public void Create_computes_promo_and_slug()
    {
        var product = ShopProduct.Create(
            "Casco Integral Pro X",
            "Casco homologado con visor solar.",
            null,
            "/uploads/casco.webp",
            220_000,
            280_000,
            "Oferta",
            "Cascos",
            "S, M, L",
            4,
            true,
            true,
            1,
            DateTime.UtcNow);

        Assert.Equal("casco-integral-pro-x", product.Slug);
        Assert.True(product.HasPromo);
        Assert.Equal(21, product.DiscountPercent);
    }

    [Fact]
    public void Create_rejects_compare_price_below_sale_price()
    {
        var ex = Assert.Throws<DomainException>(() => ShopProduct.Create(
            "Casco",
            "Descripción corta del casco.",
            null,
            null,
            200_000,
            150_000,
            null,
            "Cascos",
            null,
            1,
            true,
            false,
            0,
            DateTime.UtcNow));

        Assert.Equal("invalid_compare_price", ex.ErrorCode);
    }

    [Fact]
    public void Settings_normalize_whatsapp_digits()
    {
        var settings = ShopSettings.CreateDefault(DateTime.UtcNow);
        settings.Update(
            "Cascos Luz Verde",
            "Protección para motociclistas.",
            null,
            "+57 300 123 4567",
            "Pedir por WhatsApp",
            DateTime.UtcNow);

        Assert.Equal("573001234567", settings.WhatsApp);
    }
}
