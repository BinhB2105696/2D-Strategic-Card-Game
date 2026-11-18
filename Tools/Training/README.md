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

That 280-match collection is a rules-v4 baseline. Accepted rules v5 keeps the first Draw step but gives the first player no card on turn 1; a 200-match paired comparison reduced first-player wins from 63.5% to 53.0% while Hard stayed stable. Never merge v4 and v5 samples into one dataset without an explicit transfer-learning experiment and separate manifests.
