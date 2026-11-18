param([Parameter(Mandatory=$true)][string]$GameSessionPath,[Parameter(Mandatory=$true)][string]$Helper)
$ErrorActionPreference='Stop'
. $Helper
$summary='Original participating operation refused or continuation unconfirmed (IOException; original-connection; AcquireAsync).'
if((Get-GmOperationFailureKind ("BOE_MAIN_OPERATION_FAILURE {}`n"+$summary)) -cne 'IOException; original-connection; AcquireAsync'){throw 'Separate diagnostics changed original summary classification.'}
foreach($invalid in @("prefix $summary", "$summary`n$summary",'Original participating operation refused or continuation unconfirmed (IOException; arbitrary; AcquireAsync).')){
    if((Get-GmOperationFailureKind $invalid) -cne 'Unspecified'){throw 'Diagnostic summary allowlist was broadened.'}
}
$script:held=@()
$script:nestedOriginal=$null
Invoke-GmParticipatingConsumer $GameSessionPath {
    $script:held += $global:BoeMainOperationContext
    $outer=$global:BoeMainOperationContext
    $null=Get-GmDaemonSnapshot -Paths @('game_state/control/gm_bridge_status.json')
    Invoke-GmParticipatingConsumer $GameSessionPath {
        if(-not [object]::ReferenceEquals($outer,$global:BoeMainOperationContext)){throw 'Nested participation replaced original context.'}
        $script:nestedOriginal=$global:BoeMainOperationContext.originalClose
        $null=Get-GmDaemonSnapshot -Paths @('game_state/control/gm_bridge_status.json')
    }
}
if($null -ne $global:BoeMainOperationContext){throw 'Outer context survived completed scope.'}
Invoke-GmParticipatingConsumer $GameSessionPath {
    $script:held += $global:BoeMainOperationContext
    $null=Test-GmDaemonPath (Join-Path $GameSessionPath 'game_state/control/gm_bridge_status.json')
}
$rows=@($script:held | ForEach-Object {
    if(-not $_.closeObserved -or -not $_.closed -or -not $_.disposed -or $_.exitCode -ne 0){throw 'Actual helper did not close and exit cleanly.'}
    @{originalClose=$_.originalClose;terminalClose=$_.terminalClose;closeObserved=$_.closeObserved;exitCode=$_.exitCode;diagnostic=$_.diagnostic}
})
if($rows.Count -ne 2 -or $rows[0].originalClose.operationId -ceq $rows[1].originalClose.operationId){throw 'Fresh heartbeat reused completed operation.'}
@{nestedOriginal=$script:nestedOriginal;operations=$rows}|ConvertTo-Json -Depth 20 -Compress
