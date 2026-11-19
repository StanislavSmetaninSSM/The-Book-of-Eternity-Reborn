param([string]$RepoRoot,[ValidateSet('consumer','dispose')][string]$Mode)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
$script:events=[Collections.Generic.List[string]]::new();$script:observations=@()
function Write-GmOperationDiagnostic {param($Stage,$ErrorRecord=$null,$Context=$null);$script:events.Add($Stage);if($Stage -ceq 'daemon-turn-consumer-returned'){$script:observations+=,$Context}}
function Require($Condition,$Message){if(-not $Condition){throw $Message}}
if($Mode -eq 'dispose'){
 $script:replyJoined=$false;$script:stderrJoined=$false;$script:processDisposed=$false
 $input=[pscustomobject]@{};$input|Add-Member ScriptMethod Dispose {$script:events.Add('stdin')}
 $process=[pscustomobject]@{StandardInput=$input;ExitCode=0}
 $process|Add-Member ScriptMethod WaitForExit {param($Timeout);$script:events.Add('exit');return $true}
 $process|Add-Member ScriptMethod Dispose {$script:processDisposed=$true;$script:events.Add('dispose')}
 $reply=[pscustomobject]@{IsCompleted=$true;IsCompletedSuccessfully=$true}
 $reply|Add-Member ScriptMethod GetAwaiter {return $this}
 $reply|Add-Member ScriptMethod GetResult {$script:replyJoined=$true;$script:events.Add('reply');return $null}
 $stderr=[pscustomobject]@{IsCompleted=$true;IsCompletedSuccessfully=$true}
 $stderr|Add-Member ScriptMethod GetAwaiter {return $this}
 $stderr|Add-Member ScriptMethod GetResult {$script:stderrJoined=$true;$script:events.Add('stderr');return ''}
 $context=[pscustomobject]@{disposed=$false;process=$process;pendingRead=$reply;errorRead=$stderr}
 Dispose-GmOperationTransport $context
 Require ($script:replyJoined -and $script:stderrJoined -and $script:processDisposed) 'Actual Dispose did not join real controlled leaves'
 Require (($script:events.ToArray() -join ',') -ceq 'stdin,helper-stdin-closed,exit,reply,stderr,dispose') 'Actual disposal order changed'
 # New proof must represent the completed joins, not the early disposed flag.
 Require ($context.replyReadJoined -eq $true -and $context.stderrReadJoined -eq $true -and $context.exitCode -eq 0) 'Missing successful post-join observation facts'
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
  $source=$source.Remove($offset,$length).Insert($offset,"return 'controlled-result'`n")
 }
 . ([scriptblock]::Create($source))
}
function Start-Sleep {param($Milliseconds)}
function Write-Log {param($Message,$Level,$Color)}
function Write-DaemonStatus {param($Status,$Reason)}
function Test-GmDaemonPath {param($Path);return $script:exists}
function Read-GmPromptPending {param($Path);return [pscustomobject]@{Hash=('a'*64);Request=[pscustomobject]@{sessionId='session';requestId='request';turnNumber=42}}}
function Get-TurnRequestKey {param($TurnRequest);return 'exact-key'}
function Test-ObservedTerminalRequestKey {param($Key);return $script:duplicate}
function Test-TurnRequestHasPendingSnapshotContext {param($TurnRequest,$RequestPath,$ExpectedSourceHash,[ref]$SamplingMismatch);$SamplingMismatch.Value=$false;return $script:valid}
function Add-ObservedTerminalRequestKey {param($Key)}
function Open-GmParticipatingOperation {param($Session);$script:events.Add('open');return $script:context}
function Close-GmParticipatingOperation {param($Context,$Outcome);$script:events.Add('close');$Context.closed=$true;$Context.closeObserved=$true;$Context.closeOutcome=$Outcome}
function Dispose-GmOperationTransport {param($Context);$script:events.Add('joined');$Context.disposed=$true;$Context.exitCode=0;$Context.replyReadJoined=$true;$Context.stderrReadJoined=$true}
$GameSessionPath=[IO.Path]::GetFullPath((Join-Path $RepoRoot 'source-only-observation-data'))
$guard=[pscustomobject]@{OwnerId='daemon-owner'};$guard|Add-Member ScriptMethod Validate {}; $script:LinuxDaemonGuard=$guard
$identity=[pscustomobject]@{rootKey=$GameSessionPath;runId=('1'*32);generationId=('2'*32);epoch=1;backend=2;hostInstanceId=('3'*32);bootId='boot'}
$close=[pscustomobject]@{identity=$identity;pinId=('4'*32);closeId=('5'*32);operationId=('6'*32);outcome=0;closingFailed=$false}
foreach($case in @('missing','duplicate','context-refused','success')){
 $script:events.Clear();$script:observations=@();$global:BoeMainOperationContext=$null;$script:IsProcessing=$false;$script:ErrorCount=0
 $script:exists=$case -ne 'missing';$script:duplicate=$case -eq 'duplicate';$script:valid=$case -ne 'context-refused'
 $script:context=[pscustomobject]@{session=$GameSessionPath;closed=$false;lost=$false;disposed=$false;closeObserved=$false;publicationUncertain=$false;localScopeCompleted=$false;closeOutcome=0;originalClose=$close;terminalClose=$close;exitCode=$null;replyReadJoined=$false;stderrReadJoined=$false}
 $actual=Process-Turn -RequestPath 'controlled-unused'
 Require ($script:context.closeObserved -and $script:context.disposed -and -not $script:IsProcessing -and $null -eq $global:BoeMainOperationContext) 'Lifecycle prerequisite failed'
 if($case -ne 'success'){Require ($script:observations.Count -eq 0) 'Stale/unadmitted path emitted positive';continue}
 Require ($actual -ceq 'controlled-result') 'Actual function result changed'
 Require (($script:events.ToArray() -join ',') -like 'open,close,joined*') 'Consumer close/join order failed'
 Require ($script:observations.Count -eq 1) 'Missing post-consumer-return observation after actual close/join'
}
