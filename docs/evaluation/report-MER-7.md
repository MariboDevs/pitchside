# Evaluation report: MER-7

Agents provider `foundry`, evaluation provider `foundry`. 752 narratives from 34 moments; 1412 model calls.

## Narratives by status

| Status | Count | Share |
| --- | ---: | ---: |
| draft | 0 | 0.0 % |
| verified | 315 | 41.9 % |
| rejected | 184 | 24.5 % |
| fallback | 253 | 33.6 % |

## Quality

| Measure | Value |
| --- | ---: |
| Narratives scored for groundedness | 314 |
| Mean groundedness | 0.702 |
| Minimum groundedness | 0.500 |
| Mean citation coverage | 0.994 |
| Mean fact density (tokens per sentence) | 2.941 |
| Fallback rate | 33.6 % |

## Latency per stage

| Stage | Calls | p50 ms | p95 ms |
| --- | ---: | ---: | ---: |
| explainer | 42 | 2068 | 3672 |
| factcheck | 378 | 3779 | 8486 |
| groundedness | 315 | 4117 | 6022 |
| persona-analyst | 58 | 2461 | 3150 |
| persona-casual | 64 | 1762 | 2865 |
| persona-clubfan | 102 | 2058 | 4003 |
| persona-player | 27 | 2981 | 4015 |
| translator | 426 | 3566 | 4111 |

## Tokens and cost

| Measure | Value |
| --- | ---: |
| Input tokens | 1397567 |
| Output tokens | 112604 |
| Tokens per narrative | 2008.206 |
| Estimated cost at the configured prices | 0.0000 |
