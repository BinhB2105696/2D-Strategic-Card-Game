$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Run-Simulation.ps1') `
    -Config (Join-Path $PSScriptRoot 'ai-v2-hard-teacher-pilot.json') `
    -HeartbeatTimeoutSeconds 180 `
    -MaxBatchSeconds 3600 `
    -MaxWorkingSetMiB 1536 `
    -MaxResultsMiB 1024
if ($LASTEXITCODE -ne 0) { throw 'Hard teacher pilot failed.' }
