namespace Pitchside.Agents.Checking;

/// <summary>
/// Ordinary English words that may open a sentence with a capital letter. Any other capitalised word must be a
/// name from the match document; this is what lets the checker catch an invented player or club.
/// </summary>
public static class SentenceStarters
{
    public static IReadOnlySet<string> English { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "A", "After", "Against", "All", "Already", "Also", "An", "And", "Another", "As", "At", "Away", "Back", "Before", "Between",
        "Behind", "Both", "But", "By", "Clean", "Despite", "Distance", "Each", "Early", "Even", "Every", "Expected", "Few", "First", "Fouls",
        "For", "From", "Full", "Goal", "Half", "Hard", "He", "Her", "Here", "His", "Home", "However", "If", "In", "Inside",
        "Into", "It", "Its", "Just", "Kick", "Kick-off", "Late", "Momentum", "More", "Most", "Much", "Neither", "No", "None", "Not",
        "Now", "Of", "On", "One", "Only", "Or", "Over", "Penalty", "Possession", "Pressure", "Red", "Score", "Second", "Shots", "She", "Since",
        "So", "Some", "Speed", "Still", "That", "The", "Their", "Then", "There", "These", "They", "This", "Those", "Through",
        "Time", "To", "Too", "Top", "Two", "Under", "Until", "Up", "What", "When", "While", "With", "Within", "Without", "Yet",
    };

    /// <summary>Capitalised words allowed anywhere: the league and abbreviations football writing uses.</summary>
    public static IReadOnlySet<string> Anywhere { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "I", "Meridian", "League",
    };
}
