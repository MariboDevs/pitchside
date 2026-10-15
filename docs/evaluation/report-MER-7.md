# Evaluation report: MER-7

Agents provider `scripted`, evaluation provider `local`. 752 narratives from 34 moments; 1504 model calls.

## Narratives by status

| Status | Count | Share |
| --- | ---: | ---: |
| draft | 0 | 0.0 % |
| verified | 752 | 100.0 % |
| rejected | 0 | 0.0 % |
| fallback | 0 | 0.0 % |

## Quality

| Measure | Value |
| --- | ---: |
| Narratives scored for groundedness | 752 |
| Mean groundedness | 0.173 |
| Minimum groundedness | 0.000 |
| Mean citation coverage | 1.000 |
| Mean fact density (tokens per sentence) | 1.771 |
| Fallback rate | 0.0 % |

Groundedness here is the local proxy: the share of a narrative's sentences that cite only the moment's
evidence keys. The crew may also cite `match.*` facts and the side's and players' facts, so this measures how
closely a narrative keeps to its moment, not whether its numbers are true; citation coverage shows that every
number resolves. The Foundry judge scores groundedness against every fact the narrative cites.

## Latency per stage

| Stage | Calls | p50 ms | p95 ms |
| --- | ---: | ---: | ---: |
| explainer | 34 | 0 | 0 |
| factcheck | 752 | 0 | 0 |
| persona-analyst | 34 | 0 | 0 |
| persona-casual | 34 | 0 | 0 |
| persona-clubfan | 68 | 0 | 0 |
| persona-player | 18 | 0 | 0 |
| translator | 564 | 0 | 0 |

## Tokens and cost

| Measure | Value |
| --- | ---: |
| Input tokens | 0 |
| Output tokens | 0 |
| Tokens per narrative | 0.000 |
| Estimated cost at the configured prices | 0.0000 |
