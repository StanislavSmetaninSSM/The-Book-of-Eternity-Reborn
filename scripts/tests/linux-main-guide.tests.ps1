[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot)
$temp = Join-Path ([IO.Path]::GetTempPath()) ('boe-linux-guide-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
$script:passed = 0
$script:failed = 0
function Assert-That([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Test-Case([string]$Name, [scriptblock]$Action) {
    try { & $Action; $script:passed++; Write-Host "PASS $Name" }
    catch { $script:failed++; Write-Host "FAIL $Name`: $($_.Exception.Message)" }
}
try {
    # Execute the actual data-only launcher functions, never its entrypoint.
    $tokens = $null; $parseErrors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $root 'BookOfEternityClient/Launcher/bookofeternity.ps1'),
        [ref]$tokens, [ref]$parseErrors)
    Assert-That ($parseErrors.Count -eq 0) 'Launcher parse failed.'
    foreach ($name in @('New-DefaultGmWorkerBridgeProfiles', 'Convert-RetiredCodexLaunchDefaults', 'Read-GameConfig')) {
        $functions = @($ast.FindAll({ param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name
        }, $true))
        Assert-That ($functions.Count -eq 1) "Expected one original $name function."
        Invoke-Expression $functions[0].Extent.Text
    }
    Test-Case 'Documented partial profile is consumed without changing command model cwd or inventory' {
        $guide = Get-Content (Join-Path $root 'BookOfEternityClient/Launcher/CLI_Daemon_Quickstart.md') -Raw
        $block = [regex]::Match($guide, '(?s)```json\s*(.*?)\s*```')
        Assert-That $block.Success 'The installed Linux guide has no executable configuration example.'
        $partial = $block.Groups[1].Value | ConvertFrom-Json -AsHashtable
        $profiles = @(New-DefaultGmWorkerBridgeProfiles)
        $profile = @{
            gmCliLaunchCommand = 'neutral-fixture --model synthetic-token --argument "two words"'
            gmBridgeShellWorkingDirectory = (Join-Path $temp 'Мир with spaces')
            gmWorkerBridgeProfiles = $profiles
            gmCliInputProfile = @{ idleMarker = 'OBSERVED READY'; promptPrefix = 'OBSERVED> ' }
        }
        $before = $profile | ConvertTo-Json -Depth 30 -Compress
        foreach ($key in $partial.Keys) { $profile[$key] = $partial[$key] }
        $profile | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $temp 'config.json') -Encoding utf8NoBOM
        $loaded = Read-GameConfig $temp
        Assert-That ($loaded.GmBridgeEnabled -eq $true -and $loaded.GmBridgeBackend -ceq 'OwnedTerminal' -and
            $loaded.GmMainOwnerBackend -ceq 'NativeLineage') 'Documented fields do not select the explicit Linux route.'
        $original = $before | ConvertFrom-Json
        Assert-That ($loaded.GmCliLaunchCommand -ceq $original.gmCliLaunchCommand -and
            $loaded.GmBridgeShellWorkingDirectory -ceq $original.gmBridgeShellWorkingDirectory -and
            ($loaded.GmCliInputProfile | ConvertTo-Json -Compress) -ceq ($original.gmCliInputProfile | ConvertTo-Json -Compress)) 'Existing CLI configuration was replaced.'
        Assert-That (($loaded.GmWorkerBridgeProfiles | ConvertTo-Json -Depth 30 -Compress) -ceq
            ($original.gmWorkerBridgeProfiles | ConvertTo-Json -Depth 30 -Compress)) 'Retained inventory was replaced.'
        Assert-That (@($loaded.GmWorkerBridgeProfiles | Where-Object enabled).Count -eq 0 -and
            @($loaded.GmWorkerBridgeProfiles).Count -gt 0) 'The guide enabled helpers or removed inventory.'
    }
    Test-Case 'Missing configuration keeps defaults instead of silently enabling Linux backend' {
        $absent = Join-Path $temp 'absent'
        New-Item -ItemType Directory -Path $absent | Out-Null
        $loaded = Read-GameConfig $absent
        Assert-That ($loaded.GmBridgeBackend -ceq 'ConPTYBridge' -and
            @($loaded.GmWorkerBridgeProfiles | Where-Object enabled).Count -eq 0) 'Defaults were silently changed.'
    }
    Test-Case 'Malformed configuration preserves the original data-only fallback' {
        Set-Content (Join-Path $temp 'config.json') '{malformed' -Encoding utf8NoBOM
        $loaded = Read-GameConfig $temp
        Assert-That ($loaded.GmBridgeBackend -ceq 'ConPTYBridge' -and
            @($loaded.GmWorkerBridgeProfiles).Count -gt 0 -and
            @($loaded.GmWorkerBridgeProfiles | Where-Object enabled).Count -eq 0) 'Fallback discarded inventory or changed admission.'
    }
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }
Write-Host "CATEGORY-CONTRACT-RESULT passed=$script:passed failed=$script:failed"
if ($script:failed -gt 0) { exit 1 }
