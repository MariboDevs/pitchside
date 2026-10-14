using System.Collections.Concurrent;
using Pitchside.Contracts;
using Pitchside.Runtime.Broadcast;

namespace Pitchside.Runtime.Tests;

/// <summary>Records every push in arrival order as (kind, seq).</summary>
internal sealed class RecordingBroadcaster : IMatchBroadcaster
{
    private readonly ConcurrentQueue<(string Kind, int Seq)> _pushes = new();

    public IReadOnlyList<(string Kind, int Seq)> Pushes => [.. _pushes];

    public Task EventAsync(MatchEvent matchEvent, CancellationToken cancellationToken) => Record("event", matchEvent.Seq);

    public Task FactSheetAsync(FactSheet sheet, CancellationToken cancellationToken) => Record("facts", sheet.AsOfSeq);

    public Task MomentAsync(Moment moment, CancellationToken cancellationToken) => Record("moment", moment.TriggerSeq);

    public Task NarrativeAsync(Narrative narrative, CancellationToken cancellationToken) => Record("narrative", narrative.FactSheetSeq);

    public Task StatusAsync(MatchStatusUpdate status, CancellationToken cancellationToken) => Record("status", status.LastSeq);

    public Task OverlayEntryAsync(OverlayEntry entry, CancellationToken cancellationToken) => Record("overlay", entry.InAtMs);

    public Task ApprovalModeAsync(ApprovalModeUpdate update, CancellationToken cancellationToken) => Record("approval", 0);

    private Task Record(string kind, int seq)
    {
        _pushes.Enqueue((kind, seq));
        return Task.CompletedTask;
    }
}
