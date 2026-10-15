using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;

namespace Pitchside.Evaluation;

/// <summary>
/// Passes the evaluator's calls to the judge and keeps the SHA-256 of the system prompt it sent, never the text, so
/// the audit record's prompt hash names the prompt that actually ran. One capture serves one judgement.
/// </summary>
internal sealed class PromptCapture(IChatClient judge) : DelegatingChatClient(judge)
{
    /// <summary>The hash of the last system prompt sent, or null before any call.</summary>
    public string? PromptHash { get; private set; }

    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var sent = messages as IReadOnlyCollection<ChatMessage> ?? [.. messages];
        var system = string.Join('\n', sent.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
        if (!string.IsNullOrEmpty(options?.Instructions))
        {
            system = system.Length == 0 ? options.Instructions : $"{options.Instructions}\n{system}";
        }

        PromptHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(system)));
        return base.GetResponseAsync(sent, options, cancellationToken);
    }

    /// <summary>Not wrapped: the dispose of the judge belongs to its owner, not to one judgement.</summary>
    protected override void Dispose(bool disposing)
    {
    }
}
