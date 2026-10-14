# Implementation gap register

What is built against the specifications, and the evidence. A row closes when the "Done when" block of its WP
passed on `main` and the evidence is linked. Update this file in the pull request that lands the work.

| WP | Status | Pull request | Evidence |
| --- | --- | --- | --- |
| Scaffold | done | initial commits on `main` | 32 tests, calibration report at 5,000 matches all within tolerance, sample `data/samples/seed-7` validates |
| WP-00 | merged | [#6](https://github.com/MariboDevs/pitchside/pull/6) | Schema 0.2: incomplete passes, penalties, tracking ticks, `workRate`. 54 tests pass (`ConsistencyTests.An_incomplete_pass_is_always_followed_by_a_turnover_by_the_same_side`, `A_penalty_shot_has_the_penalty_flag_spot_and_xg`, `A_penalty_is_a_possession_of_one_shot_after_a_foul_in_the_final_third`, `Tracking_ticks_are_a_minute_apart_per_side_and_list_exactly_the_players_on_the_pitch`, `SchemaValidationTests`). `calibrate --matches 5000`: all 26 statistics within tolerance (pass completion 0.820, penalties 0.271 per match). Sample `seed-7` regenerated (1,623 events) and reproduces byte for byte. Needs a local session: none |
| WP-01 | in review | `feat/wp01-stats` | `Pitchside.Stats` with 24 tests: `IncrementalEqualsBatchTests` (incremental equals the independent `BatchStats` oracle at full time and along the sample, tags included), `MetricDefinitionTests` (hand-computed values on a 20-event match), `TagTests`, `FactSheetSchemaTests`, `PerformanceTests` (sample snapshotted at every event in under 2 s). `facts --seed 7` prints 526 facts. Needs a local session: none |
| WP-02 | not started | | |
| WP-03 | not started | | |
| WP-04 | not started | | |
| WP-05 | not started | | |
| WP-06 | not started | | |
| WP-07 | not started | | |
| WP-08 | not started | | |
| WP-09 | not started | | |
| WP-10 | not started | | |
| WP-11 | not started | | |
| WP-12 | not started | | |
