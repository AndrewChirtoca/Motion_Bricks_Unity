$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$ownerFile=Join-Path $root 'Artifacts\service-owner.json'
if(!(Test-Path $ownerFile)) { Write-Output 'No owned service recorded.'; exit 0 }
$owner=Get-Content $ownerFile -Raw | ConvertFrom-Json
$process=Get-Process -Id $owner.pid -ErrorAction SilentlyContinue
if($process) {
    if(!$owner.startTicks -or $process.StartTime.ToUniversalTime().Ticks -ne $owner.startTicks -or $process.Path -ne $owner.executable) { throw 'PID identity changed; refusing to stop unrelated process.' }
    Stop-Process -InputObject $process
}
Remove-Item -LiteralPath $ownerFile
Write-Output 'Owned provider stopped. Run start-service.ps1 to restart; Unity reconnects automatically.'
