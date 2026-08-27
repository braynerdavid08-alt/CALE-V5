using Cale.Api.Extensions;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/users")]
public sealed class UsersController : ControllerBase
{
    private readonly ListUsersHandler _list;
    private readonly CreateTeacherHandler _createTeacher;
    private readonly CreateSchoolHandler _createSchool;
    private readonly UpdateUserHandler _update;
    private readonly DeleteUserHandler _delete;
    private readonly SetUserActiveHandler _setActive;

    public UsersController(
        ListUsersHandler list,
        CreateTeacherHandler createTeacher,
        CreateSchoolHandler createSchool,
        UpdateUserHandler update,
        DeleteUserHandler delete,
        SetUserActiveHandler setActive)
    {
        _list = list;
        _createTeacher = createTeacher;
        _createSchool = createSchool;
        _update = update;
        _delete = delete;
        _setActive = setActive;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> List(
        CancellationToken ct) =>
        Ok(await _list.HandleAsync(ct));

    [HttpPost("teachers")]
    public async Task<ActionResult<UserListItemDto>> CreateTeacher(
        CreateTeacherRequest request,
        CancellationToken ct) =>
        Ok(await _createTeacher.HandleAsync(request, ct));

    [HttpPost("schools")]
    public async Task<ActionResult<UserListItemDto>> CreateSchool(
        CreateSchoolRequest request,
        CancellationToken ct) =>
        Ok(await _createSchool.HandleAsync(
            request,
            CurrentUser.GetId(User),
            ct));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserListItemDto>> Update(
        int id,
        UpdateUserRequest request,
        CancellationToken ct) =>
        Ok(await _update.HandleAsync(
            CurrentUser.GetId(User),
            id,
            request,
            ct));

    [HttpPatch("{id:int}/active")]
    public async Task<ActionResult<UserListItemDto>> SetActive(
        int id,
        SetUserActiveRequest request,
        CancellationToken ct) =>
        Ok(await _setActive.HandleAsync(
            CurrentUser.GetId(User),
            id,
            request,
            ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _delete.HandleAsync(CurrentUser.GetId(User), id, ct);
        return NoContent();
    }
}
