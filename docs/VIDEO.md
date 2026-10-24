# Demo video: script and shot list

The submission video: under two minutes, on YouTube or Vimeo, no third-party trademarks and no copyrighted music
(`docs/HACKATHON.md`). Everything filmed is the hosted demo the judges will open, so what the video shows is what
they get.

**Published:** [Pitchside: explainable match intelligence](https://www.youtube.com/watch?v=KY_6y-mHiWQ), public on
YouTube, 1:53, uploaded on 24 October 2026.

How it was made:

- **Footage** of the demo web app, filmed on loop `MER-26-170` at 1440p from a scripted browser.
- **Animated scenes** (the opening pitch, architecture, MCP call, Fabric, production board and end card) drawn in
  HTML and rendered frame by frame. The MCP scene shows a real `explain_moment` call against the demo.
- **Narration** synthesised with Azure AI Speech, an HD neural voice at about 139 words a minute. It is disclosed
  as AI-generated on YouTube.
- **Music** composed and synthesised for the video, so it carries no third-party copyright.
- **The recap excerpt** is the demo's own spoken full-time recap, in Afrikaans.

## Before recording

1. **Clean screen.** A fresh browser profile with no bookmarks bar, no other tabs, no extensions in view and no
   mail or chat windows. Nothing on screen but Pitchside, the Azure portal, the Aspire dashboard and Fabric.
2. **Start a fresh loop.** The demo plays `MER-26` (Thornbury FC 2, Meridian Wanderers 2) on repeat at ten
   times real time (`demoSpeed`), about 13 minutes per loop: 5 minutes, a 90-second break, 5 minutes, then a
   2-minute pause. Ask a local
   session to restart the demo API just before recording; the next loop (`MER-26-<n>`) kicks off within about
   15 seconds, so every moment below falls at a known time.
3. **Open the three screens** for the new match id: the viewer (`/viewer/<matchId>`), the control room
   (`/control/<matchId>`) and the broadcast overlay (`/overlay/<matchId>`) on the demo web app.
   In the viewer, choose **Club fan** and Thornbury FC in English before recording: it opens on Casual, the
   persona whose cards are rejected most often, and it remembers the choice.
4. **Use the jump button.** It runs the match ahead to ten seconds before the next moment, so the video needs no
   waiting. Each shot below names the moment to jump to.
5. **Audio.** Record the voice-over separately in a quiet room; no music, or only music you made or that is
   licensed for it.

## Moments to film

From `docs/DEMO.md`. "After kick-off" is real time on a fresh loop at speed 10, with the 90-second break included.

| Moment | Match clock | After kick-off |
| --- | --- | --- |
| Kick-off | 1H 00:00 | 0:00 |
| Goal, Meridian Wanderers 0-1 | 1H 07:18 | 0:44 |
| Goal, Thornbury FC 1-1 | 1H 12:00 | 1:12 |
| Goal, Thornbury FC 2-1 | 1H 31:36 | 3:10 |
| Half time | 1H 47:02 | 4:42 |
| Big chance missed, Milan Montero | 2H 09:38 | 7:10 |
| Penalty and goal, Meridian Wanderers 2-2 | 2H 22:57 | 8:30 |
| Full time and the spoken recap | 2H 48:19 | 11:02 |

## Script (as published, 113 seconds)

The voice-over says no number of its own: every figure it mentions is on screen or in a generated report, so it
can be checked.

| Time | Shot | Voice-over |
| --- | --- | --- |
| 0:00 to 0:12 | Animated pitch: the event counter runs to 1,537 | "Every live match throws off more than fifteen hundred events. Counting them is easy. Explaining them live, without a single wrong number, is the hard part." |
| 0:12 to 0:14 | Name card | "This is Pitchside." |
| 0:14 to 0:24 | Viewer: the "Goal: Thornbury FC 1-1" card arrives; control room: its cited values are hovered one by one | "When a goal goes in, the numbers come from code. And the words come from AI. Every sentence cites the facts behind it." |
| 0:24 to 0:35 | Viewer: player focus on a goal scorer, then Afrikaans and Spanish | "The same moment is rewritten for whoever is watching, from the analyst to the club fan. In English, Afrikaans, isiZulu, and Spanish." |
| 0:35 to 0:45 | Control room: "Approve before air", a held card opened and aired, back to "Auto-air" | "In the control room, the producer stays in charge. Hold a card, or air it. And see exactly how it was made." |
| 0:45 to 0:58 | Animated architecture: Event Hubs, Container Apps, Cosmos DB and the agent crew on Foundry | "Under the hood, Event Hubs, Container Apps and Cosmos DB carry the feed. A Microsoft Agent Framework crew, on Foundry, writes each card, checks it, and schedules it for air." |
| 0:58 to 1:09 | The MCP tool list and a real `explain_moment` answer: verified, citation coverage 1 | "And Pitchside speaks MCP. Copilot, or any other agent, can ask a live match what just happened, and get the same verified answer." |
| 1:09 to 1:19 | Fabric: the Real-Time Dashboard, the Activator rule history and the alert email | "The same feed lands in Microsoft Fabric. Any match can be queried. And an alert fires when a side builds sustained pressure." |
| 1:19 to 1:27 | Overlay: "Momentum swings to Thornbury FC" lower third | "Momentum and pressure come from rules, not guesses. So the overlay and the dashboard always agree." |
| 1:27 to 1:38 | Production board: calibration, fact-check verdicts, no secrets, live alerts, usage limits, seeded engine | "And it's built for production. Calibrated against real league averages. Fact-checked before air. And not a single secret, anywhere." |
| 1:38 to 1:47 | Viewer: the full-time recap, then its own audio in Afrikaans | "And at full time... the recap is spoken in the viewer's own language." |
| 1:47 to 1:53 | End card: Pitchside, Black Matter Labs, the repository URL | "Pitchside. Explainable match intelligence. Measured, not asserted." |

## Checks before upload

- Length under 2:00, including the title card: the script ends at 1:55, which leaves five seconds of margin.
- No real club, player, league or broadcaster name, said or shown; the clubs are the fictional Meridian League.
- No logo or trademark other than the Microsoft products being demonstrated; no third-party music.
- No email address, subscription id, key or personal notification visible in any frame.
- Upload to YouTube or Vimeo as public or unlisted, and paste the URL into the submission form.
- Play it signed out in a private window: length, audio and HD all as expected.
