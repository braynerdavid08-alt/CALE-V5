using Cale.Api.Extensions;
using Cale.Api.Services;
using Cale.Api.Services.Legal;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Queries;
using Cale.Modules.Identity.Domain;
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
    private readonly LegalService _legal;

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
        AuthCookieService cookies,
        LegalService legal)
    {
        _legal = legal;
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
    [EnableRateLimiting(RateLimitPolicies.Login)]
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
    [EnableRateLimiting(RateLimitPolicies.Register)]
    public async Task<ActionResult<PendingEmailConfirmationResponse>> Register(
        RegisterRequest request,
        CancellationToken ct)
    {
        LegalService.EnsureConsent(request.Consent, Roles.Student);
        var result = await _register.HandleAsync(request, ct);
        await RecordConsentAsync(result, Roles.Student, request.Consent!, ct);
        return Ok(result);
    }

    [HttpPost("register-teacher")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    public async Task<ActionResult<PendingEmailConfirmationResponse>> RegisterTeacher(
        RegisterRequest request,
        CancellationToken ct)
    {
        LegalService.EnsureConsent(request.Consent, Roles.Teacher);
        var result = await _registerTeacher.HandleAsync(request, ct);
        await RecordConsentAsync(result, Roles.Teacher, request.Consent!, ct);
        return Ok(result);
    }

    [HttpPost("register-school")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    public async Task<ActionResult<PendingEmailConfirmationResponse>> RegisterSchool(
        RegisterSchoolRequest request,
        CancellationToken ct)
    {
        LegalService.EnsureConsent(request.Consent, Roles.School);
        var result = await _registerSchool.HandleAsync(request, ct);
        await RecordConsentAsync(result, Roles.School, request.Consent!, ct);
        return Ok(result);
    }

    private async Task RecordConsentAsync(
        PendingEmailConfirmationResponse result,
        string role,
        LegalConsentDto consent,
        CancellationToken ct)
    {
        var userId = result.UserId ?? await _legal.FindUserIdByEmailAsync(result.Email, ct);
        if (userId is null)
        {
            return;
        }

        await _legal.RecordAsync(
            userId.Value,
            role,
            consent,
            LegalAcceptanceMethods.Registration,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            ct);
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.EmailCode)]
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
    [EnableRateLimiting(RateLimitPolicies.EmailCode)]
    public async Task<ActionResult<PendingEmailConfirmationResponse>> ResendConfirmation(
        ResendConfirmationRequest request,
        CancellationToken ct) =>
        Ok(await _resendConfirmation.HandleAsync(request, ct));

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
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
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [AllowAnonymous]
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

    [HttpGet("access-mode")]
    [AllowAnonymous]
    public IActionResult AccessMode() =>
        Ok(new { freeAccess = Cale.BuildingBlocks.Domain.Access.FreeAccessPolicy.Enabled });

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
    [EnableRateLimiting(RateLimitPolicies.ChangePassword)]
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
