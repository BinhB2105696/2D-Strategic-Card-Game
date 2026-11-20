param(
    [switch]$Confirmation,
    [switch]$Dagger,
    [ValidateRange(1, 3)][int]$RolloutStage,
    [switch]$LearnerRollout,
    [switch]$RewardPilot,
    [switch]$RewardHard,
    [switch]$RewardRound2,
    [switch]$RewardRound3,
    [switch]$RewardCritic,
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
if (@($Confirmation, $Dagger, ($RolloutStage -gt 0), $LearnerRollout, $RewardPilot, $RewardHard, $RewardRound2, $RewardRound3, $RewardCritic |
    Where-Object { $_ }).Count -gt 1) {
    throw 'Choose one comparison stage.'
}
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$template = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'learned-rules-v6-holdout.json') -Raw | ConvertFrom-Json
$holdoutMatchups = $template.matchups
$trainingRun = 'SimulationResults/TrainingRuns/rules-v6-imitation_20261119_083127'
$stage = if ($RewardCritic) { 'reward-v3-critic-development' } elseif ($RewardRound3) { 'reward-v3-development' }
    elseif ($RewardRound2) { 'reward-v2-development' }
    elseif ($RewardHard) { 'reward-v1-hard-probe' } elseif ($RewardPilot) { 'reward-v1-development' }
    elseif ($LearnerRollout) { 'learner-rollout-v1-evaluation' }
    elseif ($RolloutStage) { 'paired-rollout-v1-evaluation' }
    elseif ($Dagger) { 'dagger-v1-evaluation' }
    elseif ($Confirmation) { 'v1-confirmation' } else { 'v1-development' }
