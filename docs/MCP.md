# The Pitchside MCP server

Pitchside exposes live match intelligence as [Model Context Protocol](https://modelcontextprotocol.io) tools, so any
MCP client (Claude Code, VS Code Copilot agent mode, a judge's own agent) can ask a running match questions. The
server is part of `Pitchside.Api`, in the same process as the match actors, so every read is direct. Spec:
[WP-08](specs/WP-08-mcp-server.md).

## Endpoints

| Transport | Where | For |
| --- | --- | --- |
| Streamable HTTP, stateless | `http://localhost:5180/mcp` (the API) | any client that speaks HTTP MCP |
| stdio | `dotnet run --project src/Pitchside.Mcp.Stdio` | clients that launch a local process; it proxies every call to the API's `/mcp` |

The stdio host reads `PITCHSIDE_MCP_URL` (default `http://localhost:5180/mcp`) and `PITCHSIDE_MCP_KEY` (sent as
`X-Pitchside-Key`) from its environment, and logs to stderr so stdout carries only the protocol.

## Tools

| Tool | Arguments | Returns |
| --- | --- | --- |
| `list_matches` | | every match with its status, score, period and clock |
| `get_match` | `matchId` | the match document: clubs, team sheets, kick-off |
| `get_fact_sheet` | `matchId`, `atSeq?` | the live fact sheet, or the sheet recomputed as of `atSeq` |
| `get_fact` | `matchId`, `key` | one fact and the events it was derived from (at most 200) |
| `list_moments` | `matchId`, `type?` | the detected moments, optionally of one type |
| `get_narrative` | `matchId`, `momentId`, `persona?`, `language?`, `subject?` | the narrative and its trace from each sentence to facts and events; `subject` (a club or player id) picks one of the club-fan or player-focus narratives, and is required when there are several |
| `get_overlay_timeline` | `matchId`, `fromMs?` | the broadcast overlay entries |
| `get_events` | `matchId`, `fromSeq`, `toSeq` | at most 200 events in sequence order |
| `explain_moment` | `matchId`, `momentId` | a fresh base narrative from the agent crew, checked against the moment's facts; not stored |

Every tool is read-only except `explain_moment`, which is a model call: it counts against
`UsageGuard:NarrativesPerHour` and, when `Mcp:ApiKey` is set, needs that key in the `X-Pitchside-Key` header.
Every call writes an audit record with stage `mcp:{tool}` (`GET /api/v1/audit?matchId=`, or `?scope=server` for calls with no match, such as `list_matches`). Numbers in results come
from code; a narrative's numbers are `[[fact.key]]` tokens that `renderedText` resolves.

## Try it

Start the API and replay the sample match:

```bash
dotnet run --project src/Pitchside.Api --no-launch-profile --urls http://localhost:5180 &
curl -s -X POST localhost:5180/api/v1/replays -H 'content-type: application/json' -d '{"seed":7,"speed":0}'
```

List the tools. The server answers streamable HTTP as a server-sent event (`data: {...}`), so strip the prefix
before pretty-printing:

```bash
curl -s localhost:5180/mcp -H 'content-type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | sed -n 's/^data: //p' | python3 -m json.tool | head -40
```

Call one:

```bash
curl -s localhost:5180/mcp -H 'content-type: application/json' \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_fact","arguments":{"matchId":"MER-7","key":"match.score.away"}}}' \
  | sed -n 's/^data: //p' | python3 -m json.tool
```

## Alongside the Azure MCP server

Pitchside's tools answer questions about a match; the [Azure MCP server](https://learn.microsoft.com/azure/developer/azure-mcp-server/)
answers questions about the Azure resources it runs on (Container Apps, Cosmos DB, Event Hubs, Foundry). A client
configured with both can follow a question from a number on screen to the resource that served it.

### Claude Code

```bash
claude mcp add --transport http pitchside http://localhost:5180/mcp
claude mcp add azure -- npx -y @azure/mcp@latest server start
```

To use the stdio host instead of HTTP:

```bash
claude mcp add pitchside -e PITCHSIDE_MCP_URL=http://localhost:5180/mcp -- dotnet run --project src/Pitchside.Mcp.Stdio
```

Then ask, for example, "Which moments in MER-7 were goals, and what were the events behind the away side's score?"

### VS Code Copilot agent mode

Add `.vscode/mcp.json` to the workspace:

```json
{
  "servers": {
    "pitchside": {
      "type": "http",
      "url": "http://localhost:5180/mcp"
    },
    "azure": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@azure/mcp@latest", "server", "start"]
    }
  }
}
```

Open Copilot Chat in agent mode, choose the tools from both servers, and ask about a match.

To call `explain_moment` on a server that sets `Mcp:ApiKey`, add `"headers": { "X-Pitchside-Key": "${input:pitchsideKey}" }`
to the `pitchside` server and declare `pitchsideKey` as a password input, so the key never lands in the file.

## Adding OAuth later

Who may call what is decided in a request filter in `src/Pitchside.Api/Mcp/McpServerSetup.cs`, not in the tools.
An OAuth resource-server layer (the SDK's authentication support in `ModelContextProtocol.AspNetCore.Authentication`,
and `RequireAuthorization` on the endpoint `MapMcp` returns) can be added without changing `PitchsideTools`.
