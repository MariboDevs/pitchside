using Azure.Core;
using Pitchside.Agents.Providers;

namespace Pitchside.Agents.Tests;

public sealed class CachingTokenCredentialTests
{
    private static readonly TokenRequestContext Cognitive = new(["https://cognitiveservices.azure.com/.default"]);
    private static readonly TokenRequestContext Management = new(["https://management.azure.com/.default"]);

    [Fact]
    public async Task A_fresh_token_is_fetched_once_and_served_from_the_cache()
    {
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-10-16T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var inner = new CountingTokenCredential(time, TimeSpan.FromHours(1));
        var credential = new CachingTokenCredential(inner, time);

        var first = await credential.GetTokenAsync(Cognitive, CancellationToken.None);
        var second = await credential.GetTokenAsync(Cognitive, CancellationToken.None);

        second.Token.ShouldBe(first.Token);
        inner.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task A_token_near_expiry_is_refreshed()
    {
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-10-16T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var inner = new CountingTokenCredential(time, TimeSpan.FromHours(1));
        var credential = new CachingTokenCredential(inner, time);

        await credential.GetTokenAsync(Cognitive, CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(56));
        await credential.GetTokenAsync(Cognitive, CancellationToken.None);

        inner.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task Each_scope_set_has_its_own_token()
    {
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-10-16T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var inner = new CountingTokenCredential(time, TimeSpan.FromHours(1));
        var credential = new CachingTokenCredential(inner, time);

        await credential.GetTokenAsync(Cognitive, CancellationToken.None);
        await credential.GetTokenAsync(Management, CancellationToken.None);
        await credential.GetTokenAsync(Cognitive, CancellationToken.None);

        inner.Calls.ShouldBe(2);
    }

    [Fact]
    public void Wrap_caches_any_credential_once()
    {
        var inner = new CountingTokenCredential(TimeProvider.System, TimeSpan.FromHours(1));

        var wrapped = CachingTokenCredential.Wrap(inner);

        wrapped.ShouldBeOfType<CachingTokenCredential>();
        CachingTokenCredential.Wrap(wrapped).ShouldBeSameAs(wrapped);
    }
}
