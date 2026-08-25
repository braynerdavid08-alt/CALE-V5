namespace Cale.Modules.Identity.Application.DTOs;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string Name, string Email, string Password);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public sealed record AuthResponse(
    string Token,
    int UserId,
    string Name,
    string Email,
    string Role);

public sealed record MeResponse(
    int Id,
    string Name,
    string Email,
    string Role,
    bool IsActive);
