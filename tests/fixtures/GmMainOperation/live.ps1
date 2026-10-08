param([string]$RepoRoot,[string]$SessionPath,[string]$Scenario)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
$GameSessionPath=$SessionPath
$ControlDir=Join-Path $SessionPath 'game_state/control'
$target=Join-Path $ControlDir 'f2-live.txt'
$failed=$null;$value=$null;$script:refused=$false
if($Scenario -match 'timeout|stall') {
 $tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'),[ref]$tokens,[ref]$errors)
 foreach($f in $ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)) {if($f.Name -in @('Stop-GmBridgeAfterTurnTimeout','Write-DaemonJsonFileBestEffort')){. ([scriptblock]::Create($f.Extent.Text))}}
 function Write-Log {param($Message,$Level,$Color)}
 $TimeoutBridgeCleanupFile=Join-Path $ControlDir 'f2-timeout.json'
 $BridgeControlScript=Join-Path $ControlDir 'f2-inert-shutdown.ps1'
 $launcher=Join-Path $RepoRoot 'BookOfEternityClient/Launcher/bookofeternity.ps1'
 $ast=[Management.Automation.Language.Parser]::ParseFile($launcher,[ref]$tokens,[ref]$errors)
 $functions=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)|ForEach-Object{$_.Extent.Text}) -join "`n"
 $shim='param($Action,[string]$SessionPath)' + "`n" + $functions + "`n" + @'
function Wait-TrackedProcessesExit {param($ProcessIds,$TimeoutMilliseconds) throw 'PID wait forbidden in controlled main shutdown.'}
function Stop-SessionLocalBridgeProcesses {param($Status) throw 'PID signals forbidden in controlled main shutdown.'}
Invoke-BridgeShutdown $SessionPath | ConvertTo-Json -Depth 8 -Compress
'@
 [IO.File]::WriteAllText($BridgeControlScript,$shim)
}
try {
 $value=Invoke-GmParticipatingConsumer $SessionPath {
  Write-GmCanonicalText $SessionPath $target 'one 🌌'
  if($Scenario -like '*caught-loss') {
   $global:BoeMainOperationContext.process.StandardInput.Dispose()
   try {Write-GmCanonicalText $SessionPath $target 'must refuse'}catch {$script:refused=$true}
   return 42
  }
  if($Scenario -like '*stopping') {
   [Console]::Out.WriteLine('original-active');[Console]::Out.Flush();[void][Console]::ReadLine()
   try {Write-GmCanonicalText $SessionPath $target 'must refuse'}catch {$script:refused=$true}
   return 42
  }
  Write-GmCanonicalText $SessionPath $target 'two 🌌'
  $reason=if($Scenario -like '*stall'){'gm_validation_repair_artifact_stall'}else{'gm_turn_timeout'}
  $result=Stop-GmBridgeAfterTurnTimeout -TurnRequest ([pscustomobject]@{sessionId='controlled';requestId='original';turnNumber=1}) -ElapsedSeconds 1 -Reason $reason
  if(-not $global:BoeMainOperationContext.disposed){throw 'Stop waited before original helper transport disposal.'}
  if(Test-Path $TimeoutBridgeCleanupFile){throw 'Post-close cleanup published canonical bytes.'}
  if(-not $script:LastUnpublishedDaemonDiagnostic -or -not $script:EstablishedTimeoutDiagnostic){throw 'Established diagnostics missing.'}
  if(-not $result.ok){throw 'Original scoped shutdown was not acknowledged.'}
  return $result
 }
} catch {$failed=$_.Exception}
if($Scenario -like '*caught-loss') {
 if(-not $script:refused -or $failed.Data['EstablishedOperationResult'] -ne 42){throw 'Caught pre-receipt loss returned ordinary success or lost established value.'}
} elseif($Scenario -like '*reply-loss') {
 if(-not $failed.Data['EstablishedOperationResult'].ok){throw 'Lost close reply discarded established cleanup result.'}
} elseif($failed){throw $failed}
if($Scenario -like '*stopping' -and (-not $script:refused -or [IO.File]::ReadAllText($target) -ne ('one 🌌'+[Environment]::NewLine))){throw 'Stopping admitted a late canonical write.'}
[ordered]@{success=$true;refused=$script:refused;value=$value;continuationFailed=[bool]$failed}|ConvertTo-Json -Depth 4 -Compress
