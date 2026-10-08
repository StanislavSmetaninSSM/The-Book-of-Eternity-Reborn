param([string]$RepoRoot,[string]$SessionPath,[string]$Folder,[string]$TestSupport,[string]$Mode="qte")
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
# Only the owned helper bootstrap is substituted to attach observation hooks.
# Original RunAsync/Send/Invoke/Close/Dispose and actual main admission remain.
function Write-FixtureMarker {
 param([string]$Path,[string]$Json)
 $temporary=$Path+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 try{[IO.File]::WriteAllText($temporary,$Json);[IO.File]::Move($temporary,$Path)}
 finally{if([IO.File]::Exists($temporary)){[IO.File]::Delete($temporary)}}
}
function Open-GmParticipatingOperation {
 param([string]$SessionPath)
 $session=[IO.Path]::GetFullPath($SessionPath).TrimEnd([IO.Path]::DirectorySeparatorChar)
 if([IO.Path]::GetFileName($session) -cne 'game_session'){throw 'Fixture session is not explicit.'}
 $root=[IO.Path]::GetDirectoryName($session)
 $start=[Diagnostics.ProcessStartInfo]::new('dotnet');$start.UseShellExecute=$false
 $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 foreach($arg in @($TestSupport,'daemon-storage-bootstrap',$root,$Folder)){[void]$start.ArgumentList.Add($arg)}
 $process=[Diagnostics.Process]::Start($start)
 $context=[pscustomobject]@{process=$process;errorRead=$process.StandardError.ReadToEndAsync();session=$session;sequence=0L;closed=$false;lost=$false;disposed=$false;pendingRead=$null;originalClose=$null;closeObserved=$false;closeOutcome=0;terminalClose=$null;lastCommandReply=$null;publicationUncertain=$false;uncertainCommandReply=$null;localScopeCompleted=$false}
 $script:ObservedContext=$context
 try {
  $ready=Read-GmOperationReply $context
  if(-not $ready.ok -or $ready.state -cne 'active' -or $ready.sequence -ne 0){throw 'Original main operation was not admitted.'}
  $context.originalClose=$ready.originalClose
  return $context
 } catch {Dispose-GmOperationTransport $context;throw}
}
$script:OriginalDispose=${function:Dispose-GmOperationTransport}
function Dispose-GmOperationTransport {
 param($Context)
 if($Context.disposed){return (& $script:OriginalDispose $Context)}
 $script:OriginalHelperPid=$Context.process.Id
 try{$Context.process.StandardInput.Close()}catch{}
 $script:HelperExitedBeforeDispose=$Context.process.WaitForExit(3500)
 & $script:OriginalDispose $Context
}
$daemon=Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($daemon,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Original daemon parse failed.'}
$names=@('Read-QteDaemonEligibility','Process-QteEffectResolutionRequest','Process-QteEffectResolutionRequestCore','Get-QteEffectResolutionRequestKey','Test-QteEffectResolutionRequestStillCurrent','Test-QteEffectResolutionReadyMatchesRequest','Build-QteEffectResolutionDispatchMessage','Read-GmPromptPending','Test-GmPromptSourceCurrent','Get-GmPromptContentHash','New-GmPromptOperation','New-GmPromptDelivery','Test-GmPromptDeliveryIdentity','Invoke-GmPromptControl','Send-ToGmBridge','Send-ToCliWindow','Dispatch-WithRetry','Complete-GmPromptDispatch','Test-GmPromptDispatchPaused','New-GmDispatchDiagnostics','Get-GameConfig','New-DefaultGmWorkerBridgeProfiles','Convert-RetiredCodexLaunchDefaults','Get-GmBridgeStatus','Ensure-GmBridgeStarted','Write-Log','Write-DaemonJsonFileBestEffort','Write-DaemonStatus')
if($Mode -ne 'qte'){$names=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)|ForEach-Object Name)}
$definitions=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)|Where-Object Name -in $names)
if($definitions.Count -ne $names.Count){throw 'Original daemon function selection incomplete.'}
$definitionFile=Join-Path $Folder 'original-daemon-functions.ps1'
[IO.File]::WriteAllText($definitionFile,(($definitions|ForEach-Object{$_.Extent.Text}) -join [Environment]::NewLine))
. $definitionFile
$GameSessionPath=$SessionPath;$ControlDir=Join-Path $SessionPath 'game_state/control'
$BridgeStatusFile=Join-Path $ControlDir 'gm_bridge_status.json'
$BridgeControlScript=Join-Path $RepoRoot 'BookOfEternityClient/Launcher/bookofeternity.ps1'
$QteEffectResolutionReadyFile=Join-Path $SessionPath 'ready/qte_effect_resolution_complete.json'
$DaemonStatusFile=Join-Path $ControlDir 'daemon-storage-status.json'
$script:GmTurnHelperBootstrapPath=Join-Path $RepoRoot 'BookOfEternityClient/Launcher/GM_Turn_Helper.ps1'
$script:StartTime=Get-Date;$script:DaemonCommandLine='inert daemon storage fixture';$TurnTimeout=1
$script:TurnCount=0;$script:ErrorCount=0;$script:IsProcessing=$false;$script:BridgeDispatchMaxWaitSeconds=1
$script:BridgeAutoStartAttempted=$false;$script:GmPromptInputPaused=$false;$LogFile=''
$script:Dispatches=[Collections.Generic.List[object]]::new()
# The sole inert delivery collaborator: real allocation/correlation/message and
# retry policy run above it; no launcher/provider/desktop input is invoked.
function Invoke-GmPromptControl {
 param($Operation,[string]$Command='dispatchPrompt')
 if($Command -cne 'dispatchPrompt'){throw 'Unexpected lower delivery command.'}
 $entry=[ordered]@{command=$Command;payload=($Operation.PayloadJson|ConvertFrom-Json);sourceHash=$Operation.SourceHash}
 $script:Dispatches.Add($entry)
 $script:LastFixtureOperation=$Operation
 if($Mode -eq 'post-send-refusal'){[IO.File]::WriteAllText((Join-Path $Folder 'post-send-armed'),'armed')}
 Write-FixtureMarker -Path (Join-Path $Folder 'dispatch-entered.json') -Json ($entry|ConvertTo-Json -Depth 12 -Compress)
 if($Mode -eq 'post-send-refusal'){return (New-GmPromptDelivery $Operation 'submission-observed' 'fixture-observed')}
 return (New-GmPromptDelivery $Operation 'not-written' 'fixture-no-send')
}
function Set-Clipboard {param($Value)throw 'Forbidden desktop fallback.'}
$failure=$null
try {
 Invoke-GmParticipatingConsumer $SessionPath {
  $context=$global:BoeMainOperationContext
  if($Mode -notin @('malformed-response','request-end-loss') -and -not $context.originalClose){throw 'Fixture requires an actual Running main grant.'}
  Write-FixtureMarker -Path (Join-Path $Folder 'admitted.json') -Json ($context.originalClose|ConvertTo-Json -Depth 10 -Compress)
  if($Mode -eq 'qte'){
   if([Console]::ReadLine() -cne 'go'){throw 'Original fixture release was not received.'}
   Process-QteEffectResolutionRequest -RequestPath (Join-Path $SessionPath 'input/qte_effect_resolution_request.json')
  }else{. (Join-Path $RepoRoot 'tests/fixtures/GmDaemonStorage/current.ps1')}
 }
} catch {$failure=$_.Exception}
$ctx=$script:ObservedContext
[IO.File]::WriteAllText((Join-Path $Folder 'powershell.json'),([ordered]@{
 currentPassed=($script:CurrentPassed -eq $true);currentEvidence=$script:CurrentEvidence
 daemonReadRefused=($null -ne $failure -and $failure.Data['GmDaemonReadRefused'] -eq $true)
 dispatches=$script:Dispatches.ToArray();failure=$(if($failure){$failure.ToString()}else{$null});errorCount=$script:ErrorCount
 helperPid=$script:OriginalHelperPid;helperExitedBeforeDispose=$script:HelperExitedBeforeDispose;helperExitCode=$ctx.exitCode
 disposed=$ctx.disposed;lost=$ctx.lost;closeObserved=$ctx.closeObserved;terminalClose=$ctx.terminalClose;originalClose=$ctx.originalClose
}|ConvertTo-Json -Depth 20 -Compress))
