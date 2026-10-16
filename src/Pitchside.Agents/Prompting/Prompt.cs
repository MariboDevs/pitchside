namespace Pitchside.Agents.Prompting;

/// <summary>A prompt file: its name, its version (the file name, such as <c>explainer-v1</c>) and its text.</summary>
public sealed record Prompt(string Name, int Version, string Text)
{
    public string VersionLabel => $"{Name}-v{Version}";
}
