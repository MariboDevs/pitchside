using System.Text.Json.Serialization;

namespace Pitchside.Contracts;

/// <summary>One line of events.jsonl. Qualifiers are null unless the event type defines them.</summary>
public sealed record MatchEvent
{
    public required string Id { get; init; }
    public required string MatchId { get; init; }
    public required int Seq { get; init; }
    public required int Period { get; init; }
    public required int ClockMs { get; init; }
    public required DateTimeOffset WallTimeUtc { get; init; }
    public required EventType Type { get; init; }
    public required string TeamId { get; init; }
    public string? PlayerId { get; init; }
    public required Location Location { get; init; }
    public required int PossessionId { get; init; }

    // Pass and carry
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Location? EndLocation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? LengthM { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Difficulty { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Completed { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReceiverId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? SpeedKph { get; init; }

    // Shot and goal
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Xg { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OnTarget { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ShotOutcome? Outcome { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ShotSpeedKph { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BodyPart? BodyPart { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssistId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Penalty { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? HomeScore { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AwayScore { get; init; }

    // Turnover, out of play, foul, card, substitution, pressure
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TurnoverReason? Reason { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Restart? Restart { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FouledPlayerId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CardAwarded { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CardColour? Colour { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SecondYellow { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlayerOffId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlayerOnId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Intensity { get; init; }

    // Tracking
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TrackingEntry>? Tracking { get; init; }
}
