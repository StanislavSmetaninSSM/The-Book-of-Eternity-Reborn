<#
.SYNOPSIS
    Game Master Daemon — мост между C# клиентом и CLI-агентом геймастера.

.DESCRIPTION
    Следит за turn_request.json и отправляет команду обработки хода
    в ВИДИМОЕ окно CLI-агента. Игрок может видеть работу агента
    и вмешаться при необходимости.

    Архитектура (3 окна):
    - Окно 1: C# клиент (игра) — игрок вводит действия
    - Окно 2: CLI-агент (ГМ) — обрабатывает ходы, виден игроку
    - Окно 3: Этот демон — мост между ними

    CLI-агент запускается ОТДЕЛЬНО игроком с промптом из Launcher\CLI_Launch_Script.md.
    Демон НЕ запускает CLI, а только отправляет ему сообщения.

.PARAMETER GameSessionPath
    Путь к директории game_session.

.PARAMETER CliWindowTitle
    Fallback-заголовок окна CLI-агента для отправки сообщений.
    Используется только если отсутствует gm_cli_window_binding.json или binding невалиден.

.PARAMETER LaunchScriptPath
    Optional explicit path to the generated GM launch script. Wrappers should
    pass a session-local generated file instead of mutating the tracked sample.

.PARAMETER AutoPaste
    Автоматически вставлять команду в окно CLI.
    Если $false — только копирует в буфер обмена и уведомляет.

.PARAMETER PasteMode
    Способ вставки при AutoPaste:
    - RightClick (по умолчанию)
    - ShiftInsert
    - CtrlV

.EXAMPLE
    # Основной режим: использовать зарегистрированное окно CLI
    .\game_master_daemon.ps1 -AutoPaste

.EXAMPLE
    # Fallback по заголовку окна, если binding ещё не зарегистрирован:
    .\game_master_daemon.ps1 -CliWindowTitle "GM Codex" -AutoPaste

#>

param(
    [string]$GameSessionPath = ".\game_session",
    [string]$CliWindowTitle = "",
    [switch]$AutoPaste,
    [ValidateSet("RightClick","ShiftInsert","CtrlV")]
    [string]$PasteMode = "RightClick",
    [int]$TurnTimeout = 0,
    [int]$PollingInterval = 500,
    [string]$LogFile = "",
    [string]$LaunchScriptPath = ""
)

# ═══════════════════════════════════════════════
# Initialization
# ═══════════════════════════════════════════════

$ErrorActionPreference = "Stop"
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
chcp 65001 > $null
Add-Type -AssemblyName System.Windows.Forms

$script:TurnCount = 0
$script:ErrorCount = 0
$script:StartTime = Get-Date
$script:IsProcessing = $false
$script:BootstrapSent = $false
$script:ObservedTerminalRequestKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$script:RepoRootPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$script:TaskGuideMainPath = Join-Path $script:RepoRootPath "TaskGuides\CLI_Step_Main.txt"
$script:ExampleMainPath = Join-Path $script:RepoRootPath "Examples\E_CLI_Step_Main.txt"
$script:AfterlifeMatrixPath = Join-Path $script:RepoRootPath "OtherGuides\Afterlife_Contract_Matrix.md"
$script:AfterlifeTurnsExamplePath = Join-Path $script:RepoRootPath "Examples\E_CLI_Afterlife_Turns.txt"
$script:AfterlifeCombatGlossaryPath = Join-Path $script:RepoRootPath "OtherGuides\Afterlife_Combat_Terminology_Glossary.md"
$script:InkFeatherExamplePath = Join-Path $script:RepoRootPath "Examples\E_CLI_Ink_Feather_Actions.txt"
$script:GmDocPathDirective = " GM documentation paths are repo-local and authoritative: TaskGuide='$($script:TaskGuideMainPath)', MainExample='$($script:ExampleMainPath)', AfterlifeMatrix='$($script:AfterlifeMatrixPath)', AfterlifeTurns='$($script:AfterlifeTurnsExamplePath)'. Do not search other worktrees for GM docs; use these absolute paths."
$script:AfterlifeRealmGateDirective = " Realm Gate is mandatory before broad state reads: read input/turn_request.json and game_state/meta/soul_state.json first. If currentRealm is Chaos Sea or Shining Abode, do not read or repair mortal world files before reading world, NPC, faction, quest, inventory, combat, or mortal misc state; use only afterlife surfaces unless validation_repair_request.json explicitly names a wrong-realm repair target. In afterlife turns, before terminal signal, verify you did not change MortalWorldProfile files under game_state/world, game_state/npcs, game_state/factions, game_state/player, game_state/inventory, game_state/combat, or Mortal quest files. If you accidentally changed any of them, restore or remove those wrong-realm changes before terminal completion or repair completion. The client may auto-rollback forbidden wrong-realm mutations from the validated pending_turn_snapshot and write game_state/control/validation_auto_rollback_report.json before repair; this report is diagnostic only and is not permission to author MortalWorldProfile changes from afterlife. If the client restored a forbidden file to the validated snapshot, remaining validation errors from that restored baseline are client-owned diagnostic noise for this turn; do not repair them by editing MortalWorldProfile files."
$script:AfterlifeExamplesDirective = " If game_state/meta/soul_state.json.currentRealm is Chaos Sea or Shining Abode, or progressionControl contains any afterlife mustEvaluate*/afterlifeCatchup debt, read compact templates first and use '$($script:AfterlifeMatrixPath)' to select exact canonical afterlife surfaces/receipts. Open '$($script:AfterlifeTurnsExamplePath)' only when the compact templates and matrix do not cover the route-specific contract you need; do not open the huge example file for basic terminal, progression_report, actor reasoning, repair, or tempoAdvantage field names. Route references when needed: example 14 for Shining core action fragments, examples 16-18 for combined scheduler + pending contract + player-action turns, example 19 for ordinary scheduler-only Chaos Sea living-world turns, example 20 for system Guardian attraction, example 21 for protected return guard turns, example 22 for direct resident action / hidden pending-backed routing tags, example 23 for freeform Chaos Sea Abode search with reason/source=chaos_sea_abode_search, example 24 for afterlife spiritual conflict with diceAudit on contested exchange/resolve and either specialArtAudit/effectNote/specialCostMultiplierPercent or specialArtAudits[] when both sides use named special arts; never write both special-art audit fields on one exchange. Non-player special arts must match the resolved opposition operation used for actionCostAudit.opposition, not the player's exchange.operationType or a stale incomingAction candidate; incomingAction.finalOperationType is authoritative when present, and terminal/free player operations must not include actionCostAudit.player. Use example 25 for Source of Light capstone closure from game_state/control/pending_source_of_light_capstone.json into sourceOfLightCapstone, light_incarnate, and source_of_light_incarnated_light. Scheduler allowance is scheduler-owned only: progressionProcessingReport permits only scheduler-owned Shining/resident/trade progression fields and does not authorize availability, coreActionReceipts, gates, gachaSystem.gachaHistory, pendingNativeFactionDiscovery, preparedIncarnationPackage, lightSparks, treasury, or sourceOfLightCapstone unless that surface has its own client-authored contract closed in the same turn. Use example 26 for afterlifeEntityProfileUpdates / afterlifeEntityCustomStateChanges / afterlifeFateCardUnlocks / afterlifeActorGoalUpdates / afterlifeActorQuestUpdates / afterlifeActorActivityUpdates / completeAfterlifeActorActivities / afterlifeRelationshipChanges / afterlifeRelationshipLockUpdates / afterlifeBreakthroughQuestUpdates / afterlifeActorMaskAdds / afterlifeActorMaskUpdates / afterlifeActorMaskRemovals / afterlifeActorActiveMaskChanges / afterlifeEntityProgressionOverrides / afterlifeSpecialArtLearningReceipts / game_state/meta/afterlife_entity_profiles.json, including fateCards, guardianEffects, playerUnlocks, politicalEffects, combatEffects, trainingUnlocks, relationships, relationshipLock, breakthroughQuestId, redemptionQuestId, pointOfNoReturn, _clear_, masks, activeMaskId, concealedTruth, directives, revealConditions, deceptionRisk, linkedThreatId, linkedSarefAgentId, goals, personalQuests, currentActivity, completedActivities, gmThoughtsSummary, specialArts, upgradeCost with only inkFeathers/lightSparks and at least one positive value, no progression via initialTier in learning receipts, trainingConditions, costMultiplierPercent, customStates, statesToRemove, progressionLedger, lastAutoProgressionCycleKey, soulDissipationProof, targetStabilityCoefficient, and terminalGameOver. Use _true_self_ rather than null for active mask removal, keep hidden mask truth out of normal player UI until isRevealed=true, and never use Mortal NPCMaskAdds. Use example 26D for afterlifeThreatsToAdd / afterlifeThreatsToUpdate / completeAfterlifeThreatActivities / afterlifeThreatsToRemove / game_state/meta/afterlife_active_threats.json persistent threats: use threats[], currentActivity, impactProfile, visibleToPlayer, optional sarefLink, close currentActivity only through completion, do not leak hidden threats, and never use Mortal worldMapUpdates.activeThreats. Use example 26B for afterlifeChronicleUpdates / game_state/meta/afterlife_chronicles.json external memory: write lastEventsDescription only, never eventDescriptions[] in updates, and do not substitute worldEventsLog/currentLocationData/worldMapUpdates. Use example 28 for afterlifeGlobalFlagUpdates / game_state/meta/afterlife_global_flags.json global facts: use flags[] with visibility hidden/gm_only for private facts, include gmThoughtsSummary and obsoleteReason for obsolete flags, and never use Mortal worldStateFlags. If resolving afterlife spiritual conflict, Spiritual Arts, Source of Light capstone rewards, or afterlife entity profiles, also read '$($script:AfterlifeCombatGlossaryPath)' for Russian labels while keeping JSON keys/enums English."
$script:AfterlifeCombatConditionsDirective = " If an afterlife spiritual conflict uses combatConditions[], use only kinds mark, ward, burden, opening, or vow; each active condition needs source, target, affected operations, duration/uses, counterplay, and summary. Conditions may affect only condition-backed rollMode sources, conflictPosition, legal anti-control controlState softening/narrowing, side strain, tempoAdvantage, counterPayoff, actionCostAudit / OD costs, or specialArtAudit.effectNote. This is no generic passive stat stacking: create, consume, expire, or clear combatConditions explicitly. Show visible active combatConditions in ordinary conflict/log output and keep hidden/gm_only combatConditions private."
$script:AfterlifeSpecialArtCombatEffectDirective = " Current/new teachable specialArts[] require specialArts[].combatEffect beside effectSummary, with summary, trigger, mechanicalAxis, allowedPayoff, limit, and auditRequirement. Legal mechanicalAxis values are afterlife-only surfaces such as rollMode, conflictPosition, controlState, sideStrain, tempoAdvantage, counterPayoff, actionEconomy, actionCostAudit, or combatConditions. Preserve baseOperation, read combatEffect before applying a named special art, and record the applied trigger/payoff through specialArtAudit.effectNote or specialArtAudits[].effectNote; never use Mortal HP/status effects, unlimited passive bonuses, or tactical-matrix bypasses. Legacy profiles with only effectSummary remain readable."
$script:WeatherContractDirective = " Weather contract: if you write game_state/world/weather.json direct root or game_state/world/current_location.json.normalizedWeatherState, the weather object MUST keep both non-empty description and canonical tendency (IMPROVE, WORSEN, NO_CHANGE, or a valid JUMP_TO_* command). Do not wait for weather_direct_state_missing_required_fields repair; preserve/add description and tendency before writing the terminal marker."

