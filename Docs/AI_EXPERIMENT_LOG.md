# AI and Bot Experiment Log

Updated 2026-11-19. This is the living evidence log for supervisor reviews and the final report. Raw runs remain under `SimulationResults`; this file records why each experiment exists and what conclusion is justified.

## Maintenance rule

- Append an entry after every material rule, card, bot, dataset or model experiment; update it before the related checkpoint commit.
- Preserve earlier entries and negative results. Correct mistakes with a dated correction instead of silently rewriting history.
- Keep commit, config, seeds, schema and raw artifact paths in each entry so another session can reproduce the claim.
- Never delete or replace this log during handoff cleanup. Keep `Docs/HANDOFF.md` pointing to it.

## What to present

| System | Method | What makes it distinct | Evidence to show |
|---|---|---|---|
| Easy | Priority-rule baseline | Chooses legal actions with deliberately simple priorities and no search simulation | `Assets/Scripts/Core/EasyBot.cs`; Easy-Normal-Hard round-robin summary |
| Normal | Heuristic/forecast policy | Scores public board/card context and uses a bounded local attack forecast (historically depth 5, cap 256 nodes) | `Assets/Scripts/Bots/NormalBot.cs`; action-score examples and paired match results |
| Hard | Information-set search hybrid | Independently selects through bounded information-set tree search, tactical filters, rollouts and strategic leaf evaluation. Normal's heuristic is reused only as rollout guidance, opponent policy and explicit fallback; Hard is not a deeper copy of Normal. | `HardBot.cs`, `HardTacticalPolicy.cs`, `HardStrategicEvaluation.cs`; search diagnostics and paired seeds |
| Learned AI | Deep imitation policy | Neural model scores the current legal action candidates from a versioned observation; future self-play is a separate phase | AI contract, replay-verified dataset manifests, train/validation/test curves and held-out match evaluation |

Do not present only folders of CSV/JSON. Use one summary table, one small chart, one representative replay and links to raw artifacts. Raw files prove reproducibility; the summary explains the research result.

## Retrospective - hand-coded bot development, 2026-11-16 to 2026-11-17

- Easy, Normal and Hard solve the same legal-action game interface but use materially different policies: priority rules, heuristic/local forecast, and bounded information-set search respectively. Shared `GameRules.ApplyAction` guarantees legality; it does not make their decision algorithms identical.
- H0 introduced paired-root information-set search with 96 iterations, depth 6 and guided rollouts; it beat Easy 99/120. H1 added defense/tribute/support guards but overconstrained joint plans and regressed to 89/120.
- H2 added shared Energy commitments, staged Main preparation, softer risk and printed-cost feasibility; it recovered to 101/120 against Easy. H3 fixed prospective passive evaluation and added hand/board portfolio scoring; selected regressions improved, while the first full H3-Normal trial was 72/120 (60%).
- H4 tested draw/exposure-aware survival and scored 73/120 against Normal; H4.1 selective commitment scored 70/120. H5 joint-attack planning reached 76/120 on development seeds, but fell to 68/120 on holdout while frozen H3 scored 74/120 there. This holdout regression prevented treating the development gain as general progress.
- Four targeted H5 variants scored 7, 9, 7 and 9 wins in 16 discordant cases; H5.2 (`legacy-fallback`) was retained for the broad comparison.
- Final local round-robin used 60 holdout seeds and both seats per matchup: Normal beat Easy 92-28, Hard beat Easy 100-20, and Hard beat Normal 73-47. This supports the measured ordering Easy < Normal < Hard on this protocol, not a claim that a stronger bot must win every game.
- H4-H5.2 summaries are preserved local diagnostics marked `replayVerified=false` and `trainingEligible=false`; they support bot-development discussion but are not imitation-learning inputs.

## Retrospective - learned-policy pipeline before rules v6

- Replay/state hashes and the public-policy AI contract established reproducible state-action records without opponent hand, true deck order or seed leakage. Expansion pilots observed all 41 public card IDs; coverage did not prove mastery of every effect.
- The first Normal smoke dataset retained 878 train/246 validation choices after forced-choice filtering. A 64/32 MLP reached validation loss 1.272, top-1 52.4% and top-3 87.4%; there was no independent test.
- A two-seed Hard-teacher pilot verified the teacher/data path but was intentionally too small for strength claims. It exposed that imitation can inherit teacher blind spots.
- The rules-v4 main collection produced 280 episodes/43,297 transitions and three teacher ablations. Its held-out loss exposed a single 94-turn out-of-distribution game, motivating explicit long-state diagnosis rather than hiding the failure.
- Rules v5 corrected first-player draw advantage; rules v6 added escalating end-turn deck exhaustion and AI schema 3. Older datasets/checkpoints remain versioned baselines and were not silently mixed into the rules-v6 collection.

## Current milestones

