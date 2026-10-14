using Pitchside.Contracts;

namespace Pitchside.Moments.Tests;

internal static class MomentAssertions
{
    public static Moment Single(this IReadOnlyList<Moment> moments, string type) =>
        moments.Where(m => m.Type == type).ShouldHaveSingleItem();

    public static void ShouldHaveNone(this IReadOnlyList<Moment> moments, string type) =>
        moments.ShouldNotContain(m => m.Type == type);
}
