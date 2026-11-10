param(
    [Parameter(Mandatory=$true)][string]$RunDirectory,
    [Parameter(Mandatory=$true)][string]$TraceName,
    [switch]$ExportAi,
    [ValidateRange(1,86400)][int]$TimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([IO.Path]::GetPathRoot($projectRoot) -ne 'D:\') { throw 'Replay must run from the project on D.' }
$rootPrefix = $projectRoot.TrimEnd('\') + '\'
$run = [IO.Path]::GetFullPath($RunDirectory)
if (!$run.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'RunDirectory must be inside this project.' }
if ([IO.Path]::GetFileName($TraceName) -ne $TraceName -or ! $TraceName.EndsWith('.jsonl')) { throw 'TraceName must be a filename under run/traces.' }
$trace = Join-Path $run ('traces/' + $TraceName)
if (!(Test-Path -LiteralPath $trace -PathType Leaf)) { throw 'Trace file not found.' }
$executable = Join-Path $projectRoot 'Build/Simulation/CardGameSimulation.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'Build the headless runner in Unity first.' }
$id = [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$output = Join-Path $run ('replay/' + $id)
New-Item -ItemType Directory -Path $output | Out-Null
$reportPath = Join-Path $output 'report.json'
$logPath = Join-Path $output 'player.log'
$arguments = @('-batchmode','-nographics','-logFile',('"'+$logPath+'"'),
    '--simulation-project',('"'+$projectRoot+'"'),'--replay-run',('"'+$run+'"'),
    '--replay-trace',('"'+$trace+'"'),'--replay-report',('"'+$reportPath+'"'))
if ($ExportAi) { $arguments += @('--ai-export',('"'+(Join-Path $output 'transitions.jsonl')+'"')) }
$child = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
try {
    if (!$child.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $child.Id
        throw "Replay timed out. Only this launched process stopped. Keep $output for diagnosis."
    }
    Write-Output "Replay output: $output"
    if (!(Test-Path -LiteralPath $reportPath)) { throw "No replay report. Read $logPath" }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    Write-Output ("{0}: actions={1}; decisions={2}; complete={3}; truncated={4}" -f $report.status,$report.verifiedActions,$report.verifiedDecisions,$report.completeMatch,$report.truncated)
    if (!$report.verified -or $child.ExitCode -ne 0) {
        throw ("Replay failed at sequence {0}, component {1}: {2}. See {3}" -f $report.sequence,$report.component,$report.error,$reportPath)
    }
} finally {
    if (!$child.HasExited) { Stop-Process -Id $child.Id }
}
