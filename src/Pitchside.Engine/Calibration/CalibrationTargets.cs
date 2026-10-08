using System.Reflection;
using System.Text.Json;

namespace Pitchside.Engine.Calibration;

/// <summary>Real-league aggregate targets, embedded from data/calibration/targets.json.</summary>
public sealed class CalibrationTargets
{
    public const string PrimaryKey = "E0_2015_2025";

    private readonly Dictionary<string, double> _values;

    private CalibrationTargets(string key, Dictionary<string, double> values)
    {
        Key = key;
        _values = values;
    }

    public string Key { get; }

    public double this[string statistic] => _values.TryGetValue(statistic, out var value)
        ? value
        : throw new KeyNotFoundException($"No target named '{statistic}' under '{Key}'.");

    public static CalibrationTargets LoadEmbedded(string key = PrimaryKey)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Pitchside.Engine.Calibration.targets.json")
            ?? throw new InvalidOperationException("Embedded calibration targets are missing.");
        using var document = JsonDocument.Parse(stream);
        var section = document.RootElement.GetProperty(key);
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var property in section.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
            {
                values[property.Name] = property.Value.GetDouble();
            }
        }

        return new CalibrationTargets(key, values);
    }
}
