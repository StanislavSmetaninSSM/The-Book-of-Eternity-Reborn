[CmdletBinding()]
param(
    [ValidateSet(
        "Fast",
        "Focused",
        "FullValidation",
        "RegressionIntegration",
        "ProcessIntegration",
        "E2E",
        "Complete",
        "PreMerge"
    )]
    [string]$Lane = "Fast",

    [string]$Filter,

    [ValidateRange(0, 120)]
    [int]$TimeoutMinutes = 0,

    [ValidateRange(1, 8)]
    [Alias("GuardianParallelism")]
    [int]$Parallelism = 4,

    [switch]$NoBuild,

    [switch]$PlanOnly,

    [ValidateSet(
        "None",
        "NpmStartup",
        "ResultDirectory",
        "TrxSummary"
    )]
    [Parameter(DontShow)]
    [string]$SelfTest = "None",

    [Parameter(DontShow)]
    [string]$SelfTestTrxDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$FastParallelismLimit = 2
$PreMergeParallelism = 4
$PreMergeFastParallelismLimit = 2
$ComposedSmallClassBinCount = 4
$LargeClassCaseTarget = 120

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "scripts/test-csharp.ps1 requires PowerShell 7 or newer."
}

$laneDefinitions = @{
    Fast = @{
        Project = "Fast"
        Filter = $null
        TimeoutMinutes = 5
    }
    Focused = @{
        Project = "Fast"
        Filter = $null
        TimeoutMinutes = 5
    }
    FullValidation = @{
        Project = "Integration"
        Filter = "Category=FullValidation"
        TimeoutMinutes = 15
    }
    RegressionIntegration = @{
        Project = "Integration"
        Filter = "Category=RegressionIntegration"
        TimeoutMinutes = 15
    }
    ProcessIntegration = @{
        Project = "Integration"
        Filter = "Category=ProcessIntegration"
        TimeoutMinutes = 15
    }
    E2E = @{
        Project = "Integration"
        Filter = "Category=E2E"
        TimeoutMinutes = 15
    }
    PreMerge = @{
        Project = "Both"
        Filter = $null
        TimeoutMinutes = 15
    }
}

