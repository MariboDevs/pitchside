namespace Pitchside.Runtime.Messages;

/// <summary>Internal: the ten-second timer for the <c>MatchStatus</c> push.</summary>
public sealed record StatusTick
{
    public static StatusTick Instance { get; } = new();
}
