using Pitchside.Runtime.Usage;

namespace Pitchside.Api.Endpoints;

/// <summary>What the control room shows against the limits: each limit's use, and replays running against the cap.</summary>
public sealed record UsageReport(bool Enforced, IReadOnlyList<UsageCount> Limits, int ReplaysRunning, int MaxConcurrentReplays);
