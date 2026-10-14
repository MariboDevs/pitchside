namespace Pitchside.Runtime.Messages;

/// <summary>A message for one match's actor. The supervisor routes on <see cref="MatchId"/>, which is also the sharding key.</summary>
public interface IMatchMessage
{
    string MatchId { get; }
}
