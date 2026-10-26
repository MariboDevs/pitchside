# Judging Pitchside

A guide for the judges of the Inside the Game hackathon: what to try in three minutes, then where the evidence for
each judging criterion is. Every number on this page is copied from a generated report in this repository, and
each one links to it.

| | |
| --- | --- |
| **Live demo** | <https://pitchside-web.proudground-3a2deeab.southafricanorth.azurecontainerapps.io>: free, no login |
| **Video** | [Pitchside in under two minutes](https://www.youtube.com/watch?v=KY_6y-mHiWQ) (1:53) |
| **Category** | Best Enterprise Solution |
| **The rule** | Numbers come from code, words come from AI, and every word cites the numbers it came from. |

## Judge it in three minutes

A match is always on: the demo replays a fictional match at ten times real time, about 11 minutes of football and
then a 2-minute pause before the next loop starts. Open the demo and pick the match marked live (in the pause, open
the newest finished match; every screen works on it too).

1. **Viewer: the same moment, rewritten for you.** Choose **Club fan** and pick Thornbury FC, then switch to
   **Analyst**, then to **Afrikaans** or **Español**. The card for each moment is rewritten for the persona and the
   language; the numbers in it never change, because no model writes them. Press **Jump to next moment** to skip to
   the next goal, penalty or big chance.
2. **Control room: how a card was made.** Open **Control room** and click a goal. Hover a value in its narrative:
   the fact it came from is highlighted, with the events that fact was computed from. Below it is the card's
   verdict, the model and prompt version that wrote it, its groundedness score, and the timeline of every model call
   behind it, with its latency and tokens.
3. **Control room: the producer decides.** Switch **Auto-air** to **Approve before air**. The next cards wait for
   you; open one and press **Air**. Switch back to **Auto-air** when you are done, so the next judge sees cards air
   on their own.
4. **Overlay: what goes on air.** Open **Overlay** for the broadcast lower thirds, timed against the match clock,
   in 16:9 or 9:16.
5. **Full time: the spoken recap.** At the end of a loop the viewer plays a recap spoken in its language by Azure AI
   Speech.

### Ask the match yourself, over MCP

The MCP server is on the demo at `/mcp`. Any MCP client (GitHub Copilot agent mode, Claude Code, your own agent) can
connect to it; with `curl`, the read-only tools answer without a key:

```bash
DEMO=https://pitchside-web.proudground-3a2deeab.southafricanorth.azurecontainerapps.io
# The nine tools.
curl -s $DEMO/mcp -H 'content-type: application/json' -H 'accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | sed -n 's/^data: //p'
# One fact, with the event ids it was computed from. Use a match id from list_matches or the match list.
curl -s $DEMO/mcp -H 'content-type: application/json' -H 'accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_fact","arguments":{"matchId":"MER-26-210","key":"match.score.home"}}}' \
  | sed -n 's/^data: //p'
```

The tools and how to register the server in a client are in [MCP.md](MCP.md).

## The evidence, criterion by criterion

### 1. Technological Implementation

| Claim | Evidence |
| --- | --- |
| The synthetic data is realistic, and that is measured | [Calibration report](calibration/report.md): 5,000 simulated matches against league-level targets, all 26 statistics within tolerance. CI regenerates it on every build and fails on drift ([CALIBRATION.md](CALIBRATION.md)) |
| The engine is deterministic | The same seed gives the same match on every machine. Pinned seeds are golden masters: the sample match's events, its 34 moments and its 752 narratives are compared exactly on every build ([DeterminismTests](../tests/Pitchside.Engine.Tests/DeterminismTests.cs), [SampleMatchMomentsTests](../tests/Pitchside.Moments.Tests/SampleMatchMomentsTests.cs), [NarrativeGoldenTests](../tests/Pitchside.Agents.Tests/NarrativeGoldenTests.cs)) |
| The code is tested and reviewed | 419 .NET test methods across nine test projects in [tests/](../tests), and Playwright end-to-end specs in [frontend/e2e](../frontend/e2e). Every change lands through a pull request with CI (build, test, calibrate, CodeQL, secret scan) and Copilot code review ([ci.yml](../.github/workflows/ci.yml)) |
| The hero technologies do real work | [The integration matrix](MICROSOFT-INTEGRATION.md): what each Microsoft technology does here, and whether it is built or deployed |
| It is documented and specified | One specification per work package and a gap register of what is built and what is open ([specs/](specs/README.md)); the architecture in [ARCHITECTURE.md](ARCHITECTURE.md) |

### 2. Agentic Design and Innovation

| Claim | Evidence |
| --- | --- |
| A crew of agents with distinct roles | Microsoft Agent Framework workflow on Microsoft Foundry: explainer, fact-checker, persona writer and translator, with typed handoffs and the match actor's fact sheet as shared state ([ARCHITECTURE.md, Agent crew](ARCHITECTURE.md#agent-crew), [MomentWorkflow.cs](../src/Pitchside.Agents/Workflow/MomentWorkflow.cs)) |
| Code and models each do what they are good at | Moments are detected by twelve deterministic rules ([Rules](../src/Pitchside.Moments/Rules)), the overlay is scheduled by code, and the models write words only. A narrative's numbers are `[[fact.key]]` tokens resolved by code |
| The checker cannot be talked round | The deterministic fact-check runs first and its verdict on numbers is final: every sentence cites a fact, every token resolves, no digit appears outside a token, every name is in the match ([CodeFactChecker.cs](../src/Pitchside.Agents/Checking/CodeFactChecker.cs)). A model check of each sentence runs after it |
| It recovers from failure | A rejected draft is retried once, then replaced by a template written by code; a translation that fails falls back to the verified English. The template card airs first, so the broadcast never waits on a model |
| Agents outside Pitchside can use it | The MCP server exposes live match state, facts, moments, narratives and `explain_moment` as tools ([MCP.md](MCP.md)) |

### 3. Real-World Impact and Applicability

| Claim | Evidence |
| --- | --- |
| No rejected sentence reaches a screen | [Evaluation report for MER-7](evaluation/report-MER-7.md), a run on the Foundry deployments: 752 narratives from 34 moments and 1,412 model calls. The fact-checker verified 315 and rejected 184. The other 253 are fallbacks: a template written by code in place of a rejected text, or, for a translation that could not be verified, the verified English. Mean citation coverage 0.994 |
| It runs in production conditions | The demo is deployed on Azure Container Apps with Event Hubs, Cosmos DB, Foundry and Speech. Three consecutive demo loops on 23 October each had 1,537 events, 29 moments, 656 narratives written on Foundry and every card aired ([README, Evidence](../README.md#evidence)) |
| Analysts get the feed too | Microsoft Fabric Real-Time Intelligence: Eventstream from Event Hubs into a KQL database, a live dashboard and an Activator alert on sustained pressure. The KQL database's distinct event count (`MatchEventCount(...).events`; raw rows can include at-least-once redeliveries) matched the API's event count on every replay, and in the recorded runs the alert fired once per qualifying shot ([FABRIC.md, Evidence](FABRIC.md#evidence)) |
| Every audience is served | Four personas in four languages ([PERSONAS.md](PERSONAS.md)) |

### 4. User Experience and Presentation

| Claim | Evidence |
| --- | --- |
| The value is clear in the video | [The video](https://www.youtube.com/watch?v=KY_6y-mHiWQ), filmed on the live demo; its script and how it was made are in [VIDEO.md](VIDEO.md) |
| Three screens for three jobs | The viewer for fans, the control room for the producer and the overlay for air; every route passes a no-sideways-scroll check at 360, 768 and 1,280 px ([layout.spec.ts](../frontend/e2e/layout.spec.ts)) |
| A balance of frontend and backend | An Angular frontend with signals and SignalR, over a .NET 10 backend of match actors, agents, MCP and infrastructure as code ([ARCHITECTURE.md, Repository layout](ARCHITECTURE.md#repository-layout)) |

### 5. Best Enterprise Solution

The category asks for "the reliability, transparency, and controls required for use in production environments".

| Control | Evidence |
| --- | --- |
| Traceability | Every sentence links to the facts it cites and every fact to its events, in the control room and through `get_narrative` and `get_fact` over MCP |
| Audit | Every model call and every MCP call writes an audit record with its provider, model, prompt version, prompt hash, tokens and latency: `GET /api/v1/audit?matchId=<id>` on the demo |
| Human in the loop | Approve before air in the control room |
| Evaluation in the open | Foundry groundedness is scored for each verified narrative and shown on its card, or "pending" when the evaluation has not returned: 314 of the 315 verified narratives in the [report](evaluation/report-MER-7.md) have a score |
| Cost and abuse limits | A usage guard on every paid call: 6 replays per address per hour, 20 connections per address, 400 narrated moments and 60 spoken lines per hour ([CONFIGURATION.md](CONFIGURATION.md)) |
| No secrets | Keyless managed identity for every Azure service, Key Vault references only, local auth off on Event Hubs; gitleaks finds nothing in the whole history and the secret scan runs in CI |
| Infrastructure as code | Bicep and azd, separate dev and demo environments with one deploy identity each, deployed by a workflow over OIDC ([infra/](../infra/README.md)) |
| Observability | OpenTelemetry to Application Insights, with an availability test and alert rules ([infra/](../infra/README.md)) |

## Known limits

Stated here so a judge does not have to find them:

- **Casual and isiZulu variants often fall back.** On the demo loops of 23 October, 24 to 38 % of casual-persona
  variants were verified. When a persona variant is rejected, the viewer shows the moment's base card
  instead, in the chosen language when its translation verified; only when the translation itself fell back does it
  show the verified English, labelled "Shown in English". The checker rejects them rather than let an unsupported
  sentence air ([gap register](specs/IMPLEMENTATION-GAP.md#open-items)).
- **Later demo loops replay the narration.** The engine is deterministic, so every loop is the same match. From 26
  October a loop republishes the narratives the crew wrote for the same moment in the loop before, under its own
  ids, instead of calling the models again, after checking that each one renders to the same text on this loop's fact
  sheet. The control room shows such a narrative's provider as `Foundry replay`, and its audit records are the real
  calls that wrote it, with their original times. To see the crew write live, replay another seed from the home page.
- **Fabric runs on the dev environment**, which has the Event Hubs Standard tier it needs; the demo runs on Basic.
- **The Afrikaans and isiZulu text has not had a native-speaker review** ([LANGUAGE-REVIEW.md](LANGUAGE-REVIEW.md)).
