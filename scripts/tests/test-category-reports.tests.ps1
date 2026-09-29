[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot)
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $repo 'scripts/test-csharp.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
foreach ($name in @('Get-TrxSummary', 'Get-UnderfilledDescriptorNames')) {
    $function = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $true)
    . ([scriptblock]::Create($function.Extent.Text))
}
$temp = Join-Path ([IO.Path]::GetTempPath()) ('boe-category-reports-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
$script:passed = 0
function Assert-That([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAIL: $Message" }
    $script:passed++
    Write-Host "PASS $Message"
}
function Write-Report([int]$Total, [int]$Executed, [int]$Passed) {
    @"
<TestRun><Results>
<UnitTestResult testId="rows" testName="BookOfEternityClient.Tests.Foo.Rows(x: 1)" outcome="Passed" />
<UnitTestResult testId="rows" testName="BookOfEternityClient.Tests.Foo.Rows(x: 2)" outcome="Passed" />
</Results><TestDefinitions><UnitTest id="rows" storage="unit.dll" /></TestDefinitions>
<ResultSummary><Counters total="$Total" executed="$Executed" passed="$Passed" failed="0" /></ResultSummary></TestRun>
"@ | Set-Content -LiteralPath (Join-Path $temp 'rows.trx')
}
try {
    Write-Report 2 2 2
    $summary = Get-TrxSummary -TrxDirectory $temp
    Assert-That ($summary.ParseErrors.Count -eq 0 -and $summary.Total -eq 2 -and $summary.DuplicateTests.Count -eq 0) 'Dynamic theory rows may share a test ID inside one report.'
    $descriptor = [pscustomobject]@{
        Name = 'selected'; TrxFileName = 'rows.trx'; EstimatedCases = 2
        ExpectedMethodCounts = @(
            [pscustomobject]@{Name='BookOfEternityClient.Tests.Foo.Rows'; Cases=1}
            [pscustomobject]@{Name='BookOfEternityClient.Tests.Foo.Missing'; Cases=1}
        )
    }
    Assert-That (@(Get-UnderfilledDescriptorNames -Descriptors @($descriptor) -TrxSummary $summary).Count -eq 1) 'Surplus theory rows cannot conceal a missing selected method.'
    $descriptor.ExpectedMethodCounts = @([pscustomobject]@{Name='BookOfEternityClient.Tests.Foo.Rows'; Cases=1})
    $descriptor.EstimatedCases = 1
    Assert-That (@(Get-UnderfilledDescriptorNames -Descriptors @($descriptor) -TrxSummary $summary).Count -eq 0) 'Dynamic expansion does not require strict discovery/result count equality.'
    Write-Report 999 999 999
    $invalid = Get-TrxSummary -TrxDirectory $temp
    Assert-That ($invalid.ParseErrors.Count -gt 0) 'Report counters must match physical result rows.'
    Write-Report 2 2 -1
    $invalid = Get-TrxSummary -TrxDirectory $temp
    Assert-That ($invalid.ParseErrors.Count -gt 0) 'Negative counters are invalid evidence.'
    Write-Report 2 2 2
    Copy-Item -LiteralPath (Join-Path $temp 'rows.trx') -Destination (Join-Path $temp 'duplicate.trx')
    $duplicate = Get-TrxSummary -TrxDirectory $temp
    Assert-That ($duplicate.DuplicateTests.Count -eq 1) 'The same test ID in separate descriptors is a duplicate.'
    Write-Host "CATEGORY-CONTRACT-RESULT passed=$script:passed failed=0"
}
finally {
    foreach ($name in @('rows.trx', 'duplicate.trx')) {
        $path = Join-Path $temp $name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
    Remove-Item -LiteralPath $temp
}
