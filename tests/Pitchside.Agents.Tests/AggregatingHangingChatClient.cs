using Microsoft.Extensions.AI;

namespace Pitchside.Agents.Tests;

/// <summary>
/// Hangs until cancelled, then fails the way the OpenAI SDK's retry policy does: an aggregate of the transport error
/// and the cancelled attempt, rather than the cancellation itself.
/// </summary>
public sealed class AggregatingHangingChatClient : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException cancelled)
        {
            throw new AggregateException("Retry failed after 2 tries.", new HttpRequestException("Resource temporarily unavailable"), cancelled);
        }

        throw new InvalidOperationException("unreachable");
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
