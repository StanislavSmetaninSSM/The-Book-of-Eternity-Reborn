param([Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference = 'Stop'
try {
    [IO.File]::AppendAllText((Join-Path $Evidence 'markers.log'), "launcher.before`n")
    # Same foreground nested invocation shape as Start-Daemon in the shipped launcher.
    # This inert leaf does not load the real daemon or any world/owner.
    $daemonArgs = @('-NoLogo','-NoProfile','-File',(Join-Path $PSScriptRoot 'child.ps1'),'-Evidence',$Evidence)
    & pwsh @daemonArgs
    [IO.File]::AppendAllText((Join-Path $Evidence 'markers.log'), "launcher.after`n")
} catch {
    [IO.File]::AppendAllText((Join-Path $Evidence 'errors.log'), "launcher.body: $($_.Exception)`n")
    throw
} finally {
    try {
        [IO.File]::AppendAllText((Join-Path $Evidence 'markers.log'), "launcher.finally.enter`n")
        [IO.File]::WriteAllText((Join-Path $Evidence 'launcher-finalized'), 'inert diagnostic only')
        [IO.File]::AppendAllText((Join-Path $Evidence 'markers.log'), "launcher.finally.exit`n")
    } catch {
        [IO.File]::AppendAllText((Join-Path $Evidence 'errors.log'), "launcher.finally: $($_.Exception)`n")
        throw
    }
}
