using Cale.Api.Extensions;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly LoginUserHandler _login;
    private readonly RegisterUserHandler _register;
    private readonly RegisterTeacherHandler _registerTeacher;
    private readonly RegisterSchoolHandler _registerSchool;
    private readonly ChangePasswordHandler _changePassword;
    private readonly GetCurrentUserHandler _me;
    private readonly UpdateMyProfileHandler _updateMe;
    private readonly ListSchoolPlansHandler _plans;

    public AuthController(
        LoginUserHandler login,
        RegisterUserHandler register,
        RegisterTeacherHandler registerTeacher,
        RegisterSchoolHandler registerSchool,
        ChangePasswordHandler changePassword,
        GetCurrentUserHandler me,
        UpdateMyProfileHandler updateMe,
        ListSchoolPlansHandler plans)
    {
        _login = login;
        _register = register;
        _registerTeacher = registerTeacher;
        _registerSchool = registerSchool;
        _changePassword = changePassword;
        _me = me;
        _updateMe = updateMe;
        _plans = plans;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken ct) =>
        Ok(await _login.HandleAsync(request, ct));

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken ct) =>
        Ok(await _register.HandleAsync(request, ct));

    [HttpPost("register-teacher")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RegisterTeacher(
        RegisterRequest request,
        CancellationToken ct) =>
        Ok(await _registerTeacher.HandleAsync(request, ct));

    [HttpPost("register-school")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RegisterSchool(
        RegisterSchoolRequest request,
        CancellationToken ct) =>
        Ok(await _registerSchool.HandleAsync(request, ct));

    [HttpGet("school-plans")]
    [AllowAnonymous]
    public ActionResult<IReadOnlyList<SchoolPlanDto>> SchoolPlans() =>
        Ok(_plans.Handle());

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct) =>
        Ok(await _me.HandleAsync(CurrentUser.GetId(User), ct));

    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> UpdateMe(
        UpdateMyProfileRequest request,
        CancellationToken ct) =>
        Ok(await _updateMe.HandleAsync(CurrentUser.GetId(User), request, ct));

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken ct)
    {
        await _changePassword.HandleAsync(
            CurrentUser.GetId(User),
            request,
            ct);
        return NoContent();
    }
}
