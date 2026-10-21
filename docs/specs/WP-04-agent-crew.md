# WP-04: Agent crew on Microsoft Agent Framework and Foundry

| | |
| --- | --- |
| Branch | `feat/wp04-agents` |
| Depends on | WP-02 (contracts), WP-03 (moment sink and store) |
| Owner | Sipho (implemented by Claude Code) |
| Effort | 4 days |
| Changes contracts | adds `schemas/narrative.schema.json` |

## Goal

Turn a moment into a verified, cited narrative. Six roles, each a distinct agent or a code stage, coordinated
by a Microsoft Agent Framework workflow with shared state, typed handoffs, retries and a scripted fallback, so
the broadcast never waits and nothing uncited ever airs. This is the Best Multi-Agent rubric made concrete and
the heart of the Foundry story.

## The crew

| Role | Kind | Input | Output | On failure |
| --- | --- | --- | --- | --- |
| Stats | code (WP-01) | events | fact sheet | n/a |
| Moment detector | code (WP-02) | fact sheet, tags | moment | n/a |
| Explainer | agent | moment, fact sheet snapshot, match document | draft narrative with citations | retry once with the checker's objections appended; then scripted |
| Fact-checker | code first, agent second | draft, fact sheet | verdict with objections | code verdict is final on numbers; the agent only judges tone and claims |
| Persona writer | agent | verified narrative | four persona variants | fall back to the base narrative for that persona |
| Translator | agent | persona variant | per-language text | fall back to English |
| Overlay scheduler | code (WP-05) | verified narratives | overlay entries | n/a |

## Contracts

### Narrative (`schemas/narrative.schema.json`, `src/Pitchside.Contracts/Narrative.cs`)

```json
{
  "id": "MER-7:M:0812:goal:analyst:en",
  "matchId": "MER-7",
  "momentId": "MER-7:M:0812:goal",
  "persona": "analyst",
  "language": "en",
  "factSheetSeq": 812,
  "sentences": [
    { "text": "Amanzi lead [[match.score.home]]-[[match.score.away]] after their third big chance of the half.", "citations": ["match.score.home", "match.score.away", "home.bigChances"] },
    { "text": "Nkosi's xG is now [[player.AMZ-09.xg]] from [[player.AMZ-09.shots]] shots.", "citations": ["player.AMZ-09.xg", "player.AMZ-09.shots"] }
  ],
  "renderedText": "Amanzi lead 2-1 after their third big chance of the half. Nkosi's xG is now 0.91 from 4 shots.",
  "status": "verified",
  "provenance": { "provider": "foundry", "model": "<deployment name>", "promptVersion": "explainer-v3", "attempts": 1, "generatedUtc": "...", "fallback": false },
  "evaluation": { "citationCoverage": 1.0, "factDensity": 2.5, "groundedness": null }
}
```

- Citations are written by the model as `[[fact.key]]` tokens in place of every number. `renderedText` is
  produced by code substituting each token with the fact's value at `precision`. The model never writes a
  digit; if it does, the checker catches it.
- `status` is `draft`, `verified`, `rejected`, `fallback`.
- `evaluation` is filled by code and the evaluation pass (WP-07): `citationCoverage` is written tokens that
  resolve over tokens written (1.0 for every verified narrative), `factDensity` is tokens per sentence, and
  `groundedness` is null until WP-07's evaluation pass scores it.
- The base narrative is persona `base`, language `en`. Persona and language variants reference the same
  `momentId` and carry their own citations, verified the same way.

### Fact-checker verdict

```
Verdict { bool Passed; IReadOnlyList<Objection> Objections }
Objection { string SentenceIndex; string Kind; string Detail }   // Kind: uncited_number, uncited_sentence, unknown_fact, wrong_scope, forbidden_content
```

## Behaviour

- **WP04-R1 Workflow.** One Agent Framework workflow per moment, executed by `MomentWorkflowRunner`, a hosted
  service reading `IMomentSink`. Stages are executors with typed messages; shared state is a
  `MomentContext { Moment, FactSheet, MatchDocument, Drafts[], Verdicts[], Narratives[] }` that every stage
  reads and appends to, never mutates in place. Concurrency: at most `Agents:MaxConcurrentMoments` (default 2)
  workflows per host; the sink is a queue so nothing is lost.
