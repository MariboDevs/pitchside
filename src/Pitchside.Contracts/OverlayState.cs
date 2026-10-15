namespace Pitchside.Contracts;

/// <summary>An entry's life: waiting for its in time, held by the producer, on screen or past, or never aired.</summary>
public enum OverlayState
{
    Scheduled,
    Held,
    Aired,
    Expired,
}
