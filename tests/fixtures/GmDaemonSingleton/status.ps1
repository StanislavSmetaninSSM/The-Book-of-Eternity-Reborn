param([Parameter(Mandatory)][string]$RepoRoot,[ValidateSet('success','write-fails','readback-fails')][string]$Scenario)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_daemon_instance.ps1')
$GameSessionPath='/inert/game_session';$DaemonStatusFile='/inert/status.json'
$script:LinuxDaemonGuard=[pscustomobject]@{OwnerId='exact-owner';Checks=0}
$script:LinuxDaemonGuard|Add-Member ScriptMethod Validate {$this.Checks++}
$script:DaemonTerminalSettled=$false;$script:Written=$null
function Write-GmCanonicalText {param($SessionPath,$Path,$Value) if($Scenario -eq 'write-fails'){throw 'original write refusal'};$script:Written=$Value+[Environment]::NewLine}
function Get-GmDaemonText {param($Path) if($Scenario -eq 'readback-fails'){return 'different'};return $script:Written}
$failure=$null
try {Write-LinuxGmDaemonStatus ([ordered]@{status='running';sessionPath=$GameSessionPath})}catch{$failure=$_.Exception.Message}
if($Scenario -eq 'success'){
 if($failure){throw $failure}
 $row=$script:Written|ConvertFrom-Json
 if($row.daemonGuardVersion -ne 1 -or $row.daemonGuardOwnerId -cne 'exact-owner' -or $row.terminalSettled -ne $false -or $script:LinuxDaemonGuard.Checks -ne 2){throw 'Guarded running not confirmed'}
 $script:DaemonTerminalSettled=$true
 Write-LinuxGmDaemonStatus ([ordered]@{status='stopped';sessionPath=$GameSessionPath})
 if(-not ($script:Written|ConvertFrom-Json).terminalSettled){throw 'Clean final stop missing'}
}else{if(-not $failure){throw 'Original failed status publication must throw before bootstrap'}}
