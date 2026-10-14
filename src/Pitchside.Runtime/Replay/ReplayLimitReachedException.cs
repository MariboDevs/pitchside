namespace Pitchside.Runtime.Replay;

/// <summary>Thrown when starting a replay would exceed <c>Replay:MaxConcurrent</c>.</summary>
public sealed class ReplayLimitReachedException : Exception
{
    public ReplayLimitReachedException()
    {
    }

    public ReplayLimitReachedException(string message)
        : base(message)
    {
    }

    public ReplayLimitReachedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
