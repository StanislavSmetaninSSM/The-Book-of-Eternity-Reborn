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
  if($Scenario -like '*snapshot-stopping') {
   $script:physicalHelperPid=$global:BoeMainOperationContext.process.Id
   $script:physicalWriteStart=[Diagnostics.Stopwatch]::GetTimestamp()
  }
  Write-GmCanonicalText $SessionPath $target 'one 🌌'
  if($Scenario -like '*snapshot-stopping') {$script:physicalWriteEnd=[Diagnostics.Stopwatch]::GetTimestamp()}
  if($Scenario -like '*caught-loss') {
   $global:BoeMainOperationContext.process.StandardInput.Dispose()
   try {Write-GmCanonicalText $SessionPath $target 'must refuse'}catch {$script:refused=$true}
   return 42
  }
  if($Scenario -like '*stopping') {
   [Console]::Out.WriteLine('original-active');[Console]::Out.Flush();[void][Console]::ReadLine()
   if($Scenario -like '*snapshot-stopping') {
    $script:snapshotContext=$global:BoeMainOperationContext
    $script:beforeSequence=$script:snapshotContext.sequence
    try {[void](Get-GmDaemonSnapshot -Paths @('game_state/control/f2-live.txt'))}
    catch {
     $script:refused=Test-GmDaemonReadFailure $_
     $script:endSequence=$script:snapshotContext.sequence
     $script:endAction=$script:snapshotContext.lastAction
     $script:endReply=$script:snapshotContext.lastCommandReply
     throw
    }
    throw 'Snapshot unexpectedly admitted after original Stop'
   }
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
if($Scenario -like '*snapshot-stopping') {
 $ctx=$script:snapshotContext
 if(-not $failed -or -not (Test-GmDaemonReadFailure $failed) -or -not $script:refused){throw 'Actual snapshot refusal missing'}
 if($script:endSequence -ne $script:beforeSequence+3 -or $script:endAction -cne 'daemon-request-end' -or $script:endReply.daemonReadRefused -ne $true){throw 'Expected successful begin/chunk then actual end refusal'}
 if(-not $ctx.closeObserved -or $ctx.lost -or -not $ctx.disposed -or $ctx.exitCode -ne 0 -or $ctx.terminalClose.outcome -ne 1 -or $ctx.terminalClose.closingFailed){throw 'Original Failed close not settled'}
 foreach($key in @('pinId','closeId','operationId')){if($ctx.originalClose.$key -cne $ctx.terminalClose.$key){throw 'Original close tuple changed'}}
 if($ctx.replyReadJoined -cne $true -or $ctx.stderrReadJoined -cne $true){throw 'Actual helper reply/stderr tasks did not join'}
 $nonce=$env:BOE_MAIN_STOP_DIAGNOSTIC_NONCE;$parsedNonce=[guid]::Empty
 if(-not [guid]::TryParseExact($nonce,'N',[ref]$parsedNonce)){throw 'Controlled physical nonce unavailable'}
 $prefix='BOE_CANONICAL_LEASE_OBSERVATION '
 $physical=@($ctx.diagnostic -split "`n" | Where-Object {$_.StartsWith($prefix,[StringComparison]::Ordinal)} | ForEach-Object {$_.Substring($prefix.Length)|ConvertFrom-Json -ErrorAction Stop})
 if($physical.Count -lt 3){throw 'Actual remote physical lifecycle absent'}
 $expectedRoot=[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($SessionPath).TrimEnd([IO.Path]::DirectorySeparatorChar))
 foreach($row in $physical) {
  if($row.Nonce -cne $nonce -or $row.ProcessId -ne $script:physicalHelperPid -or $row.Root -cne $expectedRoot){throw 'Physical row nonce/process/root mismatch'}
  foreach($field in @('ProcessId','Occurrence','Attempt','CaptureMonotonicTimestamp')) {
   if(($row.$field -isnot [long] -and $row.$field -isnot [int]) -or $row.$field -le 0){throw "Invalid physical integer $field"}
  }
  if($row.Stage -cnotin @('physical-opened','lease-granted','physical-dispose-completed')){throw 'Unexpected physical stage'}
  if($row.Purpose -cnotin @('SessionMutation','SessionFinalization')){throw 'Unexpected physical purpose'}
  [void][datetimeoffset]::Parse([string]$row.CapturedAtUtc,[Globalization.CultureInfo]::InvariantCulture)
  $pin=$row.OriginalPin
  if($null -eq $pin -or $pin.Ok -isnot [bool] -or -not $pin.Ok -or ($pin.State -isnot [long] -and $pin.State -isnot [int]) -or $pin.State -ne 1 -or $null -ne $pin.Error){throw 'Physical original grant is not actual Active'}
  foreach($key in @('pinId','closeId','operationId')){if($pin.$key -isnot [string] -or $pin.$key -cne $ctx.originalClose.$key){throw 'Physical original tuple mismatch'}}
  foreach($key in @('rootKey','runId','generationId','hostInstanceId','bootId')) {
   if($pin.Identity.$key -isnot [string] -or $pin.Identity.$key -cne $ctx.originalClose.identity.$key){throw 'Physical original identity mismatch'}
  }
  foreach($key in @('epoch','backend')) {
   if(($pin.Identity.$key -isnot [long] -and $pin.Identity.$key -isnot [int]) -or $pin.Identity.$key -ne $ctx.originalClose.identity.$key){throw 'Physical original typed identity mismatch'}
  }
 }
 $writeCycles=0
 foreach($group in @($physical|Group-Object Occurrence)) {
  $events=@($group.Group)
  if($events.Count -ne 3 -or $events[0].Stage -cne 'physical-opened' -or $events[1].Stage -cne 'lease-granted' -or $events[2].Stage -cne 'physical-dispose-completed'){throw 'Physical occurrence missing/duplicate/unordered stage'}
  foreach($event in $events){if($event.Purpose -cne $events[0].Purpose -or $event.Attempt -ne $events[0].Attempt){throw 'Physical occurrence changed purpose/attempt'}}
  if($events[0].CaptureMonotonicTimestamp -gt $events[1].CaptureMonotonicTimestamp -or $events[1].CaptureMonotonicTimestamp -gt $events[2].CaptureMonotonicTimestamp){throw 'Physical transition timestamps reversed'}
  if($events[0].Purpose -ceq 'SessionMutation' -and $events[0].CaptureMonotonicTimestamp -ge $script:physicalWriteStart -and $events[2].CaptureMonotonicTimestamp -le $script:physicalWriteEnd){$writeCycles++}
 }
 if($writeCycles -lt 1){throw 'No actual pre-Stop canonical-write physical cycle'}
 [ordered]@{success=$true;refusedAtEnd=$true;beginChunkAcknowledged=$true;originalClose=$ctx.originalClose;terminalClose=$ctx.terminalClose;closeObserved=$ctx.closeObserved;lost=$ctx.lost;helperExitCode=$ctx.exitCode;replyReadJoined=$ctx.replyReadJoined;stderrReadJoined=$ctx.stderrReadJoined;physicalHelperPid=$script:physicalHelperPid;physicalWriteStart=$script:physicalWriteStart;physicalWriteEnd=$script:physicalWriteEnd;physicalWriteCycles=$writeCycles;physicalRows=$physical;physicalRaw=$ctx.diagnostic}|ConvertTo-Json -Depth 12 -Compress
 return
}
if($Scenario -like '*caught-loss') {
 if(-not $script:refused -or $failed.Data['EstablishedOperationResult'] -ne 42){throw 'Caught pre-receipt loss returned ordinary success or lost established value.'}
} elseif($Scenario -like '*reply-loss') {
 if(-not $failed.Data['EstablishedOperationResult'].ok){throw 'Lost close reply discarded established cleanup result.'}
} elseif($failed){throw $failed}
if($Scenario -like '*stopping' -and (-not $script:refused -or [IO.File]::ReadAllText($target) -ne ('one 🌌'+[Environment]::NewLine))){throw 'Stopping admitted a late canonical write.'}
[ordered]@{success=$true;refused=$script:refused;value=$value;continuationFailed=[bool]$failed}|ConvertTo-Json -Depth 4 -Compress
