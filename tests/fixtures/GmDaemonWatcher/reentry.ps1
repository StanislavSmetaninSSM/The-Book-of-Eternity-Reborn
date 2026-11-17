param([Parameter(Mandatory)][string]$RepoRoot,[Parameter(Mandatory)][string]$OutputDirectory,[ValidateSet('control','action','repairAction','terminalProtocolFailureAction','qteEffectResolutionAction')][string]$Scenario='action')
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath (Join-Path $OutputDirectory 'notify.txt')){throw 'Fresh output directory required; do not reuse event evidence'}
[void][IO.Directory]::CreateDirectory($OutputDirectory)
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Actual daemon source parse failed'}
$assignment=$ast.Find({param($n)$n -is [Management.Automation.Language.AssignmentStatementAst] -and $n.Left.Extent.Text -ceq ('$'+$Scenario)},$true)
$si=[Diagnostics.ProcessStartInfo]::new('/usr/bin/python3');$si.ArgumentList.Add((Join-Path $PSScriptRoot 'reply.py'));$si.ArgumentList.Add((Join-Path $OutputDirectory 'notify.txt'));$si.UseShellExecute=$false;$si.RedirectStandardInput=$true;$si.RedirectStandardOutput=$true;$si.RedirectStandardError=$true
$global:ProbeTrace=[Collections.Generic.List[object]]::new();$global:ProbeOuterDone=$false
$global:ProbeContext=$null
# Only consumer work is replaced by a harmless command; real callback, event
# scheduling, Send/Read functions and stdio remain the production path.
function Invoke-ProbeConsumer {
 $global:ProbeTrace.Add(@{phase='consumer';outerDone=$global:ProbeOuterDone;sequence=$global:ProbeContext.sequence})
 [void](Send-GmOperationCommand $global:ProbeContext @{action='inert-consumer'})
}
function Process-Turn {param($RequestPath) Invoke-ProbeConsumer}
function Process-RepairRequest {param($RepairPath) Invoke-ProbeConsumer}
function Process-TerminalProtocolFailureRequest {param($FailurePath) Invoke-ProbeConsumer}
function Process-QteEffectResolutionRequest {param($RequestPath) Invoke-ProbeConsumer}
$signal=[Threading.AutoResetEvent]::new($false);$watcher=$null;$job=$null;$p=$null;$failure=$null;$reply=$null;$killed=$false;$signaled=$false
$id='inert-watcher-'+[guid]::NewGuid().ToString('N')
try {
 if($Scenario -ne 'control'){
  if(-not $assignment){throw 'Actual watcher action not found'}
  . ([scriptblock]::Create($assignment.Extent.Text))
  $callback=Get-Variable -Name $Scenario -ValueOnly
  $watcher=[IO.FileSystemWatcher]::new($OutputDirectory,'notify.txt');$watcher.NotifyFilter=[IO.NotifyFilters]::FileName
  $job=Register-ObjectEvent $watcher Created -SourceIdentifier $id -MessageData $signal -Action $callback
  $watcher.EnableRaisingEvents=$true
 }
 $p=[Diagnostics.Process]::Start($si);$err=$p.StandardError.ReadToEndAsync()
 $global:ProbeContext=[pscustomobject]@{process=$p;sequence=0L;closed=$false;lost=$false;pendingRead=$null;publicationUncertain=$false;lastCommandReply=$null}
 try{$reply=Send-GmOperationCommand $global:ProbeContext @{action='inert-outer'}}catch{$failure=$_.ToString()}
 $global:ProbeOuterDone=$true
 Start-Sleep -Milliseconds 200
 $signaled=$signal.WaitOne(0)
 if($signaled -and -not $failure){Invoke-ProbeConsumer}
} finally {
 if($watcher){$watcher.EnableRaisingEvents=$false;Unregister-Event $id -ErrorAction SilentlyContinue;$watcher.Dispose()}
 $eventErrors=if($job){@($job.Error|ForEach-Object {$_.ToString()})}else{@()}
 if($job){Remove-Job $job -Force}
 $signal.Dispose()
 if($p){$p.StandardInput.Close();if(-not $p.WaitForExit(2000)){$killed=$true;$p.Kill();$p.WaitForExit()}
 $remaining=$p.StandardOutput.ReadToEnd();$stderr=$err.GetAwaiter().GetResult();$exit=$p.ExitCode;$eof=$p.StandardOutput.EndOfStream;$p.Dispose()}
}
$pass=($reply.sequence -eq 1 -and -not $failure -and -not $global:ProbeContext.lost -and $exit -eq 0 -and $eof -and $err.IsCompleted -and -not $killed -and $eventErrors.Count -eq 0)
if($Scenario -ne 'control'){$pass=$pass -and $signaled -and $global:ProbeTrace.Count -eq 1 -and $global:ProbeTrace[0].outerDone}
$report=[ordered]@{scenario=$Scenario;pass=$pass;trace=@($global:ProbeTrace.ToArray());reply=$reply;failure=$failure;lost=$global:ProbeContext.lost;signaled=$signaled;eventErrors=$eventErrors;exitCode=$exit;stdoutEOF=$eof;stderrJoined=$err.IsCompleted;stderr=$stderr;remaining=$remaining;killRequired=$killed}
$report|ConvertTo-Json -Depth 8|Set-Content (Join-Path $OutputDirectory 'result.json')
if(-not $pass){throw 'Watcher must defer all consumer work until the outer helper command has returned; see result.json'}
