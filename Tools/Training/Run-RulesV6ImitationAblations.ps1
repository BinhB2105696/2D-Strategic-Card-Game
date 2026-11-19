param(
    [int[]]$Seeds = @(20261118, 20261119, 20261120),
    [string]$OutputRoot = ''
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$python = Join-Path $repo 'SimulationResults\TrainingEnvironment\python312_cpu\Scripts\python.exe'
$datasets = Join-Path $repo 'SimulationResults\Datasets\hard_normal_main_v2'
$baseConfig = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'rules_v6_train_config.json') -Raw | ConvertFrom-Json
if (!(Test-Path -LiteralPath $python -PathType Leaf)) { throw 'Training environment is missing. Run Setup-TrainingEnvironment.ps1.' }
if (!$OutputRoot) { $OutputRoot = Join-Path $repo ('SimulationResults\TrainingRuns\rules-v6-imitation_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$summaries = [Collections.Generic.List[object]]::new()
foreach ($name in @('hard_only','normal_only','mixed_hard_normal')) {
    $dataset = Join-Path $datasets $name
    if (!(Test-Path -LiteralPath (Join-Path $dataset 'training_manifest.json') -PathType Leaf)) { throw "Prepared dataset missing: $dataset" }
    foreach ($seed in $Seeds) {
        $run = Join-Path $OutputRoot ($name + '\seed_' + $seed)
        $configPath = Join-Path $OutputRoot ($name + '_seed_' + $seed + '.json')
        $config = $baseConfig.PSObject.Copy(); $config.seed = $seed
        [IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
        & $python (Join-Path $PSScriptRoot 'train_imitation.py') --dataset $dataset --config $configPath --output $run
        if ($LASTEXITCODE -ne 0) { throw "Training failed: $name seed $seed" }
        $manifest = Get-Content -LiteralPath (Join-Path $run 'run_manifest.json') -Raw | ConvertFrom-Json
        $best = & $python -c "import torch; c=torch.load(r'$($run.Replace("'","''"))\best.pt',map_location='cpu',weights_only=False); print(c['epoch'])"
        if ($LASTEXITCODE -ne 0) { throw "Could not read checkpoint: $name seed $seed" }
        $summaries.Add([ordered]@{dataset=$name;seed=$seed;bestEpoch=[int]$best;validationLoss=$manifest.final_metrics.best_validation_loss;completedEpoch=$manifest.final_metrics.completed_epoch;elapsedSeconds=$manifest.elapsed_seconds;run=$run})
    }
}
$summary = [ordered]@{schema=1;status='completed';rulesVersion=6;catalogVersion=4;aiSchema=3;
    usage='rules-v6 imitation ablations; select by validation only';testEvaluated=$false;runs=@($summaries)}
[IO.File]::WriteAllText((Join-Path $OutputRoot 'summary.json'), ($summary | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output "Rules-v6 imitation ablations completed: $OutputRoot"
