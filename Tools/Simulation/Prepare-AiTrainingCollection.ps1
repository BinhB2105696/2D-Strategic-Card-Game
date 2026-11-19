param([string]$OutputRoot = '')
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$build = Get-Content (Join-Path $project 'Build/Simulation/build-info.json') -Raw | ConvertFrom-Json
$revision = (git -C $project rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $build.sourceRevision -ne $revision -or $build.result -notlike 'Succeeded*') { throw 'Build a successful runner from the current commit first.' }
if (!$OutputRoot) { $OutputRoot = Join-Path $project ('SimulationResults/TrainingCollections/hard_normal_main_v2_' + $revision.Substring(0,7)) }
$root = [IO.Path]::GetFullPath($OutputRoot)
$prefix = (Join-Path $project 'SimulationResults').TrimEnd('\') + '\'
if (!$root.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Collection root must be inside SimulationResults.' }
$planPath = Join-Path $root 'plan.json'
if (Test-Path -LiteralPath $planPath) { Write-Output "Existing frozen plan: $planPath"; exit 0 }
New-Item -ItemType Directory -Path (Join-Path $root 'configs') -Force | Out-Null
$ranges = @(
    [ordered]@{split='train';first=610101;last=610120},
    [ordered]@{split='validation';first=620001;last=620004},
    [ordered]@{split='test';first=630001;last=630004}
)
$shards = [Collections.Generic.List[object]]::new()
foreach ($range in $ranges) {
    for ($first=$range.first; $first -le $range.last; $first+=2) {
        $count=[Math]::Min(2,$range.last-$first+1); $id='{0}_{1}_{2}' -f $range.split,$first,($first+$count-1)
        $configPath=Join-Path $root ('configs/'+$id+'.json')
        $matchups=@(0,1,2,3,4 | ForEach-Object { [ordered]@{player0Bot='Hard';player1Bot='Normal';deck0=$_;deck1=$_;swapBots=$true} })
        $config=[ordered]@{schemaVersion=1;projectRoot=$project;seedCount=$count;firstSeed=$first;warmupMatches=2;maxTurns=200;
            maxActions=10000;maxMatchSeconds=180;decisionWarningMs=1000;detailedMatches=$count*10;matchups=$matchups}
        [IO.File]::WriteAllText($configPath,($config | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
        $shards.Add([ordered]@{id=$id;split=$range.split;firstSeed=$first;seedCount=$count;matches=$count*10;config=$configPath})
    }
}
$plannedMatches=($shards | ForEach-Object {[int]$_['matches']} | Measure-Object -Sum).Sum
$plan=[ordered]@{schema=1;status='prepared';name='hard_normal_main_v2';sourceRevision=$revision;sourceFingerprint=$build.sourceFingerprint;
    rulesVersion=$build.rulesVersion;catalogVersion=$build.catalogVersion;aiSchema=$build.aiSchema;decks=@(0,1,2,3,4);
    policy='Hard vs Normal, equal deck, both seats; sequential shards';plannedMatches=$plannedMatches;
    splitSeeds=[ordered]@{train=@(610101..610120);validation=@(620001..620004);test=@(630001..630004)};shards=@($shards)}
[IO.File]::WriteAllText($planPath,($plan | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
Write-Output "Prepared $($shards.Count) shards / $($plan.plannedMatches) matches: $planPath"
