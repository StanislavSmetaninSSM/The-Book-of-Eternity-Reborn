# Diagnostic-only prologue injected into the own relocated daemon copy.
# Function bodies and the ordinary launcher remain untouched. No receipt is minted.
$global:BoeOwnedCloseTracePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../daemon-close-trace.log'))
$global:BoeOwnedCloseTraceArmedPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../daemon-stop-request.json'))
$global:BoeOwnedCloseTraceCount = 0
$ownHelper = Join-Path $PSScriptRoot 'Launcher/gm_main_operation.ps1'
foreach ($ownLine in @(7,9,10,11,46,49,53,54,55,56,63,67,68,69,70,97,100,102,108,111,122)) {
    $ownAction = [scriptblock]::Create(@'
try {
    # Ignore preparation traffic. The actual owner snapshot arms this trace
    # immediately before the sole Ctrl+C against the observed Active pin.
    if (-not [IO.File]::Exists($global:BoeOwnedCloseTraceArmedPath)) { return }
    if ($global:BoeOwnedCloseTraceCount -ge 700) { return }
    if ([IO.File]::Exists($global:BoeOwnedCloseTracePath) -and [IO.FileInfo]::new($global:BoeOwnedCloseTracePath).Length -ge 65536) { return }
    $global:BoeOwnedCloseTraceCount++
    # PS breakpoint actions receive the Breakpoint itself as $_. Observe only
    # the named ErrorRecord saved by the real consumer catch after it completes.
    $ownCloseError = $null; $ownBodyType = 'not-observed'
    if ($null -ne $failure -and $null -ne $failure.Exception) {
        if ($failure.FullyQualifiedErrorId -eq 'OriginalOperationContinuationUnconfirmed') {
            $ownCloseError = $failure.Exception.InnerException
        } else {
            $ownBodyType = $failure.Exception.GetType().Name
            $ownCloseError = $failure.Exception.Data['MainOperationCloseFailure']
        }
    }
    $ownType = 'not-observed'; $ownInner = 'not-observed'
    if ($null -ne $ownCloseError) {
        $ownType = $ownCloseError.GetType().Name
        if ($null -ne $ownCloseError.InnerException) { $ownInner = $ownCloseError.InnerException.GetType().Name }
    }
    $ownPin = if ($null -ne $Context -and $null -ne $Context.originalClose) { $Context.originalClose.pinId } else { 'none' }
    $ownOp = if ($null -ne $Context -and $null -ne $Context.originalClose) { $Context.originalClose.operationId } else { 'none' }
    $ownText = ('registeredPhase=LINE pid={0} sequence={1} lost={2} closedObserved={3} pin={4} operation={5} closeFailureType={6} closeInnerType={7} bodyFailureType={8}' -f $PID,$Context.sequence,$Context.lost,$Context.closeObserved,$ownPin,$ownOp,$ownType,$ownInner,$ownBodyType) + [Environment]::NewLine
    $ownBytes = if ([IO.File]::Exists($global:BoeOwnedCloseTracePath)) { [IO.FileInfo]::new($global:BoeOwnedCloseTracePath).Length } else { 0 }
    if ($ownBytes + [Text.Encoding]::UTF8.GetByteCount($ownText) -le 65536) { [IO.File]::AppendAllText($global:BoeOwnedCloseTracePath, $ownText) }
} catch { }
'@.Replace('LINE',[string]$ownLine))
    Set-PSBreakpoint -Script $ownHelper -Line $ownLine -Action $ownAction | Out-Null
}
