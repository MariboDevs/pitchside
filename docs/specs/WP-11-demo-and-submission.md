# WP-11: Demo match, video, README, submission

| | |
| --- | --- |
| Branch | `docs/wp11-submission` |
| Depends on | everything deployed to `demo` |
| Owner | Hlumela (pitch, video, submission), Pretty (evidence, judge-as-user pass), Sipho (final freeze) |
| Effort | 22 to 25 October |
| Changes contracts | none |

## The demo match

Seed 7 is the committed sample, but the demo seed is chosen for drama: run `simulate` across seeds 1 to 500,
score each by goals (3 to 5), a comeback (score lead changes at least once), a red card or a penalty, a big
chance missed, and a momentum swing in each half; pick the highest-scoring seed and record it in
`docs/DEMO.md` with its moment list. The `demo` environment auto-starts that seed on a loop at speed 1 so a
judge who opens the link sees a live match within a minute, with a "jump to next moment" control that
advances the replay to 10 seconds before the next moment at speed 0 and resumes at speed 1.

Built: `demo-seed` (the scoring above, `Pitchside.Moments.Demo`), `Demo:AutoStart` and `Demo:Seed`, which start the
seed when the host is up, `POST /api/v1/replays/{matchId}/jump` with a button in the viewer and the control room, and
the loop (`Demo:Loop`, on by default). A match id is replayed once per store, so the owner chose a new id per loop
over resetting stored records: loop 1 is `MER-26`, then `MER-26-2`, `MER-26-3`, each started `Demo:LoopPauseSeconds`
(120) after the previous replay ends. The contracts accept `MER-<seed>-<loop>` (see the contract notes in WP-00, WP-01,
WP-02, WP-04, WP-05, WP-07, WP-08 and WP-10). Each loop is a full match for the crew, so with Foundry it costs what a
match costs; the usage guard's narratives-per-hour cap still applies.

## The video, under two minutes

| Second | Shot | Line |
| --- | --- | --- |
| 0 to 8 | the overlay over the pitch view, a goal lands, lower third animates in | "Football stories do not start and end with the scoreline." |
| 8 to 25 | control room: the narrative, hover a chip, the feed highlights the events | "Every number comes from code. Every word cites its number. The fact-checker refuses anything else." |
| 25 to 40 | the Aspire dashboard or Application Insights trace: explainer, checker, persona, translator swimlane | "Six agents on Microsoft Agent Framework, hosted on Foundry, with typed handoffs and a scripted fallback so the broadcast never waits." |
| 40 to 60 | viewer: switch analyst to casual to club fan to player focus; switch English to isiZulu | "The same intelligence, four audiences, four languages." |
| 60 to 75 | 9x16 overlay on a phone frame | "Vertical, for the stream." |
| 75 to 90 | calibration report and evaluation report side by side | "Synthetic data measured against the real league. Groundedness scored on every line." |
| 90 to 105 | Azure architecture diagram, Cosmos DB, Event Hubs, Container Apps, Fabric dashboard | "Cloud native, identity only, infrastructure as code, deployed with one command." |
| 105 to 118 | the three of you, one line each | "Built in South Africa by Maribo Devs." |

No music unless licensed; the team's own voice-over; no trademarks on screen; captions burned in.
Recorded at 1080p from the `demo` environment, not a local machine.

## README final state

Sections in order: what it is, the rule, 90-second demo link and screenshots, evidence (calibration report
figures, evaluation report figures, test counts), architecture diagram, Microsoft integration matrix with every
row `deployed`, how to run locally, how it was built, team, licence. Every number in the README is copied from a
generated report and links to it.

## Submission checklist (Innovation Studio project page)

- [ ] Title, tagline, keywords, description current with what shipped
- [ ] Category: Best Enterprise Solution, wherever the form asks
- [ ] Video URL public, under 2:00, tested in a private window
- [ ] Repository public on 25 October: visibility flipped, README renders, LICENSE present, no secret in
      history (`git log -p | grep -iE 'key=|secret|password'` returns nothing meaningful; run gitleaks)
- [ ] Demo URL open, free, no login; usage guard limits noted in the description
- [ ] Judge instructions: one paragraph on the project page: open the link, what to click, what to look for
- [ ] All three team members listed on the project
- [ ] Pretty's judge-as-user pass done from a clean machine and a phone, screenshots in `docs/evidence/`

## Freeze

From 25 October only a fix to something a judge would see broken lands, through a pull request, and the demo
is redeployed by `azd deploy --environment demo` with the SHA recorded in the gap register.
