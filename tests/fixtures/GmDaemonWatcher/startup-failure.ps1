param([Parameter(Mandatory)][string]$RepoRoot,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$source=[IO.File]::ReadAllText((Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'))
$t=$null;$e=$null;$ast=[Management.Automation.Language.Parser]::ParseInput($source,[ref]$t,[ref]$e)
if($e.Count){throw ($e|Out-String)}
$cleanup=$ast.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Stop-GmDaemonWatcherResources'},$true)
if(-not $cleanup){throw 'Missing startup-safe independent cleanup'}
. ([scriptblock]::Create($cleanup.Extent.Text))
$InputDir=$ControlDir=$OutputDirectory
$unrelated=[Timers.Timer]::new(60000);$unrelatedId='unrelated-'+[guid]::NewGuid().ToString('N')
$unrelatedJob=Microsoft.PowerShell.Utility\Register-ObjectEvent $unrelated Elapsed -SourceIdentifier $unrelatedId -Action {}
$script:count=0
function Register-ObjectEvent {
 param($InputObject,$EventName,$SourceIdentifier,$MessageData,$Action)
 $script:count++
 if($script:count -eq 4){throw 'Injected fourth registration failure'}
 Microsoft.PowerShell.Utility\Register-ObjectEvent -InputObject $InputObject -EventName $EventName -SourceIdentifier $SourceIdentifier -MessageData $MessageData -Action $Action
}
# Execute the exact acquisition prefix, including its enclosing try; append
# only the inert catch/finally to avoid starting the actual daemon/game loop.
$begin=$source.IndexOf('$qteEffectResolutionWatcher = $watcher =')
$end=$source.IndexOf('Write-Log "Watching:', $begin)
if($begin -lt 0 -or $end -lt 0){throw 'Acquisition prefix missing'}
$failure=$null;$cleanupFailures=@()
try {
 . ([scriptblock]::Create($source.Substring($begin,$end-$begin)+@'
} catch { $failure=$_.Exception.Message }
finally {
 $cleanupFailures=@(Stop-GmDaemonWatcherResources -Watchers @($qteEffectResolutionWatcher,$watcher,$repairWatcher,$terminalProtocolFailureWatcher) -Subscriptions $daemonWatcherSubscriptions -Signal $daemonWorkSignal)
}
'@))
 $remaining=@(Get-EventSubscriber|Where-Object {$_.SourceIdentifier -like 'boe-daemon-*'})
 $unrelatedSurvives=@(Get-EventSubscriber -SourceIdentifier $unrelatedId).Count -eq 1
 $signalDisposed=$false;try{[void]$daemonWorkSignal.Set()}catch [ObjectDisposedException]{$signalDisposed=$true}
 $jobsRemain=@($daemonWatcherSubscriptions|Where-Object {Get-Job -Id $_.Job.Id -ErrorAction SilentlyContinue}).Count
 $pass=$failure -ceq 'Injected fourth registration failure' -and $daemonWatcherSubscriptions.Count -eq 3 -and $remaining.Count -eq 0 -and $jobsRemain -eq 0 -and $cleanupFailures.Count -eq 0 -and $unrelatedSurvives -and $signalDisposed
 [ordered]@{pass=$pass;failure=$failure;retainedSuccessfulSubscriptions=$daemonWatcherSubscriptions.Count;remainingOwnSubscribers=$remaining.Count;remainingOwnJobs=$jobsRemain;cleanupFailures=$cleanupFailures;unrelatedSurvives=$unrelatedSurvives;signalDisposed=$signalDisposed}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $OutputDirectory 'startup-result.json')
 if(-not $pass){throw 'Partial startup leaked resources or removed an unrelated subscriber'}
} finally {
 Unregister-Event -SourceIdentifier $unrelatedId -ErrorAction SilentlyContinue
 Remove-Job -Job $unrelatedJob -Force -ErrorAction SilentlyContinue
 $unrelated.Dispose()
}
