using Microsoft.Extensions.Options;

namespace Pitchside.Agents.Workflow;

/// <summary>
/// The host's cap on moment workflows running at once, <c>Agents:MaxConcurrentMoments</c>, shared by every caller
/// that runs one: the runner that narrates a live match and the MCP <c>explain_moment</c> tool, so neither can push
/// the model deployment past the cap on its own.
/// </summary>
public sealed class WorkflowGate(IOptions<AgentsOptions> options) : IDisposable
{
    private readonly SemaphoreSlim _slots = new(Math.Max(1, options.Value.MaxConcurrentMoments));

    /// <summary>Waits for a free slot; the caller releases it with <see cref="Release"/>, in a finally.</summary>
    public Task WaitAsync(CancellationToken cancellationToken) => _slots.WaitAsync(cancellationToken);

    public void Release() => _slots.Release();

    public void Dispose() => _slots.Dispose();
}
