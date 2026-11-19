param([string]$RunRoot = '')

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (!$RunRoot) {
    $RunRoot = (Get-ChildItem (Join-Path $repo 'SimulationResults\TrainingRuns') -Directory -Filter 'rules-v6-imitation_*' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}
$RunRoot = [IO.Path]::GetFullPath($RunRoot)
$output = Join-Path $RunRoot 'selection.json'
if (Test-Path -LiteralPath $output) { Write-Output "Existing frozen selection: $output"; exit 0 }
$summary = Get-Content -LiteralPath (Join-Path $RunRoot 'summary.json') -Raw | ConvertFrom-Json
if ($summary.status -ne 'completed' -or $summary.rulesVersion -ne 6 -or $summary.catalogVersion -ne 4 -or
    $summary.aiSchema -ne 3 -or $summary.testEvaluated) { throw 'Run summary is not an untouched completed rules-v6 ablation.' }
$selected = @(
    foreach ($group in @($summary.runs | Group-Object dataset)) {
        $best = $group.Group | Sort-Object {[double]$_.validationLoss}, {[int]$_.seed} | Select-Object -First 1
        $manifest = Get-Content -LiteralPath (Join-Path $best.run 'run_manifest.json') -Raw | ConvertFrom-Json
        $metric = Get-Content -LiteralPath (Join-Path $best.run 'metrics.jsonl') | ForEach-Object { $_ | ConvertFrom-Json } |
            Where-Object {[int]$_.epoch -eq [int]$best.bestEpoch} | Select-Object -First 1
        [ordered]@{dataset=$best.dataset;seed=[int]$best.seed;bestEpoch=[int]$best.bestEpoch;
            validationLoss=[double]$best.validationLoss;validationTop1=[double]$metric.validation.top1;
            validationTop3=[double]$metric.validation.top3;run=$best.run;checkpoint=(Join-Path $best.run 'best.pt');
            datasetPath=(Join-Path $repo ('SimulationResults\Datasets\hard_normal_main_v2\'+$best.dataset));
            datasetManifestSha256=$manifest.dataset_manifest_sha256}
    }
)
$selection = [ordered]@{schema=1;status='selected';rulesVersion=6;catalogVersion=4;aiSchema=3;
    criterion='minimum validation loss only; seed breaks exact ties';testEvaluated=$false;
    metadataCorrection='Training inherited a stale rules-v4 status label from baseline_train_config.json. Dataset hashes, feature specs, summary versions and checkpoints are rules-v6; weights were not changed.';
    selected=$selected}
[IO.File]::WriteAllText($output,($selection|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
Write-Output "Frozen selection: $output"
