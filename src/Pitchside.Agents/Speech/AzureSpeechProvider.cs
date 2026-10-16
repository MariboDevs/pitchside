using System.Net.Http.Headers;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Options;
using Pitchside.Runtime.Recaps;

namespace Pitchside.Agents.Speech;

/// <summary>
/// Azure AI Speech text to speech over its REST endpoint, keyless: the host's identity gets an Entra ID token for
/// Cognitive Services and sends it as <c>aad#{resourceId}#{token}</c>, the form the regional speech endpoint accepts.
/// The answer is 24 kHz mono MP3.
/// </summary>
public sealed class AzureSpeechProvider(HttpClient http, TokenCredential credential, IOptions<SpeechOptions> options) : ISpeechProvider
{
    public const string OutputFormat = "audio-24khz-48kbitrate-mono-mp3";

    private static readonly string[] Scopes = ["https://cognitiveservices.azure.com/.default"];

    public string Name => "azure";

    public async Task<byte[]> SynthesizeAsync(string ssml, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ssml);
        var speech = options.Value;
        var region = speech.Region ?? throw new InvalidOperationException("Speech:Region is not configured.");
        var resourceId = speech.ResourceId ?? throw new InvalidOperationException("Speech:ResourceId is not configured.");
        var token = await credential.GetTokenAsync(new TokenRequestContext(Scopes), cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"aad#{resourceId}#{token.Token}");
        request.Headers.Add("X-Microsoft-OutputFormat", OutputFormat);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("pitchside", "1.0"));
        request.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Speech synthesis failed with {(int)response.StatusCode} {response.ReasonPhrase}.", null, response.StatusCode);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }
}
