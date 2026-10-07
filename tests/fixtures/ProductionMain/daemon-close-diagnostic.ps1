# Diagnostic-only prologue injected into the own relocated daemon copy.
# Function bodies and the ordinary launcher remain untouched. No receipt is minted.
$global:BoeOwnedCloseTracePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../daemon-close-trace.log'))
$global:BoeOwnedCloseTraceCount = 0
$ownHelper = Join-Path $PSScriptRoot 'Launcher/gm_main_operation.ps1'
foreach ($ownLine in @(7,9,10,11,46,49,53,54,55,56,63,67,68,69,70,97,100,102,108,111,122)) {
    $ownAction = [scriptblock]::Create(@'
try {
    if ($global:BoeOwnedCloseTraceCount -ge 700) { return }
    if ([IO.File]::Exists($global:BoeOwnedCloseTracePath) -and [IO.FileInfo]::new($global:BoeOwnedCloseTracePath).Length -ge 65536) { return }
    $global:BoeOwnedCloseTraceCount++
    $ownType = if ($null -ne $_ -and $null -ne $_.Exception) { $_.Exception.GetType().Name } else { 'none' }
    $ownPin = if ($null -ne $Context -and $null -ne $Context.originalClose) { $Context.originalClose.pinId } else { 'none' }
    $ownOp = if ($null -ne $Context -and $null -ne $Context.originalClose) { $Context.originalClose.operationId } else { 'none' }
    [IO.File]::AppendAllText($global:BoeOwnedCloseTracePath, ('phase=LINE pid={0} sequence={1} lost={2} closedObserved={3} pin={4} operation={5} type={6}' -f $PID,$Context.sequence,$Context.lost,$Context.closeObserved,$ownPin,$ownOp,$ownType) + [Environment]::NewLine)
} catch { }
'@.Replace('LINE',[string]$ownLine))
    Set-PSBreakpoint -Script $ownHelper -Line $ownLine -Action $ownAction | Out-Null
}
