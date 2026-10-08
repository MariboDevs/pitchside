namespace Pitchside.Contracts;

/// <summary>A point on the pitch, metres, attacking team's frame: x towards the goal attacked, y from the left touchline.</summary>
public readonly record struct Location(double X, double Y)
{
    public double DistanceTo(Location other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
