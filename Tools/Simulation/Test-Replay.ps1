param()
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path $PSScriptRoot ('Control/ReplayAcceptance/' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$results = [Collections.Generic.List[object]]::new()
function Save-Check([string]$Name,[bool]$Passed,[object]$Details) {
    $results.Add([pscustomobject]@{name=$Name;passed=$Passed;details=$Details})
    $results | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $testRoot 'report.json') -Encoding UTF8
    if (!$Passed) { throw "FAIL $Name. Keep $testRoot for diagnosis." }
    Write-Output "PASS $Name"
}
function Invoke-Child([string]$Script,[string[]]$ChildArguments,[string]$Log) {
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & powershell -NoProfile -File $Script @ChildArguments *> $Log
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previous }
    return [pscustomobject]@{exitCode=$code;text=[IO.File]::ReadAllText($Log)}
}
function Invoke-Batch([object]$Config,[string]$Name) {
    $configPath = Join-Path $testRoot ($Name + '.json')
    $Config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configPath -Encoding UTF8
    $run = Invoke-Child (Join-Path $PSScriptRoot 'Run-Simulation.ps1') @('-Config',$configPath) (Join-Path $testRoot ($Name + '.log'))
    $location = [regex]::Match($run.text,'(?m)^Results: (.+)\r?$')
    if ($run.exitCode -ne 0 -or !$location.Success) { throw "Batch failed. Read $testRoot/$Name.log" }
    return $location.Groups[1].Value.Trim()
}
function Invoke-Replay([string]$Name,[string]$Run,[string]$Trace,[bool]$Verified,[string]$Component='',[bool]$Export=$false) {
    $caseArgs = @('-RunDirectory',$Run,'-TraceName',$Trace)
    if ($Export) { $caseArgs += '-ExportAi' }
    $child = Invoke-Child (Join-Path $PSScriptRoot 'Replay-Simulation.ps1') $caseArgs (Join-Path $testRoot ($Name + '.log'))
    $location = [regex]::Match($child.text,'(?m)^Replay output: (.+)\r?$')
    if (!$location.Success) { Save-Check $Name $false @{log=$child.text}; return }
    $reportPath = Join-Path $location.Groups[1].Value.Trim() 'report.json'
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $passed = $report.verified -eq $Verified -and (($child.exitCode -eq 0) -eq $Verified)
    if ($Component) { $passed = $passed -and $report.component -eq $Component }
    if ($Export -and !$Verified) { $passed = $passed -and !$report.dataset }
    if ($Export -and $Verified) {
        $data = @(Get-Content -LiteralPath $report.dataset | ForEach-Object { $_ | ConvertFrom-Json })
        $steps = @($data | Where-Object record -eq 'transition')
        $passed = $passed -and $steps.Count -eq $report.verifiedDecisions -and $data[-1].record -eq 'episode_end'
        foreach ($step in $steps) {
            $policy = $step.policyInputJson | ConvertFrom-Json
            $passed = $passed -and $policy.globalFeatures.Count -eq 17 -and $step.selectedActionIndex -ge 0 -and $step.selectedActionIndex -lt $policy.legalActions.Count
            $passed = $passed -and @($policy.legalActionMask | Where-Object { $_ -ne 1 }).Count -eq 0
            $passed = $passed -and @($policy.entities | Where-Object { $_.features.Count -ne 18 }).Count -eq 0
            if ($step.terminated) {
                $expected = if ($data[-1].winner -lt 0) {0} elseif ($data[-1].winner -eq $step.player) {1} else {-1}
                $passed = $passed -and $step.reward -eq $expected
            }
        }
        if ($report.truncated) {
            $passed = $passed -and $data[-1].truncated -and @($steps | Where-Object terminated).Count -eq 0
        } else {
            $passed = $passed -and @($steps | Where-Object terminated).Count -eq 2
        }
    }
    Save-Check $Name $passed @{report=$reportPath;actions=$report.verifiedActions;decisions=$report.verifiedDecisions;component=$report.component;sequence=$report.sequence}
}
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'normal-vs-easy-pilot.json') -Raw | ConvertFrom-Json
$config.seedCount=1; $config.firstSeed=2001; $config.warmupMatches=0; $config.detailedMatches=6
$source = Invoke-Batch $config 'source-six'
$rows = @(Import-Csv -LiteralPath (Join-Path $source 'matches.csv'))
if ($rows.Count -ne 6 -or @($rows | Where-Object status -ne 'Finished').Count -gt 0) { throw 'Expected six finished source matches.' }
for ($i=0;$i -lt $rows.Count;$i++) { Invoke-Replay ('valid-' + $i) $source $rows[$i].trace $true '' $true }
$traceName=$rows[0].trace
$sourceTrace=Join-Path $source ('traces/' + $traceName)
foreach ($kind in @('state','action','policy','events','event-payload','observation','seed','missing-end','result','version','legacy')) {
    $scratch=Join-Path $testRoot $kind
    New-Item -ItemType Directory -Path (Join-Path $scratch 'traces') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source 'run.json') -Destination (Join-Path $scratch 'run.json')
    $changed=$false
    $reader=[IO.StreamReader]::new($sourceTrace)
    $writer=[IO.StreamWriter]::new((Join-Path $scratch ('traces/' + $traceName)),$false,[Text.UTF8Encoding]::new($false))
    try {
        while ($null -ne ($line=$reader.ReadLine())) {
            $record=$line | ConvertFrom-Json
            if (!$changed) {
                if ($kind -eq 'state' -and $record.record -eq 'accepted') { $record.stateHash='0'*64; $changed=$true }
                elseif ($kind -eq 'events' -and $record.record -eq 'accepted') { $record.eventsHash='0'*64; $changed=$true }
                elseif ($kind -eq 'event-payload' -and $record.record -eq 'accepted' -and $record.events.Count -gt 0) { $record.events[0].Amount++; $changed=$true }
                elseif ($kind -eq 'observation' -and $record.record -eq 'attempt' -and !$record.automatic) { $record.observationJson='{}'; $changed=$true }
                elseif ($kind -eq 'action' -and $record.record -eq 'attempt' -and !$record.automatic) { $record.action.source=[long]::MaxValue; $changed=$true }
                elseif ($kind -eq 'policy' -and $record.record -eq 'attempt' -and !$record.automatic) { $record.selectedActionIndex=999999; $changed=$true }
                elseif ($kind -eq 'seed' -and $record.record -eq 'start') { $record.seed++; $changed=$true }
                elseif ($kind -eq 'result' -and $record.record -eq 'end') { $record.result.resultDigest='0'*64; $changed=$true }
                elseif ($kind -eq 'missing-end' -and $record.record -eq 'end') { $changed=$true; continue }
                if ($changed) { $line=$record | ConvertTo-Json -Depth 30 -Compress }
            }
            $writer.WriteLine($line)
        }
    } finally { $reader.Dispose(); $writer.Dispose() }
    if ($kind -in @('version','legacy')) {
        $metadata=Get-Content -LiteralPath (Join-Path $scratch 'run.json') -Raw | ConvertFrom-Json
        if ($kind -eq 'version') { $metadata.sourceFingerprint='0'*64 } else { $metadata.schema=1 }
        $metadata | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $scratch 'run.json') -Encoding UTF8
        $changed=$true
    }
    if (!$changed) { throw "No corruption applied for $kind" }
    $expected=switch($kind) {
        state {'after state'}; events {'recorded event payload'}; event-payload {'recorded event payload'}
        observation {'observation'}; action {'legal action'}; policy {'selected index'}
        seed {'initial state'}; result {'result digest'}; version {'runtime source fingerprint'}
        legacy {'run schema (legacy traces cannot be fully verified)'}; default {'format/compatibility/storage'}
    }
    Invoke-Replay ('reject-' + $kind) $scratch $traceName $false $expected $true
}
$config.maxActions=6; $config.detailedMatches=1; $config.matchups=@($config.matchups[0]); $config.matchups[0].swapBots=$false
$limited=Invoke-Batch $config 'limited-prefix'
$row=Import-Csv -LiteralPath (Join-Path $limited 'matches.csv') | Select-Object -First 1
if ($row.status -ne 'ActionLimit') { throw 'Expected a deliberately truncated source.' }
Invoke-Replay 'truncated-prefix' $limited $row.trace $true '' $true
Write-Output "Replay acceptance report: $(Join-Path $testRoot 'report.json')"
