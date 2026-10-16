using System.Net;
using Pitchside.Agents.Speech;
using Pitchside.Runtime.Recaps;

namespace Pitchside.Agents.Tests;

public sealed class AzureSpeechProviderTests
{
    private const string ResourceId = "/subscriptions/0000/resourceGroups/rg-test/providers/Microsoft.CognitiveServices/accounts/speech-test";

    private static readonly SpeechOptions Options = new()
    {
        Provider = SpeechOptions.AzureProvider,
        Region = "southafricanorth",
        ResourceId = ResourceId,
    };

    [Fact]
    public async Task Synthesis_posts_the_ssml_to_the_regional_endpoint_with_an_entra_token_and_asks_for_mp3()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, [0xFF, 0xFB, 0x90]);
        var credential = new StaticTokenCredential("token-value");
        using var http = new HttpClient(handler);
        var provider = new AzureSpeechProvider(http, credential, Microsoft.Extensions.Options.Options.Create(Options));

        var audio = await provider.SynthesizeAsync("<speak version=\"1.0\">x</speak>", CancellationToken.None);

        audio.ShouldBe(new byte[] { 0xFF, 0xFB, 0x90 });
        var (request, body) = handler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri.ShouldBe(new Uri("https://southafricanorth.tts.speech.microsoft.com/cognitiveservices/v1"));
        request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        request.Headers.Authorization.Parameter.ShouldBe($"aad#{ResourceId}#token-value");
        request.Headers.GetValues("X-Microsoft-OutputFormat").Single().ShouldBe(AzureSpeechProvider.OutputFormat);
        request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/ssml+xml");
        body.ShouldBe("<speak version=\"1.0\">x</speak>");
        credential.Requested.Single().ShouldBe(["https://cognitiveservices.azure.com/.default"]);
    }

    [Fact]
    public async Task A_refused_call_throws_with_its_status()
    {
        using var http = new HttpClient(new RecordingHandler(HttpStatusCode.Unauthorized, []));
        var provider = new AzureSpeechProvider(http, new StaticTokenCredential("t"), Microsoft.Extensions.Options.Options.Create(Options));

        var failure = await Should.ThrowAsync<HttpRequestException>(() => provider.SynthesizeAsync("<speak/>", CancellationToken.None));

        failure.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Without_a_region_or_resource_id_it_says_which_setting_is_missing()
    {
        using var http = new HttpClient(new RecordingHandler(HttpStatusCode.OK, []));
        var provider = new AzureSpeechProvider(http, new StaticTokenCredential("t"), Microsoft.Extensions.Options.Options.Create(new SpeechOptions { Provider = SpeechOptions.AzureProvider }));

        (await Should.ThrowAsync<InvalidOperationException>(() => provider.SynthesizeAsync("<speak/>", CancellationToken.None))).Message.ShouldContain("Speech:Region");
    }

    [Fact]
    public async Task The_scripted_voice_is_one_second_of_valid_silent_mp3()
    {
        var audio = await new ScriptedSpeechProvider().SynthesizeAsync("<speak/>", CancellationToken.None);

        audio.Length.ShouldBe(ScriptedSpeechProvider.FrameBytes * ScriptedSpeechProvider.Frames);
        for (var frame = 0; frame < ScriptedSpeechProvider.Frames; frame++)
        {
            audio.Skip(frame * ScriptedSpeechProvider.FrameBytes).Take(4).ShouldBe(new byte[] { 0xFF, 0xFB, 0x90, 0xC4 });
        }

        // 39 frames of 1152 samples at 44.1 kHz.
        (ScriptedSpeechProvider.Frames * 1152 / 44100.0).ShouldBeInRange(1.0, 1.05);
    }
}
