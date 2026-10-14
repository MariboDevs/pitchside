namespace Pitchside.Contracts;

/// <summary>The persona names. <see cref="Base"/> is the verified narrative every persona variant is written from.</summary>
public static class Personas
{
    public const string Base = "base";
    public const string Analyst = "analyst";
    public const string Casual = "casual";
    public const string ClubFan = "club_fan";
    public const string PlayerFocus = "player_focus";

    /// <summary>
    /// Not a narrative persona: the viewer setting of the control room, which receives every variant in every
    /// language rather than one.
    /// </summary>
    public const string All = "all";

    public static IReadOnlyList<string> Viewer { get; } = [Analyst, Casual, ClubFan, PlayerFocus];
}
