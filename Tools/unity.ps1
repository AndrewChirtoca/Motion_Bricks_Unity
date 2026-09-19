param([ValidateSet('environment','setup','build','edit-tests','play-tests','native-cpu','native-gpu','chair-authoring')][string]$Action='build')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$editor='C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe'
$project=Join-Path $root 'Motion_Bricks_Unity'
$log=Join-Path $root "Artifacts\unity-$Action.log"
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
$arguments=@('-batchmode','-projectPath',('"'+$project+'"'),'-logFile',('"'+$log+'"'))
if($Action -ne 'native-gpu') { $arguments+='-nographics' }
switch($Action) {
    'edit-tests' {$arguments+=@('-runTests','-testPlatform','EditMode','-testResults',('"'+(Join-Path $root 'Artifacts\edit-tests.xml')+'"'))}
    'play-tests' {$arguments+=@('-runTests','-testPlatform','PlayMode','-testResults',('"'+(Join-Path $root 'Artifacts\play-tests.xml')+'"'))}
    default {
        $methods=@{'environment'='EnvironmentValidation.Run';'setup'='DemoSetup.Generate';'build'='DemoSetup.Build';'native-cpu'='NativeImportProbe.Run';'native-gpu'='NativeImportProbe.Run';'chair-authoring'='SmartObjectAuthoringSetup.Generate'}
        $arguments+=@('-quit','-executeMethod',('MotionBricksUnity.Editor.'+$methods[$Action]))
        if($Action -eq 'native-gpu'){$arguments+='-nativeGpu'}
    }
}
$process=Start-Process $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
# Wait for the owned editor only; -Wait also waits for persistent licensing descendants.
$process.WaitForExit()
if($process.ExitCode -ne 0){throw "Unity exited $($process.ExitCode); see $log"}
Write-Output "Unity $Action completed. See $log and Artifacts results."
