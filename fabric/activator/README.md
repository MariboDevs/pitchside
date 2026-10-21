# Activator: pressure alert

The alert WP-12 asks for: **a side's shots in any 3-minute window reach 4**. It mirrors the `sustained_pressure`
moment (`SustainedPressureRule` in `Pitchside.Moments`): the same 3-minute window of period clock, counted back from
each shot, never across half time. The moment fires at 3 shots or 2 corners and stands down when the side scores; the
alert fires at 4 shots, so it marks only the stronger spells, and it does not look at goals.

## What is committed

| File | What it is |
| --- | --- |
| `pressure-alerts.kql` | The function `PressureAlerts(lookback)` that Activator watches. `fabric-apply.sh` creates it in the KQL database with the rest of the schema. `PressureParityTests` checks its window against `SustainedPressureRule.WindowMs` |
| `ReflexEntities.json` | Not committed yet. After the rule exists, `infra/scripts/fabric-apply.sh --export-activator` saves the item's definition here |

## Why the rule is defined by hand

The Activator (Reflex) item definition is public (`ReflexEntities.json`, see the Fabric REST API "Reflex
definition" article), but the documentation gives the rule's `instance` template only for a sample data source.
It does not document a rule over a KQL source with a per-object key. Microsoft recommends building the item in the
portal and reading it back with Get Item Definition, so that is what the local session does. The exported file
then makes the rule rebuildable by Create Item with that definition.

## Manual definition (portal)

1. In workspace `pitchside-dev`, open the KQL queryset **match-analysis**, tab **Pressure alerts (Activator source)**.
2. Run `PressureAlerts(10m)`, then select **Set alert**. (Or create a new **Activator** item named
   `pitchside-alerts` and add a **KQL source** on the database `pitchside` with the query below.)

   ```kusto
   PressureAlerts(10m)
   ```

3. Run the query **every 1 minute**.
4. Object: **Pressure spell**, keyed by `matchId` and `teamId` (Activator joins two columns into one key when you
   choose both). Properties: `shotsInWindow`, `matchMinute`, `seq`.
5. Rule **Pressure alert**: condition **on every new event** of the object where `shotsInWindow` is greater than or
   equal to `4`. Every row the query returns already meets it; the condition repeats the threshold so the rule
   reads on its own.
6. Action: an email (or a Teams message) to the demo owner. Headline: `Pressure alert`. Message:
   `Side {teamId} has {shotsInWindow} shots in 3 minutes (match {matchId}, minute {matchMinute}).` Every value is a
   column of the query: numbers from code.
7. Start the rule. Replay a match with a pressure spell and take the screenshot of the alert for `docs/FABRIC.md`.
   Seed 13 has two: one side reaches 4 shots in 3 minutes at minute 27 and the other at minute 83. The demo seed 26
   (`docs/DEMO.md`) has none, nor does the sample seed 7. Of seeds 1 to 120, 11 reach the threshold (6, 8, 9, 13,
   32, 54, 57, 58, 65, 116, 119), counted from `simulate` output with the same window and threshold.
8. Run `infra/scripts/fabric-apply.sh <workspace-id> --export-activator` and commit `fabric/activator/ReflexEntities.json`.
