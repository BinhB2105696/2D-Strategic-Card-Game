# HANDOFF - Hard freeze and learned-bot foundation
Updated 2026-11-18. Project: D:\Unity\Projects\2D-Strategic-Card-Game.
Read root AGENTS.md first. Preserve pending work; do not rerun completed batches.

## Current goal
- Freeze the current Hard milestone and start a reproducible learned-bot pipeline.
- Keep Easy < Normal < Hard as same-deck difficulty evidence; learned policy is a separate algorithm family.
- User performs Git commit/push and long external Player runs; agent must not commit automatically.

## Hard checkpoint
- Published base remains commit `933a50e`; current Hard changes are not committed yet.
- Frozen best broad teacher runtime is H5.2 (`ismcts-H5.2-legacy-fallback-v1`): 73/120 vs Normal on seeds 420061-420120.
- Current source is H5.4: H5.2 primary plus sequential fallback; targeted 16 had 0 action-digest differences from H5.2.
- 360-match same-deck round robin: Easy 48/240, Normal 139/240, Hard H5.2 173/240.
- Pair rates: Normal 92/120 vs Easy; Hard 100/120 vs Easy; Hard 73/120 vs Normal.
- Pending Hard files include HardBot.cs, HardTacticalPolicy.cs and Editor verification access metadata.

## Learned-bot foundation completed
- Python 3.12 venv: `SimulationResults/TrainingEnvironment/python312_cpu` on D.
- PyTorch 2.15.0+cpu, NumPy 2.5.5; no CUDA. Eight Python tests PASS.
- Public-policy encoder v1: context 876, candidate 203; AI contract schema 2.
- Shared candidate MLP 64/32, cross-entropy behavior cloning, checkpoint/resume and independent evaluator.
- Normal smoke: 878 train/246 validation; top-1 52.4%, top-3 87.4%, loss 1.272 after 3 epochs.
- Colab bundle is portable public-policy data+trainer only; no Unity/raw traces/private hands.
- Current smoke datasets have no independent test; accuracy is not game win rate.

## Verified Hard-teacher pilot
- Config: seeds 510001-510002, three playable decks, Hard-Normal, both seats, 12 matches.
- Run `20261118_001903_a1df04d5`: 12/12 Finished; Hard 5, Normal 7; pipeline-only sample.
- Replay `20261118_003345_powershell_0633d1ab`: 12/12 verified, 1,402 transitions.
- Validation `20261118_003811_3ec9f188`: PASS 1,690,832 checks; Hard 713, Normal 689.
- Hard-only meaningful samples: 282 train (510001), 291 validation (510002); 140 forced excluded.
- Hard smoke: validation top-1 29.2%, top-3 59.1%, loss 1.916; no strength claim.
- Source revision in the built runner is still 933a50e, but source fingerprint locks the pending working tree.

## Next
- User checkpoint commit/push first; do not collect the main dataset from an uncommitted source.
- Then allocate fresh grouped ranges for train/validation/test; never use tuned 420001-420120 as final test.
- Collect roughly 20k-50k meaningful choices with verified Hard/Normal labels before strength evaluation.
- Train teacher ablations (Hard-only, Normal-only, mixed) and three model seeds; integrate game inference later.

## Quick checks only when needed
- `py -3.12 -B -m unittest discover -s Tools/Training -p test_*.py`
- `.\Tools\Simulation\Build-TrainingRunner.ps1`
- `.\Tools\Simulation\Run-HardTeacherPilot.ps1`
- `Tools/Training/README.md` has replay, validation and training commands.
