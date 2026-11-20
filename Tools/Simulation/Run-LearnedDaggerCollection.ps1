$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$configPath = Join-Path $PSScriptRoot 'learned-rules-v6-dagger-collection.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$expected = $config.seedCount * 2 * $config.matchups.Count
$policy = Join-Path $project $config.learnedPolicyPath
$export = Get-Content -LiteralPath (Join-Path (Split-Path $policy) 'export.json') -Raw | ConvertFrom-Json
$policyHash = (Get-FileHash -LiteralPath $policy -Algorithm SHA256).Hash.ToLowerInvariant()
if ($export.dataset -ne 'mixed_hard_normal' -or $policyHash -ne $export.artifactSha256 -or
    !$config.compareLearnedTeachers -or $config.detailedMatches -ne $expected) {
    throw 'DAgger collection requires frozen mixed v1 and teacher comparisons on every trace.'
}

$output = Join-Path $project 'SimulationResults/LearnedDagger/v1'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$receiptPath = Join-Path $output 'collection.json'
$configHash = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash
if (Test-Path -LiteralPath $receiptPath) {
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    if ($receipt.configSha256 -ne $configHash -or $receipt.policySha256 -ne $policyHash) {
        throw 'Existing DAgger collection receipt does not match this configuration or policy.'
    }
} else {
    & (Join-Path $PSScriptRoot 'Run-Simulation.ps1') -Config $configPath | Tee-Object -Variable simulationOutput
    $line = $simulationOutput | Where-Object { $_ -like 'Results: *' } | Select-Object -Last 1
    if (!$line) { throw 'Simulator did not report a collection directory.' }
    $receipt = [pscustomobject]@{
        runDirectory = $line.Substring('Results: '.Length).Trim()
        configSha256 = $configHash
        policySha256 = $policyHash
        replayReport = $null
    }
    $receipt | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding UTF8
}

$run = $receipt.runDirectory
$progress = Get-Content -LiteralPath (Join-Path $run 'progress.json') -Raw | ConvertFrom-Json
$matches = @(Import-Csv -LiteralPath (Join-Path $run 'matches.csv'))
if ($progress.state -ne 'completed' -or $progress.completed -ne $expected -or
    $matches.Count -ne $expected -or @($matches | Where-Object status -ne 'Finished').Count) {
    throw "Collection contains incomplete matches. Inspect $run. This receipt preserves the batch for diagnosis."
}
if (!$receipt.replayReport) {
    & (Join-Path $PSScriptRoot 'Replay-AiBatch.ps1') -RunDirectory $run | Tee-Object -Variable replayOutput
    $line = $replayOutput | Where-Object { $_ -like 'Replay batch: *' } | Select-Object -Last 1
    if (!$line) { throw 'Replay did not report an output directory.' }
    $receipt.replayReport = Join-Path ($line.Substring('Replay batch: '.Length).Trim()) 'batch-report.json'
}
$report = Get-Content -LiteralPath $receipt.replayReport -Raw | ConvertFrom-Json
if ($report.status -ne 'completed' -or $report.verifiedMatches -ne $expected -or $report.sourceRun -ne $run) {
    throw 'DAgger collection replay is incomplete.'
}
$receipt | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding UTF8
Write-Output "DAgger collection complete: $receiptPath"
