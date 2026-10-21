using System.Globalization;
using System.Text.RegularExpressions;

namespace Pitchside.Fabric.Tests;

/// <summary>Reads the committed KQL under <c>fabric/</c>: its files in apply order, its commands and its constants.</summary>
internal static partial class KqlFile
{
    /// <summary>The script folders in the order fabric-apply.sh joins them into DatabaseSchema.kql.</summary>
    public static IReadOnlyList<string> Folders { get; } = ["fabric/kql/schema", "fabric/kql/queries", "fabric/activator"];

    /// <summary>Every .kql file, relative to the repository root, in apply order (folder, then file name).</summary>
    public static IReadOnlyList<string> All() =>
    [
        .. Folders.SelectMany(folder => Directory
            .GetFiles(RepositoryRoot.Combine(folder.Split('/')), "*.kql")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .Select(name => $"{folder}/{name}")),
    ];

    public static string Read(string relativePath) => File.ReadAllText(RepositoryRoot.Combine(relativePath.Split('/'))).ReplaceLineEndings("\n");

    /// <summary>
    /// The commands of a schema script: blocks separated by empty lines, with their comment lines removed, skipping
    /// blocks that held only comments.
    /// </summary>
    public static IReadOnlyList<string> Commands(string relativePath) =>
    [
        .. Read(relativePath)
            .Split("\n\n")
            .Select(block => string.Join('\n', block.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))).Trim())
            .Where(block => block.Length > 0),
    ];

    /// <summary>The numeric <c>let name = value;</c> constants a file declares, by name.</summary>
    public static IReadOnlyDictionary<string, double> Constants(string relativePath) =>
        ConstantPattern()
            .Matches(Read(relativePath))
            .ToDictionary(m => m.Groups["name"].Value, m => double.Parse(m.Groups["value"].Value, CultureInfo.InvariantCulture), StringComparer.Ordinal);

    /// <summary>The names of the functions a script creates with <c>.create-or-alter function</c>.</summary>
    public static IReadOnlyList<string> Functions(string relativePath) =>
        [.. FunctionPattern().Matches(Read(relativePath)).Select(m => m.Groups["name"].Value)];

    [GeneratedRegex(@"^\s*let\s+(?<name>\w+)\s*=\s*(?<value>-?\d+(?:\.\d+)?)\s*;", RegexOptions.Multiline)]
    private static partial Regex ConstantPattern();

    [GeneratedRegex(@"^\.create-or-alter function(?:\s+with\s*\((?:[^()""]|""[^""]*"")*\))?\s+(?<name>\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex FunctionPattern();
}
