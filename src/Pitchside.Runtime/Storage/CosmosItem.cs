using System.Text.Json.Serialization;

namespace Pitchside.Runtime.Storage;

/// <summary>A Cosmos DB item: the document id, the <c>/matchId</c> partition key, and the contract record as the payload.</summary>
public sealed record CosmosItem<T>(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("matchId")] string MatchId,
    [property: JsonPropertyName("seq")] int Seq,
    [property: JsonPropertyName("payload")] T Payload);
