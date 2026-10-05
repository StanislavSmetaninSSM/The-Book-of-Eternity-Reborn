[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory, [switch]$IncludeFixture)
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
# The output directory is the relocatable prototype package, not a system installation.
Write-Output (Join-Path $out 'build-provenance.json')