# Resolve paths
if (!(Test-Path $GameSessionPath)) { New-Item -ItemType Directory -Path $GameSessionPath -Force | Out-Null }
$GameSessionPath = (Resolve-Path $GameSessionPath).Path

$InputDir  = Join-Path $GameSessionPath "input"
$ReadyDir  = Join-Path $GameSessionPath "ready"
$OutputDir = Join-Path $GameSessionPath "output"
$ControlDir = Join-Path $GameSessionPath "game_state\control"
$TurnRequestFile = Join-Path $InputDir "turn_request.json"
$PendingTurnSnapshotManifestFile = Join-Path $ControlDir "pending_turn_snapshot.json"
$PendingTurnSnapshotAuthorityFile = Join-Path $ControlDir "pending_turn_snapshot.authority.json"
$RepairRequestFile = Join-Path $ControlDir "validation_repair_request.json"
$TerminalProtocolFailureRequestFile = Join-Path $ControlDir "terminal_protocol_failure_request.json"
$CliBindingFile = Join-Path $ControlDir "gm_cli_window_binding.json"
$BridgeStatusFile = Join-Path $ControlDir "gm_bridge_status.json"
$BridgeControlScript = Join-Path $PSScriptRoot "Launcher\bookofeternity.ps1"
$script:GmTrajectoryLedgerPath = Join-Path $ControlDir "gm_trajectory_ledger.jsonl"

