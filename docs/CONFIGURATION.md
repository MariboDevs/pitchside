# Configuration

Every key, its default, and which work package owns it. Values come from `appsettings.json`, user-secrets
locally, and Key Vault references in Azure. No key with a secret value is ever committed.

| Key | Default | Values | Owner |
| --- | --- | --- | --- |
| `Pitchside:Mode` | `Replay` | `Replay`, `Live` | WP-03 |
| `Replay:Speed` | `1.0` | multiplier over real time; `0` means as fast as possible | WP-03 |
| `Storage:Provider` | `InMemory` | `InMemory`, `Cosmos` | WP-03 |
| `Bus:Provider` | `InProcess` | `InProcess`, `EventHubs` | WP-03 |
| `Agents:Provider` | `Scripted` | `Scripted`, `Foundry` | WP-04 |
| `Agents:MaxConcurrentMoments` | `2` | moment workflows run at once per host; the rest queue in the moment sink | WP-04 |
| `Agents:AgentFactCheck` | `true` | whether the second, model-based fact-check pass runs after the code check | WP-04 |
| `Foundry:Endpoint` | | the Azure OpenAI or Foundry resource endpoint; calls go to its `/openai/v1/` path, keyless | WP-04 |
| `Foundry:Deployments:Fast` | | deployment name for the explainer, persona writer and translator | WP-04 |
| `Foundry:Deployments:Quality` | | deployment name for half-time and full-time narratives and the agent fact-check | WP-04 |
| `Speech:Provider` | `None` | `None`, `Azure` | WP-10 |