- **WP04-R2 Explainer prompt.** System prompt (file `prompts/explainer.md`, versioned by file name suffix
  `-v{n}`) states: write 1 to 3 sentences explaining why this moment matters; use only the facts provided;
  write every number as a `[[key]]` token, never as digits; name players and clubs from the document only; no
  speculation about injuries, referees' motives or anything not in the facts; output JSON matching the
  narrative sentences shape. User message: the moment, the fact-sheet subset (facts in `evidence.factKeys` plus
  `match.*` plus the side's and involved players' facts) and the document's names. Temperature 0.
- **WP04-R3 Code fact-check.** Deterministic, runs before any model-based check:
  1. Every `[[key]]` in the text must exist in the snapshot (`unknown_fact`).
  2. The text must contain no digit outside a token, except in a player's shirt number when written as
     `#9` and in the words "first half", "second half" written as words (`uncited_number`).
  3. Every player or club name must appear in the match document (`forbidden_content`).
  4. Every cited key must be in scope for the moment: `player.*` keys only for `playerIds` on the moment,
     side keys only for sides in the match (`wrong_scope`).
  5. Forbidden vocabulary list (`prompts/forbidden.sha256`): the wagering vocabulary and real club and
     competition names, committed only as SHA-256 hashes of the lower-cased words so the repository never holds
     the words themselves (CLAUDE.md rules 1 and 2); every word of a sentence is hashed and looked up
     (`forbidden_content`).
  6. Every sentence carries at least one `[[key]]` token, so every sentence cites the facts it was written
     from (`uncited_sentence`). A shirt number such as `#9` is allowed only when a player wearing it is named
     in the same sentence, and a name is checked as a whole phrase, so words from two names do not make a third.
  Passed only when there are zero objections.
- **WP04-R4 Agent fact-check.** With `Agents:Provider=Foundry`, a second pass asks a model whether each
  sentence is supported by the cited facts and the moment, answering JSON `{ supported: bool, reason }` per
  sentence. A `supported: false` is an objection of kind `unsupported_claim`. With the scripted provider this
  stage passes everything.
- **WP04-R5 Retry and fallback.** On objections the explainer is re-run once with the objections appended as
  "Fix these:". A second failure produces a `fallback` narrative from `prompts/templates/{type}.md`, which are
  code templates with `[[key]]` tokens, rendered and verified by the same checker (they must pass by
  construction; a test proves it for every moment type).
- **WP04-R6 Personas and languages** are WP-06; this WP implements the executors and the prompts for persona
  `base` only, with the stage interfaces in place so WP-06 adds prompts and tests without touching the workflow.
- **WP04-R7 Providers.** `IChatProvider` abstraction with `ScriptedChatProvider` (returns canned JSON keyed by
  moment type and prompt name, from `tests/fixtures/scripted/`) and `FoundryChatProvider` built on Microsoft
  Agent Framework's Azure OpenAI integration (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`,
  `Azure.AI.OpenAI`), keyless with `DefaultAzureCredential`. Two deployments: `Foundry:Deployments:Fast` for
  explainer, persona and translator; `Foundry:Deployments:Quality` for the half-time and full-time narratives
  and the agent fact-check.
- **WP04-R8 Tracing.** Each workflow run is one OpenTelemetry activity `pitchside.moment` with child
  activities per stage, tagged with momentId, stage, attempts, provider, model, tokens in and out. These are
  what the Aspire dashboard shows in the video.
- **WP04-R9 Latency budget.** From moment detection to verified base narrative: p50 under 4 s, p95 under
  10 s on the Fast deployment. Measured by the integration test harness against the scripted provider for
  the code path, and recorded by WP-07 against Foundry.
