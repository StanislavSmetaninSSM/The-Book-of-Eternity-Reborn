param([string]$SessionPath,[string]$EvidencePath)
$ErrorActionPreference='Stop'
$GameSessionPath=$SessionPath
. (Join-Path $PSScriptRoot 'Launcher/gm_main_operation.ps1')
# Only function definitions from the actual shipped daemon; no top-level daemon,
# watcher, provider or game turn startup. Missing requests execute real early-return
# cores under the real original participating connection, not a fake mutation API.
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'game_master_daemon.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Shipped daemon parse failed.'}
$names=@('Quote-PowerShellSingleQuotedString','Write-GmTurnHelperBootstrap','Write-DaemonJsonFileBestEffort','Write-DaemonStatus','Process-Turn','Process-TurnCore','Process-QteEffectResolutionRequest','Process-QteEffectResolutionRequestCore','Process-RepairRequest','Process-RepairRequestCore','Get-GameConfig','Convert-RetiredCodexLaunchDefaults','New-DefaultGmWorkerBridgeProfiles','Get-GmBridgeStatus','Ensure-GmBridgeStarted','Send-ToCliWindow','Send-ToGmBridge','Dispatch-WithRetry','New-GmDispatchDiagnostics','Read-GmPromptPending','Test-GmPromptSourceCurrent','Get-GmPromptContentHash','New-GmPromptOperation','New-GmPromptDelivery','Test-GmPromptDeliveryIdentity','Invoke-GmPromptControl','Complete-GmPromptDispatch','Test-GmPromptDispatchPaused')
foreach($f in $ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)){
 if($f.Name -in $names){. ([scriptblock]::Create($f.Extent.Text))}
}
function Write-Log {param($Message,$Level,$Color)}
$script:ObservedConnections=[Collections.Generic.List[object]]::new()
Set-Item Function:Open-OriginalFixtureOperation (Get-Command Open-GmParticipatingOperation).ScriptBlock
function Open-GmParticipatingOperation {
 param([string]$SessionPath)
 $context=Open-OriginalFixtureOperation $SessionPath
 $script:ObservedConnections.Add($context)
 return $context
}
$ControlDir=Join-Path $SessionPath 'game_state/control'
$BridgeStatusFile=Join-Path $ControlDir 'gm_bridge_status.json'
$BridgeControlScript=Join-Path $PSScriptRoot 'Launcher/bookofeternity.ps1'
$script:GmTurnHelperBootstrapPath=Join-Path $ControlDir 'm1_gm_turn_helper_bootstrap.ps1'
$DaemonStatusFile=Join-Path $ControlDir 'm1_daemon_status.json'
$script:StartTime=Get-Date;$script:DaemonCommandLine='inert M1 actual functions';$TurnTimeout=20
$script:TurnCount=0;$script:ErrorCount=0;$script:IsProcessing=$false
Write-GmTurnHelperBootstrap
Write-DaemonStatus -Status 'running' -Reason 'isolated M1 consumer qualification'
if(!(Test-Path $script:GmTurnHelperBootstrapPath) -or !(Test-Path $DaemonStatusFile)){throw 'Actual participating publication missing.'}
# Each absent request admits and retires its real pin; no provider/model/gamework.
$missing=Join-Path $SessionPath 'input/missing-m1-request.json'
Process-Turn -RequestPath $missing
Process-QteEffectResolutionRequest -RequestPath $missing
Process-RepairRequest -RepairPath $missing
if($script:ObservedConnections.Count -ne 5){throw 'Actual consumer boundaries failed to acquire five distinct original connections.'}
$connections=@($script:ObservedConnections | ForEach-Object {
 if(-not $_.closed -or -not $_.closeObserved -or -not $_.disposed -or $_.lost -or $_.exitCode -ne 0){throw 'Actual original consumer close/disposal not confirmed.'}
 [ordered]@{close=$_.terminalClose;closeObserved=$_.closeObserved;disposed=$_.disposed;exitCode=$_.exitCode;sequence=$_.sequence}
})
# Actual OwnedTerminal transport -> actual shipped launcher -> actual pipe/T042;
# the configured CLI treats these as inert text. Retained duplicate is not replay.
$dispatch=Dispatch-WithRetry -Message 'consumer Ж😀' -OperationKind 'turn' -OperationRevision 'M1' -ReturnDetails
if($dispatch.Status -ne 'sent' -or $dispatch.PromptDelivery.disposition -ne 'submission-observed'){throw ('Actual dispatch unconfirmed: '+($dispatch|ConvertTo-Json -Depth 10 -Compress))}
$again=Dispatch-WithRetry -Message 'consumer Ж😀' -OperationKind 'turn' -OperationRevision 'M1' -ReturnDetails
if($again.PromptDelivery.operationId -cne $dispatch.PromptDelivery.operationId -or $again.PromptDelivery.disposition -ne 'submission-observed'){throw 'Original delivery identity changed on retained observation.'}
[IO.File]::WriteAllText($EvidencePath,([ordered]@{connections=$connections;dispatch=$dispatch;retained=$again;realFunctions=$names;gameRequests=0;modelRequests=0}|ConvertTo-Json -Depth 20))
