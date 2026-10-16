using System.Collections.Concurrent;
using Pitchside.Contracts;

namespace Pitchside.Runtime.Recaps;

/// <summary>Recaps held in process: local runs, tests and the CLI.</summary>
public sealed class InMemoryRecapStore : IRecapStore
{
    private readonly ConcurrentDictionary<(string MatchId, string Language), (Recap Recap, byte[] Audio)> _recaps = new();

    public Task SaveAsync(Recap recap, ReadOnlyMemory<byte> audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recap);
        _recaps[(recap.MatchId, recap.Language)] = (recap, audio.ToArray());
        return Task.CompletedTask;
    }

    public Task<Recap?> GetAsync(string matchId, string language, CancellationToken cancellationToken) =>
        Task.FromResult(_recaps.TryGetValue((matchId, language), out var found) ? found.Recap : null);

    public Task<byte[]?> GetAudioAsync(string matchId, string language, CancellationToken cancellationToken) =>
        Task.FromResult(_recaps.TryGetValue((matchId, language), out var found) ? found.Audio : null);
}
