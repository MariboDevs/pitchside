namespace Pitchside.Runtime.Messages;

/// <summary>Internal: try reading the match again, because its document was not stored yet.</summary>
public sealed record RetryLoad(int Attempt);
