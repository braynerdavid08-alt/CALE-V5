using System.Security.Claims;
using Cale.Api.Extensions;
using Cale.Api.Services.Tournaments;
using Cale.BuildingBlocks.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cale.Api.Hubs;

[Authorize]
public sealed class TournamentHub : Hub
{
    public const string GroupPrefix = "tournament-";
    public const string UserPrefix = "tournament-user-";

    private readonly TournamentService _tournaments;
    private readonly ILogger<TournamentHub> _logger;

    public TournamentHub(TournamentService tournaments, ILogger<TournamentHub> logger)
    {
        _tournaments = tournaments;
        _logger = logger;
    }

    public static string GroupName(int tournamentId) => $"{GroupPrefix}{tournamentId}";
    public static string UserGroup(int userId) => $"{UserPrefix}{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId();
        if (userId is int uid)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(uid));
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinTournament(int tournamentId)
    {
        var userId = CurrentUserId()
            ?? throw new HubException("No autenticado.");
        try
        {
            _ = await _tournaments.GetLobbyAsync(tournamentId, userId, Context.ConnectionAborted);
            Context.Items["tournamentId"] = tournamentId;
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tournamentId));
            await _tournaments.SetConnectionAsync(
                tournamentId,
                userId,
                Context.ConnectionId,
                connected: true,
                Context.ConnectionAborted);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            _logger.LogDebug("JoinTournament cancelled for {TournamentId}", tournamentId);
        }
    }

    public async Task LeaveTournament(int tournamentId)
    {
        var userId = CurrentUserId();
        if (userId is int uid)
        {
            await _tournaments.SetConnectionAsync(
                tournamentId,
                uid,
                Context.ConnectionId,
                connected: false,
                Context.ConnectionAborted);
        }

        Context.Items.Remove("tournamentId");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tournamentId));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("tournamentId", out var raw)
            && raw is int tournamentId
            && CurrentUserId() is int uid)
        {
            try
            {
                await _tournaments.SetConnectionAsync(
                    tournamentId,
                    uid,
                    Context.ConnectionId,
                    connected: false,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Disconnect cleanup failed for tournament {Id}", tournamentId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private int? CurrentUserId()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
