using Microsoft.Extensions.AI;

namespace Pitchside.Agents.Providers;

/// <summary>
/// Bounds each call of the chat client it wraps by a deadline, as <see cref="FoundryChatProvider"/> bounds the crew's
/// calls by <c>Foundry:TimeoutSeconds</c>: a call past it fails with <see cref="TimeoutException"/>, while the caller's
/// own cancellation still surfaces as cancellation. Used for model work outside the crew, such as the groundedness
/// judge (WP-07). <paramref name="beforeDeadline"/>, when given, runs before the deadline starts; the provider uses it
/// to fetch the access token, whose cost is not the model's.
/// </summary>
public sealed class DeadlineChatClient(IChatClient inner, TimeSpan deadline, string deployment, Func<CancellationToken, ValueTask>? beforeDeadline = null) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (beforeDeadline is not null)
        {
            await beforeDeadline(cancellationToken).ConfigureAwait(false);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(deadline);
        try
        {
            return await base.GetResponseAsync(messages, options, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Foundry deployment '{deployment}' did not answer within {deadline.TotalSeconds:0} s.", ex);
        }
    }
}
