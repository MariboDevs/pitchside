using Pitchside.Contracts;
using Pitchside.Runtime.Recaps;

namespace Pitchside.Agents.Tests;

/// <summary>A recap store that counts every read, and holds nothing.</summary>
internal sealed class CountingRecapStore : IRecapStore
{
    private int _reads;

    public int Reads => Volatile.Read(ref _reads);

    public Task SaveAsync(Recap recap, ReadOnlyMemory<byte> audio, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<Recap?> GetAsync(string matchId, string language, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _reads);
        return Task.FromResult<Recap?>(null);
    }

    public Task<byte[]?> GetAudioAsync(string matchId, string language, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _reads);
        return Task.FromResult<byte[]?>(null);
    }
}
