param([switch]$Backbone)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$env:UV_CACHE_DIR=Join-Path $root '.cache\uv'
$env:UV_PYTHON_INSTALL_DIR=Join-Path $root '.python'
$env:MPLCONFIGDIR=Join-Path $root '.cache\matplotlib'
uv python install 3.11.15 --no-bin
if($LASTEXITCODE -ne 0){throw 'Python installation failed'}
uv venv --python 3.11.15 .venv
if($LASTEXITCODE -ne 0){throw 'Virtual environment creation failed'}
$lock=if($Backbone){'requirements-lock.txt'}else{'requirements-reference-lock.txt'}
uv pip sync --python .venv/Scripts/python.exe --index-strategy unsafe-best-match --extra-index-url https://download.pytorch.org/whl/cu128 "Tools/motionbricks_reference/$lock"
if($LASTEXITCODE -ne 0){throw 'Dependency installation failed'}
if($Backbone){.venv/Scripts/python.exe Tools/motionbricks_reference/fetch.py --backbone}else{.venv/Scripts/python.exe Tools/motionbricks_reference/fetch.py}
if($LASTEXITCODE -ne 0){throw 'Artifact verification failed'}
.venv/Scripts/python.exe Tools/motionbricks_reference/reference.py
if($LASTEXITCODE -ne 0){throw 'Reference inference failed'}
