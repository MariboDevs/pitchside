namespace Pitchside.Runtime.Messages;

/// <summary>Internal: reading the match from the store failed.</summary>
public sealed record LoadFailed(Exception Cause);
