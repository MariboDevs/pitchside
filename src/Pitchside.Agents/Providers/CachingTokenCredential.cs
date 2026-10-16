using Azure.Core;

namespace Pitchside.Agents.Providers;

/// <summary>
/// Caches the access token of the credential it wraps, per scope set, and refreshes it a few minutes before it
/// expires. The OpenAI SDK's bearer token policy asks the credential on every call, and a developer credential
/// such as the Azure CLI spends about two seconds per answer, half of the fast tier's budget.
/// </summary>
public sealed class CachingTokenCredential(TokenCredential inner, TimeProvider? time = null) : TokenCredential
{
    private static readonly TimeSpan RefreshBefore = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, AccessToken> _tokens = new(StringComparer.Ordinal);

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var key = string.Join(' ', requestContext.Scopes);
        if (TryFresh(key, out var cached))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryFresh(key, out cached))
            {
                return cached;
            }

            var token = await inner.GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);
            lock (_tokens)
            {
                _tokens[key] = token;
            }

            return token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryFresh(string key, out AccessToken token)
    {
        lock (_tokens)
        {
            return _tokens.TryGetValue(key, out token) && token.ExpiresOn - RefreshBefore > _time.GetUtcNow();
        }
    }
}
