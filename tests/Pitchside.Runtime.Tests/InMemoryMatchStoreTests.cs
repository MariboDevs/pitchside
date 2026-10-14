using Pitchside.Contracts;
using Pitchside.Moments;
using Pitchside.Runtime.Storage;

namespace Pitchside.Runtime.Tests;

public sealed class InMemoryMatchStoreTests
{
    private readonly InMemoryMatchStore _store = new();
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task A_match_is_saved_listed_and_updated()
    {
        await _store.SaveMatchAsync(Sample.Stored, None);
        (await _store.ListMatchesAsync(None)).ShouldHaveSingleItem().MatchId.ShouldBe("MER-7");

        await _store.UpdateMatchStatusAsync("MER-7", MatchStatus.Live, 120, None);
        await _store.UpdateMatchStatusAsync("MER-7", MatchStatus.Live, 80, None);
        var match = (await _store.GetMatchAsync("MER-7", None))!;
        match.Status.ShouldBe(MatchStatus.Live);
        match.LastSeq.ShouldBe(120);
        (await _store.GetMatchAsync("MER-8", None)).ShouldBeNull();
    }

    [Fact]
    public async Task Events_are_read_back_by_range_in_order()
    {
        foreach (var e in Sample.Events.Take(30).Reverse())
        {
            await _store.AppendEventAsync(e, None);
        }

        (await _store.GetEventsAsync("MER-7", 5, 9, None)).Select(e => e.Seq).ShouldBe([5, 6, 7, 8, 9]);
        (await _store.GetEventsAsync("MER-7", 25, 100, None)).Count.ShouldBe(6);
        (await _store.GetEventsAsync("MER-1", 1, 100, None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_fact_sheet_at_a_seq_is_the_latest_stored_at_or_before_it()
    {
        foreach (var seq in new[] { 10, 60, 120 })
        {
            await _store.SaveFactSheetAsync(new FactSheet("MER-7", seq, 1, seq * 1000, []), None);
        }

        (await _store.GetFactSheetAsync("MER-7", 100, None))!.AsOfSeq.ShouldBe(60);
        (await _store.GetFactSheetAsync("MER-7", 120, None))!.AsOfSeq.ShouldBe(120);
        (await _store.GetFactSheetAsync("MER-7", 5, None)).ShouldBeNull();
    }

    [Fact]
    public async Task Moments_and_narratives_are_kept_per_match_and_upserted_by_id()
    {
        var moments = MatchMoments.Detect(Sample.Document, Sample.Events.Take(400));
        foreach (var moment in moments.Concat(moments))
        {
            await _store.SaveMomentAsync(moment, None);
        }

        (await _store.GetMomentsAsync("MER-7", None)).Select(m => m.Id).ShouldBe(moments.Select(m => m.Id));
        (await _store.GetNarrativesAsync("MER-7", None)).ShouldBeEmpty();
    }
}
