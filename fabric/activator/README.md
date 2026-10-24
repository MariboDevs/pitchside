# Activator: pressure alert

The alert WP-12 asks for: **a side's shots in any 3-minute window reach 4**. It mirrors the `sustained_pressure`
moment (`SustainedPressureRule` in `Pitchside.Moments`): the same 3-minute window of period clock, counted back from
each shot, never across half time. The moment fires at 3 shots or 2 corners and stands down when the side scores; the
alert fires at 4 shots, so it marks only the stronger spells, and it does not look at goals.

## What is committed

| File | What it is |
| --- | --- |
| `pressure-alerts.kql` | The function `PressureAlerts(lookback)` that Activator watches. `fabric-apply.sh` creates it in the KQL database with the rest of the schema. `PressureAlertParityTests` checks its window against `SustainedPressureRule.WindowMs` |
| `ReflexEntities.json` | The item's definition, read back with `infra/scripts/fabric-apply.sh --export-activator`: the KQL source, its event, the object **Pressure spell** keyed by `alertId` with four properties, and the rule **Pressure alert (once per shot)**. `ActivatorDefinitionTests` checks the references, the `alertId` key and the rule's transition at the KQL threshold |

## How the rule was defined

The Activator (Reflex) item definition is public (`ReflexEntities.json`, see the Fabric REST API "Reflex
definition" article). The first rule was built in the portal, as Microsoft recommends, and exported. It fired on
every event, so one shot sent an email on each of the up to ten polls that returned it. The object and the
once-per-shot rule in the committed file were then written against the documented templates (`SplitEvent`,
`IdentityPartAttribute`, `IdentityTupleAttribute`, `BasicEventAttribute`, `AttributeTrigger` with `NumberBecomes`)
and sent with Update Item Definition (`POST /v1/workspaces/{id}/reflexes/{id}/updateDefinition`, the file base64
encoded as the `ReflexEntities.json` part, no `format` field: `"format": "json"` is refused with
`InvalidDefinitionFormat`). The committed file is what Fabric returned afterwards, so the same call with it rebuilds
the item. The portal steps below build the same thing by hand.

## Manual definition (portal)

1. In workspace `pitchside-dev`, open the KQL queryset **match-analysis**, tab **Pressure alerts (Activator source)**.
2. Run `PressureAlerts(10m)`, then select **Set alert**. (Or create a new **Activator** item named
   `pitchside-alerts` and add a **KQL source** on the database `pitchside` with the query below.)

   ```kusto
   PressureAlerts(10m)
   ```

3. Run the query **every 1 minute**.
4. Object: **Pressure spell**, keyed by **`alertId`** (`<matchId>:<seq>`, one per qualifying shot). Properties:
   `shotsInWindow`, `matchMinute`, `matchId`, `teamId`.
5. Rule **Pressure alert** on the object's `shotsInWindow`: condition **is greater than or equal to `4`**, firing
   **when it becomes true** (not on every event). The query runs every minute over a 10-minute lookback, so one
   shot comes back on up to ten polls; keyed on `alertId`, the condition becomes true once per shot, so each shot
   sends one email. A rule on every event (the first version) sent one email per poll.
6. Action: an email (or a Teams message) to the demo owner. Headline: `Pressure alert`. Message:
   `Side {teamId} has {shotsInWindow} shots in 3 minutes (match {matchId}, minute {matchMinute}).` Every value is a
   column of the query: numbers from code.
7. Start the rule **before** replaying: a match id replays once per store, and `PressureAlerts` looks back only over
   its lookback, so a rule started after the spell sees nothing. Then replay a match with a pressure spell and take
   the screenshot of the alert for `docs/FABRIC.md`.
   Seed 13 has two: one side reaches 4 shots in 3 minutes at minute 27 and the other at minute 83. The demo seed 26
   (`docs/DEMO.md`) has none, nor does the sample seed 7. Of seeds 1 to 120, 11 reach the threshold (6, 8, 9, 13,
   32, 54, 57, 58, 65, 116, 119), counted from `simulate` output with the same window and threshold.
8. Run `infra/scripts/fabric-apply.sh <workspace-id> --export-activator` and commit `fabric/activator/ReflexEntities.json`.
