param(
    [string]$Config = (Join-Path $PSScriptRoot 'batch.json'),
    [ValidateRange(1, 86400)]
    [int]$HeartbeatTimeoutSeconds = 120,
    [ValidateRange(0, 86400)][int]$MaxBatchSeconds = 0,
    [ValidateRange(0, 65536)][int]$MaxWorkingSetMiB = 0,
    [ValidateRange(0, 1048576)][int]$MaxResultsMiB = 0
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([IO.Path]::GetPathRoot($projectRoot) -ne 'D:\') { throw 'The simulator must run from the project on drive D.' }
$executable = Join-Path $projectRoot 'Build/Simulation/CardGameSimulation.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'In Unity run Card Game > Simulation > Build Headless Windows Runner first.' }
$settings = Get-Content -LiteralPath $Config -Raw | ConvertFrom-Json
if ([IO.Path]::GetFullPath($settings.projectRoot) -ne $projectRoot) { throw 'batch.json projectRoot does not match this project.' }
$control = Join-Path $PSScriptRoot 'Control'
New-Item -ItemType Directory -Path $control -Force | Out-Null
$id = (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$progress = Join-Path $control ($id + '.json')
$log = Join-Path $control ($id + '.log')
$resourcePath = Join-Path $control ($id + '.resources.json')
$timer = [Diagnostics.Stopwatch]::StartNew()
$peakWorkingSet = 0L; $resultBytes = 0L; $runDirectory = $null; $resourceStatus = 'failed'
$arguments = @('-batchmode','-nographics','-logFile',('"' + $log + '"'), '--simulation-config',('"' + [IO.Path]::GetFullPath($Config) + '"'), '--simulation-progress',('"' + $progress + '"'))
$ownedProcess = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
$lastText = ''; $lastHeartbeat = [DateTime]::UtcNow

function Read-SimulationProgress([string]$Path) {
    $share = [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, $share)
    $reader = [IO.StreamReader]::new($stream)
    try { return ($reader.ReadToEnd() | ConvertFrom-Json) }
    finally { $reader.Dispose() }
}

Write-Output "Simulation started. Player log: $log"
Write-Output "Resource report: $resourcePath"
try {
    while (!$ownedProcess.HasExited) {
        Start-Sleep -Seconds 1
        $ownedProcess.Refresh()
        if (!$ownedProcess.HasExited) {
            $peakWorkingSet = [Math]::Max($peakWorkingSet, $ownedProcess.PeakWorkingSet64)
        }
        $state = $null
        if (Test-Path -LiteralPath $progress) {
            try { $state = Read-SimulationProgress $progress } catch { $state = $null }
        }
        if ($null -ne $state) {
            if ($state.runDirectory) { $runDirectory = $state.runDirectory }
            if ($state.utc) { $lastHeartbeat = [DateTime]::Parse($state.utc).ToUniversalTime() }
            $text = "$($state.state): $($state.completed)/$($state.expected)  $($state.activeMatch)  turn=$($state.turn)"
            if ($text -ne $lastText) { Write-Output $text; $lastText = $text }
        }
        if ($runDirectory -and (Test-Path -LiteralPath $runDirectory -PathType Container)) {
            $resultBytes = [long]((Get-ChildItem -LiteralPath $runDirectory -Recurse -File | Measure-Object Length -Sum).Sum)
        }
        $outputBytes = $resultBytes
        if (Test-Path -LiteralPath $log -PathType Leaf) { $outputBytes += (Get-Item -LiteralPath $log).Length }
        $limit = if ($MaxBatchSeconds -gt 0 -and $timer.Elapsed.TotalSeconds -gt $MaxBatchSeconds) {'batch time'}
            elseif ($MaxWorkingSetMiB -gt 0 -and $peakWorkingSet -gt $MaxWorkingSetMiB * 1MB) {'runner RAM'}
            elseif ($MaxResultsMiB -gt 0 -and $outputBytes -gt $MaxResultsMiB * 1MB) {'output size'} else {$null}
        if ($limit) {
            $resourceStatus = 'resource-limit'
            if (!$ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id }
            throw "Stopped the owned runner at the $limit limit. Partial results remain for diagnosis; this is not a game loss."
        }
        if (([DateTime]::UtcNow - $lastHeartbeat).TotalSeconds -gt $HeartbeatTimeoutSeconds) {
            if (!$ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id }
            throw "Simulator heartbeat timed out. Only this launched process was stopped. Keep $progress and $log for diagnosis. Partial completed results remain on D."
        }
    }
    $ownedProcess.WaitForExit()
    if (Test-Path -LiteralPath $progress) {
        $state = Read-SimulationProgress $progress
        Write-Output "Results: $($state.runDirectory)"
        if ($state.error) { Write-Output $state.error }
    }
    if ($ownedProcess.ExitCode -ne 0) { throw "Simulator exited with code $($ownedProcess.ExitCode). Read the result status and player log." }
    $resourceStatus = 'completed'
} finally {
    if (!$ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id }
    $timer.Stop()
    [ordered]@{
        schema=1; status=$resourceStatus; config=[IO.Path]::GetFullPath($Config); runDirectory=$runDirectory
        wallSeconds=$timer.Elapsed.TotalSeconds; peakRunnerWorkingSetBytes=$peakWorkingSet; resultBytes=$resultBytes
        playerLog=$log; maxBatchSeconds=$MaxBatchSeconds; maxWorkingSetMiB=$MaxWorkingSetMiB; maxResultsMiB=$MaxResultsMiB
        sampling='One-second watchdog; output includes run files plus player log. RAM is this runner only, not whole-system availability.'
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resourcePath -Encoding UTF8
}
