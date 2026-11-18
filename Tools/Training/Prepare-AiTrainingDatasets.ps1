param(
    [string]$CollectionRoot = '',
    [string]$OutputRoot = ''
)
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$CollectionRoot){$CollectionRoot=Join-Path $project 'SimulationResults/TrainingCollections/hard_normal_main_v1_32b700a'}
if(!$OutputRoot){$OutputRoot=Join-Path $project 'SimulationResults/Datasets/hard_normal_main_v1'}
$plan=Get-Content (Join-Path $CollectionRoot 'plan.json') -Raw|ConvertFrom-Json
$manifests=@()
foreach($shard in $plan.shards){$status=Get-Content (Join-Path $CollectionRoot ('shards/'+$shard.id+'/status.json')) -Raw|ConvertFrom-Json;$manifest=Join-Path $status.validation 'dataset-manifest.json';if(!(Test-Path $manifest)){throw "Missing validated manifest: $($shard.id)"};$manifests+=$manifest}
$python=Join-Path $project 'SimulationResults/TrainingEnvironment/python312_cpu/Scripts/python.exe'
function Prepare-Dataset($name,[string[]]$teachers){
    $output=Join-Path $OutputRoot $name;if(Test-Path $output){throw "Dataset output already exists: $output"}
    $args=@('Tools/Training/prepare_imitation_data.py');foreach($manifest in $manifests){$args+=@('--episode-manifest',$manifest)}
    foreach($seed in $plan.splitSeeds.validation){$args+=@('--validation-seed',[string]$seed)};foreach($seed in $plan.splitSeeds.test){$args+=@('--test-seed',[string]$seed)};foreach($teacher in $teachers){$args+=@('--teacher',$teacher)};$args+=@('--output',$output)
    & $python @args;if($LASTEXITCODE -ne 0){throw "Preparation failed: $name"}
}
Prepare-Dataset 'mixed_hard_normal' @('Hard','Normal')
Prepare-Dataset 'hard_only' @('Hard')
Prepare-Dataset 'normal_only' @('Normal')
Write-Output "Prepared three teacher ablations under $OutputRoot"
