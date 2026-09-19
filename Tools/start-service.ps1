param([int]$Port=17861)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$artifactDir=Join-Path $root 'Artifacts'
New-Item -ItemType Directory -Force $artifactDir | Out-Null
$python=Join-Path $root '.venv\Scripts\python.exe'
$script=Join-Path $root 'Tools\motionbricks_reference\service.py'
$ownerFile=Join-Path $artifactDir 'service-owner.json'
if(Test-Path $ownerFile) {
    $owner=Get-Content $ownerFile -Raw | ConvertFrom-Json
    $existing=Get-Process -Id $owner.pid -ErrorAction SilentlyContinue
    if($existing -and $owner.startTicks -and $existing.StartTime.ToUniversalTime().Ticks -eq $owner.startTicks -and $existing.Path -eq $owner.executable) { Write-Output "Owned service already running: $($owner.pid)"; return }
}
$serviceArguments=@(('"'+$script+'"'),'--port',[string]$Port)
$process=Start-Process -FilePath $python -ArgumentList $serviceArguments -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifactDir 'service.log') -RedirectStandardError (Join-Path $artifactDir 'service-error.log')
$deadline=[DateTime]::UtcNow.AddSeconds(15)
while([DateTime]::UtcNow -lt $deadline) {
    $process.Refresh()
    if($process.HasExited){throw "Provider failed to start; see Artifacts/service-error.log"}
    if((Get-Content (Join-Path $artifactDir 'service.log') -Raw -ErrorAction SilentlyContinue) -match '"ready": true'){break}
    Start-Sleep -Milliseconds 100
}
if([DateTime]::UtcNow -ge $deadline){Stop-Process -InputObject $process;throw 'Owned provider readiness timeout'}
@{pid=$process.Id;startTicks=$process.StartTime.ToUniversalTime().Ticks;executable=(Resolve-Path $python).Path} | ConvertTo-Json | Set-Content $ownerFile
Write-Output "Provider ready on loopback port $Port (PID $($process.Id))."
