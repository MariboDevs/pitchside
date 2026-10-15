using Pitchside.Contracts;

namespace Pitchside.Runtime.Broadcast;

/// <summary>A broadcaster with no clients, for hosts without SignalR and for tests that do not watch the push.</summary>
public sealed class NullMatchBroadcaster : IMatchBroadcaster
{
    public Task EventAsync(MatchEvent matchEvent, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task FactSheetAsync(FactSheet sheet, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task MomentAsync(Moment moment, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NarrativeAsync(Narrative narrative, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StatusAsync(MatchStatusUpdate status, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OverlayEntryAsync(OverlayEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ApprovalModeAsync(ApprovalModeUpdate update, CancellationToken cancellationToken) => Task.CompletedTask;
}
