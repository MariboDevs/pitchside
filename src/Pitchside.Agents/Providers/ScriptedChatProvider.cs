using System.Reflection;
using System.Text.Json;
using Pitchside.Agents.Prompts;

namespace Pitchside.Agents.Providers;

/// <summary>
/// The offline provider. Returns canned JSON keyed by prompt name and moment type, from
/// <c>tests/fixtures/scripted/{prompt}.{type}.json</c> (embedded), falling back to <c>{prompt}.default.json</c>.
/// Placeholders such as <c>{{side}}</c> and <c>{{player0Name}}</c> are filled from the user message, the same
/// input a model would see, so a canned answer cites the right keys and names for any moment.
/// </summary>
public sealed class ScriptedChatProvider : IChatProvider
{
    public const string ProviderName = "scripted";
    private const string ResourcePrefix = "Pitchside.Agents.scripted.";

    private readonly IReadOnlyDictionary<string, string> _scripts;

    public ScriptedChatProvider()
    {
        var assembly = typeof(ScriptedChatProvider).Assembly;
        _scripts = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToDictionary(n => n[ResourcePrefix.Length..^".json".Length], n => Read(assembly, n), StringComparer.Ordinal);
    }

    public string Name => ProviderName;

    public IReadOnlyCollection<string> ScriptNames => [.. _scripts.Keys];

    public Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_scripts.TryGetValue($"{request.PromptName}.{request.Moment.Type}", out var script)
            && !_scripts.TryGetValue($"{request.PromptName}.default", out script))
        {
            throw new InvalidOperationException($"No scripted answer for prompt '{request.PromptName}'.");
        }

        using var message = JsonDocument.Parse(request.UserMessage);
        var text = TemplateVariables.Fill(script, TemplateVariables.FromUserMessage(message.RootElement));
        return Task.FromResult(new ChatResult(text, ProviderName, ProviderName, 0, 0));
    }

    private static string Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
