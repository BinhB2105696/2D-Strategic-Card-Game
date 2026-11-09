param()
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([IO.Path]::GetPathRoot($projectRoot) -ne 'D:\') { throw 'Run the acceptance checks from the project on D.' }
$testRoot = Join-Path $PSScriptRoot ('Control/Acceptance/' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$results = [Collections.Generic.List[object]]::new()
$base = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'batch.json') -Raw | ConvertFrom-Json
$base.seedCount = 1
$base.warmupMatches = 2
$base.detailedMatches = 0
$base.matchups = @($base.matchups[2])

function Read-SharedJson([string]$Path) {
    $share = [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, $share)
    $reader = [IO.StreamReader]::new($stream)
    try { return ($reader.ReadToEnd() | ConvertFrom-Json) }
    finally { $reader.Dispose() }
}

function Save-Result([string]$Name, [bool]$Passed, [object]$Details) {
    $results.Add([pscustomobject]@{ name=$Name; passed=$Passed; details=$Details })
    $results | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $testRoot 'report.json') -Encoding UTF8
    if (!$Passed) { throw "Acceptance check failed: $Name. Details in $testRoot" }
    Write-Output "PASS $Name"
}

function New-TestConfig([string]$Name, [scriptblock]$Change) {
    $config = $base | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    & $Change $config
    $path = Join-Path $testRoot ($Name + '.json')
    $config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Invoke-Case([string]$Name, [scriptblock]$Change, [string]$ExpectedStatus, [string]$ErrorText) {
    $path = New-TestConfig $Name $Change
    if ($Name -eq 'invalid-json') { [IO.File]::WriteAllText($path, '{broken json') }
    $log = Join-Path $testRoot ($Name + '-launcher.log')
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & powershell -NoProfile -File (Join-Path $PSScriptRoot 'Run-Simulation.ps1') -Config $path *> $log
        $exitCode = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    $output = [IO.File]::ReadAllText($log)
    if ($ErrorText) {
        Save-Result $Name ($exitCode -ne 0 -and $output.Contains($ErrorText)) @{exitCode=$exitCode; launcherLog=$log}
        return
    }
    $match = [regex]::Match($output, '(?m)^Results: (.+)\r?$')
    if (!$match.Success) { Save-Result $Name $false @{exitCode=$exitCode; launcherLog=$log}; return }
    $run = $match.Groups[1].Value.Trim()
    $rows = @(Import-Csv -LiteralPath (Join-Path $run 'matches.csv'))
    $jsonRows = @(Get-Content -LiteralPath (Join-Path $run 'matches.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    $summary = Read-SharedJson (Join-Path $run 'summary.json')
    $passed = $exitCode -eq 0 -and $rows.Count -eq 2 -and $jsonRows.Count -eq 2 -and $summary.matches -eq 2
    $passed = $passed -and @($rows | Where-Object { $_.status -ne $ExpectedStatus }).Count -eq 0
    $traces = @(Get-ChildItem -LiteralPath (Join-Path $run 'traces') -Filter '*.jsonl')
    if ($ExpectedStatus -eq 'Finished') { $passed = $passed -and $traces.Count -eq 0 }
    else { $passed = $passed -and @($rows | Where-Object winner -ne '-2').Count -eq 0 -and $traces.Count -ge 2 }
    Save-Result $Name $passed @{runDirectory=$run; matches=$rows.Count; status=$ExpectedStatus; traceCount=$traces.Count}
}

Invoke-Case 'turn-limit' { param($c) $c.maxTurns=1 } 'TurnLimit' ''
Invoke-Case 'time-limit' { param($c) $c.maxMatchSeconds=0.001 } 'TimeLimit' ''
Invoke-Case 'trace-off' { param($c) } 'Finished' ''
Invoke-Case 'invalid-bot' { param($c) $c.matchups[0].player0Bot='UnknownPolicy' } '' 'Unsupported bot'
Invoke-Case 'invalid-deck' { param($c) $c.matchups[0].deck0=999 } '' 'Deck index'
Invoke-Case 'invalid-seed-count' { param($c) $c.seedCount=0 } '' 'Invalid batch limits'
Invoke-Case 'invalid-root' { param($c) $c.projectRoot='C:\outside-project' } '' 'projectRoot does not match'
Invoke-Case 'invalid-json' { param($c) } '' 'ConvertFrom-Json'

# Use owned child processes for interruption and write-fault checks.
$executable = Join-Path $projectRoot 'Build/Simulation/CardGameSimulation.exe'
function Start-TestPlayer([string]$ConfigPath, [string]$ProgressPath, [string]$LogPath) {
    $arguments = @('-batchmode','-nographics','-logFile',('"'+$LogPath+'"'),
        '--simulation-config',('"'+$ConfigPath+'"'),'--simulation-progress',('"'+$ProgressPath+'"'))
    return Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
}

$path = New-TestConfig 'interrupted-batch' { param($c) $c.seedCount=1000; $c.warmupMatches=0 }
$progress = Join-Path $testRoot 'interrupted-progress.json'
$playerLog = Join-Path $testRoot 'interrupted-player.log'
$child = Start-TestPlayer $path $progress $playerLog
$timer = [Diagnostics.Stopwatch]::StartNew()
$state = $null
try {
    while (!$child.HasExited -and $timer.Elapsed.TotalSeconds -lt 20) {
        Start-Sleep -Milliseconds 100
        if (Test-Path -LiteralPath $progress) {
            try { $state = Read-SharedJson $progress } catch { $state = $null }
            if ($state.completed -ge 3) { break }
        }
        $child.Refresh()
    }
    if (!$child.HasExited) { Stop-Process -Id $child.Id; $child.WaitForExit() }
} finally { if (!$child.HasExited) { Stop-Process -Id $child.Id } }
$passed = $null -ne $state -and $state.completed -ge 3 -and $state.completed -lt $state.expected
if ($passed) {
    $csvRows = @(Import-Csv -LiteralPath (Join-Path $state.runDirectory 'matches.csv'))
    $jsonIds = @{}
    Get-Content -LiteralPath (Join-Path $state.runDirectory 'matches.jsonl') | ForEach-Object {
        try { $row = $_ | ConvertFrom-Json; $jsonIds[$row.matchId]=$true } catch { }
    }
    $common = @($csvRows | Where-Object { $jsonIds.ContainsKey($_.match_id) })
    $passed = $common.Count -ge $state.completed
}
Save-Result 'interrupted-batch' $passed @{runDirectory=$state.runDirectory; acknowledged=$state.completed; recoverable=$common.Count}

$path = New-TestConfig 'write-fault' { param($c) }
$progress = Join-Path $testRoot 'readonly-progress.json'
$playerLog = Join-Path $testRoot 'write-fault-player.log'
[IO.File]::WriteAllText($progress, '{"sentinel":"keep"}')
$file = Get-Item -LiteralPath $progress
$file.IsReadOnly = $true
$child = $null
try {
    $child = Start-TestPlayer $path $progress $playerLog
    $finished = $child.WaitForExit(20000)
    if (!$finished) { Stop-Process -Id $child.Id; $child.WaitForExit() }
    $text = [IO.File]::ReadAllText($playerLog)
    $passed = $finished -and $child.ExitCode -ne 0 -and $text.Contains('UnauthorizedAccessException')
    $passed = $passed -and [IO.File]::ReadAllText($progress).Contains('sentinel')
    Save-Result 'write-fault' $passed @{exitCode=$child.ExitCode; playerLog=$playerLog; sentinelPreserved=$true}
} finally {
    $file.IsReadOnly = $false
    if ($null -ne $child -and !$child.HasExited) { Stop-Process -Id $child.Id }
}
Write-Output "Acceptance report: $(Join-Path $testRoot 'report.json')"
