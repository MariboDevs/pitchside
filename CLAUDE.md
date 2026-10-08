# Pitchside: project rules

Pitchside is Maribo Devs' entry to the Microsoft and Premier League "Inside the Game" developer hackathon.
Read [docs/HACKATHON.md](docs/HACKATHON.md) first, then [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Non-negotiables

1. **Synthetic only, fictional only.** No real club, player, competition or venue name anywhere: code, data,
   tests, UI copy, docs, commits, video. The fictional league is the Meridian League; clubs and players are
   generated. `data/calibration/targets.json` holds league-level aggregates and nothing else.
2. **No betting, no odds.** Nothing in this repository references wagering, odds or bookmakers. Win
   probability is a broadcast statistic and is the only probability shown to a viewer.
3. **No secrets in git, ever.** The whole history goes public on 25 October. User-secrets locally, Key Vault
   references in Bicep, managed identity in Azure. `.gitignore` is authoritative.
4. **Numbers from code, words from AI.** No model call may produce a number a viewer sees. Every narrative
   sentence cites the fact-sheet ids it was written from, and the fact-checker rejects an uncited number.
5. **Deterministic engine.** The same seed produces the same match on every machine and every .NET patch.
   Pinned seeds are golden masters in `tests/`; an unexpected diff is a bug, a deliberate change updates the
   master in a reviewed pull request.
6. **No trademarks in the video or the UI.** Not the Premier League's, not a club's, not a broadcaster's.

## Stack

| Layer | Choice |
| --- | --- |
| Runtime | .NET 10, warnings as errors, central package management |
| Orchestration | .NET Aspire (`src/Pitchside.AppHost`) |
| Agents | Microsoft Agent Framework (.NET) on Microsoft Foundry |
| Real time | SignalR to the UI, Azure Event Hubs between services |
| State | Azure Cosmos DB; Akka.NET actor per live match |
| Frontend | Angular, standalone, signals, Tailwind; three files per component |
| Tests | xUnit, Shouldly; Playwright and ui-invariants for the overlay |
| Infra | Bicep and azd to Azure Container Apps |

## Build and test

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build Pitchside.slnx
dotnet test Pitchside.slnx
dotnet run --project src/Pitchside.Cli -- simulate --seed 7 --out out/seed-7
dotnet run --project src/Pitchside.Cli -- calibrate --matches 2000
```

All code builds with zero warnings. All tests pass before a commit. One type per file, file-scoped namespaces,
no empty catch blocks, no task markers in committed code, English only, no em dashes in prose.

## Git

- `main` is protected. Work on `feat/*`, `fix/*`, `docs/*`; pull requests into `main`; Copilot code review runs
  on every pull request.
- Conventional commits: `feat(engine): ...`, `test(stats): ...`, `docs: ...`.
- AI-assisted development is disclosed in the README. Co-author trailers stay on.

## Where things are decided

- Judging criteria and constraints: `docs/HACKATHON.md`
- What each Microsoft technology does here: `docs/MICROSOFT-INTEGRATION.md`
- Event contract: `docs/EVENT-SCHEMA.md` and `schemas/`
- Realism evidence: `docs/CALIBRATION.md` and `docs/calibration/report.md`
- Who does what, by day: `docs/PLAN.md`
