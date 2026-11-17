#Requires -Version 5.1
<#
Run the prepared, frozen Hard experiment. Default: all batches listed in its package.
Examples: .\Tools\Simulation\Run-HardBatch.ps1
          .\Tools\Simulation\Run-HardBatch.ps1 -Batch 1
          .\Tools\Simulation\Run-HardBatch.ps1 -ValidateOnly
Rerunning verifies and skips completed matches. Interrupted output is archived by
the runner before retrying that match; existing complete results are preserved.
#>
[CmdletBinding()]
param(
    [ValidateSet(1, 2, 3)][int[]]$Batch = @(1, 2, 3),
    [string]$RunDirectory,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $RunDirectory) {
    $pointer = Join-Path $PSScriptRoot 'Control/prepared-hard-batch.txt'
    if (-not (Test-Path -LiteralPath $pointer)) { throw 'No prepared Hard experiment. Ask the assistant to prepare one.' }
    $RunDirectory = (Get-Content -LiteralPath $pointer -Raw).Trim()
}
$runRoot = [IO.Path]::GetFullPath($RunDirectory)
$resultsPrefix = [IO.Path]::GetFullPath((Join-Path $projectRoot 'SimulationResults')) + [IO.Path]::DirectorySeparatorChar
if (-not $runRoot.StartsWith($resultsPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Run directory must be inside this project SimulationResults.' }
$package = Get-Content -LiteralPath (Join-Path $runRoot 'package.json') -Raw | ConvertFrom-Json
if ($package.schema -ne 1) { throw 'Unsupported prepared package schema.' }
$batchPlans = @($package.expectedBatches)
if (-not $package.expectedBatches) { $batchPlans = @(1..3 | ForEach-Object { [pscustomobject]@{ number = $_; plannedMatches = 40 } }) }
$plannedTotal = [int](($batchPlans | Measure-Object -Property plannedMatches -Sum).Sum)
if (-not $PSBoundParameters.ContainsKey('Batch')) { $Batch = @($batchPlans | ForEach-Object { [int]$_.number }) }
foreach ($number in $Batch) {
    if ($number -notin @($batchPlans | ForEach-Object { [int]$_.number })) { throw "Batch $number is not part of this prepared experiment." }
}
foreach ($item in $package.hashes.PSObject.Properties) {
    $file = [IO.Path]::GetFullPath((Join-Path $runRoot $item.Name))
    if (-not $file.StartsWith($runRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Package file escaped its run directory.' }
    if (-not (Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $item.Value) {
        throw "Frozen package integrity check failed: $($item.Name). No match started."
    }
}
if (-not (Test-Path -LiteralPath $package.monoExe)) { throw "Required Unity 2022.3.62f3 Mono not found: $($package.monoExe)" }
$runner = Join-Path $runRoot $package.runnerExe
$utf8 = [Text.UTF8Encoding]::new($false)
function Write-JsonFile([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 16), $utf8)
}
function Get-Statistics($Rows, [int]$Planned) {
    $list = @($Rows)
    $wins = @($list | Where-Object { $_.status -eq 'Finished' -and $_.winner -eq $(if ($_.bot0 -eq 'Hard') { 0 } else { 1 }) }).Count
    $losses = @($list | Where-Object { $_.status -eq 'Finished' -and $_.winner -eq $(if ($_.bot0 -eq 'Hard') { 1 } else { 0 }) }).Count
    $easyWins = @($list | Where-Object { $_.status -eq 'Finished' -and $_.winner -ge 0 -and $(if ($_.winner -eq 0) { $_.bot0 } else { $_.bot1 }) -eq 'Easy' }).Count
    $normalWins = @($list | Where-Object { $_.status -eq 'Finished' -and $_.winner -ge 0 -and $(if ($_.winner -eq 0) { $_.bot0 } else { $_.bot1 }) -eq 'Normal' }).Count
    $truncated = @($list | Where-Object { $_.status -in @('TurnLimit', 'ActionLimit', 'TimeLimit') }).Count
    [pscustomobject]@{
        label = $package.label; completed = $list.Count; planned = $Planned
        finished = @($list | Where-Object status -eq 'Finished').Count
        hardWins = $wins; opponentWins = $losses; easyWins = $easyWins; normalWins = $normalWins
        draws = @($list | Where-Object { $_.status -eq 'Finished' -and $_.winner -lt 0 }).Count
        truncated = $truncated
        errors = @($list | Where-Object { $_.status -notin @('Finished', 'TurnLimit', 'ActionLimit', 'TimeLimit') }).Count
        hardWinRateAllAttempts = $(if ($list.Count) { $wins / [double]$list.Count } else { 0 })
        replayVerified = $false; trainingEligible = $false
    }
}
function Write-RunSummary {
    $all = @()
    $byBatch = @()
    foreach ($plan in $batchPlans) {
        $number = [int]$plan.number
        $folder = Join-Path $runRoot ('batch_{0:D2}' -f $number)
        $rows = @()
        $jsonLines = @()
        foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $folder 'results') -Filter '*.json' | Sort-Object Name)) {
            $done = Join-Path $folder ('completed/' + $file.BaseName + '.done')
            if (-not (Test-Path -LiteralPath $done)) { continue }
            $hashes = @(Get-Content -LiteralPath $done)
            if ($hashes.Count -ne 4 -or (Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant() -ne $hashes[0]) { throw "Result integrity failed: $($file.Name)" }
            $raw = (Get-Content -LiteralPath $file.FullName -Raw).Trim()
            $rows += $raw | ConvertFrom-Json
            $jsonLines += $raw
        }
        [IO.File]::WriteAllLines((Join-Path $folder 'matches.jsonl'), [string[]]$jsonLines, $utf8)
        if ($rows.Count) {
            $rows | Select-Object matchId, seed, leg, bot0, bot1, firstPlayer, status, winner, turns, actions, durationSeconds, error |
                Export-Csv -LiteralPath (Join-Path $folder 'matches.csv') -NoTypeInformation -Encoding UTF8
        }
        $stats = Get-Statistics $rows ([int]$plan.plannedMatches)
        Write-JsonFile (Join-Path $folder 'batch-report.json') $stats
        $byBatch += $stats
        $all += $rows
    }
    $summary = Get-Statistics $all $plannedTotal
    Write-JsonFile (Join-Path $runRoot 'summary.json') ([pscustomobject]@{ totals = $summary; batches = $byBatch })
    if ($all.Count) {
        $all | Select-Object matchId, seed, leg, bot0, bot1, firstPlayer, status, winner, turns, actions, durationSeconds, error |
            Export-Csv -LiteralPath (Join-Path $runRoot 'matches.csv') -NoTypeInformation -Encoding UTF8
    }
    return $summary
}
Write-Host "Frozen $($package.label): batches $($Batch -join ', '), $plannedTotal planned matches in this package."
Write-Host "Results: $runRoot"
if ($ValidateOnly) { Write-Host 'Validation PASS. No batch executed.'; return }
$runLock = $null
$previousMonoPath = $env:MONO_PATH
try {
    $runLock = [IO.File]::Open((Join-Path $runRoot '.running.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $env:MONO_PATH = $package.monoPath
    Push-Location $projectRoot
    try {
        foreach ($number in ($Batch | Select-Object -Unique)) {
            Write-Host "Starting batch $number. Ctrl+C interrupts; rerun the same command to resume."
            & $package.monoExe $runner $runRoot $number 2>&1 | Tee-Object -FilePath (Join-Path $runRoot ('batch_{0:D2}/console.log' -f $number)) -Append
            $exitCode = $LASTEXITCODE
            $stats = Write-RunSummary
            if ($exitCode -ne 0) { throw "Runner stopped (exit $exitCode). Results preserved in $runRoot; inspect status.json." }
            Write-Host "Recorded $($stats.completed)/$plannedTotal; Hard wins $($stats.hardWins); errors $($stats.errors); truncated $($stats.truncated)."
        }
    } finally { Pop-Location }
    $stats = Write-RunSummary
    Write-Host "DONE: $($stats.completed)/$plannedTotal recorded; Hard wins $($stats.hardWins), opponent wins $($stats.opponentWins) (Easy $($stats.easyWins), Normal $($stats.normalWins)), draws $($stats.draws), truncated $($stats.truncated)."
    Write-Host 'Send the completion summary to the assistant for analysis. No tuning or further batches start automatically.'
} finally {
    $env:MONO_PATH = $previousMonoPath
    if ($runLock) { $runLock.Dispose() }
}
