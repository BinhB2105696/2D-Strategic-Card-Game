# HANDOFF - accepted rules v6 and AI collection v2
Updated 2026-11-19. Project: D:\Unity\Projects\2D-Strategic-Card-Game.
Read root AGENTS.md first. Preserve pending work; do not rerun completed collections or balance batches.

## Current goal
- Checkpoint accepted rules v6, rebuild from that commit, then freeze a clean rules-v6 AI collection plan.
- A failed normal, unskipped Draw step from an empty deck increments that player's exhaustion level and schedules `level * 100` LP damage.
- Apply the scheduled damage only when that player ends the turn. They keep the whole turn to win; if the opponent is already defeated, no exhaustion damage is applied.
- Effect draws and skipped Draw steps never increment exhaustion.

## Implemented and verified
- Core state/rules/events/config expose exhaustion level and pending damage and apply it at End step.
- Search-state copies and public bot views include both values; state hashing sees the new fields automatically.
- Rules/catalog/AI versions are 6/4/3. AI schema 3 has 25 global features; action types and entity/action dimensions are unchanged.
- Simulation verification covers turn-1 skip, effect draws, first/second exhaustion levels, delayed damage, lethal End step and effect-scheduled skip.
- Dataset validation and Python feature preparation accept both legacy schema 2/21 and schema 3/25 with explicit version checks.
- Rules v6 was accepted on 19 November 2026. The rebuilt runner passed simulation verification; no rules-v6 training collection or bot heuristic change has been run yet.

## First exact-seed result
- Deck 2, seed 530003, Hard player 0 vs Normal player 1 finished at turn 24 with Hard winning at 2700 LP; the rules-v4 counterpart lasted 94 turns and ended with Hard at 550 LP.
- End-turn exhaustion applied 100 then 200 to each player. Hard won by attack on turn 24 before its pending 300 damage, directly confirming the intended full-turn last chance.
- This is not a clean v4-v6 causal estimate because rules v6 also includes the accepted first-player turn-1 draw skip.
- The swapped-seat equivalent also finished: Hard was player 1/first and won on turn 13 at 5500 LP; no exhaustion occurred. Hard therefore won both seats for this seed.

## Acceptance batch
- Deck 2, seeds 510101-510110, both seats: 20/20 finished under both rules v5 and v6.
- Hard wins changed 14/20 -> 16/20; first-player wins stayed 10/20. Mean turns changed 19.5 -> 18.7 and maximum 45 -> 33.
- Exhaustion appeared in 10/20 matches and ended 4; the user accepted this targeted pressure on long games.

## Preserved existing evidence
- Rules v5 reduced first-player wins from 63.5% to 53.0% across 200 paired matches while Hard stayed stable at 59.5% -> 61.0%.
- The completed rules-v4 dataset has 280 matches/43,297 transitions and remains baseline/pretraining only.
- The extreme baseline NLL came from one 94-turn match where Normal exhausted deck by turn 17 and Hard sustained a Field Medic lock before winning.
- Old rules-v4/v5 data and models must not be silently mixed with rules v6.

## Next step
1. Run targeted checks, commit/push only the rules-v6 and pipeline files, preserving unrelated local work.
2. Rebuild the runner from the committed checkpoint, then prepare frozen `hard_normal_main_v2_<commit>`.
3. Run and inspect one v2 shard before scheduling the remaining 13 shards.
4. After all shards validate, prepare separate mixed, Hard-only and Normal-only v2 datasets; never merge v1 silently.

## Important working-tree notes
- Preserve the separate local `Assets/Scripts/Editor/HardBotVerification.cs` change.
- Preserve old untracked detailed handoff files and `Tools/Training/diagnose_checkpoint.py`.
- The user explicitly requested the rules-v6 checkpoint and AI-training continuation on 19 November 2026.

## Quick verification
```powershell
.\Tools\Simulation\Build-TrainingRunner.ps1
SimulationResults\TrainingEnvironment\python312_cpu\Scripts\python.exe -m unittest Tools.Training.test_action_features Tools.Training.test_train_imitation
```
