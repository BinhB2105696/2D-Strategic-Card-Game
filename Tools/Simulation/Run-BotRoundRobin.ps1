#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateRange(1, 9)][int[]]$Batch = @(1..9),
    [string]$RunDirectory,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $RunDirectory) {
    $pointer = Join-Path $PSScriptRoot 'Control/prepared-bot-round-robin.txt'
    if (-not (Test-Path -LiteralPath $pointer)) { throw 'No prepared bot round robin. Ask the assistant to prepare one.' }
    $RunDirectory = (Get-Content -LiteralPath $pointer -Raw).Trim()
}
$runRoot = [IO.Path]::GetFullPath($RunDirectory)
$resultsPrefix = [IO.Path]::GetFullPath((Join-Path $projectRoot 'SimulationResults')) + [IO.Path]::DirectorySeparatorChar
if (-not $runRoot.StartsWith($resultsPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Run directory must be inside this project SimulationResults.' }
$package = Get-Content -LiteralPath (Join-Path $runRoot 'package.json') -Raw | ConvertFrom-Json
if ($package.schema -ne 1) { throw 'Unsupported prepared package schema.' }
$batchPlans = @($package.expectedBatches)
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
    $wins = [ordered]@{ Easy = 0; Normal = 0; Hard = 0 }
    foreach ($row in $list | Where-Object { $_.status -eq 'Finished' -and $_.winner -ge 0 }) {
        $winner = if ($row.winner -eq 0) { $row.bot0 } else { $row.bot1 }
        if ($wins.Contains($winner)) { $wins[$winner]++ }
    }
    [pscustomobject]@{
        label = $package.label; completed = $list.Count; planned = $Planned
        finished = @($list | Where-Object status -eq 'Finished').Count
        easyWins = $wins.Easy; normalWins = $wins.Normal; hardWins = $wins.Hard
        draws = @($list | Where-Object { $_.status -eq 'Finished' -and $_.winner -lt 0 }).Count
        truncated = @($list | Where-Object { $_.status -in @('TurnLimit', 'ActionLimit', 'TimeLimit') }).Count
        errors = @($list | Where-Object { $_.status -notin @('Finished', 'TurnLimit', 'ActionLimit', 'TimeLimit') }).Count
        replayVerified = $false; trainingEligible = $false
    }
}
function Get-MatchupStatistics($Rows) {
    $groups = @($Rows | Group-Object { [string]::Join('-', [string[]](@($_.bot0, $_.bot1) | Sort-Object)) })
    foreach ($group in $groups) {
        $bots = @($group.Group[0].bot0, $group.Group[0].bot1) | Sort-Object
        $wins = [ordered]@{}
        foreach ($bot in $bots) { $wins[$bot] = 0 }
        foreach ($row in $group.Group | Where-Object { $_.status -eq 'Finished' -and $_.winner -ge 0 }) {
            $winner = if ($row.winner -eq 0) { $row.bot0 } else { $row.bot1 }
            $wins[$winner]++
        }
        [pscustomobject]@{
            matchup = $group.Name; completed = $group.Count; botA = $bots[0]; botAWins = $wins[$bots[0]]
            botB = $bots[1]; botBWins = $wins[$bots[1]]
            draws = @($group.Group | Where-Object { $_.status -eq 'Finished' -and $_.winner -lt 0 }).Count
        }
    }
}
function Write-RunSummary {
    $all = @(); $byBatch = @()
    foreach ($plan in $batchPlans) {
        $number = [int]$plan.number
        $folder = Join-Path $runRoot ('batch_{0:D2}' -f $number)
        $rows = @(); $jsonLines = @()
        foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $folder 'results') -Filter '*.json' | Sort-Object Name)) {
            $done = Join-Path $folder ('completed/' + $file.BaseName + '.done')
            if (-not (Test-Path -LiteralPath $done)) { continue }
            $hashes = @(Get-Content -LiteralPath $done)
            if ($hashes.Count -ne 4 -or (Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant() -ne $hashes[0]) { throw "Result integrity failed: $($file.Name)" }
            $raw = (Get-Content -LiteralPath $file.FullName -Raw).Trim()
            $rows += $raw | ConvertFrom-Json; $jsonLines += $raw
        }
        [IO.File]::WriteAllLines((Join-Path $folder 'matches.jsonl'), [string[]]$jsonLines, $utf8)
        if ($rows.Count) {
            $rows | Select-Object matchId, pairId, seed, leg, bot0, bot1, firstPlayer, status, winner, turns, actions, durationSeconds, error |
                Export-Csv -LiteralPath (Join-Path $folder 'matches.csv') -NoTypeInformation -Encoding UTF8
        }
        $stats = Get-Statistics $rows ([int]$plan.plannedMatches)
        Write-JsonFile (Join-Path $folder 'batch-report.json') $stats
        $byBatch += $stats; $all += $rows
    }
    $totals = Get-Statistics $all $plannedTotal
    $matchups = @(Get-MatchupStatistics $all)
    Write-JsonFile (Join-Path $runRoot 'summary.json') ([pscustomobject]@{ totals = $totals; matchups = $matchups; batches = $byBatch })
    if ($all.Count) {
        $all | Select-Object matchId, pairId, seed, leg, bot0, bot1, firstPlayer, status, winner, turns, actions, durationSeconds, error |
            Export-Csv -LiteralPath (Join-Path $runRoot 'matches.csv') -NoTypeInformation -Encoding UTF8
    }
    return [pscustomobject]@{ totals = $totals; matchups = $matchups }
}
Write-Host "Frozen $($package.label): batches $($Batch -join ', '), $plannedTotal planned matches."
Write-Host "Results: $runRoot"
if ($ValidateOnly) { Write-Host 'Validation PASS. No match executed.'; return }
$runLock = $null; $previousMonoPath = $env:MONO_PATH
try {
    $runLock = [IO.File]::Open((Join-Path $runRoot '.running.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $env:MONO_PATH = $package.monoPath
    Push-Location $projectRoot
    try {
        foreach ($number in ($Batch | Select-Object -Unique)) {
            Write-Host "Starting batch $number. Ctrl+C interrupts; rerun the same command to resume."
            & $package.monoExe $runner $runRoot $number 2>&1 | Tee-Object -FilePath (Join-Path $runRoot ('batch_{0:D2}/console.log' -f $number)) -Append
            $exitCode = $LASTEXITCODE
            $summary = Write-RunSummary
            if ($exitCode -ne 0) { throw "Runner stopped (exit $exitCode). Results preserved in $runRoot; inspect status.json." }
            Write-Host "Recorded $($summary.totals.completed)/$plannedTotal; errors $($summary.totals.errors); truncated $($summary.totals.truncated)."
        }
    } finally { Pop-Location }
    $summary = Write-RunSummary
    Write-Host "DONE: $($summary.totals.completed)/$plannedTotal recorded; Easy wins $($summary.totals.easyWins), Normal wins $($summary.totals.normalWins), Hard wins $($summary.totals.hardWins)."
    $summary.matchups | ForEach-Object { Write-Host "$($_.matchup): $($_.botA) $($_.botAWins) - $($_.botBWins) $($_.botB), draws $($_.draws)." }
    Write-Host 'Send the completion summary to the assistant for comparative analysis.'
} finally {
    $env:MONO_PATH = $previousMonoPath
    if ($runLock) { $runLock.Dispose() }
}
