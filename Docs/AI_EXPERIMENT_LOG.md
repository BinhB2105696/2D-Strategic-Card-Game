# AI and Bot Experiment Log

Updated 2026-11-20. This is the living evidence log for supervisor reviews and the final report. Raw runs remain under `SimulationResults`; this file records why each experiment exists and what conclusion is justified.

## AI/ML training priority

**When training the AI bot, prioritize the fastest, most direct way to improve gameplay. Avoid following a routine for its own sake or repeating poorly defined experiments while the model still struggles with basic skills.**

Before planning a batch, check whether a concrete replay example or a rule-derived input can address the suspected model weakness directly. Use larger gameplay tests to measure the win-rate effect of a specific change.

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

## 2026-11-19 - LearnedBot v1 parity and gameplay holdout

- Exported frozen mixed seed `20261119`, epoch 13, into `SimulationResults/TrainingRuns/rules-v6-imitation_20261119_083127/learned-policy-v1/`. The run and export agree on artifact SHA-256 `2d652f4099d8e68e638e77e1558bcd18bb305ef194c3d9e5fef39f9c62c4acdc`.
- Deterministic C# inference uses the schema-3 public policy observation and candidate order. Python/C# matched all 19 selected indices and 125 logits within tolerance (max absolute error `5.72e-6`; 18 validation fixtures and one synthetic reversed retaliation fixture).
- Gameplay batch `20261119_114929_21caad23`: 90/90 finished, zero draws, truncations or errors. Five seeds `2026111901–2026111905`, three deck assignments per opponent and two swapped bot slots per assignment. Learned went `13–17` vs Easy (43.3%), `10–20` vs Normal (33.3%), and `9–21` vs Hard (30.0%). The same five seeds are reused across deck assignments, so 30 games per opponent do not represent 30 independent seeds.
- The batch establishes runtime viability for this candidate. It scored below each baseline in this pilot. Retain it as a supervised benchmark and keep the existing bot lineup. The holdout produced summary rows, and finished traces were discarded with `detailedMatches=0`. The results have no replay verification or diagnostic decision labels.
- Follow-up: four detailed Easy matches, deck 0 vs 0, swapped bot slots on development seeds `2026111911–2026111912`. The first automated attempt `20261119_121638_365aca95` stopped at 0/4 when Windows denied the Unity Player's atomic progress-file replacement. Run the prepared diagnostic config from the user's PowerShell, replay the retained decisions, and inspect recurring errors before deciding whether DAgger is warranted. Preserve the gameplay holdout and the consumed imitation test.

## 2026-11-19 - Easy development diagnostic

- Local diagnostic `20261119_122645_86101ba7`: Learned won 3/4 games across two development seeds and one mirrored deck assignment. Replay verified 4/4 traces, 505 applied actions and 370 decisions. Frozen Python checkpoint argmax agreed with the runner on all 158 Learned decisions with multiple legal options.
- In the one loss (`s0_seed2026111911_leg1`), on turn 2 at sequence 18 Learned entered Battle with 2 available energy. Legal 1-energy allocations to Thorn Bastion or Logistics Squire could each meet that monster's attack requirement, and Learned had no legal attack after entering Battle. Python also ranked advancing first. On turn 4, Learned attacked Sky Sage with Fire Dragon and Easy answered with Guardian Shift. These are tactical review cases from a single loss.
- A similar allocation heuristic also flagged a decision in a win, so skipping an allocation alone does not label a mistake. The four games do not establish a recurring failure or warrant a change to inference or weights. If continuing model development, use fresh development traces to compare early Main-phase allocations and Battle choices against Normal/Hard at the same policy-visible states before selecting a training change.

## 2026-11-19 - Normal/Hard choices on LearnedBot development states

- Run `20261119_123755_a53504e4` used frozen LearnedBot v1, three new seeds `2026111921–2026111923`, Easy, and the mirrored starter deck. Learned won 5/6. Replay verified 6/6, 1,124 applied actions and 836 decisions. Teacher suggestions were logged on the same legal lists before applying Learned's actions. All 150 eligible records had valid indices and Hard reported search iterations with zero fallback cases.
- Exact candidate agreement in Main (121 states): Learned/Normal 72, Learned/Hard 58, Normal/Hard 62. In Battle (29 states): 22, 21 and 24, respectively. Both teachers chose the same candidate on 86/150 states. Learned differed on 21 of those (16 Main, 5 Battle).
- Only one of those 21 was Learned advancing while both teachers allocated energy, in a win. Four Battle differences used another attack and one ended Battle when both teachers would attack. Main disagreements include spell timing, target choice and summoning. Teacher agreement flags review cases, without measuring the outcome of an unplayed move.
- Decision: retain the frozen baseline. This pilot does not isolate a recurring allocation problem or establish a training target. A future DAgger experiment should collect fresh Learned trajectories across more decks and opponents, retain separate development and evaluation seeds, and compare to frozen v1 on held-out gameplay.

## 2026-11-19 - Frozen checkpoint comparison on development traces