foreach ($dir in @($InputDir, $ReadyDir, $OutputDir, $ControlDir)) {
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

$script:GmTurnHelperBootstrapPath = Join-Path $ControlDir "gm_turn_helper.bootstrap.ps1"
$script:GmTurnHelperDirective = " GM turn helper: dot-source '$script:GmTurnHelperBootstrapPath' before writing output/state files. Use Read-BoeJson -RelativePath '<file>' for JSON reads, Write-BoeJson -RelativePath '<file>' -Data <object> for JSON writes, Get-BoeJsonValue -Object <jsonObject> -Names @('NPCId','npcId','id') for optional or differently cased JSON fields, Set-BoeJsonProperty -Object <jsonObject> -Name '<field>' -Value <value> to add or update optional object properties, and Add-BoeJsonArrayItem -Object <jsonObject> -PropertyName '<arrayProperty>' -Item <object> -UniqueBy '<idField>' when adding/upserting JSON array entries. PowerShell collapses single JSON array items into scalars, so prefer Add-BoeJsonArrayItem over manual `$array += ...` for fields such as customProperties, entries, objectives, contents, journalEntries, and similar collections. Use Complete-BoeTurn -FilesModified @('<file>') as the LAST action for successful turns, Fail-BoeTurn -ErrorMessage '<reason>' as the LAST action for terminal errors, and Complete-BoeValidationRepair as the LAST action after validation repairs. Fail-BoeTurn writes ready/turn_error.json and then fails the shell command deliberately, so do not report success after calling it. These helpers copy exact sessionId/requestId/turnNumber from the current client-authored request and refuse stale missing context. Helper writes and filesModified reject client-owned runtime state such as input/turn_request.json, game_state/history/chat_log.json, pending_turn_snapshot files, validation_repair_request.json, terminal_protocol_failure_request.json, gm_bridge_status.json, and stories/*.jsonl; let the client maintain those surfaces. When currentRealm is Chaos Sea or Shining Abode, helper writes and filesModified also reject wrong-realm Mortal World profile paths under game_state/world, game_state/npcs, game_state/factions, game_state/player, game_state/inventory, game_state/combat, and game_state/quests. Never delete or rewrite input/turn_request.json; it is client-owned authority until the client closes the wait cycle."
$script:GmContextPackRoot = Join-Path $ControlDir "gm_context_pack"
$script:GmContextPackManifestPath = Join-Path $script:GmContextPackRoot "context_pack_manifest.json"
$script:GmContextPackDirective = ""
$script:CompactTurnOutputTemplatePath = Join-Path $script:GmContextPackRoot "Templates\TURN_OUTPUT_TEMPLATE.md"
$script:CompactValidationRepairTemplatePath = Join-Path $script:GmContextPackRoot "Templates\VALIDATION_REPAIR_TEMPLATE.md"
$script:CompactProgressionReportTemplatePath = Join-Path $script:GmContextPackRoot "Templates\PROGRESSION_REPORT_TEMPLATE.json"
$script:CompactActorReasoningTemplatePath = Join-Path $script:GmContextPackRoot "Templates\ACTOR_REASONING_TEMPLATE.md"
$script:CompactTempoAdvantageTemplatePath = Join-Path $script:GmContextPackRoot "Templates\AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json"
$script:GmCompactTemplateDirective = ""

function Quote-PowerShellSingleQuotedString {
    param([string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function New-StringFromCodePoints {
    param([int[]]$CodePoints)

    return [string]::Concat(($CodePoints | ForEach-Object { [char]$_ }))
}

$script:ActorSituationLabel = New-StringFromCodePoints @(0x0421, 0x0438, 0x0442, 0x0443, 0x0430, 0x0446, 0x0438, 0x044F)
$script:ActorThoughtsLabel = New-StringFromCodePoints @(0x041C, 0x044B, 0x0441, 0x043B, 0x0438)
$script:ActorActionsLabel = New-StringFromCodePoints @(0x0414, 0x0435, 0x0439, 0x0441, 0x0442, 0x0432, 0x0438, 0x044F)

function Write-GmTurnHelperBootstrap {
    $helperPath = Join-Path $PSScriptRoot "Launcher\GM_Turn_Helper.ps1"
    $content = @(
        ". $(Quote-PowerShellSingleQuotedString $helperPath)",
        "Initialize-BoeGmTurnHelper -GameSessionPath $(Quote-PowerShellSingleQuotedString $GameSessionPath)"
    ) -join [Environment]::NewLine

    Set-Content -LiteralPath $script:GmTurnHelperBootstrapPath -Value ($content + [Environment]::NewLine) -Encoding UTF8
}

function Copy-GmContextPackFile {
    param(
        [string]$RelativePath,
        [string]$Role
    )

    $sourcePath = Join-Path $script:RepoRootPath $RelativePath
    if (!(Test-Path $sourcePath)) {
        return $null
    }

    $destinationPath = Join-Path $script:GmContextPackRoot $RelativePath
    $destinationDir = Split-Path -Parent $destinationPath
    if (!(Test-Path $destinationDir)) {
        New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
    }

    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force

    return [ordered]@{
        role = $Role
        relativePath = $RelativePath.Replace("\", "/")
        sourcePath = (Resolve-Path $sourcePath).Path
        sessionPath = $destinationPath
    }
}

function Write-GmContextPackTemplate {
    param(
        [string]$RelativePath,
        [string]$Role,
        [string]$Content
    )

    $templatePath = Join-Path $script:GmContextPackRoot $RelativePath
    $templateDir = Split-Path -Parent $templatePath
    if (!(Test-Path $templateDir)) {
        New-Item -ItemType Directory -Path $templateDir -Force | Out-Null
    }

    Set-Content -LiteralPath $templatePath -Value ($Content + [Environment]::NewLine) -Encoding UTF8

    return [ordered]@{
        role = $Role
        relativePath = $RelativePath.Replace("\", "/")
        generated = $true
        sessionPath = $templatePath
    }
}

function Write-GmContextPack {
    if (!(Test-Path $script:GmContextPackRoot)) {
        New-Item -ItemType Directory -Path $script:GmContextPackRoot -Force | Out-Null
    }

    $docSpecs = @(
        @("CLI_Agent_Daemon_Specification.md", "daemon_protocol"),
        @("TaskGuides\CLI_Step_Main.txt", "main_turn_guide"),
        @("Examples\E_CLI_Step_Main.txt", "main_turn_example"),
        @("Examples\E_CLI_Afterlife_Turns.txt", "afterlife_turn_examples"),
        @("OtherGuides\Afterlife_Contract_Matrix.md", "afterlife_contract_matrix"),
        @("OtherGuides\Afterlife_Combat_Terminology_Glossary.md", "afterlife_combat_glossary"),
        @("Examples\E_CLI_Ink_Feather_Actions.txt", "ink_feather_action_example")
    )

    $docs = @()
    foreach ($docSpec in $docSpecs) {
        $copied = Copy-GmContextPackFile -RelativePath $docSpec[0] -Role $docSpec[1]
        if ($null -ne $copied) {
            $docs += $copied
        }
    }

    $templates = @()
    $turnOutputTemplate = @'
# Compact Turn Output Template

Use this before opening large examples for ordinary live turns.

## Required flow

1. Dot-source `game_state/control/gm_turn_helper.bootstrap.ps1`.
2. Read `input/turn_request.json`, `game_state/meta/soul_state.json`, and the minimal state files needed for the current realm.
3. Write player-facing output first, then structured state/output files.
4. Finish with `Complete-BoeTurn -FilesModified @(...)` as the last command.

## Minimal files

- `output/narrative_response.json`: player-facing scene text, choices, visible consequences.
- `output/debug_logs.json`: short GM audit with scope declaration and actor reasoning.
- `output/interface_updates.json`: UI hints only when useful.
- `game_state/control/progression_report.json`: only when `progressionControl` says scheduler work is due.

## Output file skeletons

`output/narrative_response.json`:

```json
{
  "response": "<player-facing prose>",
  "timestamp": "<ISO 8601 UTC timestamp>"
}
```

`output/debug_logs.json`:

```json
{
  "gm_thoughts_markdown": "## NPC Scope\n- Mode: Scene-local | World-progression | Guardian-centric | Mixed\n- Relevant actors: <actors or none>\n- Why relevant: <why these actors matter>\n- Actors outside scope: <actors or none>\n- Why outside scope: <why excluded actors do not matter>\n\n## Reasoning\n### <actor if any>\n- __ACTOR_SITUATION_LABEL__: ...\n- __ACTOR_THOUGHTS_LABEL__: ...\n- __ACTOR_ACTIONS_LABEL__: ...",
  "timestamp": "<ISO 8601 UTC timestamp>"
}
```

`output/interface_updates.json`:

```json
{
  "dialogueOptions": [
    {
      "text": "<player-facing option text>",
      "category": "neutral"
    }
  ],
  "timestamp": "<ISO 8601 UTC timestamp>"
}
```

## Terminal rule

Never write terminal files manually. Use:

```powershell
Complete-BoeTurn -FilesModified @(
  'output/narrative_response.json',
  'output/debug_logs.json',
  'output/interface_updates.json'
)
```

Add every state/output file you changed. Do not include client-owned files:
`input/turn_request.json`, `pending_turn_snapshot*`, `validation_repair_request.json`,
`terminal_protocol_failure_request.json`, `gm_bridge_status.json`, or `stories/*.jsonl`.
'@
    $turnOutputTemplate = $turnOutputTemplate.Replace("__ACTOR_SITUATION_LABEL__", $script:ActorSituationLabel).Replace("__ACTOR_THOUGHTS_LABEL__", $script:ActorThoughtsLabel).Replace("__ACTOR_ACTIONS_LABEL__", $script:ActorActionsLabel)
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\TURN_OUTPUT_TEMPLATE.md" -Role "compact_turn_output_template" -Content $turnOutputTemplate
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\VALIDATION_REPAIR_TEMPLATE.md" -Role "compact_validation_repair_template" -Content @'
# Compact Validation Repair Template

Use this before opening large examples for repair mode.

## Required flow

1. Dot-source `game_state/control/gm_turn_helper.bootstrap.ps1`.
2. Read `game_state/control/validation_repair_request.json`.
3. Prefer `harnessRepairPackets[]`; they are the executable repair plan.
4. Patch only already written files named by errors or packets.
5. Do not create a new turn and do not write `ready/turn_complete.json`.
6. Finish with `Complete-BoeValidationRepair` as the last command.

## Repair packet discipline

- Keep `sessionId`, `requestId`, and `turnNumber` from the current repair request.
- If diagnostic-only sentinel metadata is present, restore the missing authority first.
- If `harnessRepairPackets[]` names exact fields, fix those fields instead of searching source code.
- If a wrong-realm auto-rollback report exists, treat it as diagnostic evidence, not permission to rewrite mortal files from afterlife.

## Terminal rule

```powershell
Complete-BoeValidationRepair
```
'@
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\PROGRESSION_REPORT_TEMPLATE.json" -Role "compact_progression_report_template" -Content @'
{
  "progressionProcessingReport": {
    "sessionId": "<copy exact input/turn_request.json.sessionId>",
    "requestId": "<copy exact input/turn_request.json.requestId>",
    "turnNumber": 0,
    "worldCyclesProcessed": 0,
    "factionCyclesProcessed": 0,
    "chaosSeaCyclesProcessed": 0,
    "guardianProjectCyclesProcessed": 0,
    "residentAgencyCyclesProcessed": 0,
    "shiningAbodeCyclesProcessed": 0,
    "shiningFactionCyclesProcessed": 0,
    "shiningTradeCyclesProcessed": 0,
    "newLastWorldSimulationTimeInMinutes": 0,
    "newLastFactionSimulationTimeInMinutes": 0,
    "newLastChaosSeaSimulationOrdinal": 0,
    "newLastGuardianProjectCycleOrdinal": 0,
    "newLastResidentAgencyCycleOrdinal": 0,
    "newLastShiningAbodeCycleOrdinal": 0,
    "newLastShiningFactionCycleOrdinal": 0,
    "newLastShiningTradeCycleOrdinal": 0,
    "afterlifeCatchupProcessed": false,
    "afterlifeCatchupSummaryEventsProcessed": 0
  }
}
'@
    $actorReasoningTemplate = @'
# Compact Actor Reasoning Template

Use this before opening large examples when `gm_thoughts_markdown` or structured actor coverage is needed.
Use only validator-supported scope modes: `Scene-local`, `World-progression`, `Guardian-centric`, or `Mixed`.
Do not invent any other scope mode.

## Output shape

```markdown
## NPC Scope
- Mode: Scene-local | World-progression | Guardian-centric | Mixed
- Files changed: output/narrative_response.json, ...
- Relevant actors: <comma-separated actors, or none>
- Why relevant: <why these actors matter for this turn>
- Actors outside scope: <actors deliberately excluded, or none>
- Why outside scope: <why excluded actors do not matter>

## Reasoning
### <exact actor display name from state or packet>
- __ACTOR_SITUATION_LABEL__: what situation/current position makes this actor relevant.
- __ACTOR_THOUGHTS_LABEL__: what the actor wants, fears, evaluates, or decides internally.
- __ACTOR_ACTIONS_LABEL__: what changed, what the actor did, or why no state change was needed.

### <next exact actor display name>
- __ACTOR_SITUATION_LABEL__: ...
- __ACTOR_THOUGHTS_LABEL__: ...
- __ACTOR_ACTIONS_LABEL__: ...
```

Use exact actor names from state/validation packets. Keep punctuation stable; do not add punctuation that is not present in the canonical actor name.
'@
    $actorReasoningTemplate = $actorReasoningTemplate.Replace("__ACTOR_SITUATION_LABEL__", $script:ActorSituationLabel).Replace("__ACTOR_THOUGHTS_LABEL__", $script:ActorThoughtsLabel).Replace("__ACTOR_ACTIONS_LABEL__", $script:ActorActionsLabel)
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\ACTOR_REASONING_TEMPLATE.md" -Role "compact_actor_reasoning_template" -Content $actorReasoningTemplate
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json" -Role "compact_tempo_advantage_template" -Content @'
{
  "tempoAdvantage": {
    "advantageId": "tempo_guard_<turn>_<short_id>",
    "sourceId": "exchange_<id>",
    "ownerSide": "player",
    "sourceOperation": "guard",
    "sourceExchangeId": "exchange_<id>",
    "status": "available",
    "level": "advantage",
    "summary": "<Russian player-facing summary of the one-use tempo window created by a successful guard>"
  },
  "consumeThroughRollMode": {
    "effectiveMode": "advantage",
    "advantageSources": [
      {
        "summary": "<Russian player-facing summary of the tempo window>",
        "source": "tempoAdvantage",
        "sourceId": "tempo_guard_<turn>_<short_id>",
        "sourceType": "guard_tempo_window",
        "level": "advantage"
      }
    ],
    "disadvantageSources": []
  }
}
'@

    $readmePath = Join-Path $script:GmContextPackRoot "README.md"
    $readme = @"
# GM Session Context Pack

This folder is generated for the current live game session.

Start here instead of browsing repository implementation code.

- Read context_pack_manifest.json first.
- Bootstrap scope: read only context_pack_manifest.json and README.md.
- Do not open copied guides/examples during bootstrap; they are large and route-specific.
- Use Templates/* before opening large copied examples for common turn, repair, progression, actor reasoning, and tempoAdvantage shapes.
- Open large copied docs only when a per-turn, repair, or terminal-failure prompt explicitly names them.
- Use '$($script:GmTurnHelperBootstrapPath)' for safe JSON writes and terminal signals.
- During normal play or validation repair, do not read implementation code such as BookOfEternityClient/**/*.cs.
- If validation repair is requested, use game_state/control/validation_repair_request.json, especially harnessRepairPackets[].
"@
    Set-Content -LiteralPath $readmePath -Value ($readme + [Environment]::NewLine) -Encoding UTF8

    $manifest = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        gameSessionPath = $GameSessionPath
        repoRootPath = $script:RepoRootPath
        contextPackRoot = $script:GmContextPackRoot
        manifestPath = $script:GmContextPackManifestPath
        readmePath = $readmePath
        turnHelperBootstrapPath = $script:GmTurnHelperBootstrapPath
        docs = $docs
        templates = $templates
        rules = @(
            "Bootstrap scope: read only context_pack_manifest.json and README.md.",
            "Do not open copied guides/examples during bootstrap; open them only when a per-turn, repair, or terminal-failure prompt explicitly names them.",
            "Use compact Templates/* before opening large copied examples for common turn, repair, progression, actor reasoning, and tempoAdvantage field names.",
            "Use session-local copied GM docs/examples before repository files when a turn/repair prompt names those docs.",
            "Do not read implementation code such as BookOfEternityClient/**/*.cs during normal play or validation repair.",
            "For validation repair, prefer validation_repair_request.json.harnessRepairPackets, session state/control files, and helper commands over source-code archaeology."
        )
    }

    Set-Content -LiteralPath $script:GmContextPackManifestPath -Value ($manifest | ConvertTo-Json -Depth 8) -Encoding UTF8

    $script:TaskGuideMainPath = Join-Path $script:GmContextPackRoot "TaskGuides\CLI_Step_Main.txt"
    $script:ExampleMainPath = Join-Path $script:GmContextPackRoot "Examples\E_CLI_Step_Main.txt"
    $script:AfterlifeMatrixPath = Join-Path $script:GmContextPackRoot "OtherGuides\Afterlife_Contract_Matrix.md"
    $script:AfterlifeTurnsExamplePath = Join-Path $script:GmContextPackRoot "Examples\E_CLI_Afterlife_Turns.txt"
    $script:AfterlifeCombatGlossaryPath = Join-Path $script:GmContextPackRoot "OtherGuides\Afterlife_Combat_Terminology_Glossary.md"
    $script:InkFeatherExamplePath = Join-Path $script:GmContextPackRoot "Examples\E_CLI_Ink_Feather_Actions.txt"
    $script:AfterlifeExamplesDirective = $script:AfterlifeExamplesDirective.Replace($script:RepoRootPath, $script:GmContextPackRoot)
    $script:GmContextPackDirective = " GM session context pack is the first authority: Manifest='$($script:GmContextPackManifestPath)', Root='$($script:GmContextPackRoot)', README='$readmePath'. Bootstrap scope: read only context_pack_manifest.json and README.md. Do not open copied guides/examples during bootstrap; open large copied docs only when a per-turn, repair, or terminal-failure prompt explicitly names them. Do not read implementation code such as BookOfEternityClient/**/*.cs during normal play or validation repair; use validation_repair_request.json.harnessRepairPackets, session state/control files, helper commands, and named copied GM docs instead."
    $script:GmDocPathDirective = " GM documentation paths are session-local and authoritative: TaskGuide='$($script:TaskGuideMainPath)', MainExample='$($script:ExampleMainPath)', AfterlifeMatrix='$($script:AfterlifeMatrixPath)', AfterlifeTurns='$($script:AfterlifeTurnsExamplePath)'. Do not search repository source or other worktrees for GM docs; use these context-pack paths first."
    $script:GmCompactTemplateDirective = " Compact GM templates are first for executable shapes: Turn='$($script:CompactTurnOutputTemplatePath)', Repair='$($script:CompactValidationRepairTemplatePath)', ProgressionReport='$($script:CompactProgressionReportTemplatePath)', ActorReasoning='$($script:CompactActorReasoningTemplatePath)', TempoAdvantage='$($script:CompactTempoAdvantageTemplatePath)'. Use these before opening large copied examples; open long examples only for route-specific contracts not covered by compact templates."
}

Write-GmTurnHelperBootstrap
Write-GmContextPack

$script:LastRepairRequestWrite = [datetime]::MinValue
$script:LastTerminalProtocolFailureWrite = [datetime]::MinValue
$script:BridgeAutoStartAttempted = $false

function New-DefaultGmWorkerBridgeProfiles {
    $runner = 'BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1'
    $codexWorker = 'codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -'

    return @(
        [ordered]@{
            workerId = "validation_repair_codex"
            displayName = "Codex validation repair"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 180"
            role = "validation-repair"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 210
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("validation-repair")
                readPaths = @("game_state/**", "lore/**", "input/**", "ready/**")
                proposalWritePaths = @("game_state/**", "lore/**", "ready/**")
                proposalOnly = $false
                requiresValidation = $true
            }
        },
        [ordered]@{
            workerId = "narrative_draft_codex"
            displayName = "Codex narrative drafter"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "narrative-draft"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("narrative-draft")
                readPaths = @("game_state/**", "lore/**", "Rules/**", "TaskGuides/**")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        },
        [ordered]@{
            workerId = "analysis_codex"
            displayName = "Codex analysis worker"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "analysis"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("analysis")
                readPaths = @("game_state/**", "lore/**", "Rules/**", "TaskGuides/**")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        }
    )
}

function Get-GameConfig {
    $configPath = Join-Path $GameSessionPath "config.json"
    $defaults = [ordered]@{
        GmBridgeEnabled = $true
        GmBridgeBackend = "ConPTYBridge"
        GmCliLaunchCommand = "codex --dangerously-bypass-approvals-and-sandbox"
        GmBridgeShellWorkingDirectory = ""
        GmBridgeAutoStart = $false
        GmBridgePipeNameOverride = ""
        GmWorkerBridgeProfiles = New-DefaultGmWorkerBridgeProfiles
    }

    if (!(Test-Path $configPath)) {
        return [pscustomobject]$defaults
    }

    try {
        $loaded = Get-Content -Path $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($key in @($defaults.Keys)) {
            if ($null -eq $loaded.$key) {
                $loaded | Add-Member -NotePropertyName $key -NotePropertyValue $defaults[$key]
            }
        }

        return $loaded
    }
    catch {
        return [pscustomobject]$defaults
    }
}

function Get-GmBridgeStatus {
    if (!(Test-Path $BridgeStatusFile)) {
        return $null
    }

    try {
        $status = Get-Content -Path $BridgeStatusFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -eq $status.helperPid) {
            return $status
        }

        try {
            $null = Get-Process -Id ([int]$status.helperPid) -ErrorAction Stop
            return $status
        }
        catch {
            Write-Log "  -> Removing stale GM bridge status file (dead helper pid)." -Level "WARN" -Color Yellow
            Remove-Item $BridgeStatusFile -Force -ErrorAction SilentlyContinue
            return $null
        }
    }
    catch {
        return $null
    }
}

function Ensure-GmBridgeStarted {
    $config = Get-GameConfig
    if (-not $config.GmBridgeEnabled -or $config.GmBridgeBackend -ne "ConPTYBridge") {
        return
    }

    if (-not $config.GmBridgeAutoStart -or $script:BridgeAutoStartAttempted) {
        return
    }

    $script:BridgeAutoStartAttempted = $true

    if (!(Test-Path $BridgeControlScript)) {
        Write-Log "  -> GM bridge control script not found. Auto-start skipped." -Level "WARN" -Color Yellow
        return
    }

    try {
        & $BridgeControlScript start-bridge -SessionPath $GameSessionPath | Out-Null
        Write-Log "  -> Requested GM bridge auto-start" -Color DarkGray
    }
    catch {
        Write-Log "  -> GM bridge auto-start failed: $_" -Level "WARN" -Color Yellow
    }
}

function Send-ToGmBridge {
    param(
        [string]$Message,
        [switch]$AllowNotReady
    )

    $config = Get-GameConfig
    if (-not $config.GmBridgeEnabled -or $config.GmBridgeBackend -ne "ConPTYBridge") {
        return $null
    }

    Ensure-GmBridgeStarted

    $status = Get-GmBridgeStatus
    if ($null -eq $status) {
        Write-Log "  -> GM bridge status file not found. Falling back." -Level "WARN" -Color Yellow
        return "bridge-unavailable"
    }

    if (-not $status.ready -and -not $AllowNotReady) {
        Write-Log "  -> GM bridge is running but not marked ready. Falling back." -Level "WARN" -Color Yellow
        return "bridge-not-ready"
    }

    if (!(Test-Path $BridgeControlScript)) {
        Write-Log "  -> GM bridge control script missing. Falling back." -Level "WARN" -Color Yellow
        return "bridge-control-missing"
    }

    try {
        if ($AllowNotReady) {
            & $BridgeControlScript addText $Message -SessionPath $GameSessionPath | Out-Null
            Start-Sleep -Milliseconds 100
            & $BridgeControlScript sendEnter -SessionPath $GameSessionPath | Out-Null
            Write-Log "  -> Sent bootstrap/reminder to GM bridge via addText+sendEnter" -Color Green
        }
        else {
            & $BridgeControlScript dispatchPrompt $Message -SessionPath $GameSessionPath | Out-Null
            Write-Log "  -> Sent to GM bridge via named pipe" -Color Green
        }
        return "sent"
    }
    catch {
        Write-Log "  -> GM bridge dispatch failed: $_" -Level "WARN" -Color Yellow
        return "bridge-failed"
    }
}

function Write-Log {
    param([string]$Message, [string]$Level = "INFO", [ConsoleColor]$Color = [ConsoleColor]::White)
    $timestamp = Get-Date -Format "HH:mm:ss"
    $logLine = "[$timestamp][$Level] $Message"
    Write-Host $logLine -ForegroundColor $Color
    if ($LogFile) {
        try { Add-Content -Path $LogFile -Value $logLine -Encoding UTF8 -ErrorAction SilentlyContinue } catch { }
    }
}

function New-GmDispatchDiagnostics {
    param(
        [string]$Status = "not_dispatched",
        [int]$Attempts = 0,
        [int]$BusyRetries = 0,
        [bool]$Timeout = $false
    )

    return [pscustomobject]@{
        Status = $Status
        Attempts = $Attempts
        BusyRetries = $BusyRetries
        Timeout = $Timeout
    }
}

function ConvertTo-GmTrajectoryRealm {
    param([string]$Realm)

    if ([string]::IsNullOrWhiteSpace($Realm)) {
        return "Unknown"
    }

    $normalized = $Realm.Trim()
    if ([string]::Equals($normalized, "Chaos Sea", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($normalized, "Море Хаоса", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "ChaosSea"
    }

    if ([string]::Equals($normalized, "Shining Abode", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($normalized, "Сияющая Обитель", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "ShiningAbode"
    }

    if ([string]::Equals($normalized, "Mortal World", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($normalized, "MortalWorld", [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($normalized, "Смертный мир", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "MortalWorld"
    }

    return "Unknown"
}

function Get-GmTrajectoryRealm {
    param([object]$RequestObject)

    if ($null -ne $RequestObject.currentRealm) {
        return ConvertTo-GmTrajectoryRealm ([string]$RequestObject.currentRealm)
    }

    if ($null -ne $RequestObject.progressionControl -and $null -ne $RequestObject.progressionControl.currentRealm) {
        return ConvertTo-GmTrajectoryRealm ([string]$RequestObject.progressionControl.currentRealm)
    }

    return "Unknown"
}

function Get-GmTrajectoryIssueKinds {
    param([object]$RequestObject)

    $kinds = @()
    if ($null -ne $RequestObject.errors) {
        foreach ($err in @($RequestObject.errors)) {
            if ($null -ne $err.code -and -not [string]::IsNullOrWhiteSpace([string]$err.code)) {
                $kinds += [string]$err.code
            }
            elseif ($null -ne $err.category -and -not [string]::IsNullOrWhiteSpace([string]$err.category)) {
                $kinds += [string]$err.category
            }
        }
    }

    return @($kinds | Select-Object -Unique)
}

function Get-GmTrajectoryRepairPacketRefs {
    param([object]$RequestObject)

    $refs = @()
    if ($null -ne $RequestObject.harnessRepairPackets) {
        foreach ($packet in @($RequestObject.harnessRepairPackets)) {
            if ($null -ne $packet.packetId -and -not [string]::IsNullOrWhiteSpace([string]$packet.packetId)) {
                $refs += [string]$packet.packetId
            }
            elseif ($null -ne $packet.id -and -not [string]::IsNullOrWhiteSpace([string]$packet.id)) {
                $refs += [string]$packet.id
            }
        }
    }

    return @($refs | Select-Object -Unique)
}

function Get-GmTrajectoryRollbackEvents {
    param([datetime]$SinceUtc)

    $reportPath = Join-Path $ControlDir "validation_auto_rollback_report.json"
    if (!(Test-Path $reportPath)) {
        return @()
    }

    try {
        $fileInfo = Get-Item $reportPath
        if ($SinceUtc -ne [datetime]::MinValue -and $fileInfo.LastWriteTimeUtc -lt $SinceUtc) {
            return @()
        }

        $report = Get-Content -Path $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $issueCount = if ($null -ne $report.issues) { @($report.issues).Count } else { 0 }
        $restoredCount = if ($null -ne $report.restoredFiles) { @($report.restoredFiles).Count } else { 0 }
        $deletedCount = if ($null -ne $report.deletedFiles) { @($report.deletedFiles).Count } else { 0 }
        if ($issueCount -eq 0 -and $restoredCount -eq 0 -and $deletedCount -eq 0) {
            return @()
        }

        return @([ordered]@{
            path = "game_state/control/validation_auto_rollback_report.json"
            issueCount = $issueCount
            restoredCount = $restoredCount
            deletedCount = $deletedCount
        })
    }
    catch {
        return @([ordered]@{
            path = "game_state/control/validation_auto_rollback_report.json"
            unreadable = $true
        })
    }
}

function ConvertTo-GmTrajectoryActionSummary {
    param([object]$RequestObject)

    if ($null -eq $RequestObject.playerAction) {
        return ""
    }

    $text = ([string]$RequestObject.playerAction).Trim()
    if ($text.Length -le 160) {
        return $text
    }

    return $text.Substring(0, 157) + "..."
}

function Get-GmTrajectoryOutputFiles {
    param([object]$TerminalSignal)

    if ($null -eq $TerminalSignal -or $null -eq $TerminalSignal.Signal -or $null -eq $TerminalSignal.Signal.filesModified) {
        return @()
    }

    $files = @()
    foreach ($file in @($TerminalSignal.Signal.filesModified)) {
        if ($null -ne $file -and -not [string]::IsNullOrWhiteSpace([string]$file)) {
            $files += ([string]$file).Replace('\', '/')
        }
    }

    return @($files | Select-Object -Unique)
}

function Write-GmTrajectoryRecord {
    param(
        [string]$Kind,
        [string]$Mode,
        [object]$RequestObject,
        [object]$Dispatch,
        [string]$ValidationStatus,
        [string[]]$IssueKinds = @(),
        [string[]]$RepairPacketRefs = @(),
        [int]$RepairAttempts = 0,
        [string]$RepairStatus = "none",
        [object]$TerminalSignal = $null,
        [datetime]$StartedAtUtc = [datetime]::MinValue,
        [nullable[double]]$DurationSeconds = $null,
        [string]$MissingHarnessTool = $null
    )

    try {
        $dispatchStatus = if ($Dispatch -and $Dispatch.Status) { [string]$Dispatch.Status } else { "not_dispatched" }
        $dispatchAttempts = if ($Dispatch -and $null -ne $Dispatch.Attempts) { [int]$Dispatch.Attempts } else { 0 }
        $dispatchBusyRetries = if ($Dispatch -and $null -ne $Dispatch.BusyRetries) { [int]$Dispatch.BusyRetries } else { 0 }
        $dispatchTimeout = if ($Dispatch -and $null -ne $Dispatch.Timeout) { [bool]$Dispatch.Timeout } else { $false }
        $rollbackEvents = @(Get-GmTrajectoryRollbackEvents -SinceUtc $StartedAtUtc)
        $rawWrongRealmWrite = $false
        foreach ($rollbackEvent in $rollbackEvents) {
            if (($rollbackEvent.issueCount -as [int]) -gt 0 -or
                ($rollbackEvent.restoredCount -as [int]) -gt 0 -or
                ($rollbackEvent.deletedCount -as [int]) -gt 0) {
                $rawWrongRealmWrite = $true
            }
        }

        $terminalKind = if ($null -ne $TerminalSignal -and $null -ne $TerminalSignal.Kind) { [string]$TerminalSignal.Kind } else { "none" }
        $terminalPath = if ($null -ne $TerminalSignal -and $null -ne $TerminalSignal.Path) {
            $relative = [string]$TerminalSignal.Path
            if ($relative.StartsWith($GameSessionPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                $relative = $relative.Substring($GameSessionPath.Length).TrimStart('\', '/')
            }
            $relative.Replace('\', '/')
        } else {
            $null
        }

        $requestId = if ($null -ne $RequestObject.requestId) { [string]$RequestObject.requestId } else { "" }
        $record = [ordered]@{
            recordId = "gmtraj_" + [guid]::NewGuid().ToString("N")
            kind = $Kind
            sessionId = if ($null -ne $RequestObject.sessionId) { [string]$RequestObject.sessionId } else { "" }
            turnId = $requestId
            requestId = $requestId
            turnNumber = if ($null -ne $RequestObject.turnNumber) { [int]$RequestObject.turnNumber } else { -1 }
            realm = Get-GmTrajectoryRealm -RequestObject $RequestObject
            mode = $Mode
            actionSummary = ConvertTo-GmTrajectoryActionSummary -RequestObject $RequestObject
            contextPackPath = "game_state/control/gm_context_pack"
            templateVersions = [ordered]@{
                turnOutput = "v1"
                validationRepair = "v1"
                progressionReport = "v1"
                actorReasoning = "v1"
                tempoAdvantage = "v1"
            }
            outputFiles = @(Get-GmTrajectoryOutputFiles -TerminalSignal $TerminalSignal)
            dispatch = [ordered]@{
                attempts = $dispatchAttempts
                busyRetries = $dispatchBusyRetries
                timeout = $dispatchTimeout
                status = $dispatchStatus
            }
            validation = [ordered]@{
                status = $ValidationStatus
                issueKinds = @($IssueKinds)
                repairPacketRefs = @($RepairPacketRefs)
            }
            repair = [ordered]@{
                attempts = $RepairAttempts
                status = $RepairStatus
            }
            workerEvents = @()
            rollbackEvents = @($rollbackEvents)
            terminal = [ordered]@{
                kind = $terminalKind
                signalPath = $terminalPath
            }
            durationSeconds = $DurationSeconds
            rubric = [ordered]@{
                validTurn = [string]::Equals($ValidationStatus, "accepted", [System.StringComparison]::OrdinalIgnoreCase)
                playerFacingOutputPresent = Test-Path (Join-Path $OutputDir "narrative_response.json")
                implementationSourceRead = $false
                rawWrongRealmWrite = $rawWrongRealmWrite
                manualReasoningNeeded = $false
                missingHarnessTool = $MissingHarnessTool
            }
            createdAt = (Get-Date).ToUniversalTime().ToString("o")
        }

        $ledgerDir = Split-Path $script:GmTrajectoryLedgerPath -Parent
        if (!(Test-Path $ledgerDir)) {
            New-Item -ItemType Directory -Path $ledgerDir -Force | Out-Null
        }

        Add-Content -Path $script:GmTrajectoryLedgerPath -Value ($record | ConvertTo-Json -Depth 8 -Compress) -Encoding UTF8
    }
    catch {
        Write-Log "  Failed to write GM trajectory ledger: $_" -Level "WARN" -Color Yellow
    }
}

function Get-TurnRequestKey {
    param([psobject]$TurnRequest)

    $sessionId = [string]$TurnRequest.sessionId
    $requestId = [string]$TurnRequest.requestId
    $turnNumber = [int]$TurnRequest.turnNumber
    return "$sessionId|$requestId|$turnNumber"
}

function Test-TurnRequestHasPendingSnapshotContext {
    param([psobject]$TurnRequest)

    if (!(Test-Path $PendingTurnSnapshotManifestFile) -or !(Test-Path $PendingTurnSnapshotAuthorityFile)) {
        return $false
    }

    try {
        $requestSessionId = [string]$TurnRequest.sessionId
        $requestId = [string]$TurnRequest.requestId
        $turnNumber = [int]$TurnRequest.turnNumber
        if ([string]::IsNullOrWhiteSpace($requestSessionId) -or [string]::IsNullOrWhiteSpace($requestId)) {
            return $false
        }

        $manifest = Get-Content -Path $PendingTurnSnapshotManifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $manifestSessionId = [string]$manifest.sessionId
        $manifestRequestId = [string]$manifest.requestId
        $manifestTurnNumber = [int]$manifest.turnNumber

        return [string]::Equals($manifestSessionId, $requestSessionId, [System.StringComparison]::OrdinalIgnoreCase) -and
            [string]::Equals($manifestRequestId, $requestId, [System.StringComparison]::OrdinalIgnoreCase) -and
            $manifestTurnNumber -eq $turnNumber
    }
    catch {
        Write-Log "  Pending turn snapshot context is unreadable: $_" -Level "WARN" -Color Yellow
        return $false
    }
}

function Test-ObservedTerminalRequestKey {
    param([string]$Key)

    if ([string]::IsNullOrWhiteSpace($Key)) {
        return $false
    }

    return $script:ObservedTerminalRequestKeys.Contains($Key)
}

function Add-ObservedTerminalRequestKey {
    param([string]$Key)

    if (![string]::IsNullOrWhiteSpace($Key)) {
        [void]$script:ObservedTerminalRequestKeys.Add($Key)
    }
}

# Banner
Write-Host ""
Write-Host "  +===============================================+" -ForegroundColor Cyan
Write-Host "  |  Book of Eternity: Game Master Daemon         |" -ForegroundColor Cyan
Write-Host "  +===============================================+" -ForegroundColor Cyan
Write-Host ""
Write-Log "Game Session : $GameSessionPath" -Color Gray
if ((Get-GameConfig).GmBridgeEnabled -and (Get-GameConfig).GmBridgeBackend -eq "ConPTYBridge") {
    Write-Log "GM Backend   : ConPTYBridge" -Color Gray
    if (Test-Path $BridgeStatusFile) {
        Write-Log "Bridge Status: '$BridgeStatusFile'" -Color Gray
    } else {
        Write-Log "Bridge Status: bridge not started yet (fallbacks remain available)" -Color Yellow
    }
}
elseif (Test-Path $CliBindingFile) {
    Write-Log "CLI Binding  : '$CliBindingFile'" -Color Gray
} elseif ($CliWindowTitle) {
    Write-Log "CLI Window   : '$CliWindowTitle' (title fallback)" -Color Gray
} else {
    Write-Log "Mode         : Clipboard only (no window targeting)" -Color Yellow
}
Write-Log "Auto-Paste   : $AutoPaste" -Color Gray
if ($AutoPaste) {
    Write-Log "Paste Mode   : $PasteMode" -Color Gray
}
if ($script:LaunchScriptPath) {
    Write-Log "Bootstrap    : '$($script:LaunchScriptPath)'" -Color Gray
}
if ($TurnTimeout -le 0) {
    Write-Log "Timeout      : disabled (wait indefinitely)" -Color Gray
}
else {
    Write-Log "Timeout      : ${TurnTimeout}s" -Color Gray
}
Write-Host ""

# ═══════════════════════════════════════════════
# CLI Window Communication
# ═══════════════════════════════════════════════

# Win32 API for window activation
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class Win32Window {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    public const int SW_RESTORE = 9;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
}
"@ -ErrorAction SilentlyContinue

function Invoke-RightClickPaste {
    param([System.IntPtr]$WindowHandle)

    $originalPoint = [Win32Window+POINT]::new()
    $null = [Win32Window]::GetCursorPos([ref]$originalPoint)

    $rect = [Win32Window+RECT]::new()
    if (-not [Win32Window]::GetWindowRect($WindowHandle, [ref]$rect)) {
        return $false
    }

    $targetX = [Math]::Max($rect.Left + 32, $rect.Left + [Math]::Floor(($rect.Right - $rect.Left) / 3))
    $targetY = [Math]::Max($rect.Top + 32, $rect.Top + [Math]::Floor(($rect.Bottom - $rect.Top) / 3))

    $null = [Win32Window]::SetCursorPos([int]$targetX, [int]$targetY)
    Start-Sleep -Milliseconds 120
    [Win32Window]::mouse_event([Win32Window]::MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 40
    [Win32Window]::mouse_event([Win32Window]::MOUSEEVENTF_RIGHTUP, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 180
    $null = [Win32Window]::SetCursorPos($originalPoint.X, $originalPoint.Y)
    return $true
}

function Get-BoundCliTarget {
    if (!(Test-Path $CliBindingFile)) {
        return $null
    }

    try {
        $binding = Get-Content -Path $CliBindingFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -eq $binding.processId -or $null -eq $binding.mainWindowHandle) {
            Write-Log "  -> Binding file is missing processId/mainWindowHandle. Falling back." -Level "WARN" -Color Yellow
            return $null
        }

        $windowHandle = [System.IntPtr]::new([int64]$binding.mainWindowHandle)
        if (-not [Win32Window]::IsWindow($windowHandle)) {
            Write-Log "  -> Binding file points to a non-existing window handle. Falling back." -Level "WARN" -Color Yellow
            return $null
        }

        $process = Get-Process -Id ([int]$binding.processId) -ErrorAction SilentlyContinue
        if (-not $process) {
            Write-Log "  -> Binding file points to a dead process. Falling back." -Level "WARN" -Color Yellow
            return $null
        }

        return [pscustomobject]@{
            Mode = "binding"
            Process = $process
            WindowHandle = $windowHandle
            Description = "binding pid=$($process.Id), handle=$([int64]$windowHandle)"
        }
    }
    catch {
        Write-Log "  -> Failed to read binding file. Falling back." -Level "WARN" -Color Yellow
        return $null
    }
}

function Get-TitleMatchedCliTarget {
    if (-not $CliWindowTitle) {
        return $null
    }

    $targetProcess = Get-Process | Where-Object { $_.MainWindowTitle -match $CliWindowTitle } | Select-Object -First 1
    if (-not $targetProcess -or $targetProcess.MainWindowHandle -eq 0) {
        return $null
    }

    return [pscustomobject]@{
        Mode = "title-fallback"
        Process = $targetProcess
        WindowHandle = $targetProcess.MainWindowHandle
        Description = "title fallback '$CliWindowTitle' -> pid=$($targetProcess.Id)"
    }
}

function Resolve-CliTarget {
    $bindingTarget = Get-BoundCliTarget
    if ($bindingTarget) {
        return $bindingTarget
    }

    return Get-TitleMatchedCliTarget
}

function Send-ToCliWindow {
    param(
        [string]$Message
    )

    $config = Get-GameConfig
    if ($config.GmBridgeEnabled -and $config.GmBridgeBackend -eq "ConPTYBridge") {
        return (Send-ToGmBridge -Message $Message)
    }

    # Clipboard is the universal fallback for every bridge/window failure path.
    Set-Clipboard -Value $Message
    Write-Log "  -> Clipboard: command copied" -Color DarkGray

    if (-not $AutoPaste) {
        [Console]::Beep(800, 200)
        [Console]::Beep(1000, 200)
        Write-Log "  -> Command copied to clipboard. Paste it manually into the CLI window using the method your terminal supports." -Color Yellow
        return "clipboard"
    }

    $target = Resolve-CliTarget
    if (-not $target) {
        Write-Log "  -> No bound CLI window found. Command left in clipboard." -Level "WARN" -Color Yellow
        [Console]::Beep(400, 300)
        return "unbound"
    }

    try {
        [Win32Window]::ShowWindow($target.WindowHandle, [Win32Window]::SW_RESTORE) | Out-Null
        Start-Sleep -Milliseconds 200
        [Win32Window]::SetForegroundWindow($target.WindowHandle) | Out-Null
        Start-Sleep -Milliseconds 350
        Write-Log "  -> Activated target via $($target.Description)" -Color DarkGray

        if ($PasteMode -eq "CtrlV") {
            [System.Windows.Forms.SendKeys]::SendWait("^v")
            Write-Log "  -> Paste sent via Ctrl+V" -Color DarkGray
        }
        elseif ($PasteMode -eq "ShiftInsert") {
            [System.Windows.Forms.SendKeys]::SendWait("+{INSERT}")
            Write-Log "  -> Paste sent via Shift+Insert" -Color DarkGray
        }
        else {
            if (-not (Invoke-RightClickPaste -WindowHandle $target.WindowHandle)) {
                throw "RightClick paste failed: could not target window coordinates."
            }
            Write-Log "  -> Paste sent via RightClick" -Color DarkGray
        }
        Start-Sleep -Milliseconds 250
        [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
        Write-Log "  -> Enter sent" -Color DarkGray

        Write-Log "  -> Sent to CLI window" -Color Green
        return "sent"
    }
    catch {
        Write-Log "  -> SendKeys failed: $_. Command in clipboard." -Level "WARN" -Color Yellow
        return "failed"
    }
}

function Ensure-CliBootstrapSent {
    if ($script:BootstrapSent) { return $true }

    $message = @"
BOOTSTRAP GM SESSION

This is bootstrap only, not an active turn.
Do NOT write ready/turn_complete.json or ready/turn_error.json yet.
A real turn prompt will explicitly reference input\turn_request.json with the current sessionId/requestId/turnNumber.
$($script:GmContextPackDirective)
$($script:GmTurnHelperDirective)

Read '$($script:GmContextPackManifestPath)' and '$($script:GmContextPackRoot)\README.md' as the session-local starting context.
Bootstrap scope: read only context_pack_manifest.json and README.md.
Do not open copied guides/examples during bootstrap.
Open large copied docs only when a per-turn, repair, or terminal-failure prompt explicitly names them.
Do not browse repository implementation code or repo-root planning documents during bootstrap.
After bootstrap, reply with exactly BOE_GM_BOOTSTRAP_READY and finish your response.
Do not keep this bootstrap request open; returning to the CLI input prompt is what lets the bridge accept the real turn.
"@
    $dispatch = Send-ToCliWindow -Message $message
    if ($dispatch -eq "sent" -or $dispatch -eq "clipboard") {
        $script:BootstrapSent = $true
        Write-Log "  -> Bootstrap launch script dispatched" -Color Green
        return $true
    }

    return $false
}

function Dispatch-WithRetry {
    param(
        [string]$Message,
        [string]$PendingPath = "",
        [switch]$ReturnDetails
    )

    $attempts = 0
    $busyRetries = 0

    while ($true) {
        if ($PendingPath -and !(Test-Path $PendingPath)) {
            if ($ReturnDetails) {
                return (New-GmDispatchDiagnostics -Status "cancelled" -Attempts $attempts -BusyRetries $busyRetries)
            }
            return "cancelled"
        }

        $attempts++
        $dispatch = Send-ToCliWindow -Message $Message
        if ($dispatch -eq "sent" -or $dispatch -eq "clipboard") {
            if ($ReturnDetails) {
                return (New-GmDispatchDiagnostics -Status $dispatch -Attempts $attempts -BusyRetries $busyRetries)
            }
            return $dispatch
        }

        if ($dispatch -like "bridge-*") {
            $busyRetries++
            Write-Log "  -> Waiting for GM bridge to become available/ready..." -Level "WARN" -Color Yellow
            Start-Sleep -Seconds 1
            continue
        }

        if ($ReturnDetails) {
            return (New-GmDispatchDiagnostics -Status $dispatch -Attempts $attempts -BusyRetries $busyRetries)
        }
        return $dispatch
    }
}

# ═══════════════════════════════════════════════
# Turn Processing
# ═══════════════════════════════════════════════

function Process-Turn {
    param([string]$RequestPath)

    if ($script:IsProcessing) { return }
    $script:IsProcessing = $true
    $script:TurnCount++
    $turnStart = Get-Date

    Start-Sleep -Milliseconds 300
    if (!(Test-Path $RequestPath)) {
        $script:IsProcessing = $false
        return
    }

    try {
        $turnRequest = Get-Content -Path $RequestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $turnNumber = $turnRequest.turnNumber
        $turnRequestKey = Get-TurnRequestKey -TurnRequest $turnRequest
        if (Test-ObservedTerminalRequestKey -Key $turnRequestKey) {
            return
        }

        if (-not (Test-TurnRequestHasPendingSnapshotContext -TurnRequest $turnRequest)) {
            Write-Log "  Skipping stale turn request without matching pending snapshot context (requestKey=$turnRequestKey)." -Level "WARN" -Color Yellow
            Add-ObservedTerminalRequestKey -Key $turnRequestKey
            return
        }

        $playerAction = if ($turnRequest.playerAction.Length -gt 80) {
            $turnRequest.playerAction.Substring(0, 77) + "..."
        } else { $turnRequest.playerAction }

        Write-Host ""
        Write-Log "Turn #${turnNumber}: $playerAction" -Level "TURN" -Color Green

        # Send processing command to CLI window
        $requestId = if ($turnRequest.requestId) { $turnRequest.requestId } else { "<missing-requestId>" }
        $message = "Process turn #$turnNumber (requestId=$requestId).$($script:GmContextPackDirective)$($script:GmDocPathDirective)$($script:GmCompactTemplateDirective)$($script:GmTurnHelperDirective) Read $GameSessionPath\input\turn_request.json and follow CLI_Agent_Daemon_Specification.md phases 0-4. You MUST read '$($script:CompactTurnOutputTemplatePath)', '$($script:CompactProgressionReportTemplatePath)', '$($script:CompactActorReasoningTemplatePath)', and '$($script:CompactTempoAdvantageTemplatePath)' before opening large copied examples. Read '$($script:TaskGuideMainPath)' for phase rules; use '$($script:ExampleMainPath)' only when compact templates do not cover a route-specific shape.$($script:AfterlifeRealmGateDirective)$($script:AfterlifeExamplesDirective)$($script:AfterlifeCombatConditionsDirective)$($script:AfterlifeSpecialArtCombatEffectDirective) $($script:WeatherContractDirective) If this turn uses any GM-side [INK_FEATHER_ACTION: TAG], you MUST also read '$($script:InkFeatherExamplePath)' and write output/ink_feather_action_result.json with exact metadata, actionTag, resolved=true, costInFeathers, resolutionType, summary, and stateEvidence. The client validates correlated metadata, valid JSON, realm restrictions, progressionControl/progression report, gm_thoughts_markdown scope/reasoning, and structured actor coverage. Relevant actors in NPC scope MUST cover any structured actor updates such as UpdateNPCs, NPCGoalUpdates, or UpdateGuardians. Use preGeneratedDices1d20 from the FIRST die for normal checks; afterlife spiritual conflicts use visible d20 values through diceAudit on contested exchange/resolve; gachaBaseResult is separate and does not consume visible dice. If playerAction contains [CHAOS_SEA_DIRECT_GACHA], treat it as a neutral direct pull from the Chaos Sea, not a Guardian-mediated pull, and preserve the exact cost phrase '<N> Чернильных Перьев' or '<N> Ink Feathers' because validation extracts prepaid cost from it. Guardian-mediated gacha is limited per Guardian per return from mortal life: Hostile=0, Wary/Neutral=1, Friendly=2, Devoted/Legendary=3. Guardian-mediated rarity upgrades are limited to Abode Power rarity ceiling bonus and completed relic_forging project bonus; Guardian reputation does not improve rarity odds. Charges reset only when the Soul returns to the Chaos Sea after a new mortal life. If a Guardian has no remaining charges this return, do NOT emit UpdateGuardians.processGacha for that Guardian. Direct /gacha remains neutral and does NOT consume Guardian charges. progressionControl in the request is authoritative. If progression is processed, write game_state/control/progression_report.json with exact sessionId/requestId/turnNumber copied from the CURRENT turn_request.json plus exact bounded processed cycle counts and new last-* markers. If progressionControl.afterlifeCatchupRequired=true, process only afterlifeCatchupSummaryEventsRequired summary outcomes and do NOT simulate raw elapsed cycles one by one. TERMINAL CHECKLIST: write EXACTLY ONE terminal signal for this request; use either ready/turn_complete.json OR ready/turn_error.json, never both; copy exact sessionId/requestId/turnNumber from the CURRENT turn_request.json; never delete or rewrite input/turn_request.json; write the terminal signal as the LAST step. If you write both terminal files or wrong metadata, the client will reject the terminal phase as protocol failure and write game_state/control/terminal_protocol_failure_request.json. validation_repair_request.json is only for accepted terminal completion with invalid resulting state."

        $completionPath = Join-Path $ReadyDir "turn_complete.json"
        $errorPath = Join-Path $ReadyDir "turn_error.json"
        $terminalSignal = Get-CorrelatedTerminalSignal -TurnRequest $turnRequest -CompletionPath $completionPath -ErrorPath $errorPath
        $dispatchDiagnostics = New-GmDispatchDiagnostics -Status "preexisting-terminal"
        $missingHarnessTool = $null

        if ($null -eq $terminalSignal) {
            $dispatchDiagnostics = Dispatch-WithRetry -Message $message -PendingPath $RequestPath -ReturnDetails
            if ($dispatchDiagnostics.Status -eq "cancelled") {
                Write-Log "  Turn cancelled while waiting for bridge turn dispatch" -Level "WARN" -Color Yellow
                Write-GmTrajectoryRecord `
                    -Kind "turn" `
                    -Mode "ordinary" `
                    -RequestObject $turnRequest `
                    -Dispatch $dispatchDiagnostics `
                    -ValidationStatus "not_run" `
                    -RepairStatus "interrupted" `
                    -StartedAtUtc $turnStart.ToUniversalTime() `
                    -DurationSeconds ((Get-Date) - $turnStart).TotalSeconds `
                    -MissingHarnessTool "turn_cancelled_before_dispatch"
                return
            }
        }
        elseif ($terminalSignal.Kind -eq "conflict") {
            Write-Log "  Detected conflicting correlated terminal signals for the same turn. Waiting stops as protocol failure; client should emit terminal_protocol_failure_request.json." -Level "ERROR" -Color Red
        }
        else {
            Write-Log "  Found correlated terminal signal already present; completing without re-dispatch" -Level "WARN" -Color Yellow
        }

        # Wait for terminal signal
        $elapsed = 0

        while ($null -eq $terminalSignal -and ($TurnTimeout -le 0 -or $elapsed -lt $TurnTimeout)) {
            Start-Sleep -Seconds 1
            $elapsed++

            if (!(Test-Path $RequestPath)) {
                Write-Log "  Turn cancelled by client" -Level "WARN" -Color Yellow
                break
            }

            if (-not (Test-TurnRequestHasPendingSnapshotContext -TurnRequest $turnRequest)) {
                Write-Log "  Turn wait closed because the client no longer has matching pending snapshot context. The client likely consumed the terminal signal first." -Level "WARN" -Color Yellow
                Add-ObservedTerminalRequestKey -Key $turnRequestKey
                break
            }

            $terminalSignal = Get-CorrelatedTerminalSignal -TurnRequest $turnRequest -CompletionPath $completionPath -ErrorPath $errorPath

            if ($elapsed % 60 -eq 0) {
                Write-Log "  Waiting... (${elapsed}s)" -Color DarkGray
            }
        }

        if ($TurnTimeout -gt 0 -and $elapsed -ge $TurnTimeout -and $null -eq $terminalSignal) {
            $script:ErrorCount++
            Write-Log "  Timeout after ${elapsed}s" -Level "ERROR" -Color Red
            $dispatchDiagnostics.Timeout = $true
            $missingHarnessTool = "gm_turn_timeout"
            $timeoutSignal = @{
                sessionId = $turnRequest.sessionId
                requestId = $turnRequest.requestId
                turnNumber = $turnNumber
                status = "error"
                timestamp = (Get-Date).ToUniversalTime().ToString("o")
                error = "Timeout after ${elapsed}s"
            }
            Set-Content -Path $errorPath -Value ($timeoutSignal | ConvertTo-Json -Depth 3) -Encoding UTF8
            $terminalSignal = [pscustomobject]@{
                Path = $errorPath
                Kind = "error"
                Signal = [pscustomobject]$timeoutSignal
            }
        }

        # Check terminal outcome
        if ($null -ne $terminalSignal -and $terminalSignal.Kind -eq "conflict") {
            $duration = ((Get-Date) - $turnStart).TotalSeconds
            Write-Log "  Terminal protocol conflict ($([math]::Round($duration, 1))s): both turn_complete.json and turn_error.json match the same request" -Level "TURN" -Color Red
            Write-GmTrajectoryRecord `
                -Kind "turn" `
                -Mode "ordinary" `
                -RequestObject $turnRequest `
                -Dispatch $dispatchDiagnostics `
                -ValidationStatus "rejected" `
                -TerminalSignal $terminalSignal `
                -StartedAtUtc $turnStart.ToUniversalTime() `
                -DurationSeconds $duration `
                -MissingHarnessTool "terminal_signal_conflict"
        }
        elseif ($null -ne $terminalSignal -and $terminalSignal.Kind -eq "success") {
            $duration = ((Get-Date) - $turnStart).TotalSeconds
            Write-Log "  Done ($([math]::Round($duration, 1))s)" -Level "TURN" -Color Green
            Write-GmTrajectoryRecord `
                -Kind "turn" `
                -Mode "ordinary" `
                -RequestObject $turnRequest `
                -Dispatch $dispatchDiagnostics `
                -ValidationStatus "accepted" `
                -TerminalSignal $terminalSignal `
                -StartedAtUtc $turnStart.ToUniversalTime() `
                -DurationSeconds $duration
        }
        elseif ($null -ne $terminalSignal -and $terminalSignal.Kind -eq "error") {
            $duration = ((Get-Date) - $turnStart).TotalSeconds
            try {
                $errorSignal = $terminalSignal.Signal
                $errorMessage = if ($errorSignal.error) { $errorSignal.error } else { "Unknown GM error" }
                Write-Log "  Terminal error ($([math]::Round($duration, 1))s): $errorMessage" -Level "TURN" -Color Yellow
            }
            catch {
                Write-Log "  Terminal error ($([math]::Round($duration, 1))s): unreadable turn_error.json" -Level "TURN" -Color Yellow
            }
            Write-GmTrajectoryRecord `
                -Kind "turn" `
                -Mode "ordinary" `
                -RequestObject $turnRequest `
                -Dispatch $dispatchDiagnostics `
                -ValidationStatus "rejected" `
                -TerminalSignal $terminalSignal `
                -StartedAtUtc $turnStart.ToUniversalTime() `
                -DurationSeconds $duration `
                -MissingHarnessTool $missingHarnessTool
        }

        if ($null -ne $terminalSignal) {
            Add-ObservedTerminalRequestKey -Key $turnRequestKey
        }
    }
    catch {
        $script:ErrorCount++
        Write-Log "  Error: $_" -Level "ERROR" -Color Red
    }
    finally {
        $script:IsProcessing = $false
    }
}

function Process-RepairRequest {
    param([string]$RepairPath)

    if (!(Test-Path $RepairPath)) { return }

    try {
        $fileInfo = Get-Item $RepairPath
        if ($fileInfo.LastWriteTimeUtc -le $script:LastRepairRequestWrite) { return }
        $script:LastRepairRequestWrite = $fileInfo.LastWriteTimeUtc

        $repair = Get-Content -Path $RepairPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $turnNumber = if ($repair.turnNumber) { [int]$repair.turnNumber } else { -1 }
        $requestId = if ($repair.requestId) { $repair.requestId } else { "<missing-requestId>" }
        $attempt = if ($repair.revalidationAttempt) { [int]$repair.revalidationAttempt } else { 1 }
        $hasDiagnosticOnlyMetadata = Test-ProtocolRequestUsesDiagnosticOnlyMetadata -RequestObject $repair

        $summary = @()
        if ($repair.summaryGroups) {
            foreach ($group in @($repair.summaryGroups | Select-Object -First 6)) {
                if ($group) { $summary += "- $group" }
            }
        }

        $topErrors = @()
        if ($repair.errors) {
            foreach ($err in @($repair.errors | Select-Object -First 5)) {
                $code = if ($err.code) { $err.code } else { "validation_error" }
                $category = if ($err.category) { $err.category } else { "StateConsistency" }
                $section = if ($err.section) { $err.section } else { "General" }
                $msg = if ($err.message) { $err.message } else { "Unknown validation error" }
                $expected = if ($err.expected) { " Expected: $($err.expected)" } else { "" }
                $actual = if ($err.actual) { " Actual: $($err.actual)" } else { "" }
                $hint = if ($err.repairHint) { " Hint: $($err.repairHint)" } else { "" }
                $topErrors += "- [$category/$section/$code] $msg$expected$actual$hint"
            }
        }

        $readyPath = "$GameSessionPath\game_state\control\validation_repair_ready.json"
        $message = "REPAIR MODE for rejected turn #$turnNumber (requestId=$requestId, attempt=$attempt).$($script:GmContextPackDirective)$($script:GmDocPathDirective)$($script:GmCompactTemplateDirective)$($script:GmTurnHelperDirective) You MUST reread $GameSessionPath\game_state\control\validation_repair_request.json and '$($script:CompactValidationRepairTemplatePath)' before opening large copied examples. Also use '$($script:CompactActorReasoningTemplatePath)' for actor coverage repairs and prefer validation_repair_request.json.harnessRepairPackets over source-code archaeology. Read '$($script:TaskGuideMainPath)' for repair phase rules; use '$($script:ExampleMainPath)' only when compact templates do not cover a route-specific shape.$($script:AfterlifeRealmGateDirective)$($script:AfterlifeExamplesDirective)$($script:AfterlifeCombatConditionsDirective)$($script:AfterlifeSpecialArtCombatEffectDirective) Fix only the listed validation errors in the already written files IN PLACE. Do NOT create a new turn. Do NOT run unrelated git or repository tasks. Do NOT wait for another prompt after files are fixed; finish the repair protocol immediately. never write ready/turn_complete.json for repair."
        if ($hasDiagnosticOnlyMetadata) {
            $message += " The current repair request marks sessionId/requestId/turnNumber as diagnostic-only sentinel values because validated pending snapshot context is unavailable or invalid. Do NOT copy those sentinel metadata into $readyPath. First restore pending snapshot context/authority and then use the freshest client-authored repair request with valid metadata before writing validation_repair_ready.json."
        }
        else {
            $message += " Terminal marker: after the files are fixed, create $readyPath with matching sessionId/requestId/turnNumber copied from the CURRENT repair request as the LAST action. If the files already satisfy the listed errors, create $readyPath immediately as the LAST action."
        }
        $message += " If your ready file is malformed or mismatched, the client will reject it and rewrite the repair request again. $($script:WeatherContractDirective)"
        if ($summary.Count -gt 0) {
            $message += "`nMain groups:`n" + ($summary -join "`n")
        }
        if ($topErrors.Count -gt 0) {
            $message += "`nTop issues:`n" + ($topErrors -join "`n")
        }

        Write-Host ""
        Write-Log "Repair request for turn #$turnNumber (attempt $attempt)" -Level "REPAIR" -Color Yellow

        $dispatchDiagnostics = Dispatch-WithRetry -Message $message -PendingPath $RepairPath -ReturnDetails
        Write-GmTrajectoryRecord `
            -Kind "repair" `
            -Mode "validation_repair" `
            -RequestObject $repair `
            -Dispatch $dispatchDiagnostics `
            -ValidationStatus "rejected" `
            -IssueKinds (Get-GmTrajectoryIssueKinds -RequestObject $repair) `
            -RepairPacketRefs (Get-GmTrajectoryRepairPacketRefs -RequestObject $repair) `
            -RepairAttempts $attempt `
            -RepairStatus "requested" `
            -StartedAtUtc $fileInfo.LastWriteTimeUtc
    }
    catch {
        $script:ErrorCount++
        Write-Log "Repair watcher error: $_" -Level "ERROR" -Color Red
    }
}

function Get-LaunchScriptPath {
    if (-not [string]::IsNullOrWhiteSpace($LaunchScriptPath) -and (Test-Path $LaunchScriptPath)) {
        return (Resolve-Path $LaunchScriptPath).Path
    }

    $candidates = @(
        (Join-Path $PSScriptRoot "Launcher\CLI_Launch_Script.md"),
        (Join-Path (Split-Path $PSScriptRoot -Parent) "Launcher\CLI_Launch_Script.md"),
        (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "Launcher\CLI_Launch_Script.md")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

$script:LaunchScriptPath = Get-LaunchScriptPath

function Test-ProtocolRequestUsesDiagnosticOnlyMetadata {
    param([object]$RequestObject)

    if ($null -ne $RequestObject.metadataDiagnosticOnly) {
        return [bool]$RequestObject.metadataDiagnosticOnly
    }

    # Legacy fallback for requests written before metadataDiagnosticOnly was added.
    return $RequestObject.gmInstructions -and $RequestObject.gmInstructions.Contains("служат только для диагностики")
}

function Process-TerminalProtocolFailureRequest {
    param([string]$FailurePath)

    if (!(Test-Path $FailurePath)) { return }

    try {
        $fileInfo = Get-Item $FailurePath
        if ($fileInfo.LastWriteTimeUtc -le $script:LastTerminalProtocolFailureWrite) { return }
        $script:LastTerminalProtocolFailureWrite = $fileInfo.LastWriteTimeUtc

        $failure = Get-Content -Path $FailurePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $turnNumber = if ($failure.turnNumber) { [int]$failure.turnNumber } else { -1 }
        $requestId = if ($failure.requestId) { $failure.requestId } else { "<missing-requestId>" }
        $hasDiagnosticOnlyMetadata = Test-ProtocolRequestUsesDiagnosticOnlyMetadata -RequestObject $failure

        $summary = @()
        if ($failure.summaryGroups) {
            foreach ($group in @($failure.summaryGroups | Select-Object -First 6)) {
                if ($group) { $summary += "- $group" }
            }
        }

        $topErrors = @()
        if ($failure.errors) {
            foreach ($err in @($failure.errors | Select-Object -First 5)) {
                $code = if ($err.code) { $err.code } else { "terminal_protocol_failure" }
                $category = if ($err.category) { $err.category } else { "ProtocolViolation" }
                $section = if ($err.section) { $err.section } else { "General" }
                $msg = if ($err.message) { $err.message } else { "Unknown terminal protocol failure" }
                $expected = if ($err.expected) { " Expected: $($err.expected)" } else { "" }
                $actual = if ($err.actual) { " Actual: $($err.actual)" } else { "" }
                $hint = if ($err.repairHint) { " Hint: $($err.repairHint)" } else { "" }
                $topErrors += "- [$category/$section/$code] $msg$expected$actual$hint"
            }
        }

        $message = "Terminal protocol failure for turn #$turnNumber (requestId=$requestId).$($script:GmContextPackDirective)$($script:GmDocPathDirective)$($script:GmCompactTemplateDirective)$($script:GmTurnHelperDirective) You MUST reread $GameSessionPath\game_state\control\terminal_protocol_failure_request.json and '$($script:CompactValidationRepairTemplatePath)' before opening large copied examples. Read '$($script:TaskGuideMainPath)' for terminal protocol phase rules; use '$($script:ExampleMainPath)' only when compact templates do not cover a route-specific shape.$($script:AfterlifeRealmGateDirective)$($script:AfterlifeExamplesDirective)$($script:AfterlifeCombatConditionsDirective)$($script:AfterlifeSpecialArtCombatEffectDirective) This is NOT validation_repair_request.json and NOT a repair loop. The client already closed the current wait cycle. Do NOT create validation_repair_ready.json. Do NOT create a new turn on your own. Fix your terminal-signal discipline for the NEXT correct client request: exactly one terminal signal, terminal signal written last, never both turn_complete and turn_error for one request."
        if ($hasDiagnosticOnlyMetadata) {
            $message += " The sessionId/requestId/turnNumber in this terminal protocol failure request are diagnostic-only sentinel values because validated pending snapshot context is unavailable or invalid. Do NOT treat them as authoritative correlation metadata for the next step; restore pending snapshot context/authority first and then wait for the freshest correct client request."
        }
        else {
            $message += " Keep exact sessionId/requestId/turnNumber discipline for the NEXT correct client request."
        }
        if ($summary.Count -gt 0) {
            $message += "`nMain groups:`n" + ($summary -join "`n")
        }
        if ($topErrors.Count -gt 0) {
            $message += "`nTop issues:`n" + ($topErrors -join "`n")
        }

        Write-Host ""
        Write-Log "Terminal protocol failure for turn #$turnNumber" -Level "PROTOCOL" -Color Red

        $dispatchDiagnostics = Dispatch-WithRetry -Message $message -PendingPath $FailurePath -ReturnDetails
        Write-GmTrajectoryRecord `
            -Kind "terminal" `
            -Mode "terminal_protocol" `
            -RequestObject $failure `
            -Dispatch $dispatchDiagnostics `
            -ValidationStatus "rejected" `
            -IssueKinds (Get-GmTrajectoryIssueKinds -RequestObject $failure) `
            -RepairPacketRefs (Get-GmTrajectoryRepairPacketRefs -RequestObject $failure) `
            -RepairAttempts 0 `
            -RepairStatus "none" `
            -StartedAtUtc $fileInfo.LastWriteTimeUtc `
            -MissingHarnessTool "terminal_protocol_failure"
    }
    catch {
        $script:ErrorCount++
        Write-Log "Terminal protocol failure watcher error: $_" -Level "ERROR" -Color Red
    }
}

function Read-ReadySignal {
    param(
        [string]$Path,
        [int]$MaxAttempts = 3,
        [int]$DelayMs = 150
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        if (!(Test-Path $Path)) {
            return $null
        }

        try {
            $content = Get-Content -Path $Path -Raw -Encoding UTF8
            if ([string]::IsNullOrWhiteSpace($content)) {
                throw "Ready signal file is empty."
            }

            $signal = $content | ConvertFrom-Json
            if ($null -eq $signal.sessionId -or $null -eq $signal.requestId -or $null -eq $signal.turnNumber) {
                throw "Ready signal metadata is incomplete."
            }

            return $signal
        }
        catch {
            if ($attempt -lt $MaxAttempts) {
                Start-Sleep -Milliseconds $DelayMs
                continue
            }

            return $null
        }
    }

    return $null
}

function Get-CorrelatedTerminalSignal {
    param(
        [psobject]$TurnRequest,
        [string]$CompletionPath,
        [string]$ErrorPath
    )

    $expectedSessionId = [string]$TurnRequest.sessionId
    $expectedRequestId = [string]$TurnRequest.requestId
    $expectedTurnNumber = [int]$TurnRequest.turnNumber

    $candidates = @(
        @{ Path = $CompletionPath; Kind = "success" },
        @{ Path = $ErrorPath; Kind = "error" }
    )

    $matchedSignals = @()

    foreach ($candidate in $candidates) {
        $path = $candidate.Path
        if (!(Test-Path $path)) {
            continue
        }

        $signal = Read-ReadySignal -Path $path
        $fileName = Split-Path $path -Leaf
        if ($null -eq $signal) {
            Write-Log "  Removed unreadable terminal signal artifact: $fileName" -Level "WARN" -Color Yellow
            Remove-Item $path -Force -ErrorAction SilentlyContinue
            continue
        }

        $isMatch =
            ([string]$signal.sessionId -eq $expectedSessionId) -and
            ([string]$signal.requestId -eq $expectedRequestId) -and
            ([int]$signal.turnNumber -eq $expectedTurnNumber)

        if ($isMatch) {
            $matchedSignals += [pscustomobject]@{
                Path = $path
                Kind = $candidate.Kind
                Signal = $signal
            }
            continue
        }

        Write-Log "  Removed stale terminal signal artifact: $fileName (sessionId/requestId/turnNumber mismatch)" -Level "WARN" -Color Yellow
        Remove-Item $path -Force -ErrorAction SilentlyContinue
    }

    if ($matchedSignals.Count -gt 1) {
        return [pscustomobject]@{
            Kind = "conflict"
            Signals = $matchedSignals
        }
    }

    if ($matchedSignals.Count -eq 1) {
        return $matchedSignals[0]
    }

    return $null
}

# ═══════════════════════════════════════════════
# FileSystemWatcher & Main Loop
# ═══════════════════════════════════════════════

$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $InputDir
$watcher.Filter = "turn_request.json"
$watcher.IncludeSubdirectories = $false
$watcher.NotifyFilter = [System.IO.NotifyFilters]::FileName -bor [System.IO.NotifyFilters]::CreationTime -bor [System.IO.NotifyFilters]::LastWrite
$watcher.EnableRaisingEvents = $true

$repairWatcher = New-Object System.IO.FileSystemWatcher
$repairWatcher.Path = $ControlDir
$repairWatcher.Filter = "validation_repair_request.json"
$repairWatcher.IncludeSubdirectories = $false
$repairWatcher.NotifyFilter = [System.IO.NotifyFilters]::FileName -bor [System.IO.NotifyFilters]::CreationTime -bor [System.IO.NotifyFilters]::LastWrite
$repairWatcher.EnableRaisingEvents = $true

$terminalProtocolFailureWatcher = New-Object System.IO.FileSystemWatcher
$terminalProtocolFailureWatcher.Path = $ControlDir
$terminalProtocolFailureWatcher.Filter = "terminal_protocol_failure_request.json"
$terminalProtocolFailureWatcher.IncludeSubdirectories = $false
$terminalProtocolFailureWatcher.NotifyFilter = [System.IO.NotifyFilters]::FileName -bor [System.IO.NotifyFilters]::CreationTime -bor [System.IO.NotifyFilters]::LastWrite
$terminalProtocolFailureWatcher.EnableRaisingEvents = $true

$action = {
    $path = $Event.SourceEventArgs.FullPath
    if ($Event.SourceEventArgs.ChangeType -eq "Created" -or $Event.SourceEventArgs.ChangeType -eq "Changed") {
        Process-Turn -RequestPath $path
    }
}

$repairAction = {
    $path = $Event.SourceEventArgs.FullPath
    if ($Event.SourceEventArgs.ChangeType -eq "Created" -or $Event.SourceEventArgs.ChangeType -eq "Changed") {
        Process-RepairRequest -RepairPath $path
    }
}

$terminalProtocolFailureAction = {
    $path = $Event.SourceEventArgs.FullPath
    if ($Event.SourceEventArgs.ChangeType -eq "Created" -or $Event.SourceEventArgs.ChangeType -eq "Changed") {
        Process-TerminalProtocolFailureRequest -FailurePath $path
    }
}

Register-ObjectEvent $watcher "Created" -Action $action | Out-Null
Register-ObjectEvent $watcher "Changed" -Action $action | Out-Null
Register-ObjectEvent $repairWatcher "Created" -Action $repairAction | Out-Null
Register-ObjectEvent $repairWatcher "Changed" -Action $repairAction | Out-Null
Register-ObjectEvent $terminalProtocolFailureWatcher "Created" -Action $terminalProtocolFailureAction | Out-Null
Register-ObjectEvent $terminalProtocolFailureWatcher "Changed" -Action $terminalProtocolFailureAction | Out-Null

Write-Log "Watching: $InputDir" -Color DarkGray

try {
    if (-not $CliWindowTitle) {
        Write-Host ""
        Write-Host "  +-------------------------------------------------+" -ForegroundColor Yellow
        Write-Host "  |  CLIPBOARD MODE                                  |" -ForegroundColor Yellow
        Write-Host "  |  When a turn arrives, the command is copied to   |" -ForegroundColor Yellow
        Write-Host "  |  clipboard. Paste it manually into your CLI.    |" -ForegroundColor Yellow
        Write-Host "  |                                                   |" -ForegroundColor Yellow
        Write-Host "  |  For auto-paste, use:                            |" -ForegroundColor Yellow
        Write-Host "  |  -CliWindowTitle 'claude' -AutoPaste             |" -ForegroundColor Yellow
        Write-Host "  +-------------------------------------------------+" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Log "Waiting for turns... (Ctrl+C to stop)" -Color Yellow
    Write-Host ""

    # Process existing request if any
    if (Test-Path $TurnRequestFile) {
        Write-Log "Found pending turn request" -Level "STARTUP" -Color Yellow
        Process-Turn -RequestPath $TurnRequestFile
    }
    if (Test-Path $RepairRequestFile) {
        Write-Log "Found pending repair request" -Level "STARTUP" -Color Yellow
        Process-RepairRequest -RepairPath $RepairRequestFile
    }
    if (Test-Path $TerminalProtocolFailureRequestFile) {
        Write-Log "Found pending terminal protocol failure request" -Level "STARTUP" -Color Yellow
        Process-TerminalProtocolFailureRequest -FailurePath $TerminalProtocolFailureRequestFile
    }

    # Main loop
    $statusTimer = 0
    while ($true) {
        Start-Sleep -Milliseconds $PollingInterval

        if ((Test-Path $TurnRequestFile) -and !$script:IsProcessing) {
            Process-Turn -RequestPath $TurnRequestFile
        }
        if (Test-Path $RepairRequestFile) {
            Process-RepairRequest -RepairPath $RepairRequestFile
        }
        if (Test-Path $TerminalProtocolFailureRequestFile) {
            Process-TerminalProtocolFailureRequest -FailurePath $TerminalProtocolFailureRequestFile
        }

        # Status every 5 minutes
        $statusTimer += $PollingInterval
        if ($statusTimer -ge 300000) {
            $uptime = ((Get-Date) - $script:StartTime)
            Write-Log "Status: ${script:TurnCount} turns, ${script:ErrorCount} errors, uptime $([math]::Floor($uptime.TotalHours))h$($uptime.Minutes)m" -Color DarkGray
            $statusTimer = 0
        }
    }
}
finally {
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
    $repairWatcher.EnableRaisingEvents = $false
    $repairWatcher.Dispose()
    $terminalProtocolFailureWatcher.EnableRaisingEvents = $false
    $terminalProtocolFailureWatcher.Dispose()
    Write-Host ""
    Write-Log "Daemon stopped. Turns: $($script:TurnCount), Errors: $($script:ErrorCount)" -Level "SHUTDOWN" -Color Yellow
}
