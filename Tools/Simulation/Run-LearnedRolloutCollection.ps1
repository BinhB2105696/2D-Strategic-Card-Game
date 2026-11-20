param(
    [switch]$BuildRunner,
    [switch]$LearnerStates,
    [switch]$CompareHorizons,
    [switch]$TerminalFocus,
    [ValidateRange(1, 2)][int]$LearnerExtension,
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
if ($TerminalFocus) {
    $LearnerStates = $true
    $CompareHorizons = $true
}
if ($LearnerExtension -and (!$LearnerStates -or $BuildRunner)) {
    throw 'Learner extensions use the existing learner-state runner.'
}
if ($CompareHorizons -and (!$LearnerStates -or $LearnerExtension)) {
    throw 'Horizon comparison is a separate learner-state diagnostic.'
}
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$folder = if ($TerminalFocus) { 'learner-terminal-v1' } elseif ($CompareHorizons) { 'learner-horizons-v1' }
    elseif ($LearnerExtension) { "learner-v1-extension-$LearnerExtension" }
    elseif ($LearnerStates) { 'learner-v1' } else { 'v1' }
$output = Join-Path $project "SimulationResults/LearnedRollout/$folder"
$playingBot = if ($LearnerStates) { 'Learned' } else { 'HardLow' }
$policy = 'SimulationResults/TrainingRuns/rules-v6-imitation_20261119_083127/learned-policy-v1/learned-policy-v1.bin'
$policyHash = $null
if ($LearnerStates) {
    $policyPath = Join-Path $project $policy
    $export = Get-Content -LiteralPath (Join-Path (Split-Path $policyPath) 'export.json') -Raw | ConvertFrom-Json
    $policyHash = (Get-FileHash -LiteralPath $policyPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($export.dataset -ne 'mixed_hard_normal' -or $policyHash -ne $export.artifactSha256) {
        throw 'The frozen mixed v1 artifact does not match its export.'
    }
}
$utf8 = [Text.UTF8Encoding]::new($false)
New-Item -ItemType Directory -Path $output -Force | Out-Null

function New-Batch([string]$Name, [int]$FirstSeed, [int]$SeedCount, [int]$DeckCount, [bool]$Collect) {
    $path = Join-Path $output "$Name.config.json"
    $receipt = Join-Path $output "$Name.result.json"
    $count = 2 * $SeedCount * $DeckCount
    if (!(Test-Path -LiteralPath $receipt)) {
        $config = [ordered]@{
            schemaVersion=1; projectRoot=$project; collectPairedRolloutScores=($Collect -and !$LearnerStates)
            firstSeed=$FirstSeed; seedCount=$SeedCount; warmupMatches=0
            maxTurns=200; maxActions=10000; maxMatchSeconds=240; decisionWarningMs=1000
            detailedMatches=if ($Collect -or $CompareHorizons) { $count } else { 0 }
            matchups=@(for ($deck = 0; $deck -lt $DeckCount; $deck++) {
                [ordered]@{player0Bot=$playingBot; player1Bot='Normal'; deck0=$deck; deck1=$deck; swapBots=$true}
            })
        }
        if ($LearnerStates) {
            $config['learnedPolicyPath'] = $policy
            $config['compareLearnedRollouts'] = ($Collect -or $CompareHorizons)
            if ($CompareHorizons) { $config['compareRolloutHorizons'] = $Collect }
            if ($TerminalFocus) { $config['focusTerminalRollouts'] = $true }
        }
        [IO.File]::WriteAllText($path, ($config | ConvertTo-Json -Depth 8), $utf8)
    }
    return [pscustomobject]@{Name=$Name; Config=$path; Receipt=$receipt; Count=$count; Collect=$Collect}
}

function Invoke-VerifiedBatch($Batch) {
    $configHash = (Get-FileHash -LiteralPath $Batch.Config -Algorithm SHA256).Hash
    $receipt = $null
    if (Test-Path -LiteralPath $Batch.Receipt) {
        $receipt = Get-Content -LiteralPath $Batch.Receipt -Raw | ConvertFrom-Json
        if ($receipt.configSha256 -ne $configHash) { throw "Saved configuration hash changed for $($Batch.Name)." }
        $run = $receipt.runDirectory
    } else {
        & (Join-Path $PSScriptRoot 'Run-Simulation.ps1') -Config $Batch.Config | Tee-Object -Variable runnerOutput | Out-Host
        $line = $runnerOutput | Where-Object { $_ -like 'Results: *' } | Select-Object -Last 1
        if (!$line) { throw "Missing result directory for $($Batch.Name)." }
        $run = $line.Substring('Results: '.Length).Trim()
    }
    $progress = Get-Content -LiteralPath (Join-Path $run 'progress.json') -Raw | ConvertFrom-Json
    $matches = @(Get-Content -LiteralPath (Join-Path $run 'matches.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
    $labels = [int](($matches | Measure-Object pairedRolloutDecisions -Sum).Sum)
    $comparisons = if ($LearnerStates) { [int](($matches | Measure-Object learnerRolloutComparisons -Sum).Sum) } else { $labels }
    $longLabels = 0
    if ($CompareHorizons) { $longLabels = [int](($matches | Measure-Object rolloutHorizonComparisons -Sum).Sum) }
    if ($progress.state -ne 'completed' -or $progress.completed -ne $Batch.Count -or
        $matches.Count -ne $Batch.Count -or @($matches | Where-Object status -ne 'Finished').Count -or
        (($Batch.Collect -or $CompareHorizons) -and $labels -le 0) -or
        (!$Batch.Collect -and !$CompareHorizons -and ($labels -ne 0 -or $comparisons -ne 0)) -or
        ($CompareHorizons -and $Batch.Collect -and $longLabels -ne $labels) -or
        ($CompareHorizons -and !$Batch.Collect -and $longLabels -ne 0)) {
        throw "Incomplete matches or invalid label counts in $run."
    }
    $metadata = [IO.File]::ReadAllText((Join-Path $run 'run.json'))
    $fingerprint = [regex]::Match($metadata, '"sourceFingerprint":"([0-9a-f]{64})"').Groups[1].Value
    if (!$fingerprint) { throw "Runner fingerprint missing in $run." }
    if ($LearnerStates -and
        [regex]::Match($metadata, '"learnedPolicySha256":"([0-9a-f]{64})"').Groups[1].Value -ne $policyHash) {
        throw "The simulated learner used a different policy in $run."
    }
    if (!$receipt) {
        [ordered]@{runDirectory=$run; configSha256=$configHash; sourceFingerprint=$fingerprint} |
            ConvertTo-Json | Set-Content -LiteralPath $Batch.Receipt -Encoding UTF8
    }
    Write-Host "$($Batch.Name) verified ($($Batch.Count) matches, $labels labelled decisions)"
    return [pscustomobject]@{Run=$run; Matches=$matches; Fingerprint=$fingerprint; Labels=$labels; Comparisons=$comparisons; LongLabels=$longLabels}
}

function Get-ShortScores([string]$Run, [string]$Trace) {
    Get-Content -LiteralPath (Join-Path $Run "traces/$Trace") | Where-Object { $_.Contains('"learnerRolloutComparison":') } | ForEach-Object {
        $row = $_ | ConvertFrom-Json
        $label = $row.learnerRolloutComparison
        $label.PSObject.Properties.Remove('longRelativeScores')
        $label.PSObject.Properties.Remove('longTerminalRewards')
        [pscustomobject]@{sequence=$row.sequence; label=$label} | ConvertTo-Json -Depth 10 -Compress
    }
}

$collectionSeed = if ($TerminalFocus) { 2026112261 } elseif ($CompareHorizons) { 2026112251 }
    elseif ($LearnerExtension) { 2026112161 + 12 * ($LearnerExtension - 1) }
    elseif ($LearnerStates) { 2026112141 } else { 2026112081 }
$collectionSeeds = if ($CompareHorizons -and !$TerminalFocus) { 6 } else { 12 }
$collection = New-Batch 'collection' $collectionSeed $collectionSeeds 2 $true
if ($PrepareOnly) {
    Write-Output "Prepared $($collection.Count) collection matches starting at seed $collectionSeed."
    return
}
if ($LearnerExtension) {
    $priorFolder = if ($LearnerExtension -eq 2) { 'learner-v1-extension-1' } else { 'learner-v1' }
    $priorRoot = Join-Path $project "SimulationResults/LearnedRollout/$priorFolder"
    $priorPath = Join-Path $priorRoot 'verified-collection.json'
    if (!(Test-Path -LiteralPath $priorPath)) { throw 'The previous learner-state collection must pass replay before continuing.' }
    $priorCollection = Get-Content -LiteralPath $priorPath -Raw | ConvertFrom-Json
    $priorReplay = Get-Content -LiteralPath $priorCollection.replayReport -Raw | ConvertFrom-Json
    $priorReceipt = Get-Content -LiteralPath (Join-Path $priorRoot 'collection.result.json') -Raw | ConvertFrom-Json
    if ($priorReplay.status -ne 'completed' -or $priorReplay.verifiedMatches -ne 48 -or
        $priorReplay.sourceRun -ne $priorCollection.runDirectory -or
        $priorReceipt.runDirectory -ne $priorCollection.runDirectory -or !$priorReceipt.sourceFingerprint) {
        throw 'The previous learner-state collection must pass replay before continuing.'
    }
    $requiredFingerprint = $priorReceipt.sourceFingerprint
} else {
    $smokeSeed = if ($TerminalFocus) { 2026112258 } elseif ($CompareHorizons) { 2026112247 }
        elseif ($LearnerStates) { 2026112135 } else { 2026112073 }
    $plain = New-Batch 'smoke-control' $smokeSeed 2 1 $false
    $scored = New-Batch 'smoke-scored' $smokeSeed 2 1 $true
    if ($BuildRunner -and !(Test-Path -LiteralPath $plain.Receipt)) {
        & (Join-Path $PSScriptRoot 'Build-TrainingRunner.ps1')
    }
    $before = Invoke-VerifiedBatch $plain
    $after = Invoke-VerifiedBatch $scored
    $control = @{}
    foreach ($match in $before.Matches) { $control.Add($match.matchId, $match) }
    foreach ($match in $after.Matches) {
        $original = $control[$match.matchId]
        if (!$original -or $match.actionDigest -ne $original.actionDigest -or $match.finalStateHash -ne $original.finalStateHash) {
            throw "Collecting rollout scores changed the played trajectory: $($match.matchId)."
        }
        if ($CompareHorizons -and
            ((Get-ShortScores $before.Run $original.trace) -join "`n") -ne ((Get-ShortScores $after.Run $match.trace) -join "`n")) {
            throw "The long continuation changed short rollout labels: $($match.matchId)."
        }
    }
    if ($before.Fingerprint -ne $after.Fingerprint) { throw 'Smoke runner fingerprints differ.' }
    $requiredFingerprint = $after.Fingerprint
    Write-Output 'Smoke PASS: rollout scoring preserved all played trajectories.'
}

$data = Invoke-VerifiedBatch $collection
if ($data.Fingerprint -ne $requiredFingerprint) { throw 'The collection runner differs from the verified runner.' }
$reportPath = Join-Path $output 'verified-collection.json'
if (Test-Path -LiteralPath $reportPath) {
    $existing = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($existing.runDirectory -ne $data.Run) { throw 'Saved replay report belongs to another run.' }
    $replay = $existing.replayReport
} else {
    & (Join-Path $PSScriptRoot 'Replay-AiBatch.ps1') -RunDirectory $data.Run | Tee-Object -Variable replayOutput
    $line = $replayOutput | Where-Object { $_ -like 'Replay batch: *' } | Select-Object -Last 1
    if (!$line) { throw 'Missing collection replay batch.' }
    $replay = Join-Path ($line.Substring('Replay batch: '.Length).Trim()) 'batch-report.json'
}
$verified = Get-Content -LiteralPath $replay -Raw | ConvertFrom-Json
if ($verified.status -ne 'completed' -or $verified.verifiedMatches -ne $collection.Count -or $verified.sourceRun -ne $data.Run) {
    throw 'The collection has incomplete replay verification.'
}
[ordered]@{runDirectory=$data.Run; replayReport=$replay; labelledDecisions=$data.Labels
    comparedDecisions=$data.Comparisons; horizonComparisons=$data.LongLabels} |
    ConvertTo-Json | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Output "Paired rollout collection complete: $reportPath"
