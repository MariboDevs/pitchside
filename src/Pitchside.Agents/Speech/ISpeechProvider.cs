namespace Pitchside.Agents.Speech;

/// <summary>Turns an SSML document into MP3 audio (WP10-R2).</summary>
public interface ISpeechProvider
{
    string Name { get; }

    Task<byte[]> SynthesizeAsync(string ssml, CancellationToken cancellationToken);
}
