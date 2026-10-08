# WP-05: Overlay timeline, overlay, control room, viewer

| | |
| --- | --- |
| Branch | `feat/wp05-ui` |
| Depends on | WP-03 (hub and API), WP-04 contracts (narratives may be scripted) |
| Owner | Sipho (implemented by Claude Code); design review by Pretty |
| Effort | 4 days |
| Changes contracts | adds `schemas/overlay-entry.schema.json` |

## Goal

Render the intelligence so it can sit on screen beside the match, the way a broadcaster's graphics do, and give
the producer and the fan their own views. This is the "Render" stage and most of the User Experience score.
It must look like a product a broadcaster would pay for, not a hackathon dashboard.

## Three surfaces, one Angular application

| Route | Audience | Purpose |
| --- | --- | --- |
| `/overlay/:matchId?aspect=16x9|9x16&persona=&lang=` | the vision mixer or OBS browser source | transparent background, graphics only, no chrome, no pointer events |
| `/control/:matchId` | producer, judge | the control room: feed, fact sheet, moments, narratives with the citation chain, agent trace, approvals, usage |
| `/viewer/:matchId` | fan | the second screen: pitch view, live cards in the chosen persona and language, player focus |
| `/` | everyone | match list, start a replay, links to the three surfaces |

Stack: Angular (current major), standalone components, signals, zoneless, OnPush, Tailwind v4 with CSS
variable tokens, three files per component, no Angular Material, no CDK. SignalR client via
`@microsoft/signalr`. Charts hand-drawn in SVG (no chart library) so they match the design language and
animate with the clock.

## Design

Apply the `maribo-design` skill to the control room, viewer and home routes: it is the house language and the
judges will see consistency across every screen. The overlay has its own rules below, because broadcast
graphics are a different medium.

### Overlay graphics rules

1. **Safe areas.** Nothing within 5% of any edge (title-safe). A `?safe=0.05` query overrides for a studio.
2. **Lower third anatomy.** Left: a 6 px accent bar in the side's primary colour; a club monogram (two or three
   letters in the club's colours, generated, never an image); then the headline line (display face, 600
   weight) and the narrative line (text face, 400). Right: a stat chip row of at most three chips, each a
   fact with its label, tabular numerals. Height 112 px at 1080p; scale with `vh`.
3. **Score bug.** Top left, always on when the match is live: both monograms, score, clock. The clock is
   driven by the match clock from the hub, not by the browser.
4. **xG race.** A thin dual-line chart along the bottom edge above the lower third, home above away, filled
   to the baseline at 20% opacity; goals as dots; updates on every `FactSheet`.
5. **Momentum ribbon.** A 6 px bar under the score bug, split by `match.momentum.home` and `.away`, colours
   from the clubs, eased over 600 ms.
6. **Motion.** Enter: translate 24 px with opacity over 320 ms, cubic-bezier(0.2, 0.8, 0.2, 1). Exit: 200 ms
   opacity. Never two cards animating at once; the scheduler guarantees it, the renderer asserts it.
   `prefers-reduced-motion` disables translation and keeps opacity.
7. **Typography.** Display: a geometric sans from Google Fonts with tabular numerals enabled
   (`font-variant-numeric: tabular-nums`); text: a humanist sans. Minimum 28 px for any text at 1080p, so it
   survives a 720p downscale and a phone.
8. **Colour.** Panels are near-black at 78% with a 1 px inner highlight; text is off-white; the only
   saturated colour on screen is the clubs'. Contrast of text on panel at least 7:1.
9. **Aspect ratios.** `16x9` is the default. `9x16` lays the same elements out for vertical video: score bug
   top centre, xG race as a vertical sparkline on the right edge, lower third becomes a bottom card. This is
   a named roadmap item in the Premier League's own platform work and no competitor will have it.
10. **Languages and scripts.** isiZulu and Afrikaans text are longer than English; every text box clamps to
    two lines with ellipsis and the scheduler shortens the out time, never the text.

### Control room

Three columns at desktop, stacked at phone width:

- **Feed**: the event stream, newest at top, filterable by type and side, each row showing clock, side
  monogram, type, player, and the one qualifier that matters (xG for shots, difficulty for passes).
- **Intelligence**: moments as cards in time order, each expanding to the narrative with every `[[key]]`
  rendered as a hoverable chip showing the fact's value and the `derivedFrom` event ids, which highlight in
  the feed when hovered. Below the narrative: the fact-checker verdict, attempts, provider, model, prompt
  version, latency, and the groundedness score when WP-07 has written it. A **Hold** and **Air** toggle per
  card: a held card is not sent to the overlay; **Air** releases it. Default is auto-air; a producer can set
  the match to "approve before air", which is the enterprise control the rubric wants to see.
- **State**: the live fact sheet grouped by match, home, away, players; the agent trace as a horizontal
  swimlane per moment (stage boxes with durations, red when a stage retried); replay controls (pause, resume,
  speed); the usage guard panel (WP-07).

