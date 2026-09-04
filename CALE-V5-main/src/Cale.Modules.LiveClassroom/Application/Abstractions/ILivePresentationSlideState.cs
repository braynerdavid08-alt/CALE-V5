namespace Cale.Modules.LiveClassroom.Application.Abstractions;

public interface ILivePresentationSlideState
{
    void SetSlideIndex(int sessionId, int slideIndex);
    int GetSlideIndex(int sessionId);
}
