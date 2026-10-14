namespace Pitchside.Contracts;

/// <summary>The <c>ApprovalMode</c> SignalR message and the body of <c>POST /matches/{id}/approval</c>.</summary>
public sealed record ApprovalModeUpdate(string MatchId, ApprovalMode Mode);
