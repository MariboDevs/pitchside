using Azure.Core;

namespace Pitchside.Agents.Tests;

/// <summary>A credential that mints a distinct token per call and counts the calls.</summary>
public sealed class CountingTokenCredential(TimeProvider time, TimeSpan lifetime) : TokenCredential
{
    public int Calls { get; private set; }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        Calls++;
        return ValueTask.FromResult(new AccessToken($"token-{Calls}-{requestContext.Scopes[0]}", time.GetUtcNow() + lifetime));
    }
}
