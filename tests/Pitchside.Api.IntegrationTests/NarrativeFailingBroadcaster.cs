using Pitchside.Contracts;
using Pitchside.Runtime.Broadcast;

namespace Pitchside.Api.IntegrationTests;

/// <summary>A broadcaster whose narrative pushes all fail, as a hub that is down would; every other push is dropped.</summary>
internal sealed class NarrativeFailingBroadcaster : IMatchBroadcaster
{
    public Task NarrativeAsync(Narrative narrative, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The hub is unavailable.");

    public Task EventAsync(MatchEvent matchEvent, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task FactSheetAsync(FactSheet sheet, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task MomentAsync(Moment moment, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StatusAsync(MatchStatusUpdate status, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OverlayEntryAsync(OverlayEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ApprovalModeAsync(ApprovalModeUpdate update, CancellationToken cancellationToken) => Task.CompletedTask;
}
