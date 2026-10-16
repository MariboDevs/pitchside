using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Pitchside.Agents.Prompting;

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

        var pronunciation = assembly.GetManifestResourceNames().SingleOrDefault(n => n == PromptPrefix + "pronunciation.json")
            ?? throw new InvalidOperationException("prompts/pronunciation.json is not embedded.");
        Pronunciations = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(Read(assembly, pronunciation))
            ?? throw new InvalidOperationException("prompts/pronunciation.json is empty.");

        var forbidden = assembly.GetManifestResourceNames().SingleOrDefault(n => n == PromptPrefix + "forbidden.sha256")
            ?? throw new InvalidOperationException("prompts/forbidden.sha256 is not embedded.");
        ForbiddenHashes = [.. Read(assembly, forbidden)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith('#'))];
    }

    public IReadOnlyCollection<string> PromptNames => _latest.Keys;

    /// <summary>
    /// The spoken recap's phonetic aliases (WP10-R1), per language and then per club or player name, from
    /// <c>prompts/pronunciation.json</c>. A name without one is read as written.
    /// </summary>
    public IReadOnlyDictionary<string, Dictionary<string, string>> Pronunciations { get; }

    public IReadOnlyCollection<string> TemplateTypes => _templates.Keys;

    /// <summary>
    /// SHA-256 of each word that may never appear in a narrative (see <see cref="Checking.CodeFactChecker.Hash"/>).
    /// The words themselves are never committed.
    /// </summary>
    public IReadOnlyList<string> ForbiddenHashes { get; }

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

    [GeneratedRegex(@"^(?<name>[a-z]+(?:-[a-z]+)*)-v(?<version>[0-9]+)\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionedFile();
}
