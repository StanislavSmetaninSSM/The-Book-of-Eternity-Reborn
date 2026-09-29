function Initialize-CategoryFrontend {
    param([bool]$Build)
    $script:categoryFrontendRoot = Join-Path $repoRoot 'BookOfEternityClient.WebFrontend'
    if (-not (Test-Path -LiteralPath (Join-Path $categoryFrontendRoot 'node_modules/typescript/bin/tsc'))) {
        throw 'Frontend dependencies are missing. Run npm ci --prefix BookOfEternityClient.WebFrontend, then repeat this selection.'
    }
    if ($Build) {
        $npm = Resolve-NpmCommandPath
        $null = Invoke-CategoryPhase 'Frontend-build' $npm @('run', 'build', '--prefix', $categoryFrontendRoot)
    }
}

function Write-CategoryAdapterReport {
    param($Descriptor, [object[]]$Results)
    if ($Results.Count -eq 0) { throw "Adapter '$($Descriptor.Adapter)' returned no results." }
    $settings = [Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $settings.Encoding = [Text.UTF8Encoding]::new($false)
    $writer = [Xml.XmlWriter]::Create((Join-Path $resultDirectory $Descriptor.TrxFileName), $settings)
    try {
        $writer.WriteStartElement('TestRun')
        $writer.WriteStartElement('Results')
        foreach ($result in $Results) {
            $writer.WriteStartElement('UnitTestResult')
            $writer.WriteAttributeString('testId', $result.Id)
            $writer.WriteAttributeString('testName', $result.Name)
            $writer.WriteAttributeString('outcome', 'Passed')
            $writer.WriteEndElement()
        }
        $writer.WriteEndElement()
        $writer.WriteStartElement('TestDefinitions')
        foreach ($result in $Results) {
            $writer.WriteStartElement('UnitTest')
            $writer.WriteAttributeString('id', $result.Id)
            $writer.WriteAttributeString('storage', 'category-' + $Descriptor.Adapter)
            $writer.WriteEndElement()
        }
        $writer.WriteEndElement()
        $writer.WriteStartElement('ResultSummary')
        $writer.WriteStartElement('Counters')
        foreach ($key in @('total', 'executed', 'passed')) { $writer.WriteAttributeString($key, [string]$Results.Count) }
        $writer.WriteAttributeString('failed', '0')
        $writer.WriteEndElement(); $writer.WriteEndElement(); $writer.WriteEndElement()
    }
    finally { $writer.Dispose() }
}

function New-AdapterResult {
    param([string]$File, [string]$Case)
    $identity = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes("${File}:$Case")))
    return @{ Id = $identity; Name = "$File($Case)" }
}

function Invoke-CategoryAdapter {
    param($Descriptor)
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $results = [Collections.Generic.List[object]]::new()
    if ($Descriptor.Adapter -eq 'powershell') {
        foreach ($member in $Descriptor.Members) {
            $run = Invoke-CategoryPhase $Descriptor.Name ((Get-Process -Id $PID).Path) @('-NoProfile', '-File', (Join-Path $repoRoot $member.Name))
            if ($run.StandardOutputText -notmatch 'CATEGORY-CONTRACT-RESULT passed=(\d+) failed=0' -or [int]$Matches[1] -lt 1) {
                throw "PowerShell test '$($member.Name)' did not publish its successful assertion count."
            }
            $count = [int]$Matches[1]
            for ($index = 1; $index -le $count; $index++) { $results.Add((New-AdapterResult $member.Name ([string]$index))) }
        }
    }
    elseif ($Descriptor.Adapter -eq 'vitest') {
        $outputPath = Join-Path $resultDirectory ($Descriptor.Name + '.vitest.json')
        $arguments = @((Join-Path $categoryFrontendRoot 'node_modules/vitest/vitest.mjs'), 'run', '--root', $categoryFrontendRoot)
        $arguments += @($Descriptor.Members | ForEach-Object { $_.Name.Substring('BookOfEternityClient.WebFrontend/'.Length) })
        $arguments += @('--reporter=json', '--outputFile=' + $outputPath)
        $null = Invoke-CategoryPhase $Descriptor.Name 'node' $arguments -WorkingDirectory $categoryFrontendRoot
        $report = Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
        if (-not $report.success -or $report.numTotalTests -lt 1 -or $report.numPassedTests -ne $report.numTotalTests) {
            throw "Vitest category '$($Descriptor.Category)' did not pass every selected test."
        }
        foreach ($member in $Descriptor.Members) {
            $expectedPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $member.Name))
            $fileReport = @($report.testResults | Where-Object { [IO.Path]::GetFullPath($_.name) -eq $expectedPath })
            if ($fileReport.Count -ne 1 -or @($fileReport[0].assertionResults).Count -eq 0) {
                throw "Vitest omitted selected file '$($member.Name)'."
            }
            foreach ($assertion in $fileReport[0].assertionResults) {
                if ($assertion.status -ne 'passed') { throw "Vitest assertion '$($assertion.fullName)' did not pass." }
                $results.Add((New-AdapterResult $member.Name $assertion.fullName))
            }
        }
        if ($results.Count -ne $report.numTotalTests) { throw 'Vitest executed unselected files or published inconsistent counters.' }
    }
    elseif ($Descriptor.Adapter -in @('node', 'typecheck')) {
        $compiled = Join-Path $resultDirectory ($Descriptor.Name + '-compiled')
        $configPath = Join-Path $resultDirectory ($Descriptor.Name + '.tsconfig.json')
        $config = @{
            extends = (Join-Path $categoryFrontendRoot 'tsconfig.player-facing-tests.json')
            compilerOptions = @{ outDir = $compiled; rootDir = $categoryFrontendRoot; noEmit = $Descriptor.Adapter -eq 'typecheck' }
            include = @($Descriptor.Members | ForEach-Object { Join-Path $repoRoot $_.Name })
            exclude = @()
        }
        $config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $configPath
        $null = Invoke-CategoryPhase ($Descriptor.Name + '-compile') 'node' @((Join-Path $categoryFrontendRoot 'node_modules/typescript/bin/tsc'), '-p', $configPath)
        if ($Descriptor.Adapter -eq 'node') {
            '{"type":"module"}' | Set-Content -LiteralPath (Join-Path $compiled 'package.json')
        }
        foreach ($member in $Descriptor.Members) {
            if ($Descriptor.Adapter -eq 'node') {
                $relative = $member.Name.Substring('BookOfEternityClient.WebFrontend/'.Length) -replace '\.tsx?$', '.js'
                $null = Invoke-CategoryPhase ($Descriptor.Name + '-' + [IO.Path]::GetFileName($relative)) 'node' @((Join-Path $compiled $relative))
            }
            $results.Add((New-AdapterResult $member.Name $Descriptor.Adapter))
        }
    }
    else { throw "Unsupported category adapter '$($Descriptor.Adapter)'." }
    Write-CategoryAdapterReport $Descriptor @($results)
    $script:adapterResults.Add([ordered]@{
        Descriptor = $Descriptor.Name; Adapter = $Descriptor.Adapter
        VerificationKind = if ($Descriptor.Adapter -eq 'typecheck') { 'compile-contracts' } else { 'tests' }
        Results = $results.Count; Seconds = $clock.Elapsed.TotalSeconds
    })
}
