# WP-00: Engine refinements

| | |
| --- | --- |
| Branch | `feat/wp00-engine` |
| Depends on | scaffold on `main` |
| Owner | Sipho (implemented by Claude Code) |
| Effort | 1 day |
| Changes contracts | yes: `schemaVersion` 0.1 to 0.2 |

## Goal

Close the gaps the first calibrated build left open so that every metric the rules name can be computed from
the feed: pass completion, player speed and distance, penalties. Keep every existing calibration statistic
within tolerance and keep the engine deterministic.

## Scope

In: incomplete passes, tracking ticks with per-player distance and top speed, penalties, a pass-completion
calibration target, schema bump, sample regeneration, golden-master update.

Out: injuries, own goals, fatigue curves, formations other than 4-3-3, weather.

## Contracts

### Schema 0.2 additions to `MatchEvent` (`schemas/event.schema.json`, `src/Pitchside.Contracts/MatchEvent.cs`)

| Field | Type | On event types | Meaning |
| --- | --- | --- | --- |
| `completed` | boolean | `pass` | already exists; may now be `false` |
| `penalty` | boolean | `shot` | the shot is a penalty kick |
| `tracking` | array of `{ playerId, distanceM, topSpeedKph }` | `tracking` (new type) | per-player movement over the last tracking interval |

New `EventType.Tracking`. A `tracking` event is emitted by each side every 60,000 ms of period clock, at the
possession boundary after the minute rolls over, with `playerId` null and `location` at the centre spot. Its
`tracking` array holds one entry per player on the pitch for that side.

`SchemaVersion.Current` becomes `"0.2"`. `schemas/match.schema.json` `schemaVersion` const becomes `"0.2"`.

### `Player` gains `workRate` (double, metres per minute on the ball-less run, 95 to 125), generated per
player from the squad seed. Add to `schemas/match.schema.json`.

## Behaviour

- **WP00-R1 Incomplete passes.** Each pass completes with probability `1 - PassModel.Difficulty * k` where
  `k` is a new `SimulationOptions.PassFailureScale` (default to be tuned so that league pass completion
  lands at 0.82 ± 0.02). An incomplete pass emits the `pass` event with `completed: false`, then a `turnover`
  with reason `interception`, and ends the possession with the opponent in possession at the mirrored end
  location. Possessions planned to end in a shot may be cut short this way; retune `HomeShotRate` and
  `AwayShotRate` so shots stay within tolerance.
- **WP00-R2 Pass completion target.** Add `pass_completion` to `data/calibration/targets.json` under every
  target set with value `0.82` and a `_note` field: "literature value for top-flight pass completion;
  football-data.co.uk does not carry passes". Add it to `AggregateStats` (completed passes over passes) and
  to `CalibrationReport` with tolerance `0.02`.
- **WP00-R3 Penalties.** A foul in the final third (`location.X > 88`) by the defending side awards a penalty
  with probability 0.22. A penalty is a possession of one event: a `shot` at `(94, 34)` with `penalty: true`,
  `xg: 0.76`, `bodyPart` right or left foot, taken by the side's highest-shirt-numbered forward on the pitch.
  Add `penalties_per_match` to the targets (`0.25`, literature value, note as in R2) with tolerance `0.08`,
  and to the report.
- **WP00-R4 Tracking ticks.** Every player on the pitch accumulates `workRate * minutes` metres plus carry
  lengths. Top speed in the interval is the max of carry speeds in the interval or a sampled off-ball value
  `Gaussian(21, 3)` kph clamped to [12, 34]; forwards and defenders draw from `Gaussian(23, 3)`. The tick
  carries the interval's delta, not the cumulative. The stats layer (WP-01) sums.
- **WP00-R5 Determinism.** Seeds 1, 7 and 20261027 still produce identical output on two runs. The committed
  sample `data/samples/seed-7` is regenerated and the CI diff passes.
- **WP00-R6 Calibration.** `dotnet run --project src/Pitchside.Cli -- calibrate --matches 5000` reports all
  statistics, now 26, within tolerance. Commit the regenerated `docs/calibration/report.md`.

## Files

- `src/Pitchside.Contracts/EventType.cs`, `MatchEvent.cs`, `Player.cs`, `SchemaVersion.cs`,
  new `TrackingEntry.cs`
- `schemas/event.schema.json`, `schemas/match.schema.json`
- `src/Pitchside.Engine/MatchSimulator.cs`, `SimulationOptions.cs`, `SquadFactory.cs`, `SideState.cs`,
  new `TrackingModel.cs`
- `src/Pitchside.Engine/Calibration/AggregateStats.cs`, `CalibrationReport.cs`
- `data/calibration/targets.json`, `data/samples/seed-7/*`, `docs/calibration/report.md`
- `docs/EVENT-SCHEMA.md` (document the new fields and the tracking event)
- `tests/Pitchside.Engine.Tests/*`: extend `ConsistencyTests` (an incomplete pass is always followed by a
  turnover by the same side; a penalty shot has `penalty: true` and xG 0.76; tracking ticks are 60 s apart
  per side and list exactly the players on the pitch), new `SchemaValidationTests` that validate the sample
  match against both JSON Schemas using a .NET JSON Schema validator (`JsonSchema.Net`, add to
  `Directory.Packages.props`).

## Done when

```bash
dotnet build Pitchside.slnx
dotnet test Pitchside.slnx
dotnet run --project src/Pitchside.Cli -- calibrate --matches 5000 --report docs/calibration/report.md
dotnet run --project src/Pitchside.Cli -- simulate --seed 7 --out data/samples/seed-7
git diff --stat data/samples/seed-7   # shows the regenerated sample; commit it
```

All commands exit 0; the report says all statistics within tolerance.

## Notes for the implementer

- Tune `PassFailureScale` and the shot rates by running `calibrate --matches 1500` in a loop; each run takes
  a few seconds. Do not widen a tolerance to pass.
- The tracking tick must be emitted at the same point in the loop on every run or determinism breaks.
- `JsonSchema.Net` is the schema validator; check its current major version on NuGet before pinning.
