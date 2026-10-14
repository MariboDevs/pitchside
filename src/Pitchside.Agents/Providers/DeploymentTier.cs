namespace Pitchside.Agents.Providers;

/// <summary>Which model deployment a call goes to: Fast for routine moments, Quality for summaries and checking.</summary>
public enum DeploymentTier
{
    Fast,
    Quality,
}
