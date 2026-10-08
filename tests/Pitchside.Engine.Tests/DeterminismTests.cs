using System.Text.Json;
using Pitchside.Contracts;
using Pitchside.Engine;

namespace Pitchside.Engine.Tests;

public sealed class DeterminismTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(20261027)]
    public void Same_seed_produces_identical_events(long seed)
    {
        var first = MatchSimulator.Simulate(seed);
        var second = MatchSimulator.Simulate(seed);

        Serialize(first).ShouldBe(Serialize(second));
    }

    [Fact]
    public void Different_seeds_produce_different_matches()
    {
        Serialize(MatchSimulator.Simulate(1)).ShouldNotBe(Serialize(MatchSimulator.Simulate(2)));
    }

    [Fact]
    public void Pcg32_matches_the_reference_sequence()
    {
        // First outputs of PCG32 with seed 42, sequence 54, from the reference implementation.
        var rng = new Pcg32(42, 54);
        rng.NextUInt32().ShouldBe(0xa15c02b7u);
        rng.NextUInt32().ShouldBe(0x7b47f409u);
        rng.NextUInt32().ShouldBe(0xba1d3330u);
    }

    private static string Serialize(SimulatedMatch match) =>
        JsonSerializer.Serialize(match.Events, ContractJson.Options);
}
