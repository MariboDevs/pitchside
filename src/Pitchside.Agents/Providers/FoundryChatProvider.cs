using System.ClientModel.Primitives;
using Azure.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Pitchside.Agents.Providers;

/// <summary>
/// The Foundry provider: Microsoft Agent Framework's OpenAI integration against the Azure OpenAI v1 endpoint of a
/// Foundry project, keyless through a token credential (managed identity in Azure, the developer's sign-in
/// locally). Temperature 0, JSON output, 400 output tokens per call.
/// </summary>
public sealed class FoundryChatProvider : IChatProvider
{
    public const string ProviderName = "foundry";
    private const string Scope = "https://cognitiveservices.azure.com/.default";

    private readonly FoundryOptions _options;
    private readonly TokenCredential _credential;

    public FoundryChatProvider(IOptions<FoundryOptions> options, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credential);
        _options = options.Value;
        _credential = credential;
    }

    public string Name => ProviderName;

    public async Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var deployment = Deployment(request.Tier);
        AIAgent agent = CreateClient(deployment).AsAIAgent(instructions: request.SystemPrompt, name: request.PromptName);
        var options = new ChatClientAgentRunOptions(new ChatOptions
        {
            Temperature = 0,
            MaxOutputTokens = request.MaxOutputTokens,
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Json,
        });
        var response = await agent.RunAsync(request.UserMessage, options: options, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new ChatResult(response.Text, ProviderName, deployment, response.Usage?.InputTokenCount ?? 0, response.Usage?.OutputTokenCount ?? 0);
    }

    private string Deployment(DeploymentTier tier)
    {
        var name = tier == DeploymentTier.Quality ? _options.Deployments.Quality : _options.Deployments.Fast;
        return string.IsNullOrWhiteSpace(name)
            ? throw new InvalidOperationException($"Foundry:Deployments:{tier} is not configured.")
            : name;
    }

    private ChatClient CreateClient(string deployment)
    {
        var endpoint = _options.Endpoint ?? throw new InvalidOperationException("Foundry:Endpoint is not configured.");
        var v1 = new Uri(endpoint, endpoint.AbsolutePath.TrimEnd('/').EndsWith("/openai/v1", StringComparison.Ordinal) ? endpoint.AbsolutePath : "openai/v1/");
        var policy = new BearerTokenPolicy(_credential, Scope);

        // The OpenAI SDK's token-policy constructor is the keyless route to the Azure OpenAI v1 endpoint; the SDK
        // still marks it for evaluation (OPENAI001). Azure.AI.OpenAI's stable release pins an older OpenAI SDK than
        // Microsoft.Agents.AI.OpenAI requires, so it cannot be used alongside it.
#pragma warning disable OPENAI001
        return new ChatClient(deployment, policy, new OpenAIClientOptions { Endpoint = v1 });
#pragma warning restore OPENAI001
    }
}