- Rules v6 accepted and pushed in commit `41057b2`: the first player skips only their first draw; failed later normal draws schedule escalating `n * 100` LP damage at that player's End step.
- AI contract is rules/catalog/schema `6/4/3`: 25 global features, including both players' exhaustion level and pending damage. Reward remains win `+1`, loss `-1`, ongoing/draw `0`.
- Frozen collection `hard_normal_main_v2_41057b2`: 14 shards, 280 Hard-Normal matches, five equal-deck matchups and both seats. Seeds are train `610101-610120`, validation `620001-620004`, test `630001-630004`.
- First shard `train_610101_610102`: 20/20 Finished, 20/20 replay-verified, validation PASS; 2,806 transitions (Hard 1,387, Normal 1,419), 41 public card IDs, 17.4 MB. Hard won 13/20; mean/max duration 16.6/36 turns. This validates the pipeline, not learned-model strength.

## Core formulas to report

- Win rate: `wins / finished matches`; errors and truncated matches are reported separately, never counted as losses or draws.
- Paired comparison: use the same seed/deck/seat and count discordant outcomes `n01` and `n10`; report an exact McNemar test when the sample is large enough.
- Duration: report mean, median and maximum turns; the maximum exposes long-tail locks hidden by the mean.
- Imitation objective: `L = -(1/N) * sum(log p_theta(a_teacher | state, legal actions))`. Report validation loss and top-1 accuracy; select checkpoints using validation only.
- Final evaluation: read the reserved test split once after model selection, then run learned-bot matches on untouched gameplay seeds.

## Experiment record template

For every material change, append one entry with:

1. Date, commit, rules/catalog/AI schema and exact command/config.
2. Question or hypothesis.
3. What changed and why; include any relevant formula or threshold.
4. Seeds, decks, seats, bot/model versions and sample size.
5. Completion/errors, primary metrics and notable replay IDs.
6. Conclusion, limitations and the next decision.

Write entries during development. The final thesis should synthesize these records rather than reconstructing months of decisions from memory.

## When changes require new data

| Change | Minimum response |
|---|---|
| UI/animation only; no state, action or timing change | Recheck affected UI; no AI recollection required |
| Numeric balance or rule timing with unchanged contract | Targeted deterministic tests and paired matches; fine-tune/recollect if decision distributions or outcomes move materially |
| New card using existing effects/actions | Catalog/card-vocabulary update, focused interaction tests and new samples containing that card; reuse the model only through an explicit transfer/fine-tune experiment |
| New public feature, action, target relation or effect mechanism | Bump the relevant schema, rebuild/replay/validate, collect versioned data and retrain/evaluate on a new held-out split |

Old datasets remain useful baselines or pretraining sources, but must never be silently merged with data from a different rules/catalog/AI version.

## 2026-11-19 - Rules-v6 collection milestone 6/14

- Commit/plan: `41057b2`, `hard_normal_main_v2_41057b2`, rules/catalog/AI `6/4/3`.
- Command group: first validated shard followed by `Run-AiTrainingCollection.ps1 -MaxShards 5`.
- Result: 6/14 shards validated; 120/120 episodes, 17,838 transitions, Hard 9,092 decisions and Normal 8,746 decisions; zero validation errors.
- Decision: continue with two bounded groups of four shards. Do not prepare datasets until all 14 shards validate.

## 2026-11-19 - Rules-v6 collection milestone 10/14

- Command group: `Run-AiTrainingCollection.ps1 -MaxShards 4`, covering train seeds 610113-610120.
- Result: four shards validated; 80/80 episodes, 12,108 transitions and zero validation errors. Cumulative collection: 10/14 shards, 200 episodes and 29,946 transitions.
- Measured time: 58.68 minutes wall-clock (49.98 simulation, 5.80 replay, about 2.90 validation/overhead).
- Decision: run the final four shards, including the reserved validation and test splits; do not inspect test gameplay outcomes for model tuning.

## 2026-11-19 - Rules-v6 collection complete and smoke training

- Collection result: 14/14 shards validated; 280/280 episodes, 42,298 transitions, Hard 21,493 decisions, Normal 20,805 decisions and zero validation errors.
- Prepared datasets after forced-choice exclusion: mixed `23,898 / 4,806 / 5,101`, Hard-only `12,265 / 2,446 / 2,576`, Normal-only `11,633 / 2,360 / 2,525` for train/validation/test.
- Rules-v6 Normal-only smoke used the small three-epoch config and at most 5,000 train samples. Validation loss improved from 1.2518 to 1.1036; final top-1 was 58.26%. This validates schema/data/model execution only.
- Decision: run three initialization seeds for each of Hard-only, Normal-only and mixed. Select checkpoints by validation only; keep the test split unread.

## 2026-11-19 - Rules-v6 checkpoint selection and one-time test

- Nine CPU runs completed in 59.39 minutes. For every dataset, seed `20261119` had the minimum validation loss and was frozen in `selection.json` before test evaluation.
- A stale inherited `rules-v4 baseline` text label was recorded as a metadata correction; dataset hashes, feature specs, summary versions and weights are rules v6. No checkpoint was retrained or edited.
- Reserved test, evaluated once: Hard-only loss/top-1/top-3 `1.350 / 53.73% / 81.29%` (2,576); Normal-only `0.810 / 72.67% / 94.34%` (2,525); mixed `1.110 / 63.32% / 87.14%` (5,101).
- Decision: mixed is the primary LearnedBot candidate because it covers both teachers. Teacher-only checkpoints remain ablations. Imitation accuracy is not gameplay strength; integration and untouched-seed matches are required next.

