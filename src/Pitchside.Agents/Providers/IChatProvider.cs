namespace Pitchside.Agents.Providers;

/// <summary>A pluggable model. <c>Scripted</c> is the in-process local mode; <c>Foundry</c> calls the deployed models.</summary>
public interface IChatProvider
{
    string Name { get; }

    Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken);
}
