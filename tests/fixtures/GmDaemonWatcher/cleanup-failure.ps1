param([Parameter(Mandatory)][string]$RepoRoot,[Parameter(Mandatory)][string]$OutputDirectory,[switch]$PriorFatal)
$ErrorActionPreference='Stop'
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$source=[IO.File]::ReadAllText((Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'))
$t=$null;$e=$null;$ast=[Management.Automation.Language.Parser]::ParseInput($source,[ref]$t,[ref]$e)
if($e.Count){throw 'Parse failed'}
$cleanup=$ast.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Stop-GmDaemonWatcherResources'},$true)
. ([scriptblock]::Create($cleanup.Extent.Text))
$global:LaterDisposed=$false
$qteEffectResolutionWatcher=[pscustomobject]@{EnableRaisingEvents=$true}
$qteEffectResolutionWatcher|Add-Member ScriptMethod Dispose {throw 'Injected first watcher disposal failure'}
$watcher=[pscustomobject]@{EnableRaisingEvents=$true}
$watcher|Add-Member ScriptMethod Dispose {$global:LaterDisposed=$true}
$repairWatcher=$terminalProtocolFailureWatcher=$null
$daemonWatcherSubscriptions=@();$daemonWorkSignal=[Threading.AutoResetEvent]::new($false)
$script:DaemonFatalError=if($PriorFatal){[Management.Automation.ErrorRecord]::new([IO.IOException]::new('prior fatal'),'PriorFatal',[Management.Automation.ErrorCategory]::OperationStopped,$null)}else{$null}
$prior=$script:DaemonFatalError;$script:ErrorCount=0;$script:TurnCount=0;$script:Published=$null
function Write-DaemonStatus {param($Status,$FatalError,$Reason) $script:Published=@{status=$Status;fatal=$FatalError;reason=$Reason}}
function Write-Log {param($Message,$Level,$Color)}
$begin=$source.LastIndexOf('finally {'+[Environment]::NewLine+'    $script:DaemonWatcherCleanupFailures')
if($begin -lt 0){throw 'Actual shutdown block missing'}
. ([scriptblock]::Create('& '+$source.Substring($begin+'finally '.Length)))
$signalDisposed=$false;try{[void]$daemonWorkSignal.Set()}catch [ObjectDisposedException]{$signalDisposed=$true}
$priorPreserved=(-not $PriorFatal) -or [object]::ReferenceEquals($prior,$script:Published.fatal)
$pass=$script:Published.status -ceq 'failed' -and $priorPreserved -and $script:DaemonWatcherCleanupFailures.Count -eq 1 -and $global:LaterDisposed -and $signalDisposed -and -not $watcher.EnableRaisingEvents -and -not $qteEffectResolutionWatcher.EnableRaisingEvents
[ordered]@{pass=$pass;status=$script:Published.status;reason=$script:Published.reason;priorPreserved=$priorPreserved;cleanupFailures=$script:DaemonWatcherCleanupFailures;laterDisposed=$global:LaterDisposed;signalDisposed=$signalDisposed}|ConvertTo-Json|Set-Content (Join-Path $OutputDirectory 'cleanup-result.json')
if(-not $pass){throw 'Cleanup failure must report failed, retain prior fatal and attempt remaining cleanup'}