$effectiveLane = if ($Lane -eq "Complete") { "PreMerge" } else { $Lane }
$laneDefinition = $laneDefinitions[$effectiveLane]
$effectiveTimeoutMinutes = if ($TimeoutMinutes -gt 0) {
    $TimeoutMinutes
}
else {
    [int]$laneDefinition.TimeoutMinutes
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$fastTestProject = Join-Path $repoRoot "BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj"
$integrationTestProject = Join-Path $repoRoot "BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj"

function New-UniqueResultDirectory {
    $resultRoot = Join-Path $repoRoot "TestResults\test-lanes"
    [void][System.IO.Directory]::CreateDirectory($resultRoot)

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        $runStamp = Get-Date -Format "yyyyMMdd-HHmmss-fff"
        $uniqueName = "{0}-{1}-{2}-{3}" -f @(
            $runStamp,
            $PID,
            [Guid]::NewGuid().ToString("N"),
            $Lane.ToLowerInvariant()
        )
        $candidate = Join-Path $resultRoot $uniqueName
        try {
            $created = New-Item `
                -ItemType Directory `
                -Path $candidate `
                -ErrorAction Stop
            return $created.FullName
        }
        catch [System.IO.IOException] {
            if ($attempt -eq 5) {
                throw "Could not create a unique result directory after $attempt attempts."
            }
        }
    }
}

$resultDirectory = New-UniqueResultDirectory
$logPath = Join-Path $resultDirectory "dotnet-test.log"

Set-Content -LiteralPath $logPath -Value @(
    "RequestedLane: $Lane"
    "EffectiveLane: $effectiveLane"
    "Filter: <pending validation>"
    "TimeoutMinutes: $effectiveTimeoutMinutes"
    "StartedUtc: $([DateTime]::UtcNow.ToString("O"))"
)

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$deadlineUtc = [DateTime]::UtcNow.AddMinutes($effectiveTimeoutMinutes)
$allRuns = [System.Collections.Generic.List[object]]::new()
$timedOut = $false
$cleanupSucceeded = $true
$exitCode = 0
$failureMessage = $null
$laneFilter = $null
$trxSummaryOverride = $null

function Get-ProjectDisplayPath {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath
    )

    return [System.IO.Path]::GetRelativePath($repoRoot, $ProjectPath).
        Replace([System.IO.Path]::DirectorySeparatorChar, '/')
}

function Resolve-NpmCommandPath {
    $npmCommands = @(
        Get-Command -Name "npm.cmd" -CommandType Application -ErrorAction Stop
    )
    foreach ($npmCommand in $npmCommands) {
        $candidate = $npmCommand.Path
        if ([System.IO.Path]::IsPathFullyQualified($candidate) -and
            (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw "Could not resolve npm.cmd to an absolute application path."
}

function Start-OwnedProcess {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [string]$FileName = "dotnet",

        [switch]$Quiet
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    foreach ($argument in $Arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        $process.Dispose()
        throw "Failed to start '$FileName' for owned process '$Name'."
    }

    Add-Content -LiteralPath $logPath -Value (
        "Owned process '$Name': FileName=$FileName; " +
        "UseShellExecute=$($startInfo.UseShellExecute); " +
        "CreateNoWindow=$($startInfo.CreateNoWindow); " +
        "RedirectStandardOutput=$($startInfo.RedirectStandardOutput); " +
        "RedirectStandardError=$($startInfo.RedirectStandardError)")

    $run = [pscustomobject]@{
        Name = $Name
        FileName = $FileName
        Process = $process
        StandardOutput = $process.StandardOutput.ReadToEndAsync()
        StandardError = $process.StandardError.ReadToEndAsync()
        Finalized = $false
        ExitCode = $null
        StandardOutputText = $null
        StandardErrorText = $null
        Quiet = $Quiet.IsPresent
    }
    [void]$allRuns.Add($run)
    return $run
}

function Complete-OwnedProcess {
    param(
        [Parameter(Mandatory)]
        [object]$Run
    )

    if ($Run.Finalized) {
        return
    }

    $Run.Process.WaitForExit()
    $standardOutput = $Run.StandardOutput.GetAwaiter().GetResult()
    $standardError = $Run.StandardError.GetAwaiter().GetResult()
    $Run.ExitCode = $Run.Process.ExitCode
    $Run.StandardOutputText = $standardOutput
    $Run.StandardErrorText = $standardError
    $Run.Finalized = $true

    Add-Content -LiteralPath $logPath -Value @(
        ""
        "===== $($Run.Name): stdout ====="
        $standardOutput
        "===== $($Run.Name): stderr ====="
        $standardError
        "===== $($Run.Name): exit $($Run.ExitCode) ====="
    )

    if (-not $Run.Quiet -and -not [string]::IsNullOrWhiteSpace($standardOutput)) {
        Write-Host $standardOutput.TrimEnd()
    }
    if (-not $Run.Quiet -and -not [string]::IsNullOrWhiteSpace($standardError)) {
        [Console]::Error.WriteLine($standardError.TrimEnd())
    }
}

function Stop-OwnedProcess {
    param(
        [Parameter(Mandatory)]
        [object]$Run
    )

    try {
        if (-not $Run.Process.HasExited) {
            $Run.Process.Kill($true)
            if (-not $Run.Process.WaitForExit(10000)) {
                return $false
            }
        }
        return $Run.Process.HasExited
    }
    catch {
        Add-Content -LiteralPath $logPath -Value (
            "Cleanup error for $($Run.Name): $($_.Exception.Message)")
        return $false
    }
}

function Wait-ForOwnedProcess {
    param(
        [Parameter(Mandatory)]
        [object]$Run
    )

    while (-not $Run.Process.HasExited) {
        if ([DateTime]::UtcNow -ge $deadlineUtc) {
            return $false
        }
        [void]$Run.Process.WaitForExit(250)
    }

    Complete-OwnedProcess -Run $Run
    return $true
}

function Invoke-OwnedPhase {
    param(
        [Parameter(Mandatory)]
        [object]$Run,

        [Parameter(Mandatory)]
        [string]$TimeoutMessage,

        [Parameter(Mandatory)]
        [string]$FailureDescription
    )

    if (-not (Wait-ForOwnedProcess -Run $Run)) {
        $script:timedOut = $true
        throw $TimeoutMessage
    }
    if ($Run.ExitCode -ne 0) {
        $script:exitCode = $Run.ExitCode
        throw "$FailureDescription failed with exit code $($Run.ExitCode)."
    }
}

function Get-GuardianTestMethods {
    param(
        [Parameter(Mandatory)]
        [string]$SourceRoot,

        [Parameter(Mandatory)]
        [string[]]$SourceFiles
    )

    $methods = [System.Collections.Generic.List[string]]::new()
    foreach ($sourceFile in $SourceFiles) {
        $path = Join-Path $SourceRoot $sourceFile
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Guardian shard source not found: $path"
        }

        $pendingTestAttribute = $false
        foreach ($line in Get-Content -LiteralPath $path) {
            if ($line -match '^\s*\[(Fact|Theory)(Attribute)?(\(|\])') {
                $pendingTestAttribute = $true
                continue
            }

            if (-not $pendingTestAttribute) {
                continue
            }

            if ($line -match '^\s*$' -or $line -match '^\s*\[') {
                continue
            }

            if ($line -match '^\s*public\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|void)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(') {
                [void]$methods.Add($Matches.name)
                $pendingTestAttribute = $false
                continue
            }

            throw "Could not parse guardian test declaration after Fact/Theory in ${sourceFile}: $line"
        }
    }

    return @($methods)
}

function New-TestArguments {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [Parameter(Mandatory)]
        [string]$TrxFileName,

        [AllowNull()]
        [string]$TestFilter
    )

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($argument in @(
        "test",
        $ProjectPath,
        "--no-build",
        "--no-restore",
        "--logger",
        "trx;LogFileName=$TrxFileName",
        "--results-directory",
        $resultDirectory,
        "--verbosity",
        "minimal"
    )) {
        [void]$arguments.Add($argument)
    }

    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        [void]$arguments.Add("--filter")
        [void]$arguments.Add($TestFilter)
    }

    return @($arguments)
}

function Get-DiscoveredTestCases {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [Parameter(Mandatory)]
        [string]$SelectionName,

        [AllowNull()]
        [string]$TestFilter
    )

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($argument in @(
        "test",
        $ProjectPath,
        "--no-build",
        "--no-restore",
        "--list-tests",
        "--verbosity",
        "quiet"
    )) {
        [void]$arguments.Add($argument)
    }
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        [void]$arguments.Add("--filter")
        [void]$arguments.Add($TestFilter)
    }

    $discoveryRun = Start-OwnedProcess `
        -Name "$SelectionName-discovery" `
        -Arguments @($arguments) `
        -Quiet
    Invoke-OwnedPhase `
        -Run $discoveryRun `
        -TimeoutMessage "Test discovery exceeded the lane deadline." `
        -FailureDescription "Test discovery for '$SelectionName'"

    $testCases = [System.Collections.Generic.List[object]]::new()
    foreach ($line in $discoveryRun.StandardOutputText -split "\r?\n") {
        $displayName = $line.Trim()
        if (-not $displayName.StartsWith(
            "BookOfEternityClient.Tests.",
            [StringComparison]::Ordinal)) {
            continue
        }

        $methodName = [regex]::Replace($displayName, '\(.*$', '')
        $lastSeparator = $methodName.LastIndexOf('.')
        if ($lastSeparator -le 0) {
            throw "Could not derive a test class from discovered name: $displayName"
        }

        [void]$testCases.Add([pscustomobject]@{
            ClassName = $methodName.Substring(0, $lastSeparator)
            MethodName = $methodName
            DisplayName = $displayName
        })
    }

    if ($testCases.Count -eq 0) {
        throw "Selection '$SelectionName' discovery returned no test cases."
    }
    return @($testCases)
}

function New-BalancedBins {
    param(
        [Parameter(Mandatory)]
        [object[]]$Items,

        [Parameter(Mandatory)]
        [ValidateRange(1, 1000)]
        [int]$BinCount
    )

    $bins = for ($index = 0; $index -lt $BinCount; $index++) {
        [pscustomobject]@{
            Weight = 0
            Items = [System.Collections.Generic.List[object]]::new()
        }
    }

    foreach ($item in $Items | Sort-Object Weight -Descending) {
        $bin = $bins | Sort-Object Weight | Select-Object -First 1
        [void]$bin.Items.Add($item)
        $bin.Weight += [int]$item.Weight
    }

    return @($bins | Where-Object { $_.Items.Count -gt 0 })
}

function Join-TestFilter {
    param(
        [AllowNull()]
        [string]$SelectionFilter,

        [AllowNull()]
        [string]$CategoryFilter
    )

    if ([string]::IsNullOrWhiteSpace($SelectionFilter)) {
        return $CategoryFilter
    }
    if ([string]::IsNullOrWhiteSpace($CategoryFilter)) {
        return $SelectionFilter
    }
    return "($SelectionFilter)&($CategoryFilter)"
}

function New-RunDescriptor {
    param(
        [Parameter(Mandatory)]
        [string]$Phase,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [AllowNull()]
        [string]$TestFilter,

        [Parameter(Mandatory)]
        [string]$TrxFileName,

        [Parameter(Mandatory)]
        [int]$EstimatedCases,

        [Parameter(Mandatory)]
        [int]$EstimatedCost
    )

    return [pscustomobject]@{
        Phase = $Phase
        Name = $Name
        Project = Get-ProjectDisplayPath -ProjectPath $ProjectPath
        ProjectPath = $ProjectPath
        Filter = $TestFilter
        EstimatedCases = $EstimatedCases
        EstimatedCost = $EstimatedCost
        Arguments = New-TestArguments `
            -ProjectPath $ProjectPath `
            -TrxFileName $TrxFileName `
            -TestFilter $TestFilter
    }
}

function New-SelectionRuns {
    param(
        [Parameter(Mandatory)]
        [object]$Selection,

        [Parameter(Mandatory)]
        [string]$Phase,

        [switch]$Balanced
    )

    $testCases = @(
        Get-DiscoveredTestCases `
            -ProjectPath $Selection.ProjectPath `
            -SelectionName $Selection.Name `
            -TestFilter $Selection.Filter
    )
    if (-not $Balanced) {
        return @(
            New-RunDescriptor `
                -Phase $Phase `
                -Name $Selection.Name `
                -ProjectPath $Selection.ProjectPath `
                -TestFilter $Selection.Filter `
                -TrxFileName "$($Selection.Name.ToLowerInvariant())-test-results.trx" `
                -EstimatedCases $testCases.Count `
                -EstimatedCost $testCases.Count
        )
    }

    $descriptors = [System.Collections.Generic.List[object]]::new()
    $guardianClass = "BookOfEternityClient.Tests.GuardianSystemRegressionTests"
    $guardianCases = @(
        $testCases |
            Where-Object ClassName -eq "BookOfEternityClient.Tests.GuardianSystemRegressionTests"
    )
    $baseCases = @($testCases | Where-Object ClassName -ne $guardianClass)
    $baseClassGroups = @($baseCases | Group-Object ClassName)
    $runIndex = 0

    foreach ($classGroup in $baseClassGroups | Where-Object Count -gt $LargeClassCaseTarget) {
        $methodItems = @(
            $classGroup.Group |
                Group-Object MethodName |
                ForEach-Object {
                    [pscustomobject]@{
                        Weight = $_.Count
                        Selection = "FullyQualifiedName=$($_.Name)"
                    }
                }
        )
        $binCount = [Math]::Ceiling($classGroup.Count / $LargeClassCaseTarget)
        foreach ($bin in @(New-BalancedBins -Items $methodItems -BinCount $binCount)) {
            $runIndex++
            $selectionFilter = ($bin.Items | ForEach-Object Selection) -join "|"
            $testFilter = Join-TestFilter `
                -SelectionFilter $selectionFilter `
                -CategoryFilter $Selection.Filter
            [void]$descriptors.Add((
                New-RunDescriptor `
                    -Phase $Phase `
                    -Name "$($Selection.Name)-Base-$($classGroup.Name.Split('.')[-1])-$($runIndex.ToString('D2'))" `
                    -ProjectPath $Selection.ProjectPath `
                    -TestFilter $testFilter `
                    -TrxFileName "$($Selection.Name.ToLowerInvariant())-base-$($runIndex.ToString('D2')).trx" `
                    -EstimatedCases $bin.Weight `
                    -EstimatedCost $bin.Weight
            ))
        }
    }

    $smallClassItems = @(
        $baseClassGroups |
            Where-Object Count -le $LargeClassCaseTarget |
            ForEach-Object {
                [pscustomobject]@{
                    Weight = $_.Count
                    Selection = "FullyQualifiedName~$($_.Name)."
                }
            }
    )
    $smallClassBinCount = if ($effectiveLane -eq "RegressionIntegration") {
        1
    }
    else {
        $ComposedSmallClassBinCount
    }
    foreach ($bin in @(
        New-BalancedBins -Items $smallClassItems -BinCount $smallClassBinCount
    )) {
        $runIndex++
        $selectionFilter = ($bin.Items | ForEach-Object Selection) -join "|"
        $testFilter = Join-TestFilter `
            -SelectionFilter $selectionFilter `
            -CategoryFilter $Selection.Filter
        [void]$descriptors.Add((
            New-RunDescriptor `
                -Phase $Phase `
                -Name "$($Selection.Name)-Base-mixed-$($runIndex.ToString('D2'))" `
                -ProjectPath $Selection.ProjectPath `
                -TestFilter $testFilter `
                -TrxFileName "$($Selection.Name.ToLowerInvariant())-base-$($runIndex.ToString('D2')).trx" `
                -EstimatedCases $bin.Weight `
                -EstimatedCost $bin.Weight
        ))
    }

    if ($guardianCases.Count -gt 0) {
        $guardianSourceRoot = Split-Path -Parent $Selection.ProjectPath
        $guardianShards = [ordered]@{
            AcceptedAuthority = @("GuardianSystemRegressionTests.AcceptedAuthority.cs")
            ActorBrain = @("GuardianSystemRegressionTests.ActorBrain.cs")
            Lifecycle = @(
                "GuardianSystemRegressionTests.IdleValidation.cs",
                "GuardianSystemRegressionTests.LifecycleSnapshots.cs"
            )
            MortalFactPersistence = @("MortalFactPersistenceValidationTests.cs")
            PowerJournalOfferings = @("GuardianSystemRegressionTests.PowerJournalOfferings.cs")
            ProjectsPower = @("GuardianSystemRegressionTests.ProjectsPower.cs")
            QuestProgress = @("GuardianSystemRegressionTests.QuestProgress.cs")
            RivalResidents = @("GuardianSystemRegressionTests.RivalResidents.cs")
            TradeOfferingResonance = @("GuardianSystemRegressionTests.TradeOfferingResonance.cs")
        }

        $guardianCaseGroups = @($guardianCases | Group-Object MethodName)
        $guardianCaseCounts = [System.Collections.Generic.Dictionary[string, int]]::new(
            [StringComparer]::Ordinal)
        foreach ($methodGroup in $guardianCaseGroups) {
            $guardianCaseCounts.Add($methodGroup.Name, $methodGroup.Count)
        }

        $listedFiles = [System.Collections.Generic.HashSet[string]]::new(
            [StringComparer]::Ordinal)
        $sourceMethods = [System.Collections.Generic.HashSet[string]]::new(
            [StringComparer]::Ordinal)
        $selectedGuardianMethods = [System.Collections.Generic.HashSet[string]]::new(
            [StringComparer]::Ordinal)
        $guardianRunIndex = 0

        foreach ($shard in $guardianShards.GetEnumerator()) {
            foreach ($sourceFile in $shard.Value) {
                if (-not $listedFiles.Add($sourceFile)) {
                    throw "Guardian shard source is listed more than once: $sourceFile"
                }
            }

            $methodNames = @(
                Get-GuardianTestMethods `
                    -SourceRoot $guardianSourceRoot `
                    -SourceFiles $shard.Value
            )
            if ($methodNames.Count -eq 0) {
                throw "Guardian shard '$($shard.Key)' contains no Fact/Theory methods."
            }

            $methodItems = [System.Collections.Generic.List[object]]::new()
            foreach ($methodName in $methodNames) {
                $fullyQualifiedMethod = "$guardianClass.$methodName"
                if (-not $sourceMethods.Add($fullyQualifiedMethod)) {
                    throw "Guardian test method is assigned to more than one source shard: $fullyQualifiedMethod"
                }
                if (-not $guardianCaseCounts.ContainsKey($fullyQualifiedMethod)) {
                    continue
                }
                [void]$selectedGuardianMethods.Add($fullyQualifiedMethod)
                [void]$methodItems.Add([pscustomobject]@{
                    Weight = $guardianCaseCounts[$fullyQualifiedMethod]
                    Selection = "FullyQualifiedName=$fullyQualifiedMethod"
                })
            }

            if ($methodItems.Count -eq 0) {
                continue
            }

            $domainCaseCount = ($methodItems | Measure-Object Weight -Sum).Sum
            $domainBinCount = [Math]::Max(1, [Math]::Ceiling($domainCaseCount / 60))
            $domainChunkIndex = 0
            foreach ($bin in @(
                New-BalancedBins -Items @($methodItems) -BinCount $domainBinCount
            )) {
                $runIndex++
                $guardianRunIndex++
                $domainChunkIndex++
                $selectionFilter = ($bin.Items | ForEach-Object Selection) -join "|"
                $testFilter = Join-TestFilter `
                    -SelectionFilter $selectionFilter `
                    -CategoryFilter $Selection.Filter
                [void]$descriptors.Add((
                    New-RunDescriptor `
                        -Phase $Phase `
                        -Name "$($Selection.Name)-Guardian-$($shard.Key)-$($domainChunkIndex.ToString('D2'))" `
                        -ProjectPath $Selection.ProjectPath `
                        -TestFilter $testFilter `
                        -TrxFileName "$($Selection.Name.ToLowerInvariant())-guardian-$($guardianRunIndex.ToString('D2')).trx" `
                        -EstimatedCases $bin.Weight `
                        -EstimatedCost ($bin.Weight * 4)
                ))
            }
        }

        $unassignedGuardianMethods = @(
            $guardianCaseGroups |
                Where-Object { -not $selectedGuardianMethods.Contains($_.Name) } |
                Select-Object -ExpandProperty Name
        )
        if ($unassignedGuardianMethods.Count -gt 0) {
            throw "Discovered Guardian methods are not assigned to a shard: $($unassignedGuardianMethods -join ', ')"
        }

        $unlistedTestSources = @(
            Get-ChildItem -LiteralPath $guardianSourceRoot -Filter "*.cs" |
                Where-Object {
                    $source = Get-Content -LiteralPath $_.FullName -Raw
                    $source -match 'partial\s+class\s+GuardianSystemRegressionTests' -and
                    $source -match '(?m)^\s*\[(Fact|Theory)(Attribute)?(\(|\])' -and
                    -not $listedFiles.Contains($_.Name)
                } |
                Select-Object -ExpandProperty Name
        )
        if ($unlistedTestSources.Count -gt 0) {
            throw "Guardian test source is not assigned to a shard: $($unlistedTestSources -join ', ')"
        }
    }

    return @(
        $descriptors |
            Sort-Object `
                @{ Expression = "EstimatedCost"; Descending = $true },
                @{ Expression = "Name"; Descending = $false }
    )
}

function New-TestRuns {
    if ($effectiveLane -eq "PreMerge") {
        $preMergeParallelSelections = @(
            [pscustomobject]@{
                Name = "Fast"
                ProjectPath = $fastTestProject
                Filter = $null
            },
            [pscustomobject]@{
                Name = "Integration"
                ProjectPath = $integrationTestProject
                Filter = "Category!=ProcessIntegration&Category!=E2E"
            }
        )
        $preMergeExclusiveSelections = @(
            [pscustomobject]@{
                Name = "ProcessIntegration"
                ProjectPath = $integrationTestProject
                Filter = "Category=ProcessIntegration&Category!=E2E"
            },
            [pscustomobject]@{
                Name = "E2E"
                ProjectPath = $integrationTestProject
                Filter = "Category=E2E"
            }
        )

        $preMergeRuns = [System.Collections.Generic.List[object]]::new()
        foreach ($selection in $preMergeParallelSelections) {
            foreach ($descriptor in @(
                New-SelectionRuns `
                    -Selection $selection `
                    -Phase "Parallel" `
                    -Balanced
            )) {
                [void]$preMergeRuns.Add($descriptor)
            }
        }
        foreach ($selection in $preMergeExclusiveSelections) {
            foreach ($descriptor in @(
                New-SelectionRuns `
                    -Selection $selection `
                    -Phase $selection.Name
            )) {
                [void]$preMergeRuns.Add($descriptor)
            }
        }
        return @($preMergeRuns)
    }

    $projectPath = if ($laneDefinition.Project -eq "Fast") {
        $fastTestProject
    }
    else {
        $integrationTestProject
    }
    $selection = [pscustomobject]@{
        Name = $effectiveLane
        ProjectPath = $projectPath
        Filter = $laneFilter
    }
    $balanced = $effectiveLane -in @(
        "Fast",
        "FullValidation",
        "RegressionIntegration"
    )
    return @(
        New-SelectionRuns `
            -Selection $selection `
            -Phase $effectiveLane `
            -Balanced:$balanced
    )
}

function Invoke-DescriptorBatch {
    param(
        [Parameter(Mandatory)]
        [object[]]$Descriptors,

        [Parameter(Mandatory)]
        [ValidateRange(1, 8)]
        [int]$MaximumParallelism,

        [ValidateRange(0, 8)]
        [int]$MaximumFastParallelism = 0
    )

    $pending = [System.Collections.Generic.List[object]]::new()
    foreach ($descriptor in $Descriptors) {
        [void]$pending.Add($descriptor)
    }
    $active = [System.Collections.Generic.List[object]]::new()

    while ($pending.Count -gt 0 -or $active.Count -gt 0) {
        if ([DateTime]::UtcNow -ge $deadlineUtc) {
            $script:timedOut = $true
            throw "Lane '$Lane' exceeded its $effectiveTimeoutMinutes minute timeout."
        }

        while ($pending.Count -gt 0 -and $active.Count -lt $MaximumParallelism) {
            $activeFastCount = @(
                $active |
                    Where-Object {
                        [StringComparer]::OrdinalIgnoreCase.Equals(
                            $_.Descriptor.ProjectPath,
                            $fastTestProject)
                    }
            ).Count
            $descriptor = $pending |
                Where-Object {
                    $MaximumFastParallelism -eq 0 -or
                    -not [StringComparer]::OrdinalIgnoreCase.Equals(
                        $_.ProjectPath,
                        $fastTestProject) -or
                    $activeFastCount -lt $MaximumFastParallelism
                } |
                Select-Object -First 1
            if ($null -eq $descriptor) {
                break
            }

            [void]$pending.Remove($descriptor)
            $run = Start-OwnedProcess `
                -Name $descriptor.Name `
                -Arguments $descriptor.Arguments
            [void]$active.Add([pscustomobject]@{
                Descriptor = $descriptor
                Run = $run
            })
        }

        $completed = @($active | Where-Object { $_.Run.Process.HasExited })
        if ($completed.Count -eq 0) {
            Start-Sleep -Milliseconds 200
            continue
        }

        foreach ($entry in $completed) {
            Complete-OwnedProcess -Run $entry.Run
            [void]$active.Remove($entry)
            if ($entry.Run.ExitCode -ne 0) {
                $script:exitCode = $entry.Run.ExitCode
                throw "Test run '$($entry.Run.Name)' failed with exit code $($entry.Run.ExitCode)."
            }
        }
    }
}

function Get-TrxSummary {
    param(
        [string]$TrxDirectory = $resultDirectory
    )

    $counters = @{
        Total = 0
        Executed = 0
        Passed = 0
        Failed = 0
    }
    $testOccurrences = [System.Collections.Generic.List[object]]::new()
    $parseErrors = [System.Collections.Generic.List[string]]::new()

    foreach ($trxFile in @(
        Get-ChildItem -LiteralPath $TrxDirectory -Filter "*.trx" -File
    )) {
        try {
            [xml]$trx = Get-Content -LiteralPath $trxFile.FullName -Raw
            $trxCounters = $trx.SelectSingleNode("//*[local-name()='Counters']")
            if ($null -ne $trxCounters) {
                foreach ($property in @("Total", "Executed", "Passed", "Failed")) {
                    $attributeName = $property.ToLowerInvariant()
                    $counters[$property] += [int]$trxCounters.GetAttribute($attributeName)
                }
            }

            $storageByTestId = [System.Collections.Generic.Dictionary[string, string]]::new(
                [StringComparer]::Ordinal)
            foreach ($unitTest in @(
                $trx.SelectNodes("//*[local-name()='UnitTest']")
            )) {
                $testId = $unitTest.GetAttribute("id")
                $storage = $unitTest.GetAttribute("storage")
                if ([string]::IsNullOrWhiteSpace($testId) -or
                    [string]::IsNullOrWhiteSpace($storage) -or
                    $storageByTestId.ContainsKey($testId)) {
                    continue
                }
                $storageByTestId.Add(
                    $testId,
                    [System.IO.Path]::GetFileName($storage).ToLowerInvariant())
            }

            $seenInTrx = [System.Collections.Generic.HashSet[string]]::new(
                [StringComparer]::Ordinal)
            foreach ($result in @(
                $trx.SelectNodes("//*[local-name()='UnitTestResult']")
            )) {
                $testId = $result.GetAttribute("testId")
                if ([string]::IsNullOrWhiteSpace($testId)) {
                    continue
                }
                $storage = if ($storageByTestId.ContainsKey($testId)) {
                    $storageByTestId[$testId]
                }
                else {
                    "<unknown-storage>"
                }
                $key = "$storage::$testId"
                if ($seenInTrx.Add($key)) {
                    [void]$testOccurrences.Add([pscustomobject]@{
                        Key = $key
                        TestId = $testId
                        TrxFile = $trxFile.Name
                    })
                }
            }
        }
        catch {
            [void]$parseErrors.Add("$($trxFile.Name): $($_.Exception.Message)")
        }
    }

    $duplicateTests = @(
        $testOccurrences |
            Group-Object Key |
            Where-Object Count -gt 1 |
            ForEach-Object { $_.Group[0] } |
            Select-Object -ExpandProperty TestId -Unique
    )
    return [pscustomobject]@{
        Total = $counters.Total
        Executed = $counters.Executed
        Passed = $counters.Passed
        Failed = $counters.Failed
        DuplicateTests = @($duplicateTests)
        ParseErrors = @($parseErrors)
    }
}

try {
    if ($Lane -eq "Focused" -and [string]::IsNullOrWhiteSpace($Filter)) {
        throw "Lane Focused requires -Filter with a VSTest filter expression."
    }
    if ($Lane -ne "Focused" -and -not [string]::IsNullOrWhiteSpace($Filter)) {
        throw "-Filter is supported only with -Lane Focused."
    }
    if ($TimeoutMinutes -gt [int]$laneDefinition.TimeoutMinutes) {
        throw "Lane '$Lane' has a hard limit of $($laneDefinition.TimeoutMinutes) minute(s)."
    }

    $laneFilter = if ($Lane -eq "Focused") { $Filter } else { $laneDefinition.Filter }
    Add-Content -LiteralPath $logPath -Value (
        "ValidatedFilter: $(if ([string]::IsNullOrWhiteSpace($laneFilter)) { "<none>" } else { $laneFilter })")

    if ($SelfTest -ne "None") {
        switch ($SelfTest) {
            "NpmStartup" {
                $npmCommandPath = Resolve-NpmCommandPath
                $npmProbe = Start-OwnedProcess `
                    -Name "Npm-startup-probe" `
                    -FileName $npmCommandPath `
                    -Arguments @("--version")
                Invoke-OwnedPhase `
                    -Run $npmProbe `
                    -TimeoutMessage "npm startup probe exceeded the lane deadline." `
                    -FailureDescription "npm startup probe"
            }
            "TrxSummary" {
                if ([string]::IsNullOrWhiteSpace($SelfTestTrxDirectory) -or
                    -not (Test-Path -LiteralPath $SelfTestTrxDirectory -PathType Container)) {
                    throw "TrxSummary self-test requires an existing -SelfTestTrxDirectory."
                }
                $trxSummaryOverride = Get-TrxSummary `
                    -TrxDirectory ([System.IO.Path]::GetFullPath($SelfTestTrxDirectory))
                if ($trxSummaryOverride.ParseErrors.Count -ne 0) {
                    $exitCode = 1
                    throw "TRX parsing failed: $($trxSummaryOverride.ParseErrors -join '; ')"
                }
                if ($effectiveLane -eq "PreMerge" -and
                    $trxSummaryOverride.DuplicateTests.Count -ne 0) {
                    $exitCode = 1
                    throw "PreMerge produced duplicate TRX test IDs: $($trxSummaryOverride.DuplicateTests -join ', ')"
                }
            }
            "ResultDirectory" {
                Add-Content -LiteralPath $logPath -Value (
                    "Result-directory self-test: $resultDirectory")
            }
        }
    }
    else {
        if (-not $PlanOnly) {
        if ($effectiveLane -in @("E2E", "PreMerge")) {
            $npmCommandPath = Resolve-NpmCommandPath
            $frontendRun = Start-OwnedProcess `
                -Name "Frontend-verify" `
                -FileName $npmCommandPath `
                -Arguments @("run", "verify", "--prefix", "BookOfEternityClient.WebFrontend")
            Invoke-OwnedPhase `
                -Run $frontendRun `
                -TimeoutMessage "Frontend verification exceeded the lane deadline." `
                -FailureDescription "Frontend verification"
        }

        if (-not $NoBuild) {
            $buildSelections = if ($laneDefinition.Project -eq "Both") {
                @(
                    [pscustomobject]@{
                        Name = "Build-Fast"
                        ProjectPath = $fastTestProject
                    },
                    [pscustomobject]@{
                        Name = "Build-Integration"
                        ProjectPath = $integrationTestProject
                    }
                )
            }
            elseif ($laneDefinition.Project -eq "Fast") {
                @(
                    [pscustomobject]@{
                        Name = "Build-Fast"
                        ProjectPath = $fastTestProject
                    }
                )
            }
            else {
                @(
                    [pscustomobject]@{
                        Name = "Build-Integration"
                        ProjectPath = $integrationTestProject
                    }
                )
            }

            foreach ($buildSelection in $buildSelections) {
                $buildRun = Start-OwnedProcess `
                    -Name $buildSelection.Name `
                    -Arguments @(
                        "build",
                        $buildSelection.ProjectPath,
                        "--no-restore",
                        "--verbosity",
                        "minimal"
                    )
                Invoke-OwnedPhase `
                    -Run $buildRun `
                    -TimeoutMessage "$($buildSelection.Name) exceeded the lane deadline." `
                    -FailureDescription $buildSelection.Name
            }
        }
    }

    $testRuns = @(New-TestRuns)
    if ($PlanOnly) {
        $planRows = @(
            $testRuns |
                Select-Object Phase, Name, Project, Filter, EstimatedCases, EstimatedCost
        )
        $planLines = [System.Collections.Generic.List[string]]::new()
        [void]$planLines.Add("PLAN-BEGIN EffectiveLane=$effectiveLane")
        foreach ($planRow in $planRows) {
            [void]$planLines.Add(
                "PLAN $($planRow | ConvertTo-Json -Compress -Depth 3)")
        }
        [void]$planLines.Add("PLAN-END EffectiveLane=$effectiveLane")
        Add-Content -LiteralPath $logPath -Value @("", "Execution plan", @($planLines))
        $planLines | Write-Host
    }
    else {
        if ($effectiveLane -eq "PreMerge") {
            $parallelRuns = @($testRuns | Where-Object Phase -eq "Parallel")
            Invoke-DescriptorBatch `
                -Descriptors $parallelRuns `
                -MaximumParallelism ([Math]::Min($Parallelism, $PreMergeParallelism)) `
                -MaximumFastParallelism $PreMergeFastParallelismLimit

            foreach ($phase in @("ProcessIntegration", "E2E")) {
                $exclusiveRuns = @($testRuns | Where-Object Phase -eq $phase)
                Invoke-DescriptorBatch `
                    -Descriptors $exclusiveRuns `
                    -MaximumParallelism 1
            }
        }
        else {
            $effectiveParallelism = if ($effectiveLane -eq "Fast") {
                [Math]::Min($Parallelism, $FastParallelismLimit)
            }
            elseif ($effectiveLane -in @(
                "FullValidation",
                "RegressionIntegration"
            )) {
                $Parallelism
            }
            else {
                1
            }
            Invoke-DescriptorBatch `
                -Descriptors $testRuns `
                -MaximumParallelism $effectiveParallelism
        }

        $runSummary = Get-TrxSummary
        if ($runSummary.ParseErrors.Count -ne 0) {
            $exitCode = 1
            throw "TRX parsing failed: $($runSummary.ParseErrors -join '; ')"
        }
        if ($runSummary.Total -eq 0) {
            $exitCode = 1
            throw "Lane '$Lane' produced no discovered test results."
        }
        if ($effectiveLane -eq "PreMerge" -and $runSummary.DuplicateTests.Count -ne 0) {
            $exitCode = 1
            throw "PreMerge produced duplicate TRX test IDs: $($runSummary.DuplicateTests -join ', ')"
        }
        if ($effectiveLane -eq "PreMerge" -and $runSummary.Total -lt 6560) {
            $exitCode = 1
            throw "PreMerge discovered $($runSummary.Total) cases; expected at least the 6,560-case baseline."
        }
    }
    }
}
catch {
    if ($exitCode -eq 0) {
        $exitCode = if ($timedOut) { 124 } else { 1 }
    }
    $failureMessage = $_.Exception.Message
}
finally {
    foreach ($run in $allRuns) {
        try {
            if (-not $run.Process.HasExited) {
                if (-not (Stop-OwnedProcess -Run $run)) {
                    $cleanupSucceeded = $false
                }
            }

            if ($run.Process.HasExited -and -not $run.Finalized) {
                Complete-OwnedProcess -Run $run
            }
        }
        catch {
            $cleanupSucceeded = $false
            Add-Content -LiteralPath $logPath -Value (
                "Finalization error for $($run.Name): $($_.Exception.Message)")
        }
        finally {
            $run.Process.Dispose()
        }
    }

    if (-not $cleanupSucceeded -and $exitCode -eq 0) {
        $exitCode = 1
        $failureMessage = "Owned process-tree cleanup did not complete."
    }
    $stopwatch.Stop()
}

$trxSummary = if ($null -ne $trxSummaryOverride) {
    $trxSummaryOverride
}
else {
    Get-TrxSummary
}
$summary = [ordered]@{
    RequestedLane = $Lane
    EffectiveLane = $effectiveLane
    TimeoutMinutes = $effectiveTimeoutMinutes
    WallTime = $stopwatch.Elapsed.ToString()
    ExitCode = $exitCode
    TimedOut = $timedOut
    OwnedTreeCleanupSucceeded = $cleanupSucceeded
    Tests = [ordered]@{
        Total = $trxSummary.Total
        Executed = $trxSummary.Executed
        Passed = $trxSummary.Passed
        Failed = $trxSummary.Failed
    }
    DuplicateTests = @($trxSummary.DuplicateTests)
}
$summary | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $resultDirectory "summary.json")

$summaryLines = @(
    ""
    "Lane result"
    "  Requested lane: $Lane"
    "  Effective lane: $effectiveLane"
    "  Filter: $(if ([string]::IsNullOrWhiteSpace($laneFilter)) { "<none>" } else { $laneFilter })"
    "  Timeout: $effectiveTimeoutMinutes minute(s)"
    "  Wall time: $($stopwatch.Elapsed)"
    "  Exit code: $exitCode"
    "  Timed out: $timedOut"
    "  Owned-tree cleanup: $(if ($cleanupSucceeded) { "complete" } else { "failed" })"
    "  Tests: total=$($trxSummary.Total), executed=$($trxSummary.Executed), passed=$($trxSummary.Passed), failed=$($trxSummary.Failed)"
    "  Duplicate test IDs: $($trxSummary.DuplicateTests.Count)"
    "  Results: $resultDirectory"
    "  Log: $logPath"
)
if (-not [string]::IsNullOrWhiteSpace($failureMessage)) {
    $summaryLines += "  Failure: $failureMessage"
}

$summaryLines | Tee-Object -FilePath $logPath -Append | Write-Host
exit $exitCode
