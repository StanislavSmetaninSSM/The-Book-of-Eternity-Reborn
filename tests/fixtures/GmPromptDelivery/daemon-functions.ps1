param([string]$RepoRoot, [string]$Scenario, [string]$OwnedRoot = '', [string]$SessionRoot = '')
$ErrorActionPreference = 'Stop'
$fixtureRoot = if ($OwnedRoot) { $OwnedRoot } else { Join-Path ([IO.Path]::GetTempPath()) ('boe-prompt-ps-' + [guid]::NewGuid().ToString('N')) }
[void][IO.Directory]::CreateDirectory($fixtureRoot)
try {
    $source = Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($source, [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw 'Daemon source parsing failed.' }
    $names = @('Dispatch-WithRetry', 'Send-ToGmBridge', 'Read-GmPromptPending', 'Test-GmPromptSourceCurrent', 'Get-GmPromptContentHash', 'New-GmPromptOperation', 'New-GmPromptDelivery', 'Test-GmPromptDeliveryIdentity', 'Invoke-GmPromptControl', 'Complete-GmPromptDispatch', 'Test-GmPromptDispatchPaused', 'Send-ToCliWindow', 'Process-Turn', 'Process-QteEffectResolutionRequest', 'Process-RepairRequest', 'Process-TerminalProtocolFailureRequest', 'Process-TurnCore', 'Process-QteEffectResolutionRequestCore', 'Process-RepairRequestCore', 'Process-TerminalProtocolFailureRequestCore')
    foreach ($function in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
        if ($function.Name -in $names) { . ([scriptblock]::Create($function.Extent.Text)) }
    }
    # This fixture verifies unchanged T042 delivery identity only. It does not
    # qualify main pins; its controlled file effects remain in fixtureRoot.
    function Invoke-GmParticipatingConsumer {param($SessionPath,[scriptblock]$Body) & $Body}
    function Write-GmCanonicalText {param($SessionPath,$Path,$Value,[switch]$Append) if($Append){Add-Content -LiteralPath $Path -Value $Value -Encoding utf8}else{Set-Content -LiteralPath $Path -Value $Value -Encoding utf8}}
    function Remove-GmCanonicalFile {param($SessionPath,$Path) Remove-Item -LiteralPath $Path -ErrorAction SilentlyContinue}
    $script:FixtureErrors = @()
    function Write-Log { param($Message, $Level, $Color) if ($Level -eq 'ERROR') { $script:FixtureErrors += $Message } }
    function Start-Sleep { param($Seconds, $Milliseconds) }
    function New-GmDispatchDiagnostics { param($Status, $Attempts, $BusyRetries, [switch]$Timeout) [pscustomobject]@{ Status=$Status; Attempts=$Attempts; Timeout=[bool]$Timeout } }
    function Get-GameConfig { [pscustomobject]@{ GmBridgeEnabled=$true; GmBridgeBackend='ConPTYBridge' } }
    function Get-GmBridgeStatus { [pscustomobject]@{ ready=$false; inputBindingId='controlled-binding' } }
    if ($Scenario -eq 'terminal-launcher-path') {
        $launcher = Join-Path $RepoRoot 'BookOfEternityClient/Launcher/bookofeternity.ps1'
        $ast = [Management.Automation.Language.Parser]::ParseFile($launcher, [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw 'Launcher source parsing failed.' }
        $function = $ast.Find({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Start-Bridge' }, $true)
        . ([scriptblock]::Create($function.Extent.Text))
        $script:TerminalRepo = Join-Path $fixtureRoot 'inert-repo'
        $project = Join-Path $script:TerminalRepo 'BookOfEternityGMBridge/BookOfEternityGMBridge.csproj'
        $exe = Join-Path $script:TerminalRepo 'BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityGMBridge.exe'
        [void][IO.Directory]::CreateDirectory((Split-Path $exe -Parent))
        [IO.File]::WriteAllText($project, '')
        [IO.File]::WriteAllText($exe, '')
        $script:TerminalCommands = @()
        function Read-BridgeStatus { param($Path) $null }
        function Read-GameConfig { param($Path) [pscustomobject]@{ GmBridgePipeNameOverride='inert-pipe' } }
        function Get-RepoRoot { $script:TerminalRepo }
        function Write-Host { param($Object, $ForegroundColor) }
        function Start-Process {
            param($FilePath, $ArgumentList, $WorkingDirectory, $WindowStyle)
            if ($FilePath -ne 'powershell.exe' -or $WorkingDirectory -ne $script:TerminalRepo) { throw 'Unexpected inert launcher invocation.' }
            $index = [array]::IndexOf($ArgumentList, '-EncodedCommand')
            $script:TerminalCommands += [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($ArgumentList[$index+1]))
        }
        Start-Bridge -ResolvedSessionPath $fixtureRoot
        Remove-Item -LiteralPath $exe
        Start-Bridge -ResolvedSessionPath $fixtureRoot
        [ordered]@{ commands=$script:TerminalCommands } | ConvertTo-Json -Compress
    }
    elseif ($Scenario -eq 'transport-ambiguity') {
        function Ensure-GmBridgeStarted { }
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
operationRevision=$payload.operationRevision; inputBindingId=$payload.inputBindingId; contentHash=([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes("1`n"+[string]$payload.text)))); disposition='submission-observed' } } | ConvertTo-Json -Compress
'@ | Set-Content -LiteralPath $BridgeControlScript
        function Get-GameConfig { [pscustomobject]@{ GmBridgeEnabled=$true; GmBridgeBackend='ConPTYBridge' } }
        function Ensure-GmBridgeStarted { }
        function Get-GmBridgeStatus { [pscustomobject]@{ ready=$false; inputBindingId='controlled-binding' } }
        function Refresh-GmBridgeReadiness { Get-GmBridgeStatus }
        $null = Send-ToGmBridge -Message 'controlled bootstrap' -AllowNotReady
        [ordered]@{ commands=@([IO.File]::ReadAllLines($env:BOE_PROMPT_FIXTURE_COMMANDS)) } | ConvertTo-Json -Compress
    }
    elseif ($Scenario -like 'late-*' -or $Scenario -in @('normal-complete','normal-timeout','consumed-preexisting','consumed-after-dispatch','typed-retry','repair-revisions','source-replaced','callback-replaced','turn-packet-replaced','autostart-binding','consumer-turn','consumer-qte','consumer-repair','consumer-terminal','qte-idle','connected-pipe','connected-held-pipe','launcher-lost-response')) {
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
$d = if ($env:BOE_PROMPT_FIXTURE_SCENARIO -like 'consumer-*' -or $env:BOE_PROMPT_FIXTURE_SCENARIO -eq 'turn-packet-replaced') { 'unknown-outcome' } else { 'submission-observed' }
$r = 'controlled'
if ($env:BOE_PROMPT_FIXTURE_SCENARIO -in @('typed-retry','source-replaced') -and $count -eq 1) { $d='not-written'; $r='busy' }
if ($env:BOE_PROMPT_FIXTURE_SCENARIO -in @('source-replaced','callback-replaced') -and $count -eq 1) { [IO.File]::WriteAllText($env:BOE_PROMPT_FIXTURE_PENDING, '{"replaced":true}') }
[ordered]@{ ok=$true; promptDelivery=[ordered]@{ operationId=$payload.operationId; operationKind=$payload.operationKind;
operationRevision=$payload.operationRevision; inputBindingId=$payload.inputBindingId; contentHash=([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes("1`n"+[string]$payload.text)))); disposition=$d; reason=$r; phase='terminal' } } | ConvertTo-Json -Compress
'@ | Set-Content -LiteralPath $BridgeControlScript
        function Ensure-GmBridgeStarted { $script:FixtureStarted=$true }
        if ($Scenario -eq 'autostart-binding') {
            function Get-GmBridgeStatus { if ($script:FixtureStarted) { [pscustomobject]@{ready=$true; inputBindingId='started-binding'} } }
        }
        function Get-TurnRequestKey { param($TurnRequest) 'controlled-turn' }
        function Test-ObservedTerminalRequestKey { param($Key) $false }
        function Test-TurnRequestHasPendingSnapshotContext { param($TurnRequest) $true }
        function Write-GmExperienceLessons { }
        function Get-GmExperiencePromptDigest { '' }
        function Get-FirstMortalBootstrapPrompt { param($TurnRequest) if ($Scenario -eq 'turn-packet-replaced') { [IO.File]::WriteAllText($Pending,'{"replaced":true}') }; '' }
        function Get-CorrelatedTerminalSignal { param($TurnRequest,$CompletionPath,$ErrorPath) $null }
        function Write-GmTrajectoryRecord { param($Dispatch,$ValidationStatus) $script:FixtureTrajectory = $Dispatch.Status; $script:FixtureValidation=$ValidationStatus }
        function Write-DaemonStatus { param($Status,$Reason) }
        function New-GmValidationRepairArtifactWatchState { param($RepairRequest,$DispatchStatus) [pscustomobject]@{ controlled=$true } }
        function Get-GmTrajectoryIssueKinds { param($RequestObject) @() }
        function Get-GmTrajectoryRepairPacketRefs { param($RequestObject) @() }
        function Get-GmTrajectoryValidationDiagnostics { param($RequestObject) '' }
        function Get-GmTrajectoryRepairPacketDiagnostics { param($RequestObject) '' }
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
        $script:FixtureStops=0; $script:FixtureObserved=@()
        if ($Scenario -in @('normal-complete','normal-timeout','consumed-preexisting','consumed-after-dispatch')) {
            $TurnTimeout=2; $script:FixtureTerminalReads=0
            function New-GmOutputWithoutTerminalWatchState { @{} }
            function Add-ObservedTerminalRequestKey { param($Key) $script:FixtureObserved += $Key }
            function Wait-CorrelatedValidationRepairRequest { param($TurnRequest,$GraceMilliseconds) $null }
            function Test-GmBridgeReturnedIdleWithoutTerminalSignal { param($ElapsedSeconds) $false }
            function Stop-GmBridgeAfterTurnTimeout { param($TurnRequest,$ElapsedSeconds,$Reason) $script:FixtureStops++; [pscustomobject]@{controlled=$true} }
            function Get-CorrelatedTerminalSignal {
                param($TurnRequest,$CompletionPath,$ErrorPath)
                $script:FixtureTerminalReads++
                if ($Scenario -ne 'normal-timeout' -and ($Scenario -eq 'consumed-preexisting' -or $script:FixtureTerminalReads -gt 1)) {
                    $signal=[pscustomobject]@{sessionId=$TurnRequest.sessionId; requestId=$TurnRequest.requestId; turnNumber=$TurnRequest.turnNumber; status='success'}
                    [IO.File]::WriteAllText($CompletionPath,($signal | ConvertTo-Json -Compress))
                    if ($Scenario -like 'consumed-*') { [IO.File]::Delete($Pending) }
                    return [pscustomobject]@{Path=$CompletionPath; Kind='success'; Signal=$signal}
                }
                return $null
            }
        }
        if ($Scenario -like 'late-*') {
            function New-GmOutputWithoutTerminalWatchState { @{} }
            function Add-ObservedTerminalRequestKey { param($Key) $script:FixtureObserved += $Key }
            function Start-Sleep { param($Seconds,$Milliseconds) if ($Seconds -eq 1 -and $Scenario -eq 'late-wait') { [IO.File]::WriteAllText($Pending,'{"replaced":true}') } }
            function Test-GmBridgeReturnedIdleWithoutTerminalSignal {
                param($ElapsedSeconds)
                if ($Scenario -eq 'late-idle') { [IO.File]::WriteAllText($Pending,'{"replaced":true}'); return $true }
                return $false
            }
            function Test-GmOutputWithoutTerminalSignal {
                param($ElapsedSeconds,$WatchState)
                if ($Scenario -eq 'late-payload') { [IO.File]::WriteAllText($Pending,'{"replaced":true}') }
                if ($Scenario -in @('late-payload','late-stop-payload')) { return [pscustomobject]@{isStalled=$true; changedFiles=@()} }
                return $null
            }
            function Test-GmBridgeArtifactWritingStall {
                param($ElapsedSeconds,$WatchState)
                if ($Scenario -eq 'late-artifact') { [IO.File]::WriteAllText($Pending,'{"replaced":true}') }
                if ($Scenario -in @('late-artifact','late-stop-artifact')) { return [pscustomobject]@{isStalled=$true} }
                return $null
            }
            function Stop-GmBridgeAfterTurnTimeout {
                param($TurnRequest,$ElapsedSeconds,$Reason)
                $script:FixtureStops++
                if ($Scenario -like 'late-stop-*') { [IO.File]::WriteAllText($Pending,'{"replaced":true}') }
                [pscustomobject]@{ controlled=$true }
            }
            function Write-DaemonJsonFileBestEffort { param($Path,$Payload,$Depth) [IO.File]::WriteAllText($Path,'{}'); $true }
            $OutputWithoutTerminalReportFile=Join-Path $ReadyDir 'payload-report.json'
            $ArtifactWriteStallReportFile=Join-Path $ReadyDir 'artifact-report.json'
            if ($Scenario -eq 'late-stop-timeout') { $TurnTimeout=2 }
        }
        if ($Scenario -in @('connected-pipe','connected-held-pipe','launcher-lost-response')) {
            # Extract real launcher functions; the shim executes no launcher/main startup statements.
            $launcher = Join-Path $RepoRoot 'BookOfEternityClient/Launcher/bookofeternity.ps1'
            $launcherAst = [Management.Automation.Language.Parser]::ParseFile($launcher, [ref]$tokens, [ref]$errors)
            if ($errors.Count) { throw 'Launcher source parsing failed.' }
            $lnames = @('Get-BridgeStatusPath','Test-BridgeHelperAlive','Read-BridgeStatus','Invoke-BridgeRequest','Invoke-BridgePromptDelivery','Invoke-BridgePromptDeliveryCore','Get-BridgePromptContentHash')
            $defs = $launcherAst.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -in $lnames}, $true)
            foreach ($f in $defs) { . ([scriptblock]::Create($f.Extent.Text)) }
            if ($Scenario -eq 'launcher-lost-response') {
                $script:LauncherCommands=@()
                function Invoke-BridgeRequest {
                    param($ResolvedSessionPath,$Payload)
                    $script:LauncherCommands += $Payload.command
                    if ($Payload.command -eq 'dispatchPrompt') { throw 'Controlled lost response after submit.' }
                    [pscustomobject]@{ ok=$true; promptDelivery=[pscustomobject]@{ operationId=$Payload.operationId; operationKind=$Payload.operationKind;
                        operationRevision=$Payload.operationRevision; inputBindingId=$Payload.inputBindingId; contentHash=(Get-BridgePromptContentHash $Payload); disposition='submission-observed' } }
                }
                $payload = @{command='dispatchPrompt'; text='text'; operationId='lost'; operationKind='turn'; operationRevision='one'; inputBindingId='binding'}
                $reply = Invoke-BridgePromptDelivery -ResolvedSessionPath $fixtureRoot -Payload $payload
                [ordered]@{ commands=$script:LauncherCommands; disposition=$reply.promptDelivery.disposition } | ConvertTo-Json -Compress
            }
            else {
                $GameSessionPath=$SessionRoot
                function Get-GmBridgeStatus { Read-BridgeStatus $GameSessionPath }
                # Function-backed launcher shim still executes the real Send-ToGmBridge invocation and real RPC helpers.
                $shim = "param([string]`$Action,[Parameter(ValueFromRemainingArguments=`$true)]`$Arguments,[string]`$SessionPath)`n"
                $shim += ($defs.Extent.Text -join "`n") + "`n"
                $shim += 'Invoke-BridgePromptDelivery -ResolvedSessionPath $SessionPath -Payload (($Arguments -join " ") | ConvertFrom-Json -AsHashtable) | ConvertTo-Json -Depth 8'
                # Preserve actual newlines; no executable launcher startup copied.
                [IO.File]::WriteAllText($BridgeControlScript,$shim)
                $first=Dispatch-WithRetry -Message 'connected prompt' -PendingPath $Pending -ReturnDetails -MaxWaitSeconds 2
                $second=Dispatch-WithRetry -Message 'connected prompt' -PendingPath $Pending -ReturnDetails -MaxWaitSeconds 2
                [ordered]@{ first=$first.Status; second=$second.Status; firstId=$first.PromptDelivery.operationId; secondId=$second.PromptDelivery.operationId } | ConvertTo-Json -Compress
            }
        }
        else {
            switch ($Scenario) {
                'consumer-turn' { Process-Turn $Pending }
                'turn-packet-replaced' { Process-Turn $Pending }
                'consumer-qte' { Process-QteEffectResolutionRequest $Pending }
                'consumer-repair' { Process-RepairRequest $Pending }
                'consumer-terminal' { Process-TerminalProtocolFailureRequest $Pending }
                'qte-idle' { Process-QteEffectResolutionRequest $Pending }
                'repair-revisions' {
                    Process-RepairRequest $Pending
                    [IO.File]::WriteAllText($Pending,'{"requestId":"request","turnNumber":1,"revalidationAttempt":2}')
                    [IO.File]::SetLastWriteTimeUtc($Pending,[datetime]::UtcNow.AddSeconds(1))
                    Process-RepairRequest $Pending
                }
                default { if ($Scenario -like 'late-*' -or $Scenario -in @('normal-complete','normal-timeout','consumed-preexisting','consumed-after-dispatch')) { Process-Turn $Pending } else { $reply=Dispatch-WithRetry -Message 'controlled packet' -PendingPath $Pending -ReturnDetails -MaxWaitSeconds 10 } }
            }
            $commands = if (Test-Path $env:BOE_PROMPT_FIXTURE_COMMANDS) { @([IO.File]::ReadAllLines($env:BOE_PROMPT_FIXTURE_COMMANDS) | ForEach-Object { $_ | ConvertFrom-Json }) } else { @() }
            [ordered]@{ commands=@($commands); status=$reply.Status; pending=[IO.File]::Exists($Pending);
                readyFiles=@([IO.Directory]::GetFiles($ReadyDir)).Count; errors=$script:FixtureErrors; stops=$script:FixtureStops; observed=@($script:FixtureObserved); validation=$script:FixtureValidation;
                terminal=$(if ($Scenario -in @('normal-complete','normal-timeout','consumed-preexisting','consumed-after-dispatch')) { [IO.File]::ReadAllText(@([IO.Directory]::GetFiles($ReadyDir))[0]) | ConvertFrom-Json } else { $null });
                errorCount=$script:ErrorCount; processing=$script:IsProcessing; paused=[bool]$script:GmPromptInputPaused } | ConvertTo-Json -Depth 8 -Compress
        }
    }
    else { throw 'Unknown isolated scenario.' }
}
finally {
    [IO.Directory]::Delete($fixtureRoot, $true)
    if ([IO.Directory]::Exists($fixtureRoot)) { throw 'Owned fixture cleanup failed.' }
}
