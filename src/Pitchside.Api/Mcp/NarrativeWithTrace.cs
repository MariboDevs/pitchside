using Pitchside.Api.Endpoints;
using Pitchside.Contracts;

namespace Pitchside.Api.Mcp;

/// <summary>A narrative and its traceability chain, as <c>get_narrative</c> returns it.</summary>
public sealed record NarrativeWithTrace(Narrative Narrative, NarrativeTrace Trace);
