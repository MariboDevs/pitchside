# WP-06: Personas, languages, viewer preferences

| | |
| --- | --- |
| Branch | `feat/wp06-personas` |
| Depends on | WP-04, WP-05 |
| Owner | Sipho (prompts and tests), Hlumela (persona definitions and language review) |
| Effort | 2 days |
| Changes contracts | adds the optional `Narrative.subject` and the subject-qualified narrative id (WP-04's contract, spec updated); adds the viewer persona `all` for the control room (WP-03's hub) |

## Goal

The same verified intelligence reaches a data-hungry analyst and a casual fan as two genuinely different
experiences, plus the club fan and the player-focused viewer, in four languages. Every variant is cited and
checked exactly like the base narrative.

## Personas

Definitions, tone and acceptance criteria live in [`docs/PERSONAS.md`](../PERSONAS.md), owned by Hlumela.
This WP implements them.

| Persona | Prompt file | Length | Rules |
| --- | --- | --- | --- |
| `analyst` | `prompts/persona-analyst-v1.md` | 2 to 3 sentences | numbers first; name the pattern (momentum, control, pressing); compare to the match baseline (`match.*` facts) |
| `casual` | `prompts/persona-casual-v1.md` | 1 sentence, at most 18 words | plain words; at most one number; why it was exciting |
| `club_fan` | `prompts/persona-clubfan-v1.md` | 1 to 2 sentences | from the chosen club's view; named players; warmth allowed; never disparages the opponent (forbidden list extended with abusive terms) |
| `player_focus` | `prompts/persona-player-v1.md` | 1 to 2 sentences | only the followed player and players directly involved in the sequence; the player's facts; tags as badges in the UI, not in text |

The persona writer receives the verified base narrative, the same fact subset, and the viewer context
(`clubId` for club fan, `playerId` for player focus). It outputs the same sentences-with-citations JSON and
goes through `CodeFactChecker` and the agent check like the base.

## Languages

| Code | Language | Notes |
| --- | --- | --- |
| `en` | English | the base |
| `af` | Afrikaans | |
| `zu` | isiZulu | |
| `es` | Spanish | |

The translator receives the persona variant with its `[[key]]` tokens intact and must return the same tokens
in the same number; a token count mismatch is a rejection and falls back to English. Club and player names are
never translated. Numbers are rendered by code after translation, with locale formatting (`af` and `es` use a
decimal comma). The renderer fixes each language's separators itself rather than reading `CultureInfo`: the
operating system's culture data differs between machines and is absent in invariant-globalization mode, and the
same fact must render the same text everywhere.

## Behaviour

- **WP06-R1 Fan-out.** After the base narrative is verified, the workflow runs the persona writer for all
  four personas in parallel, then the translator for each persona into `af`, `zu`, `es` in parallel. Club
  fan and player focus are generated per club and per involved player on the moment only; a viewer following
  an uninvolved player sees the casual variant with a "not involved" state, never a fabricated sentence.
- **WP06-R2 Delivery.** The hub delivers to each connection only the variant matching its `ViewerPreferences`;
  the control room receives all variants. Variants are stored in `narratives` with their own ids.
- **WP06-R3 Latency.** The base narrative airs on the overlay as soon as verified; persona variants arrive to
  viewers as they complete, so the casual viewer may see their card a second after the analyst. The viewer
  shows a subtle "writing for you" state on the card slot until then, never a spinner.
- **WP06-R4 Language quality review.** Hlumela reviews a sample of 20 cards in `af` and `zu` from the seed-7
  match and records corrections in `docs/LANGUAGE-REVIEW.md`; corrections become prompt examples in v2 of
  the translator prompt.

## Files

- `src/Pitchside.Agents/prompts/persona-*.md`, `prompts/translator-v1.md`
- `src/Pitchside.Agents/Executors/PersonaExecutor.cs` and `TranslatorExecutor.cs` (filled in; the WP-04
  stubs exist)
- `src/Pitchside.Agents/Rendering/NarrativeRenderer.cs` gains per-language number formatting
- `tests/Pitchside.Agents.Tests/PersonaTests.cs` (scripted provider: four variants, all verified, lengths
  within limits, club fan never names a forbidden term), `TranslatorTests.cs` (token count preserved, names
  untranslated, mismatch falls back), `LocaleRenderingTests.cs`
- `frontend/e2e/personas.spec.ts`: switching persona and language changes the next card

## Done when

```bash
dotnet test Pitchside.slnx
dotnet run --project src/Pitchside.Cli -- narrate --seed 7 --provider scripted --personas all --languages en,af,zu,es | grep -c verified
cd frontend && npm run e2e
```
