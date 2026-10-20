<#
.SYNOPSIS
Builds, describes or performs the single spec13 exploratory snapshot probe.
.DESCRIPTION
Uses already accepted game assemblies and a separately built throwaway harness.
Run consumes a one-attempt guard and never retries a failed component probe.
.PARAMETER Mode
Build compiles the diagnostic source only; Describe inspects metadata only;
Run invokes the existing Windows-owned child route once with a30-second cap.
RunCorrectedSetup is limited to the archived BOM setup failure before any component reads.
.PARAMETER RepoRoot
The existing dedicated repository checkout holding the accepted source/evidence.
.PARAMETER BuildDirectory
An explicitly owned external build directory for this diagnostic harness.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Build','Describe','Run','RunCorrectedSetup')][string]$Mode,
    [Parameter(Mandatory)][string]$RepoRoot,
    [Parameter(Mandatory)][string]$BuildDirectory
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$probeRepo = (Resolve-Path -LiteralPath $RepoRoot).Path
$probeBuild = [IO.Path]::GetFullPath($BuildDirectory)
$probeRepoPrefix = $probeRepo.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
if ($probeBuild.StartsWith($probeRepoPrefix,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'The diagnostic build must remain outside the repository source closure.'
}
if (-not [IO.Path]::IsPathFullyQualified($BuildDirectory) -or
    -not ([IO.Path]::GetFileName($probeBuild).StartsWith('spec13-harness-build',[StringComparison]::Ordinal))) {
    throw 'Provide an absolute explicitly owned spec13-harness-build directory.'
}
Set-Location -LiteralPath $probeRepo
$probeRecovery = Join-Path $probeRepo 'specs/1536-complete-wound-materialization/recovery/crash5'
$probePrep = Get-Content -Raw -LiteralPath (Join-Path $probeRecovery 'T277-prep.json') | ConvertFrom-Json
$probeBinding = $probePrep.Binding
foreach ($probeBinary in $probeBinding.Binaries) {
    if ((Get-FileHash -LiteralPath (Join-Path $probeRepo $probeBinary.Path) -Algorithm SHA256).Hash -ne $probeBinary.SHA256) {
        throw 'A game/test binary differs from the accepted f479 binding; no probe was started.'
    }
}
$probeSourceDelta = @(git diff --name-only $probeBinding.CompiledSource HEAD -- BookOfEternityClient BookOfEternityClient.IntegrationTests BookOfEternityClient.TestSupport native tests scripts)
if ($LASTEXITCODE -ne 0 -or $probeSourceDelta.Count -ne 0) {
    throw 'Compiled game/test source changed after the accepted binding.'
}
$probeBin = Join-Path $probeRepo 'BookOfEternityClient.IntegrationTests/bin/Debug/net8.0'
$probeSoul = Join-Path $probeRepo 'specs/1536-complete-wound-materialization/fixtures/joint-interface-source42/artifact/payload/game_session/game_state/meta/soul_state.json'
$probeSoulHash = '19E66A7A2059C8F569862E55FE728B6A6A3BA62C93FDBE42265E821B7DE68430'
if ((Get-Item -LiteralPath $probeSoul).Length -ne 415 -or
    (Get-FileHash -LiteralPath $probeSoul -Algorithm SHA256).Hash -ne $probeSoulHash) {
    throw 'The immutable diagnostic input differs from its exact accepted415-byte source.'
}
$probeSourceFiles = @('SnapshotProbe.cs','SnapshotProbe.csproj')
$probeProject = Join-Path $probeBuild 'SnapshotProbe.csproj'
$probeDll = Join-Path $probeBuild 'bin/Release/net8.0/SnapshotProbe.dll'
$probePlan = Join-Path $probeRepo '.superpowers/sdd/plan'
if ($Mode -eq 'Build') {
    New-Item -ItemType Directory -Path $probeBuild -Force | Out-Null
    foreach ($probeSourceFile in $probeSourceFiles) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $probeSourceFile) -Destination (Join-Path $probeBuild $probeSourceFile)
    }
    & dotnet build $probeProject -c Release "-p:GameBinPath=$probeBin" -p:GenerateDocumentationFile=true -p:RestoreIgnoreFailedSources=true -p:NuGetAudit=false *> (Join-Path $probePlan 'T283-harness-build.log')
    $probeBuildExit = $LASTEXITCODE
    Get-Content -LiteralPath (Join-Path $probePlan 'T283-harness-build.log') -Tail 12
    if ($probeBuildExit -ne 0) { exit $probeBuildExit }
    if (-not (Test-Path -LiteralPath $probeDll)) { throw 'The diagnostic build did not produce the expected assembly.' }
    exit 0
}
foreach ($probeSourceFile in $probeSourceFiles) {
    if ((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $probeSourceFile) -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath (Join-Path $probeBuild $probeSourceFile) -Algorithm SHA256).Hash) {
        throw 'Diagnostic build sources differ; build the current harness before invocation.'
    }
}
if (-not (Test-Path -LiteralPath $probeDll)) { throw 'Build the external diagnostic harness first.' }
if ($Mode -eq 'Describe') {
    & dotnet $probeDll describe $probeBin *> (Join-Path $probePlan 'T283-describe.log')
    $probeDescribeExit = $LASTEXITCODE
    Get-Content -LiteralPath (Join-Path $probePlan 'T283-describe.log')
    exit $probeDescribeExit
}
if (git status --porcelain) { throw 'Save a clean remote source/evidence checkpoint before the single runtime attempt.' }
$probeCorrectedSetup = $Mode -eq 'RunCorrectedSetup'
if ($probeCorrectedSetup) {
    $probeSetupRecord = Get-Content -Raw -LiteralPath (Join-Path $probeRecovery 'T284-setup-failure.json') | ConvertFrom-Json
    $probeSetupRaw = Join-Path $probeRecovery 'T284-setup-failure.zip'
    if ($probeSetupRecord.Classification -ne 'SETUP_FAIL before targetReadSnapshot; not a measured component result' -or
        (Get-FileHash -LiteralPath $probeSetupRaw -Algorithm SHA256).Hash -ne $probeSetupRecord.ArchiveSHA256 -or
        -not $probeSetupRecord.Parent.stop.CleanupComplete -or $probeSetupRecord.Parent.stop.AuthorityRetained -or
        -not $probeSetupRecord.Parent.disposed -or -not $probeSetupRecord.RootRetainedAfterArchive) {
        throw 'Only the preserved BOM setup failure before component execution permits this one corrected measurement.'
    }
    if (@($probeSetupRecord.ChildRecords | Where-Object { $_.kind -in @('fixture','child','measurementStart') }).Count) {
        throw 'The previous child executed the component; retry is prohibited.'
    }
}
$probeAttemptPath = Join-Path $probePlan $(if ($probeCorrectedSetup) { 'T284-measurement-attempt.json' } else { 'T284-attempt.json' })
$probeOutcomePath = Join-Path $probeRecovery 'T284-outcome.json'
if ((Test-Path -LiteralPath $probeAttemptPath) -or (Test-Path -LiteralPath $probeOutcomePath)) {
    throw 'The permitted spec13 probe attempt is already consumed; inspect its evidence without retry.'
}
$probeEvidence = Join-Path $probePlan $(if ($probeCorrectedSetup) { 'T284-measurement-evidence' } else { 'T284-evidence' })
if (Test-Path -LiteralPath $probeEvidence) { throw 'Probe evidence directory already exists; inspect without replacement.' }
New-Item -ItemType Directory -Path $probeEvidence | Out-Null
$probeOwn = Join-Path ([IO.Path]::GetTempPath()) ('boe-spec13-probe-' + [Guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $probeOwn) { throw 'Unexpected existing owned probe directory.' }
$probeAttempt = [ordered]@{
    State='Started'; Attempt=1; RetryAuthorized=$false; SetupCorrection=$probeCorrectedSetup; Head=(git rev-parse HEAD).Trim()
    CompiledSource=$probeBinding.CompiledSource; StartedUtc=[DateTime]::UtcNow.ToString('o')
    HarnessSHA256=(Get-FileHash -LiteralPath $probeDll -Algorithm SHA256).Hash
    SoulSHA256=$probeSoulHash; ChildSeconds=30; ParentAndCleanupSeconds=120
    OwnedRoot=$probeOwn; EvidenceDirectory=$probeEvidence; BuildDirectory=$probeBuild
    Binaries=$probeBinding.Binaries
}
$probeAttempt | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $probeAttemptPath
& dotnet $probeDll parent $probeBin $probeOwn $probeSoul $probeEvidence *> (Join-Path $probePlan 'T284-parent-host.log')
$probeRunExit = $LASTEXITCODE
$probeAttempt.State='Completed'
$probeAttempt.ExitCode=$probeRunExit
$probeAttempt.EndedUtc=[DateTime]::UtcNow.ToString('o')
$probeAttempt.RootAbsentAfterParent= -not (Test-Path -LiteralPath $probeOwn)
$probeAttempt.BinariesUnchanged = @($probeBinding.Binaries | Where-Object {
    (Get-FileHash -LiteralPath (Join-Path $probeRepo $_.Path) -Algorithm SHA256).Hash -ne $_.SHA256
}).Count -eq 0
$probeAttempt.SoulUnchanged = (Get-FileHash -LiteralPath $probeSoul -Algorithm SHA256).Hash -eq $probeSoulHash
$probeAttempt | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $probeAttemptPath
$probeAttempt | ConvertTo-Json -Depth 7
exit $probeRunExit
