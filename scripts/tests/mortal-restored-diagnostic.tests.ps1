[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot)
$scriptPath = Join-Path $root 'tests/fixtures/ProductionMain/test-mortal-restored-diagnostic.py'
# The official category runner owns the outer process deadline. This adapter
# executes only synthetic DATA tests, never the live driver entrypoint.
$lines = @(& python3 $scriptPath --category-contract 2>&1)
$code = $LASTEXITCODE
foreach ($line in $lines) { Write-Host $line }
$reports = @($lines | ForEach-Object { [string]$_ } | Where-Object { $_.StartsWith('RESTORED-DATA-RESULT ') })
if ($code -ne 0 -or $reports.Count -ne 1) { throw 'Restored DATA tests failed or lacked a unique actual result.' }
$report = $reports[0].Substring('RESTORED-DATA-RESULT '.Length) | ConvertFrom-Json
if ($report.testsRun -ne 18 -or $report.failures -ne 0 -or $report.errors -ne 0 -or $report.skipped -ne 0) {
    throw 'Restored DATA test counts are incomplete.'
}
Write-Host 'CATEGORY-CONTRACT-RESULT passed=1 failed=0'
