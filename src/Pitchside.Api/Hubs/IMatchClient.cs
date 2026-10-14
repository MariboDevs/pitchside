using Pitchside.Contracts;

namespace Pitchside.Api.Hubs;

/// <summary>What the server sends to clients in group <c>match:{id}</c>.</summary>
public interface IMatchClient
{
    Task Event(MatchEvent matchEvent);

    Task FactSheet(FactSheet sheet);

    Task Moment(Moment moment);

    Task Narrative(Narrative narrative);

    Task MatchStatus(MatchStatusUpdate status);
}