- **WP04-R10 Determinism of ids.** Narrative ids are derivable: `{momentId}:{persona}:{language}`. A variant
  written for one club or one player (WP-06's club fan and player focus) carries that club or player id in the
  optional `subject` field, and its id is `{momentId}:{persona}.{subject}:{language}`.

## Files

- `src/Pitchside.Agents/`: `Workflow/MomentWorkflow.cs`, `Workflow/MomentWorkflowRunner.cs`,
  `Workflow/MomentContext.cs`, `Executors/ExplainerExecutor.cs`, `Executors/FactCheckExecutor.cs`,
  `Executors/PersonaExecutor.cs`, `Executors/TranslatorExecutor.cs`, `Checking/CodeFactChecker.cs`,
  `Checking/Verdict.cs`, `Checking/Objection.cs`, `Rendering/NarrativeRenderer.cs`,
  `Providers/IChatProvider.cs`, `Providers/ScriptedChatProvider.cs`, `Providers/FoundryChatProvider.cs`,
  `Prompts/PromptCatalog.cs` (loads `prompts/*.md` as embedded resources, exposes versions),
  `prompts/explainer-v1.md`, `prompts/factcheck-v1.md`, `prompts/forbidden.sha256`,
  `prompts/templates/*.md` (one per moment type)
- `src/Pitchside.Contracts/Narrative.cs`, `NarrativeSentence.cs`, `NarrativeProvenance.cs`,
  `NarrativeEvaluation.cs`; `schemas/narrative.schema.json`
- Host: `src/Pitchside.Agents.Host/` (worker service with `AddServiceDefaults`, runs `MomentWorkflowRunner`
  against the bus and store, separate container in WP-09). It joins the local Aspire AppHost as a project
  resource once moments reach it over the WP-03 bus; until then a separate process would read an empty
  in-memory sink, so the AppHost leaves it out and the crew runs in the API process.
- `tests/Pitchside.Agents.Tests/`: `CodeFactCheckerTests` (one test per objection kind, plus a corpus of 30
  hand-written sentences with expected verdicts in `fixtures/checker-corpus.json`), `NarrativeRendererTests`,
  `TemplateTests` (every template verifies against the seed-7 moments), `WorkflowTests` with the scripted
  provider (happy path, retry path, fallback path, concurrency cap), `PromptCatalogTests` (versions resolve).

## Configuration

| Key | Default | Notes |
| --- | --- | --- |
| `Agents:Provider` | `Scripted` | `Foundry` |
| `Agents:MaxConcurrentMoments` | `2` | |
| `Foundry:Endpoint` | | the Azure OpenAI or Foundry project endpoint |
| `Foundry:Deployments:Fast` | | deployment name |
| `Foundry:Deployments:Quality` | | deployment name |
| `Foundry:TimeoutSeconds` | `4` | deadline for one fast-tier call; past it the attempt fails and retry or fallback follows |
| `Foundry:QualityTimeoutSeconds` | `12` | deadline for one quality-tier call; the quality model reasons before it answers |
| `Foundry:ReasoningEffort:Fast` | `none` | reasoning effort on the fast tier; the only effort that accepts temperature 0 |
| `Foundry:ReasoningEffort:Quality` | `low` | reasoning effort on the quality tier; a reasoning call rejects a fixed temperature |
| `Agents:AgentFactCheck` | `true` | second-pass model check |

## Done when

```bash
dotnet build Pitchside.slnx
dotnet test Pitchside.slnx
dotnet run --project src/Pitchside.Cli -- narrate --seed 7 --provider scripted | head -20
```

`narrate` runs the full pipeline offline over the sample match and prints each narrative's status; every
moment must end `verified` or `fallback`, never `rejected` without a fallback.

## Notes for the implementer

- Read the Microsoft Agent Framework .NET documentation and samples for the workflow and executor types
  before writing `MomentWorkflow`; the package is `Microsoft.Agents.AI.Workflows`. If the workflow API shape
  differs from the executor-and-edge description here, implement the same stages with the package's own
  constructs and record the mapping in the pull request.
- The explainer must receive the match document's names so it never invents a player. The checker's name
  rule is the backstop, not the plan.
- Keep prompts short and literal. Small, fast models follow "write every number as a [[key]] token" when the
  example in the prompt shows it three times.
- Token budgets: cap output at 400 tokens per call; log tokens per stage.

## Contract change: loop match ids

Added by the WP-11 demo loop. A match id is `MER-<seed>` or, for a later replay of the same seed under a new id,
`MER-<seed>-<loop>` with a loop number of 2 or more (`MER-26`, then `MER-26-2`, `MER-26-3`).
`schemas/narrative.schema.json` (`matchId`, and the narrative `id` and `momentId`, which start with it) accepts both
forms. Every id that was valid stays valid, and `MatchSimulator` still names a simulated match `MER-<seed>`.
