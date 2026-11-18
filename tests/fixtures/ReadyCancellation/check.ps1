param([string]$Repo,[string]$Evidence,[string]$Mode)
$ErrorActionPreference='Stop'
. (Join-Path $Repo 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
$code=Join-Path $Evidence 'code';[void][IO.Directory]::CreateDirectory($code)
[IO.File]::WriteAllText((Join-Path $code 'BookOfEternityClient.dll'),'inert test assembly locator only')
$global:BoeMainOperationCodeRoot=$code
$session=Join-Path $Evidence 'game_session';[void][IO.Directory]::CreateDirectory($session)
$failure=$null;$context=$null
try {
    $context=Open-GmParticipatingOperation $session
    if($Mode -eq 'command-timeout') {$null=Send-GmOperationCommand $context @{action='daemon-snapshot';paths=@()}}
    else {throw 'Readiness timeout unexpectedly returned a consumer context.'}
} catch {$failure=$_}
finally {if($context){Dispose-GmOperationTransport $context}}
if(-not $failure){throw 'Original caller failure was lost.'}
$rows=@(Get-Content -LiteralPath $env:BOE_READY_TEST_LEDGER|ForEach-Object {$_|ConvertFrom-Json})
$cancels=@($rows|Where-Object {$null -ne $_.PSObject.Properties['cancel']})
if($Mode -eq 'command-timeout') {
    if($cancels.Count -ne 0){throw 'In-flight command timeout was reclassified as readiness cancellation.'}
} else {
    if($cancels.Count -ne 1){throw 'Expected exactly one explicit cancellation before EOF.'}
    $cancel=$cancels[0].cancel
    if($cancel.action -cne 'close' -or $cancel.sequence -ne 1 -or $cancel.outcome -ne 2){throw 'Cancellation command differs from original sequence1 Cancelled close.'}
}
if(@($rows|Where-Object {$null -ne $_.PSObject.Properties['unexpected']}).Count -ne 0){throw 'Duplicate close or replay observed.'}
@{mode=$Mode;callerFailure=$failure.Exception.ToString();ledger=$rows}|ConvertTo-Json -Depth 20 -Compress
