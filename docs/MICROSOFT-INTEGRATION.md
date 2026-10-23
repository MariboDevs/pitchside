# Microsoft and Azure integration matrix

The judges score "how effectively does the Project leverage the hero technologies". This table is the answer,
kept current as the build lands. Status: **planned** until a commit makes it true, then **built**, then
**deployed** when it runs in the hosted demo.

| Technology | Role in Pitchside | Status |
| --- | --- | --- |
| Microsoft Foundry | Hosts the model deployments the agent crew calls, the evaluation runs that score every narrative for groundedness, and the tracing that shows each agent's handoffs | deployed (WP-04, WP-07: `pitchside-fast` writes and `pitchside-quality` checks and scores, called keyless through the apps' managed identity; groundedness from Foundry evaluation in `docs/evaluation/report-MER-7.md`) |
| Microsoft Agent Framework (.NET) | The agent crew: moment detector, explainer, fact-checker, overlay scheduler, persona writer, translator. Shared match state, typed handoffs, retry and scripted fallback | built (WP-04: one `Microsoft.Agents.AI.Workflows` workflow per moment, `src/Pitchside.Agents`) |
| Azure MCP and an MCP server of our own | Match state, statistics and the overlay timeline exposed as MCP tools so agents, Copilot and the judges' own clients can query a live match | deployed (WP-08: `/mcp` on the demo web app lists nine tools, among them `list_matches`, `get_fact_sheet` and `explain_moment`) |
| GitHub Copilot Agent Mode | Used in development alongside Claude Code; Copilot coding agent and Copilot code review run on pull requests | in use (Copilot code review on every pull request; its findings are fixed or answered before merge) |
| Azure Container Apps | Hosts the API, the agent host and the overlay UI, scale-to-zero outside demo hours | deployed (WP-09: dev and demo, the API on internal ingress behind the web app's nginx, one warm web replica in demo) |
| Azure Cosmos DB | Match state, event log and narratives, the same choice the Premier League's own platform made | deployed (WP-03, WP-09: `CosmosMatchStore`, keyless; the emulator runs from the AppHost) |
| Azure Event Hubs | The live event feed the simulator publishes and the ingest stage consumes | deployed (WP-03, WP-09: `EventHubsEventBus`, partitioned by match, blob checkpoints, keyless, local auth off; Standard in dev for the Fabric consumer group, Basic in demo) |
| Azure AI Speech | Spoken full-time recap in the viewer's language | built (WP-10: SSML by code, a neural voice per language over the keyless REST endpoint, audio in Blob Storage; deployed, and on 23 October real synthesis in all four voices in South Africa North) |
| Azure Key Vault and managed identity | No secret in any container's environment; Container Apps secret references | deployed (WP-09: every service keyless through a user-assigned managed identity; Key Vault holds the MCP key when it is turned on) |
| Azure Monitor and Application Insights | Operational telemetry, the enterprise readiness story | deployed (WP-09: the managed OpenTelemetry agent sends the apps' requests, dependencies and traces to Application Insights; an availability test on `/health` from five locations; alerts on the narrative fallback rate) |
| .NET Aspire | Local orchestration of every service, and the dashboard that shows agent traces in the demo video | built (API, agent host and Cosmos DB emulator in `src/Pitchside.AppHost`) |
| Bicep and azd | Infrastructure as code, one command to deploy | deployed (WP-09: `infra/`, `azure.yaml`, `.github/workflows/deploy.yml`; dev and demo provisioned with `azd`, deployed by the workflow over OIDC) |
| Microsoft Fabric Real-Time Intelligence | Stretch goal: Eventstream and KQL over the live feed for analyst dashboards | deployed on dev (WP-12: Eventstream from Event Hubs with workspace identity, Eventhouse and KQL database, the Pitchside Live dashboard, the pressure alert in Activator; evidence in `docs/FABRIC.md`) |
