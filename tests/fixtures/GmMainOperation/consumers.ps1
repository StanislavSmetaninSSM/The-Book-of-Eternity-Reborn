param([string]$RepoRoot,[string]$ClientRoot,[string]$Scenario)
$ErrorActionPreference='Stop'
$GameSessionPath=Join-Path $ClientRoot 'game_session'
$ControlDir=Join-Path $GameSessionPath 'game_state/control'
$BridgeStatusFile=Join-Path $ControlDir 'gm_bridge_status.json'
$script:RepoRootPath=$RepoRoot
$script:GmTurnHelperBootstrapPath=Join-Path $ControlDir 'gm_turn_helper.bootstrap.ps1'
$script:GmContextPackRoot=Join-Path $ControlDir 'gm_context_pack'
$DaemonStatusFile=Join-Path $ControlDir 'gm_daemon_status.json'
$script:StartTime=Get-Date; $script:TurnCount=0; $script:ErrorCount=0; $script:DaemonCommandLine='inert';$script:DaemonLastLoopError=$null
$helper=Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1'
if(Test-Path $helper){. $helper}
$tokens=$null;$errors=$null
$source=Join-Path $RepoRoot $(if($Scenario -like 'launcher-*'){'BookOfEternityClient/Launcher/bookofeternity.ps1'}else{'BookOfEternityClient/game_master_daemon.ps1'})
$ast=[Management.Automation.Language.Parser]::ParseFile($source,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Actual source parse failed.'}
foreach($f in $ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)) {
 if($f.Name -in @('Write-DaemonJsonFileBestEffort','Write-DaemonStatus','New-DaemonErrorPayload','Get-GmBridgeStatus','Write-GmTurnHelperBootstrap','Quote-PowerShellSingleQuotedString','Copy-GmContextPackFile','Read-BridgeStatus','Get-BridgeStatusPath','Test-BridgeHelperAlive','Remove-BridgeStatusFileIfStopped')){. ([scriptblock]::Create($f.Extent.Text))}
}
function Write-Log {param($Message,$Level,$Color)}
$failed=$false
try {
 switch($Scenario) {
  'daemon-status' {Write-DaemonStatus -Status 'running';$target=$DaemonStatusFile}
  'daemon-stale' {Get-GmBridgeStatus | Out-Null;$target=$BridgeStatusFile}
  'bootstrap' {Write-GmTurnHelperBootstrap;$target=$script:GmTurnHelperBootstrapPath}
  'context-copy' {Copy-GmContextPackFile -RelativePath 'AGENTS.md' -Role 'inert' | Out-Null;$target=Join-Path $script:GmContextPackRoot 'AGENTS.md'}
  'launcher-stale' {Read-BridgeStatus $GameSessionPath | Out-Null;$target=$BridgeStatusFile}
  'launcher-delete' {Remove-BridgeStatusFileIfStopped $GameSessionPath;$target=$BridgeStatusFile}
 }
} catch {$failed=$true}
[ordered]@{exists=(Test-Path $target);failed=$failed;contextDirectory=(Test-Path $script:GmContextPackRoot)}|ConvertTo-Json -Compress
