using System.Globalization;
using System.Text.Json.Serialization;

namespace Pitchside.Contracts;

/// <summary>A fact's value: a number for measured facts, a string for categorical facts such as <c>match.state</c>.</summary>
[JsonConverter(typeof(FactValueJsonConverter))]
public readonly record struct FactValue
{
    private FactValue(double? number, string? text)
    {
        Number = number;
        Text = text;
    }

    public double? Number { get; }

    public string? Text { get; }

    public bool IsNumber => Number.HasValue;

    public static FactValue Of(double number) => new(number, null);

    public static FactValue Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new FactValue(null, text);
    }

    public override string ToString() =>
        Number?.ToString(CultureInfo.InvariantCulture) ?? Text ?? string.Empty;
}
