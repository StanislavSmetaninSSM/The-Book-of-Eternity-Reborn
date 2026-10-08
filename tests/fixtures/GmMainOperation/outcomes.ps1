param([string]$RepoRoot,[string]$SessionPath,[string]$Scenario,[string]$Folder,[string]$TestSupport)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
# Only process bootstrap differs: same original C# control, pipes, Send/Invoke/
# Close/Dispose and real retained main owner. No production fault selector.
function Open-GmParticipatingOperation {
 param([string]$SessionPath)
 $session=[IO.Path]::GetFullPath($SessionPath).TrimEnd([IO.Path]::DirectorySeparatorChar)
 $root=[IO.Path]::GetDirectoryName($session)
 $start=[Diagnostics.ProcessStartInfo]::new('dotnet')
 $start.UseShellExecute=$false
 $start.RedirectStandardInput=$true; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
 foreach($arg in @($TestSupport,('control-outcome-bootstrap-'+$Scenario),$root,$Folder)){[void]$start.ArgumentList.Add($arg)}
 $process=[Diagnostics.Process]::Start($start)
 $context=[pscustomobject]@{process=$process;errorRead=$process.StandardError.ReadToEndAsync();session=$session;sequence=0L;closed=$false;lost=$false;disposed=$false;pendingRead=$null;originalClose=$null;closeObserved=$false;closeOutcome=0;terminalClose=$null}
 $script:ObservedContext=$context
 try {
  $ready=Read-GmOperationReply $context
  if(-not $ready.ok -or $ready.state -cne 'active' -or $ready.sequence -ne 0){throw 'Actual controlled helper did not admit original main operation.'}
  $context.originalClose=$ready.originalClose
  return $context
 } catch {Dispose-GmOperationTransport $context;throw}
}
$script:firstReply=$null; $script:failedReply=$null; $script:firstCaught=$false; $script:secondRefused=$false
$value=$null; $failure=$null
try {
 $value=Invoke-GmParticipatingConsumer $SessionPath {
  $ctx=$global:BoeMainOperationContext
  $first=@{action='write';path='game_state/control/f2-outcome.bin';bytes=[Convert]::ToBase64String([byte[]](0x42,0x80,0x01))}
  $second=@{action='write';path='game_state/control/f2-outcome-next.bin';bytes=[Convert]::ToBase64String([byte[]](0x43,0x02,0xfe))}
  try {$script:firstReply=Send-GmOperationCommand $ctx $first}
  catch {$script:firstCaught=$true;$script:failedReply=$ctx.lastCommandReply}
  if($Scenario -ne 'ps-closing') {
   try {[void](Send-GmOperationCommand $ctx $second)}
   catch {$script:secondRefused=$true;$script:failedReply=$ctx.lastCommandReply}
  }
  return 42
 }
} catch {$failure=$_.Exception}
$ctx=$script:ObservedContext
$report=[ordered]@{
 value=$value;firstReply=$script:firstReply;failedReply=$script:failedReply;firstCaught=$script:firstCaught;secondRefused=$script:secondRefused
 transportDisposed=$ctx.disposed;helperExitCode=$ctx.exitCode
 lost=$ctx.lost;closeOutcome=$ctx.closeOutcome;terminalClose=$ctx.terminalClose;originalClose=$ctx.originalClose;closeObserved=$ctx.closeObserved
 establishedResult=$(if($failure){$failure.Data['EstablishedOperationResult']}else{$null})
 establishedOutcome=$(if($failure){$failure.Data['EstablishedOperationOutcome']}else{$null})
 failure=$(if($failure){$failure.ToString()}else{$null})
}
[IO.File]::WriteAllText((Join-Path $Folder 'control-powershell.json'),($report|ConvertTo-Json -Depth 20 -Compress))
