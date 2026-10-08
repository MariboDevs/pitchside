using System.Text;
using System.Text.Json;
using Pitchside.Contracts;

namespace Pitchside.Engine;

/// <summary>Writes a simulated match as match.json and events.jsonl in a directory.</summary>
public static class MatchWriter
{
    private static readonly JsonSerializerOptions Indented = new(ContractJson.Options) { WriteIndented = true };

    public static async Task WriteAsync(SimulatedMatch match, string directory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);

        var documentPath = Path.Combine(directory, "match.json");
        await File.WriteAllTextAsync(documentPath, JsonSerializer.Serialize(match.Document, Indented), cancellationToken)
            .ConfigureAwait(false);

        var eventsPath = Path.Combine(directory, "events.jsonl");
        var builder = new StringBuilder(match.Events.Count * 160);
        foreach (var matchEvent in match.Events)
        {
            builder.Append(JsonSerializer.Serialize(matchEvent, ContractJson.Options)).Append('\n');
        }

        await File.WriteAllTextAsync(eventsPath, builder.ToString(), cancellationToken).ConfigureAwait(false);
    }
}
