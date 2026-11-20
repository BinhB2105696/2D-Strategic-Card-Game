# HANDOFF - choose the next rules-v6 LearnedBot training target
Updated 2026-11-20. Project: `D:\Unity\Projects\2D-Strategic-Card-Game`.
Read root `AGENTS.md`, then this file only. Preserve pending work and do not rerun completed collection/training/test.

## Current result and next decision
- Critic evaluation finished 288/288 on 12 fresh seeds. V1 -> critic wins: Easy 29/48 -> 30/48, Normal 19/48 -> 16/48, Hard 17/48 -> 17/48. Paired Hard change 0 pp (95% −8.33 to +8.33). Overall −1.39 pp (95% −5.56 to +2.78). Keep reward v1 as the active training baseline and pause this reward/GAE route. Receipts: `SimulationResults/LearnedCheckpointComparison/reward-v3-critic-development/`.
- The ridge critic improved held-out outcome MSE from 0.8675 to 0.5522, but its GAE update did not yield stronger play. Preserve its artifact at `SimulationResults/LearnedReward/v3/critic-candidate/` and negative results. Reward v3 also showed no gain; see the experiment log.
- Tactical diagnostic complete on 192 verified games sampled from reward v1. Across 45 certified one-action win states, greedy v1 skipped 13 chances in 12 games. All 12 were ultimately won. Against Hard, both skipped chances occurred in wins and no prior-turn removal of the eventual lethal attacker was identified by the narrow check. Skip fine-tuning on these labels.
- Against Hard, 36 of 45 collection losses ended by direct attack; in 32/36, the learner was already without a monster by its previous Main. Of those 36, 25 exited that Main empty with no legal summon. Investigate earlier loss of board presence and resources before choosing another supervised target. No collection, training, or gameplay pending.

## Current priority and fairness
- Improve the learned model itself. Goal: high win rates against fixed Easy/Normal and a real edge above 50% against original full-strength Hard, on independent swapped-side seeds across decks. HardLow is a diagnostic benchmark.
- Preserve opponent strength, rules and cards. Train/evaluate from player-visible and owned information and legal candidates. No hidden cards/order or match seed as policy input. Keep old datasets and holdouts for reproducibility.

## Current checkpoint
- Rules-v6/pipeline checkpoint `41057b2` is pushed to `origin/main`; rules/catalog/AI are `6/4/3`.
- Rules v6: first player skips only the first draw; a later failed normal Draw step schedules escalating `n*100` LP damage at that player's End step. Effect/skip draws are exempt.
- `Docs/AI_EXPERIMENT_LOG.md` is the append-only evidence log; update it before checkpoints and preserve negative results/corrections.

## Hand-coded bots and evidence
- Easy uses simple priority rules. Normal uses public-state heuristics plus local attack forecast. Hard is an information-set search hybrid with tactical filters, rollouts and strategic evaluation.
- Hard reuses Normal only as rollout guidance/opponent policy/fallback; its selection algorithm and search state are distinct, not “Normal at higher depth”.
- Historical H0/H1/H2 vs Easy: `99/120`, `89/120`, `101/120`; H3 vs Normal: `72/120` on development seeds and `74/120` on holdout.
- H4/H4.1/H5 vs Normal: `73/120`, `70/120`, `76/120` development; H5 fell to `68/120` holdout. H5.2 was retained after targeted comparisons.
- Local H5.2 round-robin: Normal-Easy `92-28`, Hard-Easy `100-20`, Hard-Normal `73-47`. These summaries are not replay-verified or training-eligible.

## Rules-v6 data and training complete
- Frozen plan `hard_normal_main_v2_41057b2`: 14/14 shards, 280/280 replay-verified episodes, 42,298 transitions, zero validation errors.
- Prepared mixed `23,898/4,806/5,101`, Hard-only `12,265/2,446/2,576`, Normal-only `11,633/2,360/2,525` train/validation/test.
- Nine CPU ablations completed in 59.39 min. Validation selected seed `20261119` for all three before test was read.
- Reserved test was evaluated once: Hard-only loss/top-1 `1.350/53.73%`, Normal-only `0.810/72.67%`, mixed `1.110/63.32%`.
- These are teacher-action imitation metrics, not win rates. Hard remains the stronger hand-coded bot under the direct round-robin protocol.
- Mixed is the primary LearnedBot candidate; teacher-only checkpoints remain ablations. Do not tune against the consumed test split.
- Selection/test artifacts: `SimulationResults/TrainingRuns/rules-v6-imitation_20261119_083127/{selection.json,test_evaluation.json}`.

