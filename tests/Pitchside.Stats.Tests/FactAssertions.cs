using System.Text.Json;
using Pitchside.Contracts;

namespace Pitchside.Stats.Tests;

internal static class FactAssertions
{
    /// <summary>Two sheets are equal when they hold the same facts, in the same order, with the same JSON.</summary>
    public static void ShouldEqualSheet(this FactSheet actual, FactSheet expected)
    {
        actual.AsOfSeq.ShouldBe(expected.AsOfSeq);
        actual.Period.ShouldBe(expected.Period);
        actual.ClockMs.ShouldBe(expected.ClockMs);
        actual.Facts.Select(f => f.Key).ToList().ShouldBe(expected.Facts.Select(f => f.Key).ToList());
        for (var i = 0; i < actual.Facts.Count; i++)
        {
            Json(actual.Facts[i]).ShouldBe(Json(expected.Facts[i]), $"fact {actual.Facts[i].Key} at seq {actual.AsOfSeq}");
        }
    }

    public static double NumberOf(this FactSheet sheet, string key) =>
        (sheet.Find(key) ?? throw new KeyNotFoundException(key)).Value.Number ?? throw new InvalidOperationException($"{key} is not a number.");

    public static string TextOf(this FactSheet sheet, string key) =>
        (sheet.Find(key) ?? throw new KeyNotFoundException(key)).Value.Text ?? throw new InvalidOperationException($"{key} is not text.");

    private static string Json(Fact fact) => JsonSerializer.Serialize(fact, ContractJson.Options);
}
