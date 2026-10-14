# Event schema

Every match is two files: `match.json` (metadata, squads, kickoff) and `events.jsonl` (one event per line, in
order). The JSON Schemas under [`schemas/`](../schemas/) are the contract; the C# records in
`src/Pitchside.Contracts` are generated to match them and tested against the sample match.

## Coordinates

Pitch is 105 m by 68 m. `x` runs 0 to 105 from the attacking team's own goal line to the goal they attack;
`y` runs 0 to 68 from the attacking team's left touchline. Coordinates are always expressed in the attacking
team's frame, so a renderer flips them for the away team.

## Clock

`period` is 1 or 2. `clockMs` is milliseconds since the start of the period, including stoppage time.
`wallTimeUtc` is when the event was emitted, so a live consumer can measure its own lag.

## Event types (v0)

| Type | Meaning | Qualifiers |
| --- | --- | --- |
| `kick_off` | Start of a period | |
| `pass` | A pass attempt | `endLocation`, `lengthM`, `difficulty` (0 to 1), `completed` |
| `carry` | Ball carried by a player | `endLocation`, `lengthM` |
| `shot` | A shot attempt | `xg` (0 to 1), `onTarget`, `outcome` (goal, saved, blocked, off_target, post), `shotSpeedKph`, `bodyPart`, `penalty` |
| `goal` | A goal, always immediately after the `shot` that scored it | `scorer`, `assist`, `homeScore`, `awayScore` |
| `turnover` | Possession lost in play | `reason` (interception, tackle, bad_touch, clearance) |
| `out_of_play` | Ball left the pitch | `restart` (throw_in, goal_kick, corner) |
| `corner` | A corner kick taken | |
| `foul` | A foul committed | `by` (the fouling player), `cardAwarded` |
| `card` | A yellow or red card | `colour`, `secondYellow` |
| `substitution` | A substitution | `playerOff`, `playerOn` |
| `pressure` | A pressing action on the ball carrier | `intensity` (0 to 1) |
| `half_end` | End of a period | |
| `full_time` | End of the match | `homeScore`, `awayScore` |
| `tracking` | Per-player movement over the last tracking interval, one event per side | `tracking` |

Every event carries `id`, `matchId`, `seq`, `period`, `clockMs`, `wallTimeUtc`, `type`, `teamId`, `playerId`,
`location`, `possessionId`. Qualifiers are nullable and only present where the type defines them.

## Passes, penalties and tracking (schema 0.2)

- `completed` on a `pass` may be `false`. An incomplete pass has no `receiverId` and is always followed by a
  `turnover` with reason `interception` by the same side, in the same possession, at the pass's end location.
- `penalty: true` marks a `shot` taken from the spot, `(94, 34)`, at a fixed xG of 0.76. A penalty is a
  possession of one shot, awarded after a foul by the defending side with the ball beyond x = 88.
- A `tracking` event is emitted by each side at the first possession boundary after each minute of period
  clock rolls over, home side first. `playerId` is null and `location` is the centre spot. Its `tracking`
  array holds one `{ playerId, distanceM, topSpeedKph }` entry per player on the pitch for that side. Each
  entry is the delta since that player's previous tick (or since kick-off of the period, or since coming on),
  never the cumulative: a consumer sums the deltas.
- Every player in `match.json` carries `workRate`, metres per minute on the ball-less run, 95 to 125.

## Why these fields

The rules name the metrics to surface: player identification with speed and distance thresholds, pass distance,
accuracy and difficulty, ball and shot speed, pressure events, possession changes. Each one is a first-class
field here, not something inferred later.

## Versioning

`match.json` carries `schemaVersion`. A breaking change bumps it; the engine always writes the current version
and the sample under `data/samples/` is regenerated and committed in the same pull request.
