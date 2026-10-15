namespace Pitchside.Api.Endpoints;

/// <summary>Chosen fact values as of one event: a point on the xG race or the score history.</summary>
public sealed record FactSeriesPoint(int AsOfSeq, int Period, int ClockMs, IReadOnlyDictionary<string, double> Values);
