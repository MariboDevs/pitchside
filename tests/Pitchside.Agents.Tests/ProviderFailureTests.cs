using Azure.Identity;
using Pitchside.Agents.Providers;

namespace Pitchside.Agents.Tests;

public sealed class ProviderFailureTests
{
    [Fact]
    public void A_credential_that_cannot_produce_a_token_is_recoverable()
    {
        ProviderFailure.IsRecoverable(new CredentialUnavailableException("no token")).ShouldBeTrue();
        ProviderFailure.IsRecoverable(new AuthenticationFailedException("refused")).ShouldBeTrue();
    }

    [Fact]
    public void An_aggregate_of_recoverable_failures_is_recoverable()
    {
        var aggregate = new AggregateException(new HttpRequestException("reset"), new CredentialUnavailableException("cancelled"));

        ProviderFailure.IsRecoverable(aggregate).ShouldBeTrue();
    }

    [Fact]
    public void An_aggregate_with_an_unrecoverable_failure_is_not()
    {
        var aggregate = new AggregateException(new HttpRequestException("reset"), new OutOfMemoryException());

        ProviderFailure.IsRecoverable(aggregate).ShouldBeFalse();
        ProviderFailure.IsRecoverable(new AggregateException()).ShouldBeFalse();
    }

    [Fact]
    public void A_cancelled_attempt_inside_an_aggregate_is_recoverable()
    {
        var aggregate = new AggregateException(new HttpRequestException("unavailable"), new TaskCanceledException("deadline"));

        ProviderFailure.IsRecoverable(aggregate).ShouldBeTrue();
        ProviderFailure.IsCancellation(aggregate).ShouldBeTrue();
        ProviderFailure.IsRecoverable(new TaskCanceledException("alone")).ShouldBeFalse();
    }

    [Fact]
    public void A_wrapped_cancellation_is_recognised()
    {
        ProviderFailure.IsCancellation(new InvalidOperationException("outer", new TaskCanceledException("inner"))).ShouldBeTrue();
        ProviderFailure.IsCancellation(new InvalidOperationException("outer")).ShouldBeFalse();
    }
}
