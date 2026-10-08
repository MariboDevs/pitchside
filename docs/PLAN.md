# Plan

Hacking period 6 to 27 October 2026. Internal submission target 25 October. Owners: Sipho (architecture and
build), Pretty (quality and evidence), Hlumela (product, pitch and submission).

## Week 1: 8 to 14 October. Engine, stats, skeleton

| Day | Sipho | Pretty | Hlumela |
| --- | --- | --- | --- |
| 8 Oct | Repo scaffold, engine v0, calibration targets, CI | Review scaffold, set up local build | Register all three at aka.ms/insidethegame, read Official Rules |
| 9 Oct | Engine: squads, substitutions, cards, game-state effects | Golden-master tests for pinned seeds | Personas as user stories with acceptance criteria |
| 10 Oct | Stats layer: xG race, momentum, control vs chaos, pass difficulty | Calibration gate in CI, tolerance decisions | Pitch v1: problem, who it is for, why enterprise |
| 11 Oct | Match actor, SignalR, Event Hubs publisher | Schema validation tests against sample match | Video storyboard v1 |
| 12 Oct | Aspire AppHost, Container Apps Bicep, azd up to a dev environment | Smoke test deployed API | Adoption path: who buys this, what it replaces |
| 13 Oct | Agent Framework spike: one agent end to end on Foundry | Evaluation harness design | Review week 1, cut scope |
| 14 Oct | Reactor session (AKS and ACA), MCP server | Playwright skeleton against the overlay | Submission form fields drafted |

## Week 2: 15 to 21 October. Crew, overlay, personas

| Day | Sipho | Pretty | Hlumela |
| --- | --- | --- | --- |
| 15 Oct | Moment detector and explainer agents | Fact-check gate tests | Persona copy review |
| 16 Oct | Fact-checker and overlay scheduler | Groundedness evaluation runs on Foundry | Video script v1 |
| 17 Oct | Overlay timeline and Angular overlay | UI invariants and Playwright on overlay | Enterprise narrative for README |
| 18 Oct | Persona writer and translator | Persona acceptance tests | Languages: review Afrikaans and isiZulu output |
| 19 Oct | Control room view, traceability chain in UI | Usage guard tests | Pitch v2 |
| 20 Oct | Registration deadline. Cosmos DB, Speech recap | Deployed demo regression | Confirm registration complete for all three |
| 21 Oct | Hardening, scale-to-zero, cost caps | Full regression on deployed demo | Video shot list final |

## Week 3: 22 to 25 October. Polish, evidence, submit

| Day | Sipho | Pretty | Hlumela |
| --- | --- | --- | --- |
| 22 Oct | Bug fixes only | Calibration report and evaluation numbers into README | Record video takes |
| 23 Oct | Bug fixes only | Final regression, judge-as-user walkthrough | Edit video, under two minutes |
| 24 Oct | Freeze | Verify demo from a clean machine and a phone | Submission dry run |
| 25 Oct | Repo public, submit | Confirm hosted demo up, credentials in form | Submit |

26 and 27 October are buffer. Nothing new lands after 25 October except a fix to something the judges would
see broken.

## Out of scope, decided

- Auto-eventing from video. No footage, no time, and the rules do not require it.
- Fabric Real-Time Intelligence, unless week 1 lands cleanly by 13 October.
- Mobile apps. The overlay and control room are responsive web.
