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

## Fact sheet

The stats layer (`src/Pitchside.Stats`) turns events into facts by code alone. A fact sheet is every fact true as
of one event sequence number; its contract is [`schemas/fact-sheet.schema.json`](../schemas/fact-sheet.schema.json)
and `src/Pitchside.Contracts/FactSheet.cs`. Print one with
`dotnet run --project src/Pitchside.Cli -- facts --seed 7 [--at-seq N]`.

Each fact has a `key` (the citation handle, built only by `FactKeys`), a `value` (a number, or a string for
categorical facts), a `unit`, a `precision` (decimals shown; the value is already rounded to it, half away from
zero), `derivedFrom` (the ids of the events that produced it, the 50 most recent, oldest first) and, for rolling
metrics only, a `window` of sequence numbers.

### Catalogue

Scopes are `match`, `home`, `away` and `player.<playerId>`. Side metrics exist for both `home` and `away`.

| Key pattern | Unit | Definition |
| --- | --- | --- |
| `match.clock.minute` | min | floor of match minute: 1 to 45+ in the first half, 46 to 90+ in the second, 0 before kick-off |
| `match.period` | | 0 before kick-off, then 1 or 2 |
| `match.state` | | `pre_match`, `first_half`, `half_time`, `second_half`, `full_time` |
| `match.score.home`, `match.score.away` | goals | `goal` events per side |
| `match.possessions` | count | distinct possession ids so far; `derivedFrom` is the first event of each |
| `match.rhythm` | per min | distinct possessions in the last 10 minutes of the period, over the minutes elapsed in that window (at least 1) |
| `match.control` | share | completed passes and carries over open-play events (passes, carries, shots, turnovers, pressures, fouls, out of play) in the last 10 minutes; 0.5 when there are none |
| `match.momentum.home`, `match.momentum.away` | share | each side's share of a weighted sum over the last 5 minutes: 3 per shot, 4 more per shot on target, 1 per final-third entry, 2 per corner, 10 per unit of xG; 0.5 each when nothing happened |
| `{side}.goals`, `.xg`, `.shots`, `.shotsOnTarget`, `.bigChances` | goals, xg, count | a big chance is a shot with xG at least 0.30; xG includes penalties |
| `{side}.passes`, `.passesCompleted`, `.passCompletion` | count, share | completion is 0 before the first pass |
| `{side}.passDifficultyAvg` | difficulty | mean difficulty of attempted passes |
| `{side}.progressivePasses` | count | completed passes gaining at least 10 m towards goal |
| `{side}.finalThirdEntries` | count | completed passes or carries ending beyond x = 70 from x at most 70 |
| `{side}.possessionShare` | share | share of possession time: the clock between two events goes to the side the earlier events showed in possession. On-ball events show their own side; a turnover or a goal hands the ball to the other side; a pressure, foul or card shows the other side in possession; a goal kick or corner goes to the side that did not put the ball out; a throw-in shows nobody |
| `{side}.corners`, `.fouls`, `.yellows`, `.reds` | count | a second yellow counts as a yellow and a red |
| `{side}.pressures`, `.pressingIntensity` | count, per pass | intensity is the side's pressures over the opponent's passes in the last 10 minutes |
| `{side}.distanceKm` | km | sum of the side's tracking deltas, which already include carry lengths (WP-00 R4) |
| `{side}.topSpeedKph` | kph | fastest player on the side |
| `player.<id>.passes`, `.passesCompleted`, `.passCompletion`, `.progressivePasses`, `.carries` | count, share | |
| `player.<id>.distanceKm`, `.topSpeedKph` | km, kph | tracking deltas; top speed is the fastest tracking reading or carry |
| `player.<id>.shots`, `.xg`, `.goals`, `.assists` | count, xg, goals | |
| `player.<id>.pressures`, `.fouls`, `.cards` | count | |
| `player.<id>.minutes` | min | whole minutes of period clock on the pitch, from kick-off or coming on to half end, going off or now |
| `player.<id>.sprints` | count | carries at 25 kph or faster plus tracking intervals topping 30 kph |
| `player.<id>.tags` | | comma-separated names of the tags the player has raised, first raise first |

Player facts exist only for players who have appeared: the starters from kick-off, substitutes from coming on.
Rolling windows use period clock and never cross half time.

### Tags

| Tag | Trigger |
| --- | --- |
| `speed.30`, `speed.33` | a player's top speed first reaches 30 (33) kph |
| `distance.5`, `distance.10` | a player's distance first reaches 5 (10) km |
| `shot.fast` | a shot at 100 kph or faster |
| `pass.hard` | a completed pass with difficulty at least 0.6 |

A tag is `{ "tag", "playerId", "eventId", "value" }` and is returned by the step that raised it.

## Moments

The moment detector (`src/Pitchside.Moments`) decides by rule, with no model, that a span of events matters and
attaches the evidence a narrative may cite. Its contract is [`schemas/moment.schema.json`](../schemas/moment.schema.json)
and `src/Pitchside.Contracts/Moment.cs`. Print a match's moments with
`dotnet run --project src/Pitchside.Cli -- moments --seed 7`.

