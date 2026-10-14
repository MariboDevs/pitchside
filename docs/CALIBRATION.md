# Calibration: how we prove the synthetic data is football-realistic

The rules ask for "creativity and optimized synthetic data creation" and for data that is "football-realistic".
Most synthetic engines assert realism. Pitchside measures it.

## Targets

[`data/calibration/targets.json`](../data/calibration/targets.json) holds league-level averages computed from
football-data.co.uk results. The primary target is the English top flight, seasons 2015/16 to 2025/26, 4,230
matches. Only aggregates are stored; no match rows are redistributed and no real club or player appears
anywhere in this repository.

| Statistic | Target (home / away or total) |
| --- | --- |
| Goals per match | 2.82 (1.55 home, 1.27 away) |
| Result split | 44.2% home, 23.8% draw, 32.0% away |
| Over 2.5 goals | 54.2% |
| Both teams score | 52.6% |
| Goalless | 6.3% |
| First-half share of goals | 44.5% |
| Shots per team | 13.9 / 11.5 |
| Shots on target per team | 4.75 / 4.01 |
| Goals per shot | 0.111 |
| Goals per shot on target | 0.322 |
| Corners per team | 5.68 / 4.70 |
| Fouls per team | 10.6 / 10.9 |
| Yellow cards per team | 1.64 / 1.84 |
| Red cards per team | 0.055 / 0.064 |

## Method

The engine is a possession-level simulator. Each possession is a short Markov chain over pitch zones that ends
in a shot, a turnover, the ball going out, or a foul. Shot quality (xG) is sampled from a mixture that
reproduces the real goals-per-shot ratio; a goal is a Bernoulli draw on that xG. Rates are modulated by
club attack and defence ratings, home advantage, game state (a leading side shoots less, a trailing side
more), fatigue and substitutions, and by period (the second half carries more goals than the first).

## What the engine models, as of the first calibrated build

- Shot rate per possession by side, scaled by club attack over opponent defence, by a per-match tempo
  multiplier shared by both sides, and by each side's independent form-on-the-day multiplier. The two
  multipliers are what give real-world dispersion: without them the engine produced too few goalless games,
  too many draws and too many matches where both sides scored.
- Game state: a leading side's shot rate falls, a trailing side's rises, both by a few percent.
- Period: the second half carries more goals than the first, matching the measured 44.5% first-half share.
- Red cards: a side with a player sent off shoots less; its opponent shoots more.
- Shot quality: a three-part xG mixture whose mean reproduces goals per shot; a goal is a Bernoulli draw on xG.
- Corners: from blocked and saved shots and from the ball going out in the final third, scaled by attacking
  pressure so the side that attacks more wins more corners.
- Fouls and cards: a per-possession foul rate; a yellow per foul rate with a measured away-side tilt; a
  straight-red rate; referees are far less likely to book a player already on a yellow.
- Substitutions: three per side between minutes 58 and 86, outfield for outfield.
- Pass completion: a pass is incomplete with probability equal to its difficulty times a tuned scale; an
  incomplete pass is intercepted and hands the ball to the opponent. Measured against a literature value of
  82%, because football-data.co.uk does not carry passes.
- Penalties: a foul beyond x = 88 by the defending side is a penalty with probability 0.22, taken from the spot
  at a fixed xG of 0.76. Defenders foul far less inside their own box, which keeps penalties near the
  literature value of 0.25 per match.
- Player movement: tracking ticks every minute carry each player's distance (work rate plus carries) and top
  speed. These feed the stats layer and are not calibrated against a target.

Not yet modelled, and therefore not measured: fatigue within a half, injuries, own goals.

## The report

`dotnet run --project src/Pitchside.Cli -- calibrate --matches 5000` simulates a season-scale sample and
writes [`docs/calibration/report.md`](calibration/report.md): each statistic above, the synthetic value, the
target, the gap, and a pass or fail against the tolerance. The test suite runs the same comparison on a smaller
sample so a change that breaks realism fails the build.

## What this does not claim

Aggregate calibration does not prove that any single match is plausible, or that sequences of events look like
real sequences. Those are judged by the narrative and fact-check layers and by human review of sample matches.
The report says exactly what was measured.
