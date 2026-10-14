namespace Pitchside.Runtime.Messages;

/// <summary>Internal: a write to the store failed.</summary>
public sealed record PersistFailed(string What, Exception Cause);