A moment's `id` is `{matchId}:M:{triggerSeq:0000}:{type}`; `factSheetSeq` is the sheet its fact keys resolve
in; `headline` is a code template. Types, priorities and rules are the table in
[WP-02](specs/WP-02-moments.md). Where the table leaves room, the detector decides:

- A missed penalty is a `penalty` moment only, not also a `big_chance_missed`.
- `sustained_pressure` is evaluated on the side's shots and corners, and a goal by that side inside the three
  minutes cancels it.
- The clean-sheet `milestone` fires once, at the first second-half event of minute 80; when both sides still
  keep one, a single moment with a null `sideId` covers both, and when a brace or hat-trick lands on that same
  event one moment carries both headlines, so the derivable id stays unique and the rate limit drops neither.
- The 90-second match-wide gap is measured on period clock and resets at half time. A moment inside the gap
  is dropped unless its priority is higher than the one that opened it; goal, red card, penalty, half time and
  full time are never limited and never open a gap.

## Narratives

The agent crew (`src/Pitchside.Agents`) turns a moment into text. Its contract is
[`schemas/narrative.schema.json`](../schemas/narrative.schema.json) and `src/Pitchside.Contracts/Narrative.cs`.
A narrative's `id` is `{momentId}:{persona}:{language}`; the base narrative is persona `base`, language `en`. A
club-fan or player-focus variant names its club or player in `subject`, and its id is
`{momentId}:{persona}.{subject}:{language}`, for example `MER-7:M:0324:goal:player_focus.INY-07:af`.

Each sentence is written with `[[fact.key]]` tokens in place of every number, and its `citations` are the keys
of those tokens. `renderedText` is produced by code, substituting each token with the fact's value at its
precision. `status` is `verified` (passed the fact-checker), `fallback` (the moment type's code template after
two failed explainer attempts), `rejected` or `draft`. Run the whole crew offline with
`dotnet run --project src/Pitchside.Cli -- narrate --seed 7 --provider scripted`.

## Spoken recap

At full time the crew speaks a recap in every viewer language (WP-10). Its contract is
[`schemas/recap.schema.json`](../schemas/recap.schema.json) and `src/Pitchside.Contracts/Recap.cs`: the match and
`full_time` moment, the narrative that was read (the analyst variant in that language, else the base), the voice,
the SSML, and `englishFallback` when the language has no voice or its translation fell back to English, in which
case `spokenLanguage` is `en` and the English audio stands in. The SSML is built by code from the rendered
sentences, so every number in the audio is a number the fact sheet holds. Try it offline with
`dotnet run --project src/Pitchside.Cli -- narrate --seed 7 --provider scripted --recap --speech scripted`, which
writes `out/seed-7/recap/{en,af,zu,es}.mp3`.

## Live runtime

A match goes live through the runtime (`src/Pitchside.Runtime`): a producer publishes `EventEnvelope`s
(`matchId`, `seq`, `type`, `payloadJson`, `publishedUtc`) to the bus, partitioned by match id; one Akka.NET
actor per match applies them strictly in sequence, runs the stats and moment stages, stores events, fact
sheets (at every moment and every minute of clock) and moments, and pushes to clients.

### SignalR hub (`/hubs/match`)

| Direction | Method | Payload |
| --- | --- | --- |
| client to server | `JoinMatch(matchId)` | replies with a `MatchSnapshot`: the stored match, the live fact sheet, every moment and the narratives for the connection's viewer preferences |
| client to server | `LeaveMatch(matchId)` | |
| client to server | `SetViewer(matchId, ViewerPreferences)` | `{ persona, language, clubId?, playerId? }`, kept per connection; persona `all` is the control room |
| server to client | `Event` | `MatchEvent`, every ingested event |
| server to client | `FactSheet` | `FactSheet`, every 5 events or on a moment |
| server to client | `Moment` | `Moment` |
| server to client | `Narrative` | `Narrative`, only to connections whose viewer preferences it matches: the base English narrative to everyone, otherwise the viewer's persona (its club or player) and the base in its language, plus the casual variant for player focus |
| server to client | `MatchStatus` | `{ matchId, status, lastSeq, period, clockMs }`, on change and every 10 s |

Messages use the contract serializer (camelCase, snake_case enums) and arrive in the order the actor produced
them.

### API (`/api/v1`)

`GET /matches`, `GET /matches/{matchId}`, `GET /matches/{matchId}/events?fromSeq=&toSeq=`,
`GET /matches/{matchId}/facts?atSeq=` (recomputed exactly from the stored events when `atSeq` is given),
`GET /matches/{matchId}/moments`, `POST /replays { seed, speed }` (202; 409 when the match is already stored;
429 at the cap), `DELETE /replays/{matchId}`, `POST /replays/{matchId}/pause` and `/resume`. A seeded match
without replaying it: `GET /simulations/{seed}` and `/simulations/{seed}/events`. The spoken recap:
`GET /matches/{matchId}/recap?language=` (the `Recap`) and `/recap/audio?language=` (its MP3); both are 404 with a
problem detail titled "Speech is off" when `Speech:Provider` is `None`, and "No recap yet" before full time. Errors are RFC 7807 problem
details and every response carries `X-Request-ID`.
