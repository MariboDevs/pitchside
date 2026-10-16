using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Pitchside.Contracts;

namespace Pitchside.Runtime.Recaps;

/// <summary>
/// Recaps in Blob Storage, reached with the host's identity: <c>{matchId}/{language}.mp3</c> and the recap beside it as
/// <c>{matchId}/{language}.json</c>, in the <c>Speech:BlobContainer</c> container. The API reads the audio back and
/// serves it, so no shared key or SAS is ever issued.
/// </summary>
public sealed class BlobRecapStore(BlobContainerClient container) : IRecapStore
{
    private bool _created;

    public async Task SaveAsync(Recap recap, ReadOnlyMemory<byte> audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recap);
        if (!_created)
        {
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _created = true;
        }

        // The audio first, so a recap that can be read always has its audio.
        await container.GetBlobClient(AudioName(recap.MatchId, recap.Language)).UploadAsync(
            BinaryData.FromBytes(audio),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "audio/mpeg" } },
            cancellationToken).ConfigureAwait(false);
        await container.GetBlobClient(RecapName(recap.MatchId, recap.Language)).UploadAsync(
            BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(recap, ContractJson.Options)),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Recap?> GetAsync(string matchId, string language, CancellationToken cancellationToken)
    {
        var content = await DownloadAsync(RecapName(matchId, language), cancellationToken).ConfigureAwait(false);
        return content is null ? null : JsonSerializer.Deserialize<Recap>(content, ContractJson.Options);
    }

    public Task<byte[]?> GetAudioAsync(string matchId, string language, CancellationToken cancellationToken) =>
        DownloadAsync(AudioName(matchId, language), cancellationToken);

    private async Task<byte[]?> DownloadAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var result = await container.GetBlobClient(name).DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            return result.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static string AudioName(string matchId, string language) => $"{matchId}/{language}.mp3";

    private static string RecapName(string matchId, string language) => $"{matchId}/{language}.json";
}
