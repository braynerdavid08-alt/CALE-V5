using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.Identity.Application.Commands;

public sealed class SetUserActiveHandler
{
    private const string DefaultReactivationReason = "Reactivada por el administrador.";

    private readonly IUserStore _users;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IAccountStatusStore _status;
    private readonly IClock _clock;
    private readonly ILogger<SetUserActiveHandler> _logger;

    public SetUserActiveHandler(
        IUserStore users,
        IRefreshTokenStore refreshTokens,
        IAccountStatusStore status,
        IClock clock,
        ILogger<SetUserActiveHandler> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _status = status;
        _clock = clock;
        _logger = logger;
    }

    public async Task<UserListItemDto> HandleAsync(
        int actorUserId,
        int targetUserId,
        SetUserActiveRequest request,
        CancellationToken ct)
    {
        if (actorUserId == targetUserId && !request.IsActive)
        {
            throw new DomainException(
                "You cannot deactivate your own account.",
                400,
                "cannot_deactivate_self");
        }

        var user = await _users.GetByIdAsync(targetUserId, ct)
            ?? throw new NotFoundException("User not found.", "user_not_found");

        var now = _clock.UtcNow;
        if (request.IsActive)
        {
            if (!user.IsActive)
            {
                var reason = Clip(request.Reason, AccountStatusEvent.ReasonMax) ?? DefaultReactivationReason;
                user.Activate();
                await _status.AddAsync(AccountStatusEvent.Reactivate(user.Id, reason, actorUserId, now), ct);
            }
        }
        else if (user.IsActive)
        {
            var reason = request.Reason?.Trim() ?? "";
            if (reason.Length < AccountStatusEvent.ReasonMin || reason.Length > AccountStatusEvent.ReasonMax)
            {
                throw new DomainException(
                    "Explica el motivo de la suspensión (10 a 500 caracteres).",
                    400,
                    "suspension_reason_required");
            }

            if (request.Evidence is { Length: > AccountStatusEvent.EvidenceMax })
            {
                throw new DomainException(
                    "La evidencia admite hasta 1000 caracteres.",
                    400,
                    "suspension_evidence_too_long");
            }

            DateTime? until = null;
            if (request.DurationDays is { } days)
            {
                if (days < 1 || days > AccountStatusEvent.MaxDurationDays)
                {
                    throw new DomainException(
                        "La duración debe estar entre 1 y 3650 días.",
                        400,
                        "suspension_duration_invalid");
                }

                until = now.AddDays(days);
            }

            user.Deactivate();
            await _status.AddAsync(
                AccountStatusEvent.Suspend(user.Id, reason, request.Evidence, until, actorUserId, now),
                ct);
        }

        await _users.SaveChangesAsync(ct);
        if (!request.IsActive)
        {
            await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
        }

        _logger.LogWarning(
            "Audit: admin {ActorId} set user {TargetId} active={Active}",
            actorUserId,
            user.Id,
            request.IsActive);

        return new UserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            Roles.Normalize(user.Role),
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt);
    }

    public async Task<IReadOnlyList<AccountStatusEventDto>> HistoryAsync(int targetUserId, CancellationToken ct)
    {
        _ = await _users.GetByIdAsync(targetUserId, ct)
            ?? throw new NotFoundException("User not found.", "user_not_found");

        var events = await _status.ListAsync(targetUserId, ct);
        var actorNames = new Dictionary<int, string?>();
        foreach (var actorId in events.Select(e => e.ActorUserId).OfType<int>().Distinct())
        {
            actorNames[actorId] = (await _users.GetByIdAsync(actorId, ct))?.Name;
        }

        return events
            .Select(e => new AccountStatusEventDto(
                e.Id,
                e.Action,
                e.Reason,
                e.Evidence,
                e.SuspendedUntil,
                e.ActorUserId,
                e.ActorUserId is { } id ? actorNames.GetValueOrDefault(id) : null,
                e.CreatedAt))
            .ToList();
    }

    private static string? Clip(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
