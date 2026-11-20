param(
    [ValidateRange(1, 3)][int]$Round = 1,
    [switch]$BuildRunner,
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $project "SimulationResults/LearnedReward/v$Round"
$policy = if ($Round -ge 2) { 'SimulationResults/LearnedReward/v1/candidate/learned-policy-v1.bin' }
    else { 'SimulationResults/TrainingRuns/rules-v6-imitation_20261119_083127/learned-policy-v1/learned-policy-v1.bin' }
$dataset = if ($Round -ge 2) { 'full_game_reward_v1' } else { 'mixed_hard_normal' }
$policyPath = Join-Path $project $policy
$export = Get-Content -LiteralPath (Join-Path (Split-Path $policyPath) 'export.json') -Raw | ConvertFrom-Json
$policyHash = (Get-FileHash -LiteralPath $policyPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($export.dataset -ne $dataset -or $export.artifactSha256 -ne $policyHash) {
    throw "Reward round $Round requires its frozen starting policy."
}
$opponents = [ordered]@{}
foreach ($path in 'Assets/Scripts/Core/EasyBot.cs', 'Assets/Scripts/Bots/NormalBot.cs',
    'Assets/Scripts/Bots/HardBot.cs', 'Assets/Scripts/Bots/HardTacticalPolicy.cs',
    'Assets/Scripts/Bots/HardStrategicEvaluation.cs') {
    $opponents[$path] = (Get-FileHash -LiteralPath (Join-Path $project $path) -Algorithm SHA256).Hash
}
$previousFingerprint = $null
$previousCatalog = $null
if ($Round -ge 2) {
    $previous = Get-Content -LiteralPath (Join-Path $project 'SimulationResults/LearnedReward/v1/verified-collection.json') -Raw | ConvertFrom-Json
    if ($export.baselineSha256 -ne $previous.policySha256 -or !$previous.sourceFingerprint) {
        throw 'Starting checkpoint provenance differs from the verified round 1 collection.'
    }
    foreach ($path in $opponents.Keys) {
        if ($opponents[$path] -ne $previous.opponentSourceHashes.$path) { throw "Frozen opponent changed: $path" }
    }
    if ($Round -eq 2) { $previousFingerprint = $previous.sourceFingerprint }
    if ($Round -eq 3) {
        $control = Get-Content -LiteralPath (Join-Path $project 'SimulationResults/LearnedReward/v2/verified-collection.json') -Raw | ConvertFrom-Json
        $controlMeta = [IO.File]::ReadAllText((Join-Path $control.runDirectory 'run.json'))
        $previousCatalog = [regex]::Match($controlMeta, '"catalogFingerprint":"([0-9a-f]{64})"').Groups[1].Value
        if ($control.policySha256 -ne $policyHash -or !$previousCatalog) {
            throw 'The stronger-exploration control must use the same starting checkpoint and catalog.'
        }
    }
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)

function New-Collection([string]$Name, [int]$FirstSeed, [int]$SeedCount, [int]$ExplorationSeed, [bool]$Smoke) {
    $file = Join-Path $output "$Name.config.json"
    $receipt = Join-Path $output "$Name.result.json"
    $strong = if ($Round -ge 2) { 'Hard' } else { 'Learned' }
    $pool = if ($Smoke) { @('Normal', $strong) } else { @('Easy', 'Normal', $strong) }
    $decks = if ($Smoke) { @(0) } else { @(0, 1) }
    $matchups = @(foreach ($opponent in $pool) {
        foreach ($deck in $decks) {
            [ordered]@{player0Bot='LearnedTrain'; player1Bot=$opponent; deck0=$deck; deck1=$deck; swapBots=$true}
        }
    })
    $count = 2 * $SeedCount * $matchups.Count
    if (!(Test-Path -LiteralPath $receipt)) {
        $settings = [ordered]@{
            schemaVersion=1; projectRoot=$project; learnedPolicyPath=$policy; learnerExplorationSeed=$ExplorationSeed
            firstSeed=$FirstSeed; seedCount=$SeedCount; warmupMatches=0; maxTurns=200; maxActions=10000
            maxMatchSeconds=180; decisionWarningMs=1000; detailedMatches=$count; matchups=$matchups
        }
        if ($Round -eq 3) { $settings['lowerLearnerExploration'] = $true }
        [IO.File]::WriteAllText($file, ($settings | ConvertTo-Json -Depth 8), $utf8)
    }
    return [pscustomobject]@{Config=$file; Receipt=$receipt; Count=$count}
}

function Invoke-Collection($Batch, [string]$RequiredFingerprint) {
    $configHash = (Get-FileHash -LiteralPath $Batch.Config -Algorithm SHA256).Hash
    if (Test-Path -LiteralPath $Batch.Receipt) {
        $saved = Get-Content -LiteralPath $Batch.Receipt -Raw | ConvertFrom-Json
        if ($saved.configSha256 -ne $configHash -or $saved.policySha256 -ne $policyHash) {
            throw 'The saved reward collection used different inputs.'
        }
        $run = $saved.runDirectory
    } else {
        & (Join-Path $PSScriptRoot 'Run-Simulation.ps1') -Config $Batch.Config | Tee-Object -Variable runnerOutput | Out-Host
        $line = $runnerOutput | Where-Object { $_ -like 'Results: *' } | Select-Object -Last 1
        if (!$line) { throw 'Missing reward collection directory.' }
        $run = $line.Substring('Results: '.Length).Trim()
    }
    $progress = Get-Content -LiteralPath (Join-Path $run 'progress.json') -Raw | ConvertFrom-Json
    $matches = @(Get-Content -LiteralPath (Join-Path $run 'matches.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    if ($progress.state -ne 'completed' -or $progress.completed -ne $Batch.Count -or
        $matches.Count -ne $Batch.Count -or @($matches | Where-Object { $_.status -ne 'Finished' -or $_.behaviorSamples -le 0 }).Count) {
        throw "Missing completed trajectory or exploration samples in $run."
    }
    $metadata = [IO.File]::ReadAllText((Join-Path $run 'run.json'))
    $source = [regex]::Match($metadata, '"sourceFingerprint":"([0-9a-f]{64})"').Groups[1].Value
    $usedPolicy = [regex]::Match($metadata, '"learnedPolicySha256":"([0-9a-f]{64})"').Groups[1].Value
    if (!$source -or $usedPolicy -ne $policyHash -or ($RequiredFingerprint -and $source -ne $RequiredFingerprint)) {
        throw "Unexpected game build or learner policy in $run."
    }
    if ($Round -eq 3) {
        $catalog = [regex]::Match($metadata, '"catalogFingerprint":"([0-9a-f]{64})"').Groups[1].Value
        $header = Get-Content -LiteralPath (Join-Path $run ('traces/' + $matches[0].trace)) -TotalCount 1 | ConvertFrom-Json
        $profile = $header.botProfiles | Where-Object bot -eq 'LearnedTrain' | Select-Object -First 1
        if ($catalog -ne $previousCatalog -or $profile.explorationTemperature -ne 0.35 -or $profile.uniformExploration -ne 0.02) {
            throw 'Wrong catalog or low-exploration profile. Use the rebuilt runner.'
        }
    }
    foreach ($path in $opponents.Keys) {
        if ((Get-FileHash -LiteralPath (Join-Path $project $path) -Algorithm SHA256).Hash -ne $opponents[$path]) {
            throw "An opponent source changed during collection: $path"
        }
    }
    $receipt = [ordered]@{
        runDirectory=$run; sourceFingerprint=$source; policySha256=$policyHash
        configSha256=$configHash; opponentSourceHashes=$opponents
    }
    $receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Batch.Receipt -Encoding UTF8
    return [pscustomobject]$receipt
}

$smoke = if ($Round -ge 2) { New-Collection 'smoke' 2026112341 2 7413937 $true }
    else { New-Collection 'smoke' 2026112276 2 7413917 $true }
$collection = if ($Round -ge 2) { New-Collection 'collection' 2026112345 16 7413947 $false }
    else { New-Collection 'collection' 2026112281 16 7413927 $false }
if ($PrepareOnly) {
    Write-Output "Prepared smoke: $($smoke.Count) matches; collection: $($collection.Count) matches."
    return
}
if ($BuildRunner -and !(Test-Path -LiteralPath $smoke.Receipt)) {
    & (Join-Path $PSScriptRoot 'Build-TrainingRunner.ps1')
}
$smokeResult = Invoke-Collection $smoke $previousFingerprint
Write-Output 'Smoke PASS: completed trajectories and exploration samples.'
$result = Invoke-Collection $collection $smokeResult.sourceFingerprint
$verifiedPath = Join-Path $output 'verified-collection.json'
if (Test-Path -LiteralPath $verifiedPath) {
    $saved = Get-Content -LiteralPath $verifiedPath -Raw | ConvertFrom-Json
    if ($saved.runDirectory -ne $result.runDirectory) { throw 'Saved replay results belong to a different collection.' }
    $replay = $saved.replayReport
} else {
    & (Join-Path $PSScriptRoot 'Replay-AiBatch.ps1') -RunDirectory $result.runDirectory | Tee-Object -Variable replayOutput
    $line = $replayOutput | Where-Object { $_ -like 'Replay batch: *' } | Select-Object -Last 1
    if (!$line) { throw 'No reward collection replay report.' }
    $replay = Join-Path ($line.Substring('Replay batch: '.Length).Trim()) 'batch-report.json'
}
$verified = Get-Content -LiteralPath $replay -Raw | ConvertFrom-Json
if ($verified.status -ne 'completed' -or $verified.verifiedMatches -ne $collection.Count -or
    $verified.sourceRun -ne $result.runDirectory) {
    throw 'Replay verification is incomplete.'
}
[ordered]@{
    runDirectory=$result.runDirectory; replayReport=$replay; policySha256=$policyHash
    sourceFingerprint=$result.sourceFingerprint; opponentSourceHashes=$opponents
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $verifiedPath -Encoding UTF8
Write-Output "Reward collection complete: $verifiedPath"
