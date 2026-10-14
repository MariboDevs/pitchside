using Pitchside.Contracts;

namespace Pitchside.Api.Hubs;

/// <summary>
/// Which narratives a viewer receives (WP06-R2). Everyone gets the base English narrative, which the overlay airs and
/// every view falls back to. A viewer also gets the base narrative and its own persona's variant in its language: for
/// the club fan the variant for its club, for player focus the variant for its player, plus the casual variant, which
/// is what a player-focus viewer sees for a moment its player is not involved in. The control room, persona
/// <see cref="Personas.All"/>, receives every variant in every language.
/// </summary>
public static class NarrativeAudience
{
    public static bool Receives(ViewerPreferences viewer, Narrative narrative)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(narrative);
        if (viewer.Persona == Personas.All || (narrative.Persona == Personas.Base && narrative.Language == Languages.English))
        {
            return true;
        }

        if (narrative.Language != viewer.Language)
        {
            return false;
        }

        return narrative.Persona switch
        {
            Personas.Base => true,
            Personas.ClubFan => viewer.Persona == Personas.ClubFan && narrative.Subject == viewer.ClubId,
            Personas.PlayerFocus => viewer.Persona == Personas.PlayerFocus && narrative.Subject == viewer.PlayerId,
            Personas.Casual => viewer.Persona is Personas.Casual or Personas.PlayerFocus,
            _ => narrative.Persona == viewer.Persona,
        };
    }
}
