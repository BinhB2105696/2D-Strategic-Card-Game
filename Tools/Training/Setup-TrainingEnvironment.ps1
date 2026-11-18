$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$environment = Join-Path $repoRoot 'SimulationResults\TrainingEnvironment\python312_cpu'
$python = Join-Path $environment 'Scripts\python.exe'

if (-not (Test-Path $python)) {
    py -3.12 -m venv $environment
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the Python 3.12 environment.' }
}

& $python -m pip install -r (Join-Path $PSScriptRoot 'requirements-cpu.txt')
if ($LASTEXITCODE -ne 0) { throw 'PyTorch installation failed.' }

& $python -c "import torch; print('torch=' + torch.__version__); print('cuda=' + str(torch.cuda.is_available()))"
if ($LASTEXITCODE -ne 0) { throw 'PyTorch verification failed.' }
