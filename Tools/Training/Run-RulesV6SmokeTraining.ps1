param([string]$Output = '')

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$python = Join-Path $repo 'SimulationResults\TrainingEnvironment\python312_cpu\Scripts\python.exe'
$dataset = Join-Path $repo 'SimulationResults\Datasets\hard_normal_main_v2\normal_only'
if (!(Test-Path -LiteralPath $python -PathType Leaf)) { throw 'Training environment is missing. Run Setup-TrainingEnvironment.ps1.' }
if (!(Test-Path -LiteralPath (Join-Path $dataset 'training_manifest.json') -PathType Leaf)) { throw "Prepared rules-v6 dataset missing: $dataset" }
if (!$Output) { $Output = Join-Path $repo ('SimulationResults\TrainingRuns\rules-v6-smoke_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')) }
& $python (Join-Path $PSScriptRoot 'train_imitation.py') --dataset $dataset `
    --config (Join-Path $PSScriptRoot 'smoke_train_config.json') --output $Output
if ($LASTEXITCODE -ne 0) { throw 'Rules-v6 smoke training failed.' }
Write-Output "Rules-v6 smoke training completed: $Output"
