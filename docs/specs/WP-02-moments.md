# WP-02: Moment detection

| | |
| --- | --- |
| Branch | `feat/wp02-moments` |
| Depends on | WP-01 |
| Owner | Sipho (implemented by Claude Code) |
| Effort | 1.5 days |
| Changes contracts | adds `schemas/moment.schema.json` |

## Goal

Decide, by rule, *that* a moment matters and attach the evidence, so the agent crew only ever explains
something the code already decided is worth explaining. No model is called here.

## Contracts

### Moment (`schemas/moment.schema.json`, `src/Pitchside.Contracts/Moment.cs`)

```json
{
  "id": "MER-7:M:0812:goal",
  "matchId": "MER-7",
  "type": "goal",
  "priority": 100,
  "period": 2,
  "clockMs": 1320000,
  "triggerSeq": 812,
  "window": { "fromSeq": 790, "toSeq": 812 },
  "sideId": "AMZ",
  "playerIds": ["AMZ-09", "AMZ-07"],
  "evidence": {
    "eventIds": ["MER-7-00790", "MER-7-00811", "MER-7-00812"],
    "factKeys": ["match.score.home", "match.score.away", "home.xg", "player.AMZ-09.goals", "match.momentum.home"]
  },
  "headline": "Goal: Amanzi United 2-1",
  "factSheetSeq": 812
}
```

`id` is `{matchId}:M:{triggerSeq:0000}:{type}` so it is derivable from the trigger. `headline` is a code
template, not model output. `factSheetSeq` is the sequence number the agent crew must snapshot the fact sheet
at, so every citation resolves to the same values the detector saw.

### Moment types, in priority order

| Type | Priority | Rule | Window | Evidence facts |
| --- | --- | --- | --- | --- |
| `goal` | 100 | a `goal` event | the possession that scored, from its first event | score both, scorer goals, assister assists, side xg, momentum both |
| `red_card` | 95 | a `card` with colour red | the foul and card | side reds, side fouls, player cards |
| `penalty` | 90 | a `shot` with `penalty: true` | foul to shot | side xg, shooter xg, score |
| `big_chance_missed` | 70 | a non-goal `shot` with `xg >= 0.30` | the possession | shooter xg, side xg, side bigChances, score |
| `momentum_swing` | 60 | `match.momentum.{side}` crosses 0.70 upward, at most once per 5 minutes per side | last 5 minutes | momentum both, side shots, side finalThirdEntries, pressingIntensity |
| `sustained_pressure` | 50 | a side has 3 or more shots or 2 or more corners in the last 3 minutes with no goal; at most once per 5 minutes per side | last 3 minutes | side shots, corners, finalThirdEntries, possessionShare |
| `milestone` | 45 | a player's 2nd goal (brace) or 3rd goal (hat-trick); a side's clean sheet still intact at minute 80 | the triggering event | player goals, score, minute |
| `speed_tag` | 40 | a `speed.33` or `distance.10` tag from WP-01 | the triggering event | player topSpeedKph or distanceKm, player sprints |
| `pass_tag` | 30 | a `pass.hard` tag that leads to a shot within the same possession | the pass to the shot | pass difficulty (as the event), side progressivePasses |
| `kick_off` | 20 | the `kick_off` of period 1 | the event | both line-ups via the document, not facts |
| `half_time` | 80 | `half_end` of period 1 | whole first half | score, xg both, shots both, possessionShare both, momentum both |
| `full_time` | 100 | the `full_time` event | whole match | score, xg both, shots both, possessionShare both, passCompletion both, top player facts |

### Rate limits

A side produces at most one `momentum_swing` and one `sustained_pressure` per 5 minutes of period clock.
Across all types other than `goal`, `red_card`, `penalty`, `half_time` and `full_time`, the detector emits at
most one moment per 90 seconds per match; a lower-priority moment inside that gap is dropped and logged at
Debug. This keeps the overlay (WP-05) from flooding.

## Design

```
Pitchside.Moments
  IMomentDetector
    DetectorState Start(MatchDocument document)
    (DetectorState next, IReadOnlyList<Moment> moments) Apply(DetectorState state, MatchEvent e, FactSheet sheet, IReadOnlyList<Tag> tags)
  rules/ one class per type implementing IMomentRule { string Type; int Priority; Moment? Evaluate(...) }
  MomentRateLimiter
```

Pure, deterministic, same pattern as WP-01.

## Files

- `src/Pitchside.Moments/` as above, `MomentTypes.cs` constants, `MomentHeadlines.cs` (templates)
- `src/Pitchside.Contracts/Moment.cs`, `MomentEvidence.cs`, `MomentWindow.cs`
- `schemas/moment.schema.json`
- CLI: `dotnet run --project src/Pitchside.Cli -- moments --seed 7` prints every moment as JSON lines
- `tests/Pitchside.Moments.Tests/`: one test class per rule on hand-written event lists; `RateLimitTests`;
  `SampleMatchMomentsTests` (seed 7 produces a stable, committed list in
  `tests/Pitchside.Moments.Tests/golden/seed-7.moments.jsonl`, compared exactly); `SchemaTests`.

## Done when

```bash
dotnet build Pitchside.slnx
dotnet test Pitchside.slnx
dotnet run --project src/Pitchside.Cli -- moments --seed 7 | wc -l   # between 12 and 40 moments
```

## Notes for the implementer

- Every goal in the sample must produce exactly one `goal` moment; the test asserts the count equals the number
  of `goal` events.
- `headline` is for the control room list and the scripted fallback; it never reaches a viewer without the
  explainer unless the scripted provider is in use.
- When a goal arrives inside a `sustained_pressure` window, the pressure moment is still valid history; do not
  retract it.

## Contract change: loop match ids

Added by the WP-11 demo loop. A match id is `MER-<seed>` or, for a later replay of the same seed under a new id,
`MER-<seed>-<loop>` with a loop number of 2 or more (`MER-26`, then `MER-26-2`, `MER-26-3`).
`schemas/moment.schema.json` (`matchId`, and the moment `id`, which starts with it) accepts both forms. Every id that
was valid stays valid, and `MatchSimulator` still names a simulated match `MER-<seed>`.
