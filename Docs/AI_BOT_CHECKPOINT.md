# AI / bot checkpoint â€” 2026-11-17

Prepared against `4618e4a` (Integrate approved card artwork and record Editor acceptance).
This is the current review entry point; older roadmap entries are historical.

Historical comparison: [H0–H3 evidence guide](AI_BOT_HISTORY.md). Ready-to-copy review instructions: [Claude prompt](CLAUDE_HARD_REVIEW_PROMPT.md).

Historical comparison: [H0–H3 evidence guide](AI_BOT_HISTORY.md). Ready-to-copy review instructions: [Claude prompt](CLAUDE_HARD_REVIEW_PROMPT.md).

## Current result and scope

- Unity **2022.3.62f3**, 41 cards: 21 Monster / 17 Spell / 3 Trap.
- Three playable 30-card decks: Starter Balance v4, Crimson Assault, Aegis Bastion.
- Two additional collection decks exist only for simulation; playable presets/rules were not rebalanced.
- Latest Hard is **H3**: bounded single-observer ISMCTS plus tactical and strategic evaluation.
- Latest complete benchmark: **72 wins / 48 losses against Normal, 120 matches (60%)**.
- The >=75% target against Normal has **not** been achieved. These are development seeds, one deck.
- AI/ML has a v2 data contract, verified pilot exports, encoder and data preparation; **no trainer, trained model or inference integration yet**.
- No new gameplay edits, matches, installs or cloud uploads were performed while preparing this checkpoint.

## Read only what the task needs

1. Read [AGENTS.md](../AGENTS.md), then [HANDOFF.md](HANDOFF.md).
2. For Hard, start with the three files below and the benchmark summary.
3. For ML, read the v2 contract, encoder/preparation scripts and smoke configuration.
4. Read relevant game rules/effect definitions before proposing a tactical change.
5. Keep all pending work. No Core/game-rule/card/Easy/Normal edits without specific user approval.
6. Long simulation batches are prepared by the assistant and launched by the user.

| Purpose | Source / entry |
| --- | --- |
| Hard search, budget, reward, hidden-state sampling calls | [HardBot.cs](../Assets/Scripts/Bots/HardBot.cs) |
| Energy commitments, posture, attacks, tributes | [HardTacticalPolicy.cs](../Assets/Scripts/Bots/HardTacticalPolicy.cs) |
| Hand + board upgrade plans, draw engines, prospective passives | [HardStrategicEvaluation.cs](../Assets/Scripts/Bots/HardStrategicEvaluation.cs) |
| Exact fork / information-set fork | [GameState.Search.cs](../Assets/Scripts/Core/GameState.Search.cs) |
| Observation adapter / Normal reference | [IStateObservingBot.cs](../Assets/Scripts/Bots/IStateObservingBot.cs), [NormalBot.cs](../Assets/Scripts/Bots/NormalBot.cs) |
| Match execution and diagnostics | [SimulationEngine.cs](../Assets/Scripts/Simulation/SimulationEngine.cs), [Run-HardBatch.ps1](../Tools/Simulation/Run-HardBatch.ps1) |
| AI observation/action/reward contract | [SimulationAiContract.cs](../Assets/Scripts/Simulation/SimulationAiContract.cs) |
| Public-policy feature encoder | [action_features.py](../Tools/Training/action_features.py) |
| Dataset checks and seed split | [prepare_imitation_data.py](../Tools/Training/prepare_imitation_data.py), [Validate-AiDataset.ps1](../Tools/Simulation/Validate-AiDataset.ps1) |
| Encoder tests / proposed neural baseline | [test_action_features.py](../Tools/Training/test_action_features.py), [smoke_train_config.json](../Tools/Training/smoke_train_config.json) |
| Historical decisions, glossary, validation records | [AI_BOT_ROADMAP.md](AI_BOT_ROADMAP.md) â€” consult relevant sections only |

## Changes since the base commit

- Add Hard search, tactical policy, strategic evaluation and the shared state-observation interface.
- Add isolated state/chain/player/card/RNG copying and hidden-state sampling for search. Existing read-only public accessors support AI features and battle history. These earlier foundation additions are included in the pending diff; no new rule changes were made during H1-H3 tuning.
- Normal's pending diff adds the observation interface and exposes its existing score internally for Hard guidance. Its tactical scoring was not tuned during the Hard experiments. Easy and GameRules have no pending edits.
- Expand AI schema v1 to v2: 21 global features, 18 per entity, public retaliation marks with local entity indices; hidden opponent hand, seed and deck order stay outside policy inputs.
- Extend non-UI simulation with Hard selection, profile/decision diagnostics, versioned output paths, resource reporting, runner archives and replay/export helpers.
- Add Editor tools for pilot/replay/resources, expansion AI checks, state-fork verification and Hard verification.
- Add simulation-only collection configs, streaming dataset validation, public-policy encoder/preparation scripts and six encoder tests.
- Update the simulation catalog to include the collection presets. Generated catalog fingerprints do not establish that an old Unity Player contains latest H3; rebuild when required.
- Preserve original simulation outputs locally. Publish compact evidence below instead of binaries or full datasets.

## Hard method and current limits

- Default budget: 96 search iterations, tree depth 6, up to 64 rollout actions per iteration, turn horizon +2.
- Horizon +2 stops at the next own Main boundary; it does **not** play out that full Main Phase.
- Search uses sampled hidden information, known own starting-deck composition and independent simulation RNG, never the true hidden shuffle/opponent hand as root policy input.
- All simulated and real actions go through actual GameRules. Response/trigger roots currently use explicit Normal fallback.
- H3 fixes prospective hand-card passives (including summon-turn attack restriction, piercing and lifesteal) and scores feasible hand/board tribute packages plus sustainable draw.
- High-rank priority is an estimate, not permission to ignore material, Energy, survival or useful support effects.
- Search depth counters count interactive actions, not turns; aggregate rollout actions are not a single trajectory depth.
- Unity UI integration and a newly rebuilt full Player containing latest H3 are not validated by the standalone Mono benchmark alone.

