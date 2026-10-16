using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Pitchside.Contracts;
using Pitchside.Runtime.Recaps;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.Endpoints;

/// <summary>
/// The spoken full-time recap (WP10-R3): <c>GET /api/v1/matches/{matchId}/recap?language=</c> gives the recap with its
/// SSML, and <c>/recap/audio?language=</c> its MP3, read from the recap store with the host's identity. With
/// <c>Speech:Provider=None</c> both answer 404 with a problem detail saying speech is off.
/// </summary>
public static class RecapEndpoints
{
    public static IEndpointRouteBuilder MapRecapEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/api/v1/matches/{matchId}/recap", GetRecapAsync).WithName("GetRecap");
        app.MapGet("/api/v1/matches/{matchId}/recap/audio", GetAudioAsync).WithName("GetRecapAudio");
        return app;
    }

    private static async Task<Results<Ok<Recap>, ProblemHttpResult, ValidationProblem>> GetRecapAsync(
        string matchId, string? language, IMatchStore store, IRecapStore recaps, IOptions<SpeechOptions> speech, CancellationToken ct)
    {
        var refused = await RefuseAsync(matchId, language, store, speech.Value, ct).ConfigureAwait(false);
        if (refused is not null)
        {
            return refused.Value.Problem is { } problem ? problem : refused.Value.Invalid!;
        }

        return await recaps.GetAsync(matchId, language ?? Languages.English, ct).ConfigureAwait(false) is { } recap
            ? TypedResults.Ok(recap)
            : NoRecapYet();
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult, ValidationProblem>> GetAudioAsync(
        string matchId, string? language, IMatchStore store, IRecapStore recaps, IOptions<SpeechOptions> speech, CancellationToken ct)
    {
        var refused = await RefuseAsync(matchId, language, store, speech.Value, ct).ConfigureAwait(false);
        if (refused is not null)
        {
            return refused.Value.Problem is { } problem ? problem : refused.Value.Invalid!;
        }

        return await recaps.GetAudioAsync(matchId, language ?? Languages.English, ct).ConfigureAwait(false) is { } audio
            ? TypedResults.File(audio, "audio/mpeg", $"{matchId}-{language ?? Languages.English}.mp3")
            : NoRecapYet();
    }

    /// <summary>Why a recap request cannot be answered, or null when it can: speech off, an unknown language, an unknown match.</summary>
    private static async Task<(ProblemHttpResult? Problem, ValidationProblem? Invalid)?> RefuseAsync(
        string matchId, string? language, IMatchStore store, SpeechOptions speech, CancellationToken ct)
    {
        if (!speech.Enabled)
        {
            return (TypedResults.Problem("Speech is off on this server, so there is no spoken recap.", statusCode: StatusCodes.Status404NotFound, title: "Speech is off"), null);
        }

        if (language is not null && !Languages.All.Contains(language))
        {
            return (null, TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["language"] = [$"language is one of {string.Join(", ", Languages.All)}."],
            }));
        }

        return await store.GetMatchAsync(matchId, ct).ConfigureAwait(false) is null
            ? (TypedResults.Problem($"No match {matchId}.", statusCode: StatusCodes.Status404NotFound, title: "No such match"), null)
            : null;
    }

    private static ProblemHttpResult NoRecapYet() =>
        TypedResults.Problem("The recap is spoken once the match reaches full time.", statusCode: StatusCodes.Status404NotFound, title: "No recap yet");
}
