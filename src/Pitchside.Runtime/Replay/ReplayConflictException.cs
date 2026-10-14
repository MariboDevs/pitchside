namespace Pitchside.Runtime.Replay;

/// <summary>Thrown when the match is already replaying or already stored: a match id is replayed once per store.</summary>
public sealed class ReplayConflictException : Exception
{
    public ReplayConflictException()
    {
    }

    public ReplayConflictException(string message)
        : base(message)
    {
    }

    public ReplayConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
