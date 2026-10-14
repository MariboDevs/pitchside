using System.Text.Json.Serialization;

namespace Pitchside.Contracts;

/// <summary>
/// One named statement about the match, computed by code. <see cref="Key"/> is the citation handle;
/// <see cref="Precision"/> is the number of decimals it is shown with, and the value is already rounded to it.
/// </summary>
public sealed record Fact
{
    public required string Key { get; init; }

    public required FactValue Value { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Unit { get; init; }

    public required int Precision { get; init; }

    public required IReadOnlyList<string> DerivedFrom { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FactWindow? Window { get; init; }
}
