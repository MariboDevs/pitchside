namespace Pitchside.Contracts;

/// <summary>The contents of match.json: everything a consumer needs before the first event arrives.</summary>
public sealed record MatchDocument(
    string SchemaVersion,
    string MatchId,
    long Seed,
    string Competition,
    string Venue,
    DateTimeOffset KickoffUtc,
    TeamSheet Home,
    TeamSheet Away);
