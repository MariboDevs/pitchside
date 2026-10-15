using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pitchside.Contracts;
using Pitchside.Runtime.Audit;

namespace Pitchside.Agents.Providers;

/// <summary>
/// Wraps the configured provider so every model call writes an <see cref="AuditRecord"/> (WP07-R6): the stage, the
/// model, the prompt version and the SHA-256 of the prompt's text (never the text), tokens, latency and outcome.
/// A failed or late call is audited and still fails. An audit write that fails is logged and never fails the call.
/// </summary>
public sealed partial class AuditingChatProvider(IChatProvider inner, IAuditStore store, TimeProvider time, ILogger<AuditingChatProvider>? logger = null) : IChatProvider
{
    private readonly ILogger _logger = logger ?? NullLogger<AuditingChatProvider>.Instance;

    public string Name => inner.Name;

    public async Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = time.GetTimestamp();
        try
        {
            var result = await inner.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            await AuditAsync(request, started, AuditOutcome.Ok, result).ConfigureAwait(false);
            return result;
        }
        catch (TimeoutException)
        {
            await AuditAsync(request, started, AuditOutcome.Timeout, null).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await AuditAsync(request, started, AuditOutcome.Error, null).ConfigureAwait(false);
            throw;
        }
    }

    public static string HashOf(string promptText) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(promptText)));

    private async Task AuditAsync(ChatRequest request, long started, AuditOutcome outcome, ChatResult? result)
    {
        var now = time.GetUtcNow();
        var record = new AuditRecord
        {
            Id = Ulid.New(now),
            MatchId = request.Moment.MatchId,
            MomentId = request.Moment.Id,
            Stage = request.PromptName,
            Provider = result?.Provider ?? inner.Name,
            Model = result?.Model ?? string.Empty,
            PromptVersion = request.PromptVersion,
            PromptHash = HashOf(request.SystemPrompt),
            InputTokens = result?.TokensIn ?? 0,
            OutputTokens = result?.TokensOut ?? 0,
            LatencyMs = (long)time.GetElapsedTime(started).TotalMilliseconds,
            Outcome = outcome,
            RequestId = Activity.Current?.TraceId.ToHexString() ?? string.Empty,
            Utc = now,
        };

        try
        {
            // The record is written even when the call was cancelled, so the audit never misses a call that ran.
            await store.SaveAsync(record, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogAuditFailed(ex, record.MomentId, record.Stage);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Moment {MomentId}: the audit record for stage {Stage} could not be written")]
    private partial void LogAuditFailed(Exception exception, string momentId, string stage);
}
