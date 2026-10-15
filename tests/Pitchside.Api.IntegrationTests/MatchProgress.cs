using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Pitchside.Contracts;
using Pitchside.Runtime.Actors;
using Pitchside.Runtime.Messages;
using Pitchside.Runtime.Replay;
using Pitchside.Runtime.Storage;

namespace Pitchside.Api.IntegrationTests;

/// <summary>
/// Where a match's pipeline stands, for a test that timed out waiting on it: the store's match, events, moments and
/// narratives, the replays still running, and the match actor's own state, so a stall shows the stage it stopped at.
/// </summary>
internal static class MatchProgress
{
    public static async Task<string> DescribeAsync(IServiceProvider services, string matchId)
    {
        var store = services.GetRequiredService<IMatchStore>();
        var match = await store.GetMatchAsync(matchId, CancellationToken.None);
        var events = await store.GetEventsAsync(matchId, 1, int.MaxValue, CancellationToken.None);
        var moments = await store.GetMomentsAsync(matchId, CancellationToken.None);
        var narratives = await store.GetNarrativesAsync(matchId, CancellationToken.None);
        var replaying = services.GetRequiredService<IReplayService>().Running;
        string actor;
        try
        {
            var matches = await services.GetRequiredService<IRequiredActor<MatchSupervisorActor>>().GetAsync();
            var state = await matches.Ask<MatchStateView>(new GetState(matchId), TimeSpan.FromSeconds(5));
            actor = $"status {state.Status}, last seq {state.LastSeq}, dropped {state.DroppedEvents}";
        }
        catch (Exception ex) when (ex is TimeoutException or AskTimeoutException)
        {
            actor = $"no answer ({ex.GetType().Name})";
        }

        return $"{matchId}: stored status {match?.Status}, last seq {match?.LastSeq}, {events.Count} events, {moments.Count} moments, " +
            $"{narratives.Count} narratives ({narratives.Count(n => n.Persona == Personas.Base && n.Language == Languages.English)} base), " +
            $"replaying [{string.Join(", ", replaying)}], actor {actor}.";
    }
}
