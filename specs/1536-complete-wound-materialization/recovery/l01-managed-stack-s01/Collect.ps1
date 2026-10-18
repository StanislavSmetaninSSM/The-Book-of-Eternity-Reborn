param([Parameter(Mandatory)][string]$Manifest, [switch]$MetadataOnly)
$ErrorActionPreference = 'Stop'
$nativeStatus = 0
$observerFailure = $false
function Emit($value) {
    [Console]::Out.WriteLine('L01_COLLECTOR ' + ($value | ConvertTo-Json -Depth 12 -Compress))
    [Console]::Out.Flush()
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
        Emit @{ Stage = 'metadata_only'; Client = $assembly.FullName; ClientPath = $assembly.Location;
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
        Emit @{ Stage = 'request_started'; TargetPid = $m.TargetPid; TargetBirthTick = $m.TargetBirthTick; Nonce = $m.Nonce; Socket = $expectedSocket; Loaded = $loaded }
        $client = [Activator]::CreateInstance($clientType, [object[]]@([int]$m.TargetPid))
        # One public API call. No direct attach or manual security-setting operation.
        [void]$write.Invoke($client, [object[]]@([Enum]::ToObject($dumpType, 1), [string]$m.DumpPath, [Enum]::ToObject($flagType, 4)))
        Emit @{ Stage = 'request_returned'; NativeStatus = 0 }
    }
} catch {
    $nativeStatus = 7
    $cause = $_.Exception.GetBaseException()
    try { Emit @{ Stage = 'failure'; Type = $cause.GetType().FullName; HResult = $cause.HResult; Message = $cause.Message; NativeStatus = $nativeStatus } }
    catch { $observerFailure = $true }
}
try { Emit @{ Stage = 'collector_exit'; NativeStatus = $nativeStatus; ObserverFailure = $observerFailure } }
catch { $observerFailure = $true }
if ($nativeStatus -ne 0) { exit $nativeStatus }
if ($observerFailure) { exit 2 }
exit 0
