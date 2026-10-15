namespace Pitchside.Contracts;

/// <summary>How a model call ended: it returned, it failed, or it ran past its deadline.</summary>
public enum AuditOutcome
{
    Ok,
    Error,
    Timeout,
}
