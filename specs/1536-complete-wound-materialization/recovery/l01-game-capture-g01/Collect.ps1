param([Parameter(Mandatory)][string]$Manifest)
$ErrorActionPreference = 'Stop'
$preflightStatus = 0; $apiStatus = $null; $apiOutcome = 'UNREACHED'
$observerErrors = [Collections.Generic.List[string]]::new()
function Emit($value) {
    [Console]::Out.WriteLine('L01_COLLECTOR ' + ($value | ConvertTo-Json -Depth 12 -Compress))
    [Console]::Out.Flush()
}
function Observe-Emit($value) {
    try { Emit $value }
    catch { [void]$observerErrors.Add($_.Exception.GetBaseException().ToString()) }
}
function Invoke-ObservedApi([scriptblock]$Request, [scriptblock]$Writer, $Started) {
    $result = @{ ApiStatus = $null; ApiOutcome = 'UNREACHED'; Invocations = 0;
        ObserverErrors = [Collections.Generic.List[string]]::new(); Failure = $null }
    try { & $Writer $Started }
    catch {
        [void]$result.ObserverErrors.Add('Entry observation: ' + $_.Exception.GetBaseException().Message)
        return $result
    }
    $result.Invocations = 1
    try {
        & $Request
        $result.ApiStatus = 0
        $result.ApiOutcome = 'RETURNED'
    } catch {
        $cause = $_.Exception.GetBaseException()
        $result.ApiStatus = 7
        $result.ApiOutcome = 'FAILED'
        $result.Failure = @{ Type = $cause.GetType().FullName; HResult = $cause.HResult; Message = $cause.Message }
    }
    try { & $Writer @{ Stage = 'api_outcome'; ApiStatus = $result.ApiStatus; ApiOutcome = $result.ApiOutcome; Failure = $result.Failure } }
    catch { [void]$result.ObserverErrors.Add('Return observation: ' + $_.Exception.GetBaseException().Message) }
    return $result
}
function Get-CollectorExit($ApiStatus, [int]$PreflightStatus, [int]$ObserverCount) {
    if ($null -ne $ApiStatus -and $ApiStatus -ne 0) { return [int]$ApiStatus }
    if ($PreflightStatus -ne 0) { return $PreflightStatus }
    if ($ObserverCount -gt 0) { return 2 }
    return 0
}
function Identity([int]$processId) {
    $stat = [IO.File]::ReadAllText("/proc/$processId/stat")
    $fields = $stat.Substring($stat.LastIndexOf(')') + 2).Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
    $uidLine = [IO.File]::ReadAllLines("/proc/$processId/status") | Where-Object { $_.StartsWith('Uid:') }
    @{ BirthTick = [UInt64]$fields[19]; ParentId = [int]$fields[1]; Uid = [int](($uidLine -split '\s+')[1]) }
}

