using System.Globalization;
using Pitchside.Contracts;

namespace Pitchside.Overlay;

/// <summary>A fact's value as shown on screen: numbers at the fact's precision, invariant culture; text as is.</summary>
public static class FactDisplay
{
    public static string Format(Fact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Value.Number is { } number
            ? number.ToString("F" + fact.Precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : fact.Value.Text ?? string.Empty;
    }
}
