using Pitchside.Contracts;

namespace Pitchside.Api.Endpoints;

/// <summary>The body of <c>POST /matches/{id}/approval</c>: <c>auto</c> or <c>approve</c>.</summary>
public sealed record ApprovalRequest(ApprovalMode Mode);
