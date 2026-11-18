param(
    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$python = Join-Path $repoRoot 'SimulationResults\TrainingEnvironment\python312_cpu\Scripts\python.exe'
if (-not (Test-Path $python)) { throw 'Run Setup-TrainingEnvironment.ps1 first.' }
if (-not $Output) {
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $Output = Join-Path $repoRoot "SimulationResults\TrainingRuns\normal_smoke_mlp_v1_$stamp"
}

& $python (Join-Path $PSScriptRoot 'train_imitation.py') `
    --dataset (Join-Path $repoRoot 'SimulationResults\Datasets\normal_smoke_v1_20261116') `
    --config (Join-Path $PSScriptRoot 'smoke_train_config.json') `
    --output $Output
if ($LASTEXITCODE -ne 0) { throw 'Smoke training failed.' }

Write-Output "Training output: $Output"
