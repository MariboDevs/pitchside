namespace Pitchside.Evaluation;

/// <summary><c>Pricing:*</c>: what a million input and output tokens cost, for the report's estimate; 0 until set.</summary>
public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    public decimal InputPerMillion { get; set; }

    public decimal OutputPerMillion { get; set; }
}
