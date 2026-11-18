param([string]$Repo,[string]$Evidence,[string]$Mode)
$ErrorActionPreference='Stop'
. (Join-Path $Repo 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
$code=Join-Path $Evidence 'code';[void][IO.Directory]::CreateDirectory($code)
[IO.File]::WriteAllText((Join-Path $code 'BookOfEternityClient.dll'),'inert test assembly locator only')
$global:BoeMainOperationCodeRoot=$code
$session=Join-Path $Evidence 'game_session';[void][IO.Directory]::CreateDirectory($session)
$failure=$null;$context=$null;$openReturned=$false
try {
    $context=Open-GmParticipatingOperation $session
    $openReturned=$true
    if($Mode -eq 'command-timeout') {$null=Send-GmOperationCommand $context @{action='daemon-snapshot';paths=@()}}

} catch {$failure=$_}
finally {if($context){Dispose-GmOperationTransport $context}}
if($Mode -ne 'command-timeout' -and $openReturned){throw 'Readiness timeout returned a usable consumer context.'}
if(-not $failure){throw 'Original caller failure was lost.'}
if($Mode -eq 'command-timeout'){
    if(-not $openReturned -or $failure.Exception.Message -cne 'Original operation reply unavailable; no replay.'){throw 'Dispatched command did not fail at its actual reply timeout.'}
} elseif($failure.Exception.Message -notmatch '^Original participating admission unavailable \(helper exit .*\)\.$' -and $failure.Exception.Message -cne 'Original operation reply unavailable; no replay.'){throw 'Readiness caller did not retain its original timeout wrapper.'}
$rows=@(Get-Content -LiteralPath $env:BOE_READY_TEST_LEDGER|ForEach-Object {$_|ConvertFrom-Json})
$cancels=@($rows|Where-Object {$null -ne $_.PSObject.Properties['cancel']})
if($Mode -eq 'command-timeout') {
    if($cancels.Count -ne 0){throw 'In-flight command timeout was reclassified as readiness cancellation.'}
    $commands=@($rows|Where-Object {$null -ne $_.PSObject.Properties['command']})
    if($commands.Count -ne 1 -or $commands[0].command.action -cne 'daemon-snapshot' -or $commands[0].command.sequence -ne 1){throw 'Exactly one original command was not dispatched.'}
} else {
    if($cancels.Count -ne 1){throw 'Expected exactly one explicit cancellation before EOF.'}
    $cancel=$cancels[0].cancel
    if($cancel.action -cne 'close' -or $cancel.sequence -ne 1 -or $cancel.outcome -ne 2){throw 'Cancellation command differs from original sequence1 Cancelled close.'}
}
if(@($rows|Where-Object {$null -ne $_.PSObject.Properties['unexpected']}).Count -ne 0){throw 'Duplicate close or replay observed.'}
@{mode=$Mode;callerFailure=$failure.Exception.ToString();ledger=$rows}|ConvertTo-Json -Depth 20 -Compress
