# Architecture

Pitchside turns a live stream of synthetic football events into explainable, personalised, broadcast-ready
match intelligence. The rule that shapes everything: **numbers come from code, words come from AI, and every
word cites the numbers it came from.**

## The five stages

```mermaid
flowchart LR
    SIM[Synthetic engine<br/>seeded, calibrated] -->|events.jsonl, live| ING[Ingest<br/>one actor per match]
    ING --> STATS[Stats layer<br/>pure functions: xG race, momentum,<br/>control vs chaos, pass difficulty, speed tags]
    STATS --> CREW[Agent crew<br/>Microsoft Agent Framework on Foundry]
    CREW --> OVL[Overlay timeline<br/>timed, machine-readable JSON]
    CREW --> PERS[Persona feeds<br/>analyst, casual, club fan, player focus<br/>en, af, zu, es]
    OVL --> UI[Overlay and control room<br/>Angular, SignalR]
    PERS --> UI
    CREW --> EVAL[Foundry evaluations<br/>groundedness per narrative]
```

| Stage | Component | Owner |
| --- | --- | --- |
| Ingest | `Pitchside.Engine` publishes; a match actor (Akka.NET) holds live state and fans out over SignalR and Event Hubs | Sipho |
| Interpret | `Pitchside.Stats`: deterministic metrics, no model calls | Sipho, tests by Pretty |
| Explain | Agent crew: moment detector, explainer, fact-checker | Sipho |
| Render | Overlay timeline JSON with in and out times, slots, layers; Angular overlay | Sipho |
| Personalise | Persona writer and translator agents; viewer preferences | Sipho, personas by Hlumela |

## Agent crew

Built on Microsoft Agent Framework (.NET), hosted through Microsoft Foundry.

| Agent | Role | Input | Output | Failure behaviour |
| --- | --- | --- | --- | --- |
| Stats | Code, not a model. Freezes a fact sheet at each moment | Events | Fact sheet with ids | Cannot fail; pure |
| Moment detector | Decides *that* a moment matters, by rule and threshold, attaching evidence | Fact sheet | Moment with evidence ids | Rules only; no model |
| Explainer | Writes *why* it matters, citing fact-sheet ids | Moment | Draft narrative with citations | Retry once, then scripted template |
| Fact-checker | Verifies every claim against the fact sheet; rejects uncited numbers | Draft | Verified narrative or rejection | Rejection falls back to template |
| Overlay scheduler | Slots verified moments into the timeline without collisions | Verified narratives | Overlay timeline entries | Deterministic |
| Persona writer | Rewrites for analyst, casual, club fan, player focus | Verified narrative | Per-persona text | Falls back to base narrative |
| Translator | Renders each persona text in the viewer's language | Persona text | Localised text | Falls back to English |

Shared state is the match actor's fact sheet. Handoffs are typed messages. The template card airs first, so the
broadcast never waits on a model.

## Enterprise controls

These are the Best Enterprise Solution argument, and each one is visible in the UI or the repo:

- Traceability: every narrative sentence links to the event ids it cites; the control room shows the chain.
- Evaluation: Foundry evaluations score groundedness; the score is displayed, and the README reports the pass
  rate over the sample season.
- Golden masters: the engine's output for pinned seeds is frozen in tests, as in our underwriting engine work.
- Usage guard: caps on concurrent replays, narratives per hour and spoken lines, so the public demo cannot be
  run up by a stranger.
- Secrets: Key Vault references and managed identity only.
- Observability: OpenTelemetry from every service, Aspire dashboard locally, Application Insights in Azure.
- Infrastructure as code: Bicep and azd, reviewed like any other change.

## Azure topology

```mermaid
graph TB
    subgraph ACA["Azure Container Apps environment"]
        WEB["pitchside-web<br/>nginx: Angular overlay and control room<br/>external ingress"]
        API["pitchside-api<br/>match actors, stats, SignalR, MCP server<br/>internal ingress"]
        AGENTS["pitchside-agents<br/>Agent Framework host"]
        SIM["pitchside-sim<br/>scheduled job: replays a seeded match on demand"]
    end
    EH["Azure Event Hubs"]
    COSMOS[("Azure Cosmos DB")]
    FOUNDRY["Microsoft Foundry<br/>models, evaluations, tracing"]
    SPEECH["Azure AI Speech"]
    KV["Key Vault"]
    MON["Application Insights"]

    SIM --> EH --> API
    API --> COSMOS
    API <--> AGENTS
    AGENTS --> FOUNDRY
    AGENTS --> SPEECH
    WEB -- "/api, /hubs" --> API
    API -.-> MON
    AGENTS -.-> MON
    API -.-> KV
    AGENTS -.-> KV
```

## Repository layout

```
src/
  Pitchside.Contracts/      event and match records, the schema's C# twin
  Pitchside.Engine/         synthetic engine: clubs, players, possession model, calibration
  Pitchside.Cli/            simulate, calibrate, replay
  Pitchside.ServiceDefaults/ OpenTelemetry, health, resilience (Aspire)
  Pitchside.Api/            match actors, stats, SignalR hub, MCP server (minimal today)
  Pitchside.AppHost/        Aspire orchestration
tests/
  Pitchside.Engine.Tests/   determinism, schema, calibration gates
docs/                       the documents in this folder
schemas/                    JSON Schema for match.json and events.jsonl
data/calibration/           real-league targets (aggregates only)
data/samples/               a committed seeded match for reviewers and tests
```
