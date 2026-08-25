using Cale.Api.Extensions;
using Cale.Modules.Classroom.Application.Commands;
using Cale.Modules.Classroom.Application.DTOs;
using Cale.Modules.Classroom.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/classroom")]
public sealed class ClassroomController : ControllerBase
{
    private readonly ClassroomQueryHandler _queries;
    private readonly ClassroomContentHandler _content;

    public ClassroomController(
        ClassroomQueryHandler queries,
        ClassroomContentHandler content)
    {
        _queries = queries;
        _content = content;
    }

    [HttpGet("{groupId:int}/announcements")]
    public async Task<IActionResult> Announcements(int groupId, CancellationToken ct) =>
        Ok(await _queries.ListAnnouncementsAsync(
            groupId, CurrentUser.GetId(User), CurrentUser.GetRole(User), ct));

    [HttpPost("{groupId:int}/announcements")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> PublishAnnouncement(
        int groupId,
        SaveAnnouncementRequest request,
        CancellationToken ct)
    {
        await _content.PublishAnnouncementAsync(
            groupId, request, CurrentUser.GetId(User), CurrentUser.IsAdmin(User), ct);
        return NoContent();
    }

    [HttpGet("{groupId:int}/materials")]
    public async Task<IActionResult> Materials(int groupId, CancellationToken ct) =>
        Ok(await _queries.ListMaterialsAsync(
            groupId, CurrentUser.GetId(User), CurrentUser.GetRole(User), ct));

    [HttpPost("{groupId:int}/materials")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> PublishMaterial(
        int groupId,
        SaveMaterialRequest request,
        CancellationToken ct)
    {
        await _content.PublishMaterialAsync(
            groupId, request, CurrentUser.GetId(User), CurrentUser.IsAdmin(User), ct);
        return NoContent();
    }

    [HttpGet("{groupId:int}/activities")]
    public async Task<IActionResult> Activities(int groupId, CancellationToken ct) =>
        Ok(await _queries.ListActivitiesAsync(
            groupId, CurrentUser.GetId(User), CurrentUser.GetRole(User), ct));

    [HttpPost("{groupId:int}/activities")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> PublishActivity(
        int groupId,
        SaveActivityRequest request,
        CancellationToken ct)
    {
        await _content.PublishActivityAsync(
            groupId, request, CurrentUser.GetId(User), CurrentUser.IsAdmin(User), ct);
        return NoContent();
    }

    [HttpGet("activities/{activityId:int}/submissions")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Submissions(int activityId, CancellationToken ct) =>
        Ok(await _queries.ListSubmissionsAsync(
            activityId, CurrentUser.GetId(User), CurrentUser.GetRole(User), ct));

    [HttpPost("activities/{activityId:int}/submit")]
    public async Task<IActionResult> Submit(
        int activityId,
        SubmitActivityRequest request,
        CancellationToken ct)
    {
        await _content.SubmitAsync(activityId, request, CurrentUser.GetId(User), ct);
        return NoContent();
    }

    [HttpPost("activities/{activityId:int}/submissions/{studentId:int}/grade")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Grade(
        int activityId,
        int studentId,
        GradeSubmissionRequest request,
        CancellationToken ct)
    {
        await _content.GradeAsync(
            activityId,
            studentId,
            request,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct);
        return NoContent();
    }
}