Keyboard: `Space` pause and resume the replay, `H` hold the selected card, `A` air it, `J`/`K` move
selection, `/` focus the feed filter.

### Viewer

- Top: score bug and clock. Middle: a pitch drawn in SVG with the last 20 events' locations as fading marks
  in club colours and the ball's current location; a goal flashes the pitch edge in the scorer's colour.
- Persona selector (Analyst, Casual, Club fan, Player focus) and language selector (English, Afrikaans,
  isiZulu, Spanish) as segmented controls; club fan asks which club, player focus asks which player, with
  the team sheets as the picker. Changing either sends `SetViewer` on the hub and the next card arrives in
  the new mode; previously shown cards re-render from stored variants when available.
- Cards stack newest on top, at most five, each with the same lower-third anatomy as the overlay so the
  product reads as one system.
- Player focus adds a strip: minutes, distance, top speed, sprints, passes and completion, xG, with the
  speed and distance tags shown as badges when they fire.

### Home

Match list from `/api/v1/matches` with status chips, a "Start replay" form (seed, speed, with 7 prefilled),
and for each match the three surface links plus an "Open in OBS" copy-URL button.

## Overlay timeline contract (`schemas/overlay-entry.schema.json`, `src/Pitchside.Contracts/OverlayEntry.cs`)

```json
{
  "id": "MER-7:M:0812:goal:lower_third",
  "matchId": "MER-7",
  "momentId": "MER-7:M:0812:goal",
  "layer": "lower_third",
  "slot": "bottom",
  "priority": 100,
  "inAtMs": 1322000,
  "outAtMs": 1330000,
  "period": 2,
  "narrativeId": "MER-7:M:0812:goal:base:en",
  "chips": [ { "label": "xG", "factKey": "home.xg" }, { "label": "Shots", "factKey": "home.shots" } ],
  "state": "scheduled"
}
```

Layers: `score_bug` (persistent), `lower_third`, `stat_chip`, `ribbon`. Slots: `top_left`, `bottom`,
`right_rail`. `state`: `scheduled`, `held`, `aired`, `expired`. Times are period clock milliseconds, so a
renderer replaying the match can place entries identically.

**Scheduler** (`Pitchside.Overlay`, code, runs in the API after a narrative is verified):

- A lower third shows for `6 s + 0.25 s per word` capped at 12 s, starting 2 s after the narrative is ready.
- One lower third at a time. A higher-priority moment pre-empts: the current entry's `outAtMs` is cut to
  now and the new one starts after the exit animation. Lower priority queues and expires if it would start
  more than 45 s after its moment.
- `goal` entries always carry the score chips; `big_chance_missed` carries xG; `momentum_swing` carries the
  momentum share and shots; `full_time` carries nothing and uses the full-width card variant.
- Held entries (control room) stay `held` until aired or expired.

## API and hub additions

| Route | Purpose |
| --- | --- |
| `GET /api/v1/matches/{id}/overlay?fromMs=` | entries |
| `POST /api/v1/matches/{id}/overlay/{entryId}/hold`, `/air` | producer controls |
| `POST /api/v1/matches/{id}/approval` `{ mode: "auto" \| "approve" }` | approval mode |
| `GET /api/v1/matches/{id}/narratives?persona=&language=` | |

Hub: `OverlayEntry` message on schedule and state change; `ApprovalMode` message.

## Files

- `frontend/` Angular workspace: `src/app/{overlay,control,viewer,home}/`, `src/app/shared/` (score bug,
  lower third, chip, monogram, pitch, xg-race, momentum-ribbon, swimlane, segmented-control),
  `src/app/core/` (hub client with reconnect and snapshot replay, API client, viewer preferences store,
  match clock store), `src/styles/tokens.css`, `ui-invariants.config.ts`, Playwright config
- `src/Pitchside.Overlay/` scheduler and tests
- `src/Pitchside.Api/Endpoints/OverlayEndpoints.cs`
- `frontend/e2e/`: Playwright specs that start a replay at speed 0, open each route, assert the score bug,
  at least one lower third, persona switching, 9x16 layout, and take screenshots to `artifacts/screens/`
- CI: `frontend` job with `npm ci`, `ng build`, `ng test`, `lint`, Prettier check, ui-invariants scan on all
  four routes, Playwright e2e against the API started in the job

## Done when

```bash
dotnet test Pitchside.slnx
cd frontend && npm ci && npm run lint && npm run format:check && npx ng build && npx ng test --watch=false
npx ui-invariants --config ui-invariants.config.ts
npm run e2e        # starts the API, runs Playwright, writes artifacts/screens/*.png
```

Pretty reviews the screenshots against this document and the design skill and records the pass in the gap
register with the screenshot paths.

## Notes for the implementer

- Build the overlay first, on scripted narratives: it is the surface in the video.
- The hub client must survive a reconnect by re-joining and applying the snapshot; test it by killing the
  API mid-replay.
- Every number on screen comes from a fact or an event; the frontend computes nothing but layout.
- Transparent background means `html, body { background: transparent }` on the overlay route only.
