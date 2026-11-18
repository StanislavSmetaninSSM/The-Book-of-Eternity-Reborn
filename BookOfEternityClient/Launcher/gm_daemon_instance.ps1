# Linux-x64 only. Retained in this PowerShell process, not in a child helper.
# Non-Linux callers neither resolve nor load the net8 guard type.
function Start-LinuxGmDaemonInstance {
    if (-not ($PSVersionTable.PSEdition -eq 'Core' -and $IsLinux)) { return }
    if ([Environment]::Version.Major -lt 8) { throw 'Linux daemon requires a Core runtime supporting .NET 8.' }
    $codeRoot = Split-Path $PSScriptRoot -Parent
    $candidates = @(Join-Path $codeRoot 'BookOfEternityClient.dll') + @(@('Debug','Release') | ForEach-Object { Join-Path $codeRoot "bin/$_/net8.0/BookOfEternityClient.dll" })
    $assembly = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $assembly) { throw 'Trusted Linux daemon guard assembly is unavailable.' }
    Add-Type -Path $assembly
    $base = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($GameSessionPath).TrimEnd([IO.Path]::DirectorySeparatorChar))
    $reader = [Func[string]] {
        $bytes = Get-GmDaemonFileBytes $DaemonStatusFile
        if ($null -eq $bytes) { return $null }
        $offset = if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) { 3 } else { 0 }
        return [Text.UTF8Encoding]::new($false,$true).GetString($bytes,$offset,$bytes.Length-$offset)
    }
    $script:LinuxDaemonGuard = [BookOfEternityClient.Services.GmRuntime.LinuxDaemonInstanceGuard]::AcquireWithStatusReader($base,$reader)
    if ($script:LinuxDaemonGuard.SessionPath -cne [IO.Path]::GetFullPath($GameSessionPath).TrimEnd([IO.Path]::DirectorySeparatorChar)) { throw 'Daemon session path must be exact game_session.' }
    $initial = [ordered]@{ status='running'; pid=$PID; sessionPath=$GameSessionPath; startedAtUtc=$script:StartTime.ToUniversalTime().ToString('o'); heartbeatAtUtc=(Get-Date).ToUniversalTime().ToString('o'); turnTimeoutSeconds=$TurnTimeout; turnCount=0; errorCount=0 }
    # The sole bootstrap exception is the ordinary admitted parent for this status.
    Invoke-GmParticipatingConsumer $GameSessionPath {
        if (-not (Test-GmDaemonPath $ControlDir)) { Ensure-GmCanonicalDirectory -SessionPath $GameSessionPath -Path $ControlDir }
        Write-LinuxGmDaemonStatus $initial
    }
}

function Write-LinuxGmDaemonStatus {
    param($Payload)
    $script:LinuxDaemonGuard.Validate()
    $Payload.daemonGuardVersion = 1
    $Payload.daemonGuardOwnerId = $script:LinuxDaemonGuard.OwnerId
    $Payload.terminalSettled = ($Payload.status -ceq 'stopped' -and $script:DaemonTerminalSettled -eq $true)
    $json = $Payload | ConvertTo-Json -Depth 12
    Write-GmCanonicalText -SessionPath $GameSessionPath -Path $DaemonStatusFile -Value $json
    $observed = Get-GmDaemonText $DaemonStatusFile
    if ($observed -cne ($json + [Environment]::NewLine)) { throw 'Guarded daemon status publication readback is not exact.' }
    $script:LinuxDaemonGuard.Validate()
}
