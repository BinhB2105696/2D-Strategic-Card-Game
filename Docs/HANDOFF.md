# HANDOFF - integrate the frozen rules-v6 LearnedBot candidate
Updated 2026-11-19. Project: `D:\Unity\Projects\2D-Strategic-Card-Game`.
Read root `AGENTS.md`, then this file only. Preserve pending work and do not rerun completed collection/training/test.

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

## Next implementation
1. Export the frozen mixed MLP weights and encoder spec to a versioned local artifact; do not change checkpoint selection.
2. Implement deterministic C# encoder/MLP inference without hidden-state leakage or a new external package.
3. Verify Python-C# logits and selected legal-action index on fixed policy fixtures.
4. Add LearnedBot only after parity passes; then compare it with Easy/Normal/Hard on untouched gameplay seeds.
5. Consider DAgger/self-play only after the supervised LearnedBot runs correctly; imitation accuracy alone is not gameplay strength.

## Working tree / preservation
- New AI scripts/config and `Docs/AI_EXPERIMENT_LOG.md` are uncommitted and should be checkpointed now; `Docs/HANDOFF.md` is modified.
- Preserve and exclude the separate local `Assets/Scripts/Editor/HardBotVerification.cs` change.
- Preserve old untracked `Docs/HANDOFF_2026-11-02.md`, `Docs/HANDOFF_DETAILED_2026-11-17.md`, and `Tools/Training/diagnose_checkpoint.py`.
- Do not commit ignored `SimulationResults`; paths above are local evidence.

## Quick checks
```powershell
SimulationResults\TrainingEnvironment\python312_cpu\Scripts\python.exe -B -m unittest Tools.Training.test_action_features Tools.Training.test_train_imitation
git diff --check -- Docs Tools/Training
```