## Experiments recorded so far

| Version / opponent | Scope | Hard result |
| --- | --- | --- |
| H0 / Easy | 60 paired seeds, Starter Balance, 120 matches | 99/120 (82.5%) |
| H1 / Easy | Same 120 development matches | 89/120 (74.17%) |
| H2 / Easy | Same 120 development matches | 101/120 (84.17%) |
| H3 / Easy | Only five selected H0-win/H2-loss regressions | 3/5 recovered; no full H3/Easy rate |
| **H3 / Normal** | **Same 60 paired seeds, 120 matches** | **72/120 (60%)** |

- Normal benchmark seeds: 420001â€“420060; leg0 Hard seat0, leg1 Hard seat1; same deck both seats.
- Batches: 23/40, 27/40, 22/40. All 120 finished; zero errors, draws or truncations.
- Pair groups: 14 Hard double wins, 2 double losses (420047, 420049), 44 splits.
- Hard seat0: 32/60; seat1: 40/60. Going first: 41/60; second: 31/60.
- Among split pairs, the starting seat wins both legs in 27 seeds, the second-moving seat in 17.
- This design combines turn order and seat-specific initial hand/shuffle. Do not infer either one's isolated causal effect.

Published evidence:
- [Summary / interpretation](AI_BOT_EVIDENCE/h3-vs-normal/analysis.json)
- [All 60 paired classifications](AI_BOT_EVIDENCE/h3-vs-normal/pair-classification.json)
- [Exact H3 algorithm profile](AI_BOT_EVIDENCE/h3-vs-normal/hard-profile.json)
- [420041: Hard wins both seats](AI_BOT_EVIDENCE/h3-vs-normal/standout-420041.json)
- [420047](AI_BOT_EVIDENCE/h3-vs-normal/standout-420047.json) / [420049](AI_BOT_EVIDENCE/h3-vs-normal/standout-420049.json): Hard loses both seats
- [420040](AI_BOT_EVIDENCE/h3-vs-normal/standout-420040.json): neither bot at seat0 has a legal summon on turns2/4; seat1 wins both at full LP
- [420014](AI_BOT_EVIDENCE/h3-vs-normal/standout-420014.json): second-moving seat1 wins both at full LP
- [Evidence origin / SHA-256](AI_BOT_EVIDENCE/h3-vs-normal/evidence-manifest.json)

These condensed post-match records may reveal both hands. They are **not** model inputs, full replay proofs or train-ready data.

## AI / ML preparation

- Two verified pilot batches and simulation-only collection decks were prepared; observed coverage reaches all 41 cards, not proof of effect mastery.
- Prepared Normal imitation subset: 1,124 meaningful-choice samples (878 train / 246 validation); group whole matches and paired seeds together.
- Train seeds 310001â€“310003, validation 310004. No independent final test set yet.
- Encoder v1: 876 context features, 203 features per legal candidate; unknown cards map to UNK without guaranteeing good play.
- Neural baseline proposal only: two hidden layers 64/32, batch16, three epochs, CPU two threads, five-minute cap.
- No PyTorch trainer/Gym adapter/checkpoint/model inference is implemented. Normal is the current small-data teacher; Hard labels/DAgger are later options.
- New H3 experimental traces explicitly remain `replayVerified=false`, `trainingEligible=false`.
- Machine budget: Ryzen5500U, 8GB RAM; work on D. Do not install tools/train/upload until specifically authorized.

## Existing validation and reproducibility limits

- H3 standalone checks previously passed: 578 prototype checks, 51 searched decisions, 4,601 rule applications; additional H1 tactical10 + extra4, H2 turn-plan10, H3 strategy22 assertions.
- Six Python encoder tests previously passed. These are recorded prior results, not new tests run for this documentation checkpoint.
- Latest benchmark package and all120 result/trace/diagnostic completion hashes passed; 80 protected non-Hard source hashes stayed unchanged through the tuning experiments.
- `SimulationResults/` and `Tools/Simulation/Control/` remain ignored by Git; executables, raw traces, prepared data and local request/pointer files do not accompany a clone.
- `Run-HardBatch.ps1` needs a prepared frozen package. It cannot recreate the historical120 matches from a clean clone by itself. Local pointers still refer to preserved packages on D.
- Additional Mono-only H1/H2/H3 tactical cases currently live in the ignored historical `_runner/tests/` directory; Editor verification sources are included in Assets/Scripts/Editor.
- State-hash schema1 includes generic type assembly names. Across differently named runtime executables, raw hashes can differ despite equivalent graphs; use matching archived runtimes/fingerprints, not only rules/catalog version numbers.
- Full replay verification of Hard benchmark traces and portable packaging of extra tests/runners remain future work; neither is required to read/review current sources and the evidence above.

## Next review / implementation decisions

1. Review sustainable pressure and draw-engine preservation in both-loss seeds, using double-win cases as regression guards.
2. Compare changes to evaluation, action filtering and search horizon; do not blindly add more high-rank bias or per-seed/card-ID exceptions.
3. After user approval, prepare another frozen candidate and let the user launch paired batches. Include new seeds/decks before final strength claims.
4. Design a small imitation trainer/inference bridge separately, preserving the public-policy contract and reproducible data split.
5. Do not alter Core rules, card balance, Easy/Normal, Editor/packages, existing results, or commit/push without explicit authorization.
