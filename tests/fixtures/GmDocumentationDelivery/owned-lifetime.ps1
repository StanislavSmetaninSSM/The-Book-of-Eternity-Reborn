param(
    [ValidateSet('launcher', 'helper')][string]$Mode,
    [string]$Root,
    [string]$Package,
    [string]$PidPath,
    [string]$AckPath
)
$ErrorActionPreference = 'Stop'
# Controlled test-only lifetime probe. Never loads/replaces the production daemon.
if ($Mode -eq 'helper') {
    [IO.File]::WriteAllText($PidPath + '.ready', 'helper-ready')
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $chunk = 'controlled-owned-helper-' + ('x' * 8192)
    while ($watch.Elapsed.TotalSeconds -lt 30) {
        [Console]::Out.WriteLine($chunk)
        [Console]::Out.Flush()
        [Console]::Error.WriteLine($chunk)
        [Console]::Error.Flush()
        Start-Sleep -Milliseconds 20
    }
    exit 0
}
$start = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
$start.UseShellExecute = $false
foreach ($argument in @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', $PSCommandPath,
        '-Mode', 'helper', '-Root', $Root, '-Package', $Package, '-PidPath', $PidPath, '-AckPath', $AckPath)) {
    $start.ArgumentList.Add($argument)
}
$helper = [Diagnostics.Process]::Start($start)
$watch = [Diagnostics.Stopwatch]::StartNew()
while (!(Test-Path -LiteralPath ($PidPath + '.ready'))) {
    if ($watch.Elapsed.TotalSeconds -gt 20) { throw 'Controlled helper readiness timed out.' }
    Start-Sleep -Milliseconds 20
}
[IO.File]::WriteAllText($PidPath, [string]$helper.Id)
[Console]::Out.WriteLine('controlled-launcher-helper-observed ' + $helper.Id)
while (!(Test-Path -LiteralPath $AckPath)) {
    if ($watch.Elapsed.TotalSeconds -gt 20) { throw 'Controlled fixture identity acknowledgement timed out.' }
    Start-Sleep -Milliseconds 20
}
# The external fixture has proved the exact helper birth/argv before allowing us to
# exit. The helper deliberately survives; only that fixture owns timeout cleanup.
exit 0
