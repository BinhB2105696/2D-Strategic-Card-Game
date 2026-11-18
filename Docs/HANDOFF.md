# HANDOFF - accepted rules v5 and AI training continuation
Updated 2026-11-18. Project: D:\Unity\Projects\2D-Strategic-Card-Game.
Read root AGENTS.md first. Preserve pending work; do not rerun completed collections or balance batches.

## Current checkpoint
- Rules v5 is accepted: the first player's turn-1 Draw step resolves but draws no card; all later draws are normal.
- Rules/catalog/AI schema versions are 5/4/2. No action type or encoder dimension changed.
- The current headless runner passed build, simulation verification and replay verification before checkpointing.
- Current Hard is frozen. `HardBotVerification.cs` has a separate preserved local change and was not included in this checkpoint.

## Rules-v5 evidence
- Two matched batches, seeds 510101-510120, five simulation decks and both seats: 200/200 Finished.
- First-player win rate: rules v4 127/200=63.5%; rules v5 106/200=53.0%.
- Paired flips removed first-player advantage 35 times and added it 14 times; exact McNemar p≈0.0038.
- Hard win rate stayed stable: 59.5% -> 61.0%; paired gains/losses 26/23, p≈0.775.
- Hard first/second rates changed 73%/46% -> 64%/58%; gap fell from 27 to 6 percentage points.
- Mean turns 16.8 -> 17.0; mean Hard decision time 383.8 -> 334.3 ms; no timeout/error.
- Deck 3-4 are simulation-only collection/coverage decks, not official balanced playable presets.

## Completed rules-v4 dataset
- Plan `hard_normal_main_v1_32b700a`: 14/14 shards, 280/280 accepted matches, 43,297 transitions.
- Meaningful mixed: 24,967 train / 4,605 validation / 5,282 test; forced choices excluded: 8,443.
- Hard-only: 12,580 / 2,376 / 2,716. Normal-only: 12,387 / 2,229 / 2,566.
- All 41 public card IDs were observed across three playable plus two simulation-only coverage decks.
- Dataset: `SimulationResults/Datasets/hard_normal_main_v1/` (ignored by Git).
- This v4 data is baseline/pretraining only; never silently merge it with v5 data.

## Next
- Rebuild the runner once after the checkpoint so `build-info.json` records the new source revision.
- Train validation-only v4 behavior-cloning ablations: Hard-only, Normal-only and mixed, using three seeds each.
- Select using validation metrics; evaluate the untouched test split only for selected checkpoints.
- Treat these v4 models as baseline/pretraining evidence, then collect separate verified v5 data for fine-tuning/final models.
- Do not start DAgger/self-play or change Hard until the supervised baseline is reviewed.
