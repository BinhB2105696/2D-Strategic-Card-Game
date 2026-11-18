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
