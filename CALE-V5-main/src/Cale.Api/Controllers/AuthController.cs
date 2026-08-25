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
    private readonly ChangePasswordHandler _changePassword;
    private readonly GetCurrentUserHandler _me;

    public AuthController(
        LoginUserHandler login,
        RegisterUserHandler register,
        ChangePasswordHandler changePassword,
        GetCurrentUserHandler me)
    {
        _login = login;
        _register = register;
        _changePassword = changePassword;
        _me = me;
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

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct) =>
        Ok(await _me.HandleAsync(CurrentUser.GetId(User), ct));

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
