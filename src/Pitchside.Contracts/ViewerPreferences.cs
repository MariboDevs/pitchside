namespace Pitchside.Contracts;

/// <summary>What one viewer wants: a persona, a language, and optionally a club or a player to follow (WP-06).</summary>
public sealed record ViewerPreferences(string Persona, string Language, string? ClubId = null, string? PlayerId = null)
{
    public static ViewerPreferences Default { get; } = new(Personas.Base, Languages.English);
}
