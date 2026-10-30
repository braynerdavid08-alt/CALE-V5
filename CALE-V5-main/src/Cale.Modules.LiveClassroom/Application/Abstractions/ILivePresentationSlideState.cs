namespace Cale.Modules.LiveClassroom.Application.Abstractions;

public interface ILivePresentationSlideState
{
    Task SetSlideIndexAsync(int sessionId, int slideIndex, CancellationToken ct = default);
    Task<int> GetSlideIndexAsync(int sessionId, CancellationToken ct = default);
}
