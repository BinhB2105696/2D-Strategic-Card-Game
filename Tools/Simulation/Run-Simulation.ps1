param(
    [string]$Config = (Join-Path $PSScriptRoot 'batch.json'),
    [ValidateRange(1, 86400)]
    [int]$HeartbeatTimeoutSeconds = 120
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
try {
    while (!$ownedProcess.HasExited) {
        Start-Sleep -Seconds 1
        $ownedProcess.Refresh()
        $state = $null
        if (Test-Path -LiteralPath $progress) {
            try { $state = Read-SimulationProgress $progress } catch { $state = $null }
        }
        if ($null -ne $state) {
            if ($state.utc) { $lastHeartbeat = [DateTime]::Parse($state.utc).ToUniversalTime() }
            $text = "$($state.state): $($state.completed)/$($state.expected)  $($state.activeMatch)  turn=$($state.turn)"
            if ($text -ne $lastText) { Write-Output $text; $lastText = $text }
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
} finally {
    if (!$ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id }
}
