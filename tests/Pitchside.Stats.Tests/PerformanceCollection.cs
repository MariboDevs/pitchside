namespace Pitchside.Stats.Tests;

/// <summary>Runs timing tests on their own, after the parallel tests, so they measure the engine and not the CI box.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceCollection
{
    public const string Name = "Performance";
}
