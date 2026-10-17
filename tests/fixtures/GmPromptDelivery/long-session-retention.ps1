param([Parameter(Mandatory)][string]$RepoRoot, [Parameter(Mandatory)][string]$OwnedRoot, [ValidateSet('retention','restart-first','restart-second')][string]$Scenario = 'retention')
$ErrorActionPreference = 'Stop'
$GameSessionPath = Join-Path $OwnedRoot 'game_session'
[void][IO.Directory]::CreateDirectory((Join-Path $GameSessionPath 'input'))
$pending = Join-Path $GameSessionPath 'input/turn_request.json'
$BridgeControlScript = Join-Path $OwnedRoot 'controlled-current-launcher.ps1'
$definitions = @{}
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Current daemon AST prerequisite failed.' }
# Current functions only, without daemon top-level startup/provider. New production
# callees must be bound explicitly at SOURCE, not hidden by a missing-function RED.
foreach ($node in $ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]}, $true)) {
    $definitions[$node.Name] = $node.Extent.Text
}
foreach ($name in @('Read-GmPromptPending','Test-GmPromptSourceCurrent','Get-GmPromptContentHash','New-GmPromptOperation','New-GmPromptDelivery','Test-GmPromptDeliveryIdentity','Invoke-GmPromptControl','Send-ToGmBridge')) {
    if (-not $definitions.ContainsKey($name)) { throw "Missing current production prerequisite: $name" }
    . ([scriptblock]::Create($definitions[$name]))
}
function Get-GameConfig { [pscustomobject]@{GmBridgeEnabled=$true;GmBridgeBackend='ConPTYBridge'} }
function Ensure-GmBridgeStarted { }
function Get-GmBridgeStatus { [pscustomobject]@{ready=$true;inputBindingId='controlled-original-binding';nextPromptSequence=$(if($Scenario -eq 'restart-second'){2}else{$script:ObservedDispatches+1})} }
function Get-GmDaemonFileBytes { param($Path) return ,([IO.File]::ReadAllBytes($Path)) }
function Assert-GmNotDaemonReadFailure { param($Failure) throw $Failure }
@'
param([string]$Action, [string]$Json, [string]$SessionPath)
if ($Action -cne 'dispatch-operation') { throw 'Unexpected controlled launcher entry.' }
$payload = $Json | ConvertFrom-Json
[IO.File]::AppendAllText((Join-Path (Split-Path $SessionPath -Parent) 'launcher-calls.jsonl'), $Json + "`n")
[ordered]@{ok=$true;promptDelivery=[ordered]@{
 operationId=$payload.operationId;operationKind=$payload.operationKind;operationRevision=$payload.operationRevision;inputBindingId=$payload.inputBindingId;
 contentHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes("1`n"+[string]$payload.text)));
 disposition='submission-observed';phase='terminal';reason='controlled-positive-launcher'
}} | ConvertTo-Json -Depth 8 -Compress
'@ | Set-Content -LiteralPath $BridgeControlScript -Encoding utf8
$script:ObservedDispatches = 0
$script:GmPromptOperations = @{}
$last = $null
if ($Scenario -ne 'retention') {
    if ($Scenario -eq 'restart-first') {
        [IO.File]::WriteAllText($pending, '{"sessionId":"owned","requestId":"restart-current","turnNumber":1}')
    }
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($pending)))
    $operation = New-GmPromptOperation -Message 'frozen exact restart prompt' -PendingPath $pending -ExpectedSourceHash $hash -OperationKind turn -OperationRevision 'same-current-revision'
    if (-not $operation) { throw 'Valid current pending/binding allocation prerequisite failed.' }
    $last = Send-ToGmBridge -Message 'frozen exact restart prompt' -Operation $operation
    [ordered]@{payload=($operation.PayloadJson | ConvertFrom-Json);delivery=$last;scope='ACTUAL_FUNCTIONS_TWO_PROCESSES_SAME_CONTROLLED_LIVE_BINDING_NOT_REAL_BRIDGE'} | ConvertTo-Json -Depth 10 -Compress
    exit 0
}
for ($sequence = 1; $sequence -le 272; $sequence++) {
    # The actual source advances; a previous operation is settled, not unresolved
    # merely discarded to fit a cache. This is allocator/launcher proof, not game acceptance.
    [IO.File]::WriteAllText($pending, (@{sessionId='owned';requestId="owned-$sequence";turnNumber=$sequence} | ConvertTo-Json -Compress))
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($pending)))
    $operation = New-GmPromptOperation -Message "current controlled prompt $sequence" -PendingPath $pending -ExpectedSourceHash $hash -OperationKind turn -OperationRevision "revision-$sequence"
    if (-not $operation) { break }
    $last = Send-ToGmBridge -Message "current controlled prompt $sequence" -Operation $operation
    if ($last.disposition -cne 'submission-observed') { throw 'Positive current launcher prerequisite did not settle.' }
    $script:ObservedDispatches++
}
[ordered]@{completed=$script:ObservedDispatches;retainedOperations=$script:GmPromptOperations.Count;last=$last;
 scope='REAL_DAEMON_FUNCTIONS_CONTROLLED_STATUS_LAUNCHER_NOT_CLIENT_ACCEPTANCE'} | ConvertTo-Json -Depth 10 -Compress
