using Pitchside.Contracts;

namespace Pitchside.Runtime.Recaps;

/// <summary>Where spoken recaps are kept: one recap and one MP3 per match and viewer language.</summary>
public interface IRecapStore
{
    Task SaveAsync(Recap recap, ReadOnlyMemory<byte> audio, CancellationToken cancellationToken);

    Task<Recap?> GetAsync(string matchId, string language, CancellationToken cancellationToken);

    /// <summary>The MP3 of a recap, or null when there is none.</summary>
    Task<byte[]?> GetAudioAsync(string matchId, string language, CancellationToken cancellationToken);
}
