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
/// locally). JSON output, 400 output tokens per call, the tier's reasoning effort (see
/// <see cref="FoundryReasoningEffort"/>), and a deadline per tier: <c>Foundry:TimeoutSeconds</c> on the fast tier,
/// <c>Foundry:QualityTimeoutSeconds</c> on the quality tier.
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
        var options = new ChatClientAgentRunOptions(FoundryChatOptionsBuilder.Build(_options, request.Tier, request.MaxOutputTokens));
        await WarmTokenAsync(cancellationToken).ConfigureAwait(false);
        var timeout = _options.TimeoutFor(request.Tier);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        AgentResponse response;
        try
        {
            response = await agent.RunAsync(request.UserMessage, options: options, cancellationToken: deadline.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested && ProviderFailure.IsCancellation(ex))
        {
            // The deadline fired: as a plain cancellation, or wrapped by the SDK's retry policy with the error before it.
            throw new TimeoutException($"Foundry deployment '{deployment}' did not answer within {timeout.TotalSeconds:0} s.", ex);
        }

        return new ChatResult(response.Text, ProviderName, deployment, response.Usage?.InputTokenCount ?? 0, response.Usage?.OutputTokenCount ?? 0);
    }

    /// <summary>
    /// A chat client on a Foundry deployment for other model work, such as the groundedness judge (WP-07); the
    /// quality deployment when <paramref name="deployment"/> is empty. Each call carries the quality tier's reasoning
    /// effort and deadline.
    /// </summary>
    public IChatClient ChatClientFor(string? deployment)
    {
        var name = string.IsNullOrWhiteSpace(deployment) ? Deployment(DeploymentTier.Quality) : deployment;
        var effort = _options.ReasoningEffort.For(DeploymentTier.Quality);
        var client = CreateClient(name).AsIChatClient().AsBuilder()
            .ConfigureOptions(chat => FoundryChatOptionsBuilder.Apply(chat, effort))
            .Build();
        return new DeadlineChatClient(client, _options.TimeoutFor(DeploymentTier.Quality), name, WarmTokenAsync);
    }

    /// <summary>
    /// Fetches the token, or serves it from the cache, before a call's deadline starts: a developer credential can
    /// take as long as the fast tier's whole budget to answer, and that time is not the model's.
    /// </summary>
    public async ValueTask WarmTokenAsync(CancellationToken cancellationToken) =>
        await _credential.GetTokenAsync(new TokenRequestContext([Scope]), cancellationToken).ConfigureAwait(false);

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
