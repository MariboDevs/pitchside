namespace Pitchside.Contracts;

/// <summary>A squad member. <see cref="WorkRate"/> is metres per minute on the ball-less run, 95 to 125.</summary>
public sealed record Player(
    string Id,
    string Name,
    int ShirtNumber,
    Position Position,
    double WorkRate);
