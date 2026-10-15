namespace Pitchside.Agents.Tests;

/// <summary>A clock that always reads the same instant, so generated narratives are byte-for-byte repeatable.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
