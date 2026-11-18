param([Parameter(Mandatory)][string]$RepoRoot)
$ErrorActionPreference='Stop'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Actual daemon source parse failed'}
foreach($name in @('Test-DaemonProcessAlive','Assert-SingleDaemonInstance')) {
 $node=$ast.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true)
 if(-not $node){throw "Missing actual function $name"}
 . ([scriptblock]::Create($node.Extent.Text))
}
# This isolates script dispatch after independently tested in-process admission.
# It does not pretend this inert object is a real exclusive handle.
$script:LinuxDaemonGuard=[pscustomobject]@{Checks=0}
$script:LinuxDaemonGuard|Add-Member ScriptMethod Validate {$this.Checks++}
$script:Writes=0;$script:PidLookups=0
$GameSessionPath='/inert/game_session';$DaemonStatusFile='/inert/status.json'
function Read-DaemonStatus {return [pscustomobject]@{status='stopped';pid=111;sessionPath=$GameSessionPath;startedAtUtc='old';heartbeatAtUtc='old'}}
function Get-Process {param($Id,$ErrorAction) $script:PidLookups++;return [pscustomobject]@{Id=$Id}}
function Write-DaemonStatus {param($Status) $script:Writes++}
function Assert-GmNotDaemonReadFailure {param($ErrorRecord)}
Assert-SingleDaemonInstance
if($script:LinuxDaemonGuard.Checks -ne 1 -or $script:PidLookups -ne 0 -or $script:Writes -ne 0){throw 'Admitted Linux guard must be checked without PID fallback or duplicate running write'}
