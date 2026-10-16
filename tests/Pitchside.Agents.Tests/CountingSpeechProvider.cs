using System.Collections.Concurrent;
using Pitchside.Agents.Speech;

namespace Pitchside.Agents.Tests;

/// <summary>The scripted voice, recording every SSML it is asked to speak; it fails for any SSML that names <c>failFor</c>.</summary>
internal sealed class CountingSpeechProvider(string? failFor = null) : ISpeechProvider
{
    private readonly ScriptedSpeechProvider _inner = new();

    public ConcurrentQueue<string> Spoken { get; } = new();

    public string Name => "counting";

    public Task<byte[]> SynthesizeAsync(string ssml, CancellationToken cancellationToken)
    {
        Spoken.Enqueue(ssml);
        return failFor is not null && ssml.Contains(failFor, StringComparison.Ordinal)
            ? throw new InvalidOperationException("The voice is unavailable.")
            : _inner.SynthesizeAsync(ssml, cancellationToken);
    }
}
