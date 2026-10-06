[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory, [switch]$IncludeFixture, [switch]$IncludeHostGuardian, [switch]$IncludeTerminalFixture)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsLinux) { throw 'Native lineage build requires Linux.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$out = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($out) | Out-Null
$compiler = (Get-Command cc -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$version = (& $compiler --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Compiler version failed.' }
$flags = @('-std=c11', '-O2', '-g', '-Wall', '-Wextra', '-Werror', '-D_FORTIFY_SOURCE=2', '-fstack-protector-strong', '-Wl,-z,relro,-z,now')
$inputs = @(@{ source = 'native/linux/boe-lineage-supervisor.c'; binary = 'boe-lineage-supervisor' })
if ($IncludeFixture) { $inputs += @{ source = 'tests/fixtures/LinuxLineage/lineage-fixture.c'; binary = 'lineage-fixture' } }
if ($IncludeHostGuardian) { $inputs += @{ source = 'tests/fixtures/LinuxHost/host-guardian.c'; binary = 'host-guardian' } }
if ($IncludeTerminalFixture) { $inputs += @{ source = 'tests/fixtures/LinuxTerminal/neutral-cli.c'; binary = 'neutral-cli' } }
$assets = @()
foreach ($inputFile in $inputs) {
    $source = Join-Path $root $inputFile.source
    $binary = Join-Path $out $inputFile.binary
    & $compiler @flags $source '-o' $binary
    if ($LASTEXITCODE -ne 0) { throw "Native build failed: $($inputFile.source)" }
    $assets += [ordered]@{ source = $inputFile.source; sourceSha256 = (Get-FileHash $source -Algorithm SHA256).Hash.ToLowerInvariant(); binary = $inputFile.binary; binarySha256 = (Get-FileHash $binary -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$manifest = [ordered]@{ schemaVersion = 1; compiler = $compiler; compilerVersion = $version; flags = $flags; architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString(); os = [Runtime.InteropServices.RuntimeInformation]::OSDescription; assets = $assets }
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'build-provenance.json') -Encoding utf8NoBOM
$readelf = (Get-Command readelf -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$versionInfo = (& $readelf '--version-info' (Join-Path $out 'boe-lineage-supervisor') | Out-String)
if ($LASTEXITCODE -ne 0) { throw 'ELF version requirement inspection failed.' }
$glibcVersions = @([regex]::Matches($versionInfo, '\bGLIBC_(\d+\.\d+(?:\.\d+)?)\b') | ForEach-Object { [version]$_.Groups[1].Value } | Sort-Object -Unique)
if ($glibcVersions.Count -eq 0) { throw 'ELF glibc requirements are missing.' }
if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'Only linux-x64 package production is currently qualified.' }
$sourceCommit = (& git -C $root rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -cnotmatch '^[0-9a-f]{40}$') { throw 'Source commit provenance is unavailable.' }
$sourceText = Get-Content -LiteralPath (Join-Path $root 'native/linux/boe-lineage-supervisor.c') -Raw
$protocolMatch = [regex]::Match($sourceText, '(?m)^#define BOE_LINEAGE_PROTOCOL_MAX ([12])$')
$maximumProtocol = if ($protocolMatch.Success) { [int]$protocolMatch.Groups[1].Value } else { 1 }
$package = [ordered]@{
    schemaVersion = 1; maximumProtocolVersion = $maximumProtocol
    guarantee = 'ordinary-same-namespace-lineage'; runtimeIdentifier = 'linux-x64'
    minimumGlibc = $glibcVersions[-1].ToString(); binary = 'boe-lineage-supervisor'
    binarySha256 = $assets[0].binarySha256; sourceCommit = $sourceCommit
    sourceSha256 = $assets[0].sourceSha256; compilerVersion = $version
    compilerSha256 = (Get-FileHash $compiler -Algorithm SHA256).Hash.ToLowerInvariant(); flags = $flags
}
$package | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'package-manifest.json') -Encoding utf8NoBOM
# Explicit developer/package build only. Player launch never invokes this script.
Write-Output (Join-Path $out 'build-provenance.json')

if ($IncludeTerminalFixture) {
    $fixture = $assets | Where-Object { $_.binary -eq 'neutral-cli' }
    [ordered]@{ schemaVersion=1; fixture='neutral-cli'; source=$fixture.source; sourceSha256=$fixture.sourceSha256; binarySha256=$fixture.binarySha256; profile='neutral-v1'; argv=@() } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $out 'neutral-terminal-manifest.json') -Encoding utf8NoBOM
}