- Re-scored the 150 same-state teacher comparisons from `20261119_123755_a53504e4` with all three frozen Python checkpoints. Mixed agreed with Normal/Hard on `94/150` and `79/150`, Normal-only on `102/150` and `65/150`, and Hard-only on `78/150` and `82/150`. Agreement when both teachers picked the same candidate was `65/86` mixed, `58/86` Normal-only and `63/86` Hard-only. Mixed reproduced the runtime choice on `150/150`. No retraining or reserved test evaluation was performed.
- The encoder produced distinct float32 action vectors for all candidates within each of these states (1,363 total). This checks exact within-state collisions for early Main/Battle on the sampled trajectories. Response and trigger observations were outside the sample.
- Next experiment: export and parity-check the two frozen teacher-only checkpoints. Compare all three variants in paired development matches with identical seeds, decks and opponents before investing in new data or architecture. Use a separate gameplay seed block to confirm the selected variant, and report uncertainty by resampling entire seed blocks. For further development, DAgger is a candidate if performance on learner-visited states remains the limiting issue. See [Ross et al.](https://proceedings.mlr.press/v15/ross11a.html) and [Agarwal et al.](https://papers.nips.cc/paper/2021/hash/f514cec81cb148559cf475e7426eed5e-Abstract.html).

## 2026-11-19 - Frozen checkpoint gameplay comparison prepared

- Exported the selected Normal-only (epoch 14) and Hard-only (epoch 15) artifacts. Both passed all 19 Python/C# action indices and 125 logits on the same mixed-validation reference fixtures as v1, including synthetic reversed retaliation. Maximum absolute errors were `4.77e-6` and `2.62e-6`, respectively. Mixed v1 was unchanged.
- `Tools/Simulation/Run-LearnedCheckpointComparison.ps1` prepared 96 matches per variant, 288 total: 12 fresh development seeds `2026111941–2026111952`, Easy and Normal, mirrored decks 0 and 1, and swapped bot slots. Schedule equality and seed separation were verified. Gameplay pending a local run.
- Primary development metric: equally weighted match score (win 1, draw 0.5, loss 0) across the two opponents and two deck matchups. Inspect completion and report paired differences from mixed by seed and by matchup. Carry mixed forward on a tied score; confirm the selected policy on a separate gameplay seed block.

## 2026-11-19 - Frozen checkpoint gameplay comparison results

- All 288 development matches finished, with zero draws, truncations or errors. The runner fingerprint and schedule matched across variants. Mixed won `45/96` (`31/48` Easy, `14/48` Normal), Normal-only `35/96` (`24/48`, `11/48`), and Hard-only `37/96` (`23/48`, `14/48`). Result receipts are under `SimulationResults/LearnedCheckpointComparison/v1-development/`.
- Paired seed-block bootstrap against mixed v1 (12 seeds, 20,000 resamples): Normal-only `−10.4` percentage points (95% interval `−17.7 to −3.1`), Hard-only `−8.3` points (`−24.0 to +6.3`). Mixed and Hard-only tied in aggregate against Normal. Mixed's edge over Hard-only came from the Easy games in this development batch.
- Carry mixed v1 forward as the development leader. Confirm mixed vs Hard-only on fresh seeds `2026111961–2026111972` in the same Easy/Normal matchups, with a separate 24-game mixed vs Hard measurement on mirrored deck 0. The Hard block is descriptive and excluded from the paired primary metric. Use `Run-LearnedCheckpointComparison.ps1 -Confirmation`. Treat the mixed/Hard-only ordering as unresolved if the paired interval includes zero.

## 2026-11-19 - Frozen checkpoint confirmation results

- Confirmed receipts, artifact checksums, shared runner fingerprint and identical paired schedules on 12 new seeds. All 192 primary Easy/Normal matches finished with no draws. Mixed won `43/96` (`27/48` Easy, `16/48` Normal); Hard-only won `28/96` (`18/48`, `10/48`). Mixed minus Hard-only was `+15.6` percentage points (paired seed-block bootstrap 95% interval `+8.3 to +22.9`, 20,000 resamples; eight seed blocks favored mixed and four tied).
- In the separate Hard block, mixed went 7 wins and 16 losses with one timeout (`s4_seed2026111964_leg1`, 60.09 s). Report `7/23` among finished games and one incomplete attempt. That block was outside the primary comparison. Receipts: `SimulationResults/LearnedCheckpointComparison/v1-confirmation/`.
- Retain mixed v1 as the frozen gameplay baseline. Hard-only's lower score does not identify why its labels were harder to imitate. A future one-round DAgger pilot should start from mixed v1, preserve its original supervised data and compare to it on new seeds after training on replay-verified learner trajectories.

## 2026-11-19 - One-round DAgger collection prepared

- Collect 48 games with frozen mixed v1 against Easy and Normal on mirrored decks 0 and 1, swapping slots on six fresh seeds `2026111977–2026111982`. The existing runner records both teacher suggestions in open early Main/Battle. Only the learner's choice drives each trajectory.
- Pilot labels will use exact Normal/Hard agreement after Hard search without fallback, including states where v1 already chose that action. Keep both teacher indices and the learner's index for provenance. Join suggestions to replay-verified policy inputs and legal candidate order before training; replay validates the trajectory, and the teacher suggestions are recorded labels.
- Preassigned collection split by seed: `2026111977–2026111980` train, `2026111981–2026111982` validation. Preserve the original mixed training data in the fine-tune and measure the new candidate against v1 on a distinct gameplay seed block. No training or collection results yet.

## 2026-11-19 - DAgger collection recovered and candidate trained

- The PowerShell watchdog reported a timeout with stale console progress at 17/48. The Unity process had completed 48/48, including the displayed game in 8.25 s. Reused run `20261119_135010_5d811592` after checking its config and model hash and replay-verified all 48 games. The watchdog now uses elapsed time since observed progress and rechecks progress and process exit before stopping.
- Of 1,066 verified candidate states, Normal and Hard agreed in 641 (436 train, 205 validation). Frozen v1 differed from that consensus in 160. Fine-tuned for five CPU epochs with a 3:1 original-train to DAgger sample ratio per epoch. Selected epoch 4 by DAgger validation loss. Consensus validation top-1 rose from `64.39%` to `72.20%`; original validation top-1 went from `64.02%` to `65.65%`. The consumed test was not opened.
- Candidate in `SimulationResults/LearnedDagger/v1/candidate/` passed Python/C# parity on 19 indices and 125 logits. Gameplay pending 192 paired games against Easy/Normal on 12 new seeds `2026111991–2026112002`. Evaluate the candidate-v1 score difference by whole seed blocks before adopting any change.

## 2026-11-19 - DAgger gameplay evaluation

- All 192 matches finished, with no draws, truncations or errors. Verified loaded artifacts, runner fingerprint and identical schedules. Mixed v1 won `42/96` (`25/48` Easy and `17/48` Normal); DAgger won `44/96` (`28/48` Easy and `16/48` Normal).
- DAgger minus mixed was `+2.1` percentage points (paired 95% seed-block bootstrap interval `−6.3 to +11.5`, 12 seeds and 20,000 resamples). Four seed blocks favored DAgger, five tied and three favored mixed. Receipts are under `SimulationResults/LearnedCheckpointComparison/dagger-v1-evaluation/`.
- Keep mixed v1 as the active baseline and preserve the fine-tuned artifact for comparison. The increase in validation teacher agreement did not establish a gameplay gain. If pursuing another improvement, first diagnose losses against Normal and define a targeted experiment on new development seeds. Keep the completed evaluation fixed.

## 2026-11-19 - Candidate shortlist feasibility

- Scored frozen mixed v1 and DAgger on replay-verified early Main/Battle traces, with no training or reserved-test reads. On 150 independent Easy development states, Hard's candidate appeared in mixed v1 top-1/top-3/top-5 `79/124/138` times and in DAgger `72/128/138` times. With more than five legal actions, mixed v1 top-5 retained Hard's choice `84/96` times.
- On Normal trajectories from the two DAgger validation seeds, mixed v1 retained Hard in top-5 `109/134` when there were more than five choices, and `53/77` in that subset when the teachers disagreed. Adding Normal's exact recommendation to top-5 recovered one extra Hard choice in each sampled opponent group. These are recall measurements from correlated states, not prospective gameplay scores.
- Next proposed pilot: test mixed v1 ranking as an input to low-budget Hard search. Preserve a tactical shortlist and the Normal option, and compare with mixed alone and low-budget Hard using its existing ranking at the same budget. Judge paired win score versus Normal together with search time and decision P95.
- If search improves play, retain paired candidate rollout evidence to train relative action quality. The current Hard root sampler gives candidates equal rounds, so its visit counts are not policy preference targets. Consider self-play on a later scale-up, tracking progress against frozen opponents. References: [Expert Iteration](https://proceedings.neurips.cc/paper/2017/hash/d8e1344e27a5b08cdfd5d027d9b8d6de-Abstract.html), [ISMCTS](https://pure.york.ac.uk/portal/en/publications/information-set-monte-carlo-tree-search/), [DouZero](https://proceedings.mlr.press/v139/zha21a.html), [ReBeL](https://papers.nips.cc/paper/2020/hash/c61f571dbd2fb949d3fe5ae1608dd48b-Abstract.html).

## 2026-11-19 - Equal-budget search experiment against Normal

- All 48 smoke games and 576 primary games finished with zero draws or truncations. Primary games share 48 seeds, both slots and mirrored decks 0 and 1. Verified the schedules, shared runner fingerprint, artifact hashes and timing counts. Smoke is excluded from comparison. The primary completed traces were not retained; use the run results, not replay-verified decision labels. Receipts: `SimulationResults/LearnedSearchExperiment/v1/`.
- Mixed v1 won `71/192`, HardLow `102/192`, and LearnedSearch `104/192`. HardLow improved over mixed by `16.15` percentage points (paired seed-block bootstrap 95% interval `+10.42 to +22.40`; 20,000 resamples). The primary LearnedSearch-minus-HardLow contrast was `+1.04` points (`−2.08 to +4.17`). LearnedSearch flipped five losses to wins and three wins to losses; 145/192 pairs had identical action digests.
- Pooled decision P95 was `3.59 / 223.69 / 224.39 ms` for mixed / HardLow / LearnedSearch. LearnedSearch was `0.31%` slower than HardLow at P95. Each search variant had one decision over 1,000 ms. Mean iterations per completed search were about `23.47) for both; LearnedSearch invoked model ranking on 705 decisions in 133 matches.
- Decision: HardLow is the strongest supported option at this search budget before device acceptance. LearnedSearch met the latency criterion and missed the preregistered gameplay criterion of +5 points with a positive lower bound. Preserve the mixed policy for research. For a later learning experiment, compare relative scores from paired simulated candidate actions before collecting another imitation batch.

## 2026-11-20 - Paired rollout score pilot prepared

- On the first eligible open Main/Battle decision in each phase through turn 8, collect the tactical baseline and up to three alternatives ranked by HardLow's initial search. Score them on eight further common sampled information sets, with fresh tree roots to give fixed rollout continuations. Save scores relative to the tactical baseline with legal-candidate indices. Extra rollouts do not enter the 24-iteration move selection. They do add to collection wall time.
- The launcher first runs four control and four annotated games on seeds `2026112073–2026112074`. Both sides must match in action digest and final state hash between modes. The collection then runs 48 HardLow–Normal games on `2026112081–2026112092`, two mirrored decks and swapped slots. Replay must verify every trajectory before training.
- Initial split: nine collection seeds for training (`2026112081–2026112089)), three for validation (`2026112090–2026112092)). Check whether paired orderings agree across samples and how many usable labels remain per phase before choosing a training margin. Keep the previous evaluation seeds separate. Outcome labels and fine-tuning are pending the Unity build and collection.


## 2026-11-20 - Paired rollout pilot trained

- Smoke control and scored copies matched all four action digests and final state hashes. All 48 collection games finished and replay verified. Joined 301 labelled decisions to verified policy inputs and candidate order.
- Required the score difference to have the same nonzero sign in both four-world halves and a mean magnitude of at least 0.02. Chose the margin on training seeds only. Retained 439 train pairs and 108 validation pairs (312/127 and 79/29 Main/Battle).
- Fine-tuned mixed v1 for six epochs with one rollout pair per three original supervised examples. Epoch 4 minimized paired validation loss. Pair accuracy increased from 58.33% to 62.96% (63 to 68 of 108). Original validation top-1 increased from 64.02% to 65.25%. Candidate parity passed 19 indices and 125 logits.
- Gameplay evaluation prepared on 32 independent seeds 2026112101–2026112132. Mixed v1, paired-rollout and HardLow each play 128 games against Normal. Compare candidate minus mixed by whole seed with score and decision P95. Target at least +5 pp with a positive lower interval bound.

## 2026-11-20 - Paired rollout gameplay evaluation

- Verified 384/384 finished matches, zero draws or truncations, shared runner fingerprint, model hashes and identical 32-seed schedules. Mixed v1 won 38/128, paired-rollout 40/128, and HardLow 76/128. Pooled choice P95 was 3.55, 3.58 and 242.42 ms, respectively.
- Paired-rollout minus mixed was +1.56 pp (paired 95% bootstrap interval −4.69 to +8.59, 32 seed blocks, 20,000 resamples), below the +5 pp target and with an interval spanning zero. Paired-rollout minus HardLow was −28.13 pp (−36.72 to −20.31). Retain mixed v1 and HardLow as the respective model-only and search baselines.
- Total active runner time was 19m32s versus 39m40s for the prior 576-game search experiment. This round had 384 rather than 576 games and 128 rather than 384 search games. Mean HardLow game time stayed near 5.28 seconds. Two model-only arms averaged about 1.93 seconds per game. Receipts: SimulationResults/LearnedCheckpointComparison/paired-rollout-v1-evaluation/.

## 2026-11-20 - Learner-state rollout diagnostic prepared

- Compare the exact mixed v1 action, HardLow's pre-search tactical choice and its searched choice on mixed v1 trajectories. Sample at most once per open Main/Battle phase through turn 8, and use eight common information-set worlds per sampled disagreement. Only mixed v1's chosen action drives the live trajectory. Record unsupported sampled roots and agreements explicitly.
- 48 games against Normal, two mirrored decks, paired slots, 12 fresh seeds 2026112141–2026112152. Reserve 2026112150–2026112152 for validation. Four smoke pairs on separate seeds must preserve action and final-state digests before collection.
- Schedule and frozen artifact checks passed. Unity build, smoke, collection and replay remain pending. Inspect mean preference direction across two world halves and seed coverage before selecting a correction experiment.

## 2026-11-20 - Learner-state diagnostic results

- Four annotated smoke trajectories matched controls. Main collection 48/48 replay-verified, with 308 comparisons, 139 exact agreements and 169 scored decisions. Verified each score label against the replay policy input and candidate index.
- A pair counted as stable when both four-world means shared a nonzero sign and the full mean differed by at least 0.02. HardLow versus mixed: 27 higher/6 lower in train and 14 higher/2 lower on the three validation seeds. HardLow versus the tactical choice: 21/4 in train and 9/2 in validation. These are rollout preferences, not causal match win estimates.
- The 27 direct train corrections are too few for the next fine-tune. Prepare two replay-gated 48-game extensions with unchanged scoring and the same runner. Reserve the last three seeds of each shard for validation. No training yet.

## 2026-11-20 - Learner-state extensions complete

- All 144 games on 36 seeds replay-verified under the same runner/catalog fingerprints and frozen mixed policy. Observed 931 eligible comparisons, 415 exact agreements and 516 scored disagreements. Every scored learner state was joined to its verified policy input and candidate order.
- Applying the frozen stability rule (same nonzero sign in both four-world means and absolute overall margin at least 0.02), HardLow minus mixed was 87 higher, 19 lower and 216 unclear in train, and 32 higher, 4 lower and 70 unclear in validation. HardLow had more higher than lower cases in 24/27 train seeds (2 ties, 1 lower) and 9/9 validation seeds. Both decks and phases contributed.
- Tactical choice alone versus mixed: 59 higher/58 lower in train and 19/11 in validation. HardLow versus the tactical choice: 78/9 and 23/3. Direct positive HardLow-vs-mixed labels include 37 Battle Attack-vs-Attack comparisons and 29 Main Summon-vs-Summon across splits. Scores reflect the rollout evaluator, not measured match wins.
- On the 36 stable direct validation pairs, both prior fine-tunes reordered only two of the 32 cases favouring HardLow and retained mixed's choice in all four opposite cases. This is a deliberately selected disagreement slice, not a general accuracy estimate. Next: train a targeted pairwise candidate from mixed v1 with both preference directions and original supervised examples. Choose by seed-separated validation and original validation retention, then measure paired gameplay on fresh seeds. No fine-tune was run in this analysis.

## 2026-11-20 - Targeted learner-state candidate trained

- Fine-tuned mixed v1 on 106 stable direct learner-vs-HardLow pairs (87 HardLow-favoured and 19 mixed-favoured), with three original supervised examples per pair in each epoch. AdamW 3e-4, batches of 64, 12 CPU epochs. Epoch 8 selected by pair-validation loss. On 36 pairs, correct rankings rose from 4 to 11 (7/32 HardLow-favoured and 4/4 mixed-favoured). Original validation top-1 declined from 64.02% to 62.75%. C# parity passed 19/19 indices, 125 logits (max error 2.86e-6). Prior test was not accessed.
- One-command comparison prepared: 384 games, two model-only variants versus Normal, 48 fresh seeds 2026112191–2026112238, two mirrored decks and swapped slots. Retain mixed as baseline and use the existing runner. Gameplay pending; compare paired score improvement and P95. At the observed 1.93 s per model-only match, expect roughly 12–15 minutes.

## 2026-11-20 - Targeted learner-state gameplay evaluation

- All 384 games finished without draws or truncations. Verified matching runner/catalog fingerprints, artifact hashes, and paired schedules across 48 fresh seeds. Mixed v1 won 62/192 (32.29%), targeted learner-state 63/192 (32.81%). Receipts: SimulationResults/LearnedCheckpointComparison/learner-rollout-v1-evaluation/.
- Candidate minus mixed was +0.52 pp (paired 95% seed-block bootstrap interval −4.69 to +5.73, 20,000 resamples). Candidate led on 11 seeds, tied on 27 and trailed on 10. It changed the action digest in 185/192 paired games, flipping 16 losses to wins and 15 wins to losses. Decision P95 was 3.64 ms versus 3.84 ms for mixed, and active runner time was about 12m26s.
- The candidate missed the +5 pp target and the paired interval crossed zero. Retain frozen mixed v1 as the model-only baseline and HardLow as the search baseline. Preserve the experimental candidate. If pursuing another learning experiment, first test whether current short-horizon rollout labels agree with longer continuations on consequential disagreements before collecting more of the same labels.

## 2026-11-20 - Short versus long rollout diagnostic prepared

- Compare exact actions on the same eight sampled information-set worlds. Continue each fork from the existing two-turn short checkpoint to a horizon of eight turns from the root, with a cap of 192 extra decisions. Save long heuristic scores and separate terminal returns. A branch that does not finish has no terminal return.
- Smoke pairs the short-only and extended modes on four trajectories. Action digests, final-state hashes and short label arrays must match. The collection then runs 24 mixed v1 vs Normal games on seeds 2026112251–2026112256 across two mirrored decks and swapped slots. All collection traces must replay-verify.
- Compare stable short disagreement preferences with long direction and paired terminal outcomes. This is a label diagnostic; no new weights or gameplay evaluation are planned for its seeds. Configuration verified; Unity build and execution pending.

## 2026-11-20 - Short versus long rollout diagnostic results

- Smoke matched all four trajectories and their short labels. All 24 collection games replay-verified (mixed v1 won 9/24), with 153 compared states: 83 scored and 70 exact agreements. HardLow and mixed chose distinct exact actions in 74 of the 83 scored states.
- Of 23 stable two-turn HardLow-vs-mixed preferences (same sign in both four-world halves, mean magnitude at least 0.02), 14 remained stable in the same direction at the eight-turn checkpoint and nine became unclear; zero reversed stably. Three unclear cases had an opposite mean sign. Main kept 9/16 and Battle kept 5/7.
- In 209 of 592 paired worlds (35.3%) both branches reached terminal and the winners matched in all 209 (84 wins for both, 125 losses for both). In 56 worlds only one branch finished, and in 327 neither finished. Of 74 decisions only eight had terminal outcomes for both actions in all sampled worlds. This does not validate short preference labels as win-probability targets.
- Decision: preserve the heuristic label data and frozen baselines. Next diagnostic should choose late or tactical decisions for high terminal coverage, hold the sampled world and continuation policy fixed between actions, and measure paired win-return differences directly before training again.

## 2026-11-20 - Terminal-focused diagnostic prepared

- 48 mixed v1 vs Normal games, two mirrored decks and swapped slots, fresh seeds 2026112261–2026112272. Select open Main/Battle from turn 9 or with either player's LP at most 1,500. Cap at 12 comparisons and six exact disagreements per match. Sample eight common worlds for the exact mixed and HardLow actions, using the same fixed continuation policies. Extend through up to 20 turns with a cap of 384 extra decisions; keep unfinished terminal returns null.
- Smoke checks four short-only vs extended trajectories, including byte-equivalent scenarios and short label content. All main trajectories require replay verification. Use score at terminal (win 1, draw 0, loss −1) for the primary diagnostic.
- Predeclared pilot gate: at least 75% of paired worlds both finish, at least 12 stable favourable decisions and twice as many favourable as adverse, plus net favourable in at least six seed blocks. A decision needs at least three paired terminal outcomes in each four-world half and the same nonzero mean direction in both halves to count as stable. If the gate is missed, report and discuss alternatives before collecting further or training. Config checks passed. Unity build and execution pending.

## 2026-11-20 - Terminal-focused diagnostic stopped at the pilot gate

- All 48 games replay-verified on 12 new seeds: 263 comparisons, 128 exact agreements and 135 scored disagreements. Smoke matched all four played trajectories and short labels. Collection: SimulationResults/LearnedRollout/learner-terminal-v1/verified-collection.json.
- Both branches finished in 1,078/1,080 paired worlds (99.8%, coverage gate passed). In complete pairs, HardLow improved the terminal outcome 26 times, worsened it 25 times, and tied 1,027 times.
- Four decisions stably favoured HardLow versus three favouring mixed. Two of 12 seed blocks had more favourable than adverse decisions. All seven stable cases came from mirrored deck 1. The predeclared gates of 12 favourable decisions, 2:1 ratio and six positive seeds all failed.
- Stop terminal paired-label collection and fine-tuning pending discussion. The limitation in this pilot is scarcity and lack of a clear net direction in match-outcome changes despite high terminal coverage. Keep mixed v1 and HardLow frozen. Discuss optimizing HardLow strength per compute or a different learning objective based on full games.

## 2026-11-20 - Project objective and full-game reward pilot

- User priority: improve the learned model itself, with high win rates against unchanged Easy and Normal and a genuine edge over the original full-strength Hard. Track win rates against each separately on independent seeds, both slots and multiple decks. Medium or HardLow modes are diagnostic only. Hard acceptance uses the original Hard and a paired uncertainty interval.
- Integrity: preserve bot strengths, game rules and card presets; keep opponent hashes and runtime/catalog fingerprints. The learner sees its entitled public/owned observation and legal actions, with a separate RNG for exploration. No opponent hidden cards, private deck order, match seed as policy input, or special gameplay advantages. Turn off exploration for evaluation. Preserve old data, frozen models, negative results and held-out seeds for reproducibility and regression checks.
- Pilot prepared: 8 smoke matches, then 192 completed exploratory mixed-v1 episodes against fixed Easy, Normal and greedy mixed v1 on two decks with slot swaps, on fresh seeds 2026112281–2026112296. Record the sampled legal action log-probability at each non-forced choice; game results supply terminal rewards. Before training, join samples to verified transitions and check behavior probabilities against the frozen policy. Rules and opponent sources were untouched by this change.
- Standalone C# exploration and inference parity checks passed. Unity build, collection, and full-game reward training are pending.

## 2026-11-20 - Reward collection recovered and model trained

- The PowerShell display stopped refreshing at 150/192. Unity completed all 192 at 11:44:41 local, all Finished. Recovered the original run 20261120_043651_04855992 after verifying its config, policy and source hashes. No games rerun. Completed all 192 replays; receipt SimulationResults/LearnedReward/v1/verified-collection.json.
- Reproduced all 7,633 sampled choices and logged behavior probabilities from the frozen model and independent exploration RNG (max logp error 3.17e-6). The exploratory learner won Easy 20/64, Normal 12/64 and greedy mixed 16/64. These are collection outcomes of the sampling policy.
- Trained one bounded update of two clipped policy-gradient epochs with actual terminal returns, a leave-one-match-out opponent/deck/slot baseline and an original supervised loss anchor. Candidate export passed 19/19 decision parity and 125 logits. No opponent, rule or catalog changes. Candidate gameplay remains unmeasured.
- Paired first evaluation prepared: 384 games, 24 fresh seeds 2026112301–2026112324, two models against unchanged Easy and Normal, two decks and both slots. Deployment and full-strength Hard performance must be decided on independent gameplay measurement.

## 2026-11-20 - Full-game reward development result

- All 384 games finished on identical paired schedules and verified fingerprints. Frozen Easy, Normal and Hard source hashes matched collection. Against unchanged Easy, mixed v1 won 52/96 and reward v1 won 58/96 (+6.25 pp, paired 95% interval +1.04 to +12.50). Against unchanged Normal, 37/96 versus 39/96 (+2.08 pp, −2.08 to +6.25).
- Aggregate 89/192 versus 97/192, +4.17 pp (95% +0.52 to +7.81). Candidate ahead on 8/24 seeds, tied on 14, behind on two. Decision P95 4.15 ms to 3.88 ms. This is encouraging development evidence, concentrated in Easy, and does not establish an edge over Hard.
- Next: bounded full-strength Hard probe on seeds 2026112331–2026112338, two mirrored decks, both slots, 32 games per policy. Evaluation is greedy, opponents are unchanged, and the runner fingerprint must match collection. This 64-game probe assesses transfer and is not the final >50% confirmation.

## 2026-11-20 - Full-strength Hard probe and reward round 2

- All 64 paired games finished, with frozen source/catalog and opponent hashes. Verified standard Hard profile H5.4 at 96 root iterations. Mixed v1 won 12/32 (37.50%), reward v1 won 13/32 (40.63%). Paired delta +3.13 pp, 95% seed-block bootstrap −6.25 to +12.50. Two seeds improved, five tied, one regressed. Active runner time was 16m51s. The probe neither establishes an improvement before Hard nor meets the target of an edge above 50%.
- Reward round 2 prepared: initialize from reward v1. Collect 192 finished exploratory games on fresh seeds 2026112345–2026112360, with 64 games each against unchanged Easy, Normal and full-strength Hard. Same two mirrored decks, both slots, fresh independent exploration stream. Reuse the frozen runner and verify source/policy/opponent hashes plus all replay trajectories before training.

## 2026-11-20 - Reward round 2 trained

- All 192 round 2 trajectories replay-verified, 64 per unchanged opponent. Standard Hard profile confirmed on every Hard training trace (96 iterations without learned ranking). Reproduced all 7,158 sampled choices (2,198 from Hard matches), including independent RNG draws and logged probabilities; max log-probability error 2.60e-6.
- Initialized from frozen reward v1, preserved original supervised anchor, and took the same two clipped-policy-gradient epochs with actual end-of-match rewards. Candidate parity passed 19/19 indices and 125 logits. No changes to opponents, game rules, card catalog or C# inference.
- Prepared one-command comparison of reward v1 and v2: 288 games, 48 per opponent per policy, unchanged Easy, Normal and Hard. Use 12 fresh seeds 2026112369–2026112380, both slots and two mirrored decks. Report paired changes per opponent; Hard is primary. No new gameplay results yet.

## 2026-11-20 - Reward v2 evaluation and exploration diagnostic

- All 288 evaluation games finished without draws, errors or truncations. Verified paired schedules on 12 fresh seeds, common runner fingerprint, checkpoint hashes, disabled evaluation exploration and original opponent source hashes. Hard profile remained H5.4 with 96 iterations.
- Reward v1 vs v2: Easy 30/48 vs 28/48 (−4.17 pp, paired 95% interval −16.67 to +6.25), Normal 20/48 vs 16/48 (−8.33 pp, −16.67 to 0.00), Hard 13/48 vs 12/48 (−2.08 pp, −8.33 to +4.17). Aggregate 63/144 vs 56/144 (−4.86 pp, −8.33 to −1.39). Keep reward v1. Receipts: SimulationResults/LearnedCheckpointComparison/reward-v2-development/.
- Re-scored all 7,158 logged collection decisions against greedy reward v1. The behavior policy chose a non-greedy action 2,558 times (35.74%, median 11 deviations per game), including 771/2,198 (35.08%) versus Hard. Rescoring the same states with softmax temperature 0.35 and uniform mix 0.02 suggests about 16% expected deviation, without evidence yet about its gameplay. One Hard deck/slot stratum had no terminal reward variation. These findings do not identify a causal reason for the v2 regression.
- Pause repeat training. Discuss a controlled next pilot from frozen reward v1 that changes only the exploration distribution and retains the full-game objective, unchanged opponents and independent paired evaluation. Measure exploration divergence and opponent-specific win rates, with Hard primary. No new training or gameplay was run here.

## 2026-11-20 - Lower-exploration reward control prepared

- Reward round 3 starts from frozen reward v1, the same parent as round 2. Match round 2's collection schedule exactly: 192 games, training seeds 2026112345–2026112360, both slots, two mirrored decks, 64 games each against unchanged Easy, Normal and full-strength Hard. Reuse the same independent exploration RNG seed for control. These are training seeds; future gameplay evaluation will use a separate seed block.
- Change only the training sampler from temperature 1.0 and 5% uniform to temperature 0.35 and 2% uniform. Keep original supervised anchor, two clipped-policy-gradient epochs, all optimizer settings and training RNG seed as in round 2. Greedy evaluation is unchanged.
- Verified round-2/round-3 configs match except the sampler flag, frozen opponent hashes, and C#/Python sampling checks. Unity smoke must confirm low-exploration metadata with the same catalog. Next collect and replay-verify all games, check actual deviation rate and behavior probability parity, then evaluate on fresh seeds. Collection/training currently pending.

## 2026-11-20 - Lower-exploration reward round trained

- Round 3 replay verification passed 192/192. The frozen policy reproduced the logged probabilities and draws for 7,256 choices (max log-probability error 7.41e-6). Non-greedy choices fell from 2,558/7,158 (35.74%) in round 2 to 1,240/7,256 (17.09%) in round 3. Exploratory wins were 32/64 Easy, 18/64 Normal and 19/64 full-strength Hard. They measure the collection policy, not the trained candidate.
- Trained reward v3 using the same two clipped updates and supervised anchor as round 2. Export in `SimulationResults/LearnedReward/v3/candidate/` passed Python/C# parity for 19 indices and 125 logits (max error 2.38e-6).
- `Run-LearnedCheckpointComparison.ps1 -RewardRound3` prepares a one-command, 288-game v1/v3 comparison using 12 fresh seeds (2026112393–2026112404), both slots, both mirrored decks and fixed Easy, Normal and full-strength Hard. The attempted launch from Codex stopped at 0/144 with `UnauthorizedAccessException` in `File.Replace` when writing progress. No comparison result is available. Run the same command in the project's local PowerShell; the frozen runner is already built.
- After the paired evaluation, test the proposed critic on replayed training games split by seed. Compare its terminal-return prediction against an opponent/deck/slot mean estimated solely from each training fold. Keep the actor and opponents fixed during this diagnostic.

## 2026-11-20 - Reward v3 result and critic pilot

- All 288 games finished. Verified checkpoint hashes, identical schedule on 12 fresh seed blocks, frozen source and opponent hashes, greedy evaluation, and 96-iteration Hard profiles in the retained traces. Reward v1 -> v3: Easy 30/48 -> 29/48, Normal 20/48 -> 19/48, Hard 19/48 -> 16/48 (−6.25 pp, paired 95% interval −16.67 to +4.17). Overall 69/144 -> 64/144 (−3.47 pp, −9.72 to +2.78). Keep v1. Lower exploration changed the behavior distribution but produced no observed win-rate improvement. Receipts: `SimulationResults/LearnedCheckpointComparison/reward-v3-development/`.
- Tested a ridge critic on the 192 round-3 replays (7,256 decisions, 16 seeds). Four folds split by whole seed, with scaling and fitting trained on the other three folds. Fit the encoded public/owned context with fixed unit ridge penalty, equal match weighting and final −1/0/+1 target. Compared with the opponent/deck/seat mean learned from each training fold. Held-out MSE 0.8675 -> 0.5522 overall, 0.8038 -> 0.5405 on Hard. Restricting to the first half of sampled decisions, critic MSE was 0.7878 overall and 0.7571 on Hard. This measures outcome prediction under the collection policy.
- Trained `SimulationResults/LearnedReward/v3/critic-candidate/` from reward v1 using the same round-3 replay collection and optimizer settings. Only the advantage estimator changed to GAE with the out-of-fold value predictions, gamma 1 and lambda 0.95. Terminal reward remains −1/0/+1. Inference still uses the action scorer and no critic input. Python/C# parity passed 19/19 indices and 125 logits (max error 4.05e-6).
- Prepared `Run-LearnedCheckpointComparison.ps1 -RewardCritic`: 288 paired matches, 48 per opponent per checkpoint, unchanged Easy/Normal/Hard, fresh seeds 2026112417–2026112428 with mirrored decks and seat swaps. Gameplay evaluation is pending local PowerShell. Compare against reward v1, with Hard primary. Pause for discussion if it shows no practical gain.

## 2026-11-20 - Critic gameplay evaluation and decision

- Verified all 288 games finished with no draws, identical 12-seed schedules, checkpoint/config and runner fingerprints, frozen opponent source, greedy policies, and 96-iteration Hard profiles on the retained traces. Reward v1 -> critic: Easy 29/48 -> 30/48 (+2.08 pp, paired bootstrap 95% −4.17 to +8.33), Normal 19/48 -> 16/48 (−6.25 pp, −16.67 to 0.00), Hard 17/48 -> 17/48 (0.00 pp, −8.33 to +8.33). On Hard, four losses flipped to wins and four wins to losses. Overall 65/144 -> 63/144 (−1.39 pp, −5.56 to +2.78). Receipts: `SimulationResults/LearnedCheckpointComparison/reward-v3-critic-development/`.
- Keep reward v1 and pause the current full-game reward/GAE path. The critic improved held-out outcome prediction on the collection, while this policy update showed no gameplay gain. Neither this trial nor the prior terminal-branch pilot establishes that more GAE updates, reward tuning, or the same rollout labels would improve play.
- Proposed next step for discussion: use reward v1's existing verified replay states to look for recurring tactical misses whose alternative is demonstrably better from legal observations. Start with immediate win/loss examples and require stability across seeds and both decks before training. Skip ambiguous hidden-response cases. If evidence supports a recurring actionable error, train one targeted action-ranking candidate from reward v1 with the original supervised anchor. Use seed-separated tactical validation before paired gameplay testing. No further training or simulation launched.

## 2026-11-20 - Tactical replay diagnostic

- Audited all 192 replay-verified matches from the round-3 collection generated by sampling frozen reward v1 (64 per opponent, 16 seeds, two mirrored decks and both seats). Scored the greedy frozen checkpoint on exact legal candidates in each flagged actor-visible state. Restricted the win proof to ordinary open Battle with vacant back rows; used public attack, defense, health, rank and retaliation marks, no assumed piercing, and screened out possible lethal reflection. An attack had to deal the remaining LP directly or KO a visible monster whose default penalty would end the game. All 32 sampled actions flagged as actual lethal resolved to an immediate learner win in their verified transitions.
- Across 7,256 multi-choice states, found 45 certified lethal opportunities and 13 greedy skips across 12 games. Easy: 7/20 opportunities skipped across six games. Normal: 4/15 across four. Hard: 2/10 across two seeds and both decks. All 12 affected trajectories were ultimately wins, including the two Hard cases, in which the sampled action also skipped lethal. These observations are from the exploratory collection and the narrow proof excludes uncertain responses. No recoverable loss demonstrated for this target.
- In the 45 losses to Hard, every terminal action was a Hard attack: 36 direct and nine targeted. No prior-turn safe KO of the eventual lethal attacker was identified against Hard with the same narrow screen. Of 36 direct-attack losses, the learner entered its previous Main without a monster in 32 and exited that Main without a monster in 25. At all 25 empty-board exits there was no legal summon. This locates the vulnerability earlier in the replay but does not identify the choice that caused it.
- Stop this immediate-lethal target without training a candidate or running another matchup batch. Preserve reward v1 and all evaluation data. Proposed next discussion: investigate the earlier loss of board presence and resources. Prioritize earlier decisions that cause large board differences, and validate public-information alternatives before training on them.

## 2026-11-20 - Lost-board replay diagnostic

- Committed and pushed the LearnedBot integration, simulation/training tools and evidence through the tactical diagnostic in `8fdb490` (`origin/main`). Local HardBot verification edits, generated files and training artifacts were excluded.
- Traced the final loss of board presence in all 36 direct-attack losses to full-strength Hard. Hard's attack removed the last monster in 35 games; a learner card activation removed it in one. After losing the board, 11 games ended during the same turn, 19 two turns later, four four turns later and two six turns later.
- Of 33 matched Main exits immediately before that collapse, greedy v1 passed on a legal extra summon in two games. Five had a legal Defense switch for the eventual victim, and one of those passed a conservative same-hit survival screen using visible stats, up to 300 DEF piercing and no retaliation reduction. These three leads were on separate seeds of the second deck only. They identify local choices; survival against the opponent's subsequent choices is unverified.
- Across the two learner Main turns before collapse, a non-summon action removed an available summon option in three games, also on the second deck (two allocation, one activation). In the 25 final-Main exits with a vacant board and no legal summon, 14 hands had only tribute-requiring monsters and 11 had no monster. This locates the failure to rebuild and does not establish that earlier summoning or allocation decisions would have won those games.
- Hold reward v1 as model baseline. Do not train on these isolated choices or repeat a large comparison. Proposed next experiment for discussion: first benchmark a separate, stronger information-set search teacher against the unchanged Hard on development seeds, then consider offline distillation on early Main/Battle states only if the teacher's gameplay and action preferences justify it. Keep evaluation seeds separate and the search teacher out of the model's runtime.

## 2026-11-20 - Search teacher benchmark prepared

- `SearchTeacher` is a simulator-only Hard search profile with a 192-iteration root budget. The reference Hard keeps 96. Both use the same tactical policy, root shortlist, paired information-set sampling, search depth, rollout horizon and known-own-deck rules. Opponent sources match `8fdb490`; the experiment changes only the teacher's sampling budget. It uses no Learned policy artifact.
- `Tools/Simulation/Run-LearnedSearchTeacher.ps1` prepares three disjoint seed blocks with both seats on each mirrored deck: smoke 8 games (2026112441–2026112442), development 96 (2026112451–2026112474), and independent confirmation 192 (2026112501–2026112548). The smoke verifies the 192/96 profiles and measures gameplay time. All phases require finished games and the planned schedule. Confirmation requires a development win rate of at least 55% overall and at least 50% in both decks. Evidence of a teacher edge requires the confirmation 95% seed-block bootstrap lower bound above 50% and an observed win rate above 50% in each deck. Draws are reported separately.
- PowerShell syntax and configuration generation passed. Synthetic fixtures exercised report arithmetic, per-deck gates, invalid schedule and excess search-budget rejection. Unity build and real gameplay pending. If confirmation supports a teacher edge, investigate action preferences on independent early Main/Battle training states and distill into Reward v1 before evaluating a pure learned candidate. If the gate fails, review before spending more CPU or generating labels.

## 2026-11-20 - Search teacher smoke completed

- Unity runner built and completed 8/8 games. Verified the two seeds, mirrored decks, both legs, and search profiles on all eight traces. Actual per-decision maxima reached 192 for SearchTeacher and 96 for reference Hard. SearchTeacher won 4/8 (2/4 per deck), with no draws. This stage verifies configuration and runtime only.
- Mean match wall time 45.45 s, slowest 98.26 s, batch wall time 365.56 s. At this throughput the 96-game Development batch would take about 73 minutes. Development config prepared on seeds 2026112451–2026112474. Run `./Tools/Simulation/Run-LearnedSearchTeacher.ps1 -Stage Development` in local PowerShell with the same built runner. Results pending at `SimulationResults/LearnedSearchTeacher/v1/Development.result.json`.

## 2026-11-20 - Search teacher development finished below the gate

- Simulator completed 96/96 on the planned 24 seed blocks, including both seats and both decks. The PowerShell wrapper left neither a resource report nor a stage receipt. Validated the completed run, full configuration, schedule, frozen opponent sources, 192/96 trace profiles and matching smoke runner/catalog fingerprints. Recovered `SimulationResults/LearnedSearchTeacher/v1/Development.result.json` from the finished data. No games were rerun.
- SearchTeacher won 50/96 (52.08%), lost 46, and drew zero. Deck results were 23/48 and 27/48. Seed-block bootstrap 95% interval: 45.83%–57.29%. Mean 46.50 s/game, about 74 minutes of gameplay. The predeclared 55% overall and 50% per-deck gate failed. Pause this profile before confirmation or distillation. Reward v1 remains the model baseline.

## 2026-11-20 - Direct fundamentals lessons and scorecard

- Built 161 scorecard rows from 192 replay-verified games. Kept observed local outcomes and match win rates in separate columns, with action and unique-match denominators. See `Docs/AI_ACTION_SCORECARD.md` and `SimulationResults/LearnedFundamentals/v1/action-scorecard.csv`.
- Extracted 564 direct lessons: 183 energy, 152 summon, 113 attack, 109 certified lethal, and 7 defense. Reused original supervised examples as an anchor. Certainties came from legal rules with empty back rows; other lessons were successful Hard demonstrations with observable inputs. Used 12 development seeds for training and four disjoint seeds for 109 validation examples. No defense sample appeared in validation.
- Compared frozen reward v1 (64/109), tuned lesson control (66/109), and lessons plus explicit context (66/109). Selected the last on lower held-out lesson loss, `0.8995` vs `0.9323` for the tuned control. Original validation subset top-1 62.21% -> 61.23%; this is a small offline signal. Encoder 2 adds six board slots and rule-derived features for energy and summon counts. A two-monster energy fixture verified that v1 aliases `[2,0]` and `[1,1]` for advancing phase while v2 inputs differ. Python/C# parity passed 31 indices and 166 logits; v1 backward parity passed 19/125.
- Prepared a CMD-native 96-game Hard screen (48 per model, 12 fresh seeds 2026112561–2026112572, two mirrored decks and seat swaps). `Tools/Simulation/run_learned_fundamentals_evaluation.py --prepare-only` passed. Rebuild the Windows Unity runner once for encoder 2. Gameplay pending. Retain v1 pending results, then test all original opponents if the Hard screen supports further evaluation.

## 2026-11-20 - Fundamentals Hard screen complete

- Verified 96/96 finished matches with no draws. Checked artifact hashes and encoder versions, 48 paired seed/deck/seat keys, identical schedules, matching runner/catalog fingerprints and original 96-iteration Hard. Results: `SimulationResults/LearnedFundamentals/v1/{reward-v1,fundamentals}.result.json`.
- Reward v1 18/48 (37.50%) vs Fundamentals 19/48 (39.58%). Paired difference +2.08 pp, seed-block bootstrap 95% −6.25 to +10.42. Three losses flipped to wins, two wins flipped to losses and 43 pairs kept the same outcome. Deck 0: 8/24 -> 8/24. Deck 1: 10/24 -> 11/24. Gameplay took about 31 minutes. Keep Reward v1 and retain the candidate and scorecard as evidence. This screen does not support a larger evaluation of the same candidate.
- Next proposed diagnostic: isolate attack readiness rather than re-tune the entire lesson mixture. Use existing seed-separated replay states and concrete legal alternatives to check whether Reward v1 and Fundamentals distinguish allocations of equal cost that do and do not complete an eligible attacker, and whether they leave Main with no ready attacker when a completing allocation exists. Verify the local consequence before assigning a preference. Check the frequency and baseline error set before training or running another gameplay batch.

## 2026-11-20 - Targeted attack-readiness pilot

- Offline audit used 192 verified reward-v3 replays and 144 independent verified mixed-v1 vs Normal replays. Restricted to open Main on turns that reached Battle, no ready attacker, and otherwise eligible attack-position monsters. Compared legal allocations at the same cost for completion of the first attacker's energy requirement. This measures readiness at the chosen state, not a counterfactual game win. Reward v1 and Fundamentals each preferred readiness in 16/31 independent states. In the 14 states against Hard, both chose the readiness side by rank in 6/14. Also monitored observed early Battle transitions as a separate measure.
- From Reward v1, taught 22 equal-cost examples with 12 updates. Only three added allocation-feature columns could change; no early-Battle examples used as labels. On four separated reward-v3 validation seeds, preference accuracy rose 3/13 -> 9/13. On the 144 independent Normal trajectories, 16/31 -> 27/31, with greedy incomplete allocations 3 -> 2 in the 31 cases. Original validation top-1 on 1,024 examples 62.21% -> 61.72%. Python/C# parity passed 27 choices and 237 logits. Artifacts: `SimulationResults/LearnedFundamentals/attack-readiness-v1/`.
- Prepared a 48-game Hard screen using the already verified Reward v1 control on the same seeds, decks, seats and built runner from the Fundamentals pilot. `py -3.12 -B Tools/Simulation/run_learned_fundamentals_evaluation.py --attack-readiness --prepare-only` passed. No Unity rebuild or repeat baseline games needed. Use fresh seeds for any follow-up confirmation.
- Discussed Hard-vs-trainable-Hard and self-play. Previous work includes frozen-bot imitation, matches against a frozen learned opponent and a larger-budget Hard search benchmark without policy updates. Iterative self-play is a separate option. Hard currently samples hypothetical hidden worlds from legal information; real hidden cards and match seed are excluded from policy inputs.

## 2026-11-20 - Attack-readiness Hard screen complete

- All 48 new candidate games and 48 existing Reward v1 control games verified. Identical paired seed/deck/seat schedule, first player, runner/catalog fingerprints, model hashes and standard Hard profile. Candidate 20/48 (41.67%) vs Reward v1 18/48 (37.50%), no draws. Paired +4.17 pp, 95% seed-block bootstrap 0.00 to +10.42. Three losses became wins, one win became a loss and 44 final outcomes agreed. Deck 0: 8/24 -> 10/24; deck 1: 10/24 -> 10/24. The net gain came from two seed blocks, both when the learner acted second. Candidate runtime about 14 minutes.
- The two available paired detailed traces had the same final outcomes. Their first differences on identical states were `ChangeBattlePosition` -> `AllocateEnergy` and `SummonMonster` -> `AllocateEnergy`. The same-cost training also changed ranking across action families. The four pairs that flipped the winner have no detailed traces, so the decisive moves are unverified.
- Keep Reward v1 active and keep the readiness candidate for follow-up. Proposed diagnostic: record detailed traces for the four flipped pairs (eight games) to see when allocating first displaced summoning or a defensive choice. Treat this as explanation on the existing seeds. Validate any selected variant on independent seeds and all original opponents before promotion. Receipts: `SimulationResults/LearnedFundamentals/attack-readiness-v1/attack-readiness.result.json` and `SimulationResults/LearnedFundamentals/v1/reward-v1.result.json`.

## 2026-11-20 - Direct full-turn coaching diagnostic

- Compared the two existing detailed readiness/control pairs from the same decisions using legal actions and actor-visible observations. In `s0_seed2026112561_leg1`, Reward v1 used Bloodfang Raider as tribute for Fire Dragon, developed the board and funded Fire Dragon's attack. It recorded a KO and 500 LP damage. The readiness candidate funded Bloodfang Raider first, later tributed Bastion Keeper for Fire Dragon, and declined to attack in Battle. Bloodfang Raider still had three legal attacks, but its 850 ATK was insufficient to KO any visible target in that battle state. Both versions lost this match. The sequence identifies a local tradeoff between immediate readiness and the attack enabled by a complete turn plan.
- Also screened all 192 verified reward-v3 replays for allocations to a monster later tributed before attacking in the same turn. Learner vs Hard had 5/293 such allocations; the exact subsequent summon was already legal at the allocation decision in two. Hard had 0/318. This frequency supports a targeted coaching example, with limited coverage as a standalone improvement.
- Proposed a coach interface that exposes the acting player's legal view at selected decisions, accepts a legal choice or a short turn plan, and lets the simulator play the remainder across seeds. Prefer measuring complete Main-to-Battle plans from matched states before using demonstrations as training labels. The current runner selects registered bots internally; a live coach input channel would require implementation.

## 2026-11-21 - Full-strength Hard integrated into PvE

- Added Hard to the existing Duel Mode menu and connected the standard 96-iteration HardBot to the match, result screen and history. Hard searches an isolated state in a worker task. The Unity thread applies the original legal action only if the same session and action count are still current. Hard Auto mode uses a separate bot instance with the appropriate own-deck knowledge for each seat.
- C# syntax and diff checks passed. Unity Play Mode logs show accepted Hard moves in both seats, including phase changes, card activations and attacks. To check the presentation, open AppShell and select Duel, then Hard Bot. Verify that the board remains responsive on a Hard turn and that the result and history list Hard Bot.
