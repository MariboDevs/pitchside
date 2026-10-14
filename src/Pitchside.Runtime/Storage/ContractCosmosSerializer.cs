using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Pitchside.Contracts;

namespace Pitchside.Runtime.Storage;

/// <summary>Serialises Cosmos items with <see cref="ContractJson.Options"/>, so stored documents match the schemas.</summary>
public sealed class ContractCosmosSerializer : CosmosSerializer
{
    public override T FromStream<T>(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using (stream)
        {
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            return JsonSerializer.Deserialize<T>(stream, ContractJson.Options)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, ContractJson.Options);
        stream.Position = 0;
        return stream;
    }
}
