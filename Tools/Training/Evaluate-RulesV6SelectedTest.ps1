param([string]$RunRoot = '')

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$python = Join-Path $repo 'SimulationResults\TrainingEnvironment\python312_cpu\Scripts\python.exe'
if (!$RunRoot) {
    $RunRoot = (Get-ChildItem (Join-Path $repo 'SimulationResults\TrainingRuns') -Directory -Filter 'rules-v6-imitation_*' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}
$RunRoot = [IO.Path]::GetFullPath($RunRoot)
$selectionPath = Join-Path $RunRoot 'selection.json'
$output = Join-Path $RunRoot 'test_evaluation.json'
if (Test-Path -LiteralPath $output) { throw "Reserved test was already evaluated: $output" }
$selection = Get-Content -LiteralPath $selectionPath -Raw | ConvertFrom-Json
if ($selection.status -ne 'selected' -or $selection.testEvaluated -or @($selection.selected).Count -ne 3) {
    throw 'Selection is missing, invalid or already marked evaluated.'
}
$results = @(
    foreach ($item in $selection.selected) {
        $json = & $python (Join-Path $PSScriptRoot 'evaluate_checkpoint.py') --dataset $item.datasetPath `
            --checkpoint $item.checkpoint --split test
        if ($LASTEXITCODE -ne 0) { throw "Test evaluation failed: $($item.dataset)" }
        $metric = $json | ConvertFrom-Json
        [ordered]@{dataset=$item.dataset;seed=[int]$item.seed;checkpoint=$item.checkpoint;
            datasetManifestSha256=$item.datasetManifestSha256;metrics=$metric}
    }
)
$report = [ordered]@{schema=1;status='completed';rulesVersion=6;catalogVersion=4;aiSchema=3;
    evaluatedOnce=$true;selectionCriterion=$selection.criterion;results=$results}
[IO.File]::WriteAllText($output,($report|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
Write-Output "Reserved test evaluation completed once: $output"
