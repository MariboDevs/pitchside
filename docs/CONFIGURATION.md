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
| `Agents:Personas` | `analyst`, `casual`, `club_fan`, `player_focus` | the viewer personas written for every moment; a configured list replaces the defaults, and an empty value (`Agents__Personas=`) writes the base narrative only | WP-06 |
| `Agents:Languages` | `en`, `af`, `zu`, `es` | the languages every narrative is written in; a configured list replaces the defaults; English is always written, the others are translated | WP-06 |
| `Foundry:Endpoint` | | the Azure OpenAI or Foundry resource endpoint; calls go to its `/openai/v1/` path, keyless | WP-04 |
| `Foundry:Deployments:Fast` | | deployment name for the explainer, persona writer and translator | WP-04 |
| `Foundry:Deployments:Quality` | | deployment name for half-time and full-time narratives and the agent fact-check | WP-04 |
| `Foundry:TimeoutSeconds` | `4` | deadline for one model call on the fast tier; a call past it counts as a failed attempt, so retry and the template fallback still land inside the latency budget | WP-04 |
| `Foundry:QualityTimeoutSeconds` | `12` | deadline for one model call on the quality tier (half-time and full-time narratives, the agent fact-check, the groundedness judge); quality models reason before they answer | WP-04 |
| `Foundry:ReasoningEffort:Fast` | `none` | reasoning effort sent on the fast tier; `none` answers inside the latency budget and is the only effort that accepts temperature 0, which the provider then sends; empty sends nothing | WP-04 |
| `Foundry:ReasoningEffort:Quality` | `low` | reasoning effort sent on the quality tier; GPT-6 quality models accept `low` and above and reject a fixed temperature, so none is sent; empty sends nothing | WP-04 |
| `AZURE_TOKEN_CREDENTIALS` (environment) | | narrows `DefaultAzureCredential` to one part of its chain. Set `dev` in a developer shell so the managed identity probe is skipped (on WSL it hangs long enough to time the call out); in Container Apps, Aspire sets it to `ManagedIdentityCredential`. Tokens are cached across calls | WP-04 |
| `Evaluation:Provider` | `Local` | `Local` (deterministic proxy: the share of sentences citing only the moment's evidence keys), `Foundry` (a judge model scores groundedness) | WP-07 |
| `Evaluation:BatchSeconds` | `30` | verified narratives are scored in batches this many seconds apart | WP-07 |
| `Evaluation:Deployment` | | the Foundry deployment that judges groundedness; empty uses `Foundry:Deployments:Quality` | WP-07 |
| `UsageGuard:Enabled` | | unset means on whenever a paid service is called (`Agents:Provider=Foundry` or `Speech:Provider=Azure`) and off when both are scripted; `true` or `false` overrides | WP-07, WP-10 |
| `UsageGuard:ReplaysPerHourPerIp` | `6` | replays one address may start per clock hour; the next gets 429 with `Retry-After` | WP-07 |
| `UsageGuard:NarrativesPerHour` | `400` | moment workflows (a base narrative and its variants) one host runs per clock hour; later moments are skipped | WP-07 |
| `UsageGuard:SpokenLinesPerHour` | `60` | spoken commentary lines per host per clock hour, enforced by the speech service | WP-07, WP-10 |
| `UsageGuard:ConnectionsPerIp` | `20` | hub connections one address may hold open; the next gets 429 | WP-07 |
| `UsageGuard:TrustForwardedFor` | | unset means on in Container Apps (where `CONTAINER_APP_NAME` is set) and off elsewhere; when on, the caller's address is read from `X-Forwarded-For` as `UsageGuard:ForwardedHops` says | WP-07 |
| `UsageGuard:ForwardedHops` | `1` | how many trusted proxies append to `X-Forwarded-For` before the API; the caller is the entry that many from the end. `1` is the ingress alone; Azure sets `2`, because the web app's nginx forwards the viewer's address alone to the API's internal ingress, which appends nginx's | WP-07 |
| `Pricing:InputPerMillion` | `0` | price of a million input tokens, for the evaluation report's cost estimate | WP-07 |
| `Pricing:OutputPerMillion` | `0` | price of a million output tokens, for the evaluation report's cost estimate | WP-07 |
| `Mcp:ApiKey` | | when set, the MCP tool `explain_moment` needs it in the `X-Pitchside-Key` header; user-secrets locally, a Key Vault reference in Azure, never in a file | WP-08 |
| `Demo:AutoStart` | `false` | when on, the host starts the demo seed once it is up | WP-11 |
| `Demo:Seed` | `26` | the demo seed, chosen by `demo-seed` and recorded in `docs/DEMO.md` | WP-11 |
| `Demo:Speed` | `1.0` | the demo replay's speed; `1` is real time. The overlay scales its card timings (lead, airtime, the 45 s deadline) and its presentation clock by a replay's speed, so a card reads the same on screen at any speed (a replay's time scale is capped at 100, `StoredMatch.MaxTimeScale`). In Azure, `demoSpeed` in `main.demo.bicepparam` sets it (`3`) | WP-11 |
| `Demo:Loop` | `true` | with `Demo:AutoStart`, play the seed again under the next loop id (`MER-26-2`, `MER-26-3`) once a replay ends, however it ended; a loop id already stored (a restart) is skipped. `false` starts the seed once and leaves a stored match as it is | WP-11 |
| `Demo:LoopPauseSeconds` | `120` | how long a finished demo match stays the latest one before the next loop starts | WP-11 |
| `Demo:MaxLoops` | `1000` | the highest loop number tried | WP-11 |
| `Speech:Provider` | `None` | `None` (no recap; the recap endpoints answer 404 "Speech is off"), `Scripted` (one second of silent MP3, no network), `Azure` (Azure AI Speech, keyless) | WP-10 |
| `Speech:Region` | | the Speech resource's region, such as `southafricanorth`; the REST endpoint is `https://{region}.tts.speech.microsoft.com` | WP-10 |
| `Speech:ResourceId` | | the Speech resource's ARM id; the Entra ID token is sent as `aad#{resourceId}#{token}` | WP-10 |
| `Speech:Voices:en`, `:af`, `:zu`, `:es` | `en-ZA-LeahNeural`, `af-ZA-AdriNeural`, `zu-ZA-ThandoNeural`, `es-ES-ElviraNeural` | the neural voice per language; check each is offered in the region; a language without one gets the English audio and the viewer says so | WP-10 |
| `Speech:BlobServiceUri` | | the blob service the recap audio is kept in, such as `https://<account>.blob.core.windows.net`; unset keeps it in process | WP-10 |
| `Speech:BlobContainer` | `recaps` | the container for `{matchId}/{language}.mp3` and its `{matchId}/{language}.json` recap | WP-10 |
