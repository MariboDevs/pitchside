namespace Pitchside.Engine.Calibration;

public sealed record CalibrationRow(string Statistic, double Synthetic, double Target, double Tolerance)
{
    public double Gap => Synthetic - Target;

    public bool Passes => Math.Abs(Gap) <= Tolerance;
}
