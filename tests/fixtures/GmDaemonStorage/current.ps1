# Invoked inside the same actually admitted operation as the original daemon.
$script:CurrentPassed=$false;$script:CurrentEvidence=[ordered]@{mode=$Mode}
$TurnRequestFile=[IO.Path]::Combine($SessionPath,'input/turn_request.json')
$PendingTurnSnapshotManifestFile=[IO.Path]::Combine($ControlDir,'pending_turn_snapshot.json')
$PendingTurnSnapshotAuthorityFile=[IO.Path]::Combine($ControlDir,'pending_turn_snapshot.authority.json')
$ObservedTerminalRequestKeysFile=[IO.Path]::Combine($ControlDir,'gm_observed_terminal_requests.json')
$script:ObservedTerminalRequestKeys=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$script:LastRepairRequestWrite=[datetime]::MinValue;$script:LastTerminalProtocolFailureWrite=[datetime]::MinValue
$request=[pscustomobject]@{sessionId='session';requestId='request';turnNumber=1}
function Assert-Current([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message}}
try {
 switch($Mode){
 'large-batch' {
  $paths=@('output/daemon-large.json','output/daemon-A.json','output/daemon-a.json','output/daemon-literal\leaf.json','output/daemon-literal/leaf.json')
  $batch=Get-GmDaemonSnapshot -Paths $paths
  Invoke-GmDaemonSnapshotView $batch {
   Assert-Current ((Get-GmDaemonText $paths[0]|ConvertFrom-Json).value.Length -eq 80000) 'Large response was truncated.'
   foreach($pair in @(@($paths[1],'A'),@($paths[2],'a'),@($paths[3],'literal'),@($paths[4],'slash'))){Assert-Current ((Get-GmDaemonText $pair[0]) -ceq $pair[1]) 'Host path siblings were collapsed.'}
  }
  Assert-Current ($batch.files.Count -eq 5) 'Exact batch membership lost.'
 }
 'malformed-response' {
  $script:OriginalFixtureSend=${function:Send-GmOperationCommand};$script:CorruptHeader=$false;$script:CorruptChunk=$false
  function Send-GmOperationCommand {
   param($Context,[hashtable]$Command)
   $reply=& $script:OriginalFixtureSend $Context $Command
   if($Command.action -ceq 'daemon-request-end'){
    $script:CorruptHeader=$true;$reply.length=4;$reply.hash=Get-GmDaemonBytesHash ([Text.Encoding]::UTF8.GetBytes('null'))
   }elseif($Command.action -ceq 'daemon-response-chunk'){
    $script:CorruptChunk=$true;$reply.bytes=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('null'));$reply.complete=$true
   }
   return $reply
  }
  $ErrorActionPreference='Continue'
  try{$null=Get-GameConfig;throw 'Malformed successful response was accepted.'}
  catch {Assert-Current (Test-GmDaemonReadFailure $_) 'Malformed response became defaults.';Assert-Current $global:BoeMainOperationContext.lost 'Malformed response was not transport loss';Assert-Current ($script:CorruptHeader -and $script:CorruptChunk) 'Response cut not reached';$script:CurrentPassed=$true;throw}
 }
 'request-end-loss' {
  $script:OriginalFixtureRead=${function:Read-GmOperationReply};$script:OriginalFixtureSend=${function:Send-GmOperationCommand};$script:CorruptEnd=$false
  function Read-GmOperationReply {param($Context) if($script:CorruptEnd){throw [IO.EndOfStreamException]::new('Actual end-reply consumer loss.')};& $script:OriginalFixtureRead $Context}
  function Send-GmOperationCommand {param($Context,[hashtable]$Command) if($Command.action -ceq 'daemon-request-end'){$script:CorruptEnd=$true};& $script:OriginalFixtureSend $Context $Command}
  try{$null=Get-GameConfig;throw 'Lost end reply became defaults.'}catch{Assert-Current (Test-GmDaemonReadFailure $_) 'End loss lost its read marker';Assert-Current $global:BoeMainOperationContext.lost 'End loss did not retain transport state';Assert-Current $script:CorruptEnd 'End loss was not reached';$script:CurrentPassed=$true;throw}
 }
 'malformed-request' {
  $ctx=$global:BoeMainOperationContext;$id=[guid]::NewGuid().ToString('N');$bytes=[Text.Encoding]::UTF8.GetBytes('{}')
  [void](Send-GmOperationCommand $ctx @{action='daemon-request-begin';transfer=$id;length=2;hash=(Get-GmDaemonBytesHash $bytes)})
  $rejected=$false;try{[void](Send-GmOperationCommand $ctx @{action='daemon-request-chunk';transfer=$id;offset=1;bytes=[Convert]::ToBase64String($bytes)})}catch{$rejected=$true}
  Assert-Current ($rejected -and -not $ctx.lost) 'Malformed chunk was not a live protocol refusal.'
  Assert-Current ((Get-GmDaemonText 'output/daemon-A.json') -ceq 'A') 'Aborted partial request published or poisoned subsequent read.'
  $operation=New-GmPromptOperation -Message 'inert fallback' -PendingPath $TurnRequestFile
  Assert-Current ($null -ne $operation) 'Fallback control lacks actual original prompt identity.'
  $BridgeControlScript=[IO.Path]::Combine($Folder,'deliberately-absent-launcher.ps1')
  $fallback=& $script:OriginalPromptControl $operation
  Assert-Current ($fallback.disposition -ceq 'unknown-outcome' -and $fallback.reason -ceq 'launcher-or-transport-ambiguous') 'Nonstorage launcher failure lost original fallback.'
 }
 'config-refusal' {Get-GameConfig|Out-Null;throw 'Config refusal became defaults.'}
 'cache-refusal' {
  $script:ObservedTerminalGeneration='old-generation';[void]$script:ObservedTerminalRequestKeys.Add('old-key')
  Load-ObservedTerminalRequestKeys
  Assert-Current (-not $script:ObservedTerminalRequestKeys.Contains('old-key') -and $script:ObservedTerminalRequestKeys.Contains('saved-key')) 'Actual current-generation load inherited old cache or lost durable keys.'
  $script:CurrentEvidence.cacheGeneration=$script:ObservedTerminalGeneration
  [IO.File]::WriteAllText((Join-Path $Folder 'cache-armed'),'armed')
  Load-ObservedTerminalRequestKeys;throw 'Cache refusal became an empty cache.'
 }
 'repair-refusal' {Process-RepairRequest -RepairPath ([IO.Path]::Combine($ControlDir,'validation_repair_request.json'));throw 'Repair refusal was swallowed.'}
 'terminal-refusal' {Process-TerminalProtocolFailureRequest -FailurePath ([IO.Path]::Combine($ControlDir,'terminal_protocol_failure_request.json'));throw 'Terminal refusal was swallowed.'}
 'ready-race' {
  $positive=Get-GmDaemonFileObservation 'ready/daemon-positive.json'
  Assert-Current (Remove-GmDaemonObservedFile $positive) 'Exact original positive conditional deletion refused.'
  Assert-Current (-not (Test-GmDaemonPath 'ready/daemon-positive.json')) 'Committed conditional deletion retained member.'
  $signal=Get-CorrelatedTerminalSignal $request ([IO.Path]::Combine($SessionPath,'ready/turn_complete.json')) ([IO.Path]::Combine($SessionPath,'ready/absent-error.json'));Assert-Current ($null -eq $signal) 'Changed Ready acquired old correlation.'}
 'notes-race' {Update-GmLiveTestNoteRecordLinks -RequestId 'request' -RecordId 'actual-record'}
 'cohort-positive' {Assert-Current (Test-TurnRequestHasPendingSnapshotContext $request) 'Authentic declared cohort refused.'}
 'cohort-change' {
  $keysBefore=Get-GmDaemonFileBytes $ObservedTerminalRequestKeysFile
  Process-Turn -RequestPath $TurnRequestFile
  Assert-Current ($script:Dispatches.Count -eq 0 -and -not $script:IsProcessing -and -not $script:ObservedTerminalRequestKeys.Contains((Get-TurnRequestKey $request))) 'Incoherent sample suppressed or dispatched the request.'
  Assert-Current ((Get-GmDaemonBytesHash (Get-GmDaemonFileBytes $ObservedTerminalRequestKeysFile)) -ceq (Get-GmDaemonBytesHash $keysBefore)) 'Incoherent sample persisted an observed key.'
  Assert-Current (Test-TurnRequestHasPendingSnapshotContext $request) 'Later coherent same-request authority was incorrectly suppressed.'
 }
 'cohort-read-refusal' {
  $initialRefused=$false
  try{Process-Turn -RequestPath $TurnRequestFile}catch{Assert-Current (Test-GmDaemonReadFailure $_) 'Initial turn failure lost its read marker';$initialRefused=$true}
  Assert-Current ($initialRefused -and -not $script:IsProcessing) 'Initial turn refusal retained the processing flag.'
  Process-Turn -RequestPath $TurnRequestFile;throw 'Turn authority read refusal was swallowed.'
 }
 'post-send-refusal' {
  $pending=Read-GmPromptPending $TurnRequestFile
  $operation=New-GmPromptOperation -Message 'inert exact message' -PendingPath $TurnRequestFile -OperationKind 'turn' -OperationRevision 'fixture' -ExpectedSourceHash $pending.Hash
  Assert-Current ($null -ne $operation) 'Actual prompt allocation failed.'
  $script:ExpectedOperationId=($operation.PayloadJson|ConvertFrom-Json).operationId
  try{Dispatch-WithRetry -Message 'inert exact message' -PendingPath $TurnRequestFile -Operation $operation -ReturnDetails|Out-Null;throw 'Post-send refusal was swallowed.'}
  catch{
   Assert-Current (Test-GmDaemonReadFailure $_) 'Post-send failure was not read refusal.'
   Assert-Current ($script:Dispatches.Count -eq 1 -and $script:LastFixtureOperation -eq $operation) 'Original delivery was replayed or replaced.'
   Assert-Current (($operation.PayloadJson|ConvertFrom-Json).operationId -ceq $script:ExpectedOperationId -and $operation.LastDelivery.disposition -ceq 'submission-observed') 'Original actual delivery was relabeled or lost.'
   $script:CurrentEvidence.operationId=($operation.PayloadJson|ConvertFrom-Json).operationId;$script:CurrentEvidence.lastDelivery=$operation.LastDelivery
   $script:CurrentPassed=$true;throw
  }
 }
 default {throw 'Unknown bounded current mode.'}
 }
 $script:CurrentPassed=$true
}catch{
 if($Mode -in @('config-refusal','cache-refusal','repair-refusal','terminal-refusal','cohort-read-refusal')){
  Assert-Current (Test-GmDaemonReadFailure $_) 'Expected original read refusal was replaced.'
  Assert-Current ($script:Dispatches.Count -eq 0 -and -not $script:ObservedTerminalRequestKeys.Contains((Get-TurnRequestKey $request))) 'Read refusal authorized dispatch/cache adoption.'
  Assert-Current ($script:LastRepairRequestWrite -eq [datetime]::MinValue -and $script:LastTerminalProtocolFailureWrite -eq [datetime]::MinValue) 'Read refusal advanced watermark.'
  $script:CurrentPassed=$true
 }
 throw
}
