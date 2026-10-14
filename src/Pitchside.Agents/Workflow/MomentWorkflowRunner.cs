using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pitchside.Runtime.MomentSink;

namespace Pitchside.Agents.Workflow;

/// <summary>
/// Reads the moment sink and runs one workflow per moment, at most <c>Agents:MaxConcurrentMoments</c> at once.
/// The sink is a queue, so a moment that arrives while the crew is busy waits rather than being dropped.
/// </summary>
public sealed partial class MomentWorkflowRunner(
    IMomentSink sink,
    MomentWorkflow workflow,
    INarrativePublisher publisher,
    IOptions<AgentsOptions> options,
    ILogger<MomentWorkflowRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var gate = new SemaphoreSlim(Math.Max(1, options.Value.MaxConcurrentMoments));
        var running = new List<Task>();
        try
        {
            await foreach (var item in sink.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await gate.WaitAsync(stoppingToken).ConfigureAwait(false);
                running.RemoveAll(t => t.IsCompleted);
                running.Add(RunGatedAsync(item, gate, stoppingToken));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogStopping(running.Count);
        }

        await Task.WhenAll(running).ConfigureAwait(false);
    }

    /// <summary>Runs one moment and publishes every narrative it produced.</summary>
    public async Task<MomentContext> ProcessAsync(MomentWorkItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var context = await workflow.RunAsync(item, cancellationToken).ConfigureAwait(false);
        foreach (var narrative in context.Narratives)
        {
            await publisher.PublishAsync(narrative, cancellationToken).ConfigureAwait(false);
        }

        return context;
    }

    private async Task RunGatedAsync(MomentWorkItem item, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Yield();
            await ProcessAsync(item, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogCancelled(item.Moment.Id);
        }
        finally
        {
            gate.Release();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Moment workflow runner stopping with {Running} workflows in flight")]
    private partial void LogStopping(int running);

    [LoggerMessage(Level = LogLevel.Information, Message = "Moment {MomentId} cancelled at shutdown")]
    private partial void LogCancelled(string momentId);
}
