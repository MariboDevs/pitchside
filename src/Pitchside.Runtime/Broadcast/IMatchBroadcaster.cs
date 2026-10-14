using Pitchside.Contracts;

namespace Pitchside.Runtime.Broadcast;

/// <summary>Pushes to every client watching a match. The API implements it with SignalR groups.</summary>
public interface IMatchBroadcaster
{
    Task EventAsync(MatchEvent matchEvent, CancellationToken cancellationToken);

    Task FactSheetAsync(FactSheet sheet, CancellationToken cancellationToken);

    Task MomentAsync(Moment moment, CancellationToken cancellationToken);

    Task NarrativeAsync(Narrative narrative, CancellationToken cancellationToken);

    Task StatusAsync(MatchStatusUpdate status, CancellationToken cancellationToken);
}
