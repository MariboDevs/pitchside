# WP-07: Evaluation, traceability, usage guard, audit

| | |
| --- | --- |
| Branch | `feat/wp07-evaluation` |
| Depends on | WP-04 |
| Owner | Pretty (implemented by Claude Code under Pretty's direction) |
| Effort | 3 days |
| Changes contracts | adds `schemas/audit-record.schema.json` |

## Goal

Produce the evidence that makes Pitchside an enterprise solution rather than a demo: a groundedness score on
every narrative, a trace from every sentence to the events behind it, hard caps that keep a public demo safe,
and an audit record of every model call. The README's headline numbers come from this WP.

## Behaviour

- **WP07-R1 Citation coverage (code).** For every narrative: `citationCoverage` = cited tokens that resolved
  over tokens written, always 1.0 for a verified narrative by construction, and `factDensity` = tokens per
  sentence. Stored on `Narrative.evaluation`.
- **WP07-R2 Groundedness (Foundry evaluations).** With `Evaluation:Provider=Foundry`, each verified narrative is
  submitted to a Foundry evaluation run using the groundedness evaluator, with the fact subset as context and
  the rendered text as the response. The score (0 to 1 after normalisation) is stored on
  `Narrative.evaluation.groundedness` and shown in the control room. Runs are batched per match every 30 s to
  keep cost down. With `Evaluation:Provider=Local`, a deterministic proxy is used: the share of sentences whose
  every cited fact is in the moment's evidence (so CI has a number).
- **WP07-R3 Evaluation report.** `dotnet run --project src/Pitchside.Cli -- evaluate --match MER-7` writes
  `docs/evaluation/report-{matchId}.md`: narratives by status, mean and minimum groundedness, mean citation
  coverage, fallback rate, p50 and p95 latency per stage, tokens per narrative, cost estimate from configured
  per-token prices. The README quotes this report for the demo match.
- **WP07-R4 Traceability chain.** `GET /api/v1/matches/{id}/narratives/{nid}/trace` returns, for every
  sentence, the citations, each fact's value and `derivedFrom` event ids, and the events themselves. The
  control room renders it (WP-05); this WP adds the endpoint and a test that every verified narrative's trace
  resolves fully.
- **WP07-R5 Usage guard.** Fixed-window limits, configurable, enforced in middleware and in the agent host:
  replays per hour per IP (default 6), concurrent replays per host (3, from WP-03), narratives per hour per
  host (default 400), spoken lines per hour (default 60, WP-10), hub connections per IP (default 20). Exceeding
  returns 429 with a problem details body and a `Retry-After`. The control room shows current counts against
  limits. Limits never apply to the scripted provider in tests.
- **WP07-R6 Audit record.** Every model call writes an `AuditRecord { id (ulid), matchId, momentId, stage,
  provider, model, promptVersion, promptHash, inputTokens, outputTokens, latencyMs, outcome, requestId,
  utc }` to the `audit` container. Prompt text is not stored; its hash is. `GET /api/v1/audit?matchId=` pages
  them; the control room's trace swimlane reads them.
- **WP07-R7 Golden masters for narratives.** With the scripted provider, the full seed-7 narrative set is
  committed under `tests/Pitchside.Agents.Tests/golden/seed-7.narratives.jsonl` and compared exactly. A
  prompt or checker change that alters them updates the golden in the same pull request with a reason.
- **WP07-R8 Security baseline.** `dotnet list package --vulnerable --include-transitive` clean; `npm audit
  --audit-level=high` clean; CodeQL workflow enabled on the repository; secrets scanning on. All four run in
  CI.

## Files

- `src/Pitchside.Evaluation/`: `IEvaluationProvider.cs`, `LocalEvaluationProvider.cs`,
  `FoundryEvaluationProvider.cs` (`Azure.AI.Projects` evaluations; verify the current API and the
  groundedness evaluator name before coding), `EvaluationReport.cs`
- `src/Pitchside.Runtime/Audit/`, `src/Pitchside.Contracts/AuditRecord.cs`, `schemas/audit-record.schema.json`
- `src/Pitchside.Api/Middleware/UsageGuardMiddleware.cs`, `Endpoints/AuditEndpoints.cs`,
  `Endpoints/TraceEndpoints.cs`
- `.github/workflows/codeql.yml`, additions to `ci.yml`
- `tests/Pitchside.Evaluation.Tests/`, `tests/Pitchside.Api.IntegrationTests/UsageGuardTests.cs`,
  `TraceTests.cs`, `AuditTests.cs`

## Configuration

| Key | Default |
| --- | --- |
| `Evaluation:Provider` | `Local` |
| `Evaluation:BatchSeconds` | `30` |
| `UsageGuard:ReplaysPerHourPerIp` | `6` |
| `UsageGuard:NarrativesPerHour` | `400` |
| `UsageGuard:SpokenLinesPerHour` | `60` |
| `UsageGuard:ConnectionsPerIp` | `20` |
| `Pricing:InputPerMillion`, `Pricing:OutputPerMillion` | `0` until set |

## Done when

```bash
dotnet test Pitchside.slnx
dotnet run --project src/Pitchside.Cli -- evaluate --match MER-7 --provider local
dotnet list package --vulnerable --include-transitive
cd frontend && npm audit --audit-level=high
```

The evaluation report exists and the README's "Evidence" section (WP-11) can quote it.
