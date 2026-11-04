using Cale.Api.Extensions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

/// <summary>
/// School-owned commercial presence. Always scoped to the authenticated school.
/// </summary>
[ApiController]
[Authorize(Policy = "SchoolOnly")]
[Route("api/school/commercial-profile")]
public sealed class SchoolCommercialController : ControllerBase
{
    private readonly ISchoolCommercialProfileService _profiles;
    private readonly ISchoolCommercialContentService _content;

    public SchoolCommercialController(
        ISchoolCommercialProfileService profiles,
        ISchoolCommercialContentService content)
    {
        _profiles = profiles;
        _content = content;
    }

    [HttpGet]
    public Task<SchoolCommercialProfileDto> Get(CancellationToken ct) =>
        _profiles.GetOwnAsync(CurrentUser.GetId(User), ct);

    [HttpPut]
    public Task<SchoolCommercialProfileDto> Put(
        [FromBody] UpdateSchoolCommercialProfileRequest body,
        CancellationToken ct) =>
        _profiles.UpdateOwnAsync(CurrentUser.GetId(User), body, ct);

    [HttpPost("publication")]
    public Task<SchoolCommercialProfileDto> SetPublication(
        [FromBody] PublishSchoolCommercialProfileRequest body,
        CancellationToken ct) =>
        _profiles.SetPublicationAsync(CurrentUser.GetId(User), body.Publish, ct);

    [HttpGet("posts")]
    public Task<IReadOnlyList<SchoolCommercialPostAdminDto>> Posts(CancellationToken ct) =>
        _content.ListPostsAsync(CurrentUser.GetId(User), ct);

    [HttpPost("posts")]
    public Task<SchoolCommercialPostAdminDto> CreatePost(
        [FromBody] UpsertSchoolCommercialPostRequest body,
        CancellationToken ct) =>
        _content.UpsertPostAsync(CurrentUser.GetId(User), null, body, ct);

    [HttpPut("posts/{postId:int}")]
    public Task<SchoolCommercialPostAdminDto> UpdatePost(
        int postId,
        [FromBody] UpsertSchoolCommercialPostRequest body,
        CancellationToken ct) =>
        _content.UpsertPostAsync(CurrentUser.GetId(User), postId, body, ct);

    [HttpPost("posts/{postId:int}/publication")]
    public Task<SchoolCommercialPostAdminDto> PublishPost(
        int postId,
        [FromBody] PublishSchoolCommercialProfileRequest body,
        CancellationToken ct) =>
        _content.SetPostStatusAsync(CurrentUser.GetId(User), postId, body.Publish, ct);

    [HttpDelete("posts/{postId:int}")]
    public async Task<IActionResult> DeletePost(int postId, CancellationToken ct)
    {
        await _content.DeletePostAsync(CurrentUser.GetId(User), postId, ct);
        return NoContent();
    }

    [HttpGet("promotions")]
    public Task<IReadOnlyList<SchoolCommercialPromotionAdminDto>> Promotions(CancellationToken ct) =>
        _content.ListPromotionsAsync(CurrentUser.GetId(User), ct);

    [HttpPost("promotions")]
    public Task<SchoolCommercialPromotionAdminDto> CreatePromotion(
        [FromBody] UpsertSchoolCommercialPromotionRequest body,
        CancellationToken ct) =>
        _content.UpsertPromotionAsync(CurrentUser.GetId(User), null, body, ct);

    [HttpPut("promotions/{promoId:int}")]
    public Task<SchoolCommercialPromotionAdminDto> UpdatePromotion(
        int promoId,
        [FromBody] UpsertSchoolCommercialPromotionRequest body,
        CancellationToken ct) =>
        _content.UpsertPromotionAsync(CurrentUser.GetId(User), promoId, body, ct);

    [HttpPost("promotions/{promoId:int}/publication")]
    public Task<SchoolCommercialPromotionAdminDto> PublishPromotion(
        int promoId,
        [FromBody] PublishSchoolCommercialProfileRequest body,
        CancellationToken ct) =>
        _content.SetPromotionStatusAsync(CurrentUser.GetId(User), promoId, body.Publish, ct);

    [HttpDelete("promotions/{promoId:int}")]
    public async Task<IActionResult> DeletePromotion(int promoId, CancellationToken ct)
    {
        await _content.DeletePromotionAsync(CurrentUser.GetId(User), promoId, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/commercial")]
public sealed class AdminCommercialController : ControllerBase
{
    private readonly ISchoolCommercialProfileService _profiles;

    public AdminCommercialController(ISchoolCommercialProfileService profiles) =>
        _profiles = profiles;

    [HttpGet("schools/{schoolUserId:int}")]
    public Task<AdminCommercialPresenceDto> Get(int schoolUserId, CancellationToken ct) =>
        _profiles.GetAdminViewAsync(schoolUserId, ct);

    [HttpPost("schools/{schoolUserId:int}/suspend")]
    public async Task<AdminCommercialPresenceDto> Suspend(
        int schoolUserId,
        [FromBody] AdminCommercialSuspendRequest body,
        CancellationToken ct)
    {
        await _profiles.SuspendByAdminAsync(schoolUserId, body.Reason, ct);
        return await _profiles.GetAdminViewAsync(schoolUserId, ct);
    }

    [HttpPost("schools/{schoolUserId:int}/unsuspend")]
    public async Task<AdminCommercialPresenceDto> Unsuspend(int schoolUserId, CancellationToken ct)
    {
        await _profiles.ClearSuspensionByAdminAsync(schoolUserId, ct);
        return await _profiles.GetAdminViewAsync(schoolUserId, ct);
    }
}
