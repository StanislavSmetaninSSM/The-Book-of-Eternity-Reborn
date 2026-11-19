param([string]$RepoRoot,[ValidateSet('consumer','dispose')][string]$Mode)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
$script:events=[Collections.Generic.List[string]]::new();$script:observations=@()
function Write-GmOperationDiagnostic {param($Stage,$ErrorRecord=$null,$Context=$null);$script:events.Add($Stage);if($Stage -ceq 'daemon-turn-consumer-returned'){$script:observations+=,$Context}}
function Require($Condition,$Message){if(-not $Condition){throw $Message}}
if($Mode -eq 'dispose'){
 foreach($case in @('reply-failure','reply-missing','stderr-failure','nonzero','success')){
  $script:events.Clear();$script:replyJoined=$false;$script:stderrJoined=$false;$script:processDisposed=$false;$script:joinCase=$case
  $input=[pscustomobject]@{};$input|Add-Member ScriptMethod Dispose {$script:events.Add('stdin')}
  $process=[pscustomobject]@{StandardInput=$input;ExitCode=$(if($case -eq 'nonzero'){1}else{0})}
  $process|Add-Member ScriptMethod WaitForExit {param($Timeout);$script:events.Add('exit');return $true}
  $process|Add-Member ScriptMethod Dispose {$script:processDisposed=$true;$script:events.Add('dispose')}
  $reply=[pscustomobject]@{IsCompleted=$true;IsCompletedSuccessfully=($case -ne 'reply-failure')}
  $reply|Add-Member ScriptMethod GetAwaiter {return $this}
  $reply|Add-Member ScriptMethod GetResult {if($script:joinCase -eq 'reply-failure'){throw 'controlled reply join failure'};$script:replyJoined=$true;$script:events.Add('reply');return $null}
  if($case -eq 'reply-missing'){$reply=$null}
  $stderr=[pscustomobject]@{IsCompleted=$true;IsCompletedSuccessfully=($case -ne 'stderr-failure')}
  $stderr|Add-Member ScriptMethod GetAwaiter {return $this}
  $stderr|Add-Member ScriptMethod GetResult {if($script:joinCase -eq 'stderr-failure'){throw 'controlled stderr join failure'};$script:stderrJoined=$true;$script:events.Add('stderr');return ''}
  $context=[pscustomobject]@{disposed=$false;process=$process;pendingRead=$reply;errorRead=$stderr}
  $caught=$null;try{Dispose-GmOperationTransport $context}catch{$caught=$_}
  if($case -eq 'stderr-failure'){Require ($null -ne $caught) 'Existing stderr failure was swallowed'}else{Require ($null -eq $caught) 'Unexpected disposal failure'}
  if($case -in @('reply-failure','reply-missing')){Require ($context.replyReadJoined -ne $true) 'Failed/missing reply falsely joined';continue}
  if($case -eq 'stderr-failure'){Require ($context.stderrReadJoined -ne $true) 'Failed stderr falsely joined';continue}
  if($case -eq 'nonzero'){Require ($context.exitCode -eq 1) 'Nonzero helper exit hidden';continue}
  Require ($script:replyJoined -and $script:stderrJoined -and $script:processDisposed) 'Actual Dispose did not join real controlled leaves'
  Require (($script:events.ToArray() -join ',') -ceq 'stdin,helper-stdin-closed,exit,reply,stderr,dispose') 'Actual disposal order changed'
  Require ($context.replyReadJoined -eq $true -and $context.stderrReadJoined -eq $true -and $context.exitCode -eq 0) 'Missing successful post-join observation facts'
 }
 return
}
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'),[ref]$tokens,[ref]$errors)
Require (-not $errors.Count) 'Daemon parse failure'
foreach($name in @('Process-Turn','Process-TurnCore')){
 $fn=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true));Require ($fn.Count -eq 1) 'Missing exact daemon function'
 $source=$fn[0].Extent.Text
 if($name -eq 'Process-TurnCore'){
  $tr=@($fn[0].Body.EndBlock.Statements|Where-Object{$_ -is [Management.Automation.Language.TryStatementAst]});Require ($tr.Count -eq 1) 'Core try shape'
  $boundary=@($tr[0].Body.Statements|Where-Object{$_.Extent.Text -ceq '$script:TurnCount++'});Require ($boundary.Count -eq 1) 'Unique processing milestone missing'
  $offset=$boundary[0].Extent.StartOffset-$fn[0].Extent.StartOffset
  $length=$tr[0].Body.Extent.EndOffset-1-$boundary[0].Extent.StartOffset
  $source=$source.Remove($offset,$length).Insert($offset,'if($script:case -eq ''body-refusal''){throw (New-GmDaemonReadFailure ''controlled body refusal'' $null)}; return ''controlled-result'''+"`n")
 }
 . ([scriptblock]::Create($source))
}
function Start-Sleep {param($Milliseconds)}
function Write-Log {param($Message,$Level,$Color)}
function Write-DaemonStatus {param($Status,$Reason)}
function Test-GmDaemonPath {param($Path);return $script:exists}
function Read-GmPromptPending {param($Path);$script:readCount++;return [pscustomobject]@{Hash=('A'*64);Request=[pscustomobject]@{sessionId='session';requestId='request';turnNumber=42}}}
function Get-TurnRequestKey {param($TurnRequest);return 'exact-key'}
function Test-ObservedTerminalRequestKey {param($Key);return $script:duplicate}
function Test-TurnRequestHasPendingSnapshotContext {param($TurnRequest,$RequestPath,$ExpectedSourceHash,[ref]$SamplingMismatch);$SamplingMismatch.Value=($script:case -eq 'sampling-mismatch');return $script:valid}
function Add-ObservedTerminalRequestKey {param($Key)}
function Open-GmParticipatingOperation {param($Session);$script:events.Add('open');return $script:context}
function Close-GmParticipatingOperation {param($Context,$Outcome);$script:events.Add('close');if($script:case -eq 'close-failure'){throw 'controlled close failure'};$Context.closed=$true;$Context.closeObserved=$true;$Context.closeOutcome=$Outcome}
function Dispose-GmOperationTransport {param($Context);$script:events.Add('joined');$Context.disposed=$true;$Context.exitCode=0;$Context.replyReadJoined=$true;$Context.stderrReadJoined=$true}
$root=[IO.Path]::GetFullPath((Join-Path $RepoRoot 'source-only-observation-data'));$GameSessionPath=Join-Path $root 'game_session'
$env:BOE_MAIN_STOP_DIAGNOSTIC_NONCE='77777777777777777777777777777777';$script:BoeOperationDiagnosticNonce=$env:BOE_MAIN_STOP_DIAGNOSTIC_NONCE
$guard=[pscustomobject]@{OwnerId=('8'*32)};$guard|Add-Member ScriptMethod Validate {}; $script:LinuxDaemonGuard=$guard
$identity=[pscustomobject]@{rootKey=$root;runId=('1'*32);generationId=('2'*32);epoch=1;backend=2;hostInstanceId=('3'*32);bootId='boot'}
$close=[pscustomobject]@{identity=$identity;pinId=('4'*32);closeId=('5'*32);operationId=('6'*32);outcome=0;closingFailed=$false}
foreach($case in @('missing','duplicate','context-refused','sampling-mismatch','nested','body-refusal','close-failure','uncertain','success')){
 $script:events.Clear();$script:observations=@();$global:BoeMainOperationContext=$null;$script:IsProcessing=$false;$script:ErrorCount=0;$script:readCount=0;$script:case=$case
 $script:exists=$case -ne 'missing';$script:duplicate=$case -eq 'duplicate';$script:valid=$case -notin @('context-refused','sampling-mismatch')
 $script:context=[pscustomobject]@{session=$GameSessionPath;closed=$false;lost=$false;disposed=$false;closeObserved=$false;publicationUncertain=($case -eq 'uncertain');uncertainCommandReply=$null;localScopeCompleted=$false;closeOutcome=0;originalClose=$close;terminalClose=$close;exitCode=$null;replyReadJoined=$false;stderrReadJoined=$false}
 if($case -eq 'nested'){$global:BoeMainOperationContext=$script:context}
 $caught=$null;$actual=$null;try{$actual=Process-Turn -RequestPath 'controlled-unused'}catch{$caught=$_}
 if($case -eq 'nested'){Require ($script:observations.Count -eq 0 -and -not $script:context.disposed) 'Nested call emitted/closed outer operation';$global:BoeMainOperationContext=$null;continue}
 if($case -in @('body-refusal','close-failure','uncertain')){Require ($null -ne $caught -and $script:observations.Count -eq 0 -and $script:context.disposed) 'Failed/uncertain consumer emitted positive or skipped disposal';continue}
 Require ($null -eq $caught) 'Unexpected consumer failure'

 Require ($script:context.closeObserved -and $script:context.disposed -and -not $script:IsProcessing -and $null -eq $global:BoeMainOperationContext) 'Lifecycle prerequisite failed'
 if($case -ne 'success'){Require ($script:observations.Count -eq 0) 'Stale/unadmitted path emitted positive';continue}
 Require ($actual -ceq 'controlled-result') 'Actual function result changed'
 Require (($script:events.ToArray() -join ',') -like 'open,close,joined*') 'Consumer close/join order failed'
 Require ($script:observations.Count -eq 1) 'Missing post-consumer-return observation after actual close/join'
 $proof=$script:observations[0]
 Require ($script:readCount -eq 1 -and $proof.requestSHA256 -ceq ('A'*64)) 'Request hash not captured exactly once from actual snapshot'
 Require ($proof.turnIdentity.sessionId -ceq 'session' -and $proof.turnIdentity.requestId -ceq 'request' -and $proof.turnIdentity.turnNumber -eq 42) 'Captured turn tuple differs'
 Require ($proof.daemonGuardOwnerId -ceq ('8'*32) -and $proof.requestAdmitted -eq $true -and $proof.consumerReturned -eq $true) 'Wrong owner/admission/return proof'
 Require (($proof.originalClose|ConvertTo-Json -Depth 12 -Compress) -ceq ($close|ConvertTo-Json -Depth 12 -Compress) -and ($proof.terminalClose|ConvertTo-Json -Depth 12 -Compress) -ceq ($close|ConvertTo-Json -Depth 12 -Compress)) 'Wrong original close tuple'
 Require ($proof.closeObserved -eq $true -and $proof.lost -eq $false -and $proof.publicationUncertain -eq $false -and $proof.helperExitCode -eq 0 -and $proof.replyReadJoined -eq $true -and $proof.stderrReadJoined -eq $true) 'Incomplete joined closure proof'
 Require ($proof.handledError -eq $false -and $proof.errorCountBefore -eq 0 -and $proof.errorCountAfter -eq 0) 'Handled error hidden'

}
