param([string]$RepoRoot, [string]$Scenario)
$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('boe-prompt-ps-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($fixtureRoot)
try {
    $source = Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($source, [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw 'Daemon source parsing failed.' }
    $names = @('Dispatch-WithRetry', 'Send-ToGmBridge')
    foreach ($function in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
        if ($function.Name -in $names) { . ([scriptblock]::Create($function.Extent.Text)) }
    }
    function Write-Log { param($Message, $Level, $Color) }
    function Start-Sleep { param($Seconds, $Milliseconds) }
    function New-GmDispatchDiagnostics { param($Status, $Attempts, $BusyRetries, [switch]$Timeout) [pscustomobject]@{ Status=$Status; Attempts=$Attempts } }
    if ($Scenario -eq 'transport-ambiguity') {
        $script:Calls = 0
        function Send-ToCliWindow {
            param($Message)
            $script:Calls++
            if ($script:Calls -eq 1) { return 'bridge-failed' }
            return 'sent'
        }
        $reply = Dispatch-WithRetry -Message 'controlled prompt' -ReturnDetails
        [ordered]@{ calls=$script:Calls; status=[string]$reply.Status } | ConvertTo-Json -Compress
    }
    elseif ($Scenario -eq 'allow-not-ready') {
        $BridgeControlScript = Join-Path $fixtureRoot 'controlled-launcher.ps1'
        $GameSessionPath = $fixtureRoot
        $env:BOE_PROMPT_FIXTURE_COMMANDS = Join-Path $fixtureRoot 'commands.txt'
        @'
param([string]$Action, [Parameter(ValueFromRemainingArguments=$true)]$Arguments, [string]$SessionPath)
[IO.File]::AppendAllText($env:BOE_PROMPT_FIXTURE_COMMANDS, $Action + "`n")
'{"ok":true,"promptDelivery":{"disposition":"submission-observed"}}'
'@ | Set-Content -LiteralPath $BridgeControlScript
        function Get-GameConfig { [pscustomobject]@{ GmBridgeEnabled=$true; GmBridgeBackend='ConPTYBridge' } }
        function Ensure-GmBridgeStarted { }
        function Get-GmBridgeStatus { [pscustomobject]@{ ready=$false; inputBindingId='controlled-binding' } }
        function Refresh-GmBridgeReadiness { Get-GmBridgeStatus }
        $null = Send-ToGmBridge -Message 'controlled bootstrap' -AllowNotReady
        [ordered]@{ commands=@([IO.File]::ReadAllLines($env:BOE_PROMPT_FIXTURE_COMMANDS)) } | ConvertTo-Json -Compress
    }
    else { throw 'Unknown isolated scenario.' }
}
finally {
    [IO.Directory]::Delete($fixtureRoot, $true)
    if ([IO.Directory]::Exists($fixtureRoot)) { throw 'Owned fixture cleanup failed.' }
}
