using System.Globalization;

namespace Pitchside.Moments;

/// <summary>
/// Code templates for moment headlines. They are for the control room list and the scripted fallback; a viewer
/// never sees one without the explainer unless the scripted provider is in use.
/// </summary>
public static class MomentHeadlines
{
    public static string Goal(string club, int home, int away) => Format("Goal: {0} {1}-{2}", club, home, away);

    public static string RedCard(string player, string club) => Format("Red card: {0}, {1}", player, club);

    public static string Penalty(string club) => Format("Penalty to {0}", club);

    public static string BigChanceMissed(string player, string club) => Format("Big chance missed: {0}, {1}", player, club);

    public static string MomentumSwing(string club) => Format("Momentum swings to {0}", club);

    public static string SustainedPressure(string club) => Format("{0} keep up the pressure", club);

    public static string Brace(string player, string club) => Format("Brace: {0}, {1}", player, club);

    public static string HatTrick(string player, string club) => Format("Hat-trick: {0}, {1}", player, club);

    public static string CleanSheet(string club) => Format("Clean sheet intact at 80 minutes: {0}", club);

    public static string CleanSheets() => "Clean sheets intact at 80 minutes for both sides";

    public static string Speed(string player) => Format("Speed: {0} passes 33 kph", player);

    public static string Distance(string player) => Format("Distance: {0} passes 10 km", player);

    public static string HardPass(string player) => Format("Hard pass from {0} leads to a shot", player);

    public static string KickOff(string home, string away) => Format("Kick-off: {0} v {1}", home, away);

    public static string HalfTime(string home, int homeGoals, int awayGoals, string away) => Format("Half time: {0} {1}-{2} {3}", home, homeGoals, awayGoals, away);

    public static string FullTime(string home, int homeGoals, int awayGoals, string away) => Format("Full time: {0} {1}-{2} {3}", home, homeGoals, awayGoals, away);

    private static string Format(string template, params object[] values) => string.Format(CultureInfo.InvariantCulture, template, values);
}
