namespace Pitchside.Api.Endpoints;

/// <summary>The body of <c>POST /api/v1/replays</c>: a seed and an optional speed over real time (0 is as fast as possible).</summary>
public sealed record ReplayRequest(long Seed, double? Speed);
