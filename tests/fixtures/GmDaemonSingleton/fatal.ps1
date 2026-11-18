param([Parameter(Mandatory)][string]$RepoRoot)
$ErrorActionPreference='Stop';$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Actual daemon source parse failed'}
foreach($name in @('New-DaemonErrorPayload','Write-DaemonFatalReport')){
 $node=$ast.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true)
 if(-not $node){throw 'Actual fatal function missing'}
 . ([scriptblock]::Create($node.Extent.Text))
}
$script:LinuxDaemonGuard=[pscustomobject]@{}
$script:LinuxDaemonGuard|Add-Member ScriptMethod Validate {throw 'Original guard namespace changed'}
$script:Writes=0;$GameSessionPath='/inert/game_session';$script:DaemonCommandLine='inert';$script:TurnCount=0;$script:ErrorCount=0;$DaemonFatalErrorFile='/inert/fatal.json'
function Write-DaemonJsonFileBestEffort {param($Path,$Payload,$Depth) $script:Writes++;return $true}
$failure=[Management.Automation.ErrorRecord]::new([IO.IOException]::new('original failure'),'original',[Management.Automation.ErrorCategory]::InvalidData,$null)
try {Write-DaemonFatalReport -ErrorRecord $failure}catch{}
if($script:Writes -ne 0){throw 'Lost singleton must not publish canonical fatal diagnostic'}
