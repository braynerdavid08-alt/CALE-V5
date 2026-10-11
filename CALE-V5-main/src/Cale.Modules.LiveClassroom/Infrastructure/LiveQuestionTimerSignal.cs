using System.Threading.Channels;
using Cale.Modules.LiveClassroom.Application.Abstractions;

namespace Cale.Modules.LiveClassroom.Infrastructure;

public sealed class LiveQuestionTimerSignal : ILiveQuestionTimerSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });

    public void Notify() => _channel.Writer.TryWrite(true);

    /// <summary>Completes when notified; throws OperationCanceledException when the token fires.</summary>
    public async Task WaitAsync(CancellationToken ct) => await _channel.Reader.ReadAsync(ct);
}
