[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot)
$module = Join-Path $root 'scripts/testing/TestCategoryCatalog.psm1'
if (-not (Test-Path -LiteralPath $module)) {
    throw 'FAIL: documented category selection requires a catalog module.'
}
Import-Module $module -Force
$script:passed = 0

function Assert-That([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAIL: $Message" }
}
function Assert-Rejected([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert-That $rejected $Message
}
function Test-Case([string]$Name, [scriptblock]$Action) {
    & $Action
    $script:passed++
    Write-Host "PASS $Name"
}
function New-Category([string]$Id, [string]$Project, [string[]]$Tests) {
    return @{
        id = $Id; responsibility = 'Fixture authority'; excludes = 'Unrelated rendering'
        changeHints = @('BookOfEternityClient/Services/Fixture*.cs'); related = @()
        requirements = @{ frontendBuild = $false; exclusive = $false }
        timeoutMinutes = 2; expectedSeconds = $null
        selectors = @(@{ project = $Project; tests = $Tests })
    }
}
$temp = Join-Path ([IO.Path]::GetTempPath()) ('boe-category-contract-' + [guid]::NewGuid().ToString('N') + '.json')
function Read-Fixture($Categories) {
    @{ schemaVersion = 1; categories = @($Categories) } |
        ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $temp -Encoding utf8NoBOM
    return Read-TestCategoryCatalog -Path $temp
}
$inventory = @(
    @{ Project = 'unit'; Name = 'BookOfEternityClient.Tests.FooTests.First'; CaseName = 'First' }
    @{ Project = 'unit'; Name = 'BookOfEternityClient.Tests.FooTests.Rows'; CaseName = 'Rows(x: 1)' }
    @{ Project = 'unit'; Name = 'BookOfEternityClient.Tests.FooTests.Rows'; CaseName = 'Rows(x: 2)' }
    @{ Project = 'integration'; Name = 'BookOfEternityClient.Tests.FooTests.First'; CaseName = 'First' }
)
try {
    Test-Case 'An executor can add a new category without a runner enum' {
        $catalog = Read-Fixture @((New-Category 'new-fixture-contract' 'unit' @('BookOfEternityClient.Tests.FooTests.*')))
        $selection = @(Resolve-TestCategorySelection -Catalog $catalog -Category @('new-fixture-contract') -Inventory $inventory)
        Assert-That ($selection.Count -eq 2) 'Method union preserves both ordinary and parameterized methods.'
        Assert-That (@($selection | Where-Object Name -Like '*.Rows')[0].Cases.Count -eq 2) 'Theory rows are retained.'
    }
    Test-Case 'Cross-project method identities are distinct' {
        $first = New-Category 'unit-fixture' 'unit' @('BookOfEternityClient.Tests.FooTests.First')
        $second = New-Category 'integration-fixture' 'integration' @('BookOfEternityClient.Tests.FooTests.First')
        $selection = @(Resolve-TestCategorySelection -Catalog (Read-Fixture @($first, $second)) -Category @('unit-fixture', 'integration-fixture') -Inventory $inventory)
        Assert-That ($selection.Count -eq 2) 'Identical FQN in different assemblies must both execute.'
    }
    Test-Case 'Overlapping memberships execute once and merge stricter requirements' {
        $first = New-Category 'owner' 'unit' @('BookOfEternityClient.Tests.FooTests.First')
        $second = New-Category 'consumer' 'unit' @('BookOfEternityClient.Tests.FooTests.First')
        $second.requirements.exclusive = $true
        $second.requirements.frontendBuild = $true
        $selection = @(Resolve-TestCategorySelection -Catalog (Read-Fixture @($first, $second)) -Category @('owner', 'consumer') -Inventory $inventory)
        Assert-That ($selection.Count -eq 1) 'Overlap must not schedule duplicate tests.'
        Assert-That ($selection[0].Categories.Count -eq 2 -and $selection[0].Exclusive -and $selection[0].FrontendBuild) 'Provenance and stricter requirements must survive deduplication.'
    }
    Test-Case 'Empty unknown and wildcard category selections never expand to everything' {
        $catalog = Read-Fixture @((New-Category 'owner' 'unit' @('BookOfEternityClient.Tests.FooTests.First')))
        foreach ($requested in @(@(), @('missing'), @('*'))) {
            Assert-Rejected { Resolve-TestCategorySelection -Catalog $catalog -Category $requested -Inventory $inventory } 'Missing explicit selection must fail.'
        }
    }
    Test-Case 'Class selection does not swallow a similarly named class' {
        $extra = @{ Project = 'unit'; Name = 'BookOfEternityClient.Tests.FooTestsExtra.First'; CaseName = 'First' }
        $catalog = Read-Fixture @((New-Category 'owner' 'unit' @('BookOfEternityClient.Tests.FooTests.*')))
        $selection = @(Resolve-TestCategorySelection -Catalog $catalog -Category @('owner') -Inventory @($inventory + $extra))
        Assert-That ($selection.Count -eq 2) 'A class delimiter is part of matching.'
    }
    Test-Case 'Every selector must resolve and a newly added split-class method is unmapped' {
        $catalog = Read-Fixture @((New-Category 'owner' 'unit' @('BookOfEternityClient.Tests.FooTests.First')))
        $audit = Test-TestCategoryInventory -Catalog $catalog -Inventory $inventory
        Assert-That (-not $audit.Valid -and $audit.Unmapped.Count -eq 2) 'Audit must report Rows and integration First.'
        $stale = Read-Fixture @((New-Category 'stale' 'unit' @('BookOfEternityClient.Tests.FooTests.First', 'BookOfEternityClient.Tests.FooTests.Deleted')))
        Assert-Rejected { Resolve-TestCategorySelection -Catalog $stale -Category @('stale') -Inventory $inventory } 'One matching selector cannot conceal another stale selector.'
    }
    Test-Case 'Unsafe broad selectors invalid budgets unknown relationships and duplicate IDs fail' {
        foreach ($pattern in @('*', 'BookOfEternityClient.Tests.*', 'FullyQualifiedName~Foo', '../outside.cs')) {
            Assert-Rejected { Read-Fixture @((New-Category 'owner' 'unit' @($pattern))) } 'Selectors must identify a specific class or method.'
        }
        $category = New-Category 'owner' 'unit' @('BookOfEternityClient.Tests.FooTests.First')
        $category.timeoutMinutes = 60
        Assert-Rejected { Read-Fixture @($category) } 'Hour-long category budgets are not allowed.'
        $category.timeoutMinutes = 2
        $category.related = @(@{ id = 'missing'; when = 'Shared authority changes' })
        Assert-Rejected { Read-Fixture @($category) } 'A relationship must resolve.'
        $category.related = @()
        Assert-Rejected { Read-Fixture @($category, $category) } 'Category IDs must be unique.'
    }
    Test-Case 'Frontend adapters are explicit and files stay within the test directory' {
        $category = New-Category 'ui-contract' 'frontend' @('BookOfEternityClient.WebFrontend/test/example.test.ts')
        $category.selectors[0].runner = 'node'
        $catalog = Read-Fixture @($category)
        $selection = @(Resolve-TestCategorySelection -Catalog $catalog -Category @('ui-contract') -Inventory @(@{
            Project = 'frontend'; Name = 'BookOfEternityClient.WebFrontend/test/example.test.ts'; CaseName = 'example'; Runner = 'node'
        }))
        Assert-That ($selection.Count -eq 1 -and $selection[0].Runner -eq 'node') 'Selected frontend adapter is retained.'
        $category.selectors[0].tests = @('BookOfEternityClient.WebFrontend/test/../../evil.ts')
        Assert-Rejected { Read-Fixture @($category) } 'Traversal must not select files outside the test directory.'
    }
    Test-Case 'Frontend files cannot hide aggregate test imports' {
        foreach ($source in @("import './other.test.js';", "export * from './other.test.ts';", "await import('./other.test.js');", "require('./other.test.js');")) {
            Assert-Rejected { Assert-TestCategoryFrontendIsolation -Source $source -Path 'selected.test.ts' } 'A selected file must not execute another test file.'
        }
        Assert-TestCategoryFrontendIsolation -Source "import { helper } from './helpers.js';" -Path 'selected.test.ts'
        Get-ChildItem -LiteralPath (Join-Path $root 'BookOfEternityClient.WebFrontend/test') -Recurse -File |
            Where-Object Name -Match '\.test\.tsx?$' | ForEach-Object {
                Assert-TestCategoryFrontendIsolation -Source (Get-Content -LiteralPath $_.FullName -Raw) -Path $_.Name
            }
    }
    Write-Host "CATEGORY-CONTRACT-RESULT passed=$script:passed failed=0"
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp }
}
