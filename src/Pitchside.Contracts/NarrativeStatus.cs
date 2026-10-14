namespace Pitchside.Contracts;

/// <summary>Where a narrative stands: written, checked and passed, rejected, or replaced by a code template.</summary>
public enum NarrativeStatus
{
    Draft,
    Verified,
    Rejected,
    Fallback,
}
