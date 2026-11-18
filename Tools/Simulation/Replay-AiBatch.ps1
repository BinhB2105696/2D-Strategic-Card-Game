param(
    [Parameter(Mandatory=$true)][string]$RunDirectory,
    [ValidateRange(1,3600)][int]$TraceTimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) {
    $pwshCommand = Get-Command pwsh.exe -ErrorAction SilentlyContinue
    $pwsh = if ($pwshCommand) { $pwshCommand.Source } else {
        Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe'
    }
    if (!(Test-Path -LiteralPath $pwsh -PathType Leaf)) { throw 'PowerShell 7 is required to read case-sensitive simulation JSON.' }
    & $pwsh -NoProfile -File $PSCommandPath -RunDirectory $RunDirectory -TraceTimeoutSeconds $TraceTimeoutSeconds
    exit $LASTEXITCODE
}
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$run = [IO.Path]::GetFullPath($RunDirectory)
$prefix = (Join-Path $project 'SimulationResults').TrimEnd('\') + '\'
if (!$run.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'RunDirectory must be inside SimulationResults.' }
$runner = Join-Path $project 'Build/Simulation'
$executable = Join-Path $runner 'CardGameSimulation.exe'
if (!(Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'Build the training runner first.' }
$progress = Get-Content -LiteralPath (Join-Path $run 'progress.json') -Raw | ConvertFrom-Json
$expected = [int]$progress.expected
if ($progress.state -ne 'completed' -or $expected -lt 1 -or $progress.completed -ne $expected) { throw 'Replay requires a completed simulation batch.' }
$recorded = Get-Content -LiteralPath (Join-Path $run 'run.json') -Raw | ConvertFrom-Json -AsHashtable
$built = Get-Content -LiteralPath (Join-Path $runner 'build-info.json') -Raw | ConvertFrom-Json -AsHashtable
if ($recorded['sourceFingerprint'] -ne $built['sourceFingerprint'] -or $recorded['rulesVersion'] -ne $built['rulesVersion'] -or
    $recorded['catalogVersion'] -ne $built['catalogVersion'] -or $recorded['unityVersion'] -ne $built['unityVersion']) {
    throw 'Current training runner does not match the source pilot.'
}
$traces = @(Get-ChildItem -LiteralPath (Join-Path $run 'traces') -File -Filter '*.jsonl' | Sort-Object Name)
if ($traces.Count -ne $expected) { throw "Expected $expected traces." }
$batch = Join-Path $run ('replay/' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '_powershell_' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $batch | Out-Null
$results = [Collections.Generic.List[object]]::new()
$timer = [Diagnostics.Stopwatch]::StartNew()
$peak = 0L; $status = 'failed'; $message = ''; $failure = $null
try {
    foreach ($trace in $traces) {
        $output = Join-Path $batch $trace.BaseName
        New-Item -ItemType Directory -Path $output | Out-Null
        $reportPath = Join-Path $output 'report.json'; $dataset = Join-Path $output 'transitions.jsonl'; $log = Join-Path $output 'player.log'
        $arguments = @('-batchmode','-nographics','-logFile',('"'+$log+'"'),'--simulation-project',('"'+$project+'"'),
            '--replay-run',('"'+$run+'"'),'--replay-trace',('"'+$trace.FullName+'"'),'--replay-report',('"'+$reportPath+'"'),'--ai-export',('"'+$dataset+'"'))
        $child = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $project -WindowStyle Hidden -PassThru
        $childTimer = [Diagnostics.Stopwatch]::StartNew()
        try {
            while (!$child.HasExited) {
                Start-Sleep -Seconds 1; $child.Refresh()
                if (!$child.HasExited) { $peak = [Math]::Max($peak,$child.PeakWorkingSet64) }
                if ($childTimer.Elapsed.TotalSeconds -gt $TraceTimeoutSeconds -or $timer.Elapsed.TotalSeconds -gt 1800 -or $peak -gt 1536MB) {
                    Stop-Process -Id $child.Id
                    throw "Replay resource limit reached for $($trace.Name)."
                }
            }
            $child.WaitForExit()
            if ($child.ExitCode -ne 0 -or !(Test-Path -LiteralPath $reportPath) -or !(Test-Path -LiteralPath $dataset)) { throw "Replay process failed for $($trace.Name). See $log" }
            $verified = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            if (!$verified.verified -or !$verified.completeMatch -or $verified.truncated -or $verified.status -ne 'verified-match' -or
                [IO.Path]::GetFullPath($verified.dataset) -ne [IO.Path]::GetFullPath($dataset)) { throw "Replay verification failed for $($trace.Name): $($verified.component) $($verified.error)" }
            $results.Add([ordered]@{trace=$trace.Name;report=$reportPath;dataset=$dataset;verifiedActions=$verified.verifiedActions;verifiedDecisions=$verified.verifiedDecisions})
            Write-Output "Verified $($results.Count)/${expected}: $($trace.Name)"
        } finally {
            if (!$child.HasExited) { Stop-Process -Id $child.Id }
            $child.Dispose()
        }
    }
    $status = 'completed'; $message = "All $expected traces replay-verified and exported."
} catch {
    $failure = $_; $message = $_.Exception.Message
} finally {
    $timer.Stop()
    [ordered]@{schema=1;status=$status;sourceRun=$run;verifiedMatches=$results.Count;wallSeconds=$timer.Elapsed.TotalSeconds;
        peakRunnerWorkingSetBytes=if($peak){$peak}else{$null};ramMeasurementAvailable=($peak -gt 0);
        sampling='One-second polling of owned replay Player processes.';results=@($results);message=$message} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $batch 'batch-report.json') -Encoding UTF8
}
Write-Output "Replay batch: $batch"
if ($failure) { throw $failure }
