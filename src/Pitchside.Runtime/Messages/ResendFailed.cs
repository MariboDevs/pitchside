namespace Pitchside.Runtime.Messages;

/// <summary>Internal: reading the missing events from the store failed; the gap is requested again later.</summary>
public sealed record ResendFailed(Exception Cause);
