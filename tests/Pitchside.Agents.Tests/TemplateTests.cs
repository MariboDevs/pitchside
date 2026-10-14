using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Moments;

namespace Pitchside.Agents.Tests;

/// <summary>Every fallback template verifies by construction, for every moment type and every seed-7 moment.</summary>
public sealed class TemplateTests
{
    private static readonly string[] AllTypes =
    [
        MomentTypes.Goal, MomentTypes.RedCard, MomentTypes.Penalty, MomentTypes.BigChanceMissed, MomentTypes.MomentumSwing,
        MomentTypes.SustainedPressure, MomentTypes.Milestone, MomentTypes.SpeedTag, MomentTypes.PassTag, MomentTypes.KickOff,
        MomentTypes.HalfTime, MomentTypes.FullTime,
    ];

    [Fact]
    public void There_is_a_template_for_every_moment_type() =>
        Crew.Catalog.TemplateTypes.Order(StringComparer.Ordinal).ShouldBe(AllTypes.Order(StringComparer.Ordinal));

    [Fact]
    public void Every_seed_7_moment_has_a_verifying_template()
    {
        foreach (var detected in Matches.Moments(7))
        {
            AssertVerifies(detected);
        }
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void The_template_verifies_for_every_moment_type(string type)
    {
        // Rare types (a red card, a penalty, a speed tag at 33 kph) are found in the first seeds that have them.
        var found = Enumerable.Range(1, 400).SelectMany(seed => Matches.Moments(seed).Where(m => m.Moment.Type == type)).Take(3).ToList();
        found.ShouldNotBeEmpty($"no {type} moment in seeds 1 to 400");
        foreach (var detected in found)
        {
            AssertVerifies(detected);
        }
    }

    public static TheoryData<string> Types() => [.. AllTypes];

    private static void AssertVerifies(DetectedMoment detected)
    {
        var sentences = FallbackComposer.Sentences(detected.Moment, detected.Document, Crew.Catalog);
        sentences.ShouldNotBeEmpty();
        var verdict = Crew.Checker.Check(sentences, detected.Sheet, detected.Moment, detected.Document);
        verdict.Passed.ShouldBeTrue($"{detected.Moment.Id}: {string.Join("; ", verdict.Objections.Select(o => $"{o.Kind} {o.Detail}"))}");

        var context = new MomentContext { Moment = detected.Moment, FactSheet = detected.Sheet, Document = detected.Document };
        var narrative = new FallbackComposer(Crew.Catalog, Crew.Checker, TimeProvider.System).Compose(context);
        narrative.Status.ShouldBe(NarrativeStatus.Fallback);
        narrative.RenderedText.ShouldNotContain("[[");
        narrative.RenderedText.ShouldNotContain("{{");
    }
}
