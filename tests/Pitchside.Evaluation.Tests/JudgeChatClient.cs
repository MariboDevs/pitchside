using Microsoft.Extensions.AI;

namespace Pitchside.Evaluation.Tests;

/// <summary>A judge model that always gives one answer, or fails when it has none, and keeps what it was shown.</summary>
internal sealed class JudgeChatClient(string? answer) : IChatClient
{
    public List<ChatMessage> Seen { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Seen.AddRange(messages);
        return answer is null
            ? throw new InvalidOperationException("The judge is unavailable.")
            : Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)) { ModelId = "judge" });
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
