#requires -Version 7.0
param([Parameter(Mandatory=$true)][string]$BatchDirectory)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$batch = [IO.Path]::GetFullPath($BatchDirectory)
$prefix = (Join-Path $project 'SimulationResults').TrimEnd('\') + '\'
if (!$batch.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'BatchDirectory must be inside SimulationResults.' }
$output = Join-Path $batch ('validation/' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $output | Out-Null
$reportPath = Join-Path $output 'report.json'
$metrics = [ordered]@{schema=1;status='failed';sourceBatch=$batch;episodes=0;transitions=0;normalDecisions=0;easyDecisions=0;policiesChecked=0;checks=0;datasetBytes=0;errors=@()}
$episodes = [Collections.Generic.List[object]]::new()
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$cards = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$actionCounts = @{}
function Read-Json([string]$Path) { Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable }
function Check([bool]$Condition,[string]$Reason) {
    $metrics.checks++
    if (!$Condition) { throw $Reason }
}
function Validate-Policy([System.Collections.IDictionary]$Policy) {
    Check ($Policy.schema -eq 2 -and @($Policy.globalFeatures).Count -eq 21) 'Invalid policy schema/global dimensions.'
    Check (@($Policy.Keys | Where-Object { $_ -match '^(seed|instanceId|sourceFingerprint|catalogFingerprint|deckDefinitions|cardDefinitions)$' }).Count -eq 0) 'Diagnostic metadata appeared as policy fields.'
    foreach ($number in $Policy.globalFeatures) {
        Check ($number -is [ValueType] -and ![double]::IsNaN([double]$number) -and ![double]::IsInfinity([double]$number)) 'Non-finite/non-numeric global feature.'
    }
    $entities=@($Policy.entities); $actions=@($Policy.legalActions); $mask=@($Policy.legalActionMask)
    Check ($mask.Count -eq $actions.Count -and @($mask | Where-Object { $_ -ne 1 }).Count -eq 0) 'Legal mask does not match legal candidates.'
    foreach ($entity in $entities) {
        Check (@($entity.features).Count -eq 18) 'Invalid entity dimensions.'
        Check ($entity.features[3] -in @(0,1)) 'Invalid known/hidden flag.'
        if ($entity.features[3] -eq 0) { Check ([string]::IsNullOrEmpty($entity.cardId)) 'Hidden/opaque entity leaked a card identity.' }
        else { Check (![string]::IsNullOrEmpty($entity.cardId)) 'Known entity has no public card ID.'; [void]$cards.Add($entity.cardId) }
        foreach ($number in $entity.features) {
            Check ($number -is [ValueType] -and ![double]::IsNaN([double]$number) -and ![double]::IsInfinity([double]$number)) 'Non-finite/non-numeric entity feature.'
        }
    }
    foreach ($action in $actions) {
        Check (@($action.features).Count -eq 4) 'Invalid action dimensions.'
        foreach ($number in $action.features) {
            Check ($number -is [ValueType] -and ![double]::IsNaN([double]$number) -and ![double]::IsInfinity([double]$number)) 'Non-finite/non-numeric action feature.'
        }
        foreach ($index in @($action.source) + @($action.targets) + @($action.tributes)) {
            Check ($index -is [ValueType] -and $index -ge -1 -and $index -lt $entities.Count -and [double]$index -eq [int]$index) 'Action reference is not a local entity index/absent sentinel.'
        }
    }
    foreach ($mark in $Policy.retaliationMarks) {
        Check (@($mark.features).Count -eq 2 -and $mark.source -ge 0 -and $mark.source -lt $entities.Count -and $mark.target -ge 0 -and $mark.target -lt $entities.Count) 'Invalid mark dimensions/entity references.'
        Check ($entities[$mark.source].features[3] -eq 1 -and $entities[$mark.target].features[3] -eq 1) 'Mark reveals a hidden endpoint.'
        Check ($mark.features[0] -gt 0 -and $mark.features[1] -ge $Policy.globalFeatures[0]) 'Invalid/expired retaliation mark.'
    }
    $metrics.policiesChecked++
}
try {
    $batchReport = Read-Json (Join-Path $batch 'batch-report.json')
    Check ($batchReport.status -eq 'completed' -and $batchReport.verifiedMatches -eq 12 -and @($batchReport.results).Count -eq 12) 'Expected a completed 12-match replay/export batch.'
    $run = [IO.Path]::GetFullPath($batchReport.sourceRun)
    Check ($run.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) 'Source run is outside SimulationResults.'
    $metadata = Read-Json (Join-Path $run 'run.json')
    Check ($metadata.rulesVersion -eq 4 -and $metadata.catalogVersion -eq 4 -and $metadata.aiContract.schema -eq 2) 'Unexpected rules/catalog/AI versions.'
    Check (@($metadata.cardDefinitions).Count -eq 41 -and @($metadata.deckDefinitions).Count -ge 3) 'Expected 41 cards and at least the three playable presets.'
    $rows=@(Import-Csv -LiteralPath (Join-Path $run 'matches.csv'))
    Check ($rows.Count -eq 12 -and @($rows | Where-Object status -ne 'Finished').Count -eq 0) 'Source pilot has unfinished/failed episodes.'
    foreach ($result in $batchReport.results) {
        $verified=Read-Json $result.report
        Check ($verified.verified -and $verified.completeMatch -and !$verified.truncated -and $verified.status -eq 'verified-match') 'An export is not a fully verified match.'
        $path=[IO.Path]::GetFullPath($result.dataset)
        Check ($path.StartsWith($batch.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -and $path -eq [IO.Path]::GetFullPath($verified.dataset)) 'Dataset/report path mismatch.'
        $row=@($rows | Where-Object trace -eq $result.trace)
        Check ($row.Count -eq 1) 'Trace does not map to exactly one source match.'
        $row=$row[0]; $header=$null; $ending=$null; $count=0; $perPlayer=@(0,0); $lastSequence=@(-1,-1)
        $terminal=[Collections.Generic.List[object]]::new()
        $reader=[IO.StreamReader]::new($path)
        try {
            while ($null -ne ($line=$reader.ReadLine())) {
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $record=$line | ConvertFrom-Json -AsHashtable
                Check ($null -eq $ending) 'Records appeared after episode_end.'
                if ($record.record -eq 'dataset_start') {
                    Check ($null -eq $header -and $count -eq 0) 'Duplicate/misplaced dataset header.'
                    $header=$record
                    Check ($header.schema -eq 2 -and $header.matchId -eq $row.match_id -and $header.sourceFingerprint -eq $metadata.sourceFingerprint -and $header.catalogFingerprint -eq $metadata.catalogFingerprint) 'Dataset provenance/version mismatch.'
                    Check ([IO.Path]::GetFullPath($header.sourceTrace) -eq [IO.Path]::GetFullPath((Join-Path $run ('traces/'+$row.trace)))) 'Dataset points to the wrong source trace.'
                } elseif ($record.record -eq 'transition') {
                    Check ($null -ne $header -and $record.schema -eq 2 -and $record.player -in @(0,1) -and !$record.truncated) 'Invalid/truncated transition.'
                    $player=[int]$record.player
                    Check ($record.sequence -gt $lastSequence[$player]) 'Non-increasing sequence for the same player.'
                    $lastSequence[$player]=$record.sequence
                    Check ($seen.Add($header.matchId+'/'+$player+'/'+$record.sequence)) 'Duplicate transition.'
                    $policy=$record.policyInputJson | ConvertFrom-Json -AsHashtable
                    $nextPolicy=$record.nextPolicyInputJson | ConvertFrom-Json -AsHashtable
                    Validate-Policy $policy; Validate-Policy $nextPolicy
                    Check ($record.selectedActionIndex -ge 0 -and $record.selectedActionIndex -lt @($policy.legalActions).Count -and [double]$record.selectedActionIndex -eq [int]$record.selectedActionIndex) 'Selected index is outside legal candidates.'
                    Check ($record.reward -in @(-1,0,1)) 'Unexpected reward.'
                    if ($record.terminated) {
                        Check ($nextPolicy.globalFeatures[16] -eq 1) 'Terminal transition has a non-terminal next observation.'
                        $terminal.Add(@{player=$player;reward=$record.reward})
                    } else { Check ($record.reward -eq 0 -and $nextPolicy.globalFeatures[16] -eq 0) 'Ongoing transition has terminal reward/state.' }
                    $bot=if($player -eq 0){$row.bot0}else{$row.bot1}
                    Check ($bot -in @('Normal','Easy')) 'Unexpected teacher policy label.'
                    if ($bot -eq 'Normal') { $metrics.normalDecisions++ } else { $metrics.easyDecisions++ }
                    $key=$bot+'/'+[string]$policy.legalActions[$record.selectedActionIndex].features[0]
                    if (!$actionCounts.ContainsKey($key)) {$actionCounts[$key]=0}; $actionCounts[$key]++
                    $count++; $perPlayer[$player]++; $metrics.transitions++
                } elseif ($record.record -eq 'episode_end') { $ending=$record }
                else { throw ('Unknown dataset record: '+$record.record) }
            }
        } finally { $reader.Dispose() }
        Check ($null -ne $header -and $null -ne $ending -and $ending.status -eq 'Finished' -and $ending.terminated -and !$ending.truncated -and $ending.winner -eq [int]$row.winner) 'Invalid/missing episode ending.'
        Check ($count -eq $verified.verifiedDecisions -and $count -eq $result.verifiedDecisions -and $perPlayer[0] -eq [int]$row.decisions0 -and $perPlayer[1] -eq [int]$row.decisions1) 'Dataset decision counts disagree with reports/CSV.'
        Check ($terminal.Count -eq 2 -and @($terminal.player | Sort-Object -Unique).Count -eq 2) 'Expected one final transition for each player.'
        foreach ($last in $terminal) {
            $expected=if($ending.winner -lt 0){0}elseif($ending.winner -eq $last.player){1}else{-1}
            Check ($last.reward -eq $expected) 'Terminal reward disagrees with the winner.'
        }
        $metrics.datasetBytes+=(Get-Item -LiteralPath $path).Length; $metrics.episodes++
        $episodes.Add([ordered]@{matchId=$header.matchId;seed=[long]$row.seed;deck0=[int]$row.deck0;deck1=[int]$row.deck1;bot0=$row.bot0;bot1=$row.bot1;decisions=$count;dataset=$path;datasetSha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant();sourceTrace=$header.sourceTrace;sourceTraceSha256=(Get-FileHash -LiteralPath $header.sourceTrace -Algorithm SHA256).Hash.ToLowerInvariant()})
    }
    Check ($metrics.episodes -eq 12 -and $metrics.normalDecisions+$metrics.easyDecisions -eq $metrics.transitions) 'Aggregate episode/teacher counts are inconsistent.'
    $metrics.status='passed'; $metrics.knownPublicCardIds=@($cards | Sort-Object); $metrics.selectedActionCountsByBotAndEnumValue=$actionCounts
    $manifest=[ordered]@{schema=1;usage='pipeline smoke data; not held-out evaluation';rulesVersion=4;catalogVersion=4;aiSchema=2;sourceFingerprint=$metadata.sourceFingerprint;catalogFingerprint=$metadata.catalogFingerprint;teacherSelection='Filter each transition player using bot0/bot1 below; do not label Easy decisions as Normal.';splitPolicy='Keep whole matches and paired seeds together. Pilot is not a held-out test set.';coverageCaution='Observed public card IDs do not prove every card/effect was played or every interaction tested.';episodes=@($episodes.ToArray())}
    [IO.File]::WriteAllText((Join-Path $output 'dataset-manifest.json'),($manifest | ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
} catch { $metrics.errors=@($_.Exception.Message) }
finally { [IO.File]::WriteAllText($reportPath,($metrics | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false)) }
Write-Output ('Dataset validation {0}: episodes={1}; transitions={2}; Normal={3}; Easy={4}; checks={5}' -f $metrics.status,$metrics.episodes,$metrics.transitions,$metrics.normalDecisions,$metrics.easyDecisions,$metrics.checks)
Write-Output ('Report: '+$reportPath)
if ($metrics.status -ne 'passed') { throw ($metrics.errors -join '; ') }
