# WP-12: Microsoft Fabric Real-Time Intelligence

| | |
| --- | --- |
| Branch | `feat/wp12-fabric` |
| Depends on | WP-03 with the Event Hubs bus in `dev` |
| Owner | Sipho, executed by Claude Code local for what the APIs allow; portal steps named |
| Effort | 2 days |
| Changes contracts | none |

## Goal

Land the live event feed in Fabric so an analyst can query a match with KQL and a studio can watch a real-time
dashboard, the way a league's data team would. Fabric is a named hero technology that no competitor repo
touches; one working Eventstream, one KQL database, one dashboard and one alert are the whole scope.

## Design

```
Event Hubs (hub match-events, consumer group fabric)
   --> Fabric Eventstream "pitchside-events"
        --> Eventhouse / KQL database "pitchside", table events (one row per event, JSON columns expanded)
              --> KQL queryset "match-analysis" (saved queries)
              --> Real-Time Dashboard "Pitchside Live"
              --> Activator rule: alert when a side's shots in any 3-minute window reach 4 (mirrors sustained_pressure)
```

## Steps

1. **Capacity.** `fabric.bicep` creates an F2 capacity in the resource group (`Microsoft.Fabric/capacities`),
   paused by default; `infra/scripts/fabric-capacity.sh resume|pause` toggles it. A trial capacity, if the
   tenant offers one, may be used for `dev` instead. **Human step:** enabling Fabric for the tenant and
   assigning a workspace to the capacity happens in the Fabric admin portal.
2. **Workspace.** Workspace `pitchside`, created through the Fabric REST API with a service principal that
   Claude Code local registers (`az ad sp create-for-rbac`, no secret stored: use certificate or federated
   credential; if the API insists on a secret, keep it in Key Vault only).
3. **Eventstream** from the Event Hubs source using the `fabric` consumer group with identity-based auth where
   Fabric supports it; otherwise the connection uses a Key Vault-held SAS scoped to Listen on that one hub
   and documented as the single exception.
4. **Eventhouse and table** with an update policy that expands the envelope's `payloadJson` into typed
   columns: `matchId, seq, period, clockMs, type, teamId, playerId, x, y, xg, outcome, difficulty, lengthM,
   speedKph, publishedUtc`.
5. **Saved KQL queries** committed under `fabric/kql/`: shots by side per 5 minutes, xG race, pass completion
   by player, pressing intensity over time, momentum (the same weighted formula as WP-01, so the dashboard and
   the overlay agree), top speeds.
6. **Dashboard** `fabric/dashboard/pitchside-live.json` exported and committed; tiles for the six queries
   plus a match selector.
7. **Activator** rule committed as JSON where the API allows export; otherwise documented with a screenshot.
8. **Evidence.** `docs/FABRIC.md` with screenshots of the stream, the table row count matching the API's
   event count for a replay, the dashboard, and the alert firing.

## Done when

- A replay in `dev` produces rows in the KQL table within 30 s; `count` equals the API's event count for the
  match.
- The dashboard shows the match and updates live.
- `docs/FABRIC.md` exists with the evidence and the exact portal steps a human performed.

## Notes for the implementer

- Every Fabric artefact that can be exported is committed; what cannot is screenshotted. The repo must let a
  reader rebuild it.
- Pause the capacity whenever the team is not demonstrating; the budget alert from WP-09 covers it.
