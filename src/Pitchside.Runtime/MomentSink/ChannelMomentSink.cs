using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Pitchside.Runtime.MomentSink;

/// <summary>The in-process moment sink: an unbounded channel, single reader.</summary>
public sealed class ChannelMomentSink : IMomentSink
{
    private readonly Channel<MomentWorkItem> _channel = Channel.CreateUnbounded<MomentWorkItem>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(MomentWorkItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public async IAsyncEnumerable<MomentWorkItem> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>Signals that no more moments will arrive, so readers finish once the queue drains.</summary>
    public void Complete() => _channel.Writer.TryComplete();
}
