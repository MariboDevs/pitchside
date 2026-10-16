namespace Pitchside.Agents.Speech;

/// <summary>
/// One second of silent MP3 for any SSML (WP10-R5), so tests and CI run the whole recap path with no network. The
/// clip is 39 MPEG-1 Layer III frames, 128 kbit/s, 44.1 kHz, mono, each a valid header followed by silence.
/// </summary>
public sealed class ScriptedSpeechProvider : ISpeechProvider
{
    /// <summary>144 * 128000 / 44100 bytes per frame, no padding.</summary>
    public const int FrameBytes = 417;

    /// <summary>A frame holds 1152 samples, so 39 frames last just over one second at 44.1 kHz.</summary>
    public const int Frames = 39;

    private static readonly byte[] Clip = Build();

    public string Name => "scripted";

    public Task<byte[]> SynthesizeAsync(string ssml, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ssml);
        return Task.FromResult(Clip.ToArray());
    }

    private static byte[] Build()
    {
        var clip = new byte[FrameBytes * Frames];
        for (var frame = 0; frame < Frames; frame++)
        {
            // Sync, MPEG-1, Layer III, no CRC; 128 kbit/s, 44.1 kHz, no padding; mono, original.
            var offset = frame * FrameBytes;
            clip[offset] = 0xFF;
            clip[offset + 1] = 0xFB;
            clip[offset + 2] = 0x90;
            clip[offset + 3] = 0xC4;
        }

        return clip;
    }
}
