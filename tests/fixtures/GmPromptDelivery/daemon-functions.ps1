param([string]$RepoRoot, [string]$Scenario, [string]$OwnedRoot = '', [string]$SessionRoot = '')
$ErrorActionPreference = 'Stop'
$fixtureRoot = if ($OwnedRoot) { $OwnedRoot } else { Join-Path ([IO.Path]::GetTempPath()) ('boe-prompt-ps-' + [guid]::NewGuid().ToString('N')) }
[void][IO.Directory]::CreateDirectory($fixtureRoot)
try {
    $source = Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($source, [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw 'Daemon source parsing failed.' }
    $names = @('Dispatch-WithRetry', 'Send-ToGmBridge', 'New-GmPromptOperation', 'New-GmPromptDelivery', 'Test-GmPromptDeliveryIdentity', 'Invoke-GmPromptControl', 'Complete-GmPromptDispatch', 'Test-GmPromptDispatchPaused', 'Send-ToCliWindow', 'Process-Turn', 'Process-QteEffectResolutionRequest', 'Process-RepairRequest', 'Process-TerminalProtocolFailureRequest')
    foreach ($function in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
        if ($function.Name -in $names) { . ([scriptblock]::Create($function.Extent.Text)) }
    }
    $script:FixtureErrors = @()
    function Write-Log { param($Message, $Level, $Color) if ($Level -eq 'ERROR') { $script:FixtureErrors += $Message } }
    function Start-Sleep { param($Seconds, $Milliseconds) }
    function New-GmDispatchDiagnostics { param($Status, $Attempts, $BusyRetries, [switch]$Timeout) [pscustomobject]@{ Status=$Status; Attempts=$Attempts } }
    function Get-GameConfig { [pscustomobject]@{ GmBridgeEnabled=$true; GmBridgeBackend='ConPTYBridge' } }
    function Get-GmBridgeStatus { [pscustomobject]@{ ready=$false; inputBindingId='controlled-binding' } }
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
$payload = ($Arguments -join ' ') | ConvertFrom-Json
[ordered]@{ ok=$true; promptDelivery=[ordered]@{ operationId=$payload.operationId; operationKind=$payload.operationKind;
operationRevision=$payload.operationRevision; inputBindingId=$payload.inputBindingId; disposition='submission-observed' } } | ConvertTo-Json -Compress
'@ | Set-Content -LiteralPath $BridgeControlScript
        function Get-GameConfig { [pscustomobject]@{ GmBridgeEnabled=$true; GmBridgeBackend='ConPTYBridge' } }
        function Ensure-GmBridgeStarted { }
        function Get-GmBridgeStatus { [pscustomobject]@{ ready=$false; inputBindingId='controlled-binding' } }
        function Refresh-GmBridgeReadiness { Get-GmBridgeStatus }
        $null = Send-ToGmBridge -Message 'controlled bootstrap' -AllowNotReady
        [ordered]@{ commands=@([IO.File]::ReadAllLines($env:BOE_PROMPT_FIXTURE_COMMANDS)) } | ConvertTo-Json -Compress
    }
    elseif ($Scenario -in @('typed-retry','source-replaced','consumer-turn','consumer-qte','consumer-repair','consumer-terminal','qte-idle','connected-pipe','launcher-lost-response')) {
        $BridgeControlScript = Join-Path $fixtureRoot 'controlled-launcher.ps1'
        $GameSessionPath = $fixtureRoot
        $env:BOE_PROMPT_FIXTURE_COMMANDS = Join-Path $fixtureRoot 'commands.txt'
        $env:BOE_PROMPT_FIXTURE_SCENARIO = $Scenario
        $Pending = Join-Path $fixtureRoot 'pending.json'
        $env:BOE_PROMPT_FIXTURE_PENDING = $Pending
        @'
param([string]$Action, [Parameter(ValueFromRemainingArguments=$true)]$Arguments, [string]$SessionPath)
$payload = ($Arguments -join ' ') | ConvertFrom-Json
[IO.File]::AppendAllText($env:BOE_PROMPT_FIXTURE_COMMANDS, ($payload | ConvertTo-Json -Compress) + "`n")
$count = [IO.File]::ReadAllLines($env:BOE_PROMPT_FIXTURE_COMMANDS).Count
$d = if ($env:BOE_PROMPT_FIXTURE_SCENARIO -like 'consumer-*') { 'unknown-outcome' } else { 'submission-observed' }
$r = 'controlled'
if ($env:BOE_PROMPT_FIXTURE_SCENARIO -in @('typed-retry','source-replaced') -and $count -eq 1) { $d='not-written'; $r='busy' }
if ($env:BOE_PROMPT_FIXTURE_SCENARIO -eq 'source-replaced' -and $count -eq 1) { [IO.File]::WriteAllText($env:BOE_PROMPT_FIXTURE_PENDING, '{"replaced":true}') }
[ordered]@{ ok=$true; promptDelivery=[ordered]@{ operationId=$payload.operationId; operationKind=$payload.operationKind;
operationRevision=$payload.operationRevision; inputBindingId=$payload.inputBindingId; disposition=$d; reason=$r; phase='terminal' } } | ConvertTo-Json -Compress
'@ | Set-Content -LiteralPath $BridgeControlScript
        function Ensure-GmBridgeStarted { }
        function Get-TurnRequestKey { param($TurnRequest) 'controlled-turn' }
        function Test-ObservedTerminalRequestKey { param($Key) $false }
        function Test-TurnRequestHasPendingSnapshotContext { param($TurnRequest) $true }
        function Write-GmExperienceLessons { }
        function Get-GmExperiencePromptDigest { '' }
        function Get-FirstMortalBootstrapPrompt { param($TurnRequest) '' }
        function Get-CorrelatedTerminalSignal { param($TurnRequest,$CompletionPath,$ErrorPath) $null }
        function Write-GmTrajectoryRecord { param($Dispatch) $script:FixtureTrajectory = $Dispatch.Status }
        function Write-DaemonStatus { param($Status,$Reason) }
        function Test-ProtocolRequestUsesDiagnosticOnlyMetadata { param($RequestObject) $false }
        function Get-QteEffectResolutionRequestKey { param($Request) 'wave-1' }
        function Test-QteEffectResolutionReadyMatchesRequest { param($Request,$ReadyPath) $false }
        function Test-QteEffectResolutionRequestStillCurrent { param($RequestPath,$ExpectedRequestKey) $true }
        function Build-QteEffectResolutionDispatchMessage { param($Request) 'controlled QTE' }
        function Test-GmBridgeReturnedIdleWithoutTerminalSignal { param($ElapsedSeconds) $true }
        $TurnTimeout = 20; $script:BridgeDispatchMaxWaitSeconds=20
        $script:IsProcessing=$false; $script:ErrorCount=0; $script:TurnCount=0
        $script:LastRepairRequestWrite=[datetime]::MinValue; $script:LastTerminalProtocolFailureWrite=[datetime]::MinValue
        $ReadyDir = Join-Path $fixtureRoot 'ready'; [void][IO.Directory]::CreateDirectory($ReadyDir)
        $QteEffectResolutionReadyFile=Join-Path $ReadyDir 'qte.json'
        [IO.File]::WriteAllText($Pending, '{"requestKind":"qte_deferred_effect_resolution","safePacket":{},"sessionId":"fixture","requestId":"request","turnNumber":1,"playerAction":"controlled","waveOrdinal":1,"acceptedSourceTurn":1}')
        if ($Scenario -in @('connected-pipe','launcher-lost-response')) {
            # Extract real launcher functions; the shim executes no launcher/main startup statements.
            $launcher = Join-Path $RepoRoot 'BookOfEternityClient/Launcher/bookofeternity.ps1'
            $launcherAst = [Management.Automation.Language.Parser]::ParseFile($launcher, [ref]$tokens, [ref]$errors)
            if ($errors.Count) { throw 'Launcher source parsing failed.' }
            $lnames = @('Get-BridgeStatusPath','Test-BridgeHelperAlive','Read-BridgeStatus','Invoke-BridgeRequest','Invoke-BridgePromptDelivery')
            $defs = $launcherAst.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -in $lnames}, $true)
            foreach ($f in $defs) { . ([scriptblock]::Create($f.Extent.Text)) }
            if ($Scenario -eq 'launcher-lost-response') {
                $script:LauncherCommands=@()
                function Invoke-BridgeRequest {
                    param($ResolvedSessionPath,$Payload)
                    $script:LauncherCommands += $Payload.command
                    if ($Payload.command -eq 'dispatchPrompt') { throw 'Controlled lost response after submit.' }
                    [pscustomobject]@{ ok=$true; promptDelivery=[pscustomobject]@{ operationId=$Payload.operationId; operationKind=$Payload.operationKind;
                        operationRevision=$Payload.operationRevision; inputBindingId=$Payload.inputBindingId; disposition='submission-observed' } }
                }
                $payload = @{command='dispatchPrompt'; text='text'; operationId='lost'; operationKind='turn'; operationRevision='one'; inputBindingId='binding'}
                $reply = Invoke-BridgePromptDelivery -ResolvedSessionPath $fixtureRoot -Payload $payload
                [ordered]@{ commands=$script:LauncherCommands; disposition=$reply.promptDelivery.disposition } | ConvertTo-Json -Compress
            }
            else {
                $GameSessionPath=$SessionRoot
                function Get-GmBridgeStatus { Read-BridgeStatus $GameSessionPath }
                # Function-backed launcher shim still executes the real Send-ToGmBridge invocation and real RPC helpers.
                $shim = "param([string]`$Action,[Parameter(ValueFromRemainingArguments=`$true)]`$Arguments,[string]`$SessionPath)\n"
                $shim += ($defs.Extent.Text -join "`n") + "`n"
                $shim += 'Invoke-BridgePromptDelivery -ResolvedSessionPath $SessionPath -Payload (($Arguments -join " ") | ConvertFrom-Json -AsHashtable) | ConvertTo-Json -Depth 8'
                # Preserve actual newlines; no executable launcher startup copied.
                $shim = $shim.Replace('\n', "`n")
                [IO.File]::WriteAllText($BridgeControlScript,$shim)
                $first=Dispatch-WithRetry -Message 'connected prompt' -PendingPath $Pending -ReturnDetails -MaxWaitSeconds 2
                $second=Dispatch-WithRetry -Message 'connected prompt' -PendingPath $Pending -ReturnDetails -MaxWaitSeconds 2
                [ordered]@{ first=$first.Status; second=$second.Status; firstId=$first.PromptDelivery.operationId; secondId=$second.PromptDelivery.operationId } | ConvertTo-Json -Compress
            }
        }
        else {
            switch ($Scenario) {
                'consumer-turn' { Process-Turn $Pending }
                'consumer-qte' { Process-QteEffectResolutionRequest $Pending }
                'consumer-repair' { Process-RepairRequest $Pending }
                'consumer-terminal' { Process-TerminalProtocolFailureRequest $Pending }
                'qte-idle' { Process-QteEffectResolutionRequest $Pending }
                default { $reply=Dispatch-WithRetry -Message 'controlled packet' -PendingPath $Pending -ReturnDetails -MaxWaitSeconds 10 }
            }
            $commands = if (Test-Path $env:BOE_PROMPT_FIXTURE_COMMANDS) { @([IO.File]::ReadAllLines($env:BOE_PROMPT_FIXTURE_COMMANDS) | ForEach-Object { $_ | ConvertFrom-Json }) } else { @() }
            [ordered]@{ commands=$commands; status=$reply.Status; pending=[IO.File]::Exists($Pending);
                readyFiles=@([IO.Directory]::GetFiles($ReadyDir)).Count; errors=$script:FixtureErrors;
                errorCount=$script:ErrorCount; processing=$script:IsProcessing; paused=[bool]$script:GmPromptInputPaused } | ConvertTo-Json -Depth 8 -Compress
        }
    }
    else { throw 'Unknown isolated scenario.' }
}
finally {
    [IO.Directory]::Delete($fixtureRoot, $true)
    if ([IO.Directory]::Exists($fixtureRoot)) { throw 'Owned fixture cleanup failed.' }
}
