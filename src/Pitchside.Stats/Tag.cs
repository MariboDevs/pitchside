using System.Text.Json.Serialization;

namespace Pitchside.Stats;

/// <summary>A threshold crossed by a player on one event: <c>speed.30</c>, <c>distance.10</c>, <c>shot.fast</c>.</summary>
public sealed record Tag(
    [property: JsonPropertyName("tag")] string Name,
    string PlayerId,
    string EventId,
    double Value);
