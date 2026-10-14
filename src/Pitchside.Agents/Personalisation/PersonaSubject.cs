namespace Pitchside.Agents.Personalisation;

/// <summary>
/// Who a variant is written for: a persona, and for the club fan the club, for player focus the player. The side is
/// the subject's fact scope (<c>home</c> or <c>away</c>).
/// </summary>
public sealed record PersonaSubject(PersonaSpec Spec, string? SubjectId = null, string? SubjectName = null, string? SubjectSide = null);
