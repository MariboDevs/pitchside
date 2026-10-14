namespace Pitchside.Contracts;

/// <summary>
/// One event on the bus. The partition key is <see cref="MatchId"/>, so one match's events stay in order;
/// <see cref="PayloadJson"/> is the event serialised with <see cref="ContractJson.Options"/>.
/// </summary>
public sealed record EventEnvelope(string MatchId, int Seq, string Type, string PayloadJson, DateTimeOffset PublishedUtc);
