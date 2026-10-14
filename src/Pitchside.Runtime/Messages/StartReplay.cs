namespace Pitchside.Runtime.Messages;

/// <summary>Starts replaying a seeded match at a speed over real time; 0 means as fast as possible.</summary>
public sealed record StartReplay(long Seed, double Speed);
