using Pitchside.Contracts;

namespace Pitchside.Engine;

/// <summary>A fictional club with the ratings the simulator reads. Ratings are multipliers around 1.0.</summary>
public sealed record EngineClub(Club Club, double Attack, double Defence);