function Test-AdmissionHealth($h, $m, [long]$Now, [long]$Frequency) {
    foreach ($name in @('Pid','BirthTick','Root','Nonce','Cut','Sequence','Stamp','Frequency','DeadlineStamp','Cancelled','PhaseAdmitted','PolicyState','Health')) {
        if ($name -notin $h.PSObject.Properties.Name) { return 'MISSING_HEALTH_FIELD' }
    }
    if ($h.Pid -ne $m.TargetPid -or $h.BirthTick -ne $m.TargetBirthTick -or $h.Root -cne $m.Root -or
        $h.Nonce -cne $m.Nonce -or $h.Cut -ne 1 -or $h.DeadlineStamp -ne $m.DeadlineStamp -or
        $h.Sequence -lt $m.HealthFloor -or $h.Frequency -ne $Frequency -or $h.Stamp -gt $Now -or $h.Stamp -le 0) { return 'HEALTH_CONTEXT_OR_CLOCK_UNKNOWN' }
    if (($Now - $h.Stamp) / [double]$Frequency -gt 1 -or ($m.DeadlineStamp - $Now) / [double]$Frequency -lt 25) { return 'STALE_OR_INSUFFICIENT_WINDOW' }
    if ($h.Cancelled -isnot [bool] -or $h.PhaseAdmitted -isnot [bool] -or $h.Cancelled -or -not $h.PhaseAdmitted -or $h.PolicyState -cne 'ADMITTED') { return 'CANCELLED_OR_PHASE_NOT_ADMITTED' }
    foreach ($name in @('Uncertain','Eof','RawReadFault','AcceptedSeen','LateReturnSeen')) {
        if ($name -notin $h.Health.PSObject.Properties.Name -or $h.Health.$name -isnot [bool] -or $h.Health.$name) { return 'STICKY_HEALTH_UNCERTAINTY' }
    }
    foreach ($name in @('Dropped','Malformed','Oversize','IncompleteTail','IdentityRejected','Duplicates','ElapsedRegressions','TrackingExhausted')) {
        if ($name -notin $h.Health.PSObject.Properties.Name -or $h.Health.$name -ne 0) { return 'HEALTH_GAP' }
    }
    if ('ObserverFailure' -notin $h.Health.PSObject.Properties.Name -or $null -ne $h.Health.ObserverFailure -or -not $h.Health.CustodySeen) { return 'OBSERVER_OR_CUSTODY_UNKNOWN' }
    return $null
}
try {
    $m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
    foreach ($row in $m.LoadedFiles) {
        if ((Get-FileHash -LiteralPath $row.path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $row.sha256) { throw 'Loaded-file hash mismatch' }
        [void][Reflection.Assembly]::LoadFrom($row.path)
    }
    foreach ($row in $m.BinaryImages) {
        if ((Get-FileHash -LiteralPath $row.path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $row.sha256) { throw 'Game/source/dependency hash mismatch' }
    }
    $assembly = [Reflection.Assembly]::LoadFrom($m.ClientAssembly)
    $clientType = $assembly.GetType('Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient', $true)
    $dumpType = $assembly.GetType('Microsoft.Diagnostics.NETCore.Client.DumpType', $true)
    $flagType = $assembly.GetType('Microsoft.Diagnostics.NETCore.Client.WriteDumpFlags', $true)
    $write = $clientType.GetMethod('WriteDump', [Type[]]@($dumpType, [string], $flagType))
    if ($null -eq $write -or [int][Enum]::Parse($flagType, 'CrashReportEnabled') -ne 4) { throw 'Installed ABI differs' }
    $self = Identity $PID; $watchdog = Identity $m.WatchdogPid
    $owner = Identity $m.OwnerPid; $target = Identity $m.TargetPid
    if ($self.ParentId -ne $m.WatchdogPid -or $watchdog.BirthTick -ne $m.WatchdogBirthTick -or
        $watchdog.ParentId -ne $m.OwnerPid -or $owner.BirthTick -ne $m.OwnerBirthTick -or
        $target.ParentId -ne $m.OwnerPid -or $target.BirthTick -ne $m.TargetBirthTick -or
        $target.Uid -ne $m.Uid -or $owner.Uid -ne $m.Uid -or $watchdog.Uid -ne $m.Uid) { throw 'Owned real-resumer parent chain differs' }
    $parentPath = Join-Path $m.Own 'parent.json'
    if ((Get-FileHash -LiteralPath $parentPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $m.ParentFileSha256) { throw 'Immutable parent image differs' }
    $parent = [IO.File]::ReadAllText($parentPath) | ConvertFrom-Json
    if ($parent.Nonce -cne $m.Nonce -or $parent.ParentPid -ne $m.OwnerPid -or $parent.Cut -ne 1 -or
        -not $parent.Diagnostic -or $null -ne $parent.Fault) { throw 'Independent parent context differs' }
    $exe = [IO.File]::ResolveLinkTarget("/proc/$($m.TargetPid)/exe", $true).FullName
    $cmd = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes("/proc/$($m.TargetPid)/cmdline")).TrimEnd([char]0).Split([char]0)
    if ($exe -cne $m.TargetExecutable -or $cmd.Count -ne $m.TargetCommand.Count) { throw 'Owned command/executable differs' }
    for ($i = 0; $i -lt $cmd.Count; $i++) { if ($cmd[$i] -cne $m.TargetCommand[$i]) { throw 'Owned real-resumer argv differs' } }
    $expectedSocket = Join-Path (Join-Path $m.Own 'tmp') "dotnet-diagnostic-$($m.TargetPid)-$($m.TargetBirthTick)-socket"
    if ($env:TMPDIR -cne (Join-Path $m.Own 'tmp') -or $m.Socket -cne $expectedSocket -or
        [Text.Encoding]::UTF8.GetByteCount($expectedSocket) -gt 107 -or -not (Test-Path -LiteralPath $expectedSocket) -or
        $m.DumpPath -cne (Join-Path (Split-Path $Manifest) 'game-normal.core') -or
        $m.HealthPath -cne (Join-Path (Split-Path $Manifest) 'health.json') -or
        (Test-Path -LiteralPath $m.DumpPath) -or (Test-Path -LiteralPath ($m.DumpPath + '.crashreport.json'))) { throw 'Private socket/output binding differs' }
    $client = [Activator]::CreateInstance($clientType, [object[]]@([int]$m.TargetPid))
    # Final guard follows library loading/client construction and directly precedes the observed API call.
    if ((Identity $m.TargetPid).BirthTick -ne $m.TargetBirthTick) { throw 'Target changed before request' }
    $h = [IO.File]::ReadAllText($m.HealthPath) | ConvertFrom-Json
    $cancel = Test-AdmissionHealth $h $m ([Diagnostics.Stopwatch]::GetTimestamp()) ([Diagnostics.Stopwatch]::Frequency)
    if ($null -ne $cancel) { Observe-Emit @{ Stage = 'cancelled_before_api'; Reason = $cancel; ApiOutcome = 'UNREACHED' } }
    else {
        $result = Invoke-ObservedApi -Request {
            [void]$write.Invoke($client, [object[]]@([Enum]::ToObject($dumpType, 1), [string]$m.DumpPath, [Enum]::ToObject($flagType, 4)))
        } -Writer { param($event) Emit $event } -Started @{ Stage = 'request_started'; TargetPid = $m.TargetPid;
            TargetBirthTick = $m.TargetBirthTick; Nonce = $m.Nonce; HealthSequence = $h.Sequence; CapturePhase = 'UNKNOWN' }
        $apiStatus = $result.ApiStatus; $apiOutcome = $result.ApiOutcome
        foreach ($errorText in $result.ObserverErrors) { [void]$observerErrors.Add($errorText) }
    }
} catch {
    $preflightStatus = 7
    $cause = $_.Exception.GetBaseException()
    Observe-Emit @{ Stage = 'preflight_failure'; Type = $cause.GetType().FullName; HResult = $cause.HResult;
        PreflightStatus = $preflightStatus; ApiOutcome = $apiOutcome; ApiStatus = $apiStatus }
}
Observe-Emit @{ Stage = 'collector_exit'; ApiStatus = $apiStatus; ApiOutcome = $apiOutcome;
    PreflightStatus = $preflightStatus; ObserverErrors = @($observerErrors.ToArray()) }
exit (Get-CollectorExit $apiStatus $preflightStatus $observerErrors.Count)
