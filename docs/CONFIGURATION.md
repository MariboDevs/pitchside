# Configuration

Every key, its default, and which work package owns it. Values come from `appsettings.json`, user-secrets
locally, and Key Vault references in Azure. No key with a secret value is ever committed.

| Key | Default | Values | Owner |
| --- | --- | --- | --- |
| `Pitchside:Mode` | `Replay` | `Replay`, `Live`; `Live` does not map the replay endpoints | WP-03 |
| `Pitchside:RunAgentsInApi` | `true` | the API hosts the agent crew in process (local mode); `false` when the agent host runs separately | WP-03 |
| `Replay:DefaultSpeed` | `1.0` | multiplier over real time when a request gives none; `0` means as fast as possible | WP-03 |
| `Replay:MaxConcurrent` | `3` | replays at once per host; the next one gets 429 | WP-03 |
| `Storage:Provider` | `InMemory` | `InMemory`, `Cosmos` | WP-03 |
| `Storage:Cosmos:AccountEndpoint` | | Cosmos DB account endpoint; authentication is `DefaultAzureCredential` | WP-03 |
| `Storage:Cosmos:Database` | `pitchside` | database name; containers are created on startup if missing | WP-03 |
| `ConnectionStrings:cosmos` | | set only by the Aspire AppHost at run time for the local Cosmos DB emulator; never in a file | WP-03 |
| `Bus:Provider` | `InProcess` | `InProcess`, `EventHubs` | WP-03 |
| `Bus:EventHubs:Namespace` | | fully qualified namespace; authentication is `DefaultAzureCredential`, a connection string is never read | WP-03 |
| `Bus:EventHubs:HubName` | | event hub name | WP-03 |
| `Bus:EventHubs:CheckpointBlobUri` | | blob container URI for processor checkpoints | WP-03 |
| `Agents:Provider` | `Scripted` | `Scripted`, `Foundry` | WP-04 |
| `Agents:MaxConcurrentMoments` | `2` | moment workflows run at once per host; the rest queue in the moment sink | WP-04 |
| `Agents:AgentFactCheck` | `true` | whether the second, model-based fact-check pass runs after the code check | WP-04 |
| `Foundry:Endpoint` | | the Azure OpenAI or Foundry resource endpoint; calls go to its `/openai/v1/` path, keyless | WP-04 |
| `Foundry:Deployments:Fast` | | deployment name for the explainer, persona writer and translator | WP-04 |
| `Foundry:Deployments:Quality` | | deployment name for half-time and full-time narratives and the agent fact-check | WP-04 |
| `Speech:Provider` | `None` | `None`, `Azure` | WP-10 |
