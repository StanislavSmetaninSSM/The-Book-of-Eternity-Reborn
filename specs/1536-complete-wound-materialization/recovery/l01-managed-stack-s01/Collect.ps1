param([Parameter(Mandatory)][string]$Manifest, [switch]$MetadataOnly)
$ErrorActionPreference = 'Stop'
$preflightStatus = 0
$apiStatus = $null
$apiOutcome = 'UNREACHED'
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
try {
    $m = [IO.File]::ReadAllText($Manifest) | ConvertFrom-Json
    foreach ($row in $m.LoadedFiles) {
        if ((Get-FileHash -LiteralPath $row.path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $row.sha256) { throw "Loaded-file hash mismatch: $($row.path)" }
        [void][Reflection.Assembly]::LoadFrom($row.path)
    }
    $assembly = [Reflection.Assembly]::LoadFrom($m.ClientAssembly)
    $clientType = $assembly.GetType('Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient', $true)
    $dumpType = $assembly.GetType('Microsoft.Diagnostics.NETCore.Client.DumpType', $true)
    $flagType = $assembly.GetType('Microsoft.Diagnostics.NETCore.Client.WriteDumpFlags', $true)
    $write = $clientType.GetMethod('WriteDump', [Type[]]@($dumpType, [string], $flagType))
    if ($null -eq $write -or [int][Enum]::Parse($flagType, 'CrashReportEnabled') -ne 4) { throw 'Installed ABI differs.' }
    $loaded = @($m.LoadedFiles | ForEach-Object {
        $a = [Reflection.Assembly]::LoadFrom($_.path)
        @{ Path = $a.Location; Name = $a.FullName; Sha256 = (Get-FileHash -LiteralPath $a.Location -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    if ($MetadataOnly) {
        $target = [Reflection.Assembly]::LoadFile($m.TargetAssembly)
        Observe-Emit @{ Stage = 'metadata_only'; Client = $assembly.FullName; ClientPath = $assembly.Location;
            WriteDump = $write.ToString(); Loaded = $loaded; TargetMvid = $target.ManifestModule.ModuleVersionId.ToString() }
    } else {
        # This private collector accepts only this controller's live synthetic child.
        $self = Identity $PID
        $supervisor = Identity $m.SupervisorPid
        $target = Identity $m.TargetPid
        if ($self.ParentId -ne $m.SupervisorPid -or $supervisor.BirthTick -ne $m.SupervisorBirthTick -or
            $target.ParentId -ne $m.SupervisorPid -or $target.BirthTick -ne $m.TargetBirthTick -or
            $target.Uid -ne $m.Uid -or $supervisor.Uid -ne $m.Uid -or $m.Nonce.Length -ne 32) { throw 'Owned synthetic identity mismatch.' }
        $exe = [IO.File]::ResolveLinkTarget("/proc/$($m.TargetPid)/exe", $true).FullName
        $cmd = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes("/proc/$($m.TargetPid)/cmdline")).TrimEnd([char]0).Split([char]0)
        if ($exe -ne $m.TargetExecutable -or $cmd.Count -ne $m.TargetCommand.Count) { throw 'Owned command/executable mismatch.' }
        for ($i = 0; $i -lt $cmd.Count; $i++) { if ($cmd[$i] -ne $m.TargetCommand[$i]) { throw 'Owned command differs.' } }
        $expectedSocket = Join-Path $env:TMPDIR "dotnet-diagnostic-$($m.TargetPid)-$($m.TargetBirthTick)-socket"
        if ($expectedSocket -ne $m.Socket -or [Text.Encoding]::UTF8.GetByteCount($expectedSocket) -gt 107 -or
            -not (Test-Path -LiteralPath $expectedSocket) -or $m.DumpPath -ne (Join-Path (Split-Path $Manifest) 'synthetic-normal.core')) { throw 'Private socket/output mismatch.' }
        if ((Identity $m.TargetPid).BirthTick -ne $m.TargetBirthTick) { throw 'Target changed before request.' }
        $client = [Activator]::CreateInstance($clientType, [object[]]@([int]$m.TargetPid))
        # One public API call. No direct attach or manual security-setting operation.
        $result = Invoke-ObservedApi -Request {
            [void]$write.Invoke($client, [object[]]@([Enum]::ToObject($dumpType, 1), [string]$m.DumpPath, [Enum]::ToObject($flagType, 4)))
        } -Writer { param($event) Emit $event } -Started @{ Stage = 'request_started'; TargetPid = $m.TargetPid;
            TargetBirthTick = $m.TargetBirthTick; Nonce = $m.Nonce; Socket = $expectedSocket; Loaded = $loaded }
        $apiStatus = $result.ApiStatus
        $apiOutcome = $result.ApiOutcome
        foreach ($errorText in $result.ObserverErrors) { [void]$observerErrors.Add($errorText) }
    }
} catch {
    $preflightStatus = 7
    $cause = $_.Exception.GetBaseException()
    Observe-Emit @{ Stage = 'preflight_failure'; Type = $cause.GetType().FullName; HResult = $cause.HResult;
        Message = $cause.Message; PreflightStatus = $preflightStatus; ApiOutcome = $apiOutcome; ApiStatus = $apiStatus }
}
Observe-Emit @{ Stage = 'collector_exit'; ApiStatus = $apiStatus; ApiOutcome = $apiOutcome;
    PreflightStatus = $preflightStatus; ObserverErrors = @($observerErrors.ToArray()) }
exit (Get-CollectorExit $apiStatus $preflightStatus $observerErrors.Count)
