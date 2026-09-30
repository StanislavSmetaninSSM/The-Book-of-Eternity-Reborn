[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot)
$runner = Join-Path $repo 'scripts/test-csharp.ps1'
$source = Get-Content -LiteralPath $runner -Raw
if ($source -notmatch '\[switch\]\$ListCategories') {
    throw 'FAIL: runner must provide category discovery before any workload.'
}
$script:passed = 0
function Invoke-Runner([string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.WorkingDirectory = $repo
    foreach ($argument in @('-NoProfile', '-File', $runner) + $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(20000)) {
            $process.Kill($true)
            throw 'FAIL: metadata-only command started a workload or hung.'
        }
        return @{ ExitCode = $process.ExitCode; Output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult() }
    }
    finally { $process.Dispose() }
}
function Assert-Runner([string]$Name, [string[]]$Arguments, [bool]$Success, [string]$Pattern) {
    $result = Invoke-Runner $Arguments
    if (($result.ExitCode -eq 0) -ne $Success -or $result.Output -notmatch $Pattern) {
        throw "FAIL ${Name}: exit=$($result.ExitCode) $($result.Output)"
    }
    $script:passed++
    Write-Host "PASS $Name"
}
Assert-Runner 'No arguments show help without tests' @() $true 'Category'
Assert-Runner 'Catalog listing requires no build or discovery' @('-ListCategories') $true 'responsibility'
Assert-Runner 'Unknown category fails before workloads' @('-Category', 'does-not-exist') $false 'Unknown category'
Assert-Runner 'Legacy Fast is retired' @('-Lane', 'Fast') $false 'retired'
Assert-Runner 'Legacy PreMerge is retired' @('-Lane', 'PreMerge') $false 'retired'
Assert-Runner 'An all-category wildcard fails' @('-Category', '*') $false 'Unknown category'
# PowerShell parameter-binding diagnostics are localized by the host.
Assert-Runner 'Metadata and execution options cannot mix' @('-ListCategories', '-Category', 'does-not-exist') $false '.'
Write-Host "CATEGORY-CONTRACT-RESULT passed=$script:passed failed=0"
