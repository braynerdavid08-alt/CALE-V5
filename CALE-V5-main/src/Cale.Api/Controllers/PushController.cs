using Cale.Api.Extensions;
using Cale.Modules.Engagement.Infrastructure.Push;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/push")]
public sealed class PushController : ControllerBase
{
    private readonly PushSubscriptionService _push;

    public PushController(PushSubscriptionService push) => _push = push;

    [HttpGet("public-key")]
    public async Task<IActionResult> PublicKey(CancellationToken ct) =>
        Ok(new { publicKey = await _push.GetPublicKeyAsync(ct) });

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct) =>
        Ok(new { devices = await _push.CountDevicesAsync(CurrentUser.GetId(User), ct) });

    [HttpPost("subscriptions")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequest body, CancellationToken ct)
    {
        if (!IsValidEndpoint(body.Endpoint)
            || string.IsNullOrWhiteSpace(body.Keys?.P256dh)
            || string.IsNullOrWhiteSpace(body.Keys?.Auth)
            || body.Keys.P256dh.Length > 200
            || body.Keys.Auth.Length > 100)
        {
            return BadRequest(new { message = "Suscripción push inválida." });
        }

        await _push.SubscribeAsync(
            CurrentUser.GetId(User),
            body.Endpoint!,
            body.Keys.P256dh,
            body.Keys.Auth,
            Request.Headers.UserAgent.ToString(),
            ct);
        return NoContent();
    }

    [HttpPost("subscriptions/remove")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest body, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(body.Endpoint))
        {
            await _push.UnsubscribeAsync(CurrentUser.GetId(User), body.Endpoint, ct);
        }

        return NoContent();
    }

    [HttpPost("test")]
    public IActionResult Test()
    {
        _push.SendTest(CurrentUser.GetId(User));
        return Accepted();
    }

    private static bool IsValidEndpoint(string? endpoint) =>
        !string.IsNullOrWhiteSpace(endpoint)
        && endpoint.Length <= 1000
        && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
}

public sealed record PushSubscribeKeys(string? P256dh, string? Auth);

public sealed record PushSubscribeRequest(string? Endpoint, PushSubscribeKeys? Keys);

public sealed record PushUnsubscribeRequest(string? Endpoint);
