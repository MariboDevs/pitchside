# Personas

The rules require that "the same intelligence reaches a data-hungry analyst and a casual fan as two genuinely
different experiences". These are the viewer modes Pitchside ships. Hlumela owns this file: each persona is a
user story with acceptance criteria that Pretty tests against.

| Persona | Who | What they want from a moment | Example output for the same goal |
| --- | --- | --- | --- |
| Analyst | Studio pundit, club analyst | Numbers first, the pattern behind the moment, comparison to match baseline | "Third shot from the left half-space in six minutes; xG 0.31 against a first-half average of 0.07. The press has moved the away block ten metres deeper." |
| Casual fan | Watching with friends, second screen | One sentence, plain words, why it was exciting | "That is the goal the whole second half was building towards." |
| Club fan | Supports one of the two clubs | The moment through their club's eyes, named players, emotion allowed, never abusive | "Nkosi finally gets the reward for forty minutes of running the channel." |
| Player focus | Follows one player | Everything that player did in the sequence, speed and distance tags, their contribution to the moment | "Dlamini: 32.4 km/h recovery run, then the pass that broke the line, 24 m, difficulty 0.78." |

## Languages

English, Afrikaans, isiZulu and Spanish in the demo. The translator agent falls back to English when a
language model is unavailable, and the UI says so rather than showing a blank.

## Acceptance criteria per persona

- The same moment produces four different texts, each grounded in the same fact sheet ids.
- No text contains a number that is not on the fact sheet.
- Club-fan text never disparages the opposition.
- Player-focus text names only the followed player and the players directly involved in the sequence.
- Switching persona or language mid-match takes effect on the next moment without a reload.
