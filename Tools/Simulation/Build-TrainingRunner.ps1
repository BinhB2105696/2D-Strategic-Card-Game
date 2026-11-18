param(
    [string]$UnityPath = ''
)

$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([IO.Path]::GetPathRoot($project) -ne 'D:\') { throw 'The project and build output must remain on drive D.' }
if (!$UnityPath) {
    $hubPath = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    $installRoot = if (Test-Path -LiteralPath $hubPath) { Get-Content -LiteralPath $hubPath -Raw | ConvertFrom-Json } else { $null }
    $candidates = @(
        $(if ($installRoot) { Join-Path $installRoot '2022.3.62f3\Editor\Unity.exe' }),
        'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }
    $UnityPath = $candidates | Select-Object -First 1
}
if (!(Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity 2022.3.62f3 was not found at $UnityPath" }
$log = Join-Path $project ('SimulationResults/TrainingBuildLogs/build_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.log')
New-Item -ItemType Directory -Path (Split-Path $log) -Force | Out-Null
$arguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+$project+'"'),
    '-executeMethod','SimulationBuildTool.PrepareAndBuildRunnerFromCommandLine','-logFile',('"'+$log+'"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $project -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Unity build failed with exit code $($process.ExitCode). Log: $log" }
Write-Output "Training runner built. Log: $log"