$output = Join-Path $projectRoot "SimulationResults/LearnedCheckpointComparison/$stage"
$utf8 = [Text.UTF8Encoding]::new($false)
$variants = [ordered]@{
    mixed_hard_normal = "$trainingRun/learned-policy-v1"
    normal_only = "$trainingRun/learned-policy-v1-normal_only"
    hard_only = "$trainingRun/learned-policy-v1-hard_only"
}
if ($Confirmation) { $variants.Remove('normal_only') }
if ($Dagger) {
    $variants.Remove('normal_only')
    $variants.Remove('hard_only')
    $variants.Add('dagger_v1', 'SimulationResults/LearnedDagger/v1/candidate')
}
if ($LearnerRollout) {
    $variants.Remove('normal_only')
    $variants.Remove('hard_only')
    $variants.Add('learner_rollout_v1', 'SimulationResults/LearnedRollout/learner-v1/candidate')
    $template | Add-Member -NotePropertyName recordDecisionTimes -NotePropertyValue $true
}
$rewardComparison = $RewardRound2 -or $RewardRound3 -or $RewardCritic
$rewardEvaluation = $RewardPilot -or $RewardHard -or $rewardComparison
if ($rewardEvaluation) {
    $variants.Remove('normal_only')
    $variants.Remove('hard_only')
    $rewardRound = if ($RewardRound3 -or $RewardCritic) { 3 } elseif ($RewardRound2) { 2 } else { 1 }
    $rewardRoot = "SimulationResults/LearnedReward/v$rewardRound"
    $rewardCandidate = if ($RewardCritic) { "$rewardRoot/critic-candidate" } else { "$rewardRoot/candidate" }
    $rewardReceipt = Get-Content -LiteralPath (Join-Path $projectRoot "$rewardRoot/verified-collection.json") -Raw | ConvertFrom-Json
    foreach ($entry in $rewardReceipt.opponentSourceHashes.PSObject.Properties) {
        if ((Get-FileHash -LiteralPath (Join-Path $projectRoot $entry.Name) -Algorithm SHA256).Hash -ne $entry.Value) {
            throw "The frozen opponent source changed: $($entry.Name)."
        }
    }
    if ($rewardComparison) {
        $priorModel = 'SimulationResults/LearnedReward/v1/candidate'
        $prior = Get-Content -LiteralPath (Join-Path $projectRoot "$priorModel/export.json") -Raw | ConvertFrom-Json
        $next = Get-Content -LiteralPath (Join-Path $projectRoot "$rewardCandidate/export.json") -Raw | ConvertFrom-Json
        if ($prior.artifactSha256 -ne $rewardReceipt.policySha256 -or $next.baselineSha256 -ne $rewardReceipt.policySha256) {
            throw "Reward round $rewardRound checkpoint provenance changed."
        }
        $variants.Remove('mixed_hard_normal')
        $variants.Add('full_game_reward_v1', $priorModel)
        $nextName = if ($RewardCritic) { 'full_game_reward_v3_critic' } else { "full_game_reward_v$rewardRound" }
        $variants.Add($nextName, $rewardCandidate)
    } else {
        $variants.Add('full_game_reward_v1', "$rewardRoot/candidate")
    }
    $template | Add-Member -NotePropertyName recordDecisionTimes -NotePropertyValue $true
}
if ($RolloutStage) {
    $variants.Remove('normal_only')
    $variants.Remove('hard_only')
    $variants.Add('paired_rollout_v1', 'SimulationResults/LearnedRollout/v1/candidate')
    $variants.Add('hard_low', $null)
    $arms = @($variants.Keys)
    if (!$PrepareOnly) {
        for ($prior = 0; $prior -lt ($RolloutStage - 1); $prior++) {
            if (!(Test-Path -LiteralPath (Join-Path $output "$($arms[$prior]).result.json"))) {
                throw "Rollout stage $($prior + 1) must pass first."
            }
        }
    }
    $arm = $arms[$RolloutStage - 1]
    $modelFolder = $variants[$arm]
    $variants.Clear()
    $variants.Add($arm, $modelFolder)
    $template | Add-Member -NotePropertyName recordDecisionTimes -NotePropertyValue $true
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($name in $variants.Keys) {
    $modelFolder = $variants[$name]
    $modelPath = $null
    $hash = $null
    if ($modelFolder) {
        $modelPath = "$modelFolder/learned-policy-v1.bin"
        $export = Get-Content -LiteralPath (Join-Path $projectRoot "$modelFolder/export.json") -Raw | ConvertFrom-Json
        $hash = (Get-FileHash -LiteralPath (Join-Path $projectRoot $modelPath) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($export.dataset -ne $name -or $export.artifactSha256 -ne $hash) {
            throw "Policy artifact does not match export metadata for $name."
        }
    }

    $template.projectRoot = $projectRoot
    $template.learnedPolicyPath = $modelPath
    $template.firstSeed = if ($RewardCritic) { 2026112417 } elseif ($RewardRound3) { 2026112393 }
        elseif ($RewardRound2) { 2026112369 } elseif ($RewardHard) { 2026112331 }
        elseif ($RewardPilot) { 2026112301 } elseif ($LearnerRollout) { 2026112191 }
        elseif ($RolloutStage) { 2026112101 } elseif ($Dagger) { 2026111991 }
        elseif ($Confirmation) { 2026111961 } else { 2026111941 }
    $template.seedCount = if ($RewardHard) { 8 } elseif ($RewardPilot) { 24 }
        elseif ($LearnerRollout) { 48 } elseif ($RolloutStage) { 32 } else { 12 }
    if ($rewardComparison) {
        $template.maxMatchSeconds = 180
        $template.detailedMatches = 2
        $template.matchups = @(foreach ($opponent in 'Hard', 'Easy', 'Normal') {
            $holdoutMatchups | Where-Object { $_.deck0 -eq $_.deck1 -and $_.player1Bot -eq $opponent }
        })
    } elseif ($RewardHard) {
        $template.maxMatchSeconds = 180
        $template.detailedMatches = 2
        $template.matchups = @($holdoutMatchups | Where-Object { $_.deck0 -eq $_.deck1 -and $_.player1Bot -eq 'Hard' })
    } else {
        $template.matchups = @($holdoutMatchups | Where-Object {
            $_.deck0 -eq $_.deck1 -and ($_.player1Bot -eq 'Normal' -or
                (!$RolloutStage -and !$LearnerRollout -and $_.player1Bot -eq 'Easy'))
        })
    }
    if ($name -eq 'hard_low') {
        foreach ($matchup in $template.matchups) { $matchup.player0Bot = 'HardLow' }
    }
    if ($Confirmation -and $name -eq 'mixed_hard_normal') {
        $template.matchups += @($holdoutMatchups | Where-Object {
            $_.player1Bot -eq 'Hard' -and $_.deck0 -eq 0 -and $_.deck1 -eq 0
        })
    }
    $expected = 2 * $template.seedCount * $template.matchups.Count
    $configPath = Join-Path $output "$name.config.json"
    $receipt = Join-Path $output "$name.result.json"
    if (!(Test-Path -LiteralPath $receipt)) {
        $configJson = $template | ConvertTo-Json -Depth 8
        [IO.File]::WriteAllText($configPath, $configJson, $utf8)
    }
    $configHash = (Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash
    if ($PrepareOnly) {
        Write-Output "$name prepared ($expected matches): $configPath"
        continue
    }

    if (Test-Path -LiteralPath $receipt) {
        $previous = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
        if ($previous.configSha256 -ne $configHash -or $previous.policySha256 -ne $hash) {
            throw "Existing comparison receipt does not match the frozen configuration or artifact: $receipt"
        }
        $progress = Get-Content -LiteralPath (Join-Path $previous.runDirectory 'progress.json') -Raw | ConvertFrom-Json
        if ($progress.state -ne 'completed' -or $progress.completed -ne $expected) {
            throw "Existing result is incomplete: $receipt"
        }
        Write-Output "$name already completed: $($previous.runDirectory)"
        continue
    }

    Write-Output "Running $name ($expected matches)"
    & (Join-Path $PSScriptRoot 'Run-Simulation.ps1') -Config $configPath | Tee-Object -Variable runnerOutput
    $resultLine = $runnerOutput | Where-Object { $_ -like 'Results: *' } | Select-Object -Last 1
    if (!$resultLine) { throw "Runner did not report a result directory for $name." }
    $runDirectory = $resultLine.Substring('Results: '.Length).Trim()
    $progress = Get-Content -LiteralPath (Join-Path $runDirectory 'progress.json') -Raw | ConvertFrom-Json
    if ($progress.state -ne 'completed' -or $progress.completed -ne $expected) {
        throw "Incomplete comparison for $name. Inspect $runDirectory."
    }
    if ($RolloutStage -or $LearnerRollout -or $rewardEvaluation) {
        $matches = @(Import-Csv -LiteralPath (Join-Path $runDirectory 'matches.csv'))
        if ($matches.Count -ne $expected -or @($matches | Where-Object status -ne 'Finished').Count) {
            throw "Incomplete rollout matches for $name. Inspect $runDirectory before continuing."
        }
    }
    if ($rewardEvaluation) {
        $metadata = [IO.File]::ReadAllText((Join-Path $runDirectory 'run.json'))
        $source = [regex]::Match($metadata, '"sourceFingerprint":"([0-9a-f]{64})"').Groups[1].Value
        if ($source -ne $rewardReceipt.sourceFingerprint) {
            throw "The evaluation runner fingerprint differs from the frozen collection runner: $runDirectory"
        }
    }
    [ordered]@{candidate=$name; policySha256=$hash; configSha256=$configHash; matches=$expected; runDirectory=$runDirectory} |
        ConvertTo-Json | Set-Content -LiteralPath $receipt -Encoding UTF8
}
if (!$PrepareOnly) {
    if ($RolloutStage) { Write-Output "Rollout stage $RolloutStage complete: $output" }
    else { Write-Output "Comparison complete: $output" }
}
