# Pitchside specifications

This folder is the build contract. Each work package (WP) is a self-contained document that one implementer,
human or model, can take from start to finish without reading the others, except for the contracts it names.
The index below is the order of dependency. Nothing in a WP may change a contract owned by another WP; a needed
change is raised as a note in the pull request and the owning spec is updated first.

## Who implements

Every package is implemented by Claude Code (Opus) in a local session on Sipho's machine, one package per
session, with commits authored by Sipho Maribo. The specification is the brief; the session reads this folder,
not the conversation that produced it. Decisions already made and not up for re-litigation: .NET 10 and Aspire,
Akka.NET actor per match, Angular with the Maribo design language, Cosmos DB, Event Hubs, Microsoft Agent
Framework on Foundry, Fabric in scope, no shortcuts on any of them.

## How to implement a work package

1. Read [`CLAUDE.md`](../../CLAUDE.md) at the repository root, then the WP document, then only the sections of
   other documents it links to.
2. Create a branch named in the WP's header. One WP, one branch, one pull request into `main`.
3. Build in the order the WP's "Files" section lists. Write the tests the WP names before the code they cover.
4. Run the WP's "Done when" block. Every command must pass on a clean checkout. Paste the output in the pull
   request description.
5. Update the row for the WP in [`IMPLEMENTATION-GAP.md`](IMPLEMENTATION-GAP.md) with the evidence (test
   names, commands, screenshots where the WP asks for them). A row is not closed by intention; it is closed by
   evidence.
6. If a package API differs from what the spec describes, follow the package. The spec names the behaviour
   and the acceptance test; the package's own documentation decides the call. Say in the pull request what
   differed.

## Rules that apply to every work package

- Every rule in `CLAUDE.md` applies: fictional only, no betting, no secrets, numbers from code, deterministic
  engine, warnings as errors, one type per file, no em dashes.
- **Providers have a local mode.** Every external dependency (model, storage, bus, speech) sits behind an
  interface with an in-process implementation selected by configuration. `dotnet test` never needs the
  network. CI runs with every provider in local mode.
- **Contracts are files.** A JSON shape named in a spec exists as a JSON Schema under `schemas/` and as a C#
  record under `src/Pitchside.Contracts`, with a test that serialises the record and validates it against the
  schema.
- **Identifiers are stable and derivable.** A consumer must be able to compute an id from the inputs that
  produced it, so a narrative can cite a fact and a test can predict a moment id.
- **Logging is structured** and never includes a secret or a prompt's full text at Information level.
- **Configuration keys** are listed in the WP and added to `docs/CONFIGURATION.md` in the same pull request.

## Index

| WP | Title | Depends on | Owner | Branch |
| --- | --- | --- | --- | --- |
| [WP-00](WP-00-engine-refinements.md) | Engine refinements: pass completion, speed, distance, penalties | scaffold | Sipho | `feat/wp00-engine` |
| [WP-01](WP-01-stats.md) | Stats layer and the fact sheet | WP-00 | Sipho | `feat/wp01-stats` |
| [WP-02](WP-02-moments.md) | Moment detection | WP-01 | Sipho | `feat/wp02-moments` |
| [WP-03](WP-03-live-runtime.md) | Live runtime: replay, match actor, SignalR, storage, bus | WP-02 | Sipho | `feat/wp03-runtime` |
| [WP-04](WP-04-agent-crew.md) | Agent crew on Microsoft Agent Framework and Foundry | WP-02 | Sipho | `feat/wp04-agents` |
| [WP-05](WP-05-overlay-and-ui.md) | Overlay timeline, overlay, control room, viewer | WP-03 | Sipho | `feat/wp05-ui` |
| [WP-06](WP-06-personalisation.md) | Personas, languages, viewer preferences | WP-04, WP-05 | Sipho, Hlumela | `feat/wp06-personas` |
| [WP-07](WP-07-evaluation-and-controls.md) | Evaluation, traceability, usage guard, audit | WP-04 | Pretty | `feat/wp07-evaluation` |
| [WP-08](WP-08-mcp-server.md) | MCP server | WP-03 | Sipho | `feat/wp08-mcp` |
| [WP-09](WP-09-infrastructure.md) | Azure infrastructure, deployment, operations | WP-03 | Sipho | `feat/wp09-infra` |
| [WP-10](WP-10-speech-recap.md) | Spoken full-time recap (Azure AI Speech) | WP-04 | Sipho | `feat/wp10-speech` |
| [WP-11](WP-11-demo-and-submission.md) | Demo match, video, README, submission | all | Hlumela, Pretty | `docs/wp11-submission` |
| [WP-12](WP-12-fabric.md) | Microsoft Fabric Real-Time Intelligence | WP-03, WP-09 | Sipho | `feat/wp12-fabric` |

WP-00 to WP-03 are the critical path. WP-09 is executed by Claude Code running on Sipho's machine, signed in
to Azure; it is the only package that touches real cloud resources, and it says where a portal click is needed. WP-04 can start as soon as WP-02's contracts exist, using the scripted
provider. WP-05 can start against WP-03's replay mode with scripted narratives. WP-09 can start on day one with
the API alone and grow as services land.

## Vocabulary

| Term | Meaning |
| --- | --- |
| Event | One line of `events.jsonl`, the contract in `docs/EVENT-SCHEMA.md` |
| Fact | A named, numeric or categorical statement about the match computed by code from events, with a stable key |
| Fact sheet | The set of facts true as of a given event sequence number |
| Moment | A span of events that a rule decided matters, with its evidence (event ids and fact keys) |
| Narrative | Text written about a moment, for one persona in one language, every number cited to a fact key |
| Overlay entry | A narrative placed on the broadcast timeline with in and out times, a layer and a slot |
| Persona | One of analyst, casual, club_fan, player_focus |
| Provider | A pluggable implementation of an external dependency, always with a local mode |
