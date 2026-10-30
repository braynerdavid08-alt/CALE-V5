using System.Collections.Concurrent;
using Cale.Modules.LiveClassroom.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.Modules.LiveClassroom.Infrastructure;

/// <summary>
/// Presentation slide index: DB is source of truth (survives API restart);
/// in-process cache avoids a round-trip on every student join in the same instance.
/// </summary>
public sealed class LivePresentationSlideState : ILivePresentationSlideState
{
    private readonly ConcurrentDictionary<int, int> _cache = new();
    private readonly IServiceScopeFactory _scopes;

    public LivePresentationSlideState(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task SetSlideIndexAsync(int sessionId, int slideIndex, CancellationToken ct = default)
    {
        var index = Math.Max(0, slideIndex);
        _cache[sessionId] = index;

        using var scope = _scopes.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ILiveSessionStore>();
        var session = await store.GetByIdAsync(sessionId, ct);
        if (session is null)
        {
            return;
        }

        if (session.CurrentSlideIndex == index)
        {
            return;
        }

        session.SetSlideIndex(index);
        await store.SaveChangesAsync(ct);
    }

    public async Task<int> GetSlideIndexAsync(int sessionId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(sessionId, out var cached))
        {
            return cached;
        }

        using var scope = _scopes.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ILiveSessionStore>();
        var session = await store.GetByIdAsync(sessionId, ct);
        var index = session?.CurrentSlideIndex ?? 0;
        _cache[sessionId] = index;
        return index;
    }
}
