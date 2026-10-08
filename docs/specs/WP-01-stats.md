# WP-01: Stats layer and the fact sheet

| | |
| --- | --- |
| Branch | `feat/wp01-stats` |
| Depends on | WP-00 |
| Owner | Sipho (implemented by Claude Code) |
| Effort | 2 days |
| Changes contracts | adds `schemas/fact-sheet.schema.json` |

## Goal

Turn the event stream into every number a viewer will ever see, by code alone, incrementally, with a stable
key per fact so narratives can cite it and the fact-checker can verify it. This is the "Interpret" stage and
the foundation of the "numbers from code" rule.

## Scope

In: the `Pitchside.Stats` library, incremental computation, the fact-sheet contract, a CLI command that
prints the sheet at any sequence number, tests against the sample match.

Out: moment detection (WP-02), persistence (WP-03), anything that calls a model.

## Contracts

### Fact sheet (`schemas/fact-sheet.schema.json`, `src/Pitchside.Contracts/FactSheet.cs`, `Fact.cs`)

```json
{
  "matchId": "MER-7",
  "asOfSeq": 812,
  "period": 2,
  "clockMs": 1_320_000,
  "facts": [
    { "key": "match.score.home", "value": 1, "unit": "goals", "precision": 0, "derivedFrom": ["MER-7-00402"] },
    { "key": "home.xg", "value": 1.37, "unit": "xg", "precision": 2, "derivedFrom": ["MER-7-00120", "..."] },
    { "key": "player.AMZ-09.topSpeedKph", "value": 32.4, "unit": "kph", "precision": 1, "derivedFrom": ["MER-7-00655"] },
    { "key": "match.momentum.home", "value": 0.64, "unit": "share", "precision": 2, "window": { "fromSeq": 700, "toSeq": 812 }, "derivedFrom": [] }
  ]
}
```

- `key` is the citation handle. It is composed of a scope and a metric: `match.*`, `home.*`, `away.*`,
  `player.<playerId>.*`. Scopes `home` and `away` are fixed per match; the UI maps them to club names.
- `value` is a number or a string (categorical facts such as `match.state`).
- `precision` is the number of decimals the fact is displayed with. The fact-checker compares a cited number
  to the value rounded to this precision.
- `derivedFrom` lists the event ids that produced the value, capped at 50 most recent, so the trace never
  grows without bound.
- `window` is present only for rolling metrics.

### Metric catalogue

Every key below must exist in a full-time fact sheet of the sample match. Values are per side unless noted.

| Key pattern | Unit | Definition |
| --- | --- | --- |
| `match.clock.minute` | min | floor of match minute, 1 to 90+, second half starts at 46 |
| `match.period` | | 1 or 2 |
| `match.state` | | `pre_match`, `first_half`, `half_time`, `second_half`, `full_time` |
| `match.score.home`, `match.score.away` | goals | |
| `match.possessions` | count | possessions so far |
| `match.rhythm` | per min | possessions per minute over the last 10 minutes |
| `match.control` | share | share of the last 10 minutes' events that are completed passes or carries, over all open-play events; near 1 is control, near 0 is chaos |
| `match.momentum.home`, `match.momentum.away` | share | each side's share of a weighted sum over the last 5 minutes: shots 3, shots on target 4, final-third entries 1, corners 2, xG 10; sums to 1, both 0.5 when nothing happened |
| `{side}.goals`, `{side}.xg`, `{side}.shots`, `{side}.shotsOnTarget`, `{side}.bigChances` | | big chance is xG >= 0.30 |
| `{side}.passes`, `{side}.passesCompleted`, `{side}.passCompletion` | share | |
| `{side}.passDifficultyAvg` | 0 to 1 | mean difficulty of attempted passes |
| `{side}.progressivePasses` | count | completed passes gaining >= 10 m towards goal |
| `{side}.finalThirdEntries` | count | completed passes or carries ending with x > 70 from x <= 70 |
| `{side}.possessionShare` | share | share of possession time, by possession duration |
| `{side}.corners`, `{side}.fouls`, `{side}.yellows`, `{side}.reds` | count | |
| `{side}.pressures`, `{side}.pressingIntensity` | | pressures per opponent pass, last 10 minutes |
| `{side}.distanceKm` | km | sum of tracking deltas plus carries, all players |
| `{side}.topSpeedKph` | kph | max over players |
| `player.<id>.passes`, `.passesCompleted`, `.passCompletion`, `.progressivePasses`, `.carries`, `.distanceKm`, `.topSpeedKph`, `.shots`, `.xg`, `.goals`, `.assists`, `.pressures`, `.fouls`, `.cards`, `.minutes` | | per player, only for players who have appeared |
| `player.<id>.sprints` | count | carries with `speedKph >= 25` plus tracking intervals with `topSpeedKph >= 30` |

