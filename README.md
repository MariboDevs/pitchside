# Pitchside

**Explainable match intelligence for broadcast, built on synthetic football data that is measured, not asserted.**

Pitchside is Maribo Devs' entry to the Microsoft and Premier League *Inside the Game* developer hackathon,
declared in the **Best Enterprise Solution** category. It ingests a live stream of synthetic match events,
turns them into statistics and patterns, explains why a moment matters, renders that as a timed overlay beside
the match, and personalises it for an analyst, a casual fan, a club fan and a player-focused viewer, in
several languages.

One rule shapes the whole system: **numbers come from code, words come from AI, and every word cites the
numbers it came from.** A narrative sentence that contains a number not on the fact sheet is rejected before
it reaches a screen.

## What is here today (8 October)

| Piece | State |
| --- | --- |
| Synthetic engine: fictional Meridian League, 20 clubs, seeded possession-level simulation | built |
| Calibration against real top-flight aggregates, 24 statistics, all within tolerance over 5,000 matches | built, see [the report](docs/calibration/report.md) |
| Event contract: `match.json` and `events.jsonl` with JSON Schemas and C# twins | built |
| Determinism, consistency and calibration gates in CI | built |
| API: a seeded match on demand, health probes, OpenTelemetry | first slice |
| Aspire AppHost | first slice |
| Match actors, SignalR, Event Hubs ingest | planned |
| Agent crew on Microsoft Agent Framework and Foundry | planned |
| Overlay timeline and Angular overlay, control room | planned |
| Personas and languages | planned |
| Bicep, azd, Container Apps, Cosmos DB, Key Vault | planned |

The full plan by day and owner is in [docs/PLAN.md](docs/PLAN.md).

## Why the data is different

No dataset is provided by the hackathon. Every entrant generates their own and most assert it is realistic.
Pitchside measures it. [`data/calibration/targets.json`](data/calibration/targets.json) holds league-level
averages from the English top flight, 2015/16 to 2025/26, 4,230 matches: goals, result split, shots, shots on
target, corners, fouls, cards, first-half share. The engine is tuned until a season-scale sample matches every
one of them within a stated tolerance, and CI fails if it drifts. Read [docs/CALIBRATION.md](docs/CALIBRATION.md)
for the method and what it does not claim.

Everything in the data is fictional. No real club, player, competition or venue appears anywhere.

## Run it

```bash
dotnet build Pitchside.slnx
dotnet test Pitchside.slnx

# one seeded match: match.json and events.jsonl
dotnet run --project src/Pitchside.Cli -- simulate --seed 7 --out out/seed-7

# the calibration report
dotnet run --project src/Pitchside.Cli -- calibrate --matches 5000

# the API: GET /api/v1/clubs, /api/v1/matches/7, /api/v1/matches/7/events, /health
dotnet run --project src/Pitchside.Api

# everything under Aspire (needs the Aspire workload or CLI on your machine)
dotnet run --project src/Pitchside.AppHost
```

A committed sample match lives in [`data/samples/seed-7`](data/samples/seed-7); CI regenerates it and diffs.

## Microsoft and Azure technologies

The integration matrix in [docs/MICROSOFT-INTEGRATION.md](docs/MICROSOFT-INTEGRATION.md) names what each
hero technology does in Pitchside and whether it is planned, built or deployed. It is kept honest: a row only
says built when a commit made it true.

## Documents

- [docs/HACKATHON.md](docs/HACKATHON.md): the rules distilled, criteria, dates, submission checklist
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): the five stages, the agent crew, enterprise controls, Azure topology
- [docs/EVENT-SCHEMA.md](docs/EVENT-SCHEMA.md): the event contract
- [docs/CALIBRATION.md](docs/CALIBRATION.md): how realism is measured
- [docs/PERSONAS.md](docs/PERSONAS.md): the viewer modes and their acceptance criteria
- [docs/PLAN.md](docs/PLAN.md): the day-by-day plan
- [docs/specs/](docs/specs/README.md): the build contract, one document per work package, and the gap register

## Team

| | Role |
| --- | --- |
| Sipho Maribo | Senior software architect: engine, agents, infrastructure |
| Pretty Kunene | Tester: evaluation harness, fact-check gate, golden masters, browser gates |
| Hlumela Tshazibana | Project manager and business analyst: personas, pitch, video, submission |

## How it is built

Pitchside is developed with GitHub Copilot agent mode and Claude Code alongside the team. Every design
decision, calibration target, tolerance and test is the team's. AI-assisted commits carry co-author trailers.

## License

MIT. See [LICENSE](LICENSE).
