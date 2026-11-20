param(
    [ValidateRange(1, 3)][int]$Stage = 1,
    [switch]$BuildRunner,
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
if ($BuildRunner -and $Stage -ne 1) { throw 'Build the runner in stage 1 only.' }
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $project 'SimulationResults/LearnedSearchExperiment/v1'
$policy = 'SimulationResults/TrainingRuns/rules-v6-imitation_20261119_083127/learned-policy-v1/learned-policy-v1.bin'
$hash = (Get-FileHash -LiteralPath (Join-Path $project $policy) -Algorithm SHA256).Hash.ToLowerInvariant()
$exportPath = Join-Path (Split-Path (Join-Path $project $policy)) 'export.json'
$export = Get-Content -LiteralPath $exportPath -Raw | ConvertFrom-Json
if ($export.dataset -ne 'mixed_hard_normal' -or $export.artifactSha256 -ne $hash) {
    throw 'The frozen mixed v1 artifact does not match its export.'
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$bots = @('Learned', 'HardLow', 'LearnedSearch')
$utf8 = [Text.UTF8Encoding]::new($false)

function Save-BatchConfig([string]$Name, [int]$FirstSeed, [int]$SeedCount) {
    $configPath = Join-Path $output "$Name.config.json"
    $receiptPath = Join-Path $output "$Name.result.json"
    $matchups = @(foreach ($bot in $bots) {
        foreach ($deck in 0,1) {
            [ordered]@{player0Bot=$bot; player1Bot='Normal'; deck0=$deck; deck1=$deck; swapBots=$true}
        }
    })
    $expected = 2 * $matchups.Count * $SeedCount
    if (!(Test-Path -LiteralPath $receiptPath)) {
        $config = [ordered]@{
            schemaVersion=1; projectRoot=$project; learnedPolicyPath=$policy; recordDecisionTimes=$true
            firstSeed=$FirstSeed; seedCount=$SeedCount; warmupMatches=0
            maxTurns=200; maxActions=10000; maxMatchSeconds=180; decisionWarningMs=1000
            detailedMatches=if ($Name -eq 'smoke') { $expected } else { 0 }
            matchups=$matchups
        }
        [IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 8), $utf8)
    }
    return [pscustomobject]@{
        Name=$Name; Config=$configPath; Receipt=$receiptPath; Expected=$expected
        ConfigHash=(Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash
    }
}

function Read-Receipt($Batch) {
    $receipt = Get-Content -LiteralPath $Batch.Receipt -Raw | ConvertFrom-Json
    if ($receipt.configSha256 -ne $Batch.ConfigHash -or $receipt.policySha256 -ne $hash -or
        $receipt.matches -ne $Batch.Expected -or !$receipt.sourceFingerprint) {
        throw "Result receipt does not match $($Batch.Name)."
    }
    return $receipt
}

function Invoke-ExperimentBatch($Batch, [string]$RequiredFingerprint) {
    if (Test-Path -LiteralPath $Batch.Receipt) {
        $receipt = Read-Receipt $Batch
        if ($RequiredFingerprint -and $receipt.sourceFingerprint -ne $RequiredFingerprint) {
            throw "Runner fingerprint differs for $($Batch.Name)."
        }
        Write-Output "$($Batch.Name) already verified: $($receipt.runDirectory)"
        return
    }
    & (Join-Path $PSScriptRoot 'Run-Simulation.ps1') -Config $Batch.Config | Tee-Object -Variable runnerOutput
    $line = $runnerOutput | Where-Object { $_ -like 'Results: *' } | Select-Object -Last 1
    if (!$line) { throw "Runner did not report a result directory for $($Batch.Name)." }
    $run = $line.Substring('Results: '.Length).Trim()
    $progress = Get-Content -LiteralPath (Join-Path $run 'progress.json') -Raw | ConvertFrom-Json
    $rows = @(Get-Content -LiteralPath (Join-Path $run 'matches.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    if ($progress.state -ne 'completed' -or $progress.completed -ne $Batch.Expected -or
        $rows.Count -ne $Batch.Expected -or @($rows | Where-Object status -ne 'Finished').Count) {
        throw "Batch $($Batch.Name) contains incomplete matches. Inspect $run before continuing."
    }

    $guided = 0
    $searchIterations = 0
    foreach ($row in $rows) {
        for ($side = 0; $side -lt 2; $side++) {
            if (!$row.decisionTimesMs -or $row.decisionTimesMs[$side].Count -ne $row.decisions[$side] -or
                $row.hardIterations[$side] -gt 24 * $row.decisions[$side]) {
                throw "Decision timing or search budget check failed in $($row.matchId)."
            }
            if ($row."bot$side" -eq 'LearnedSearch') { $guided += $row.hardGuidedRootDecisions[$side] }
            if ($row."bot$side" -eq 'HardLow') { $searchIterations += $row.hardIterations[$side] }
        }
    }
    if ($guided -lt 1 -or $searchIterations -lt 1) { throw 'Both experimental search variants must exercise their search.' }
    $metadata = [IO.File]::ReadAllText((Join-Path $run 'run.json'))
    $source = [regex]::Match($metadata, '"sourceFingerprint":"([0-9a-f]{64})"').Groups[1].Value
    $usedPolicy = [regex]::Match($metadata, '"learnedPolicySha256":"([0-9a-f]{64})"').Groups[1].Value
    if (!$source -or $usedPolicy -ne $hash -or ($RequiredFingerprint -and $source -ne $RequiredFingerprint)) {
        throw "Runner or policy fingerprint check failed in $run."
    }
    [ordered]@{
        matches=$Batch.Expected; guideUses=$guided; runDirectory=$run
        sourceFingerprint=$source; configSha256=$Batch.ConfigHash; policySha256=$hash
    } | ConvertTo-Json | Set-Content -LiteralPath $Batch.Receipt -Encoding UTF8
    Write-Output "$($Batch.Name) verified: $($Batch.Expected) finished, $guided learned ranking decisions"
}

$smoke = Save-BatchConfig 'smoke' 2026112011 4
$batch = Save-BatchConfig "stage-$Stage" (2026112021 + 16 * ($Stage - 1)) 16
if ($PrepareOnly) {
    if ($Stage -eq 1) { Write-Output "smoke: $($smoke.Expected) matches" }
    Write-Output "$($batch.Name): $($batch.Expected) matches"
    return
}
if ($Stage -eq 1) {
    if ($BuildRunner -and !(Test-Path -LiteralPath $smoke.Receipt)) {
        & (Join-Path $PSScriptRoot 'Build-TrainingRunner.ps1')
    }
    Invoke-ExperimentBatch $smoke
}
if (!(Test-Path -LiteralPath $smoke.Receipt)) { throw 'Stage 1 smoke tests must pass before continuing.' }
$fingerprint = (Read-Receipt $smoke).sourceFingerprint
for ($prior = 1; $prior -lt $Stage; $prior++) {
    $previous = Save-BatchConfig "stage-$prior" (2026112021 + 16 * ($prior - 1)) 16
    if (!(Test-Path -LiteralPath $previous.Receipt) -or (Read-Receipt $previous).sourceFingerprint -ne $fingerprint) {
        throw "Stage $prior must finish and pass validation before stage $Stage."
    }
}
Invoke-ExperimentBatch $batch $fingerprint
Write-Output "Stage $Stage complete: $output"
