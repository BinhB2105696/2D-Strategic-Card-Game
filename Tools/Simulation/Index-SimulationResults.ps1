param()
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root = Join-Path $projectRoot 'SimulationResults'
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'SimulationResults does not exist.' }
# Only create the reserved groups and index files. Never move, rewrite or delete source evidence.
foreach ($name in @('Runs','Datasets','Models','Runners')) {
    New-Item -ItemType Directory -Path (Join-Path $root $name) -Force | Out-Null
}
function Read-JsonIfPresent([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) { return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json) }
    return $null
}
$entries = [Collections.Generic.List[object]]::new()
foreach ($folder in Get-ChildItem -LiteralPath $root -Directory | Sort-Object Name) {
    $files = @(Get-ChildItem -LiteralPath $folder.FullName -Recurse -File)
    $run = Read-JsonIfPresent (Join-Path $folder.FullName 'run.json')
    $summary = Read-JsonIfPresent (Join-Path $folder.FullName 'summary.json')
    $kind = 'Unclassified'
    $note = 'Review metadata before use.'
    if ($null -ne $run) {
        $kind = if ($run.schema -lt 2) {'HistoricalMatches'} else {'HistoricalReplay'}
        $note = 'Historical rules/catalog; not part of the new expansion training set.'
        if ($null -eq $summary) { $note += ' No final summary; completion not established.' }
    } elseif ($folder.Name -like 'verification_*') {
        $kind = 'VerificationFixtures'; $note = 'Test fixtures; not training episodes.'
    } elseif ($folder.Name -eq 'Experiments') {
        $kind = 'CuratedExperiments'; $note = 'Keep packaged baselines, manifests and checksums intact; source copies are intentional.'
    } elseif ($folder.Name -eq 'Runs') {
        $kind = 'VersionedRuns'; $note = 'New runs: rules-vN/catalog-vN/ai-vN/<run-id>.'
    } elseif ($folder.Name -eq 'Runners') {
        $kind = 'PreservedRunners'; $note = 'Complete archived Players and hash manifests; replay compatibility still requires verification.'
    } elseif ($folder.Name -eq 'Datasets') {
        $kind = 'DatasetExports'; $note = 'Reserved for verified, versioned datasets; never treat all historical traces as training data.'
    } elseif ($folder.Name -eq 'Models') {
        $kind = 'ModelCheckpoints'; $note = 'Reserved for model, preprocessing/vocabulary, configuration and training provenance.'
    }
    $reports = @($files | Where-Object { $_.Name -eq 'report.json' -and $_.FullName -match '[\\/]replay[\\/]' })
    $verified = 0; $prefixes = 0
    foreach ($reportFile in $reports) {
        $report = Read-JsonIfPresent $reportFile.FullName
        if ($report.verified -and $report.status -eq 'verified-match') { $verified++ }
        if ($report.verified -and $report.status -eq 'verified-prefix') { $prefixes++ }
    }
    $entries.Add([pscustomobject][ordered]@{
        path=$folder.Name; classification=$kind; bytes=[long](($files | Measure-Object Length -Sum).Sum)
        files=$files.Count; runSchema=$run.schema; rulesVersion=$run.rulesVersion; catalogVersion=$run.catalogVersion
        aiSchema=$run.aiContract.schema; matches=$summary.matches; sourceFingerprint=$run.sourceFingerprint
        catalogFingerprint=$run.catalogFingerprint; verifiedMatchReports=$verified; verifiedPrefixReports=$prefixes
        note=$note
    })
}
$generated = [DateTime]::UtcNow.ToString('o')
$totalBytes = [long](($entries | Measure-Object bytes -Sum).Sum)
$inventory = [ordered]@{schema=1;generatedUtc=$generated;operation='indexed; source files and locations retained';totalBytes=$totalBytes;entries=@($entries.ToArray())}
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# Simulation results index')
$lines.Add('')
$lines.Add("Generated UTC: $generated. Data size: $([math]::Round($totalBytes/1GB,3)) GiB.")
$lines.Add('')
$lines.Add('Historical folders retain their original names/paths so published references and evidence remain usable. Nothing was moved or deleted. Size includes intentional copies inside curated experiments.')
$lines.Add('')
$lines.Add('## Storage groups')
$lines.Add('')
$lines.Add('- `Runs/rules-vN/catalog-vN/ai-vN/<run-id>/`: future simulation batches (new runner required).')
$lines.Add('- `Datasets/`: future verified exports with versioned manifests and fixed train/validation/test splits.')
$lines.Add('- `Models/`: future checkpoints with training and preprocessing provenance.')
$lines.Add('- `Runners/`: the previous full Windows Player is copied and hash-verified before an Editor rebuild overwrites it.')
$lines.Add('- Existing dated runs, `verification_*` and `Experiments/`: historical evidence. Rules/catalog v2 is not the expansion v4 dataset.')
$lines.Add('')
$lines.Add('## Inventory')
$lines.Add('')
$lines.Add('| Folder | Group | MiB | Files | Run schema | Rules/catalog | Matches | Verified reports: full/prefix |')
$lines.Add('| --- | --- | ---: | ---: | --- | --- | ---: | --- |')
foreach ($entry in $entries) {
    $lines.Add(('| `{0}` | {1} | {2:F2} | {3} | {4} | {5}/{6} | {7} | {8}/{9} |' -f
        $entry.path,$entry.classification,($entry.bytes/1MB),$entry.files,$entry.runSchema,
        $entry.rulesVersion,$entry.catalogVersion,$entry.matches,$entry.verifiedMatchReports,$entry.verifiedPrefixReports))
}
$lines.Add('')
$lines.Add('Report counts are historical reports, not a new verification, unique episode count or confirmation that the latest runner can replay them. Schema-1 traces cannot be fully verified by the new replay contract. Keep truncated prefixes separate from completed-match training data.')
$lines.Add('')
$lines.Add('Refresh this index: `powershell -NoProfile -File .\Tools\Simulation\Index-SimulationResults.ps1` from the project directory. Detailed machine-readable metadata is in `inventory.json`.')
$encoding = [Text.UTF8Encoding]::new($false)
foreach ($output in @(@{name='inventory.json';text=($inventory | ConvertTo-Json -Depth 10)},@{name='INDEX.md';text=($lines -join "`n")})) {
    $path = Join-Path $root $output.name
    $pending = $path + '.tmp'
    [IO.File]::WriteAllText($pending,$output.text,$encoding)
    if (Test-Path -LiteralPath $path) { [IO.File]::Replace($pending,$path,$null) } else { [IO.File]::Move($pending,$path) }
}
Write-Output ('Indexed {0} groups; {1:F3} GiB; source evidence unchanged.' -f $entries.Count,($totalBytes/1GB))
