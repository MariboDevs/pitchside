using Pitchside.Contracts;

namespace Pitchside.Agents.Personalisation;

/// <summary>The four viewer personas of <c>docs/PERSONAS.md</c>, as WP-06 sets their prompts and lengths.</summary>
public static class PersonaSpecs
{
    public static PersonaSpec Analyst { get; } = new(Personas.Analyst, "persona-analyst", 2, 3);

    public static PersonaSpec Casual { get; } = new(Personas.Casual, "persona-casual", 1, 1, MaxWords: 18, MaxNumbers: 1);

    public static PersonaSpec ClubFan { get; } = new(Personas.ClubFan, "persona-clubfan", 1, 2);

    public static PersonaSpec PlayerFocus { get; } = new(Personas.PlayerFocus, "persona-player", 1, 2);

    public static IReadOnlyList<PersonaSpec> All { get; } = [Analyst, Casual, ClubFan, PlayerFocus];

    public static PersonaSpec For(string persona) =>
        All.FirstOrDefault(s => s.Persona == persona) ?? throw new KeyNotFoundException($"No persona '{persona}'.");
}
