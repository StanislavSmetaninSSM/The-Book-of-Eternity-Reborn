# Dot-sourced by test-csharp.ps1: all workloads use its owned process registry.
$script:categoryRevision = $null
$script:catalogAudit = $null
$script:categoryResults = [Collections.Generic.List[object]]::new()
$script:adapterResults = [Collections.Generic.List[object]]::new()
$script:categoryPhaseResults = [Collections.Generic.List[object]]::new()
. (Join-Path $PSScriptRoot 'TestCategoryAdapters.ps1')

function Invoke-CategoryPhase {
    param([string]$Name, [string]$FileName, [string[]]$Arguments, [string]$WorkingDirectory = $repoRoot)
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $completed = $false
    try {
        $run = Start-OwnedProcess -Name $Name -FileName $FileName -Arguments $Arguments -WorkingDirectory $WorkingDirectory -Quiet
        Invoke-OwnedPhase -Run $run -TimeoutMessage "$Name exceeded the selected verification budget." -FailureDescription $Name
        $completed = $true
        return $run
    }
    finally {
        $script:categoryPhaseResults.Add([ordered]@{ Name = $Name; Seconds = $clock.Elapsed.TotalSeconds; Completed = $completed })
    }
}

function Get-CategoryRevision {
    $head = Invoke-CategoryPhase 'Revision' 'git' @('rev-parse', 'HEAD')
    $changes = Invoke-CategoryPhase 'Changed-paths' 'git' @('-c', 'core.quotepath=false', 'diff', '--name-only', 'HEAD')
    $untracked = Invoke-CategoryPhase 'New-paths' 'git' @('-c', 'core.quotepath=false', 'ls-files', '--others', '--exclude-standard')
    $records = [Collections.Generic.List[string]]::new()
    foreach ($relative in (@(($changes.StandardOutputText + "`n" + $untracked.StandardOutputText) -split '\r?\n') |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)) {
        $path = Join-Path $repoRoot $relative
        $hash = if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { '<deleted>' }
        $records.Add("${relative}:$hash")
        if ([DateTime]::UtcNow -ge $deadlineUtc) { throw 'Revision fingerprint exceeded the command budget.' }
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($records -join "`n"))
    return [ordered]@{
        Head = $head.StandardOutputText.Trim()
        WorkingTreeFingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
        ChangedFiles = $records.Count
    }
}

function Get-CategoryInventory {
    param([string[]]$Projects)
    $items = [Collections.Generic.List[object]]::new()
    foreach ($project in ($Projects | Sort-Object -Unique)) {
        if ($project -in @('unit', 'integration')) {
            $projectPath = if ($project -eq 'unit') { $fastTestProject } else { $integrationTestProject }
            if (-not $NoBuild) {
                $build = Invoke-CategoryPhase "Build-$project" 'dotnet' @('build', $projectPath, '--verbosity', 'minimal', '-p:GenerateDocumentationFile=true', '-p:NoWarn=1591')
                ($build.StandardOutputText + $build.StandardErrorText) | Set-Content -LiteralPath (Join-Path $resultDirectory "Build-$project.log")
            }
            $cases = @(Get-DiscoveredTestCases -ProjectPath $projectPath -SelectionName $project -TestFilter $null)
            foreach ($case in $cases) {
                $items.Add([pscustomobject]@{ Project = $project; Name = $case.MethodName; CaseName = $case.DisplayName })
            }
        }
        elseif ($project -eq 'frontend') {
            foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repoRoot 'BookOfEternityClient.WebFrontend/test') -Recurse -File |
                Where-Object Name -Match '\.test\.tsx?$') {
                $name = [IO.Path]::GetRelativePath($repoRoot, $file.FullName).Replace('\', '/')
                Assert-TestCategoryFrontendIsolation -Source (Get-Content -LiteralPath $file.FullName -Raw) -Path $name
                $items.Add([pscustomobject]@{ Project = $project; Name = $name; CaseName = $name })
            }
        }
        elseif ($project -eq 'powershell') {
            foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repoRoot 'scripts/tests') -File -Filter '*.tests.ps1') {
                $name = [IO.Path]::GetRelativePath($repoRoot, $file.FullName).Replace('\', '/')
                $items.Add([pscustomobject]@{ Project = $project; Name = $name; CaseName = $name })
            }
        }
    }
    return @($items)
}

