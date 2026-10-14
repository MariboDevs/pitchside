using System.Collections.Concurrent;
using Pitchside.Contracts;
using Pitchside.Runtime.Broadcast;

namespace Pitchside.Runtime.Tests;

/// <summary>Records every push in arrival order as (kind, seq).</summary>
internal sealed class RecordingBroadcaster : IMatchBroadcaster
{
    private readonly ConcurrentQueue<(string Kind, int Seq)> _pushes = new();
    private readonly ConcurrentQueue<MatchStatusUpdate> _statuses = new();

    public IReadOnlyList<(string Kind, int Seq)> Pushes => [.. _pushes];

    public IReadOnlyList<MatchStatusUpdate> Statuses => [.. _statuses];

    public Task EventAsync(MatchEvent matchEvent, CancellationToken cancellationToken) => Record("event", matchEvent.Seq);

    public Task FactSheetAsync(FactSheet sheet, CancellationToken cancellationToken) => Record("facts", sheet.AsOfSeq);

    public Task MomentAsync(Moment moment, CancellationToken cancellationToken) => Record("moment", moment.TriggerSeq);

    public Task NarrativeAsync(Narrative narrative, CancellationToken cancellationToken) => Record("narrative", narrative.FactSheetSeq);

    public Task StatusAsync(MatchStatusUpdate status, CancellationToken cancellationToken)
    {
        _statuses.Enqueue(status);
        return Record("status", status.LastSeq);
    }

    private Task Record(string kind, int seq)
    {
        _pushes.Enqueue((kind, seq));
        return Task.CompletedTask;
    }
}
