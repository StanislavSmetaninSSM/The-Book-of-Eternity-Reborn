param([Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
$parseTokens = $null
$parseErrors = $null
$collectorPath = Join-Path $PSScriptRoot 'Collect.ps1'
$ast = [Management.Automation.Language.Parser]::ParseFile($collectorPath, [ref]$parseTokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Collector parse errors.' }
foreach ($name in @('Invoke-ObservedApi', 'Get-CollectorExit')) {
    $functionAst = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true) | Where-Object Name -EQ $name)
    if ($functionAst.Count -ne 1) { throw 'Expected one source helper function.' }
    . ([scriptblock]::Create($functionAst[0].Extent.Text))
}
$cases = @()
foreach ($apiFails in @($false, $true)) {
    foreach ($beforeFails in @($false, $true)) {
        foreach ($afterFails in @($false, $true)) {
            $caseState = @{ Calls = 0 }
            $result = Invoke-ObservedApi -Request {
                $caseState.Calls++
                if ($apiFails) { throw [IO.IOException]::new('fake API failure') }
            } -Writer {
                param($event)
                if (($event.Stage -eq 'request_started' -and $beforeFails) -or
                    ($event.Stage -eq 'api_outcome' -and $afterFails)) { throw [IO.IOException]::new('fake emission failure') }
            } -Started @{ Stage = 'request_started' }
            $code = Get-CollectorExit $result.ApiStatus 0 $result.ObserverErrors.Count
            if ($beforeFails) {
                if ($result.ApiOutcome -ne 'UNREACHED' -or $null -ne $result.ApiStatus -or $caseState.Calls -ne 0 -or $code -ne 2) { throw 'Entry emission failure misclassified.' }
            } else {
                $expectedStatus = if ($apiFails) { 7 } else { 0 }
                $expectedOutcome = if ($apiFails) { 'FAILED' } else { 'RETURNED' }
                $expectedExit = if ($apiFails) { 7 } elseif ($afterFails) { 2 } else { 0 }
                if ($result.ApiStatus -ne $expectedStatus -or $result.ApiOutcome -ne $expectedOutcome -or
                    $caseState.Calls -ne 1 -or $code -ne $expectedExit) { throw 'Native/API observer priority differs.' }
            }
            $cases += @{ ApiFails = $apiFails; BeforeEmitFails = $beforeFails; AfterEmitFails = $afterFails;
                ApiOutcome = $result.ApiOutcome; ApiStatus = $result.ApiStatus; FakeInvocations = $caseState.Calls;
                ObserverErrors = @($result.ObserverErrors.ToArray()); CollectorExit = $code }
        }
    }
}
$report = @{ Kind = 'L01_S01_FAKE_API_OBSERVER_PROOF'; Outcome = 'PASS'; Cases = $cases;
    SourceSha256 = (Get-FileHash -LiteralPath $collectorPath -Algorithm SHA256).Hash.ToLowerInvariant();
    Scope = 'Extracted two source functions only; eight fake callback cases, no SDK load/attach/diagnostic call or target.' }
[IO.File]::WriteAllText($OutputPath, ($report | ConvertTo-Json -Depth 15) + "`n")
[Console]::Out.WriteLine('Fake API observer proof PASS:8 cases, no real API invocation.')
