# Demo video: script and shot list

The submission video: under two minutes, on YouTube or Vimeo, no third-party trademarks and no copyrighted music
(`docs/HACKATHON.md`). Recorded on Sunday 25 October in the afternoon, edited and uploaded on Monday 26 October.
Everything filmed is the hosted demo the judges will open, so what the video shows is what they get.

## Before recording

1. **Clean screen.** A fresh browser profile with no bookmarks bar, no other tabs, no extensions in view and no
   mail or chat windows. Nothing on screen but Pitchside, the Azure portal, the Aspire dashboard and Fabric.
2. **Start a fresh loop.** The demo plays `MER-26` (Thornbury FC 2, Meridian Wanderers 2) on repeat, about
   1 hour 53 minutes per loop: 47 minutes, a 15-minute break, 48 minutes, then a 2-minute pause. Ask a local
   session to restart the demo API just before recording; the next loop (`MER-26-<n>`) kicks off within about
   15 seconds, so every moment below falls at a known time.
3. **Open the three screens** for the new match id: the viewer (`/viewer/<matchId>`), the control room
   (`/control/<matchId>`) and the broadcast overlay (`/overlay/<matchId>`) on the demo web app.
4. **Use the jump button.** It runs the match ahead to ten seconds before the next moment, so the video needs no
   waiting. Each shot below names the moment to jump to.
5. **Audio.** Record the voice-over separately in a quiet room; no music, or only music you made or that is
   licensed for it.

## Moments to film

From `docs/DEMO.md`. "After kick-off" is real time on a fresh loop, with the 15-minute break included.

| Moment | Match clock | After kick-off |
| --- | --- | --- |
| Kick-off | 1H 00:00 | 0:00 |
| Goal, Meridian Wanderers 0-1 | 1H 07:18 | 7:18 |
| Goal, Thornbury FC 1-1 | 1H 12:00 | 12:00 |
| Goal, Thornbury FC 2-1 | 1H 31:36 | 31:36 |
| Half time | 1H 47:02 | 47:02 |
| Big chance missed, Milan Montero | 2H 09:38 | 71:40 |
| Penalty and goal, Meridian Wanderers 2-2 | 2H 22:57 | 84:59 |
| Full time and the spoken recap | 2H 48:19 | 110:21 |

## Script (about 115 seconds)

The voice-over says no number of its own: every figure it mentions is on screen, from the overlay or from
`docs/evaluation/report-MER-7.md`, so it can be checked.

| Time | Shot | Voice-over |
| --- | --- | --- |
| 0:00 to 0:10 | Viewer, live match, the kick-off card appears beside the pitch | "This is Pitchside: live match intelligence for a broadcast, explained as it happens." |
| 0:10 to 0:30 | Jump to the first goal. The goal card appears in the viewer; cut to the control room and click a cited value in its narrative so the fact it came from lights up | "When a goal goes in, the numbers come from code and the words come from AI. Every sentence cites the facts it was written from, and a fact-checker rejects any number that is not on the fact sheet." |
| 0:30 to 0:45 | Switch the persona from analyst to casual fan, then the language to isiZulu and Afrikaans | "The same moment is written for an analyst, a casual fan, a club fan or a player-focused viewer, in English, Afrikaans, isiZulu and Spanish." |
| 0:45 to 1:00 | Control room: switch to "Approve before air", hold a card, then air it; show its verdict panel (verdict, provider, model, prompt, groundedness) and the timeline of its model calls | "In the control room the producer holds or airs each card, and every card shows its verdict, the model that wrote it, its groundedness score and every call behind it." |
| 1:00 to 1:20 | Azure portal: the Container Apps, Event Hubs and Foundry deployments; then the Aspire trace of one moment through the agent crew | "Under the hood: Container Apps, Event Hubs and Cosmos DB carry the feed, and a Microsoft Agent Framework crew on Foundry writes, checks and schedules every card." |
| 1:20 to 1:35 | Fabric (the dev environment, which has the feed; demo has none): the Pitchside Live dashboard for `MER-13`, then the pressure alert in Activator | "The same feed lands in Fabric, where an analyst can query any match in KQL and an alert fires when a side builds sustained pressure." |
| 1:35 to 1:45 | Jump to the penalty: the momentum swing and the equaliser card | "Momentum, pressure and big chances are detected by rules, so the overlay and the dashboard always agree." |
| 1:45 to 1:55 | Full time: the spoken recap plays in the viewer's language (Azure AI Speech; film it only once the WP-10 run against the real Speech resource has passed, otherwise cut this shot and give its time to the control room) | "At full time, the recap is spoken in the viewer's own language." |
| 1:55 to 2:00 | Title card: Pitchside, Maribo Devs, the repository URL | "Explainable match intelligence. Measured, not asserted." |

## Checks before upload

- Length under 2:00, including the title card.
- No real club, player, league or broadcaster name, said or shown; the clubs are the fictional Meridian League.
- No logo or trademark other than the Microsoft products being demonstrated; no third-party music.
- No email address, subscription id, key or personal notification visible in any frame.
- Upload to YouTube or Vimeo as public or unlisted, and paste the URL into the submission form.
