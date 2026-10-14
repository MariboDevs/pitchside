using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Pitchside.Agents.Prompts;

/// <summary>
/// The prompts, templates and forbidden vocabulary, embedded from <c>src/Pitchside.Agents/prompts/</c>. A prompt is
/// versioned by its file name suffix, <c>{name}-v{n}.md</c>; <see cref="Get"/> returns the highest version.
/// </summary>
public sealed partial class PromptCatalog
{
    private const string PromptPrefix = "Pitchside.Agents.prompts.";
    private const string TemplatePrefix = "Pitchside.Agents.prompts.templates.";

    private readonly Dictionary<string, Prompt> _latest = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _templates = new(StringComparer.Ordinal);

    public PromptCatalog()
    {
        var assembly = typeof(PromptCatalog).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (resource.StartsWith(TemplatePrefix, StringComparison.Ordinal) && resource.EndsWith(".md", StringComparison.Ordinal))
            {
                _templates[resource[TemplatePrefix.Length..^3]] = Read(assembly, resource);
            }
            else if (resource.StartsWith(PromptPrefix, StringComparison.Ordinal) && VersionedFile().Match(resource[PromptPrefix.Length..]) is { Success: true } match)
            {
                var prompt = new Prompt(match.Groups["name"].Value, int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture), Read(assembly, resource));
                if (!_latest.TryGetValue(prompt.Name, out var current) || current.Version < prompt.Version)
                {
                    _latest[prompt.Name] = prompt;
                }
            }
        }

        var forbidden = assembly.GetManifestResourceNames().SingleOrDefault(n => n == PromptPrefix + "forbidden.txt")
            ?? throw new InvalidOperationException("prompts/forbidden.txt is not embedded.");
        ForbiddenWords = [.. Read(assembly, forbidden)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith('#'))];
    }

    public IReadOnlyCollection<string> PromptNames => _latest.Keys;

    public IReadOnlyCollection<string> TemplateTypes => _templates.Keys;

    /// <summary>Words that may never appear in a narrative, whole word, any case.</summary>
    public IReadOnlyList<string> ForbiddenWords { get; }

    public Prompt Get(string name) => _latest.TryGetValue(name, out var prompt)
        ? prompt
        : throw new KeyNotFoundException($"No prompt named '{name}'.");

    /// <summary>The fallback template for a moment type: one sentence per line, numbers as <c>[[key]]</c> tokens.</summary>
    public string Template(string momentType) => _templates.TryGetValue(momentType, out var template)
        ? template
        : throw new KeyNotFoundException($"No template for moment type '{momentType}'.");

    private static string Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"^(?<name>[a-z]+)-v(?<version>[0-9]+)\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionedFile();
}
