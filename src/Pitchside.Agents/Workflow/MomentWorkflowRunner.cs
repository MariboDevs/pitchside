using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pitchside.Contracts;
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

    /// <summary>
    /// Runs one moment, publishing each narrative as soon as the workflow finishes it (WP06-R3), then any it ended
    /// with that were not published yet, such as a template composed after a failed run. Each is published once.
    /// </summary>
    public async Task<MomentContext> ProcessAsync(MomentWorkItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var published = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        async Task PublishOnceAsync(Narrative narrative, CancellationToken ct)
        {
            if (published.TryAdd(narrative.Id, true))
            {
                await publisher.PublishAsync(narrative, ct).ConfigureAwait(false);
            }
        }

        var context = await workflow.RunAsync(item, PublishOnceAsync, cancellationToken).ConfigureAwait(false);
        foreach (var narrative in context.Narratives)
        {
            await PublishOnceAsync(narrative, cancellationToken).ConfigureAwait(false);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A fault here is a bug or a publisher outage, not a model failure (those become fallbacks inside the
            // workflow). It is logged with the moment id so the moment can be replayed from the store, and the
            // runner carries on with the next moment rather than stopping the host.
            LogFailed(ex, item.Moment.Id);
        }
        finally
        {
            gate.Release();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Moment workflow runner stopping with {Running} workflows in flight")]
    private partial void LogStopping(int running);

    [LoggerMessage(Level = LogLevel.Error, Message = "Moment {MomentId} failed in the workflow or while publishing")]
    private partial void LogFailed(Exception exception, string momentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Moment {MomentId} cancelled at shutdown")]
    private partial void LogCancelled(string momentId);
}
