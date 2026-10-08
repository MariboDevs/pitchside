namespace Pitchside.Contracts;

public sealed record TeamSheet(
    Club Club,
    string Formation,
    IReadOnlyList<Player> Starters,
    IReadOnlyList<Player> Bench);
