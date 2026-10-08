[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot)
Import-Module (Join-Path $root 'scripts/testing/TestCategoryCatalog.psm1') -Force
$script:passed = 0
$script:failed = 0
$temp = Join-Path ([IO.Path]::GetTempPath()) ('boe-ci-platform-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
function Assert-That([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-Rejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert-That $rejected 'Invalid input must fail before emitting a runnable matrix.'
}
function Test-Case([string]$Name, [scriptblock]$Action) {
    try { & $Action; $script:passed++; Write-Host "PASS $Name" }
    catch { $script:failed++; Write-Host "FAIL $Name`: $($_.Exception.Message)" }
}
function New-Category([string]$Id, [string]$Runner = '') {
    $category = @{
        id = $Id; responsibility = 'Controlled CI selection'; excludes = 'Native qualification'
        changeHints = @('scripts/testing/**'); related = @()
        requirements = @{ frontendBuild = $false; exclusive = $false }
        timeoutMinutes = 1; expectedSeconds = $null
        selectors = @(@{ project = 'powershell'; tests = @('scripts/tests/test-ci-platform.tests.ps1') })
    }
    if ($Runner) { $category.requirements.ciRunner = $Runner }
    return $category
}
function Read-Fixture($Categories) {
    $path = Join-Path $temp ([guid]::NewGuid().ToString('N') + '.json')
    @{ schemaVersion = 1; categories = @($Categories) } |
        ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding utf8NoBOM
    return Read-TestCategoryCatalog $path
}
function New-Selection([string[]]$Ids) {
    return @{ schemaVersion = 1; selections = @($Ids | ForEach-Object {
        @{ category = $_; reason = "Actual reason $_"; contracts = @('#1553', 'T052-CI-PLATFORM-SELECTION') }
    }) }
}
try {
    Test-Case 'Workflow routes explicit selection rather than fixed Windows host' {
        $workflow = Get-Content (Join-Path $root '.github/workflows/dotnet-ci.yml') -Raw
        Assert-That ($workflow -match 'scripts/plan-test-ci\.ps1' -and
            $workflow -match 'runs-on: \$\{\{ matrix\.runner \}\}') 'The production workflow still binds Linux fixtures to Windows.'
    }
    Test-Case 'Production matrix planner is available' {
        Assert-That ($null -ne (Get-Command Get-TestCategoryCiMatrix -ErrorAction SilentlyContinue)) 'CI matrix planner is missing.'
    }
    if (Get-Command Get-TestCategoryCiMatrix -ErrorAction SilentlyContinue) {
        Test-Case 'Default Windows convention is retained without a portability claim' {
            $plan = Get-TestCategoryCiMatrix (Read-Fixture @((New-Category 'default'))) (New-Selection @('default'))
            Assert-That ($plan.include.Count -eq 1 -and $plan.include[0].runner -ceq 'windows-latest') 'Default changed without qualification.'
        }
        Test-Case 'Linux-only selection produces one Linux job' {
            $plan = Get-TestCategoryCiMatrix (Read-Fixture @((New-Category 'linux' 'ubuntu-24.04'))) (New-Selection @('linux'))
            Assert-That ($plan.include.Count -eq 1 -and $plan.include[0].platform -ceq 'linux') 'Linux request was omitted or misrouted.'
        }
        Test-Case 'Mixed jobs preserve every entry reason contract and order within group' {
            $catalog = Read-Fixture @((New-Category 'l1' 'ubuntu-24.04'), (New-Category 'w1'), (New-Category 'l2' 'ubuntu-24.04'))
            $request = New-Selection @('l1', 'w1', 'l2')
            $request.selections[0].reason = "Unicode Δ`nreason"
            $plan = Get-TestCategoryCiMatrix $catalog $request
            Assert-That ($plan.include.Count -eq 2) 'Two distinct hosts required.'
            $linux = @($plan.include | Where-Object runner -CEQ 'ubuntu-24.04')[0]
            Assert-That (($linux.selection.selections.category -join ',') -ceq 'l1,l2') 'Per-group ordering changed.'
            $actual = @($plan.include | ForEach-Object { $_.selection.selections })
            Assert-That ($actual.Count -eq $request.selections.Count) 'Selection was dropped or duplicated.'
            foreach ($entry in $request.selections) {
                $copy = @($actual | Where-Object category -CEQ $entry.category)
                Assert-That ($copy.Count -eq 1 -and $copy[0].reason -ceq $entry.reason -and
                    ($copy[0].contracts -join ',') -ceq ($entry.contracts -join ',')) 'Selection provenance changed.'
            }
        }
        Test-Case 'Real S2A catalog routes native bodies to Linux and exact affected metadata to Windows' {
            $catalog = Read-TestCategoryCatalog (Join-Path $root 'tests/categories.json')
            $request = New-Selection @('gm-main-systemd-cgroup-source', 'gm-main-systemd-cgroup-connected', 'gm-main-systemd-cgroup-affected')
            $plan = Get-TestCategoryCiMatrix $catalog $request
            $linux = @($plan.include | Where-Object runner -CEQ 'ubuntu-24.04')
            $windows = @($plan.include | Where-Object runner -CEQ 'windows-latest')
            Assert-That ($linux.Count -eq 1 -and $windows.Count -eq 1 -and
                ($linux[0].selection.selections.category -join ',') -ceq 'gm-main-systemd-cgroup-source,gm-main-systemd-cgroup-connected' -and
                ($windows[0].selection.selections.category -join ',') -ceq 'gm-main-systemd-cgroup-affected') 'Real native consumers reach the wrong host.'
        }
        Test-Case 'Frontend dependencies belong only to the relevant job' {
            $ui = New-Category 'ui'; $ui.requirements.frontendBuild = $true
            $plan = Get-TestCategoryCiMatrix (Read-Fixture @((New-Category 'linux' 'ubuntu-24.04'), $ui)) (New-Selection @('linux', 'ui'))
            Assert-That (-not $plan.include[0].frontend -and $plan.include[1].frontend) 'Frontend setup leaked or was omitted.'
            $ui.requirements.frontendBuild = $false
            $ui.selectors = @(@{ project = 'frontend'; tests = @('BookOfEternityClient.WebFrontend/test/fixture.test.ts'); runner = 'node' })
            $plan = Get-TestCategoryCiMatrix (Read-Fixture @($ui)) (New-Selection @('ui'))
            Assert-That $plan.include[0].frontend 'Explicit frontend selector must prepare Node.'
        }
        foreach ($bad in @('', 'self-hosted', 'ubuntu-latest', 'Windows-latest', "ubuntu-24.04`nother=x")) {
            Test-Case "Catalog rejects runner [$bad]" {
                $category = New-Category 'invalid'; $category.requirements.ciRunner = $bad
                Assert-Rejected { Read-Fixture @($category) }
            }
        }
        $catalog = Read-Fixture @((New-Category 'known'))
        Test-Case 'Empty schema unknown and duplicate selections fail without broad fallback' {
            foreach ($request in @(@{schemaVersion=1;selections=@()}, @{schemaVersion=2;selections=@()},
                (New-Selection @('missing')), (New-Selection @('known','known')))) {
                Assert-Rejected { Get-TestCategoryCiMatrix $catalog $request }
            }
        }
        Test-Case 'Missing reasons empty contracts and nontext contracts fail' {
            foreach ($field in @('reason','contracts')) {
                $request = New-Selection @('known'); $request.selections[0].Remove($field)
                Assert-Rejected { Get-TestCategoryCiMatrix $catalog $request }
            }
            $request = New-Selection @('known'); $request.selections[0].contracts = @('')
            Assert-Rejected { Get-TestCategoryCiMatrix $catalog $request }
            $request.selections[0].contracts = @(42)
            Assert-Rejected { Get-TestCategoryCiMatrix $catalog $request }
        }
        Test-Case 'CLI emits exact compact JSON to GitHub output without executing requests' {
            $request = New-Selection @('test-ci-platform-contracts','test-ci-platform-affected')
            $request.selections[0].reason = "safe`nmatrix=unexpected Δ"
            $path = Join-Path $temp 'selection.json'; $output = Join-Path $temp 'github-output.txt'
            $request | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding utf8NoBOM
            $json = & (Join-Path $root 'scripts/plan-test-ci.ps1') -SelectionFile $path -GitHubOutput $output
            $lines = @(Get-Content -LiteralPath $output)
            Assert-That ($lines.Count -eq 1 -and $lines[0] -ceq "matrix=$json") 'Selection text injected extra workflow outputs.'
            $decoded = $json | ConvertFrom-Json -AsHashtable
            Assert-That ($decoded.include.Count -eq 2) 'CLI failed to preserve both groups.'
        }
        Test-Case 'Workflow keeps all groups artifacts warning budget and stable failure gate' {
            $workflow = Get-Content (Join-Path $root '.github/workflows/dotnet-ci.yml') -Raw
            Assert-That ($workflow -match 'fail-fast: false' -and $workflow -notmatch 'continue-on-error:') 'A failed group may cancel or conceal another selected group.'
            Assert-That ($workflow -match 'name: dotnet-test-results-\$\{\{ matrix.platform \}\}' -and
                $workflow -match 'name: browser-smoke-artifacts-\$\{\{ matrix.platform \}\}') 'Matrix artifacts collide.'
            Assert-That ($workflow -match '(?s)build-and-test:.*name: Selected category verification.*needs: \[plan, selected-tests\].*if: \$\{\{ always\(\) \}\}' -and
                $workflow -match 'BOE_PLAN_RESULT' -and $workflow -match 'BOE_TEST_RESULT' -and
                $workflow -match "-cne 'success'") 'Skipped or failed prerequisites could produce a successful required check.'
            Assert-That ($workflow -match 'check-dotnet-warning-budget.ps1 -LogPath \$_\.FullName -MaxWarnings 158' -and
                $workflow -match 'contents: read' -and $workflow -notmatch 'contents: write') 'Existing verification budget or permissions changed.'
        }
    }
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
Write-Host "CATEGORY-CONTRACT-RESULT passed=$script:passed failed=$script:failed"
if ($script:failed) { throw "$script:failed CI platform assertions failed." }
