using Pitchside.Agents.Workflow;
using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Agents.Personalisation;

/// <summary>
/// The variants one moment gets (WP06-R1): one analyst and one casual variant, a club-fan variant for each of the two
/// clubs, and a player-focus variant for each player on the moment and no one else. A viewer following a player who
/// is not involved sees the casual variant, never a sentence written about someone absent.
/// </summary>
public static class PersonaSubjects
{
    public static IReadOnlyList<PersonaSubject> For(MomentContext context, IReadOnlyCollection<string> personas)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(personas);
        var document = context.Document;
        var subjects = new List<PersonaSubject>();
        foreach (var spec in PersonaSpecs.All.Where(s => personas.Contains(s.Persona)))
        {
            if (spec.Persona == Personas.ClubFan)
            {
                subjects.Add(new PersonaSubject(spec, document.Home.Club.Id, document.Home.Club.Name, FactKeys.HomeScope));
                subjects.Add(new PersonaSubject(spec, document.Away.Club.Id, document.Away.Club.Name, FactKeys.AwayScope));
            }
            else if (spec.Persona == Personas.PlayerFocus)
            {
                foreach (var playerId in context.Moment.PlayerIds.Distinct(StringComparer.Ordinal))
                {
                    var home = document.Home.Starters.Concat(document.Home.Bench).FirstOrDefault(p => p.Id == playerId);
                    var player = home ?? document.Away.Starters.Concat(document.Away.Bench).FirstOrDefault(p => p.Id == playerId);
                    if (player is not null)
                    {
                        subjects.Add(new PersonaSubject(spec, player.Id, player.Name, home is null ? FactKeys.AwayScope : FactKeys.HomeScope));
                    }
                }
            }
            else
            {
                subjects.Add(new PersonaSubject(spec));
            }
        }

        return subjects;
    }
}