## LearnedBot v1 complete
- Frozen mixed epoch 13 exported under `SimulationResults/TrainingRuns/rules-v6-imitation_20261119_083127/learned-policy-v1/`. See `export.json` for the feature spec and hashes.
- `LearnedBot.cs`, `LearnedPolicy.cs` and `LearnedPolicyEncoder.cs` run the policy in the headless simulator using the schema-3 public observation and legal actions. Python/C# parity passed 19/19 indices and 125 logits (max absolute error `5.72e-6`).
- Holdout `SimulationResults/Runs/rules-v6/catalog-v4/ai-v3/20261119_114929_21caad23`: 90/90 finished. Learned wins vs Easy/Normal/Hard: `13/30, 10/30, 9/30`. Five seeds were reused across deck assignments. No finished traces were retained. Keep Learned as a supervised benchmark.

## Easy diagnostic and next step
- Development run `SimulationResults/Runs/rules-v6/catalog-v4/ai-v3/20261119_122645_86101ba7`: 3/4 wins across two seeds, all four traces replay-verified, 158/158 Learned choices with multiple legal actions matched Python argmax.
- Review case `s0_seed2026111911_leg1`, sequence 18: Learned entered Battle without a legal attack despite legal allocations that met two attack requirements. One win also contained an allocation opportunity when advancing. No recurring tactical mistake is established on this sample.
- Teacher diagnostic `SimulationResults/Runs/rules-v6/catalog-v4/ai-v3/20261119_123755_a53504e4`: 5/6 wins vs Easy on three fresh development seeds. Replay verified 6/6. All 150 eligible teacher comparisons have valid indices; Hard searched on all 150 and reported no fallback.
- Exact Learned/Normal and Learned/Hard agreements: Main `72/121` and `58/121`, Battle `22/29` and `21/29`. Of 86 same-choice Normal/Hard states, Learned differed on 21. Only one was Learned advancing where both teachers would allocate, in a win. No concentrated error warrants an allocation rule or a weight change.
- Offline rescoring on the same 150 states: mixed matched Normal/Hard `94/79`, Normal-only `102/65` and Hard-only `78/82`. Mixed matched all 150 runtime choices. No exact within-state encoder collisions among 1,363 candidates in this early Main/Battle sample.
- Frozen Normal-only and Hard-only passed 19/19 parity indices each. Development comparison finished 288/288: mixed `45/96`, Normal-only `35/96`, Hard-only `37/96`. The paired 95% interval for Hard-only minus mixed included zero. Receipts: `SimulationResults/LearnedCheckpointComparison/v1-development/`.
- Confirmation finished: mixed `43/96` vs Hard-only `28/96` on Easy/Normal, `+15.6` pp (paired 95% interval `+8.3 to +22.9`). Separate mixed vs Hard: 7 wins, 16 losses, 1 timeout. Receipts: `SimulationResults/LearnedCheckpointComparison/v1-confirmation/`.
- DAgger collection recovered after a watchdog timeout: actual run finished 48/48 and all replays verified. Receipt: `SimulationResults/LearnedDagger/v1/collection.json`. The launcher now rechecks the process and heartbeat before stopping.
- Fine-tuned from mixed v1 on 436 consensus training labels and original train samples. Epoch 4 selected on 205 validation labels (top-1 `64.39% -> 72.20%`). Candidate `SimulationResults/LearnedDagger/v1/candidate/` passed 19/19 Python/C# parity indices.
- DAgger evaluation finished 192/192: DAgger `44/96`, mixed v1 `42/96`; paired 95% difference `−6.3 to +11.5` pp on 12 fresh seed blocks. Keep mixed v1 active. Receipts: `SimulationResults/LearnedCheckpointComparison/dagger-v1-evaluation/`.
- Offline shortlist check: on Normal validation trajectories with more than five legal choices, mixed top-5 retained Hard's action `109/134` times, falling to `53/77` when teachers differed. Adding Normal's choice recovered one case. Keep a tactical shortlist alongside model ranking.
- Search comparison complete versus Normal: 48/48 smoke and 576/576 primary matches. On 48 primary seeds, mixed v1 `71/192`, HardLow `102/192`, LearnedSearch `104/192`. Receipts: `SimulationResults/LearnedSearchExperiment/v1/`. Do not rerun this completed evaluation.
- HardLow and LearnedSearch both use at most 24 root simulations and 8 candidate groups. Both retain up to three tactical choices, including Normal's suggestion if it survives the filter. LearnedSearch uses mixed v1's ranking for remaining slots in open Main/Battle through turn 8 when the shortlist needs pruning. Raw decision timings are retained for pooled P95.
- LearnedSearch minus HardLow `+1.04` pp (paired 95% interval `−2.08 to +4.17`); P95 `224.39 vs 223.69 ms` (+0.31%). Hybrid missed the gameplay criterion. HardLow beat mixed v1 by `+16.15` pp (paired 95% `+10.42 to +22.40`) and is the supported search baseline.
- Paired rollout pilot trained: the four smoke action digests matched and all 48 collection games replay-verified. Retained 439 train and 108 validation pairs from 301 scored states. Epoch 4 improved pair validation accuracy from 58.33% to 62.96%; parity passed 19/19. Candidate: SimulationResults/LearnedRollout/v1/candidate/.
- Paired rollout evaluation complete 384/384: mixed v1 38/128, paired-rollout 40/128, HardLow 76/128. Paired-rollout minus mixed +1.56 pp (95% −4.69 to +8.59). Retain mixed v1 and HardLow as baselines. Receipts: SimulationResults/LearnedCheckpointComparison/paired-rollout-v1-evaluation/.
- Learner-state collection complete: 144/144 replay-verified over 36 seeds. Of 931 comparisons, 415 exact agreements and 516 scored. Stable HardLow-versus-mixed preferences: train 87 higher/19 lower, validation 32/4. HardLow had more higher than lower labels across 24/27 train seeds and 9/9 validation seeds.
- Targeted learner-state fine-tune complete on 106 direct pairs plus original samples. Selected epoch 8: recovered 7/32 HardLow-favoured cases and retained 4/4 mixed-favoured cases on validation. Original validation top-1 fell from 64.02% to 62.75%; candidate is experimental. C# parity passed 19/19.
- Targeted learner-state evaluation complete 384/384: mixed v1 62/192, candidate 63/192, paired +0.52 pp (95% −4.69 to +5.73). P95 3.84 vs 3.64 ms. Keep mixed and HardLow as baselines. Receipts: SimulationResults/LearnedCheckpointComparison/learner-rollout-v1-evaluation/.
- Horizon diagnostic complete 24/24 replay-verified. Among 74 HardLow-vs-mixed disagreements, 23 short preferences were stable; at eight turns 14 kept direction, nine became unclear and none reversed stably. Of 592 paired worlds, both actions finished in 209, with the same winner in all 209.
- Terminal diagnostic complete 48/48 replay-verified. Both branches finished in 1,078/1,080 paired worlds. Complete outcomes: HardLow 26 better, 25 worse, 1,027 equal. Stable decisions 4 better/3 worse; 2/12 net-positive seeds. All seven stable cases on deck 1.
- Full-game reward collection complete 192/192 replay-verified. Initial PowerShell display stalled at 150, but Unity finished all 192 and data was recovered without rerunning games.
- Reward v1 development: Easy 52/96 -> 58/96, Normal 37/96 -> 39/96; paired aggregate +4.17 pp (95% +0.52 to +7.81). Full-strength Hard probe: mixed 12/32, reward v1 13/32; paired +3.13 pp (95% −6.25 to +12.50). All opponents and fingerprints verified.
- Reward v2 evaluation complete 288/288. Reward v1 -> v2: Easy 30/48 -> 28/48, Normal 20/48 -> 16/48, Hard 13/48 -> 12/48. Aggregate −4.86 pp (95% −8.33 to −1.39). Keep reward v1; opponent strengths and fingerprints verified.

## Working tree / preservation
- Learned inference source, exporter, parity harness, holdout config and current documentation updates are uncommitted.
- Preserve and exclude the separate local `Assets/Scripts/Editor/HardBotVerification.cs` change.
- Preserve old untracked `Docs/HANDOFF_2026-11-02.md`, `Docs/HANDOFF_DETAILED_2026-11-17.md`, and `Tools/Training/diagnose_checkpoint.py`.
- Do not commit ignored `SimulationResults`; paths above are local evidence.

## Quick checks
```powershell
Get-ChildItem .\SimulationResults\LearnedCheckpointComparison\reward-v3-critic-development\*.result.json
git diff --check -- Docs
```
