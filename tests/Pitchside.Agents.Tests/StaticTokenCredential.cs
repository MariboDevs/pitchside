using Azure.Core;

namespace Pitchside.Agents.Tests;

/// <summary>A credential that always hands out the same token, so no test signs in anywhere.</summary>
internal sealed class StaticTokenCredential(string token) : TokenCredential
{
    public List<string[]> Requested { get; } = [];

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        Requested.Add(requestContext.Scopes);
        return new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1));
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        ValueTask.FromResult(GetToken(requestContext, cancellationToken));
}
