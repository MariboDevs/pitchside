# Activator: pressure alert

The alert WP-12 asks for: **a side's shots in any 3-minute window reach 4**. It mirrors the `sustained_pressure`
moment (`SustainedPressureRule` in `Pitchside.Moments`): the same 3-minute window of period clock, counted back from
each shot, never across half time. The moment fires at 3 shots or 2 corners and stands down when the side scores; the
alert fires at 4 shots, so it marks only the stronger spells, and it does not look at goals.

## What is committed

| File | What it is |
| --- | --- |
| `pressure-alerts.kql` | The function `PressureAlerts(lookback)` that Activator watches. `fabric-apply.sh` creates it in the KQL database with the rest of the schema. `PressureAlertParityTests` checks its window against `SustainedPressureRule.WindowMs` |
| `ReflexEntities.json` | The item's definition, read back with `infra/scripts/fabric-apply.sh --export-activator`: the KQL source, its event, and the rule **Pressure alert (once per shot)**. `ActivatorDefinitionTests` checks the references, the source's one-minute window and the rule's threshold |

## How the rule was defined

The Activator (Reflex) item definition is public (`ReflexEntities.json`, see the Fabric REST API "Reflex
definition" article). The first rule was built in the portal, as Microsoft recommends, and exported. Its source ran
`PressureAlerts(10m)` every minute, so one shot came back on up to ten polls, and the rule fired on every event.
The committed definition fixes this at the source and was sent with Update Item Definition
(`POST /v1/workspaces/{id}/reflexes/{id}/updateDefinition`, the file base64 encoded as the `ReflexEntities.json`
part, no `format` field: `"format": "json"` is refused with `InvalidDefinitionFormat`). The committed file is what
Fabric returned afterwards, so the same call with it rebuilds the item.

Two versions came in between, and the tests now reject both:

- An object keyed on `alertId`, with a rule on `shotsInWindow` "becomes" 4 or more. It never fired: each object
  had one value, already at 4, so nothing crossed the threshold.
- The source in time-axis mode on `publishedUtc`, with `startTime` and `endTime` parameters. The watermark passed
  to the query did not move, so the `MER-6` row came back on every run for as long as it stayed within the
  lookback (24 runs). The rule still fired once for it (its history, 11:52:13 UTC), so Activator drops events at or
  before its own watermark. That behaviour is not documented, so the committed source does not rely on it.

The source now selects by the clock instead: each run keeps the rows whose shot reached the database
(`ingestedUtc`, the shot's first `ingestion_time()`) in the whole minute that ended 2 minutes before it. Runs come
once a minute, about 20 seconds past it, so consecutive runs take consecutive minutes. Selecting by arrival rather
than publication means a shot that reaches the database late is still sent, once, and a redelivered copy keeps the
first arrival, so it is not sent again. Rows reach the database 1 to 2 seconds after publication (`MER-9`).

The window keeps no state between runs, so it relies on one run per clock minute. From 22 October 18:32 to
24 October 13:03 UTC Activator ran 2,551 times over 2,552 minutes (`.show queries`), none failed: two minutes had no
run, whose alerts would be lost, and one had two, whose alerts would be sent twice. That is about 1 alert in 1,000
lost and 1 in 2,500 repeated, accepted for a broadcast demo alert. Removing it needs consumption state outside the
query, for example a table of sent `alertId`s written by the action, which this version does not have.

## Manual definition (portal)

1. In workspace `pitchside-dev`, open the KQL queryset **match-analysis**, tab **Pressure alerts (Activator source)**.
2. Create an **Activator** item named `pitchside-alerts` and add a **KQL source** on the database `pitchside` with
   the query below.

   ```kusto
   let until = bin(now(), 1m) - 2m;
   PressureAlerts(1h)
   | where ingestedUtc >= until - 1m and ingestedUtc < until
   ```

3. Run the query **every 1 minute**, with no event time setting. Each run keeps the shots that arrived in one whole clock
   minute, ending 2 minutes before the run, so each row arrives once. The one-hour lookback only bounds the scan.
4. No object is needed: the rule works on the source's events.
5. Rule **Pressure alert (once per shot)** on the event: field `shotsInWindow` **is greater than or equal to `4`**.
   Each qualifying shot is one row and arrives once, so each shot sends one email.
6. Action: an email (or a Teams message) to the demo owner. Headline: `Pressure alert`. Message:
   `Side {teamId} has {shotsInWindow} shots in 3 minutes (match {matchId}, minute {matchMinute}).` Every value is a
   column of the query: numbers from code.
7. Start the rule **before** replaying: a match id replays once per store, and each run reads only the minute
   just past, so a rule started after the spell sees nothing. Then replay a match with a pressure spell and take
   the screenshot of the alert for `docs/FABRIC.md`.
   Seed 13 has two: one side reaches 4 shots in 3 minutes at minute 27 and the other at minute 83. The demo seed 26
   (`docs/DEMO.md`) has none, nor does the sample seed 7. Of seeds 1 to 120, 11 reach the threshold (6, 8, 9, 13,
   32, 54, 57, 58, 65, 116, 119), counted from `simulate` output with the same window and threshold.
8. Run `infra/scripts/fabric-apply.sh <workspace-id> --export-activator` and commit `fabric/activator/ReflexEntities.json`.
