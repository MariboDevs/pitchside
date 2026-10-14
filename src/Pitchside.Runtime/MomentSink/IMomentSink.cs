namespace Pitchside.Runtime.MomentSink;

/// <summary>
/// The queue between the match actor, which writes every detected moment, and the agent crew, which reads them.
/// A queue, so nothing is lost when the crew is busy.
/// </summary>
public interface IMomentSink
{
    ValueTask EnqueueAsync(MomentWorkItem item, CancellationToken cancellationToken);

    IAsyncEnumerable<MomentWorkItem> ReadAllAsync(CancellationToken cancellationToken);
}
