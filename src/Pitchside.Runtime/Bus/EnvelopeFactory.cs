using System.Text.Json;
using Pitchside.Contracts;

namespace Pitchside.Runtime.Bus;

/// <summary>Wraps events in envelopes and unwraps them, with the contract serializer.</summary>
public static class EnvelopeFactory
{
    public static EventEnvelope Wrap(MatchEvent matchEvent, DateTimeOffset publishedUtc)
    {
        ArgumentNullException.ThrowIfNull(matchEvent);
        var type = JsonSerializer.Serialize(matchEvent.Type, ContractJson.Options).Trim('"');
        return new EventEnvelope(matchEvent.MatchId, matchEvent.Seq, type, JsonSerializer.Serialize(matchEvent, ContractJson.Options), publishedUtc);
    }

    public static MatchEvent Unwrap(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.Deserialize<MatchEvent>(envelope.PayloadJson, ContractJson.Options)
            ?? throw new JsonException($"Envelope {envelope.MatchId}/{envelope.Seq} has no event.");
    }
}