### Speed and distance tags

The rules ask for "speed and distance indicators triggered when predefined thresholds are reached". The stats
layer raises a `Tag` alongside the facts whenever a threshold is first crossed:

| Tag | Trigger |
| --- | --- |
| `speed.30` | a player's `topSpeedKph` first reaches 30 |
| `speed.33` | first reaches 33 |
| `distance.5` | a player's `distanceKm` first reaches 5.0 |
| `distance.10` | first reaches 10.0 |
| `shot.fast` | a shot with `shotSpeedKph >= 100` |
| `pass.hard` | a completed pass with `difficulty >= 0.6` |

Tags are `{ "tag": "speed.30", "playerId": "AMZ-09", "eventId": "MER-7-00655", "value": 30.2 }` and are
returned by the computation step; WP-02 turns them into moments. They are also facts: `player.<id>.tags`
holds the list of tag names raised so far.

## Design

```
Pitchside.Stats
  IMatchStatsEngine
    MatchStatsState Start(MatchDocument document)
    StepResult Apply(MatchStatsState state, MatchEvent e)      // pure: returns a new state and the tags raised
    FactSheet Snapshot(MatchStatsState state)                  // materialises the facts
  MatchStatsState           immutable record of accumulators; Apply returns a new instance
  RollingWindow<T>          events in the last N ms of period clock, by seq
  FactSheetBuilder          turns accumulators into facts with keys, precision, derivedFrom
```

- `Apply` is pure and O(1) amortised per event apart from window eviction. The whole sample match (about
  1,500 events) must snapshot at every event in under 2 seconds on CI hardware.
- `Snapshot` after the last event equals a sheet computed by a separate batch implementation
  `BatchStats.Compute(events)` written independently in the test project. This is the correctness oracle.
- Rolling windows use period clock, not wall time, and do not cross the half-time boundary.

## Files

- `src/Pitchside.Stats/Pitchside.Stats.csproj` (references Contracts only)
- `src/Pitchside.Stats/IMatchStatsEngine.cs`, `MatchStatsEngine.cs`, `MatchStatsState.cs`,
  `StepResult.cs`, `Tag.cs`, `RollingWindow.cs`, `FactSheetBuilder.cs`, `FactKeys.cs` (constants for every
  key pattern; no string literals elsewhere), `SideAccumulator.cs`, `PlayerAccumulator.cs`
- `src/Pitchside.Contracts/FactSheet.cs`, `Fact.cs`, `FactWindow.cs`
- `schemas/fact-sheet.schema.json`
- CLI: `dotnet run --project src/Pitchside.Cli -- facts --seed 7 [--at-seq N]` prints the sheet as JSON
- `tests/Pitchside.Stats.Tests/`: `BatchStats.cs` (the oracle), `IncrementalEqualsBatchTests`,
  `MetricDefinitionTests` (hand-computed expectations on a tiny hand-written event list of 20 events),
  `TagTests`, `FactSheetSchemaTests` (snapshot validates against the schema), `PerformanceTests`
  (full sample under 2 s)
- Add `Pitchside.Stats` and its tests to `Pitchside.slnx`; add `src/Pitchside.Stats` as a reference of
  `Pitchside.Api`.

## Done when

```bash
dotnet build Pitchside.slnx
dotnet test Pitchside.slnx
dotnet run --project src/Pitchside.Cli -- facts --seed 7 | python3 -c "import json,sys; d=json.load(sys.stdin); print(len(d['facts']), 'facts')"
```

The last command prints at least 120 facts. `docs/EVENT-SCHEMA.md` gains a "Fact sheet" section listing the
catalogue above.

## Notes for the implementer

- `FactKeys` must be the only place a key string is built. The fact-checker (WP-04) and the UI (WP-05) import
  the same constants.
- Momentum and control are the explainability metrics the rules call out ("control vs. chaos, tactical
  pressure changes, and game rhythm"). Keep their definitions exactly as the table says; the narrative prompts
  describe them in those words.
- Floating point: round at `Snapshot`, never inside accumulators.
