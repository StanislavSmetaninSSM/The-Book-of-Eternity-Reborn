param([Parameter(Mandatory)][string]$RepoRoot,[Parameter(Mandatory)][string]$Root,[ValidateSet('missing','empty','initial-write-fails','terminal-readback-fails','wrong-session','malformed-json','malformed-utf8')][string]$Scenario)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_daemon_instance.ps1')
[void][IO.Directory]::CreateDirectory($Root)
$GameSessionPath=Join-Path $Root 'game_session';$ControlDir=Join-Path $GameSessionPath 'game_state/control';$DaemonStatusFile=Join-Path $ControlDir 'gm_daemon_status.json'
$script:StartTime=Get-Date;$TurnTimeout=600;$script:DaemonTerminalSettled=$false;$script:LinuxDaemonGuard=$null
$script:Written=if($Scenario -eq 'empty'){''}elseif($Scenario -eq 'malformed-json'){'{'}else{$null};$script:ReadbackFailure=$false;$script:Writes=0
function Get-GmDaemonFileBytes {param($Path) if($Scenario -eq 'malformed-utf8'){return ,([byte[]]@(255,254,255))};if($null -eq $script:Written){return $null};return ,([Text.Encoding]::UTF8.GetBytes($script:Written))}
function Get-GmDaemonText {param($Path) if($script:ReadbackFailure){throw 'original terminal readback refused'};return $script:Written}
function Invoke-GmParticipatingConsumer {param($SessionPath,[scriptblock]$Body) & $Body}
function Test-GmDaemonPath {param($Path) return $true}
function Ensure-GmCanonicalDirectory {param($SessionPath,$Path) throw 'Unexpected fixture directory write'}
function Write-GmCanonicalText {param($SessionPath,$Path,$Value)
 if($Scenario -eq 'initial-write-fails'){throw 'original initial status write refused'}
 $script:Writes++;$script:Written=$Value+[Environment]::NewLine
}
if($Scenario -eq 'wrong-session'){$GameSessionPath=Join-Path $Root 'not_game_session'}
$failure=$null
try {Start-LinuxGmDaemonInstance}catch{$failure=$_.Exception.Message}
if($Scenario -eq 'wrong-session'){
 if(-not $failure){throw 'Wrong sibling must refuse'}
 if(Test-Path -LiteralPath (Join-Path $Root '.boe_runtime/locks/gm-daemon-instance.lock')){throw 'Wrong sibling poisoned canonical root guard'}
 $GameSessionPath=Join-Path $Root 'game_session';$failure=$null
 try {Start-LinuxGmDaemonInstance}catch{$failure=$_.Exception.Message}
}
try {
 if($Scenario -in @('missing','terminal-readback-fails','wrong-session')){
  if($failure){throw "Actual Start unexpectedly refused: $failure"}
  if(-not $script:LinuxDaemonGuard -or $script:Writes -ne 1){throw 'Actual Start did not retain guard and confirm running'}
  if($Scenario -in @('missing','wrong-session')){
   $oldOwner=$script:LinuxDaemonGuard.OwnerId
   $script:DaemonTerminalSettled=$true
   Write-LinuxGmDaemonStatus ([ordered]@{status='stopped';sessionPath=$GameSessionPath})
   $script:LinuxDaemonGuard.Dispose();$script:LinuxDaemonGuard=$null;$script:DaemonTerminalSettled=$false
   Start-LinuxGmDaemonInstance
   if($script:LinuxDaemonGuard.OwnerId -ceq $oldOwner -or ($script:Written|ConvertFrom-Json).status -cne 'running'){throw 'Actual clean stop/restart did not establish new owner'}
  }
  if($Scenario -eq 'terminal-readback-fails'){
   $script:DaemonTerminalSettled=$true;$script:ReadbackFailure=$true;$terminalFailure=$null
   try {Write-LinuxGmDaemonStatus ([ordered]@{status='stopped';sessionPath=$GameSessionPath})}catch{$terminalFailure=$_.Exception.Message}
   if(-not $terminalFailure -or ($script:Written|ConvertFrom-Json).status -cne 'stopped'){throw 'Failed-after-write premise was not reached'}
   $script:LinuxDaemonGuard.Dispose();$script:LinuxDaemonGuard=$null
   $retry=$null;$refused=$false
   try {$retry=[BookOfEternityClient.Services.GmRuntime.LinuxDaemonInstanceGuard]::Acquire($Root,$script:Written)}catch{$refused=$true}
   finally {if($retry){$retry.Dispose()}}
   if(-not $refused){throw 'Stopped JSON with unconfirmed terminal readback must not authorize restart'}
  }
 }else{
  if(-not $failure){throw 'Malformed/empty status and initial publication refusals must be preserved'}
  if($script:Writes -ne 0){throw 'Refused startup must not publish running status'}
 }
}finally{if($script:LinuxDaemonGuard){$script:LinuxDaemonGuard.Dispose();$script:LinuxDaemonGuard=$null}}
