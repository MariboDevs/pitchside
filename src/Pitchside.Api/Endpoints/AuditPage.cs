using Pitchside.Contracts;

namespace Pitchside.Api.Endpoints;

/// <summary>A page of a match's audit records in call order; <see cref="Next"/> is the cursor for the next page, or null at the end.</summary>
public sealed record AuditPage(IReadOnlyList<AuditRecord> Items, string? Next);
