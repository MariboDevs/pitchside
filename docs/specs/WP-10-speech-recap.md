# WP-10: Spoken full-time recap

| | |
| --- | --- |
| Branch | `feat/wp10-speech` |
| Depends on | WP-04, WP-06 |
| Owner | Sipho (implemented by Claude Code) |
| Effort | 1 day |
| Changes contracts | none |

## Goal

At full time, the recap is spoken in the viewer's language by a neural voice, so the product shows a studio
and streaming use case beyond text, and the multi-language story becomes audible in the video.

## Behaviour

- **WP10-R1.** When the `full_time` moment's base narrative and its persona and language variants are
  verified, the recap executor assembles the analyst variant into SSML: a 0.4 s pause between sentences,
  club and player names wrapped in `<sub>` with a phonetic alias only where a committed dictionary provides
  one (`prompts/pronunciation.json`), rate `+0%`, and a `<prosody>` lift on the sentence containing a goal.
- **WP10-R2.** Azure AI Speech synthesises to MP3 per language with a voice table in configuration: `en` and
  `af` and `zu` and `es` each map to a named neural voice; if a language has no configured voice, or its
  translation fell back to English, the English audio is used and the UI says so. The call goes to the regional
  REST endpoint with an Entra ID token (`aad#{resourceId}#{token}`) rather than through the
  `Microsoft.CognitiveServices.Speech` SDK: the same service and voices, no native library in the container, and
  the request is testable offline.
- **WP10-R3.** Audio is stored in Blob (`recaps/{matchId}/{language}.mp3`, with the recap beside it as
  `{language}.json`) and served by the API: `GET /api/v1/matches/{id}/recap?language=` gives the recap with its
  SSML and `/recap/audio?language=` streams the MP3, read with the API's identity. No SAS is issued, so no
  shared-key or delegation credential leaves the API. The viewer shows a player; the control room shows the SSML.
- **WP10-R4.** Usage guard: spoken lines per hour from WP-07; `Speech:Provider=None` disables synthesis and
  the endpoint returns 404 with a problem detail saying speech is off.
- **WP10-R5.** A scripted provider writes a silent 1-second MP3 so tests and CI exercise the path.

## Files

- `src/Pitchside.Agents/Executors/RecapExecutor.cs`, `Speech/ISpeechProvider.cs`,
  `AzureSpeechProvider.cs`, `ScriptedSpeechProvider.cs`, `Speech/SsmlBuilder.cs`, `Speech/RecapWriter.cs`,
  `prompts/pronunciation.json`
- `src/Pitchside.Contracts/Recap.cs`, `schemas/recap.schema.json`, `src/Pitchside.Runtime/Recaps/` (the in-process
  and Blob recap stores)
- `src/Pitchside.Api/Endpoints/RecapEndpoints.cs`
- `tests/Pitchside.Agents.Tests/SsmlBuilderTests.cs`, `RecapTests.cs`

## Configuration

| Key | Default |
| --- | --- |
| `Speech:Provider` | `None` |
| `Speech:Region`, `Speech:ResourceId` | identity-based auth |
| `Speech:Voices:en`, `:af`, `:zu`, `:es` | voice names from the Speech voice gallery; verify availability in the region |
| `Speech:BlobServiceUri` | unset keeps recaps in process |
| `Speech:BlobContainer` | `recaps` |

## Done when

```bash
dotnet test Pitchside.slnx
dotnet run --project src/Pitchside.Cli -- narrate --seed 7 --provider scripted --recap --speech scripted
ls out/seed-7/recap/   # en.mp3 af.mp3 zu.mp3 es.mp3
```
