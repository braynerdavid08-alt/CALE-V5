using Cale.Api.Extensions;
using Cale.Api.Services;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly LoginUserHandler _login;
    private readonly RegisterUserHandler _register;
    private readonly RegisterTeacherHandler _registerTeacher;
    private readonly RegisterSchoolHandler _registerSchool;
    private readonly ConfirmEmailHandler _confirmEmail;
    private readonly ResendConfirmationHandler _resendConfirmation;
    private readonly ChangePasswordHandler _changePassword;
    private readonly GetCurrentUserHandler _me;
    private readonly UpdateMyProfileHandler _updateMe;
    private readonly ListSchoolPlansHandler _plans;
    private readonly IssueAuthSessionHandler _issueSession;
    private readonly RefreshAuthSessionHandler _refreshSession;
    private readonly LogoutAuthSessionHandler _logoutSession;
    private readonly AuthCookieService _cookies;

    public AuthController(
        LoginUserHandler login,
        RegisterUserHandler register,
        RegisterTeacherHandler registerTeacher,
        RegisterSchoolHandler registerSchool,
        ConfirmEmailHandler confirmEmail,
        ResendConfirmationHandler resendConfirmation,
        ChangePasswordHandler changePassword,
        GetCurrentUserHandler me,
        UpdateMyProfileHandler updateMe,
        ListSchoolPlansHandler plans,
        IssueAuthSessionHandler issueSession,
        RefreshAuthSessionHandler refreshSession,
        LogoutAuthSessionHandler logoutSession,
        AuthCookieService cookies)
    {
        _login = login;
        _register = register;
        _registerTeacher = registerTeacher;
        _registerSchool = registerSchool;
        _confirmEmail = confirmEmail;
        _resendConfirmation = resendConfirmation;
        _changePassword = changePassword;
        _me = me;
        _updateMe = updateMe;
        _plans = plans;
        _issueSession = issueSession;
        _refreshSession = refreshSession;
        _logoutSession = logoutSession;
        _cookies = cookies;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken ct)
    {
        var result = await _login.HandleAsync(request, ct);
        var session = await _issueSession.IssueAsync(
            result.UserId,
            result.Email,
            result.Name,
            result.Role,
            result.MustChangePassword,
            ct);
        _cookies.Set(Response, session.AccessToken, session.RefreshToken);
        return Ok(session.Response);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<PendingEmailConfirmationResponse>> Register(
        RegisterRequest request,
        CancellationToken ct) =>
        Ok(await _register.HandleAsync(request, ct));

    [HttpPost("register-teacher")]
    [AllowAnonymous]
    public async Task<ActionResult<PendingEmailConfirmationResponse>> RegisterTeacher(
        RegisterRequest request,
        CancellationToken ct) =>
        Ok(await _registerTeacher.HandleAsync(request, ct));

    [HttpPost("register-school")]
    [AllowAnonymous]
    public async Task<ActionResult<PendingEmailConfirmationResponse>> RegisterSchool(
        RegisterSchoolRequest request,
        CancellationToken ct) =>
        Ok(await _registerSchool.HandleAsync(request, ct));

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> ConfirmEmail(
        ConfirmEmailRequest request,
        CancellationToken ct)
    {
        var result = await _confirmEmail.HandleAsync(request, ct);
        var session = await _issueSession.IssueAsync(
            result.UserId,
            result.Email,
            result.Name,
            result.Role,
            result.MustChangePassword,
            ct);
        _cookies.Set(Response, session.AccessToken, session.RefreshToken);
        return Ok(session.Response);
    }

    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    public async Task<ActionResult<PendingEmailConfirmationResponse>> ResendConfirmation(
        ResendConfirmationRequest request,
        CancellationToken ct) =>
        Ok(await _resendConfirmation.HandleAsync(request, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(AuthCookieNames.Refresh, out var refresh)
            || string.IsNullOrWhiteSpace(refresh))
        {
            return Unauthorized();
        }

        var session = await _refreshSession.HandleAsync(refresh, ct);
        _cookies.Set(Response, session.AccessToken, session.RefreshToken);
        return Ok(session.Response);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        Request.Cookies.TryGetValue(AuthCookieNames.Refresh, out var refresh);
        await _logoutSession.HandleAsync(refresh, ct);
        _cookies.Clear(Response);
        return NoContent();
    }

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
