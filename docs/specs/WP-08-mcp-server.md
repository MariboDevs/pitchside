# WP-08: MCP server

| | |
| --- | --- |
| Branch | `feat/wp08-mcp` |
| Depends on | WP-03 |
| Owner | Sipho (implemented by Claude Code) |
| Effort | 1 day |
| Changes contracts | none |

## Goal

Expose live match intelligence as Model Context Protocol tools, so any MCP client (GitHub Copilot, Claude, a
judge's own agent, the Azure MCP ecosystem) can ask a running match questions. This is the "Azure MCP" and
"MCP integration" line of the rubric, and it is the same pattern as CodeLens's nine-tool server.

## Tools

| Tool | Arguments | Returns |
| --- | --- | --- |
| `list_matches` | | matches with status, score, clock |
| `get_match` | `matchId` | the document |
| `get_fact_sheet` | `matchId`, `atSeq?` | the fact sheet |
| `get_fact` | `matchId`, `key` | one fact with its `derivedFrom` events |
| `list_moments` | `matchId`, `type?` | moments |
| `get_narrative` | `matchId`, `momentId`, `persona?`, `language?` | the narrative and its trace |
| `get_overlay_timeline` | `matchId`, `fromMs?` | overlay entries |
| `get_events` | `matchId`, `fromSeq`, `toSeq` (max 200) | events |
| `explain_moment` | `matchId`, `momentId` | runs the base explainer on demand (respects the usage guard) |

Every tool is read-only except `explain_moment`. Descriptions are written for a model reader: one sentence of
purpose, the argument semantics, and the shape of the result.

## Behaviour

- Package `ModelContextProtocol` (C# SDK). HTTP transport hosted inside `Pitchside.Api` at `/mcp` (same
  process as the actors, so reads are direct), and a stdio host `src/Pitchside.Mcp.Stdio/` for local clients
  that proxies to the API.
- Tenant and auth: the hackathon demo is anonymous read; `explain_moment` requires the `X-Pitchside-Key`
  header matching `Mcp:ApiKey` when set. Design the tool registration so an OAuth resource-server layer can
  be added, as CodeLens did, without changing tool code.
- Audit: every tool call writes an `AuditRecord` with stage `mcp:{tool}`.
- Azure MCP: document in `docs/MCP.md` how the server is registered alongside the Azure MCP server in a
  client configuration, with a worked example for Claude Code and for VS Code Copilot agent mode.

## Files

- `src/Pitchside.Api/Mcp/PitchsideTools.cs` (one class, tool methods with attributes), `Program.cs` wiring
- `src/Pitchside.Mcp.Stdio/`
- `docs/MCP.md`
- `tests/Pitchside.Api.IntegrationTests/McpTests.cs` (list tools, call each read tool against a replayed
  match, schema of results)

## Done when

```bash
dotnet test Pitchside.slnx
dotnet run --project src/Pitchside.Api &
# with any MCP client, or:
curl -s localhost:5180/mcp -H 'content-type: application/json' -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | python3 -m json.tool | head -40
```

## Notes for the implementer

- Read the current ModelContextProtocol C# SDK README for hosting in ASP.NET Core; the attribute and
  registration names there win over anything in this document.
- Results must be compact: never return a full event list without the range limit.
