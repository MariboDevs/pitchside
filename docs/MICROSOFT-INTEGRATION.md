# Microsoft and Azure integration matrix

The judges score "how effectively does the Project leverage the hero technologies". This table is the answer,
kept current as the build lands. Status: **planned** until a commit makes it true, then **built**, then
**deployed** when it runs in the hosted demo.

| Technology | Role in Pitchside | Status |
| --- | --- | --- |
| Microsoft Foundry | Hosts the model deployments the agent crew calls, the evaluation runs that score every narrative for groundedness, and the tracing that shows each agent's handoffs | planned |
| Microsoft Agent Framework (.NET) | The agent crew: moment detector, explainer, fact-checker, overlay scheduler, persona writer, translator. Shared match state, typed handoffs, retry and scripted fallback | built (WP-04: one `Microsoft.Agents.AI.Workflows` workflow per moment, `src/Pitchside.Agents`) |
| Azure MCP and an MCP server of our own | Match state, statistics and the overlay timeline exposed as MCP tools so agents, Copilot and the judges' own clients can query a live match | planned |
| GitHub Copilot Agent Mode | Used in development alongside Claude Code; Copilot coding agent and Copilot code review run on pull requests | planned |
| Azure Container Apps | Hosts the API, the agent host and the overlay UI, scale-to-zero outside demo hours | planned |
| Azure Cosmos DB | Match state, event log and narratives, the same choice the Premier League's own platform made | built (WP-03: `CosmosMatchStore`, keyless; the emulator runs from the AppHost) |
| Azure Event Hubs | The live event feed the simulator publishes and the ingest stage consumes | built (WP-03: `EventHubsEventBus`, partitioned by match, blob checkpoints, keyless) |
| Azure AI Speech | Spoken full-time recap in the viewer's language | planned |
| Azure Key Vault and managed identity | No secret in any container's environment; Container Apps secret references | planned |
| Azure Monitor and Application Insights | Operational telemetry, the enterprise readiness story | planned |
| .NET Aspire | Local orchestration of every service, and the dashboard that shows agent traces in the demo video | built (API, agent host and Cosmos DB emulator in `src/Pitchside.AppHost`) |
| Bicep and azd | Infrastructure as code, one command to deploy | built (WP-09: `infra/`, `azure.yaml`, `.github/workflows/deploy.yml`; not yet provisioned) |
| Microsoft Fabric Real-Time Intelligence | Stretch goal: Eventstream and KQL over the live feed for analyst dashboards | stretch |
