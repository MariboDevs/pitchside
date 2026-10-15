using Pitchside.Contracts;

namespace Pitchside.Api.Endpoints;

/// <summary>One cited key: the fact behind it as of the narrative's sheet (null if it does not resolve), whose
/// <c>derivedFrom</c> names the events.</summary>
public sealed record TraceCitation(string Key, Fact? Fact);
