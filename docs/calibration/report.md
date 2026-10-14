# Calibration report

Generated 2026-10-14 from 5,000 simulated matches, compared to target set `E0_2015_2025`.

Result: **all statistics within tolerance**.

| Statistic | Synthetic | Target | Gap | Tolerance | Verdict |
| --- | ---: | ---: | ---: | ---: | --- |
| goals_per_match | 2.862 | 2.823 | +0.039 | 0.150 | pass |
| home_goals | 1.579 | 1.549 | +0.030 | 0.120 | pass |
| away_goals | 1.284 | 1.274 | +0.010 | 0.120 | pass |
| p_home | 0.441 | 0.442 | -0.001 | 0.030 | pass |
| p_draw | 0.242 | 0.238 | +0.004 | 0.030 | pass |
| p_away | 0.317 | 0.320 | -0.003 | 0.030 | pass |
| p_over25 | 0.531 | 0.542 | -0.011 | 0.040 | pass |
| p_btts | 0.555 | 0.526 | +0.029 | 0.040 | pass |
| p_0_0 | 0.065 | 0.063 | +0.002 | 0.020 | pass |
| first_half_goal_share | 0.445 | 0.445 | 0.000 | 0.030 | pass |
| home_shots | 13.955 | 13.922 | +0.033 | 1.000 | pass |
| away_shots | 11.738 | 11.488 | +0.250 | 1.000 | pass |
| home_sot | 4.753 | 4.754 | -0.001 | 0.500 | pass |
| away_sot | 4.002 | 4.012 | -0.010 | 0.500 | pass |
| goals_per_shot | 0.111 | 0.111 | 0.000 | 0.015 | pass |
| goals_per_sot | 0.327 | 0.322 | +0.005 | 0.040 | pass |
| home_corners | 5.936 | 5.681 | +0.255 | 0.600 | pass |
| away_corners | 4.795 | 4.703 | +0.092 | 0.600 | pass |
| home_fouls | 10.800 | 10.555 | +0.245 | 1.200 | pass |
| away_fouls | 10.426 | 10.932 | -0.506 | 1.200 | pass |
| home_yellows | 1.642 | 1.638 | +0.004 | 0.300 | pass |
| away_yellows | 1.729 | 1.835 | -0.106 | 0.300 | pass |
| home_reds | 0.060 | 0.055 | +0.005 | 0.030 | pass |
| away_reds | 0.055 | 0.064 | -0.009 | 0.030 | pass |
| pass_completion | 0.820 | 0.820 | 0.000 | 0.020 | pass |
| penalties_per_match | 0.271 | 0.250 | +0.021 | 0.080 | pass |

Targets are league-level averages from football-data.co.uk (English top flight, 2015/16 to 2025/26), except
`pass_completion` and `penalties_per_match`, which are literature values because football-data.co.uk does not
carry passes or penalties. See docs/CALIBRATION.md.
