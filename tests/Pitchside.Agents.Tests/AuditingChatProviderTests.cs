using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Json.Schema;
using Pitchside.Agents.Providers;
using Pitchside.Contracts;
using Pitchside.Runtime.Audit;

namespace Pitchside.Agents.Tests;

public sealed class AuditingChatProviderTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromText(File.ReadAllText(RepositoryRoot.Combine("schemas", "audit-record.schema.json")));

    private static ChatRequest Request() =>
        new("explainer", "explainer-v1", "You are the explainer. Secret prompt text.", "facts here", DeploymentTier.Fast, Matches.Moments(7)[0].Moment, 1);

    [Fact]
    public async Task Every_call_is_audited_with_its_prompt_hash_but_never_its_text()
    {
        var store = new InMemoryAuditStore();
        var audited = new AuditingChatProvider(new FakeChatProvider((_, _) => "answer"), store, TimeProvider.System);
        var request = Request();

        var result = await audited.CompleteAsync(request, CancellationToken.None);

        result.Text.ShouldBe("answer");
        var record = (await store.ListAsync(request.Moment.MatchId, null, 10, CancellationToken.None)).ShouldHaveSingleItem();
        record.MomentId.ShouldBe(request.Moment.Id);
        record.Stage.ShouldBe("explainer");
        record.Provider.ShouldBe("fake");
        record.Model.ShouldBe("fake-model");
        record.PromptVersion.ShouldBe("explainer-v1");
        record.PromptHash.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(request.SystemPrompt))));
        (record.InputTokens, record.OutputTokens).ShouldBe((10L, 20L));
        record.LatencyMs.ShouldBeGreaterThanOrEqualTo(0);
        record.Outcome.ShouldBe(AuditOutcome.Ok);

        var json = JsonSerializer.Serialize(record, ContractJson.Options);
        json.ShouldNotContain("Secret prompt text");
        using var document = JsonDocument.Parse(json);
        Schema.Evaluate(document.RootElement).IsValid.ShouldBeTrue(json);
    }

    [Fact]
    public async Task A_failed_or_late_call_is_audited_and_still_fails()
    {
        var store = new InMemoryAuditStore();
        var failing = new AuditingChatProvider(new FakeChatProvider((_, call) => call == 1 ? throw new TimeoutException() : throw new InvalidOperationException()), store, TimeProvider.System);

        await Should.ThrowAsync<TimeoutException>(() => failing.CompleteAsync(Request(), CancellationToken.None));
        await Should.ThrowAsync<InvalidOperationException>(() => failing.CompleteAsync(Request(), CancellationToken.None));

        var records = await store.ListAsync("MER-7", null, 10, CancellationToken.None);
        records.Select(r => r.Outcome).ShouldBe([AuditOutcome.Timeout, AuditOutcome.Error]);
        records.ShouldAllBe(r => r.Provider == "fake" && r.Model == string.Empty && r.InputTokens == 0);
    }

    [Fact]
    public async Task Records_page_in_call_order()
    {
        var store = new InMemoryAuditStore();
        var audited = new AuditingChatProvider(new FakeChatProvider((_, _) => "answer"), store, TimeProvider.System);
        for (var i = 0; i < 5; i++)
        {
            await audited.CompleteAsync(Request(), CancellationToken.None);
        }

        var first = await store.ListAsync("MER-7", null, 3, CancellationToken.None);
        var rest = await store.ListAsync("MER-7", first[^1].Id, 3, CancellationToken.None);
        first.Count.ShouldBe(3);
        rest.Count.ShouldBe(2);
        first.Concat(rest).Select(r => r.Id).ShouldBe(first.Concat(rest).Select(r => r.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Ulids_sort_by_time_and_use_the_crockford_alphabet()
    {
        var earlier = Ulid.New(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        var later = Ulid.New(new DateTimeOffset(2026, 10, 25, 12, 0, 1, TimeSpan.Zero));
        string.CompareOrdinal(earlier, later).ShouldBeLessThan(0);
        earlier.Length.ShouldBe(26);
        earlier.ShouldAllBe(c => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(c));
    }

    [Fact]
    public void Ulids_created_in_one_millisecond_sort_in_creation_order()
    {
        var instant = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var ids = Enumerable.Range(0, 1_000).Select(_ => Ulid.New(instant)).ToList();

        ids.ShouldBe(ids.Order(StringComparer.Ordinal));
        ids.Distinct().Count().ShouldBe(ids.Count);
        string.CompareOrdinal(Ulid.New(instant.AddSeconds(-1)), ids[^1]).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Ulids_created_at_once_on_many_threads_are_all_distinct()
    {
        var instant = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var ids = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 2_000, _ => ids.Add(Ulid.New(instant)));
        ids.Distinct().Count().ShouldBe(2_000);
    }

    [Fact]
    public async Task A_cancelled_call_is_audited_as_an_error_and_still_cancels()
    {
        var store = new InMemoryAuditStore();
        var cancelled = new AuditingChatProvider(new FakeChatProvider((_, _) => throw new OperationCanceledException()), store, TimeProvider.System);

        await Should.ThrowAsync<OperationCanceledException>(() => cancelled.CompleteAsync(Request(), CancellationToken.None));

        (await store.ListAsync("MER-7", null, 10, CancellationToken.None)).ShouldHaveSingleItem().Outcome.ShouldBe(AuditOutcome.Error);
    }
}
