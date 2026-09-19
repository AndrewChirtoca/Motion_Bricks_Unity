param([int]$Seconds=600,[switch]$InjectFailure,[string]$Name='nominal')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if($Seconds -lt 20 -or $Seconds -gt 3600){throw 'Smoke duration must be between 20 and 3600 seconds'}
if($Name -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Use a simple output name'}
$outputFolder=Join-Path $root "Artifacts\smoke-$Name"
New-Item -ItemType Directory -Force $outputFolder | Out-Null
& (Join-Path $PSScriptRoot 'start-service.ps1')
$exe=Join-Path $root 'Artifacts\Windows\MotionBricks.exe'
$arguments=@('-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-motionSmokeSeconds',[string]$Seconds,'-motionOutputFolder',('"'+$outputFolder+'"'),'-logFile',('"'+(Join-Path $outputFolder 'player.log')+'"'))
$player=Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
@{pid=$player.Id;startTime=$player.StartTime.ToUniversalTime().ToString('o');seconds=$Seconds;injectedFailure=[bool]$InjectFailure} | ConvertTo-Json | Set-Content (Join-Path $outputFolder 'run.json')
$watch=[Diagnostics.Stopwatch]::StartNew();$stopped=$false;$restarted=$false;$nextSample=0
while(!$player.HasExited) {
    $elapsed=$watch.Elapsed.TotalSeconds
    if($InjectFailure -and !$stopped -and $elapsed -ge 8) {& (Join-Path $PSScriptRoot 'stop-service.ps1');$stopped=$true}
    if($InjectFailure -and !$restarted -and $elapsed -ge 14) {& (Join-Path $PSScriptRoot 'start-service.ps1');$restarted=$true}
    if($elapsed -ge $nextSample) {
        $player.Refresh();$service=$null
        $ownerPath=Join-Path $root 'Artifacts\service-owner.json'
        if(Test-Path $ownerPath){
            $ready=Get-Content (Join-Path $root 'Artifacts\service.log') -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json
            if($ready.processId){$service=Get-Process -Id $ready.processId -ErrorAction SilentlyContinue}
        }
        [pscustomobject]@{seconds=[math]::Round($elapsed,2);playerWorkingSetMiB=$player.WorkingSet64/1MB;playerThreads=$player.Threads.Count;serviceWorkingSetMiB=if($service){$service.WorkingSet64/1MB}else{0};serviceThreads=if($service){$service.Threads.Count}else{0}} | Export-Csv (Join-Path $outputFolder 'process-samples.csv') -NoTypeInformation -Append
        $nextSample=$elapsed+5
    }
    if($elapsed -gt $Seconds+45) {Stop-Process -InputObject $player;throw 'Owned player exceeded smoke-test deadline'}
    Start-Sleep -Milliseconds 500
}
$player.WaitForExit()
if($player.ExitCode -ne 0){throw "Player exited $($player.ExitCode)"}
Get-Content (Join-Path $outputFolder 'motionbricks-smoke.txt')
