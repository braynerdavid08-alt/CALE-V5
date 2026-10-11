using System.Text.RegularExpressions;
using Cale.Api.Services.Live;
using Cale.BuildingBlocks.Domain.Time;

namespace Cale.Api.Middleware;

/// <summary>
/// Marks a live class / game show as active whenever a write action on it succeeds (host
/// controls, answers, buzzes). Reads (polling) do not count, so an idle tab left open does not
/// keep an abandoned room alive.
/// </summary>
public sealed class LiveActivityMiddleware
{
    private static readonly Regex LiveRoute = new(
        "^/api/live/sessions/(?<id>[0-9]+)(/|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex GameShowRoute = new(
        "^/api/game-show/(?<id>[0-9]+)(/|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly RequestDelegate _next;
    private readonly LiveActivityTracker _tracker;
    private readonly IClock _clock;

    public LiveActivityMiddleware(RequestDelegate next, LiveActivityTracker tracker, IClock clock)
    {
        _next = next;
        _tracker = tracker;
        _clock = clock;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method)
            || context.Response.StatusCode >= 400)
        {
            return;
        }

        if (TryMatch(context.Request.Path.Value, out var kind, out var roomId))
        {
            _tracker.Touch(kind, roomId, _clock.UtcNow);
        }
    }

    public static bool TryMatch(string? path, out LiveRoomKind kind, out int roomId)
    {
        kind = default;
        roomId = 0;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var live = LiveRoute.Match(path);
        if (live.Success && int.TryParse(live.Groups["id"].Value, out roomId))
        {
            kind = LiveRoomKind.LiveClass;
            return true;
        }

        var game = GameShowRoute.Match(path);
        if (game.Success && int.TryParse(game.Groups["id"].Value, out roomId))
        {
            kind = LiveRoomKind.GameShow;
            return true;
        }

        return false;
    }
}
