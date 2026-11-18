param(
    [string]$CollectionRoot = '',
    [ValidateSet('all','train','validation','test')][string]$Split = 'all',
    [ValidateRange(1,100)][int]$MaxShards = 1
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) {
    $command=Get-Command pwsh.exe -ErrorAction SilentlyContinue
    $pwsh=if($command){$command.Source}else{Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe'}
    if (!(Test-Path -LiteralPath $pwsh)) { throw 'PowerShell 7 is required.' }
    $arguments=@('-NoProfile','-File',$PSCommandPath,'-Split',$Split,'-MaxShards',[string]$MaxShards)
    if($CollectionRoot){$arguments+=@('-CollectionRoot',$CollectionRoot)}
    & $pwsh @arguments
    exit $LASTEXITCODE
}
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$CollectionRoot) {$CollectionRoot=Join-Path $project 'SimulationResults/TrainingCollections/hard_normal_main_v1_32b700a'}
$root=[IO.Path]::GetFullPath($CollectionRoot); $plan=Get-Content (Join-Path $root 'plan.json') -Raw | ConvertFrom-Json
$build=Get-Content (Join-Path $project 'Build/Simulation/build-info.json') -Raw | ConvertFrom-Json
if ($build.sourceRevision -ne $plan.sourceRevision -or $build.sourceFingerprint -ne $plan.sourceFingerprint) {throw 'Runner differs from the frozen collection plan.'}
function Save-State($path,$state) {[IO.File]::WriteAllText($path,($state|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))}
$completed=0
foreach($shard in @($plan.shards | Where-Object {$Split -eq 'all' -or $_.split -eq $Split})) {
    $folder=Join-Path $root ('shards/'+$shard.id); New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $statePath=Join-Path $folder 'status.json'
    $state=if(Test-Path $statePath){Get-Content $statePath -Raw|ConvertFrom-Json}else{[pscustomobject]@{schema=1;id=$shard.id;stage='pending';run=$null;replay=$null;validation=$null}}
    if($state.validation -and (Test-Path (Join-Path $state.validation 'report.json')) -and (Get-Content (Join-Path $state.validation 'report.json') -Raw|ConvertFrom-Json).status -eq 'passed'){Write-Output "Skip completed $($shard.id)";continue}
    if(!$state.run){
        $started=Get-Date; & (Join-Path $PSScriptRoot 'Run-Simulation.ps1') -Config $shard.config -HeartbeatTimeoutSeconds 180 -MaxBatchSeconds 7200 -MaxWorkingSetMiB 1536 -MaxResultsMiB 2048
        $resource=Get-ChildItem (Join-Path $PSScriptRoot 'Control') -File -Filter '*.resources.json'|Where-Object LastWriteTime -ge $started.AddSeconds(-2)|Sort-Object LastWriteTime -Descending|ForEach-Object {Get-Content $_.FullName -Raw|ConvertFrom-Json}|Where-Object {$_.status -eq 'completed' -and [IO.Path]::GetFullPath($_.config) -eq [IO.Path]::GetFullPath($shard.config)}|Select-Object -First 1
        if(!$resource){throw "No completed resource report for $($shard.id)"}
        $rows=@(Import-Csv (Join-Path $resource.runDirectory 'matches.csv'))
        if($rows.Count -ne [int]$shard.matches -or @($rows|Where-Object status -ne 'Finished').Count){throw "Shard $($shard.id) has unfinished matches; preserve the run and retry with an explicit timeout decision."}
        $state.run=$resource.runDirectory;$state.stage='simulated';Save-State $statePath $state
    }
    if(!$state.replay){
        & (Join-Path $PSScriptRoot 'Replay-AiBatch.ps1') -RunDirectory $state.run
        $replay=Get-ChildItem (Join-Path $state.run 'replay') -Directory|Sort-Object LastWriteTime -Descending|Where-Object {(Get-Content (Join-Path $_.FullName 'batch-report.json') -Raw|ConvertFrom-Json).status -eq 'completed'}|Select-Object -First 1
        if(!$replay){throw "No completed replay for $($shard.id)"};$state.replay=$replay.FullName;$state.stage='replayed';Save-State $statePath $state
    }
    if(!$state.validation){
        & (Join-Path $PSScriptRoot 'Validate-AiDataset.ps1') -BatchDirectory $state.replay
        $validation=Get-ChildItem (Join-Path $state.replay 'validation') -Directory|Sort-Object LastWriteTime -Descending|Where-Object {(Get-Content (Join-Path $_.FullName 'report.json') -Raw|ConvertFrom-Json).status -eq 'passed'}|Select-Object -First 1
        if(!$validation){throw "No passed validation for $($shard.id)"};$state.validation=$validation.FullName;$state.stage='validated';Save-State $statePath $state
    }
    $completed++;Write-Output "Completed shard $($shard.id) ($completed/$MaxShards this invocation)"
    if($completed -ge $MaxShards){break}
}
