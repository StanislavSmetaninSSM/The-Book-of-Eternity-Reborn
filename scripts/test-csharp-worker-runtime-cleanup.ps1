[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RuntimeBasePath,

    [Parameter(DontShow)]
    [ValidateRange(0, 5000)]
    [int]$SelfTestDelayMilliseconds = 0,

    [Parameter(DontShow)]
    [string]$SelfTestStartedPath,

    [Parameter(DontShow)]
    [string]$SelfTestFinishedPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# This child is owned by the test runner's existing containment and deadline.
# Refuse every destination except its unique direct child of the physical temp root.
if (-not [System.IO.Path]::IsPathFullyQualified($RuntimeBasePath)) {
    throw "Worker runtime cleanup requires an absolute path."
}
$runtimePath = [System.IO.Path]::GetFullPath($RuntimeBasePath).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar)
$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar)
$runtimeName = [System.IO.Path]::GetFileName($runtimePath)
$prefix = "boe-test-worker-runtime-"
$runtimeId = [Guid]::Empty
if (-not [string]::Equals([System.IO.Path]::GetDirectoryName($runtimePath),
        $tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not $runtimeName.StartsWith($prefix, [StringComparison]::Ordinal) -or
    -not [Guid]::TryParseExact($runtimeName.Substring($prefix.Length), "N", [ref]$runtimeId)) {
    throw "Refusing cleanup of an unexpected worker runtime base."
}
foreach ($path in @($tempRoot, $runtimePath)) {
    if (Test-Path -LiteralPath $path) {
        if (((Get-Item -Force -LiteralPath $path).Attributes -band
                [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing worker runtime cleanup through a reparse point."
        }
    }
}
if (-not (Test-Path -LiteralPath $runtimePath -PathType Container)) {
    return
}
foreach ($entry in Get-ChildItem -Force -Recurse -LiteralPath $runtimePath) {
    if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing worker runtime cleanup containing a reparse point."
    }
}

if ($SelfTestDelayMilliseconds -ne 0) {
    if ([string]::IsNullOrWhiteSpace($SelfTestStartedPath) -or
        [string]::IsNullOrWhiteSpace($SelfTestFinishedPath)) {
        throw "Runtime cleanup self-test requires both observation paths."
    }
    $startedTempPath = $SelfTestStartedPath + ".tmp"
    [ordered]@{ ProcessId = $PID } | ConvertTo-Json -Compress |
        Set-Content -LiteralPath $startedTempPath
    [System.IO.File]::Move($startedTempPath, $SelfTestStartedPath)
    Start-Sleep -Milliseconds $SelfTestDelayMilliseconds
    Set-Content -LiteralPath $SelfTestFinishedPath -Value "delay completed"
}
Remove-Item -LiteralPath $runtimePath -Recurse -Force
if (Test-Path -LiteralPath $runtimePath) {
    throw "Worker runtime cleanup did not remove the owned directory."
}
