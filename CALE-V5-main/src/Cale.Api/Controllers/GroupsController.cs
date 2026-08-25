using Cale.Api.Extensions;
using Cale.Modules.Classroom.Application.Commands;
using Cale.Modules.Classroom.Application.DTOs;
using Cale.Modules.Classroom.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/groups")]
public sealed class GroupsController : ControllerBase
{
    private readonly GroupCommandHandler _commands;
    private readonly ClassroomQueryHandler _queries;

    public GroupsController(
        GroupCommandHandler commands,
        ClassroomQueryHandler queries)
    {
        _commands = commands;
        _queries = queries;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _queries.ListGroupsAsync(
            CurrentUser.GetId(User),
            CurrentUser.GetRole(User),
            ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(await _queries.GetGroupAsync(
            id,
            CurrentUser.GetId(User),
            CurrentUser.GetRole(User),
            ct));

    [HttpGet("{id:int}/members")]
    public async Task<IActionResult> Members(int id, CancellationToken ct) =>
        Ok(await _queries.ListMembersAsync(
            id,
            CurrentUser.GetId(User),
            CurrentUser.GetRole(User),
            ct));

    [HttpPost]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Create(
        SaveGroupRequest request,
        CancellationToken ct) =>
        Ok(await _commands.CreateAsync(request, CurrentUser.GetId(User), ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> Update(
        int id,
        SaveGroupRequest request,
        CancellationToken ct) =>
        Ok(await _commands.UpdateAsync(
            id,
            request,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct));

    [HttpPost("join")]
    public async Task<IActionResult> Join(
        JoinGroupRequest request,
        CancellationToken ct) =>
        Ok(await _commands.JoinAsync(request, CurrentUser.GetId(User), ct));

    [HttpPost("{id:int}/members")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> AddMember(
        int id,
        AddMemberRequest request,
        CancellationToken ct)
    {
        await _commands.AddMemberAsync(
            id,
            request,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/members/{userId:int}")]
    [Authorize(Policy = "TeacherOrAdmin")]
    public async Task<IActionResult> RemoveMember(
        int id,
        int userId,
        CancellationToken ct)
    {
        await _commands.RemoveMemberAsync(
            id,
            userId,
            CurrentUser.GetId(User),
            CurrentUser.IsAdmin(User),
            ct);
        return NoContent();
    }
}
