namespace Pitchside.Contracts;

public sealed record Player(
    string Id,
    string Name,
    int ShirtNumber,
    Position Position);
