using Cale.Api.Extensions;
using Cale.Modules.Assessment.Application.Queries;
using Cale.Modules.Classroom.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class DashboardsController : ControllerBase
{
    private readonly ClassroomQueryHandler _classroom;
    private readonly ListResultsHandler _results;

    public DashboardsController(
        ClassroomQueryHandler classroom,
        ListResultsHandler results)
    {
        _classroom = classroom;
        _results = results;
    }

    [HttpGet("student/dashboard")]
    public async Task<IActionResult> Student(CancellationToken ct) =>
        Ok(await _classroom.StudentDashboardAsync(
            CurrentUser.GetId(User),
            User.Identity?.Name ?? "",
            ct));

    [HttpGet("teacher/dashboard")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Teacher(CancellationToken ct) =>
        Ok(await _classroom.TeacherDashboardAsync(CurrentUser.GetId(User), ct));

    [HttpGet("admin/dashboard")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Admin(CancellationToken ct) =>
        Ok(await _classroom.AdminDashboardAsync(ct));

    [HttpGet("student/results")]
    public async Task<IActionResult> MyResults(CancellationToken ct) =>
        Ok(await _results.HandleAsync(CurrentUser.GetId(User), null, ct));

    [HttpGet("admin/results")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AllResults(CancellationToken ct) =>
        Ok(await _results.HandleAsync(null, null, ct));

    [HttpGet("teacher/results")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> TeacherResults(CancellationToken ct)
    {
        var dashboard = await _classroom.TeacherDashboardAsync(
            CurrentUser.GetId(User),
            ct);
        var userIds = dashboard.LowScores
            .Select(x => x.UserId)
            .Concat(dashboard.PendingGrades.Select(x => x.UserId))
            .Distinct()
            .ToList();

        // Prefer members of teacher's groups for a complete view.
        var memberIds = new HashSet<int>();
        foreach (var group in dashboard.Groups)
        {
            var members = await _classroom.ListMembersAsync(
                group.Id,
                CurrentUser.GetId(User),
                CurrentUser.GetRole(User),
                ct);
            foreach (var member in members)
            {
                memberIds.Add(member.UserId);
            }
        }

        var ids = memberIds.Count > 0 ? memberIds.ToList() : userIds;
        return Ok(await _results.HandleAsync(null, ids, ct));
    }
}
