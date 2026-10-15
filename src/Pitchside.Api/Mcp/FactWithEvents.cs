using Pitchside.Contracts;

namespace Pitchside.Api.Mcp;

/// <summary>One fact and the events it was derived from, as <c>get_fact</c> returns it.</summary>
public sealed record FactWithEvents(Fact Fact, int AsOfSeq, IReadOnlyList<MatchEvent> Events);
