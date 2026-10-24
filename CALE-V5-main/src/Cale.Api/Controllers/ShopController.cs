using Cale.Api.Services.Shop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/public/shop")]
[AllowAnonymous]
public sealed class PublicShopController : ControllerBase
{
    private readonly ShopService _shop;

    public PublicShopController(ShopService shop) => _shop = shop;

    [HttpGet]
    public Task<PublicShopDto> Get(CancellationToken ct) => _shop.GetPublicAsync(ct);

    [HttpGet("{slug}")]
    public async Task<ActionResult<PublicShopProductDto>> GetBySlug(string slug, CancellationToken ct)
    {
        var product = await _shop.GetPublicBySlugAsync(slug, ct);
        return product is null ? NotFound() : product;
    }
}

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/shop")]
public sealed class AdminShopController : ControllerBase
{
    private readonly ShopService _shop;

    public AdminShopController(ShopService shop) => _shop = shop;

    [HttpGet]
    public Task<AdminShopDto> Get(CancellationToken ct) => _shop.GetAdminAsync(ct);

    [HttpPut("settings")]
    public Task<AdminShopDto> PutSettings([FromBody] UpdateShopSettingsRequest body, CancellationToken ct) =>
        _shop.SaveSettingsAsync(body, ct);

    [HttpPost("products")]
    public Task<AdminShopProductDto> CreateProduct(
        [FromBody] UpsertShopProductRequest body,
        CancellationToken ct) =>
        _shop.CreateProductAsync(body, ct);

    [HttpPut("products/{id:int}")]
    public Task<AdminShopProductDto> UpdateProduct(
        int id,
        [FromBody] UpsertShopProductRequest body,
        CancellationToken ct) =>
        _shop.UpdateProductAsync(id, body, ct);

    [HttpDelete("products/{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        await _shop.DeleteProductAsync(id, ct);
        return NoContent();
    }
}
