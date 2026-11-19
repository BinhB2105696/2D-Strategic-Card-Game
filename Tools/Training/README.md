# Learned-bot training foundation

The first baseline is a compact deep-learning policy: a shared MLP scores every legal action from public state features. It is trained by supervised behavior cloning with cross-entropy and backpropagation.

Local smoke run (PowerShell, repository root):

```powershell
.\Tools\Training\Setup-TrainingEnvironment.ps1
.\Tools\Training\Run-SmokeTraining.ps1
```

Outputs are append-safe: an existing directory is rejected unless `--resume` is supplied. The trainer verifies prepared dataset hashes, groups were already split by seed, masks padded candidates, saves best/last checkpoints, and records reproducibility metadata.

Evaluate a checkpoint without retraining:

```powershell
$python = 'SimulationResults\TrainingEnvironment\python312_cpu\Scripts\python.exe'
& $python Tools\Training\evaluate_checkpoint.py --dataset <dataset> --checkpoint <best.pt> --split validation
```

Run the rules-v4 baseline ablations (Hard-only, Normal-only and mixed; three seeds each):

```powershell
.\Tools\Training\Run-BaselineAblations.ps1
```

The baseline config uses early stopping and deliberately leaves the independent test split untouched. Select one checkpoint per dataset from validation results before evaluating test once. These models are baseline/pretraining artifacts for accepted rules v5, not final gameplay models.

This is pipeline validation, not the final LearnedBot. Next data collection needs new verified training seeds and an untouched test split; tuned seeds 420001-420120 must not become the final test set.

Verified Hard-teacher pilot (no visible Unity Editor):

```powershell
.\Tools\Simulation\Build-TrainingRunner.ps1
.\Tools\Simulation\Run-HardTeacherPilot.ps1
.\Tools\Simulation\Replay-AiBatch.ps1 -RunDirectory <pilot result path>
.\Tools\Simulation\Validate-AiDataset.ps1 -BatchDirectory <replay batch path>
```

The build step invokes Unity 2022.3.62f3 in hidden batch mode once. Later pilot/replay/validation commands use the built Player and PowerShell only.

Main verified collection is sequential and resumable:

```powershell
.\Tools\Simulation\Prepare-AiTrainingCollection.ps1
.\Tools\Simulation\Run-AiTrainingCollection.ps1 -MaxShards 1
```

The frozen v1 plan contains 14 shards/280 matches: 200 train, 40 validation and 40 test, using five equal-deck Hard-Normal matchups with both seats. Repeat the runner command to resume; increase `-MaxShards` only when the machine can be left running. After every shard validates, run `Prepare-AiTrainingDatasets.ps1` to create mixed, Hard-only and Normal-only ablations.

That 280-match collection is a rules-v4 baseline. Accepted rules v5 keeps the first Draw step but gives the first player no card on turn 1; a 200-match paired comparison reduced first-player wins from 63.5% to 53.0% while Hard stayed stable. Accepted rules v6 adds escalating End-step damage after a failed normal draw from an empty deck and advances the AI contract to schema 3 with 25 global features. Keep rules/schema versions separate; use older samples only in an explicit transfer-learning experiment with separate manifests.

The rules-v6 collection is `hard_normal_main_v2`: 14 shards/280 matches over five equal-deck Hard-Normal matchups with both seats. It reserves new seeds 610101-610120 for training, 620001-620004 for validation and 630001-630004 for final test. Prepare it only after rebuilding from the committed checkpoint; the default collection and dataset scripts then target v2 without modifying v1.
