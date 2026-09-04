using System.Collections.Concurrent;
using Cale.Modules.LiveClassroom.Application.Abstractions;

namespace Cale.Modules.LiveClassroom.Infrastructure;

public sealed class LivePresentationSlideState : ILivePresentationSlideState
{
    private readonly ConcurrentDictionary<int, int> _indexes = new();

    public void SetSlideIndex(int sessionId, int slideIndex) =>
        _indexes[sessionId] = Math.Max(0, slideIndex);

    public int GetSlideIndex(int sessionId) =>
        _indexes.TryGetValue(sessionId, out var index) ? index : 0;
}