function New-CategoryDescriptors {
    param([object[]]$Selection)
    $number = 0
    # Assign an overlapping test to its first selected category once. Its merged
    # requirements and all category provenance remain visible in the plan.
    foreach ($categoryGroup in ($Selection | Group-Object { $_.Categories[0] })) {
        foreach ($projectGroup in ($categoryGroup.Group | Group-Object Project, Runner, Exclusive, FrontendBuild)) {
            $members = @($projectGroup.Group)
            for ($offset = 0; $offset -lt $members.Count; $offset += 60) {
                $batch = @($members | Select-Object -Skip $offset -First 60)
                $number++
                $first = $batch[0]
                $name = '{0}-{1}-{2:D3}' -f $categoryGroup.Name, $first.Project, $number
                $expected = @($batch | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Cases = $_.Cases.Count } })
                $count = [int]($expected | Measure-Object Cases -Sum).Sum
                $path = if ($first.Project -eq 'unit') { $fastTestProject } else { $integrationTestProject }
                $descriptor = if ($first.Project -in @('unit', 'integration')) {
                    $filter = ($batch | ForEach-Object { 'FullyQualifiedName=' + $_.Name }) -join '|'
                    New-RunDescriptor -Phase 'Category' -Name $name -ProjectPath $path -TestFilter $filter `
                        -TrxFileName "$name.trx" -EstimatedCases $count -EstimatedCost $count -ExpectedMethodCounts $expected
                }
                else {
                    [pscustomobject]@{
                        Phase = 'Category'; Name = $name; Project = $first.Project; ProjectPath = $null
                        Filter = $null; TrxFileName = "$name.trx"; EstimatedCases = $count
                        EstimatedCost = $count; ExpectedMethodCounts = $expected; Arguments = @()
                    }
                }
                $descriptor | Add-Member NoteProperty Category $categoryGroup.Name
                $descriptor | Add-Member NoteProperty Categories @($batch.Categories | ForEach-Object { $_ } | Sort-Object -Unique)
                $descriptor | Add-Member NoteProperty Members $batch
                $descriptor | Add-Member NoteProperty Adapter $first.Runner
                $descriptor | Add-Member NoteProperty Exclusive ([bool]$first.Exclusive)
                $descriptor | Add-Member NoteProperty FrontendBuild ([bool]$first.FrontendBuild)
                $descriptor | Add-Member NoteProperty BudgetMinutes ([int]($batch | Measure-Object TimeoutMinutes -Minimum).Minimum)
                $descriptor
            }
        }
    }
}

function Assert-CategoryResultsComplete {
    $result = Get-TrxSummary
    if ($result.ParseErrors.Count -ne 0 -or $result.DuplicateTests.Count -ne 0) {
        throw "Invalid category reports: $($result.ParseErrors -join '; '); duplicate IDs: $($result.DuplicateTests -join ', ')."
    }
    $underfilled = @(Get-UnderfilledDescriptorNames -Descriptors $script:testRuns -TrxSummary $result)
    if ($underfilled.Count -ne 0 -or $result.Total -le 0 -or $result.Executed -ne $result.Total -or
        $result.Passed -ne $result.Total -or $result.Failed -ne 0) {
        throw "Incomplete or unsuccessful selection: total=$($result.Total), passed=$($result.Passed), executed=$($result.Executed); missing: $($underfilled -join ', ')."
    }
    foreach ($descriptor in $script:testRuns) {
        $expected = @($descriptor.ExpectedMethodCounts.Name)
        $actual = $result.MethodCasesByTrxFile[$descriptor.TrxFileName]
        foreach ($method in $actual.Keys) {
            if ($method -cnotin $expected) { throw "Unplanned test method '$method' in '$($descriptor.Name)'." }
        }
    }
}

function Invoke-TestCategoryWork {
    $script:categoryRevision = Get-CategoryRevision
    $phaseClock = [Diagnostics.Stopwatch]::StartNew()
    $projects = if ($ValidateCatalog) { @('unit', 'integration', 'frontend', 'powershell') } else {
        @($selectedCategories.selectors | ForEach-Object project | Sort-Object -Unique)
    }
    $inventory = @(Get-CategoryInventory $projects)
    if ($ValidateCatalog) {
        $script:catalogAudit = Test-TestCategoryInventory -Catalog $catalog -Inventory $inventory
        $script:catalogAudit | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $resultDirectory 'catalog-audit.json')
        if (-not $script:catalogAudit.Valid) {
            throw "Catalog inventory is incomplete: $($script:catalogAudit.Unmapped.Count) unmapped methods/files; $($script:catalogAudit.StaleSelectors.Count) stale selectors. See catalog-audit.json."
        }
        Write-Host "CATALOG-VALID categories=$($script:catalogAudit.Categories) methods/files=$($script:catalogAudit.Methods); no tests executed."
        return
    }
    $selection = @(Resolve-TestCategorySelection -Catalog $catalog -Category $Category -Inventory $inventory)
    $script:testRuns = @(New-CategoryDescriptors $selection)
    $plan = [ordered]@{
        Categories = @($Category); Reasons = $selectionReasons; Revision = $script:categoryRevision
        PreparationSeconds = $phaseClock.Elapsed.TotalSeconds
        Descriptors = @($script:testRuns | Select-Object Name, Project, Category, Categories, Filter, Adapter, Exclusive, FrontendBuild, BudgetMinutes, EstimatedCases, ExpectedMethodCounts)
    }
    $plan | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $resultDirectory 'plan.json')
    foreach ($row in $plan.Descriptors) { Write-Host "PLAN $($row | ConvertTo-Json -Compress -Depth 8)" }
    if ($PlanOnly) { return }
    if (@($selection | Where-Object { $_.Project -eq 'frontend' -or $_.FrontendBuild }).Count -gt 0) {
        Initialize-CategoryFrontend -Build (@($selection | Where-Object FrontendBuild).Count -gt 0)
    }
    $overallDeadline = $deadlineUtc
    foreach ($group in ($script:testRuns | Group-Object Category)) {
        $clock = [Diagnostics.Stopwatch]::StartNew()
        $limit = [int]($group.Group | Measure-Object BudgetMinutes -Minimum).Minimum
        $script:deadlineUtc = @($overallDeadline, [DateTime]::UtcNow.AddMinutes($limit)) | Sort-Object | Select-Object -First 1
        $completed = $false
        try {
            # Categories are small semantic selections. Keep category deadlines
            # meaningful and resource isolation explicit; do not overlap categories.
            $csharp = @($group.Group | Where-Object { $_.Adapter -in @('unit', 'integration') })
            if ($csharp.Count -gt 0) {
                $capacity = if (@($csharp | Where-Object Exclusive).Count -gt 0) { 1 } else { $Parallelism }
                Invoke-DescriptorBatch -Descriptors $csharp -MaximumParallelism $capacity -MaximumFastParallelism 2
            }
            foreach ($descriptor in @($group.Group | Where-Object { $_.Adapter -notin @('unit', 'integration') })) {
                Invoke-CategoryAdapter $descriptor
            }
            $completed = $true
        }
        finally {
            $script:deadlineUtc = $overallDeadline
            $script:categoryResults.Add([ordered]@{
                Category = $group.Name; Seconds = $clock.Elapsed.TotalSeconds
                BudgetMinutes = $limit; Completed = $completed
            })
        }
    }
    Assert-CategoryResultsComplete
}
