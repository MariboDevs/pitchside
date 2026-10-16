using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace Pitchside.Agents.Providers;

/// <summary>
/// The per-call options the Foundry provider sends: the output cap, JSON output, the tier's reasoning effort, and
/// temperature 0 only when the effort is <c>none</c>, because a reasoning call rejects any fixed temperature. An empty
/// effort sends no effort and no temperature: the model reasons at its default and would reject a fixed temperature.
/// </summary>
public static class FoundryChatOptionsBuilder
{
    public static ChatOptions Build(FoundryOptions options, DeploymentTier tier, int maxOutputTokens)
    {
        ArgumentNullException.ThrowIfNull(options);
        var chat = new ChatOptions
        {
            MaxOutputTokens = maxOutputTokens,
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Json,
        };
        Apply(chat, options.ReasoningEffort.For(tier));
        return chat;
    }

    /// <summary>Applies the tier's reasoning effort and temperature rule to options another caller built.</summary>
    public static void Apply(ChatOptions chat, string? reasoningEffort)
    {
        ArgumentNullException.ThrowIfNull(chat);
        var effort = reasoningEffort?.Trim();
        chat.Temperature = string.Equals(effort, FoundryReasoningEffort.None, StringComparison.OrdinalIgnoreCase) ? 0 : null;
        if (string.IsNullOrEmpty(effort))
        {
            return;
        }

        // Reasoning effort is still marked for evaluation in the OpenAI SDK (OPENAI001), as the keyless token policy is.
#pragma warning disable OPENAI001
        chat.RawRepresentationFactory = _ => new ChatCompletionOptions { ReasoningEffortLevel = new ChatReasoningEffortLevel(effort) };
#pragma warning restore OPENAI001
    }
}
