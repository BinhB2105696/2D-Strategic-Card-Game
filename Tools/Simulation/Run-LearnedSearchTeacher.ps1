param(
    [ValidateSet('Smoke', 'Development', 'Confirmation')][string]$Stage = 'Smoke',
    [switch]$BuildRunner,
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
if ($BuildRunner -and $Stage -ne 'Smoke') { throw 'Build the runner with the Smoke stage.' }
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $project 'SimulationResults/LearnedSearchTeacher/v1'
$plans = @{
    Smoke = @{firstSeed=2026112441; seedCount=2; detailedMatches=8}
    Development = @{firstSeed=2026112451; seedCount=24; detailedMatches=4}
    Confirmation = @{firstSeed=2026112501; seedCount=48; detailedMatches=4}
}
$plan = $plans[$Stage]
$expected = 4 * $plan.seedCount
$configPath = Join-Path $output "$Stage.config.json"
$receiptPath = Join-Path $output "$Stage.result.json"
$config = [ordered]@{
    schemaVersion=1; projectRoot=$project; firstSeed=$plan.firstSeed; seedCount=$plan.seedCount
    warmupMatches=0; maxTurns=200; maxActions=10000; maxMatchSeconds=300; decisionWarningMs=1000
    detailedMatches=$plan.detailedMatches
    matchups=@(foreach ($deck in 0,1) {
        [ordered]@{player0Bot='SearchTeacher'; player1Bot='Hard'; deck0=$deck; deck1=$deck; swapBots=$true}
    })
}

function Get-SeedInterval([double[]]$SeedRates) {
    $random = [Random]::new(20261120)
    $resamples = [double[]]::new(10000)
    for ($i = 0; $i -lt $resamples.Length; $i++) {
        $total = 0.0
        for ($j = 0; $j -lt $SeedRates.Length; $j++) {
            $total += $SeedRates[$random.Next($SeedRates.Length)]
        }
        $resamples[$i] = $total / $SeedRates.Length
    }
    [Array]::Sort($resamples)
    return @($resamples[250], $resamples[9749])
}

function Measure-TeacherRun($Rows, $Settings, [string]$Batch) {
    $matchCount = 4 * $Settings.seedCount
    if ($Rows.Count -ne $matchCount) { throw "Expected $matchCount results in $Batch." }
    $seen = [Collections.Generic.HashSet[string]]::new()
    $winsByDeck = @(0, 0)
    $winsBySeed = [double[]]::new($Settings.seedCount)
    $draws = $teacherSearches = $hardSearches = $teacherIterations = $hardIterations = 0
    foreach ($row in $Rows) {
        $deck = [int]$row.deck0
        $leg = [int]$row.leg
        $offset = [int]$row.seed - $Settings.firstSeed
        $hardSide = 1 - $leg
        if ($row.status -ne 'Finished' -or $row.error -or $row.winner -notin -1,0,1 -or
            $deck -notin 0,1 -or $deck -ne $row.deck1 -or $deck -ne $row.scenario -or $leg -notin 0,1 -or
            $offset -lt 0 -or $offset -ge $Settings.seedCount -or !$seen.Add("$deck/$($row.seed)/$leg") -or
            $row."bot$leg" -ne 'SearchTeacher' -or $row."bot$hardSide" -ne 'Hard') {
            throw "Incomplete or unexpected match $($row.matchId) in $Batch."
        }
        if ($row.hardIterations[$leg] -gt 192 * $row.decisions[$leg] -or
            $row.hardIterations[$hardSide] -gt 96 * $row.decisions[$hardSide]) {
            throw "Unexpected search budget in $($row.matchId)."
        }
        $teacherSearches += $row.hardSearchedDecisions[$leg]
        $hardSearches += $row.hardSearchedDecisions[$hardSide]
        $teacherIterations += $row.hardIterations[$leg]
        $hardIterations += $row.hardIterations[$hardSide]
        if ($row.winner -eq $leg) {
            $winsByDeck[$deck]++
            $winsBySeed[$offset]++
        }
        if ($row.winner -eq -1) { $draws++ }
    }
    if ($teacherSearches -lt 1 -or $hardSearches -lt 1) { throw 'Both bots must exercise their search.' }
    $wins = $winsByDeck[0] + $winsByDeck[1]
    $rate = $wins / $matchCount
    $interval = if ($Batch -ne 'Smoke') { Get-SeedInterval @($winsBySeed | ForEach-Object { $_ / 4 }) }
    $gate = switch ($Batch) {
        'Development' { $rate -ge 0.55 -and $winsByDeck[0] -ge $Settings.seedCount -and $winsByDeck[1] -ge $Settings.seedCount }
        'Confirmation' { $interval[0] -gt 0.50 -and $winsByDeck[0] -gt $Settings.seedCount -and $winsByDeck[1] -gt $Settings.seedCount }
    }
    $durations = @($Rows.durationSeconds | Sort-Object)
    return [ordered]@{
        wins=$wins; losses=$matchCount-$wins-$draws; draws=$draws; winRate=$rate
        winsByDeck=$winsByDeck; gamesPerDeck=2*$Settings.seedCount; seedBlock95=$interval; passesGate=$gate
        teacherSearches=$teacherSearches; hardSearches=$hardSearches
        teacherIterationsPerSearch=$teacherIterations/$teacherSearches; hardIterationsPerSearch=$hardIterations/$hardSearches
        meanMatchSeconds=($durations | Measure-Object -Average).Average
        p95MatchSeconds=$durations[[math]::Ceiling(0.95*$matchCount)-1]
    }
}

function Read-TeacherReceipt([string]$Batch) {
    $path = Join-Path $output "$Batch.result.json"
    if (!(Test-Path -LiteralPath $path)) { throw "Run $Batch first." }
    $receipt = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $hash = (Get-FileHash -LiteralPath (Join-Path $output "$Batch.config.json") -Algorithm SHA256).Hash
    $progress = Get-Content -LiteralPath (Join-Path $receipt.runDirectory 'progress.json') -Raw | ConvertFrom-Json
    if ($receipt.stage -ne $Batch -or $receipt.configSha256 -ne $hash -or $receipt.matches -ne 4*$plans[$Batch].seedCount -or
        $progress.state -ne 'completed' -or $progress.completed -ne $receipt.matches -or
        !$receipt.sourceFingerprint -or !$receipt.catalogFingerprint) { throw "Invalid $Batch receipt." }
    return $receipt
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
if (!(Test-Path -LiteralPath $receiptPath)) {
    [IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
}
if ($PrepareOnly) { Write-Output "$Stage prepared ($expected matches): $configPath"; return }

git -C $project diff --quiet 8fdb490 -- Assets/Scripts/Bots/HardBot.cs Assets/Scripts/Bots/HardTacticalPolicy.cs Assets/Scripts/Bots/HardStrategicEvaluation.cs Assets/Scripts/Bots/NormalBot.cs Assets/Scripts/Core/EasyBot.cs
if ($LASTEXITCODE -ne 0) { throw 'Could not verify the frozen hand-coded opponents against 8fdb490.' }
$smoke = if ($Stage -ne 'Smoke') { Read-TeacherReceipt 'Smoke' }
if ($Stage -eq 'Confirmation' -and !(Read-TeacherReceipt 'Development').summary.passesGate) {
    throw 'Development did not meet the win-rate gate. Review the result before running more games.'
}
if (Test-Path -LiteralPath $receiptPath) {
    Read-TeacherReceipt $Stage | ConvertTo-Json -Depth 6
    return
}
if ($BuildRunner) { & (Join-Path $PSScriptRoot 'Build-TrainingRunner.ps1') }
& (Join-Path $PSScriptRoot 'Run-Simulation.ps1') -Config $configPath | Tee-Object -Variable runnerOutput | Out-Host
$line = $runnerOutput | Where-Object { $_ -like 'Results: *' } | Select-Object -Last 1
if (!$line) { throw 'The runner did not report a result folder.' }
$run = $line.Substring('Results: '.Length).Trim()
$progress = Get-Content -LiteralPath (Join-Path $run 'progress.json') -Raw | ConvertFrom-Json
if ($progress.state -ne 'completed' -or $progress.completed -ne $expected) { throw "Incomplete ${Stage}: $run" }
$rows = @(Get-Content -LiteralPath (Join-Path $run 'matches.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
$summary = Measure-TeacherRun $rows $plan $Stage
$detailedResults = @($rows | Where-Object trace)
if ($detailedResults.Count -ne $plan.detailedMatches) { throw 'Expected the planned detailed traces.' }
foreach ($row in $detailedResults) {
    $start = Get-Content -LiteralPath (Join-Path (Join-Path $run 'traces') "$($row.matchId).jsonl") -TotalCount 1 | ConvertFrom-Json
    $teacher = $start.botProfiles[$row.leg]
    $hard = $start.botProfiles[1-$row.leg]
    if ($start.matchId -ne $row.matchId -or $start.seed -ne $row.seed -or !$start.detailed -or
        $start.bot0 -ne $row.bot0 -or $start.bot1 -ne $row.bot1 -or
        $hard.iterations -ne 96 -or $teacher.iterations -ne 192 -or $hard.learnedRootRanking -or $teacher.learnedRootRanking -or
        $hard.rootCandidateLimit -or $teacher.rootCandidateLimit -or $hard.algorithm -ne $teacher.algorithm -or
        $hard.treeDepth -ne $teacher.treeDepth -or $hard.rolloutSteps -ne $teacher.rolloutSteps -or
        $hard.policySeed -ne $teacher.policySeed -or $hard.pairedRootSampling -ne $teacher.pairedRootSampling -or
        $hard.turnHorizon -ne $teacher.turnHorizon) { throw "Unexpected search profiles in $($row.matchId)." }
}
$metadata = Get-Content -LiteralPath (Join-Path $run 'run.json') -Raw
$source = [regex]::Match($metadata, '"sourceFingerprint":"([a-f0-9]{64})"').Groups[1].Value
$catalog = [regex]::Match($metadata, '"catalogFingerprint":"([a-f0-9]{64})"').Groups[1].Value
if (!$source -or !$catalog -or $metadata -notmatch '"rulesVersion":6\b' -or $metadata -notmatch '"catalogVersion":4\b' -or
    ($smoke -and ($source -ne $smoke.sourceFingerprint -or $catalog -ne $smoke.catalogFingerprint))) {
    throw "Unexpected runner or catalog fingerprint in $run."
}
$receipt = [ordered]@{
    stage=$Stage; matches=$expected; runDirectory=$run; summary=$summary
    configSha256=(Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash
    sourceFingerprint=$source; catalogFingerprint=$catalog
}
$json = $receipt | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText($receiptPath, $json, [Text.UTF8Encoding]::new($false))
Write-Output $json
Write-Output "$Stage complete: $receiptPath"
