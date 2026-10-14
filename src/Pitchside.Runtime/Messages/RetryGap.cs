namespace Pitchside.Runtime.Messages;

/// <summary>Internal: ask the store again for a gap that is still open.</summary>
public sealed record RetryGap
{
    public static RetryGap Instance { get; } = new();
}
