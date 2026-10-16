using Pitchside.Contracts;

namespace Pitchside.Agents.Speech;

/// <summary>A spoken recap and its MP3, as the recap stage hands them on.</summary>
public sealed record RecapAudio(Recap Recap, byte[] Audio);
