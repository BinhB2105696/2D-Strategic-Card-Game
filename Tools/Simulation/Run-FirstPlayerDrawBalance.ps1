param([switch]$Confirm)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$build = Get-Content (Join-Path $project 'Build/Simulation/build-info.json') -Raw | ConvertFrom-Json
if ($build.result -notlike 'Succeeded*' -or $build.rulesVersion -ne 5 -or $build.catalogVersion -ne 4 -or $build.aiSchema -ne 2) {
    throw 'Build a verified rules-v5/catalog-v4/ai-v2 runner first.'
}
$configName = if ($Confirm) { 'ai-v5-first-draw-balance-confirm.json' } else { 'ai-v5-first-draw-balance.json' }
$seedLabel = if ($Confirm) { '510111-510120' } else { '510101-510110' }
Write-Output "Frozen balance comparison: rules v5, seeds $seedLabel, five decks, both seats, 100 matches."
& (Join-Path $PSScriptRoot 'Run-Simulation.ps1') `
    -Config (Join-Path $PSScriptRoot $configName) `
    -HeartbeatTimeoutSeconds 180 `
    -MaxBatchSeconds 10800 `
    -MaxWorkingSetMiB 1536 `
    -MaxResultsMiB 2048
if ($LASTEXITCODE -ne 0) { throw 'Rules-v5 first-player balance comparison failed.' }
