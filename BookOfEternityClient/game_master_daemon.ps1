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
$DaemonStatusFile = Join-Path $ControlDir "gm_daemon_status.json"
$DaemonFatalErrorFile = Join-Path $ControlDir "gm_daemon_fatal_error.json"
$ObservedTerminalRequestKeysFile = Join-Path $ControlDir "gm_observed_terminal_requests.json"
$TimeoutBridgeCleanupFile = Join-Path $ControlDir "gm_timeout_bridge_cleanup.json"
$ArtifactWriteStallReportFile = Join-Path $ControlDir "gm_artifact_write_stall_report.json"
$OutputWithoutTerminalReportFile = Join-Path $ControlDir "gm_output_without_terminal_report.json"
$ValidationRepairArtifactStallReportFile = Join-Path $ControlDir "gm_validation_repair_artifact_stall_report.json"
$BridgeControlScript = Join-Path $PSScriptRoot "Launcher\bookofeternity.ps1"
$script:GmTrajectoryLedgerPath = Join-Path $ControlDir "gm_trajectory_ledger.jsonl"
$script:BridgeDispatchMaxWaitSeconds = 60
$script:ArtifactWritingStallMinimumSeconds = 120
$script:ArtifactWritingStallNoProgressSeconds = 180
$script:OutputWithoutTerminalMinimumSeconds = 120
$script:OutputWithoutTerminalNoProgressSeconds = 90
$script:ValidationRepairArtifactStallMinimumSeconds = 120
$script:ValidationRepairArtifactStallNoProgressSeconds = 180
$script:ActiveValidationRepairWatch = $null
$script:DaemonCommandLine = [Environment]::CommandLine
$script:DaemonLastHeartbeatUtc = [DateTime]::MinValue
$script:DaemonFatalError = $null
$script:DaemonLastLoopError = $null
$script:CorrelatedRepairGraceMilliseconds = 5000
$script:CorrelatedRepairPollMilliseconds = 200

foreach ($dir in @($InputDir, $ReadyDir, $OutputDir, $ControlDir)) {
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

$script:GmTurnHelperBootstrapPath = Join-Path $ControlDir "gm_turn_helper.bootstrap.ps1"
$script:GmTurnHelperDirective = " GM turn helper: dot-source '$script:GmTurnHelperBootstrapPath' before writing output/state files. Use Read-BoeJson -RelativePath '<file>' for JSON reads; it returns mutable JSON-like objects that preserve arrays and tolerate missing optional fields added with `$object.newField = <value>. Use Write-BoeJson -RelativePath '<file>' -Data <object> for JSON writes, Get-BoeJsonValue -Object <jsonObject> -Names @('NPCId','npcId','id','initialId') for optional or differently cased JSON fields, Set-BoeJsonProperty -Object <jsonObject> -Name '<field>' -Value <value> to add or update optional object properties, and Add-BoeJsonArrayItem -Object <jsonObject> -PropertyName '<arrayProperty>' -Item <value> -UniqueBy '<idField>' when adding/upserting JSON array entries. For Mortal NPC trade pending stock, prefer Complete-BoeNpcTradeInventoryRequest -RequestId '<requestId>' -Items <items>; it finds NPCs by NPCId/npcId/id/initialId, validates itemData.tradeItemClass, recalculates slot prices from pricingTradeTier, writes npc.tradeInventory, and adds the matching UpdateNpcTradeInventoryReceipts entry. PowerShell collapses single JSON array items into scalars, so prefer Add-BoeJsonArrayItem over manual `$array += ...` for fields such as customProperties, entries, objectives, contents, journalEntries, and similar collections. For item journalEntries specifically, pass plain non-empty player-facing strings as -Item values without technical turn anchors such as '#[3].'; item journalEntries is a string array, not an array of objects. For NPCJournals in game_state/npcs/npc_journals.json, journalEntries is different: use objects with a non-empty description field, plus optional timestamp, event, emotionalImpact, and relationshipChange. Use Complete-BoeTurn -FilesModified @('<file>') as the LAST action for successful turns, Fail-BoeTurn -ErrorMessage '<reason>' as the LAST action for terminal errors, and Complete-BoeValidationRepair as the LAST action after validation repairs. Fail-BoeTurn writes ready/turn_error.json and then fails the shell command deliberately, so do not report success after calling it. After any terminal signal for the current request exists, the helper blocks further Write-BoeJson, Complete-BoeTurn, and Fail-BoeTurn calls; stop working on that request and wait for the client rollback/cleanup cycle instead of trying to repair it in-place. These helpers copy exact sessionId/requestId/turnNumber from the current client-authored request and refuse stale missing context. Complete-BoeTurn and Fail-BoeTurn also require current game_state/control/pending_turn_snapshot.json plus game_state/control/pending_turn_snapshot.authority.json with matching sessionId/requestId/turnNumber; if that pending authority is missing, stop and do not write a terminal signal. Helper writes and filesModified reject client-owned runtime state such as input/turn_request.json, game_state/history/chat_log.json, pending_turn_snapshot files, validation_repair_request.json, validation_diagnostic_failure_report.json, terminal_protocol_failure_request.json, gm_bridge_status.json, and stories/*.jsonl; let the client maintain those surfaces. When currentRealm is Chaos Sea or Shining Abode, helper writes and filesModified reject wrong-realm Mortal World profile paths under game_state/world, game_state/npcs, game_state/factions, game_state/player, game_state/inventory, game_state/combat, and game_state/quests; Complete-BoeTurn and Complete-BoeValidationRepair also compare these paths with game_state/control/pending_turn_snapshot and block raw wrong-realm mutations before writing completion signals. Never delete or rewrite input/turn_request.json or pending_turn_snapshot files; they are client-owned authority until the client closes the wait cycle."
$script:GmContextPackRoot = Join-Path $ControlDir "gm_context_pack"
$script:GmContextPackManifestPath = Join-Path $script:GmContextPackRoot "context_pack_manifest.json"
$script:GmContextPackDirective = ""
$script:CompactTurnOutputTemplatePath = Join-Path $script:GmContextPackRoot "Templates\TURN_OUTPUT_TEMPLATE.md"
$script:CompactValidationRepairTemplatePath = Join-Path $script:GmContextPackRoot "Templates\VALIDATION_REPAIR_TEMPLATE.md"
$script:CompactOutputArtifactRepairTemplatePath = Join-Path $script:GmContextPackRoot "Templates\OUTPUT_ARTIFACT_REPAIR_TEMPLATE.md"
$script:CompactProgressionReportTemplatePath = Join-Path $script:GmContextPackRoot "Templates\PROGRESSION_REPORT_TEMPLATE.json"
$script:CompactActorReasoningTemplatePath = Join-Path $script:GmContextPackRoot "Templates\ACTOR_REASONING_TEMPLATE.md"
$script:CompactMortalNpcTemplatePath = Join-Path $script:GmContextPackRoot "Templates\MORTAL_NPC_UPDATE_TEMPLATE.md"
$script:CompactMortalFactionTemplatePath = Join-Path $script:GmContextPackRoot "Templates\MORTAL_FACTION_UPDATE_TEMPLATE.md"
$script:CompactMortalLocationTemplatePath = Join-Path $script:GmContextPackRoot "Templates\MORTAL_LOCATION_TRANSITION_TEMPLATE.md"
$script:CompactMortalSkillTemplatePath = Join-Path $script:GmContextPackRoot "Templates\MORTAL_SKILL_PROGRESSION_TEMPLATE.md"
$script:CompactMortalExperienceTemplatePath = Join-Path $script:GmContextPackRoot "Templates\MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md"
$script:CompactMortalCombatTemplatePath = Join-Path $script:GmContextPackRoot "Templates\MORTAL_COMBAT_STATE_TEMPLATE.md"
$script:CompactAfterlifeChronicleTemplatePath = Join-Path $script:GmContextPackRoot "Templates\AFTERLIFE_CHRONICLE_TEMPLATE.md"
$script:CompactTempoAdvantageTemplatePath = Join-Path $script:GmContextPackRoot "Templates\AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json"
$script:GmExperienceLessonJsonPath = Join-Path $script:GmContextPackRoot "Lessons\GM_EXPERIENCE_LESSONS.json"
$script:GmExperienceLessonMarkdownPath = Join-Path $script:GmContextPackRoot "Lessons\GM_EXPERIENCE_LESSONS.md"
$script:GmSafeProbeJsonPath = Join-Path $script:GmContextPackRoot "Probes\GM_SAFE_PROBES.json"
$script:GmSafeProbeMarkdownPath = Join-Path $script:GmContextPackRoot "Probes\GM_SAFE_PROBES.md"
$script:GmLiveTestRubricJsonPath = Join-Path $script:GmContextPackRoot "Rubrics\GM_LIVE_TEST_RUBRIC.json"
$script:GmLiveTestRubricMarkdownPath = Join-Path $script:GmContextPackRoot "Rubrics\GM_LIVE_TEST_RUBRIC.md"
$script:GmCompactTemplateDirective = ""
$script:GmExperienceLessonsDirective = ""
$script:GmSafeProbeDirective = ""
$script:GmSourceFallbackDirective = ""
$script:GmLiveTestRubricDirective = ""

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
$script:ActorLocationLabel = (New-StringFromCodePoints @(0x0422, 0x0435, 0x043A, 0x0443, 0x0449, 0x0430, 0x044F, 0x20, 0x043B, 0x043E, 0x043A, 0x0430, 0x0446, 0x0438, 0x044F)) + " / Current location"

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

function ConvertTo-GmExperienceRealm {
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

function Get-GmExperienceObjectRealm {
    param([object]$Object)

    if ($null -ne $Object.currentRealm) {
        return ConvertTo-GmExperienceRealm ([string]$Object.currentRealm)
    }

    if ($null -ne $Object.realm) {
        return ConvertTo-GmExperienceRealm ([string]$Object.realm)
    }

    if ($null -ne $Object.progressionControl -and $null -ne $Object.progressionControl.currentRealm) {
        return ConvertTo-GmExperienceRealm ([string]$Object.progressionControl.currentRealm)
    }

    return "Unknown"
}

function Test-GmExperienceIssueDescribesItemDurabilityPercentage {
    param([object]$Issue)

    if ($null -eq $Issue) {
        return $false
    }

    $pathParts = @()
    foreach ($name in @("path", "filePath", "Path", "FilePath")) {
        $prop = $Issue.PSObject.Properties[$name]
        if ($null -ne $prop -and $null -ne $prop.Value -and -not [string]::IsNullOrWhiteSpace([string]$prop.Value)) {
            $pathParts += [string]$prop.Value
        }
    }

    $messageParts = @()
    foreach ($name in @("message", "repairHint", "expected", "Message", "RepairHint", "Expected")) {
        $prop = $Issue.PSObject.Properties[$name]
        if ($null -ne $prop -and $null -ne $prop.Value -and -not [string]::IsNullOrWhiteSpace([string]$prop.Value)) {
            $messageParts += [string]$prop.Value
        }
    }

    $pathText = ($pathParts -join " ").ToLowerInvariant()
    $messageText = ($messageParts -join " ").ToLowerInvariant()
    return $pathText.Contains("game_state/inventory/items.json") -and
        $pathText.Contains("durability") -and
        $messageText.Contains("percentage string")
}

function Test-GmExperienceIssueDescribesItemJournalEntriesStringArray {
    param([object]$Issue)

    if ($null -eq $Issue) {
        return $false
    }

    $pathParts = @()
    foreach ($name in @("path", "filePath", "Path", "FilePath")) {
        $prop = $Issue.PSObject.Properties[$name]
        if ($null -ne $prop -and $null -ne $prop.Value -and -not [string]::IsNullOrWhiteSpace([string]$prop.Value)) {
            $pathParts += [string]$prop.Value
        }
    }

    $messageParts = @()
    foreach ($name in @("code", "message", "repairHint", "expected", "actual", "Code", "Message", "RepairHint", "Expected", "Actual")) {
        $prop = $Issue.PSObject.Properties[$name]
        if ($null -ne $prop -and $null -ne $prop.Value -and -not [string]::IsNullOrWhiteSpace([string]$prop.Value)) {
            $messageParts += [string]$prop.Value
        }
    }

    $pathText = ($pathParts -join " ").ToLowerInvariant()
    $messageText = ($messageParts -join " ").ToLowerInvariant()
    return $pathText.Contains("game_state/inventory/items.json") -and
        $pathText.Contains("journalentries") -and
        ($messageText.Contains("invalid_string_array_item") -or
            $messageText.Contains("non-empty string") -or
            $messageText.Contains("непустой строк"))
}

function Test-GmExperienceIssueDescribesNarrativeResponseAfterlifeChronicleWrongSurface {
    param([object]$Issue)

    if ($null -eq $Issue) {
        return $false
    }

    $pathParts = @()
    foreach ($name in @("path", "filePath", "Path", "FilePath")) {
        $prop = $Issue.PSObject.Properties[$name]
        if ($null -ne $prop -and $null -ne $prop.Value -and -not [string]::IsNullOrWhiteSpace([string]$prop.Value)) {
            $pathParts += [string]$prop.Value
        }
    }

    $messageParts = @()
    foreach ($name in @("code", "message", "repairHint", "expected", "actual", "Code", "Message", "RepairHint", "Expected", "Actual")) {
        $prop = $Issue.PSObject.Properties[$name]
        if ($null -ne $prop -and $null -ne $prop.Value -and -not [string]::IsNullOrWhiteSpace([string]$prop.Value)) {
            $messageParts += [string]$prop.Value
        }
    }

    $pathText = ($pathParts -join " ").ToLowerInvariant()
    $messageText = ($messageParts -join " ").ToLowerInvariant()
    return $pathText.Contains("output/narrative_response.json") -and
        ($pathText.Contains("afterlifechronicleupdates") -or $messageText.Contains("afterlifechronicleupdates")) -and
        ($messageText.Contains("narrative_response_unknown_field") -or $messageText.Contains("unsupported field") -or $messageText.Contains("response | timestamp"))
}

function Get-GmExperienceIssueKinds {
    param([object]$Object)

    $issueKinds = @()
    if ($null -ne $Object.errors) {
        foreach ($err in @($Object.errors)) {
            if ($null -ne $err.code -and -not [string]::IsNullOrWhiteSpace([string]$err.code)) {
                $issueKinds += [string]$err.code
            }
            elseif ($null -ne $err.category -and -not [string]::IsNullOrWhiteSpace([string]$err.category)) {
                $issueKinds += [string]$err.category
            }
            if (Test-GmExperienceIssueDescribesItemDurabilityPercentage -Issue $err) {
                $issueKinds += "item_durability_percentage_string"
            }
            if (Test-GmExperienceIssueDescribesItemJournalEntriesStringArray -Issue $err) {
                $issueKinds += "item_journal_entries_string_array"
            }
            if (Test-GmExperienceIssueDescribesNarrativeResponseAfterlifeChronicleWrongSurface -Issue $err) {
                $issueKinds += "narrative_response_afterlife_chronicle_wrong_surface"
            }
        }
    }

    if ($null -ne $Object.validation -and $null -ne $Object.validation.issueKinds) {
        foreach ($issueKind in @($Object.validation.issueKinds)) {
            if ($null -ne $issueKind -and -not [string]::IsNullOrWhiteSpace([string]$issueKind)) {
                $issueKinds += [string]$issueKind
            }
        }
    }

    if ($null -ne $Object.validation -and $null -ne $Object.validation.diagnostics) {
        foreach ($diag in @($Object.validation.diagnostics)) {
            if ($null -ne $diag.code -and -not [string]::IsNullOrWhiteSpace([string]$diag.code)) {
                $issueKinds += [string]$diag.code
            }
            elseif ($null -ne $diag.category -and -not [string]::IsNullOrWhiteSpace([string]$diag.category)) {
                $issueKinds += [string]$diag.category
            }
            if (Test-GmExperienceIssueDescribesItemDurabilityPercentage -Issue $diag) {
                $issueKinds += "item_durability_percentage_string"
            }
            if (Test-GmExperienceIssueDescribesItemJournalEntriesStringArray -Issue $diag) {
                $issueKinds += "item_journal_entries_string_array"
            }
            if (Test-GmExperienceIssueDescribesNarrativeResponseAfterlifeChronicleWrongSurface -Issue $diag) {
                $issueKinds += "narrative_response_afterlife_chronicle_wrong_surface"
            }
        }
    }

    if ($null -ne $Object.rubric -and
        $null -ne $Object.rubric.missingHarnessTool -and
        -not [string]::IsNullOrWhiteSpace([string]$Object.rubric.missingHarnessTool)) {
        $issueKinds += [string]$Object.rubric.missingHarnessTool
    }

    if ($null -ne $Object.terminal -and
        $null -ne $Object.terminal.signal -and
        $null -ne $Object.terminal.signal.harnessSource -and
        -not [string]::IsNullOrWhiteSpace([string]$Object.terminal.signal.harnessSource)) {
        $issueKinds += [string]$Object.terminal.signal.harnessSource
    }

    return @($issueKinds | Select-Object -Unique)
}

function Get-GmExperienceQuery {
    if (Test-Path $RepairRequestFile) {
        try {
            $repair = Get-Content -Path $RepairRequestFile -Raw -Encoding UTF8 | ConvertFrom-Json
            return [ordered]@{
                realm = Get-GmExperienceObjectRealm -Object $repair
                mode = "validation_repair"
                issueKinds = @(Get-GmExperienceIssueKinds -Object $repair)
                taskTypes = @()
            }
        }
        catch { }
    }

    if (Test-Path $TerminalProtocolFailureRequestFile) {
        try {
            $failure = Get-Content -Path $TerminalProtocolFailureRequestFile -Raw -Encoding UTF8 | ConvertFrom-Json
            return [ordered]@{
                realm = Get-GmExperienceObjectRealm -Object $failure
                mode = "terminal_protocol"
                issueKinds = @(Get-GmExperienceIssueKinds -Object $failure)
                taskTypes = @()
            }
        }
        catch { }
    }

    if (Test-Path $TurnRequestFile) {
        try {
            $turnRequest = Get-Content -Path $TurnRequestFile -Raw -Encoding UTF8 | ConvertFrom-Json
            return [ordered]@{
                realm = Get-GmExperienceObjectRealm -Object $turnRequest
                mode = "ordinary"
                issueKinds = @()
                taskTypes = @()
            }
        }
        catch { }
    }

    return [ordered]@{
        realm = "Unknown"
        mode = "ordinary"
        issueKinds = @()
        taskTypes = @()
    }
}

function Test-GmExperienceRecordMatchesQuery {
    param(
        [object]$Record,
        [object]$Query
    )

    $recordRealm = Get-GmExperienceObjectRealm -Object $Record
    if ($Query.realm -ne "Unknown" -and $recordRealm -ne "Unknown" -and $recordRealm -ne $Query.realm) {
        return $false
    }

    if ($Query.mode -eq "validation_repair" -and [string]$Record.mode -ne "validation_repair") {
        return $false
    }

    if ($Query.mode -eq "terminal_protocol" -and [string]$Record.mode -ne "terminal_protocol") {
        return $false
    }

    $queryIssues = @($Query.issueKinds)
    if ($queryIssues.Count -gt 0) {
        $recordIssues = @(Get-GmExperienceIssueKinds -Object $Record)
        $matched = $false
        foreach ($issue in $queryIssues) {
            if ($recordIssues -contains $issue) {
                $matched = $true
                break
            }
        }
        if (-not $matched) {
            return $false
        }
    }

    return $true
}

function Get-GmExperiencePreferredSurface {
    param([string[]]$IssueKinds)

    $joined = (($IssueKinds | ForEach-Object { [string]$_ }) -join " ").ToLowerInvariant()
    if ($joined.Contains("gm_bridge_idle_without_terminal_signal") -or
        $joined.Contains("gm_output_without_terminal_signal")) {
        return "TURN_OUTPUT_TEMPLATE.md"
    }

    if ($joined.Contains("faction_full_object") -or
        $joined.Contains("canonical_faction_sidecar") -or
        $joined.Contains("faction_command_unknown_faction_id") -or
        $joined.Contains("unknown_faction") -or
        $joined.Contains("faction_identity")) {
        return "MORTAL_FACTION_UPDATE_TEMPLATE.md"
    }

    if ($joined.Contains("current_location_unknown_location_id") -or
        $joined.Contains("npc_unknown_current_location_id") -or
        $joined.Contains("world_map_new_location") -or
        $joined.Contains("world_map_adjacency") -or
        $joined.Contains("world_map_link") -or
        $joined.Contains("world_map_storage") -or
        $joined.Contains("world_map_threat") -or
        $joined.Contains("mortal_location_transition")) {
        return "MORTAL_LOCATION_TRANSITION_TEMPLATE.md"
    }

    if ($joined.Contains("mortal_relevant_actor_missing_persistence") -or
        $joined.Contains("npc_") -or
        $joined.Contains("npc.") -or
        $joined.Contains("current_location") -or
        $joined.Contains("missing_actor_current_location")) {
        return "MORTAL_NPC_UPDATE_TEMPLATE.md"
    }

    if ($joined.Contains("actor") -or $joined.Contains("reasoning") -or $joined.Contains("npc_scope")) {
        return "ACTOR_REASONING_TEMPLATE.md"
    }

    if ($joined.Contains("skill") -or $joined.Contains("mastery")) {
        return "MORTAL_SKILL_PROGRESSION_TEMPLATE.md"
    }

    if ($joined.Contains("game_state/player/experience") -or
        $joined.Contains("experiencegained") -or
        $joined.Contains("currentexperience") -or
        $joined.Contains("experiencefornextlevel") -or
        $joined.Contains("playerlevel") -or
        $joined.Contains("level-up") -or
        $joined.Contains("level_up") -or
        $joined.Contains("stat_points") -or
        $joined.Contains("stat points") -or
        $joined.Contains("statsincreased")) {
        return "MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md"
    }

    if ($joined.Contains("mortal_combat_state_missing") -or
        $joined.Contains("mortal_combat") -or
        $joined.Contains("combat_state") -or
        $joined.Contains("combat_log")) {
        return "MORTAL_COMBAT_STATE_TEMPLATE.md"
    }

    if ($joined.Contains("afterlife_conflict_reward") -or
        $joined.Contains("afterlife_entity_profile") -or
        $joined.Contains("special_art_learning")) {
        return "VALIDATION_REPAIR_TEMPLATE.md"
    }

    if ($joined.Contains("progression")) {
        return "PROGRESSION_REPORT_TEMPLATE.json"
    }

    if ($joined.Contains("narrative_response_afterlife_chronicle_wrong_surface")) {
        return "AFTERLIFE_CHRONICLE_TEMPLATE.md"
    }

    if ($joined.Contains("accepted_turn_stale_player_facing_output_after_canonical_repair")) {
        return "OUTPUT_ARTIFACT_REPAIR_TEMPLATE.md"
    }

    if ($joined.Contains("narrative_response_unknown_field") -or
        $joined.Contains("narrative_response_missing_timestamp") -or
        $joined.Contains("missing_gm_thoughts") -or
        $joined.Contains("debug_logs_unknown_field") -or
        $joined.Contains("debug_logs_missing_timestamp") -or
        $joined.Contains("interface_updates_missing_payload") -or
        $joined.Contains("interface_updates_unknown_field") -or
        $joined.Contains("interface_updates_missing_timestamp")) {
        return "TURN_OUTPUT_TEMPLATE.md"
    }

    if ($joined.Contains("terminal")) {
        return "VALIDATION_REPAIR_TEMPLATE.md"
    }

    return "VALIDATION_REPAIR_TEMPLATE.md"
}

function New-GmExperienceLesson {
    param([object]$Record)

    $issueKinds = @(Get-GmExperienceIssueKinds -Object $Record)
    $preferredSurface = Get-GmExperiencePreferredSurface -IssueKinds $issueKinds
    $recordId = if ($Record.recordId) { [string]$Record.recordId } else { "unknown" }
    $sourceRecordIds = @($recordId)
    $repairPacketRefs = if ($null -ne $Record.validation -and $null -ne $Record.validation.repairPacketRefs) {
        @($Record.validation.repairPacketRefs)
    } else {
        @()
    }
    $issueSummary = if ($issueKinds.Count -gt 0) { ($issueKinds -join ", ") } else { "unclassified harness friction" }
    $hasMortalNpcIssue = $preferredSurface -eq "MORTAL_NPC_UPDATE_TEMPLATE.md"
    $hasMortalFactionIssue = $preferredSurface -eq "MORTAL_FACTION_UPDATE_TEMPLATE.md"
    $hasMortalLocationIssue = $preferredSurface -eq "MORTAL_LOCATION_TRANSITION_TEMPLATE.md"
    $joinedIssues = ($issueKinds -join " ").ToLowerInvariant()
    $hasMortalItemDurabilityIssue = $joinedIssues.Contains("item_durability_percentage_string") -or
        $joinedIssues.Contains("item_missing_durability")
    $hasMortalItemJournalEntriesIssue = $joinedIssues.Contains("item_journal_entries_string_array")
    $hasNarrativeResponseAfterlifeChronicleWrongSurface = $joinedIssues.Contains("narrative_response_afterlife_chronicle_wrong_surface")
    $hasMortalSkillIssue = $preferredSurface -eq "MORTAL_SKILL_PROGRESSION_TEMPLATE.md"
    $hasMortalExperienceIssue = $preferredSurface -eq "MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md"
    $hasMortalCombatIssue = $preferredSurface -eq "MORTAL_COMBAT_STATE_TEMPLATE.md"
    $hasIdleWithoutTerminalSignal = $joinedIssues.Contains("gm_bridge_idle_without_terminal_signal")
    $hasStalePlayerFacingOutputIssue = $joinedIssues.Contains("accepted_turn_stale_player_facing_output_after_canonical_repair")
    $repairPacketText = (($repairPacketRefs | ForEach-Object { [string]$_ }) -join " ").ToLowerInvariant()
    $hasGenericTurnOutputArtifactIssue = -not $hasNarrativeResponseAfterlifeChronicleWrongSurface -and (
        $repairPacketText.Contains("accepted_turn_output_artifact_repair") -or
        $joinedIssues.Contains("narrative_response_unknown_field") -or
        $joinedIssues.Contains("narrative_response_missing_timestamp") -or
        $joinedIssues.Contains("missing_gm_thoughts") -or
        $joinedIssues.Contains("debug_logs_unknown_field") -or
        $joinedIssues.Contains("debug_logs_missing_timestamp") -or
        $joinedIssues.Contains("interface_updates_missing_payload") -or
        $joinedIssues.Contains("interface_updates_unknown_field") -or
        $joinedIssues.Contains("interface_updates_missing_timestamp"))
    $hasAfterlifeConflictRewardIssue = $joinedIssues.Contains("afterlife_conflict_reward") -or
        $repairPacketText.Contains("afterlife_spiritual_conflict_reward_repair")
    $hasAfterlifeEntityProfileScaffoldIssue = $joinedIssues.Contains("afterlife_entity_profile") -or
        $joinedIssues.Contains("special_art_learning") -or
        $repairPacketText.Contains("afterlife_entity_profile_scaffold_repair")
    $hasGuardianPendingCreationMaterializationIssue = $joinedIssues.Contains("guardian_materialized_without_create_surface") -or
        $joinedIssues.Contains("stale_pending_guardian_creation_after_materialization") -or
        $joinedIssues.Contains("pending_guardian_creation_missing_materialized_guardian") -or
        $joinedIssues.Contains("pending_guardian_creation_unresolved_after_startup_turn") -or
        $repairPacketText.Contains("guardian_pending_creation_materialization_repair")
    $hasGuardianTradeInventoryResolutionIssue = $joinedIssues.Contains("guardian_trade_request_missing_guardian_resolution") -or
        $joinedIssues.Contains("guardian_trade_request_missing_inventory_resolution") -or
        $joinedIssues.Contains("guardian_trade_request_missing_receipt_resolution") -or
        $repairPacketText.Contains("guardian_trade_inventory_resolution_repair")
    $fix = if ($hasIdleWithoutTerminalSignal) {
        "Use FIRST MORTAL BOOTSTRAP OUTPUT CHECKLIST and TURN_OUTPUT_TEMPLATE.md before broad docs. For first Mortal bootstrap, open game_state/control/mortal_bootstrap_scaffold.json and write the minimum complete output set: output/narrative_response.json, output/debug_logs.json with NPC Scope, output/interface_updates.json with clear choices, game_state/control/progression_report.json if progression counts are required, then finish with Complete-BoeTurn -FilesModified. Do not open large examples first and do not leave the player with no scene."
    }
    elseif ($hasMortalLocationIssue) {
        "Use MORTAL_LOCATION_TRANSITION_TEMPLATE.md before editing game_state/world/current_location.json, game_state/world/world_map.json, or NPC location ids. Register durable destination locations in world_map first, then update current_location and NPC currentLocationId/currentLocationName only to known ids. Every world_map adjacency/link/storage/threat target must point to an existing locationId or a same-turn newLocations.initialId that is fully materialized in the same response; do not leave unknown target/source ids. Resolve duplicate coordinates in same-turn map updates. If the place is narrative color inside the current room, keep current_location unchanged and describe it as part of the existing location."
    }
    elseif ($hasMortalFactionIssue) {
        "Use MORTAL_FACTION_UPDATE_TEMPLATE.md before editing game_state/factions/*. For unknown faction ids, choose one explicit path: reference an existing canonical factionId from faction_core.json, create the missing faction as a complete factions[] object when the story truly introduced it, or remove/retarget sidecar entries that point to a faction that should not exist. Preserve ranks, branches, chronicles, relations, projects, resources, and reputation details; do not silence validation by deleting unrelated faction data."
    }
    elseif ($hasMortalNpcIssue) {
        "Use MORTAL_NPC_UPDATE_TEMPLATE.md before editing game_state/npcs/npc_core.json or game_state/npcs/npc_journals.json. Materialize meaningful Mortal World NPCs through UpdateNPCs/NPCsInScene as full objects with relationshipLock, goals, personalityTraits, attitude, and culturalStance in canonical shapes. Direct-speaking or directly addressed Mortal actors must not be excluded only because their personal name is unknown; use a stable role-based visible name until the real name is learned. NPCsInScene is only for actors physically present in currentLocationData: voices behind a door, people near nearbyExitLocationId, nearby corridors, and route pressure stay in narrative/location/quest/faction memory or Actors outside scope until they are actually in the current scene. For NPCsInScene in a known current location, set currentLocationId to currentLocationData.locationId and initialLocationId to JSON null. For NPCsInScene in a same-turn new location, set initialLocationId to currentLocationData.initialId/newLocations.initialId and currentLocationId to JSON null. For NPCJournals, set lastJournalNote to the latest player-facing thought/observation and write journalEntries[] as objects with non-empty description, not as bare strings. If an NPC is only background-only color, move the name from Relevant actors to Actors outside scope instead of creating a partial NPC object."
    }
    elseif ($hasMortalSkillIssue) {
        "Use MORTAL_SKILL_PROGRESSION_TEMPLATE.md before writing Mortal World training, skill unlocks, active skill use, or mastery updates. If harnessRepairPackets[].kind is mortal_skill_progression_shape_repair, repair activeSkillChanges, passiveSkillChanges, removeActiveSkills, removePassiveSkills, and skillMasteryChanges as arrays, even for a single changed skill; read pending_training_showcase_requests.json for paid lesson authority and do not charge resources again. Attribute-only checks may stay prose/math only, but prose-only learning must be avoided when the fiction says the player learned or practiced a concrete skill: create/update passiveSkillChanges or activeSkillChanges, and initialize/update skillMasteryChanges for active skills."
    }
    elseif ($hasMortalExperienceIssue) {
        "Use MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md before awarding Mortal World XP, resolving combat rewards, or crossing a level threshold. Update game_state/player/experience.json through experienceGained/currentExperience/totalExperience/playerLevel metadata, include the changed file in Complete-BoeTurn, and let the client-owned level-up/stat allocation screen handle unspent stat points after the accepted turn."
    }
    elseif ($hasMortalCombatIssue) {
        "Use MORTAL_COMBAT_STATE_TEMPLATE.md when a Mortal World turn resolves open combat. If the narrative has an enemy exchange plus XP, active-skill mastery, or resource changes, leave /бой useful by writing game_state/combat/combat_log.json and, when relevant, enemies.json/allies.json."
    }
    elseif ($hasAfterlifeConflictRewardIssue) {
        "Use validation_repair_request.json.harnessRepairPackets[] kind afterlife_spiritual_conflict_reward_repair before editing game_state/meta/afterlife_spiritual_conflict_state.json. Currency rewardAudit is allowed only for resolved contested player victory with diceAudit.outcomeBand player_success or decisive_player_success. For negotiated, training-only, withdrawn, failed, or non-contested outcomes, remove rewardAudit and matching currency reward deltas while preserving learning, chronicle, relationship, and narrative consequences."
    }
    elseif ($hasAfterlifeEntityProfileScaffoldIssue) {
        "Use validation_repair_request.json.harnessRepairPackets[] kind afterlife_entity_profile_scaffold_repair before editing game_state/meta/afterlife_entity_profiles.json. Patch the minimum profile scaffold in place: goals as object with goalId/shortTermGoal/longTermGoal/plan/gmThoughtsSummary/updatedAtTurn, progressionStrategy with strategyId/summary/priorityOrder/resourceReserve/allowedSpends/forbiddenSpends, ledger as array, and profileCommands.specialArtLearningReceipts with receiptId/artId/teacherActorType/teacherActorId/playerActorId/trainingConditionSatisfied/learnedAtTurn/roleplayEvidence/summary plus initialTier absent or 0."
    }
    elseif ($hasGuardianPendingCreationMaterializationIssue) {
        "Use validation_repair_request.json.harnessRepairPackets[] kind guardian_pending_creation_materialization_repair before broad Guardian scope examples. Read game_state/meta/guardians.json.pendingGuardianCreation as startup authority. For a New Game freeform startup request, write UpdateGuardians.create as the UpdateGuardians[] JSON array: UpdateGuardians = @(@{ command='create'; data=<full canonical Guardian> }); that create command is the authority. Start from harnessRepairPackets[].canonicalCreateSkeleton and allowedEnums, then make guardians[] + activeGuardian + chaosSeaNavigation match it and remove pendingGuardianCreation. Do not repair only materialized mirrors, do not keep pendingGuardianCreation as a pending-only fallback, do not delete it by itself, do not leave the Guardian only in prose, and do not rewrite the requested freeform Guardian into an unrelated preset or NPC."
    }
    elseif ($hasGuardianTradeInventoryResolutionIssue) {
        "Use validation_repair_request.json.harnessRepairPackets[] kind guardian_trade_inventory_resolution_repair before editing game_state/meta/guardians.json. Read game_state/control/pending_guardian_trade_request.json as read-only authority, keep it unchanged, patch the matching guardian.tradeInventory to the request returnCycleId/slot count/tier fields, and add a matching tradeInventoryReceipts/UpdateGuardianTradeInventoryReceipts ready receipt. Do not create a new turn, do not rewrite the pending request, and do not leave the vitrine only in prose."
    }
    elseif ($hasMortalItemDurabilityIssue) {
        "Use VALIDATION_REPAIR_TEMPLATE.md and the current harnessRepairPackets before editing game_state/inventory/items.json. For item durability, write a percentage string such as 100% for intact items, never a bare number such as 100. Preserve the item and patch only the malformed durability/shape fields named by validation."
    }
    elseif ($hasMortalItemJournalEntriesIssue) {
        "Use VALIDATION_REPAIR_TEMPLATE.md and the current harnessRepairPackets before editing game_state/inventory/items.json. For item journalEntries, write an array of non-empty strings, not objects; each entry should be a single player-facing note string without technical turn anchors, for example 'The seal matched the wax mark.' Preserve the item and patch only the malformed journalEntries fields named by validation."
    }
    elseif ($hasNarrativeResponseAfterlifeChronicleWrongSurface) {
        "Use TURN_OUTPUT_TEMPLATE.md for output/narrative_response.json: only response and timestamp are allowed there. Never put afterlifeChronicleUpdates into output/narrative_response.json. Use AFTERLIFE_CHRONICLE_TEMPLATE.md and the afterlifeChronicleUpdates surface for game_state/meta/afterlife_chronicles.json external memory, then include game_state/meta/afterlife_chronicles.json in Complete-BoeTurn -FilesModified when that state was changed."
    }
    elseif ($hasStalePlayerFacingOutputIssue) {
        "Use OUTPUT_ARTIFACT_REPAIR_TEMPLATE.md for this output-only validation repair. Rewrite only stale or technically contaminated player-facing output artifacts such as output/narrative_response.json, output/interface_updates.json, and output/debug_logs.json so they match the already repaired canonical state. Do not mention JSON, validation, repair, canonical state, arrays, file paths, field names, or storage mechanics inside narrative_response.response. Do not touch canonical game_state files unless the current validation_repair_request.json still lists canonical state errors."
    }
    elseif ($hasGenericTurnOutputArtifactIssue) {
        "Use TURN_OUTPUT_TEMPLATE.md before writing ordinary turn output artifacts. Write output/narrative_response.json with only response and timestamp. Write output/debug_logs.json with gm_thoughts_markdown and timestamp. Write output/interface_updates.json with payload and timestamp. Do not write generic checks/mode/outcome/requestId/rewards/sessionId/turnNumber envelopes into output artifacts. Finish with Complete-BoeTurn -FilesModified only after canonical state changes are written."
    }
    elseif ($repairPacketRefs.Count -gt 0) {
        "Open validation_repair_request.json.harnessRepairPackets first, patch only named files, then use the compact repair/template surface."
    }
    elseif ($preferredSurface -eq "ACTOR_REASONING_TEMPLATE.md") {
        "Use NPC Scope plus separate Situation, Thoughts, and Actions bullets for every relevant canonical actor."
    }
    else {
        "Use the compact generated template before opening large examples or implementation source."
    }

    $templateVersion = "unknown"
    if ($null -ne $Record.templateVersions) {
        if ($null -ne $Record.templateVersions.actorReasoning -and $preferredSurface -eq "ACTOR_REASONING_TEMPLATE.md") {
            $templateVersion = [string]$Record.templateVersions.actorReasoning
        }
        elseif ($preferredSurface -eq "MORTAL_LOCATION_TRANSITION_TEMPLATE.md") {
            $templateVersion = if ($null -ne $Record.templateVersions.mortalLocation) { [string]$Record.templateVersions.mortalLocation } else { "v1" }
        }
        elseif ($preferredSurface -eq "MORTAL_SKILL_PROGRESSION_TEMPLATE.md") {
            $templateVersion = if ($null -ne $Record.templateVersions.mortalSkill) { [string]$Record.templateVersions.mortalSkill } else { "v1" }
        }
        elseif ($preferredSurface -eq "MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md") {
            $templateVersion = if ($null -ne $Record.templateVersions.mortalExperience) { [string]$Record.templateVersions.mortalExperience } else { "v1" }
        }
        elseif ($preferredSurface -eq "MORTAL_COMBAT_STATE_TEMPLATE.md") {
            $templateVersion = if ($null -ne $Record.templateVersions.mortalCombat) { [string]$Record.templateVersions.mortalCombat } else { "v1" }
        }
        elseif ($preferredSurface -eq "MORTAL_FACTION_UPDATE_TEMPLATE.md") {
            $templateVersion = if ($null -ne $Record.templateVersions.mortalFaction) { [string]$Record.templateVersions.mortalFaction } else { "v1" }
        }
        elseif ($preferredSurface -eq "MORTAL_NPC_UPDATE_TEMPLATE.md") {
            $templateVersion = if ($null -ne $Record.templateVersions.mortalNpc) { [string]$Record.templateVersions.mortalNpc } else { "v1" }
        }
        elseif ($preferredSurface -eq "TURN_OUTPUT_TEMPLATE.md") {
            $templateVersion = if ($null -ne $Record.templateVersions.turnOutput) { [string]$Record.templateVersions.turnOutput } else { "v1" }
        }
        elseif ($null -ne $Record.templateVersions.progressionReport -and $preferredSurface -eq "PROGRESSION_REPORT_TEMPLATE.json") {
            $templateVersion = [string]$Record.templateVersions.progressionReport
        }
        elseif ($null -ne $Record.templateVersions.validationRepair) {
            $templateVersion = [string]$Record.templateVersions.validationRepair
        }
        elseif ($null -ne $Record.templateVersions.turnOutput) {
            $templateVersion = [string]$Record.templateVersions.turnOutput
        }
    }

    $confidence = if ($hasIdleWithoutTerminalSignal -or $repairPacketRefs.Count -gt 0) { "high" } else { "medium" }

    return [ordered]@{
        lessonId = ("gmlesson_" + $recordId.Replace("gmtraj_", ""))
        sourceRecordIds = $sourceRecordIds
        match = [ordered]@{
            realm = Get-GmExperienceObjectRealm -Object $Record
            mode = if ($Record.mode) { [string]$Record.mode } else { "ordinary" }
            issueKinds = @($issueKinds)
            taskTypes = @()
        }
        versions = [ordered]@{
            contract = "gm-trajectory-ledger-v1"
            template = $templateVersion
        }
        badPattern = "Prior GM trajectory hit validation/friction pattern: $issueSummary."
        acceptedFix = $fix
        preferredHarnessSurface = $preferredSurface
        confidence = $confidence
        lastSeenAt = if ($Record.createdAt) { [string]$Record.createdAt } else { (Get-Date).ToUniversalTime().ToString("o") }
    }
}

function Test-GmExperienceRecordIsSuccessfulLessonSource {
    param([object]$Record)

    $issueKinds = @(Get-GmExperienceIssueKinds -Object $Record)
    if ($issueKinds.Count -eq 0) {
        return $false
    }

    if ($null -eq $Record.validation -or
        -not [string]::Equals([string]$Record.validation.status, "accepted", [System.StringComparison]::OrdinalIgnoreCase)) {
        $terminalKind = if ($null -ne $Record.terminal -and $null -ne $Record.terminal.kind) {
            [string]$Record.terminal.kind
        } else {
            ""
        }
        $playerFacingOutputPresent = if ($null -ne $Record.rubric -and $null -ne $Record.rubric.playerFacingOutputPresent) {
            [bool]$Record.rubric.playerFacingOutputPresent
        } else {
            $true
        }

        return ($issueKinds -contains "gm_bridge_idle_without_terminal_signal") -and
            [string]::Equals($terminalKind, "error", [System.StringComparison]::OrdinalIgnoreCase) -and
            -not $playerFacingOutputPresent
    }

    $repairStatus = if ($null -ne $Record.repair -and $null -ne $Record.repair.status) {
        [string]$Record.repair.status
    } else {
        ""
    }

    return @("accepted", "completed", "fixed", "success") -contains $repairStatus.ToLowerInvariant()
}

function Get-GmExperienceLessons {
    param(
        [object]$Query,
        [int]$MaxLessons = 5,
        [int]$MaxBytes = 12000
    )

    if (!(Test-Path $script:GmTrajectoryLedgerPath)) {
        return @()
    }

    $records = @()
    foreach ($line in @(Get-Content -Path $script:GmTrajectoryLedgerPath -Encoding UTF8 -ErrorAction SilentlyContinue)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try {
            $record = $line | ConvertFrom-Json
            if (-not (Test-GmExperienceRecordIsSuccessfulLessonSource -Record $record)) {
                continue
            }
            if (Test-GmExperienceRecordMatchesQuery -Record $record -Query $Query) {
                $records += $record
            }
        }
        catch { }
    }

    $records = @($records | Sort-Object @{ Expression = { if ($_.createdAt) { [datetime]$_.createdAt } else { [datetime]::MinValue } }; Descending = $true })
    $lessons = @()
    foreach ($record in $records) {
        if ($lessons.Count -ge $MaxLessons) {
            break
        }

        $lessons += (New-GmExperienceLesson -Record $record)
    }

    while ($lessons.Count -gt 0) {
        $serialized = [System.Text.Encoding]::UTF8.GetBytes(($lessons | ConvertTo-Json -Depth 8 -Compress)).Length
        if ($serialized -le $MaxBytes) {
            break
        }

        $lessons = @($lessons | Select-Object -First ($lessons.Count - 1))
    }

    return @($lessons)
}

function Write-GmExperienceLessons {
    $lessonsDir = Split-Path $script:GmExperienceLessonJsonPath -Parent
    if (!(Test-Path $lessonsDir)) {
        New-Item -ItemType Directory -Path $lessonsDir -Force | Out-Null
    }

    $query = Get-GmExperienceQuery
    $lessons = @(Get-GmExperienceLessons -Query $query)
    $guidance = "Experience lessons are selected from accepted prior repair outcomes and actionable prior harness failures. They are hints only; validators and current templates remain authoritative."
    $payload = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        guidance = $guidance
        maxLessons = 5
        maxSerializedBytes = 12000
        query = $query
        lessons = @($lessons)
    }

    Set-Content -LiteralPath $script:GmExperienceLessonJsonPath -Value (($payload | ConvertTo-Json -Depth 10) + [Environment]::NewLine) -Encoding UTF8

    $markdown = @(
        "# GM Experience Lessons",
        "",
        $guidance,
        ""
    )
    if ($lessons.Count -eq 0) {
        $markdown += "No relevant prior lessons were selected for this context pack."
    }
    else {
        foreach ($lesson in $lessons) {
            $markdown += "## $($lesson.lessonId)"
            $markdown += "- Match: realm=$($lesson.match.realm), mode=$($lesson.match.mode), issues=$($lesson.match.issueKinds -join ', ')"
            $markdown += "- Bad pattern: $($lesson.badPattern)"
            $markdown += "- Accepted fix: $($lesson.acceptedFix)"
            $markdown += "- Preferred harness surface: $($lesson.preferredHarnessSurface)"
            $markdown += ""
        }
    }
    Set-Content -LiteralPath $script:GmExperienceLessonMarkdownPath -Value (($markdown -join [Environment]::NewLine) + [Environment]::NewLine) -Encoding UTF8

    return [ordered]@{
        role = "experience_lessons"
        relativePath = "Lessons/GM_EXPERIENCE_LESSONS.json"
        generated = $true
        sessionPath = $script:GmExperienceLessonJsonPath
        markdownPath = $script:GmExperienceLessonMarkdownPath
        lessonCount = $lessons.Count
    }
}

function Get-GmExperiencePromptDigest {
    param(
        [int]$MaxLessons = 3,
        [int]$MaxFixChars = 520
    )

    if (!(Test-Path $script:GmExperienceLessonJsonPath)) {
        return ""
    }

    try {
        $payload = Get-Content -LiteralPath $script:GmExperienceLessonJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        return ""
    }

    $lessons = @()
    if ($null -ne $payload -and $null -ne $payload.lessons) {
        $lessons = @($payload.lessons)
    }

    if ($lessons.Count -eq 0) {
        return ""
    }

    $lines = @(
        "",
        "RLM PRE-TURN LESSONS: compact hints from accepted prior repairs and actionable prior harness failures. Use them before writing this turn; validators, current compact templates, and repair packets remain authoritative."
    )

    foreach ($lesson in @($lessons | Select-Object -First $MaxLessons)) {
        $issues = "unclassified"
        if ($null -ne $lesson.match -and $null -ne $lesson.match.issueKinds) {
            $issueArray = @($lesson.match.issueKinds) | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            if ($issueArray.Count -gt 0) {
                $issues = $issueArray -join ", "
            }
        }

        $surface = if ($null -ne $lesson.preferredHarnessSurface -and -not [string]::IsNullOrWhiteSpace([string]$lesson.preferredHarnessSurface)) {
            [string]$lesson.preferredHarnessSurface
        } else {
            "current compact template"
        }

        $fix = if ($null -ne $lesson.acceptedFix) { [string]$lesson.acceptedFix } else { "" }
        $fix = ($fix -replace "\s+", " ").Trim()
        if ($fix.Length -gt $MaxFixChars) {
            $fix = $fix.Substring(0, $MaxFixChars - 1) + "…"
        }

        $lines += "- issues=$issues; preferredHarnessSurface=$surface; acceptedFix=$fix"
    }

    return (($lines -join "`n") + "`n")
}

function Get-FirstMortalBootstrapPrompt {
    param([object]$TurnRequest)

    if ($null -eq $TurnRequest) {
        return ""
    }

    $realm = Get-GmExperienceObjectRealm -Object $TurnRequest
    if ($realm -ne "MortalWorld") {
        return ""
    }

    $systemReminder = if ($null -ne $TurnRequest.systemReminder) { [string]$TurnRequest.systemReminder } else { "" }
    if (-not $systemReminder.Contains("MORTAL BOOTSTRAP BASELINE")) {
        return ""
    }

    return " FIRST MORTAL BOOTSTRAP OUTPUT CHECKLIST: do not open large examples first. Open game_state/control/mortal_bootstrap_scaffold.json and '$script:CompactTurnOutputTemplatePath'. Produce the minimum complete first mortal scene: output/narrative_response.json, output/debug_logs.json with NPC Scope, output/interface_updates.json with clear choices, and game_state/control/progression_report.json when progression counts are required. If you add or update player-facing Mortal anchors, use the compact Mortal templates for that exact surface. Finish with Complete-BoeTurn -FilesModified as the LAST action; never return idle with no player-facing output."
}

function Build-FirstMortalBootstrapDispatchMessage {
    param(
        [object]$TurnRequest,
        [object]$TurnNumber,
        [string]$RequestId,
        [string]$ExperiencePrompt,
        [string]$FirstMortalBootstrapPrompt
    )

    return "Process first Mortal bootstrap turn #$TurnNumber (requestId=$RequestId).$($script:GmContextPackDirective)$($script:GmDocPathDirective)$($script:GmSafeProbeDirective)$($script:GmSourceFallbackDirective)$($script:GmExperienceLessonsDirective)$($ExperiencePrompt)$($FirstMortalBootstrapPrompt)$($script:GmLiveTestRubricDirective)$($script:GmTurnHelperDirective) Read $GameSessionPath\input\turn_request.json, game_state/control/mortal_bootstrap_scaffold.json, '$($script:CompactTurnOutputTemplatePath)', '$($script:CompactActorReasoningTemplatePath)', and '$($script:CompactProgressionReportTemplatePath)' before writing output. This is a bounded bootstrap task packet: do not open broad examples, afterlife directives, weather guidance, combat guidance, faction/location/NPC/skill templates, or repository source unless validation later names an exact surface. FIRST MORTAL BOOTSTRAP OUTPUT CHECKLIST: produce the minimum complete first mortal scene only: output/narrative_response.json, output/debug_logs.json with NPC Scope, output/interface_updates.json with clear choices, and game_state/control/progression_report.json only when progression counts are required. If the baseline files are already valid, preserve them and avoid rebuilding the world. If you must add a player-facing anchor from the scaffold, use the smallest matching compact Mortal template after the basic output files are ready. TERMINAL CHECKLIST: write EXACTLY ONE terminal signal for this request, copy exact sessionId/requestId/turnNumber from input/turn_request.json, never delete or rewrite input/turn_request.json or pending_turn_snapshot files, and finish with Complete-BoeTurn -FilesModified as the LAST action."
}

function Write-GmSafeProbes {
    $probeDir = Split-Path $script:GmSafeProbeJsonPath -Parent
    if (!(Test-Path $probeDir)) {
        New-Item -ItemType Directory -Path $probeDir -Force | Out-Null
    }

    $guidance = "Safe GM probes are read-only context surfaces. Prefer them before repository source; if a needed fact is missing, record a missing harness surface instead of treating implementation source as normal workflow."
    $probes = @(
        [ordered]@{
            probeId = "current_realm_mode_summary"
            purpose = "Identify the active realm, turn mode, request metadata, and progression contour without broad state spelunking."
            readOnly = $true
            sourceAuthority = @("input/turn_request.json", "game_state/meta/soul_state.json", "game_state/meta/shining_abode_state.json")
            outputShape = [ordered]@{
                realm = "MortalWorld|ChaosSea|ShiningAbode|Unknown"
                mode = "ordinary|validation_repair|terminal_protocol"
                requestId = "<current request id>"
                turnNumber = 0
            }
            limitations = "Does not authorize writes; validators and pending/control files remain authoritative."
        },
        [ordered]@{
            probeId = "active_pending_contracts"
            purpose = "List active pending/control contracts the GM must close or respect this turn."
            readOnly = $true
            sourceAuthority = @("game_state/control/pending_*.json", "game_state/control/*_request.json", "game_state/control/progression_schedule.json")
            outputShape = [ordered]@{
                pendingFiles = @()
                requiredClosures = @()
                blockedSurfaces = @()
            }
            limitations = "Summarizes contracts only; use compact templates and contract matrix for exact output shape."
        },
        [ordered]@{
            probeId = "validation_issue_summary"
            purpose = "Explain current validation issues, repair packets, and allowed repair targets without source-code archaeology."
            readOnly = $true
            sourceAuthority = @("game_state/control/validation_repair_request.json", "game_state/control/terminal_protocol_failure_request.json")
            outputShape = [ordered]@{
                issueKinds = @()
                summaryGroups = @()
                harnessRepairPackets = @()
                allowedTargets = @()
            }
            limitations = "Does not replace validation; repair only listed files and use helper terminal functions."
        },
        [ordered]@{
            probeId = "allowed_output_templates"
            purpose = "Point to compact executable templates for common output shapes."
            readOnly = $true
            sourceAuthority = @("Templates/TURN_OUTPUT_TEMPLATE.md", "Templates/VALIDATION_REPAIR_TEMPLATE.md", "Templates/PROGRESSION_REPORT_TEMPLATE.json", "Templates/ACTOR_REASONING_TEMPLATE.md", "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md", "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md", "Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md", "Templates/MORTAL_SKILL_PROGRESSION_TEMPLATE.md", "Templates/MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md", "Templates/MORTAL_COMBAT_STATE_TEMPLATE.md", "Templates/AFTERLIFE_CHRONICLE_TEMPLATE.md", "Templates/AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json")
            outputShape = [ordered]@{
                templateRefs = @()
                version = "v1"
            }
            limitations = "Templates are shape guidance; current validators are final authority."
        },
        [ordered]@{
            probeId = "rollback_status"
            purpose = "Show rollback baseline and auto-rollback diagnostics before the GM attempts manual undo."
            readOnly = $true
            sourceAuthority = @("game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json", "game_state/control/validation_auto_rollback_report.json")
            outputShape = [ordered]@{
                hasValidatedBaseline = $false
                rollbackReport = $null
                wrongRealmWriteDetected = $false
            }
            limitations = "Any future restore action must route through an explicit validated rollback/apply gate, not ad hoc file edits."
        },
        [ordered]@{
            probeId = "worker_role_summary"
            purpose = "List enabled worker roles and proposal-only task types without giving workers canonical write authority."
            readOnly = $true
            sourceAuthority = @("config.json:GmWorkerBridgeProfiles", "game_state/control/gm_worker_audit.jsonl", "worker_proposals/inbox")
            outputShape = [ordered]@{
                enabledRoles = @()
                proposalOnlyTaskTypes = @(
                    "narrative-draft",
                    "analysis",
                    "lore-consistency",
                    "npc-analysis",
                    "qte-content",
                    "inventory-content",
                    "skill-content",
                    "npc-content",
                    "social-dialogue-content",
                    "faction-content",
                    "location-content",
                    "quest-content",
                    "book-document-content",
                    "economy-crafting-content",
                    "world-state-content",
                    "encounter-content"
                )
                validationRepairProfiles = @()
            }
            limitations = "Worker proposals are suggestions until the apply gate accepts them."
        }
    )

    $payload = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        guidance = $guidance
        mutationPolicy = "read-only unless a documented probe explicitly routes through an existing validated apply or rollback gate"
        missingHarnessSurfaceRule = "If safe probes do not expose a needed fact, record a missing harness surface finding instead of normalizing implementation source reads."
        probes = $probes
    }

    Set-Content -LiteralPath $script:GmSafeProbeJsonPath -Value (($payload | ConvertTo-Json -Depth 10) + [Environment]::NewLine) -Encoding UTF8

    $markdown = @(
        "# GM Safe Probes",
        "",
        $guidance,
        "",
        "Mutation policy: read-only unless a probe explicitly routes through an existing validated apply or rollback gate.",
        "Missing harness surface rule: if the probe set lacks a fact, record that gap instead of making implementation source the normal route.",
        ""
    )
    foreach ($probe in $probes) {
        $markdown += "## $($probe.probeId)"
        $markdown += $probe.purpose
        $markdown += "- Source authority: $($probe.sourceAuthority -join ', ')"
        $markdown += "- Limitations: $($probe.limitations)"
        $markdown += ""
    }

    Set-Content -LiteralPath $script:GmSafeProbeMarkdownPath -Value (($markdown -join [Environment]::NewLine) + [Environment]::NewLine) -Encoding UTF8

    return [ordered]@{
        role = "safe_gm_probes"
        relativePath = "Probes/GM_SAFE_PROBES.json"
        generated = $true
        sessionPath = $script:GmSafeProbeJsonPath
        markdownPath = $script:GmSafeProbeMarkdownPath
        probeCount = $probes.Count
        mutationPolicy = "read-only"
    }
}

function Write-GmLiveTestRubric {
    $rubricDir = Split-Path $script:GmLiveTestRubricJsonPath -Parent
    if (!(Test-Path $rubricDir)) {
        New-Item -ItemType Directory -Path $rubricDir -Force | Out-Null
    }

    $notesPath = "game_state/control/gm_live_test_notes.jsonl"
    $dimensions = @(
        [ordered]@{
            id = "turn_success"
            prompt = "Valid output, player-facing narrative, correct command lifecycle, and exactly one terminal signal."
        },
        [ordered]@{
            id = "harness_containment"
            prompt = "No implementation-code browsing, no raw wrong-realm writes, no direct worker canonical writes."
        },
        [ordered]@{
            id = "friction"
            prompt = "Time to first valid turn, repair loops, missing-tool moments, context pack size/read volume."
        },
        [ordered]@{
            id = "delegation"
            prompt = "Whether a worker was used or should have been used; proposal quality and apply/reject outcome."
        },
        [ordered]@{
            id = "experience_memory"
            prompt = "Whether retrieved lessons, safe probes, and compact templates were used and helped."
        },
        [ordered]@{
            id = "follow_up_generation"
            prompt = "Repeated GM difficulty becomes a harness, validator/normalizer, rollback/tool issue, or explicit no-change rationale."
        }
    )

    $payload = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        issue = "#1290"
        notesPath = $notesPath
        ledgerPath = "game_state/control/gm_trajectory_ledger.jsonl"
        workerAuditPath = "game_state/control/gm_worker_audit.jsonl"
        dimensions = $dimensions
        noteSchema = [ordered]@{
            noteId = "gmlivetest_<short_id>"
            recordId = "gmtraj_<id or unknown>"
            requestId = "<request id or empty>"
            turnNumber = 0
            realm = "MortalWorld|ChaosSea|ShiningAbode|Unknown"
            dimension = "turn_success|harness_containment|friction|delegation|experience_memory|follow_up_generation"
            severity = "info|warning|blocker"
            observation = "<what happened>"
            harnessFollowUp = "<tool/validator/rollback/template/worker change, issue ref, or no-change rationale>"
            issueRef = "#<issue number or empty>"
            createdAtUtc = "<ISO 8601 UTC timestamp>"
        }
    }

    Set-Content -LiteralPath $script:GmLiveTestRubricJsonPath -Value (($payload | ConvertTo-Json -Depth 8) + [Environment]::NewLine) -Encoding UTF8

    $markdown = @(
        "# GM Live-Test Rubric",
        "",
        "Use this during harness live tests. The goal is not only to finish a turn; record whether the harness made the GM's work bounded, safe, and easy.",
        "",
        "- Issue: #1290",
        '- Trajectory ledger: `game_state/control/gm_trajectory_ledger.jsonl`',
        '- Worker audit: `game_state/control/gm_worker_audit.jsonl`',
        "- Structured notes: ``$notesPath``",
        "",
        "## Dimensions",
        ""
    )
    foreach ($dimension in $dimensions) {
        $markdown += "### $($dimension.id)"
        $markdown += $dimension.prompt
        $markdown += ""
    }
    $markdown += "## Structured Note Shape"
    $markdown += ""
    $markdown += "Append one JSON object per line to ``$notesPath`` when a notable success, friction point, missing tool, delegation decision, or follow-up appears."
    $markdown += ""
    $markdown += '```json'
    $markdown += ($payload.noteSchema | ConvertTo-Json -Depth 4)
    $markdown += '```'
    $markdown += ""
    $markdown += "Repeated difficulty should become a harness issue, validator/normalizer issue, rollback/tool issue, worker-packet issue, or explicit no-change rationale."

    Set-Content -LiteralPath $script:GmLiveTestRubricMarkdownPath -Value (($markdown -join [Environment]::NewLine) + [Environment]::NewLine) -Encoding UTF8

    return [ordered]@{
        role = "live_test_rubric"
        relativePath = "Rubrics/GM_LIVE_TEST_RUBRIC.json"
        generated = $true
        sessionPath = $script:GmLiveTestRubricJsonPath
        markdownPath = $script:GmLiveTestRubricMarkdownPath
        notesPath = $notesPath
        dimensionCount = $dimensions.Count
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
        @("Examples\E_CLI_Training_Showcases.txt", "training_showcase_example"),
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
   - If `input/turn_request.json.afterlifeSpiritualConflictPreview` exists, read it before writing an afterlife spiritual conflict exchange; copy its authoritative action-cost tiers/costs and dice outcome preview instead of guessing deterministic math.
3. Write player-facing output first, then structured state/output files.
4. Finish with `Complete-BoeTurn -FilesModified @(...)` as the last command.

## Minimal files

- `output/narrative_response.json`: player-facing scene text, choices, visible consequences.
- `output/debug_logs.json`: short GM audit with scope declaration and actor reasoning.
- `output/interface_updates.json`: UI hints only when useful.
- `game_state/control/progression_report.json`: only when `progressionControl` says scheduler work is due.

## Pre-turn validation guard

- Always include timestamp in both output/narrative_response.json and output/debug_logs.json.
- Do not skip NPC Scope during incarnation bootstrap, even if the scene looks like a simple transition; write relevant actors or explicitly state that no actor is relevant.
- Copy exact sessionId/requestId/turnNumber and include every processed-count field even when value is 0 when `progressionControl` requires a progression report.
- If afterlife memory mentions a mortal destination, use localized player-facing realm terms in visible prose; do not write internal labels such as `MortalWorld`, `Mortal World`, `ChaosSea`, or `ShiningAbode`.
- For fresh Mortal bootstrap, copy canonical coordinates from game_state/control/mortal_bootstrap_scaffold.json `canonicalCoordinateAuthority`. This prevents `current_location_coordinates_mismatch`: the same locationId must have the same coordinates in `current_location`, `world_map.newLocations`, `adjacencyMap`, and `newLinks`.

## Output file skeletons

`output/narrative_response.json`:

```json
{
  "response": "<player-facing prose>",
  "timestamp": "<ISO 8601 UTC timestamp>"
}
```

Only `response` and `timestamp` belong in `output/narrative_response.json`.
Never put `afterlifeChronicleUpdates` into `output/narrative_response.json`; afterlife memory uses the afterlife chronicle surface and `game_state/meta/afterlife_chronicles.json`.

`output/debug_logs.json`:

```json
{
  "gm_thoughts_markdown": "## NPC Scope\n- Mode: Scene-local | World-progression | Guardian-centric | Mixed\n- Relevant actors: <actors or none>\n- Why relevant: <why these actors matter>\n- Actors outside scope: <actors or none>\n- Why outside scope: <why excluded actors do not matter>\n\n## Reasoning\n### <actor if any>\n- __ACTOR_LOCATION_LABEL__: where this actor is now and whether they stay there or relocate this turn.\n- __ACTOR_SITUATION_LABEL__: ...\n- __ACTOR_THOUGHTS_LABEL__: ...\n- __ACTOR_ACTIONS_LABEL__: ...",
  "timestamp": "<ISO 8601 UTC timestamp>"
}
```

For EVERY relevant NPC block, the current-location line is mandatory. If the actor is a player character or non-spatial afterlife locus, state that explicitly instead of deleting the line.

`output/interface_updates.json`:

```json
{
  "dialogueOptions": [
    {
      "text": "<player-facing option text>",
      "inputValue": "<optional exact submitted value when it must differ from visible text>",
      "category": "neutral"
    }
  ],
  "timestamp": "<ISO 8601 UTC timestamp>"
}
```

Canonical authoring rule: write each `dialogueOptions` entry as an object with at least player-facing `text`.
If the option needs a hidden machine marker or exact submitted command, keep `text` clean for the player and put the full value in optional `inputValue`.
Do not show control tags such as `[AFTERLIFE_SPIRITUAL_ACTION: ...]`, `[INK_FEATHER_ACTION: ...]`, or other `*_ACTION` / `*_CONTROL` markers in player-facing `text`.
The client may normalize a legacy string-only list into objects as a repair-prevention fallback,
but the GM must not rely on that fallback when authoring normal turns.

## Guardian bootstrap guard

Fresh New Game system Guardian seed is client-owned; freeform New Game Guardian seed is client-owned too.
If turn #1 in the Chaos Sea already has a materialized Guardian in `game_state/meta/guardians.json`
and no pending Guardian contract, narrate the first meeting and use `afterlifeChronicleUpdates[]` for
durable memory. The client also creates a matching starter mentor profile in
`game_state/meta/afterlife_entity_profiles.json` for the same Guardian actor; keep that profile as
authority and update it through documented profile surfaces when needed. System seeds carry
`sourcePreset`; freeform seeds carry `originType=freeform` and `freeformSourceDescription`. Do not
write `game_state/meta/guardians.json`, do not emit
`UpdateGuardians.create`, and do not include `game_state/meta/guardians.json` in
`Complete-BoeTurn -FilesModified @(...)` unless the current turn_request/player action has an explicit
Guardian mutation contract such as system attraction, player-founded Guardian, Guardian trade, Chaos
Sea travel, Guardian project, or Guardian gacha.

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
`validation_diagnostic_failure_report.json`, `terminal_protocol_failure_request.json`,
`gm_bridge_status.json`, or `stories/*.jsonl`.
'@
    $turnOutputTemplate = $turnOutputTemplate.Replace("__ACTOR_LOCATION_LABEL__", $script:ActorLocationLabel).Replace("__ACTOR_SITUATION_LABEL__", $script:ActorSituationLabel).Replace("__ACTOR_THOUGHTS_LABEL__", $script:ActorThoughtsLabel).Replace("__ACTOR_ACTIONS_LABEL__", $script:ActorActionsLabel)
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\TURN_OUTPUT_TEMPLATE.md" -Role "compact_turn_output_template" -Content $turnOutputTemplate
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\OUTPUT_ARTIFACT_REPAIR_TEMPLATE.md" -Role "compact_output_artifact_repair_template" -Content @'
# Output-only accepted turn repair

Use this template only in validation repair mode when
`validation_repair_request.json.harnessRepairPackets[].kind` is
`accepted_turn_output_artifact_repair`, especially for
`accepted_turn_stale_player_facing_output_after_canonical_repair` or
`narrative_response_technical_repair_leak`.

## Required flow

1. Dot-source `game_state/control/gm_turn_helper.bootstrap.ps1`.
2. Read `game_state/control/validation_repair_request.json`.
3. Read the current canonical state only as needed to align player-facing prose/options with the already repaired state.
4. Rewrite only listed output artifacts:
   - `output/narrative_response.json`
   - `output/interface_updates.json`
   - `output/debug_logs.json`
5. Do not touch canonical game_state files unless the current repair request still lists canonical state errors.
6. Finish with `Complete-BoeValidationRepair` as the last command.

## Narrative response must stay diegetic

`output/narrative_response.json.response` is player-facing prose. Never mention
JSON, validation, repair, canonical state, arrays, file paths, field names,
storage mechanics, or that a technical state write succeeded. Keep such details
only in `output/debug_logs.json` or repair evidence.

## Minimal shapes

`output/narrative_response.json`:

```json
{
  "response": "<fresh player-facing narrative for the same accepted turn>",
  "timestamp": "<ISO 8601 UTC timestamp>"
}
```

`output/interface_updates.json`:

```json
{
  "dialogueOptions": [
    {
      "text": "<clean visible option>",
      "inputValue": "<optional exact input>"
    }
  ],
  "timestamp": "<ISO 8601 UTC timestamp>"
}
```

`output/debug_logs.json`:

```json
{
  "gm_thoughts_markdown": "## NPC Scope\n- Mode: Scene-local | World-progression | Guardian-centric | Mixed\n- Relevant actors: <actors or нет>\n- Why relevant: <short reason>\n- Actors outside scope: <actors or нет>\n- Why outside scope: <short reason>\n\n## Reasoning\n### <actor if any>\n- Ситуация: ...\n- Мысли: ...\n- Действия: ...",
  "timestamp": "<ISO 8601 UTC timestamp>"
}
```

## Rules

- This is output-only repair. Do not create a new turn, reroll dice, advance time, change rewards, or rewrite canonical state.
- Preserve the accepted player action and already repaired story meaning.
- If the old narrative/options contradict repaired canonical state, rewrite the text/options to match current canonical state.
- Keep player-facing `text` clean; put exact machine input in `inputValue` only when needed.
- Do not write `ready/turn_complete.json` in validation repair mode.
- Do not search implementation source for output schema. This template and `validation_repair_request.json` are enough.

## Terminal rule

```powershell
Complete-BoeValidationRepair
```
'@
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\AFTERLIFE_CHRONICLE_TEMPLATE.md" -Role "compact_afterlife_chronicle_template" -Content @'
# Compact Afterlife Chronicle Template

Use this on ordinary `Chaos Sea` / `Shining Abode` turns before deciding that no durable afterlife memory is needed.

## When to write `afterlifeChronicleUpdates[]`

Write an afterlife chronicle update when the turn includes any of these:

- first meeting with a Guardian, resident, Shining faction, playable memory, Source of Light scene, or Saref story scene;
- significant Guardian dialogue, resident dialogue, faction negotiation, or values/teaching answer that future scenes should remember;
- a new promise, debt, unresolved hook, warning, clue, oath, injury, mercy, betrayal, or political consequence;
- a discovered or materially changed Guardian Abode, Chaos Sea region, Shining district, memory scene, threat scene, or hidden story thread.

It is acceptable to skip a chronicle only for purely mechanical/status commands, repeated clarification with no new fact or hook, or a scene that intentionally leaves no future-facing memory. If you skip it, say why in `debug_logs.json`.

## Valid update fragment

Never write `afterlifeChronicleUpdates` into `output/narrative_response.json`.
That file is only for player-facing prose (`response`) and `timestamp`.
Write the chronicle update through the accepted afterlife update surface for
`game_state/meta/afterlife_chronicles.json`:

```json
{
  "afterlifeChronicleUpdates": [
    {
      "chronicleId": "chronicle_guardian_elyara_first_mercy",
      "scopeType": "guardian_scene",
      "scopeId": "guardian_system_elyara_001",
      "displayName": "Первая беседа с Элиарой",
      "lastEventsDescription": "Северная Искра узнала, что милость Элиары требует правды, согласия, последствий и памяти.",
      "persistentConsequences": [
        "Элиара будет оценивать будущие просьбы души через честность к собственной ране."
      ],
      "openThreads": [
        "Понять, какую рану Северная Искра готова назвать правдиво."
      ],
      "lastUpdatedTurn": 2
    }
  ]
}
```

## Field rules

- Use stable `chronicleId`; reuse it for the same region, Abode, Guardian scene, memory scene, Source of Light event, or Saref story thread.
- Valid `scopeType` values: `chaos_sea_region`, `shining_abode_district`, `guardian_abode`, `guardian_scene`, `resident_scene`, `faction_zone`, `memory_scene`, `source_of_light`, `saref_story`.
- `lastEventsDescription` is the current turn summary that future GM reasoning can read.
- `persistentConsequences[]` is an array of non-empty strings for durable facts/consequences.
- `openThreads[]` is an array of non-empty strings for unresolved hooks.
- Use Russian in-world terms: посмертие, Море Хаоса, Сияющая Обитель, смертный мир. Do not expose internal English labels such as `afterlife`, `ChaosSea`, `ShiningAbode`, `MortalWorld`, or `Mortal World` in visible chronicle prose.
- Never include `eventDescriptions[]` inside `afterlifeChronicleUpdates[]`; that archive is read-only.
- Do not use Mortal `worldEventsLog`, `currentLocationData`, `worldMapUpdates`, NPC journals, faction chronicles, or lore files as substitutes for afterlife memory.
- Include `game_state/meta/afterlife_chronicles.json` in `Complete-BoeTurn -FilesModified @(...)` when you write chronicle updates.
'@
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
- If `harnessRepairPackets[]` contains `exactFieldCorrections[]`, apply those path -> expected replacements first, then recompute only dependent fields named by the same packet. Do not infer different values from prose when exact corrections are present.
- If `harnessRepairPackets[]` names exact fields, fix those fields instead of searching source code.
- If an inventory item `durability` field is named, write a percentage string such as `100%`; never write a bare number such as `100`.
- If an inventory item `journalEntries[]` field is named, write an array of non-empty strings, not objects; each entry is one player-facing note string without technical turn anchors such as `#[3].`.
- If `game_state/npcs/npc_journals.json` or `NPCJournals[].journalEntries[]` is named, write `journalEntries[]` as objects with non-empty `description`; optional object fields are `timestamp`, `event`, `emotionalImpact`, and `relationshipChange`. Do not use the inventory item string-array shape for NPC journals.
- If a wrong-realm auto-rollback report exists, treat it as diagnostic evidence, not permission to rewrite mortal files from afterlife.
- If `harnessRepairPackets[].kind` is `accepted_turn_output_artifact_repair`, use `OUTPUT_ARTIFACT_REPAIR_TEMPLATE.md` and repair only the listed output artifacts (`output/narrative_response.json`, `output/interface_updates.json`, `output/debug_logs.json`) for the same turn; do not create a new turn and do not touch canonical game_state files unless canonical errors remain listed in the current request.
- If validation reports `narrative_response_technical_repair_leak`, rewrite `output/narrative_response.json.response` as diegetic in-world prose only. Do not mention JSON, validation, repair, canonical state, arrays, file paths, field names, storage shape, or that a technical state write succeeded.
- If validation reports `narrative_response_unknown_field`, remove the unsupported field from `output/narrative_response.json`; keep only `response` and `timestamp`. If the field is `afterlifeChronicleUpdates`, move/keep that data only on the afterlife chronicle surface described by `AFTERLIFE_CHRONICLE_TEMPLATE.md` and `game_state/meta/afterlife_chronicles.json`.
- If `harnessRepairPackets[].kind` is `mortal_skill_progression_shape_repair`, repair the listed Mortal player skill files in place. `activeSkillChanges`, `passiveSkillChanges`, `removeActiveSkills`, `removePassiveSkills`, and `skillMasteryChanges` are always arrays, even for one changed skill. Read `game_state/control/pending_training_showcase_requests.json` for paid lesson `targetKind` authority and do not charge money/XP/currency again.
- If `harnessRepairPackets[].kind` is `afterlife_chronicle_string_array_repair`, repair the listed `persistentConsequences[]` / `openThreads[]` fields into arrays of non-empty strings; do not add `eventDescriptions[]`, do not substitute Mortal memory files, and do not create a new turn.
- If `harnessRepairPackets[].kind` is `afterlife_spiritual_conflict_action_cost_repair`, repair the listed `actionCostAudit` / `actionEconomy` fields sequentially in the already written spiritual conflict file; do not create a new exchange, reroll dice, or edit pending snapshots.
- If `harnessRepairPackets[].kind` is `afterlife_spiritual_conflict_reward_repair`, repair only the listed `rewardAudit` / currency reward fields in `game_state/meta/afterlife_spiritual_conflict_state.json`. Currency rewards require a contested player victory with `diceAudit.outcomeBand = player_success|decisive_player_success`; for negotiated, training-only, withdrawn, failed, or non-contested outcomes, remove `rewardAudit` and matching currency deltas while preserving valid learning/chronicle consequences.
- If `harnessRepairPackets[].kind` is `afterlife_entity_profile_scaffold_repair`, repair `game_state/meta/afterlife_entity_profiles.json` in place. Keep `goals` as an object with goal fields and `updatedAtTurn`, add/repair `progressionStrategy`, keep `ledger` as an array, and complete `profileCommands.specialArtLearningReceipts[]` with required teacher/player fields and `initialTier` absent or `0`.
- If `harnessRepairPackets[].kind` is `guardian_pending_creation_materialization_repair`, repair `game_state/meta/guardians.json` in place from `pendingGuardianCreation`. For a New Game freeform startup request, write `UpdateGuardians.create` as the `UpdateGuardians[]` JSON array: `"UpdateGuardians": [{ "command": "create", "data": <full canonical Guardian> }]`; the required marker is `command=create data=<full canonical Guardian>`, and that create command is the authority. Start from `harnessRepairPackets[].canonicalCreateSkeleton` and `harnessRepairPackets[].allowedEnums` before improvising missing lore text. Then make `guardians[]`, matching `activeGuardian`, and `chaosSeaNavigation.currentAbodeId` mirror that create result and remove `pendingGuardianCreation`. Do not repair only materialized mirrors, do not keep `pendingGuardianCreation` as a pending-only fallback, do not delete it by itself, and do not leave the Guardian only in prose.
- If `harnessRepairPackets[].kind` is `guardian_trade_inventory_resolution_repair`, repair `game_state/meta/guardians.json` in place. Read `game_state/control/pending_guardian_trade_request.json` as read-only authority, keep it unchanged, patch the matching `guardian.tradeInventory` to the request `returnCycleId`, reputation tier, bonus signature, and slot count, then add a matching ready receipt through `UpdateGuardianTradeInventoryReceipts` or `guardians[].tradeInventoryReceipts[]`. Do not create a new turn, do not rewrite the pending request, and do not leave Guardian trade stock only in prose.

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
- __ACTOR_LOCATION_LABEL__: where this actor is now and whether they stay there or relocate this turn.
- __ACTOR_SITUATION_LABEL__: what situation/current position makes this actor relevant.
- __ACTOR_THOUGHTS_LABEL__: what the actor wants, fears, evaluates, or decides internally.
- __ACTOR_ACTIONS_LABEL__: what changed, what the actor did, or why no state change was needed.

### <next exact actor display name>
- __ACTOR_LOCATION_LABEL__: ...
- __ACTOR_SITUATION_LABEL__: ...
- __ACTOR_THOUGHTS_LABEL__: ...
- __ACTOR_ACTIONS_LABEL__: ...
```

For EVERY relevant NPC block, the current-location line is mandatory. If the actor is a player character or non-spatial afterlife locus, state that explicitly instead of deleting the line.

Use exact actor names from state/validation packets. Keep punctuation stable; do not add punctuation that is not present in the canonical actor name.
Direct-speaking or directly addressed Mortal actors must not be excluded only because their personal name is unknown; if they are visible, acting, clue-giving, or receiving a player action, treat them as role-identifiable NPC candidates and use `MORTAL_NPC_UPDATE_TEMPLATE.md`.
'@
    $actorReasoningTemplate = $actorReasoningTemplate.Replace("__ACTOR_LOCATION_LABEL__", $script:ActorLocationLabel).Replace("__ACTOR_SITUATION_LABEL__", $script:ActorSituationLabel).Replace("__ACTOR_THOUGHTS_LABEL__", $script:ActorThoughtsLabel).Replace("__ACTOR_ACTIONS_LABEL__", $script:ActorActionsLabel)
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\ACTOR_REASONING_TEMPLATE.md" -Role "compact_actor_reasoning_template" -Content $actorReasoningTemplate
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\MORTAL_NPC_UPDATE_TEMPLATE.md" -Role "compact_mortal_npc_update_template" -Content @'
# Compact Mortal World NPC Update Template

Use this before creating or repairing Mortal World NPCs in `game_state/npcs/npc_core.json`.

## When to use

- A Mortal World turn introduces a named NPC who is present, speaks, gives a clue, changes state, or changes relationship.
- Role-identifiable visible, speaking, acting, clue-giving, or directly addressed scene actors are NPC candidates even when their personal name is unknown. Use a stable role-based visible name such as `Агент дома Виренто`, then update/rename later when the player learns the real name.
- Validation reports `mortal_relevant_actor_missing_persistence`, `npc_full_object_missing_required_fields`, `npc_attitude_relationship_tier_mismatch`, `npc_invalid_cultural_stance`, `npc_same_turn_initial_location_requires_null_current_location`, `npc_journal_unknown_npc_reference`, `missing_actor_current_location`, or object/nullability errors under `game_state/npcs/npc_core.json`.

## Scene NPC location rules

- Put scene-local present NPCs in `NPCsInScene`.
- NPCsInScene is only for actors physically present in currentLocationData. voices behind a door, guards in a nearby corridor, people near nearbyExitLocationId, route pressure, and exit pressure are not scene NPCs for the current room; keep them in narrative, current location memory, quest/faction/location memory, Actors outside scope, or materialize them through UpdateNPCs at their own location only if they are durable known actors.
- If `currentLocationData.locationId` is a known permanent id, set `currentLocationId` to that id and set `initialLocationId` to `null`.
- If the current scene is a same-turn new location (`currentLocationData.locationId = null` with `currentLocationData.initialId`), set `initialLocationId` to that exact initial id and set `currentLocationId` to `null`.
- Keep `currentLocationName` as the visible current location name.
- For a genuinely new NPC, use `NPCId: null` plus non-empty `initialId`; do not invent a permanent NPCId before the client materializes it.
- `inventory` inside the full NPC object is allowed for a genuinely new NPC's initial carried inventory. For an existing NPC, do not resend `inventory` inside `UpdateNPCs`; use NPC inventory command surfaces for deltas.
- Use canonical `culturalStance`: `Conformist`, `Pragmatist`, or `Dissident`.
- Keep `relationshipLevel` numeric. For neutral/unknown NPCs use `0` and `attitude: "Neutral"`.
- Nullable string fields must be either a real string or JSON `null`, not `{}` and not missing when validator names them.
- Object-shaped fields must stay objects: `progressionTrackers`, `goals`, `relationshipLock`, `personalityTraits[]` entries.
- `activeSkills` and `passiveSkills` must contain full skill objects, not string names. Put the visible name in `skillName`/`displayName`; active skills need action/combat fields, passive skills need type/group/bonus fields.

## NPC journal notes

Use `game_state/npcs/npc_journals.json` when the player can later inspect what the NPC thought, noticed, promised, hid, or remembered.

NPC journal shape is not the same as inventory item journal shape:

```json
{
  "NPCJournals": [
    {
      "npcId": "npc_<stable_slug>",
      "npcName": "<visible Russian name>",
      "lastJournalNote": "<latest player-facing note>",
      "journalEntries": [
        {
          "description": "<what this NPC thought, noticed, or remembered>",
          "timestamp": "<ISO 8601 UTC timestamp>",
          "event": "<short optional event label>",
          "emotionalImpact": "<optional emotional shift>",
          "relationshipChange": "<optional relationship shift>"
        }
      ]
    }
  ]
}
```

## Minimal safe NPC scene object

```json
{
  "NPCId": null,
  "initialId": "npc_<stable_slug>",
  "npcName": "<visible Russian name>",
  "name": "<same visible Russian name>",
  "role": "<short role in this scene>",
  "image_prompt": "<English image prompt>",
  "rarity": "Common",
  "worldview": "<one sentence>",
  "personalityArchetype": "<stable_snake_case>",
  "culturalStance": "Pragmatist",
  "race": "Человек",
  "class": "<social/combat role>",
  "appearanceDescription": "<visible appearance>",
  "history": "<relevant known background>",
  "progressionType": "static_social_npc",
  "initialLocationId": null,
  "currentLocationId": "<currentLocationData.locationId for known current location>",
  "currentLocationName": "<current location name>",
  "age": 30,
  "level": 1,
  "experience": 0,
  "experienceForNextLevel": 100,
  "relationshipLevel": 0,
  "attitude": "Neutral",
  "playerCompanionDirective": "not_companion",
  "culturalLayer": "<world/culture/faction context>",
  "personalityTraits": [
    {
      "traitId": "trait_<npc_slug>_<short>",
      "name": "<trait name>",
      "summary": "<short trait summary>",
      "traitName": "<same trait name>",
      "description": "<short description>",
      "valueDescription": "<how it matters in play>"
    }
  ],
  "maxWeight": 0,
  "totalWeight": 0,
  "isOverloaded": false,
  "progressionTrackers": {
    "active": [],
    "completed": [],
    "summary": "No mechanical progression tracker changed in this scene."
  },
  "plans": "<immediate plan or empty string>",
  "personalQuests": [],
  "relationshipLock": {
    "isLocked": false,
    "lockState": "none",
    "reason": "",
    "breakthroughQuestId": null
  },
  "characteristics": {
    "strength": 8,
    "dexterity": 10,
    "constitution": 10,
    "intelligence": 10,
    "wisdom": 10,
    "faith": 8,
    "attractiveness": 10,
    "trade": 8,
    "persuasion": 10,
    "perception": 10,
    "luck": 9,
    "speed": 8
  },
  "activeSkills": [],
  "passiveSkills": [],
  "equippedItems": {},
  "fateCards": [],
  "inventory": [],
  "goals": {
    "primary": {
      "goalId": "goal_<npc_slug>_<short>",
      "title": "<goal title>",
      "status": "active",
      "summary": "<why it matters now>"
    },
    "shortTerm": "<current scene aim>",
    "longTerm": "<longer aim>"
  },
  "sceneStatus": "present",
  "turn": 0,
  "lastSeenAtUtc": "<ISO 8601 UTC timestamp>"
}
```

If the NPC is only background color and does not act, speak, give clues, receive a player action, or change state, do not put them in `Relevant actors`; move them to `Actors outside scope` instead of creating a partial NPC object.
'@
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\MORTAL_FACTION_UPDATE_TEMPLATE.md" -Role "compact_mortal_faction_update_template" -Content @'
# Compact Mortal World Faction Update Template

Use this before creating or repairing Mortal World factions in `game_state/factions/faction_core.json` or sidecar faction files.

## Mortal faction identity repair

Validation issue kinds usually mean one of three things:

- `faction_full_object_unknown_faction_id`: a full `factions[]` object claims a permanent `factionId` that does not exist in canonical `faction_core.json`.
- `canonical_faction_sidecar_unknown_faction_id`: a sidecar entry references a faction that is not present in canonical `factions[]`.
- `faction_command_unknown_faction_id`: a response command targeted a faction id that is not canonical.

## Choose exactly one correction path

1. Reference an existing canonical factionId.
   - Use this when the sidecar/full object was meant to update an already existing faction.
   - Replace the bad id with the exact `factionId` from `game_state/factions/faction_core.json`.

2. Create the missing faction.
   - Use this only when the story really introduced a durable faction.
   - Add a complete `factions[]` object to `game_state/factions/faction_core.json` before sidecars reference it.
   - Keep the id stable, lowercase/snake-like, and based on the faction, not on one scene line.

3. Remove or retarget the invalid sidecar.
   - Use this when the sidecar was speculative, duplicate, or belonged to a faction that should not exist.
   - Remove only the invalid sidecar entry, or retarget it to an existing canonical factionId.

## Minimal durable faction object

```json
{
  "factionId": "faction_<stable_slug>",
  "name": "<Russian faction name>",
  "displayName": "<Russian faction name>",
  "description": "<what the faction wants and why it matters>",
  "type": "organization",
  "status": "active",
  "visibility": "known",
  "reputation": 0,
  "influence": 10,
  "resources": {
    "wealth": 10,
    "manpower": 10,
    "information": 10,
    "magic": 0
  },
  "ranks": [],
  "rankBranches": [],
  "relations": [],
  "controlledTerritories": [],
  "projects": [],
  "chronicle": [],
  "customStates": []
}
```

Preserve existing faction ranks, rankBranches, chronicles, relations, projects, resources, reputation, and custom states. Do not delete unrelated faction data to silence one identity error.
'@
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\MORTAL_LOCATION_TRANSITION_TEMPLATE.md" -Role "compact_mortal_location_transition_template" -Content @'
# Compact Mortal Location Transition Template

Use this before repairing Mortal World movement, new location creation, `world_map`, `current_location`, or NPC location ids.

## Mortal location transition repair

When validation reports `current_location_unknown_location_id`, `npc_unknown_current_location_id`, or `world_map_new_location_coordinates_duplicate_same_turn`, fix the map chain in this order:

1. Decide whether the destination is a durable location.
   - Durable: a room/street/site the player can return to, map, search, or reference later.
   - Narrative color: a corner, shelf, alcove, or moment inside the current location.

2. If durable, register the destination in `game_state/world/world_map.json` first.
   - Use one stable `locationId`.
   - Add visible name, region, description, exits/adjacency, required arrays, difficulty profiles, and unique coordinates.
   - If `type`/`locationType` is `outdoor`, add a canonical `biome`: `TemperateForest`, `ColdForest`, `Swamp`, `Urban`, `Plains`, `Mountains`, `Desert`, `Coast`, or `Unique`. For `Unique`, also add `biomeDescription`.
   - Avoid duplicate coordinates with existing locations and same-turn new locations.

3. Only after registration, update `game_state/world/current_location.json`.
   - `locationId` must be a known id from `world_map`.
   - Keep name/description consistent with the map entry.

4. Move NPCs only to known ids.
   - `currentLocationId` must point to a known world-map location.
   - Keep `currentLocationName` aligned with that location.

5. If the destination is narrative color, keep `current_location` unchanged.
   - Describe the action as happening inside the existing location.
   - Do not create a new canonical id just because the narration named a corner or object.

## Minimal durable location entry

```json
{
  "locationId": "loc_<stable_slug>",
  "name": "<visible Russian name>",
  "displayName": "<visible Russian name>",
  "region": "<region or settlement>",
  "type": "indoor",
  "biome": null,
  "description": "<what the player can see and use here>",
  "coordinates": { "x": 0, "y": 0, "z": 0 },
  "exits": [
    {
      "targetLocationId": "<known adjacent location id>",
      "direction": "<visible direction>",
      "isKnown": true
    }
  ],
  "knownExits": [],
  "adjacencyMap": [],
  "factionControl": [],
  "locationStorages": [],
  "activeThreats": [],
  "internalDifficultyProfile": {
    "combat": 1,
    "environment": 1,
    "social": 1,
    "exploration": 1
  },
  "externalDifficultyProfile": {
    "combat": 1,
    "environment": 1,
    "social": 1,
    "exploration": 1
  },
  "lastEventsDescription": "<what just happened here>"
}
```

For an outdoor location, set both `type` and `locationType` to `outdoor`, replace `biome: null` with a canonical biome value, and add `biomeDescription` only when the biome is `Unique`.

## Minimal world-map link preview

Every new map link preview must include the target name, target coordinates, and both estimated difficulty profiles.

```json
{
  "sourceLocationId": "<known source location id>",
  "targetLocationId": "<known target location id or same-turn newLocations.initialId>",
  "targetName": "<visible Russian target name>",
  "targetCoordinates": { "x": 1, "y": 0, "z": 0 },
  "estimatedInternalDifficultyProfile": {
    "combat": 1,
    "environment": 1,
    "social": 1,
    "exploration": 1
  },
  "estimatedExternalDifficultyProfile": {
    "combat": 1,
    "environment": 1,
    "social": 1,
    "exploration": 1
  }
}
```

Do not fix unknown locations by deleting NPCs, quests, faction links, storage links, or exits that should still point to the destination. Repair the map identity instead.
'@
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\MORTAL_SKILL_PROGRESSION_TEMPLATE.md" -Role "compact_mortal_skill_progression_template" -Content @'
# Compact Mortal Skill Progression Template

Use this before Mortal World turns that teach, unlock, practice, use, or improve player skills.

## Mortal skill progression rule

- An attribute-only check is allowed when the fiction is just "try with Strength/Intelligence/Perception/etc." and no durable technique, craft, combat move, or knowledge skill is learned.
- Prose-only learning is not enough when the fiction says the player learned, trained, practiced, unlocked, or repeatedly applied a concrete skill.
- Fresh Mortal bootstrap may already create starter passive skills from the player's explicit character concept in `game_state/player/skills_passive.json`. Preserve and use these starter passive skills during early checks; if the player repeatedly applies one, update the passive skill object instead of narrating permanent competence as prose-only text.
- If the player learns durable knowledge, perception, craft, social, or utility expertise, write `passiveSkillChanges` with a complete passive skill object.
- If the player learns a usable combat move or activated technique, write `activeSkillChanges` with a complete active skill object and initialize/update its mastery through `skillMasteryChanges`.
- If the player uses an already-known active skill, update `skillMasteryChanges`; do not write mastery for a skill that is not already present in `game_state/player/skills_active.json` or added in the same turn.
- If `game_state/control/pending_training_showcase_requests.json` contains requestKind `afterlife_teacher_showcase` for a system or freeform Guardian actor, treat the existing Guardian profile in `game_state/meta/afterlife_entity_profiles.json` as client-created authority. Fill `mentorTrainingShowcase` through `afterlifeEntityProfileUpdates`, copy the requested `sourceActorSnapshotHash`, and put a positive `cost` object on every offer; afterlife standard-art and `spirit_focus` mentor prices are client-owned and normalized to the current cost policy if missing or mismatched. Do not create a second Guardian, do not replace the profile with a bare stub, and do not charge the player.
- If `game_state/control/pending_training_showcase_requests.json` contains requestKind `mortal_training_skill_evolution`, the client has already charged the paid lesson. Resolve it by writing the complete updated `activeSkillChanges` or `passiveSkillChanges` plus matching `skillMasteryChanges`; do not charge money/XP again and do not leave the level-up as prose only. For these requests, follow `details.targetKind`: the client may normalize a generic showcase offer such as `skill_mastery` into `active_skill_mastery`, `passive_skill_mastery`, `active_skill_unlock`, or `passive_skill_unlock`; passive targets require `passiveSkillChanges`, not invented active skills.
- `trainingPurchaseReceipts` are client-owned historical audit records. Do not rewrite an old receipt only because the teacher profile changed after the paid lesson.
- Do not imply a mechanical skill in player-facing prose unless the corresponding state is updated or the prose clearly says this is only early practice, not a learned skill yet.

## Files and response fields

- Player active skills: `game_state/player/skills_active.json` from `activeSkillChanges` / `removeActiveSkills`.
- Player passive skills: `game_state/player/skills_passive.json` from `passiveSkillChanges` / `removePassiveSkills`.
- Active skill mastery: `game_state/player/skill_mastery.json` from `skillMasteryChanges`.
- Level/stat progression is separate; use the progression and stat-point surfaces only when the player actually gains level/points.

## Passive training example

Use this for durable expertise that helps checks but is not a button-like combat action.

```json
{
  "passiveSkillChanges": [
    {
      "skillName": "Чтение свидетельских меток",
      "skillDescription": "Искра учится различать домовые печати, свидетельские метки и порядок подписей в архивных лентах.",
      "rarity": "Common",
      "type": "KnowledgeBased",
      "group": "Архивное дело",
      "masteryLevel": 1,
      "maxMasteryLevel": 5,
      "playerStatBonus": "Помогает проверкам Интеллекта и Восприятия при чтении архивных печатей.",
      "structuredBonuses": [
        {
          "targetType": "skill",
          "skillName": "Архивное дело",
          "valueType": "flat",
          "value": 1,
          "condition": "чтение печатей, свидетельских меток и порядка подписей",
          "source": "Чтение свидетельских меток",
          "summary": "Архивное дело +1 при разборе печатей и свидетельских меток"
        }
      ],
      "knowledgeDomain": "архивные печати и свидетельские метки",
      "effectDetails": "Не заменяет взрослого доступа к нижнему хранилищу, но делает вопросы точнее."
    }
  ]
}
```

## Active skill plus mastery example

Use this for a named action the player can deliberately perform in combat or a contest.

```json
{
  "activeSkillChanges": [
    {
      "skillName": "Быстрый выпад",
      "skillDescription": "Короткая атака тонким клинком по открывшейся цели.",
      "rarity": "Common",
      "actionCost": "Fast",
      "scalingCharacteristic": "dexterity",
      "scalesValue": true,
      "scalesDuration": false,
      "scalesChance": false,
      "combatEffect": {
        "isActivatedEffect": true,
        "actionName": "Быстрый выпад",
        "actionDescription": "Колючий удар стилетом в уязвимое место.",
        "damageType": "piercing",
        "baseDamage": 8,
        "range": "melee",
        "actionCost": "Fast",
        "actionPointCost": 1,
        "cooldown": 0,
        "duration": 0,
        "scalesValue": true,
        "scalesDuration": false,
        "scalesChance": false
      }
    }
  ],
  "skillMasteryChanges": [
    {
      "skillName": "Быстрый выпад",
      "newMasteryLevel": 1,
      "newCurrentMasteryProgress": 0,
      "newMasteryProgressNeeded": 5,
      "masteryLeveledUp": true
    }
  ]
}
```

## Output discipline

- Mention the new or improved skill in the narrative response in player-facing Russian.
- Include the changed skill files in `Complete-BoeTurn -FilesModified`.
- If this was only a single lesson toward future learning, say that explicitly and do not create a skill yet.
- If a later turn repeats the same training and the character now crosses the learning threshold, create/update the skill then.
'@
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md" -Role "compact_mortal_experience_level_template" -Content @'
# Compact Mortal experience and level template

Use this before Mortal World turns that award ordinary XP, resolve combat rewards, cross a level threshold, or need a controlled level-up test.

## Mortal experience rule

- Mortal XP lives in `game_state/player/experience.json`.
- Fresh Mortal bootstrap creates the baseline file with `playerLevel`, `level`, `currentExperience`, `experience`, `totalExperience`, `experienceForNextLevel`, and `experienceGained`.
- If a Mortal World scene grants XP, update the XP file in the same turn and include `game_state/player/experience.json` in `Complete-BoeTurn -FilesModified`.
- If updated `totalExperience >= experienceForNextLevel`, the level-up is not materialized yet. Advance `playerLevel` and `level`, then set `experienceForNextLevel` to the next threshold above `totalExperience`; otherwise validation will request repair.
- Do not leave a meaningful victory, completed objective, or combat reward prose-only when the scene clearly grants mechanical progress.
- Do not directly award stat points for ordinary level-up. The client-owned level-up/stat allocation flow grants stat points after it detects a higher `playerLevel`/`level`.
- Do not edit or remove `game_state/player/stat_points.json.levelUpStatPointsAwardedThroughLevel`; it is the client-owned restart-safe marker that prevents duplicate level-up stat-point awards.
- `statsIncreased` is for training a characteristic under Training Cap, not for distributing level-up points.

## Level-up metadata

The client detects level-up by comparing the new `playerLevel` or `level` in `experience.json` against the previous known level.

Keep both `playerLevel` and `level` synchronized when possible:

```json
{
  "playerLevel": 2,
  "level": 2,
  "currentExperience": 10,
  "experience": 10,
  "totalExperience": 110,
  "experienceForNextLevel": 150,
  "experienceGained": 25
}
```

If the turn does not cross a threshold, keep `playerLevel`/`level` unchanged and only advance counters:

```json
{
  "playerLevel": 1,
  "level": 1,
  "currentExperience": 35,
  "experience": 35,
  "totalExperience": 35,
  "experienceForNextLevel": 100,
  "experienceGained": 15
}
```

## Combat reward discipline

- Ordinary Mortal World combat may use `activeSkillChanges`, `skillMasteryChanges`, and `experienceGained` together when the scene used a known active skill and awarded XP.
- Skill mastery is separate from XP: update `skillMasteryChanges` for used active skills, and update `experience.json` for character-level progress.
- Explain the reward in player-facing Russian, but keep mechanical counters in `experience.json`.
- If the player should level now during a live test, ensure the final `totalExperience` crosses the old `experienceForNextLevel`, set the resulting `playerLevel`/`level` to the next level, and move `experienceForNextLevel` beyond the final `totalExperience`.

## Output checklist

- Read the current `game_state/player/experience.json` first.
- Write the updated `experience.json` atomically through the GM turn helper.
- Include `game_state/player/experience.json` in `Complete-BoeTurn -FilesModified`.
- Do not edit `game_state/player/stat_points.json` for ordinary level-up; the client handles stat points after turn acceptance.
- If `stat_points.json` exists, preserve its client-owned `levelUpStatPointsAwardedThroughLevel` marker.
'@
    $templates += Write-GmContextPackTemplate -RelativePath "Templates\MORTAL_COMBAT_STATE_TEMPLATE.md" -Role "compact_mortal_combat_state_template" -Content @'
# Compact Mortal combat state template

Use this before Mortal World turns that resolve open combat, create an enemy exchange, award combat XP, update active-skill mastery from a combat action, or change health/energy/poise because of a fight.

## Player-facing rule

- `/бой` must remain useful after a player-facing combat scene.
- If the fight is still active, write enemies/allies plus a combat log.
- If the fight ended in the same turn, still write `game_state/combat/combat_log.json` with the recent exchange and outcome so `/бой` can explain what just happened.
- Do not leave a visible enemy, duel, ambush, monster, guard, or protective shadow only in prose when XP, active-skill mastery, or combat resources changed.

## Canonical files

- `game_state/combat/combat_log.json`: recent combat summary for `/бой`; always write this for explicit combat.
- `game_state/combat/enemies.json`: active, defeated, fled, or suppressed opponents when the enemy remains tactically relevant.
- `game_state/combat/allies.json`: allies who participated tactically or whose state matters.
- `game_state/player/skill_mastery.json`: active skill practice/progress when a known active skill was used.
- `game_state/player/experience.json`: ordinary Mortal combat XP.
- `game_state/core/player_status.json`: health/energy/poise/status changes.

## Minimal ended-in-one-turn combat log

If the fight is over and no enemy remains active, it is valid to write only a combat log. Keep it player-facing and specific:

```json
{
  "combat_log_markdown": "Открытый бой с клятвенной тенью завершён: «Быстрый выпад» пробил ядро защитного узла. Тень рассеялась, но удар стоил энергии и оставил усталость."
}
```

## Active enemy shape guidance

When the opponent remains relevant, keep enough structure for `/бой`:

- id/name/displayName
- type/archetype or short role
- currentHealth/currentPoise as readable percentages or canonical values accepted by validation
- status: active, defeated, fled, suppressed, restrained, or comparable canonical/readable state
- actions[] with at least one useful combat action if it can still act
- resistances/activeBuffs/activeDebuffs only when meaningful
- player-facing description of what the enemy is doing

## Output checklist

- If the narrative says "открытый бой", "вступает в бой", "бой с ...", or shows a concrete enemy exchange, write `combat_log.json`.
- If XP is awarded, update `experience.json` using `MORTAL_EXPERIENCE_LEVEL_TEMPLATE.md`.
- If an active skill was used, update `skill_mastery.json` using `MORTAL_SKILL_PROGRESSION_TEMPLATE.md` and keep the accepted-turn evidence aligned with `skillMasteryChanges`.
- Include every touched combat/player file in `Complete-BoeTurn -FilesModified`.
- If validation repair is requested with `mortal_combat_state_repair`, repair the existing combat scene; do not delete XP/mastery/status changes to avoid the combat state.
'@
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

    $experienceLessons = Write-GmExperienceLessons
    $safeProbes = Write-GmSafeProbes
    $liveTestRubric = Write-GmLiveTestRubric

    $readmePath = Join-Path $script:GmContextPackRoot "README.md"
    $readme = @"
# GM Session Context Pack

This folder is generated for the current live game session.

Start here instead of browsing repository implementation code.

- Read context_pack_manifest.json first.
- Bootstrap scope: read only context_pack_manifest.json and README.md.
- Do not open copied guides/examples during bootstrap; they are large and route-specific.
- Use Probes/GM_SAFE_PROBES.md for bounded read-only context questions before implementation source.
- Use Templates/* before opening large copied examples for common turn, repair, progression, Mortal World NPC updates, Mortal World faction updates, Mortal World location transitions, Mortal World skill progression, Mortal World experience and level progression, Mortal World combat state, actor reasoning, and tempoAdvantage shapes.
- Use Lessons/GM_EXPERIENCE_LESSONS.md as short hints only; validators and current templates remain authoritative.
- Ordinary turn prompts may include an inline RLM PRE-TURN LESSONS digest when relevant; use that compact digest before writing state.
- Use Rubrics/GM_LIVE_TEST_RUBRIC.md during live tests to record harness friction and follow-up notes in game_state/control/gm_live_test_notes.jsonl.
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
        experienceLessons = $experienceLessons
        safeProbes = $safeProbes
        liveTestRubric = $liveTestRubric
        rules = @(
            "Bootstrap scope: read only context_pack_manifest.json and README.md.",
            "Do not open copied guides/examples during bootstrap; open them only when a per-turn, repair, or terminal-failure prompt explicitly names them.",
            "Use Probes/GM_SAFE_PROBES.md for bounded read-only context probes before considering implementation source.",
            "Use compact Templates/* before opening large copied examples for common turn, repair, progression, Mortal World NPC/faction update, Mortal World location transitions, Mortal World skill progression, Mortal World experience and level progression, Mortal World combat state, afterlife chronicle memory, actor reasoning, and tempoAdvantage field names.",
            "Use Lessons/GM_EXPERIENCE_LESSONS.md as hints only; validators and current compact templates remain authoritative.",
            "Use inline RLM PRE-TURN LESSONS in ordinary prompts before writing state; they are compact reminders, not validator replacements.",
            "Use Rubrics/GM_LIVE_TEST_RUBRIC.md during live tests to connect observations to gm_trajectory_ledger.jsonl record ids and follow-up issues.",
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
    $script:GmContextPackDirective = " GM session context pack is the first authority: Manifest='$($script:GmContextPackManifestPath)', Root='$($script:GmContextPackRoot)', README='$readmePath'. Bootstrap scope: read only context_pack_manifest.json and README.md. Do not open copied guides/examples during bootstrap; open large copied docs only when a per-turn, repair, or terminal-failure prompt explicitly names them."
    $script:GmDocPathDirective = " GM documentation paths are session-local and authoritative: TaskGuide='$($script:TaskGuideMainPath)', MainExample='$($script:ExampleMainPath)', AfterlifeMatrix='$($script:AfterlifeMatrixPath)', AfterlifeTurns='$($script:AfterlifeTurnsExamplePath)'. Do not search repository source or other worktrees for GM docs; use these context-pack paths first."
    $script:GmCompactTemplateDirective = " Compact GM templates are first for executable shapes: Turn='$($script:CompactTurnOutputTemplatePath)', Repair='$($script:CompactValidationRepairTemplatePath)', OutputArtifactRepair='$($script:CompactOutputArtifactRepairTemplatePath)', ProgressionReport='$($script:CompactProgressionReportTemplatePath)', ActorReasoning='$($script:CompactActorReasoningTemplatePath)', MortalNpc='$($script:CompactMortalNpcTemplatePath)', MortalFaction='$($script:CompactMortalFactionTemplatePath)', MortalLocation='$($script:CompactMortalLocationTemplatePath)', MortalSkill='$($script:CompactMortalSkillTemplatePath)', MortalExperience='$($script:CompactMortalExperienceTemplatePath)', MortalCombat='$($script:CompactMortalCombatTemplatePath)', AfterlifeChronicle='$($script:CompactAfterlifeChronicleTemplatePath)', TempoAdvantage='$($script:CompactTempoAdvantageTemplatePath)'. Use these before opening large copied examples; open long examples only for route-specific contracts not covered by compact templates. For accepted_turn_output_artifact_repair, use OutputArtifactRepair before broad repair examples. Direct-speaking or directly addressed Mortal actors must not be excluded only because their personal name is unknown; use MortalNpc template and a stable role-based visible name when a visible actor speaks, acts, gives clues, receives a player action, or blocks/opens a route. For Mortal World training, skill unlocks, active skill use, or mastery updates, use MortalSkill template and avoid prose-only learning unless this turn is explicitly only early practice. For Mortal World XP rewards, combat rewards, level-up thresholds, or stat-allocation tests, use MortalExperience template and update game_state/player/experience.json rather than leaving rewards prose-only. For Mortal World open combat, enemy exchanges, combat XP, active-skill combat mastery, or combat resource changes, use MortalCombat template and leave /бой useful through game_state/combat/combat_log.json plus enemies/allies when relevant."
    $script:GmExperienceLessonsDirective = " Experience lessons are hints only: '$($script:GmExperienceLessonMarkdownPath)'. Use them to avoid repeated mistakes, but current validators, repair packets, and compact templates remain authoritative."
    $script:GmSafeProbeDirective = " Safe GM probes are first for bounded context questions: '$($script:GmSafeProbeMarkdownPath)'. They are read-only; if a needed fact is missing, record a missing harness surface instead of treating implementation source as normal workflow."
    $script:GmSourceFallbackDirective = " Do not read implementation code such as BookOfEternityClient/**/*.cs during normal play or validation repair; use safe GM probes, validation_repair_request.json.harnessRepairPackets, session state/control files, helper commands, and named copied GM docs instead."
    $script:GmLiveTestRubricDirective = " Live-test rubric: '$($script:GmLiveTestRubricMarkdownPath)'. When running a harness live test, tie notable observations to gm_trajectory_ledger.jsonl records and append structured notes to game_state/control/gm_live_test_notes.jsonl."
}

Write-GmTurnHelperBootstrap
Write-GmContextPack

$script:LastRepairRequestWrite = [datetime]::MinValue
$script:LastTerminalProtocolFailureWrite = [datetime]::MinValue
$script:BridgeAutoStartAttempted = $false

function New-DefaultGmWorkerBridgeProfiles {
    $runner = 'BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1'
    $codexWorker = 'codex exec -m gpt-5.5 -c model_reasoning_effort=\"high\" --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -'

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
        },
        [ordered]@{
            workerId = "guardian_abode_content_codex"
            displayName = "Codex Guardian/Abode content author"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "guardian-abode-content"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("guardian-abode-content")
                readPaths = @("game_state/meta/guardians.json", "game_state/meta/guardian_projects.json", "game_state/meta/guardian_abode_residents.json", "game_state/meta/abode_power_journal.json", "game_state/meta/chaos_sea_guardian_politics.json", "game_state/meta/afterlife_chronicles.json", "game_state/control/system_guardian_attraction.json", "game_state/control/afterlife_return_guard.json", "game_state/control/progression_schedule.json", "OtherGuides/Afterlife_Contract_Matrix.md", "Examples/E_CLI_Afterlife_Turns.txt")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        },
        [ordered]@{
            workerId = "soul_content_codex"
            displayName = "Codex soul content author"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "soul-content"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("soul-content")
                readPaths = @("game_state/meta/soul_state.json", "game_state/meta/afterlife_chronicles.json", "game_state/meta/afterlife_global_flags.json", "game_state/control/progression_schedule.json", "game_state/control/pending_dice_state.json", "OtherGuides/Afterlife_Contract_Matrix.md", "Examples/E_CLI_Afterlife_Turns.txt")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        },
        [ordered]@{
            workerId = "inventory_content_codex"
            displayName = "Codex inventory content author"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "inventory-content"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("inventory-content")
                readPaths = @("game_state/core/**", "game_state/inventory/**", "game_state/world/**", "game_state/skills/**", "lore/**", "Rules/**", "TaskGuides/**")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        },
        [ordered]@{
            workerId = "skill_content_codex"
            displayName = "Codex skill content author"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "skill-content"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("skill-content")
                readPaths = @("game_state/core/**", "game_state/player/**", "game_state/skills/**", "game_state/combat/**", "game_state/world/**", "lore/**", "Rules/**", "TaskGuides/**")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        },
        [ordered]@{
            workerId = "npc_content_codex"
            displayName = "Codex NPC content author"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "npc-content"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("npc-content")
                readPaths = @("game_state/core/**", "game_state/npcs/**", "game_state/factions/**", "game_state/quests/**", "game_state/world/**", "lore/**", "Rules/**", "TaskGuides/**")
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
        GmCliLaunchCommand = 'codex -m gpt-5.5 -c model_reasoning_effort="high" --dangerously-bypass-approvals-and-sandbox'
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
                $loaded | Add-Member -NotePropertyName $key -NotePropertyValue $defaults[$key] -Force
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

function Refresh-GmBridgeReadiness {
    if (!(Test-Path $BridgeControlScript)) {
        return $null
    }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $responseJson = (& $BridgeControlScript diagnostics -SessionPath $GameSessionPath) -join "`n"
        if ([string]::IsNullOrWhiteSpace($responseJson)) {
            Write-Log "  -> GM bridge readiness refresh via diagnostics returned an empty response." -Level "WARN" -Color Yellow
            return $null
        }

        $response = $responseJson | ConvertFrom-Json
        $stopwatch.Stop()
        $state = if ($null -ne $response.status -and $response.status.state) { [string]$response.status.state } else { "<unknown>" }
        $ready = $false
        if ($null -ne $response.status -and $null -ne $response.status.ready) {
            $ready = [bool]$response.status.ready
        }

        Write-Log "  -> GM bridge readiness refresh via diagnostics: ready=$ready state=$state elapsedMs=$($stopwatch.ElapsedMilliseconds)" -Color DarkGray
        return $response.status
    }
    catch {
        $stopwatch.Stop()
        Write-Log "  -> GM bridge readiness refresh via diagnostics failed after $($stopwatch.ElapsedMilliseconds)ms: $_" -Level "WARN" -Color Yellow
        return $null
    }
}

function Get-GmBridgeDiagnosticsSnapshot {
    $config = Get-GameConfig
    if (-not $config.GmBridgeEnabled -or $config.GmBridgeBackend -ne "ConPTYBridge") {
        return $null
    }

    if (!(Test-Path $BridgeControlScript)) {
        return $null
    }

    try {
        $responseJson = (& $BridgeControlScript diagnostics -SessionPath $GameSessionPath) -join "`n"
        if ([string]::IsNullOrWhiteSpace($responseJson)) {
            return $null
        }

        return $responseJson | ConvertFrom-Json
    }
    catch {
        Write-Log "  -> GM bridge idle-terminal diagnostics failed: $_" -Level "WARN" -Color Yellow
        return $null
    }
}

function Test-GmBridgeDiagnosticsIndicatesActiveCodexWork {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $false
    }

    $normalized = $Text.Replace([char]0x00A0, ' ')
    return $normalized.IndexOf("Working", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -and
        $normalized.IndexOf("esc to interrupt", [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Test-GmBridgeReturnedIdleWithoutTerminalSignal {
    param(
        [int]$ElapsedSeconds,
        [int]$MinimumElapsedSeconds = 30
    )

    if ($ElapsedSeconds -lt $MinimumElapsedSeconds) {
        return $false
    }

    $snapshot = Get-GmBridgeDiagnosticsSnapshot
    if ($null -eq $snapshot) {
        return $false
    }

    $ready = $false
    if ($null -ne $snapshot.status -and $null -ne $snapshot.status.ready) {
        $ready = [bool]$snapshot.status.ready
    }

    $visibleScreenText = if ($null -ne $snapshot.diagnostics -and $null -ne $snapshot.diagnostics.visibleScreenText) {
        [string]$snapshot.diagnostics.visibleScreenText
    } else {
        ""
    }

    $recentOutputTail = if ($null -ne $snapshot.diagnostics -and $null -ne $snapshot.diagnostics.recentOutputTail) {
        [string]$snapshot.diagnostics.recentOutputTail
    } else {
        ""
    }

    $combinedOutput = $visibleScreenText + "`n" + $recentOutputTail
    if (Test-GmBridgeDiagnosticsIndicatesActiveCodexWork -Text $visibleScreenText) {
        return $false
    }

    $hasCodexIdlePrompt =
        $combinedOutput.IndexOf("› Implement {feature}", [System.StringComparison]::Ordinal) -ge 0 -or
        $combinedOutput.IndexOf("> Implement {feature}", [System.StringComparison]::Ordinal) -ge 0 -or
        $combinedOutput.IndexOf("› Write tests for @filename", [System.StringComparison]::Ordinal) -ge 0 -or
        $combinedOutput.IndexOf("> Write tests for @filename", [System.StringComparison]::Ordinal) -ge 0

    return ($ready -and $hasCodexIdlePrompt)
}

function ConvertTo-GmSessionRelativePath {
    param([string]$FullName)

    if ([string]::IsNullOrWhiteSpace($FullName)) {
        return ""
    }

    $root = [IO.Path]::GetFullPath($GameSessionPath)
    if (-not $root.EndsWith([IO.Path]::DirectorySeparatorChar)) {
        $root += [IO.Path]::DirectorySeparatorChar
    }

    $full = [IO.Path]::GetFullPath($FullName)
    if ($full.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($root.Length).Replace("\", "/")
    }

    return $full.Replace("\", "/")
}

function Test-GmTerminalPayloadCandidatePath {
    param([string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $false
    }

    $normalized = $RelativePath.Replace("\", "/")
    if ($normalized.EndsWith(".tmp", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    if ($normalized.StartsWith("game_state/control/", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    return $normalized.StartsWith("output/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("game_state/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("lore/", [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-GmTerminalPayloadFileSnapshot {
    $snapshot = @{}
    $roots = @(
        $OutputDir,
        (Join-Path $GameSessionPath "game_state"),
        (Join-Path $GameSessionPath "lore")
    )

    foreach ($root in $roots) {
        if (!(Test-Path $root)) {
            continue
        }

        Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            $relativePath = ConvertTo-GmSessionRelativePath -FullName $_.FullName
            if (Test-GmTerminalPayloadCandidatePath -RelativePath $relativePath) {
                $snapshot[$relativePath] = "$($_.LastWriteTimeUtc.Ticks):$($_.Length)"
            }
        }
    }

    return $snapshot
}

function New-GmOutputWithoutTerminalWatchState {
    return @{
        baseline = Get-GmTerminalPayloadFileSnapshot
        firstPayloadElapsed = -1
        lastProgressElapsed = 0
        lastChangeSignature = ""
        lastChangedFiles = @()
    }
}

function Test-GmOutputWithoutTerminalSignal {
    param(
        [int]$ElapsedSeconds,
        [hashtable]$WatchState,
        [int]$MinimumElapsedSeconds = $script:OutputWithoutTerminalMinimumSeconds,
        [int]$NoProgressSeconds = $script:OutputWithoutTerminalNoProgressSeconds
    )

    if ($null -eq $WatchState) {
        $WatchState = New-GmOutputWithoutTerminalWatchState
    }

    $baseline = if ($WatchState.ContainsKey("baseline") -and $null -ne $WatchState.baseline) { $WatchState.baseline } else { @{} }
    $current = Get-GmTerminalPayloadFileSnapshot
    $changedFiles = @()

    foreach ($key in @($current.Keys | Sort-Object)) {
        if (-not $baseline.ContainsKey($key) -or [string]$baseline[$key] -ne [string]$current[$key]) {
            $changedFiles += [string]$key
        }
    }

    $changeSignature = ($changedFiles | ForEach-Object { "$_=$($current[$_])" }) -join "|"
    if ($changedFiles.Count -gt 0 -and [string]$WatchState.lastChangeSignature -ne $changeSignature) {
        $WatchState.lastChangeSignature = $changeSignature
        $WatchState.lastProgressElapsed = $ElapsedSeconds
        $WatchState.lastChangedFiles = @($changedFiles)
        if (-not $WatchState.ContainsKey("firstPayloadElapsed") -or [int]$WatchState.firstPayloadElapsed -lt 0) {
            $WatchState.firstPayloadElapsed = $ElapsedSeconds
        }
    }

    $firstPayloadElapsed = if ($WatchState.ContainsKey("firstPayloadElapsed")) { [int]$WatchState.firstPayloadElapsed } else { -1 }
    $lastProgressElapsed = if ($WatchState.ContainsKey("lastProgressElapsed")) { [int]$WatchState.lastProgressElapsed } else { $ElapsedSeconds }
    $payloadAgeSeconds = if ($firstPayloadElapsed -ge 0) { [Math]::Max(0, $ElapsedSeconds - $firstPayloadElapsed) } else { 0 }
    $noProgressElapsed = [Math]::Max(0, $ElapsedSeconds - $lastProgressElapsed)
    $isStalled = (
        $changedFiles.Count -gt 0 -and
        $ElapsedSeconds -ge $MinimumElapsedSeconds -and
        $payloadAgeSeconds -ge $NoProgressSeconds -and
        $noProgressElapsed -ge $NoProgressSeconds
    )

    return [pscustomobject]([ordered]@{
        isStalled = $isStalled
        harnessSource = "gm_output_without_terminal_signal"
        elapsedSeconds = $ElapsedSeconds
        minimumElapsedSeconds = $MinimumElapsedSeconds
        noProgressSeconds = $NoProgressSeconds
        payloadAgeSeconds = $payloadAgeSeconds
        noProgressElapsedSeconds = $noProgressElapsed
        changedFileCount = $changedFiles.Count
        changedFiles = @($changedFiles | Select-Object -First 40)
    })
}

function Test-GmBridgeArtifactWritingIntent {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $false
    }

    return $Text.IndexOf("Complete-BoeTurn", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $Text.IndexOf("ready/turn_complete.json", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $Text.IndexOf("turn artifacts", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $Text.IndexOf("accepted-turn artifacts", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $Text.IndexOf("writing the", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $Text.IndexOf("write the", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $Text.IndexOf("emit Complete-BoeTurn", [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Test-GmBridgeArtifactWritingStall {
    param(
        [int]$ElapsedSeconds,
        [hashtable]$WatchState,
        [int]$MinimumElapsedSeconds = $script:ArtifactWritingStallMinimumSeconds,
        [int]$NoProgressSeconds = $script:ArtifactWritingStallNoProgressSeconds
    )

    if ($null -eq $WatchState) {
        $WatchState = @{}
    }

    $snapshot = Get-GmBridgeDiagnosticsSnapshot
    if ($null -eq $snapshot) {
        return $null
    }

    $ready = $false
    $state = "<unknown>"
    if ($null -ne $snapshot.status) {
        if ($null -ne $snapshot.status.ready) {
            $ready = [bool]$snapshot.status.ready
        }
        if ($snapshot.status.state) {
            $state = [string]$snapshot.status.state
        }
    }

    $outputVersion = -1
    if ($null -ne $snapshot.diagnostics -and $null -ne $snapshot.diagnostics.outputVersion) {
        $outputVersion = [int]$snapshot.diagnostics.outputVersion
    }

    if (-not $WatchState.ContainsKey("lastOutputVersion") -or [int]$WatchState.lastOutputVersion -ne $outputVersion) {
        $WatchState.lastOutputVersion = $outputVersion
        $WatchState.lastProgressElapsed = $ElapsedSeconds
    }

    $visibleScreenText = if ($null -ne $snapshot.diagnostics -and $null -ne $snapshot.diagnostics.visibleScreenText) {
        [string]$snapshot.diagnostics.visibleScreenText
    } else {
        ""
    }

    $recentOutputTail = if ($null -ne $snapshot.diagnostics -and $null -ne $snapshot.diagnostics.recentOutputTail) {
        [string]$snapshot.diagnostics.recentOutputTail
    } else {
        ""
    }

    $combinedOutput = $visibleScreenText + "`n" + $recentOutputTail
    $hasArtifactIntent = Test-GmBridgeArtifactWritingIntent -Text $combinedOutput

    if ($hasArtifactIntent -and -not $WatchState.ContainsKey("firstArtifactIntentElapsed")) {
        $WatchState.firstArtifactIntentElapsed = $ElapsedSeconds
    }

    $lastProgressElapsed = if ($WatchState.ContainsKey("lastProgressElapsed")) { [int]$WatchState.lastProgressElapsed } else { $ElapsedSeconds }
    $firstArtifactIntentElapsed = if ($WatchState.ContainsKey("firstArtifactIntentElapsed")) { [int]$WatchState.firstArtifactIntentElapsed } else { -1 }
    $noProgressElapsed = [Math]::Max(0, $ElapsedSeconds - $lastProgressElapsed)
    $artifactIntentAgeSeconds = if ($firstArtifactIntentElapsed -ge 0) { [Math]::Max(0, $ElapsedSeconds - $firstArtifactIntentElapsed) } else { 0 }
    $isStalled = (
        -not $ready -and
        $hasArtifactIntent -and
        $ElapsedSeconds -ge $MinimumElapsedSeconds -and
        $noProgressElapsed -ge $NoProgressSeconds -and
        $artifactIntentAgeSeconds -ge $NoProgressSeconds
    )

    return [pscustomobject]([ordered]@{
        isStalled = $isStalled
        harnessSource = "gm_bridge_artifact_write_stall"
        state = $state
        ready = $ready
        elapsedSeconds = $ElapsedSeconds
        minimumElapsedSeconds = $MinimumElapsedSeconds
        noProgressSeconds = $NoProgressSeconds
        noProgressElapsedSeconds = $noProgressElapsed
        artifactIntentAgeSeconds = $artifactIntentAgeSeconds
        outputVersion = $outputVersion
        visibleScreenText = $visibleScreenText
        recentOutputTail = $recentOutputTail
    })
}

function ConvertTo-GmValidationRepairTargetPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    $normalized = $Path.Trim().Replace("\", "/")
    $match = [regex]::Match($normalized, "([A-Za-z0-9_\-./]+\.json)")
    if (-not $match.Success) {
        return ""
    }

    return $match.Groups[1].Value.TrimStart(".", "/")
}

function Get-GmValidationRepairTargetFiles {
    param([object]$RepairRequest)

    $targets = [System.Collections.Generic.List[string]]::new()

    foreach ($packetCollectionName in @("harnessRepairPackets", "repairPackets")) {
        $packetCollection = $RepairRequest.PSObject.Properties[$packetCollectionName]
        if ($null -eq $packetCollection -or $null -eq $packetCollection.Value) {
            continue
        }

        foreach ($packet in @($packetCollection.Value)) {
            if ($null -ne $packet.targetFiles) {
                foreach ($targetFile in @($packet.targetFiles)) {
                    $target = ConvertTo-GmValidationRepairTargetPath -Path ([string]$targetFile)
                    if ($target) { $targets.Add($target) }
                }
            }
        }
    }

    if ($null -ne $RepairRequest.errors) {
        foreach ($err in @($RepairRequest.errors)) {
            foreach ($fieldName in @("path", "filePath")) {
                $field = $err.PSObject.Properties[$fieldName]
                if ($null -ne $field -and $null -ne $field.Value) {
                    $target = ConvertTo-GmValidationRepairTargetPath -Path ([string]$field.Value)
                    if ($target) { $targets.Add($target) }
                }
            }
        }
    }

    if ($targets.Count -eq 0) {
        $targets.Add("output/narrative_response.json")
        $targets.Add("output/debug_logs.json")
    }

    return @($targets | Select-Object -Unique)
}

function Get-GmValidationRepairTargetSnapshot {
    param([string[]]$TargetFiles)

    $snapshot = [ordered]@{}
    foreach ($relativePath in @($TargetFiles)) {
        $normalized = ConvertTo-GmValidationRepairTargetPath -Path $relativePath
        if (-not $normalized) {
            continue
        }

        $fullPath = Join-Path $GameSessionPath $normalized
        if (Test-Path $fullPath) {
            $item = Get-Item $fullPath
            $snapshot[$normalized] = "$($item.LastWriteTimeUtc.Ticks):$($item.Length)"
        }
        else {
            $snapshot[$normalized] = "<missing>"
        }
    }

    return $snapshot
}

function ConvertTo-GmValidationRepairSnapshotSignature {
    param([object]$Snapshot)

    return (($Snapshot.GetEnumerator() | Sort-Object Key | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "|")
}

function New-GmValidationRepairArtifactWatchState {
    param(
        [object]$RepairRequest,
        [string]$DispatchStatus
    )

    $targetFiles = @(Get-GmValidationRepairTargetFiles -RepairRequest $RepairRequest)
    $snapshot = Get-GmValidationRepairTargetSnapshot -TargetFiles $targetFiles
    $now = (Get-Date).ToUniversalTime()
    return @{
        request = $RepairRequest
        dispatchStatus = $DispatchStatus
        startedAtUtc = $now
        lastProgressUtc = $now
        targetFiles = $targetFiles
        lastSnapshotSignature = ConvertTo-GmValidationRepairSnapshotSignature -Snapshot $snapshot
    }
}

function Get-GmValidationRepairWatchCorrelation {
    param([object]$RepairRequest)

    if ($null -eq $RepairRequest) {
        return ""
    }

    $sessionId = if ($RepairRequest.sessionId) { [string]$RepairRequest.sessionId } else { "" }
    $requestId = if ($RepairRequest.requestId) { [string]$RepairRequest.requestId } else { "" }
    $turnNumber = if ($null -ne $RepairRequest.turnNumber) { [string]$RepairRequest.turnNumber } else { "" }
    $attempt = if ($null -ne $RepairRequest.revalidationAttempt) { [string]$RepairRequest.revalidationAttempt } else { "" }
    return "$sessionId|$requestId|$turnNumber|$attempt"
}

function Test-GmValidationRepairWatchStillCurrent {
    param([hashtable]$WatchState)

    if ($null -eq $WatchState) {
        return $false
    }

    if (-not (Test-Path $RepairRequestFile)) {
        return $false
    }

    try {
        $currentRepairRequest = Get-Content -Path $RepairRequestFile -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        return $false
    }

    $watchedCorrelation = Get-GmValidationRepairWatchCorrelation -RepairRequest $WatchState.request
    $currentCorrelation = Get-GmValidationRepairWatchCorrelation -RepairRequest $currentRepairRequest
    return -not [string]::IsNullOrWhiteSpace($watchedCorrelation) -and
        [string]::Equals($watchedCorrelation, $currentCorrelation, [System.StringComparison]::Ordinal)
}

function Test-GmValidationRepairArtifactWritingStall {
    param(
        [hashtable]$WatchState,
        [int]$MinimumElapsedSeconds = $script:ValidationRepairArtifactStallMinimumSeconds,
        [int]$NoProgressSeconds = $script:ValidationRepairArtifactStallNoProgressSeconds
    )

    if ($null -eq $WatchState) {
        return $null
    }

    $readyPath = Join-Path $ControlDir "validation_repair_ready.json"
    if (Test-Path $readyPath) {
        return [pscustomobject]([ordered]@{
            isStalled = $false
            completed = $true
            harnessSource = "gm_validation_repair_artifact_stall"
        })
    }

    $targetFiles = @($WatchState.targetFiles)
    $snapshot = Get-GmValidationRepairTargetSnapshot -TargetFiles $targetFiles
    $signature = ConvertTo-GmValidationRepairSnapshotSignature -Snapshot $snapshot
    $now = (Get-Date).ToUniversalTime()
    if ([string]$WatchState.lastSnapshotSignature -ne $signature) {
        $WatchState.lastSnapshotSignature = $signature
        $WatchState.lastProgressUtc = $now
    }

    $elapsedSeconds = [int][Math]::Floor(($now - [datetime]$WatchState.startedAtUtc).TotalSeconds)
    $noProgressElapsed = [int][Math]::Floor(($now - [datetime]$WatchState.lastProgressUtc).TotalSeconds)
    $isStalled = (
        $targetFiles.Count -gt 0 -and
        $elapsedSeconds -ge $MinimumElapsedSeconds -and
        $noProgressElapsed -ge $NoProgressSeconds
    )

    return [pscustomobject]([ordered]@{
        isStalled = $isStalled
        completed = $false
        harnessSource = "gm_validation_repair_artifact_stall"
        elapsedSeconds = $elapsedSeconds
        minimumElapsedSeconds = $MinimumElapsedSeconds
        noProgressSeconds = $NoProgressSeconds
        noProgressElapsedSeconds = $noProgressElapsed
        dispatchStatus = [string]$WatchState.dispatchStatus
        targetFiles = @($targetFiles)
        currentSnapshot = $snapshot
    })
}

function Watch-ActiveValidationRepairProgress {
    if ($null -eq $script:ActiveValidationRepairWatch) {
        return
    }

    if (-not (Test-GmValidationRepairWatchStillCurrent -WatchState $script:ActiveValidationRepairWatch)) {
        Write-Log "  Active validation repair request disappeared or changed; clearing artifact watch." -Level "INFO" -Color DarkGray
        $script:ActiveValidationRepairWatch = $null
        return
    }

    $stall = Test-GmValidationRepairArtifactWritingStall -WatchState $script:ActiveValidationRepairWatch
    if ($null -eq $stall) {
        return
    }

    if ($stall.completed) {
        $script:ActiveValidationRepairWatch = $null
        return
    }

    if (-not $stall.isStalled) {
        return
    }

    Write-Log "  Validation repair appears stalled without target artifact progress; stopping GM bridge." -Level "ERROR" -Color Red
    [void](Write-DaemonJsonFileBestEffort -Path $ValidationRepairArtifactStallReportFile -Payload $stall -Depth 10)
    $repairRequest = $script:ActiveValidationRepairWatch.request
    $cleanup = Stop-GmBridgeAfterTurnTimeout -TurnRequest $repairRequest -ElapsedSeconds ([int]$stall.elapsedSeconds) -Reason "gm_validation_repair_artifact_stall"
    $stall | Add-Member -NotePropertyName bridgeCleanup -NotePropertyValue $cleanup -Force
    [void](Write-DaemonJsonFileBestEffort -Path $ValidationRepairArtifactStallReportFile -Payload $stall -Depth 10)
    $repairAttempts = if ($repairRequest.revalidationAttempt) { [int]$repairRequest.revalidationAttempt } else { 1 }
    Write-GmTrajectoryRecord `
        -Kind "repair" `
        -Mode "validation_repair" `
        -RequestObject $repairRequest `
        -Dispatch (New-GmDispatchDiagnostics -Status "gm_validation_repair_artifact_stall" -Attempts 0 -BusyRetries 0 -Timeout $true) `
        -ValidationStatus "rejected" `
        -IssueKinds (Get-GmTrajectoryIssueKinds -RequestObject $repairRequest) `
        -RepairPacketRefs (Get-GmTrajectoryRepairPacketRefs -RequestObject $repairRequest) `
        -ValidationDiagnostics (Get-GmTrajectoryValidationDiagnostics -RequestObject $repairRequest) `
        -RepairPacketDiagnostics (Get-GmTrajectoryRepairPacketDiagnostics -RequestObject $repairRequest) `
        -RepairAttempts $repairAttempts `
        -RepairStatus "stalled" `
        -MissingHarnessTool "gm_validation_repair_artifact_stall"
    $script:ActiveValidationRepairWatch = $null
}

function Stop-GmBridgeAfterTurnTimeout {
    param(
        [psobject]$TurnRequest,
        [int]$ElapsedSeconds,
        [string]$Reason = "gm_turn_timeout"
    )

    $cleanup = [ordered]@{
        status = "not_attempted"
        reason = $Reason
        sessionId = if ($TurnRequest.sessionId) { [string]$TurnRequest.sessionId } else { "" }
        requestId = if ($TurnRequest.requestId) { [string]$TurnRequest.requestId } else { "" }
        turnNumber = if ($null -ne $TurnRequest.turnNumber) { [int]$TurnRequest.turnNumber } else { -1 }
        elapsedSeconds = $ElapsedSeconds
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
        bridgeCommand = "shutdown-bridge"
        ok = $false
        fallbackUsed = $false
        processIds = @()
        stoppedProcessIds = @()
        remainingProcessIds = @()
        error = ""
    }

    if (!(Test-Path $BridgeControlScript)) {
        $cleanup.status = "bridge-control-missing"
        $cleanup.error = "GM bridge control script not found."
        [void](Write-DaemonJsonFileBestEffort -Path $TimeoutBridgeCleanupFile -Payload $cleanup -Depth 8)
        Write-Log "  -> GM bridge timeout cleanup skipped: control script missing." -Level "WARN" -Color Yellow
        return [pscustomobject]$cleanup
    }

    try {
        $responseJson = (& $BridgeControlScript shutdown-bridge -SessionPath $GameSessionPath) -join "`n"
        if ([string]::IsNullOrWhiteSpace($responseJson)) {
            $cleanup.status = "empty-response"
            $cleanup.error = "shutdown-bridge returned an empty response."
        }
        else {
            $response = $responseJson | ConvertFrom-Json
            $cleanup.status = if ($response.status) { [string]$response.status } else { "shutdown-response" }
            $cleanup.ok = if ($null -ne $response.ok) { [bool]$response.ok } else { $false }
            $cleanup.fallbackUsed = if ($null -ne $response.fallbackUsed) { [bool]$response.fallbackUsed } else { $false }
            $cleanup.processIds = if ($null -ne $response.processIds) { @($response.processIds) } else { @() }
            $cleanup.stoppedProcessIds = if ($null -ne $response.stoppedProcessIds) { @($response.stoppedProcessIds) } else { @() }
            $cleanup.remainingProcessIds = if ($null -ne $response.remainingProcessIds) { @($response.remainingProcessIds) } else { @() }
            if ($response.error) {
                $cleanup.error = [string]$response.error
            }
        }
    }
    catch {
        $cleanup.status = "shutdown-failed"
        $cleanup.error = $_.Exception.Message
    }
    finally {
        $script:BridgeAutoStartAttempted = $false
        [void](Write-DaemonJsonFileBestEffort -Path $TimeoutBridgeCleanupFile -Payload $cleanup -Depth 8)
    }

    $remainingCount = @($cleanup.remainingProcessIds).Count
    $stoppedCount = @($cleanup.stoppedProcessIds).Count
    Write-Log "  -> GM bridge timeout cleanup: status=$($cleanup.status) ok=$($cleanup.ok) stopped=$stoppedCount remaining=$remainingCount" -Color DarkGray
    return [pscustomobject]$cleanup
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
        $refreshedStatus = Refresh-GmBridgeReadiness
        if ($null -ne $refreshedStatus -and $refreshedStatus.ready) {
            $status = $refreshedStatus
        }
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

function Test-DaemonProcessAlive {
    param([object]$ProcessId)

    if ($null -eq $ProcessId) {
        return $false
    }

    $pidValue = 0
    if (-not [int]::TryParse([string]$ProcessId, [ref]$pidValue)) {
        return $false
    }

    try {
        $null = Get-Process -Id $pidValue -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Read-DaemonStatus {
    if (!(Test-Path $DaemonStatusFile)) {
        return $null
    }

    try {
        return Get-Content -Path $DaemonStatusFile -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function New-DaemonErrorPayload {
    param(
        [object]$ErrorRecord,
        [string]$Phase = "unknown"
    )

    $exception = if ($null -ne $ErrorRecord.Exception) { $ErrorRecord.Exception } else { $null }
    $invocation = if ($null -ne $ErrorRecord.InvocationInfo) { $ErrorRecord.InvocationInfo } else { $null }

    $payload = [ordered]@{
        phase = $Phase
        occurredAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        message = if ($null -ne $exception) { [string]$exception.Message } else { [string]$ErrorRecord }
        type = if ($null -ne $exception) { $exception.GetType().FullName } else { "" }
        category = if ($null -ne $ErrorRecord.CategoryInfo) { [string]$ErrorRecord.CategoryInfo } else { "" }
        scriptStackTrace = if ($null -ne $ErrorRecord.ScriptStackTrace) { [string]$ErrorRecord.ScriptStackTrace } else { "" }
    }

    if ($null -ne $invocation) {
        $payload.invocation = [ordered]@{
            scriptName = [string]$invocation.ScriptName
            scriptLineNumber = $invocation.ScriptLineNumber
            offsetInLine = $invocation.OffsetInLine
            line = [string]$invocation.Line
        }
    }

    return $payload
}

function Write-DaemonJsonFileBestEffort {
    param(
        [string]$Path,
        [object]$Payload,
        [int]$Depth = 8
    )

    $tmpPath = $null
    try {
        $directory = Split-Path $Path -Parent
        if (!(Test-Path $directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }

        $tmpName = "." + [IO.Path]::GetFileName($Path) + ".$PID.tmp"
        $tmpPath = Join-Path $directory $tmpName
        Set-Content -LiteralPath $tmpPath -Value ($Payload | ConvertTo-Json -Depth $Depth) -Encoding UTF8
        Move-Item -LiteralPath $tmpPath -Destination $Path -Force
        return $true
    }
    catch {
        try {
            if ($tmpPath -and (Test-Path $tmpPath)) {
                Remove-Item -LiteralPath $tmpPath -Force
            }
        }
        catch {
            # Best-effort cleanup only.
        }

        return $false
    }
}

function Write-DaemonFatalReport {
    param(
        [object]$ErrorRecord,
        [string]$Phase = "fatal"
    )

    $payload = [ordered]@{
        status = "failed"
        pid = $PID
        sessionPath = $GameSessionPath
        command = $script:DaemonCommandLine
        turnCount = $script:TurnCount
        errorCount = $script:ErrorCount
        error = (New-DaemonErrorPayload -ErrorRecord $ErrorRecord -Phase $Phase)
    }

    [void](Write-DaemonJsonFileBestEffort -Path $DaemonFatalErrorFile -Payload $payload -Depth 8)
}

function Write-DaemonStatus {
    param(
        [string]$Status = "running",
        [object]$FatalError = $null,
        [string]$Reason = "",
        [Nullable[int]]$CurrentTurnNumber = $null,
        [Nullable[int]]$TurnElapsedSeconds = $null
    )

    $payload = [ordered]@{
        status = $Status
        pid = $PID
        sessionPath = $GameSessionPath
        startedAtUtc = $script:StartTime.ToUniversalTime().ToString("o")
        heartbeatAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        command = $script:DaemonCommandLine
        turnTimeoutSeconds = $TurnTimeout
        turnCount = $script:TurnCount
        errorCount = $script:ErrorCount
    }

    if (-not [string]::IsNullOrWhiteSpace($Reason)) {
        $payload.reason = $Reason
    }

    if ($null -ne $CurrentTurnNumber) {
        $payload.currentTurnNumber = $CurrentTurnNumber
    }

    if ($null -ne $TurnElapsedSeconds) {
        $payload.turnElapsedSeconds = $TurnElapsedSeconds
    }

    if ($null -ne $script:DaemonLastLoopError) {
        $payload.lastLoopError = $script:DaemonLastLoopError
    }

    if ($null -ne $FatalError) {
        $payload.fatalError = New-DaemonErrorPayload -ErrorRecord $FatalError -Phase "fatal"
    }

    [void](Write-DaemonJsonFileBestEffort -Path $DaemonStatusFile -Payload $payload -Depth 8)
}

function Assert-SingleDaemonInstance {
    $status = Read-DaemonStatus
    if ($null -ne $status -and $null -ne $status.pid) {
        $pidValue = 0
        if ([int]::TryParse([string]$status.pid, [ref]$pidValue) -and $pidValue -ne $PID -and (Test-DaemonProcessAlive -ProcessId $pidValue)) {
            throw "GM daemon already running for this game_session (pid=$pidValue, sessionPath=$($status.sessionPath), startedAtUtc=$($status.startedAtUtc), heartbeatAtUtc=$($status.heartbeatAtUtc)). Stop that process before starting another daemon for this session. Status file: $DaemonStatusFile"
        }
    }

    Write-DaemonStatus -Status "running"
}

function Update-DaemonHeartbeat {
    $nowUtc = (Get-Date).ToUniversalTime()
    if (!(Test-Path $DaemonStatusFile)) {
        $script:DaemonLastHeartbeatUtc = $nowUtc
        Write-DaemonStatus -Status "running" -Reason "status_file_missing"
        return
    }

    if (($nowUtc - $script:DaemonLastHeartbeatUtc).TotalSeconds -lt 5) {
        return
    }

    $script:DaemonLastHeartbeatUtc = $nowUtc
    Write-DaemonStatus -Status "running"
}

function Update-DaemonProcessingHeartbeat {
    param(
        [int]$TurnNumber,
        [int]$ElapsedSeconds
    )

    $nowUtc = (Get-Date).ToUniversalTime()
    if ((Test-Path $DaemonStatusFile) -and ($nowUtc - $script:DaemonLastHeartbeatUtc).TotalSeconds -lt 5) {
        return
    }

    $script:DaemonLastHeartbeatUtc = $nowUtc
    Write-DaemonStatus -Status "processing" -Reason "turn_processing_waiting" -CurrentTurnNumber $TurnNumber -TurnElapsedSeconds $ElapsedSeconds
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

function New-GmTrajectoryRealmResolution {
    param(
        [string]$Realm,
        [string]$Source,
        [string]$RawValue = "",
        [string]$Reason = ""
    )

    return [ordered]@{
        realm = $Realm
        source = $Source
        rawValue = $RawValue
        reason = $Reason
    }
}

function Get-GmTrajectorySoulStateRealmCandidate {
    $soulStatePath = Join-Path $GameSessionPath "game_state\meta\soul_state.json"
    if (!(Test-Path $soulStatePath)) {
        return $null
    }

    try {
        $soulState = Get-Content -Path $soulStatePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -ne $soulState.currentRealm -and -not [string]::IsNullOrWhiteSpace([string]$soulState.currentRealm)) {
            return [pscustomobject]@{
                Source = "soul_state.currentRealm"
                RawValue = [string]$soulState.currentRealm
            }
        }
    }
    catch {
        return [pscustomobject]@{
            Source = "soul_state.currentRealm"
            RawValue = ""
            ReadError = $true
        }
    }

    return $null
}

function Get-GmTrajectoryRealmResolution {
    param([object]$RequestObject)

    $candidates = @()
    if ($null -ne $RequestObject.currentRealm -and -not [string]::IsNullOrWhiteSpace([string]$RequestObject.currentRealm)) {
        $candidates += [pscustomobject]@{
            Source = "turn_request.currentRealm"
            RawValue = [string]$RequestObject.currentRealm
        }
    }

    if ($null -ne $RequestObject.progressionControl -and
        $null -ne $RequestObject.progressionControl.currentRealm -and
        -not [string]::IsNullOrWhiteSpace([string]$RequestObject.progressionControl.currentRealm)) {
        $candidates += [pscustomobject]@{
            Source = "turn_request.progressionControl.currentRealm"
            RawValue = [string]$RequestObject.progressionControl.currentRealm
        }
    }

    $soulCandidate = Get-GmTrajectorySoulStateRealmCandidate
    if ($null -ne $soulCandidate) {
        $candidates += $soulCandidate
    }

    foreach ($candidate in @($candidates)) {
        if ($candidate.ReadError) {
            continue
        }

        $realm = ConvertTo-GmTrajectoryRealm ([string]$candidate.RawValue)
        if ($realm -ne "Unknown") {
            return New-GmTrajectoryRealmResolution `
                -Realm $realm `
                -Source ([string]$candidate.Source) `
                -RawValue ([string]$candidate.RawValue)
        }
    }

    $readErrorCandidate = @($candidates | Where-Object { $_.ReadError } | Select-Object -First 1)
    if ($readErrorCandidate.Count -gt 0) {
        return New-GmTrajectoryRealmResolution `
            -Realm "Unknown" `
            -Source ([string]$readErrorCandidate[0].Source) `
            -Reason "unreadable_current_realm"
    }

    $unrecognizedCandidate = @($candidates | Select-Object -First 1)
    if ($unrecognizedCandidate.Count -gt 0) {
        return New-GmTrajectoryRealmResolution `
            -Realm "Unknown" `
            -Source ([string]$unrecognizedCandidate[0].Source) `
            -RawValue ([string]$unrecognizedCandidate[0].RawValue) `
            -Reason "unrecognized_current_realm"
    }

    return New-GmTrajectoryRealmResolution `
        -Realm "Unknown" `
        -Source "unavailable" `
        -Reason "missing_current_realm"
}

function Get-GmTrajectoryRealm {
    param([object]$RequestObject)

    $resolution = Get-GmTrajectoryRealmResolution -RequestObject $RequestObject
    return [string]$resolution.realm
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
            elseif ($null -ne $packet.kind -and -not [string]::IsNullOrWhiteSpace([string]$packet.kind)) {
                $refs += [string]$packet.kind
            }
        }
    }

    return @($refs | Select-Object -Unique)
}

function ConvertTo-GmTrajectoryCompactString {
    param(
        [object]$Value,
        [int]$MaxLength = 240
    )

    if ($null -eq $Value) {
        return ""
    }

    $text = ([string]$Value).Trim()
    if ($text.Length -le $MaxLength) {
        return $text
    }

    return $text.Substring(0, [Math]::Max(0, $MaxLength - 3)) + "..."
}

function Get-GmTrajectoryCompactStringProperty {
    param(
        [object]$Object,
        [string[]]$Names,
        [int]$MaxLength = 240
    )

    if ($null -eq $Object -or $null -eq $Object.PSObject) {
        return ""
    }

    foreach ($name in $Names) {
        $property = $Object.PSObject.Properties[$name]
        if ($null -ne $property -and $null -ne $property.Value -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            return ConvertTo-GmTrajectoryCompactString -Value $property.Value -MaxLength $MaxLength
        }
    }

    return ""
}

function Get-GmTrajectoryCompactStringArrayProperty {
    param(
        [object]$Object,
        [string[]]$Names,
        [int]$MaxItems = 6,
        [int]$MaxLength = 180
    )

    if ($null -eq $Object -or $null -eq $Object.PSObject) {
        return @()
    }

    $values = @()
    foreach ($name in $Names) {
        $property = $Object.PSObject.Properties[$name]
        if ($null -eq $property -or $null -eq $property.Value) {
            continue
        }

        foreach ($value in @($property.Value)) {
            if ($null -ne $value -and -not [string]::IsNullOrWhiteSpace([string]$value)) {
                $values += (ConvertTo-GmTrajectoryCompactString -Value $value -MaxLength $MaxLength)
            }
        }

        if ($values.Count -gt 0) {
            break
        }
    }

    return @($values | Select-Object -First $MaxItems)
}

function Get-GmTrajectoryValidationDiagnostics {
    param([object]$RequestObject)

    $diagnostics = @()
    if ($null -eq $RequestObject -or $null -eq $RequestObject.errors) {
        return @()
    }

    foreach ($err in @($RequestObject.errors | Select-Object -First 8)) {
        if ($null -eq $err) { continue }

        $diagnostics += [ordered]@{
            code = Get-GmTrajectoryCompactStringProperty -Object $err -Names @("code") -MaxLength 120
            category = Get-GmTrajectoryCompactStringProperty -Object $err -Names @("category") -MaxLength 120
            section = Get-GmTrajectoryCompactStringProperty -Object $err -Names @("section") -MaxLength 120
            path = Get-GmTrajectoryCompactStringProperty -Object $err -Names @("path", "file", "filePath", "relativePath") -MaxLength 180
            jsonPath = Get-GmTrajectoryCompactStringProperty -Object $err -Names @("jsonPath", "jsonPointer", "fieldPath", "propertyPath") -MaxLength 180
            message = Get-GmTrajectoryCompactStringProperty -Object $err -Names @("message") -MaxLength 240
            expected = Get-GmTrajectoryCompactStringProperty -Object $err -Names @("expected") -MaxLength 180
            actual = Get-GmTrajectoryCompactStringProperty -Object $err -Names @("actual") -MaxLength 180
            repairHint = Get-GmTrajectoryCompactStringProperty -Object $err -Names @("repairHint") -MaxLength 240
        }
    }

    return @($diagnostics)
}

function Get-GmTrajectoryRepairPacketDiagnostics {
    param([object]$RequestObject)

    $diagnostics = @()
    if ($null -eq $RequestObject -or $null -eq $RequestObject.harnessRepairPackets) {
        return @()
    }

    foreach ($packet in @($RequestObject.harnessRepairPackets | Select-Object -First 8)) {
        if ($null -eq $packet) { continue }

        $diagnostics += [ordered]@{
            packetId = Get-GmTrajectoryCompactStringProperty -Object $packet -Names @("packetId", "id") -MaxLength 120
            kind = Get-GmTrajectoryCompactStringProperty -Object $packet -Names @("kind") -MaxLength 120
            targetFiles = @(Get-GmTrajectoryCompactStringArrayProperty -Object $packet -Names @("targetFiles", "targetFile", "files") -MaxItems 6 -MaxLength 180)
            targetFields = @(Get-GmTrajectoryCompactStringArrayProperty -Object $packet -Names @("targetFields", "targetField", "fields", "jsonPaths") -MaxItems 6 -MaxLength 180)
        }
    }

    return @($diagnostics)
}

function Get-GmObjectPropertyValue {
    param(
        [object]$Object,
        [string]$Name
    )

    if ($null -eq $Object -or $null -eq $Object.PSObject) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Test-GmRequestCorrelation {
    param(
        [object]$ExpectedRequest,
        [object]$CandidateRequest
    )

    $expectedSessionId = Get-GmObjectPropertyValue -Object $ExpectedRequest -Name "sessionId"
    $expectedRequestId = Get-GmObjectPropertyValue -Object $ExpectedRequest -Name "requestId"
    $expectedTurnNumber = Get-GmObjectPropertyValue -Object $ExpectedRequest -Name "turnNumber"
    $candidateSessionId = Get-GmObjectPropertyValue -Object $CandidateRequest -Name "sessionId"
    $candidateRequestId = Get-GmObjectPropertyValue -Object $CandidateRequest -Name "requestId"
    $candidateTurnNumber = Get-GmObjectPropertyValue -Object $CandidateRequest -Name "turnNumber"

    if ([string]::IsNullOrWhiteSpace([string]$expectedSessionId) -or
        [string]::IsNullOrWhiteSpace([string]$expectedRequestId) -or
        [string]::IsNullOrWhiteSpace([string]$candidateSessionId) -or
        [string]::IsNullOrWhiteSpace([string]$candidateRequestId)) {
        return $false
    }

    $expectedTurn = -1
    $candidateTurn = -2
    if (-not [int]::TryParse([string]$expectedTurnNumber, [ref]$expectedTurn) -or
        -not [int]::TryParse([string]$candidateTurnNumber, [ref]$candidateTurn)) {
        return $false
    }

    return [string]::Equals([string]$expectedSessionId, [string]$candidateSessionId, [System.StringComparison]::Ordinal) -and
        [string]::Equals([string]$expectedRequestId, [string]$candidateRequestId, [System.StringComparison]::Ordinal) -and
        $expectedTurn -eq $candidateTurn
}

function Read-CorrelatedValidationRepairRequest {
    param([object]$TurnRequest)

    if (!(Test-Path $RepairRequestFile)) {
        return $null
    }

    try {
        $repair = Get-Content -Path $RepairRequestFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if (Test-GmRequestCorrelation -ExpectedRequest $TurnRequest -CandidateRequest $repair) {
            return $repair
        }
    }
    catch {
        return $null
    }

    return $null
}

function Wait-CorrelatedValidationRepairRequest {
    param(
        [object]$TurnRequest,
        [int]$GraceMilliseconds = 0
    )

    $repair = Read-CorrelatedValidationRepairRequest -TurnRequest $TurnRequest
    if ($null -ne $repair -or $GraceMilliseconds -le 0) {
        return $repair
    }

    $elapsed = 0
    while ($elapsed -lt $GraceMilliseconds) {
        $sleep = [Math]::Min($script:CorrelatedRepairPollMilliseconds, $GraceMilliseconds - $elapsed)
        Start-Sleep -Milliseconds $sleep
        $elapsed += $sleep

        $repair = Read-CorrelatedValidationRepairRequest -TurnRequest $TurnRequest
        if ($null -ne $repair) {
            return $repair
        }
    }

    return $null
}

function Get-GmValidationRepairAttempt {
    param([object]$RepairRequest)

    $attemptValue = Get-GmObjectPropertyValue -Object $RepairRequest -Name "revalidationAttempt"
    $attempt = 0
    if ($null -ne $attemptValue -and [int]::TryParse([string]$attemptValue, [ref]$attempt) -and $attempt -gt 0) {
        return $attempt
    }

    return 1
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

function Get-GmTrajectoryDetailValues {
    param(
        [object]$Details,
        [string]$Key
    )

    if ($null -eq $Details -or $null -eq $Details.PSObject -or $null -eq $Details.PSObject.Properties[$Key]) {
        return @()
    }

    $raw = $Details.PSObject.Properties[$Key].Value
    $values = @()
    foreach ($value in @($raw)) {
        if ($null -ne $value -and -not [string]::IsNullOrWhiteSpace([string]$value)) {
            $values += [string]$value
        }
    }

    return @($values)
}

function Get-GmTrajectoryFirstDetailValue {
    param(
        [object]$Details,
        [string]$Key
    )

    $values = @(Get-GmTrajectoryDetailValues -Details $Details -Key $Key)
    if ($values.Count -eq 0) {
        return ""
    }

    return [string]$values[0]
}

function ConvertTo-GmTrajectoryWorkerTaskType {
    param([object]$AuditEvent)

    $fromDetails = Get-GmTrajectoryFirstDetailValue -Details $AuditEvent.details -Key "taskType"
    if (-not [string]::IsNullOrWhiteSpace($fromDetails)) {
        return $fromDetails
    }

    $summary = if ($AuditEvent.summary) { ([string]$AuditEvent.summary).ToLowerInvariant() } else { "" }
    $taskId = if ($AuditEvent.taskId) { ([string]$AuditEvent.taskId).ToLowerInvariant() } else { "" }
    $joined = "$summary $taskId"
    if ($joined.Contains("validation") -and $joined.Contains("repair")) {
        return "validation-repair"
    }
    if ($joined.Contains("narrative")) {
        return "narrative-draft"
    }
    if ($joined.Contains("analysis")) {
        return "analysis"
    }

    return ""
}

function ConvertTo-GmTrajectoryWorkerEvent {
    param([object]$AuditEvent)

    $taskType = ConvertTo-GmTrajectoryWorkerTaskType -AuditEvent $AuditEvent
    $allowedPaths = @(Get-GmTrajectoryDetailValues -Details $AuditEvent.details -Key "allowedProposalPaths")
    $changedFiles = @(Get-GmTrajectoryDetailValues -Details $AuditEvent.details -Key "changedFiles")
    $appliedFiles = @(Get-GmTrajectoryDetailValues -Details $AuditEvent.details -Key "appliedFiles")
    $rejectionReasons = @(Get-GmTrajectoryDetailValues -Details $AuditEvent.details -Key "rejectionReasons")
    $proposalOnly = $taskType -in @(
        "narrative-draft",
        "analysis",
        "lore-consistency",
        "npc-analysis",
        "qte-content",
        "inventory-content",
        "skill-content",
        "npc-content",
        "guardian-abode-content",
        "social-dialogue-content",
        "faction-content",
        "location-content",
        "quest-content",
        "book-document-content",
        "economy-crafting-content",
        "world-state-content",
        "encounter-content"
    )
    if ($taskType -eq "validation-repair") {
        $proposalOnly = $false
    }

    $summary = if ($AuditEvent.summary) { ([string]$AuditEvent.summary).Trim() } else { "" }
    if ($summary.Length -gt 180) {
        $summary = $summary.Substring(0, 177) + "..."
    }

    return [ordered]@{
        eventId = if ($AuditEvent.eventId) { [string]$AuditEvent.eventId } else { "" }
        eventType = if ($AuditEvent.eventType) { [string]$AuditEvent.eventType } else { "" }
        workerId = if ($AuditEvent.workerId) { [string]$AuditEvent.workerId } else { "" }
        taskId = if ($AuditEvent.taskId) { [string]$AuditEvent.taskId } else { "" }
        proposalId = if ($AuditEvent.proposalId) { [string]$AuditEvent.proposalId } else { "" }
        timestampUtc = if ($AuditEvent.timestampUtc) { [string]$AuditEvent.timestampUtc } else { "" }
        summary = $summary
        taskType = $taskType
        proposalOnly = $proposalOnly
        allowedProposalPathCount = $allowedPaths.Count
        changedFileCount = $changedFiles.Count
        appliedFileCount = $appliedFiles.Count
        rejectionCount = $rejectionReasons.Count
    }
}

function Get-GmTrajectoryWorkerEvents {
    param([datetime]$SinceUtc)

    $auditPath = Join-Path $ControlDir "gm_worker_audit.jsonl"
    if (!(Test-Path $auditPath)) {
        return @()
    }

    $events = @()
    foreach ($line in @(Get-Content -Path $auditPath -Encoding UTF8 -ErrorAction SilentlyContinue)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try {
            $auditEvent = $line | ConvertFrom-Json
            $timestampUtc = [datetime]::MinValue
            if ($auditEvent.timestampUtc) {
                try {
                    $timestampUtc = ([datetimeoffset]::Parse([string]$auditEvent.timestampUtc)).UtcDateTime
                }
                catch { }
            }

            if ($SinceUtc -ne [datetime]::MinValue -and $timestampUtc -ne [datetime]::MinValue -and $timestampUtc -lt $SinceUtc) {
                continue
            }

            $events += (ConvertTo-GmTrajectoryWorkerEvent -AuditEvent $auditEvent)
        }
        catch {
            $events += [ordered]@{
                eventId = ""
                eventType = "unreadable"
                workerId = ""
                taskId = ""
                proposalId = ""
                timestampUtc = ""
                summary = "Unreadable gm_worker_audit.jsonl entry."
                taskType = ""
                proposalOnly = $true
                allowedProposalPathCount = 0
                changedFileCount = 0
                appliedFileCount = 0
                rejectionCount = 0
            }
        }
    }

    return @($events | Select-Object -Last 20)
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
        [object[]]$ValidationDiagnostics = @(),
        [object[]]$RepairPacketDiagnostics = @(),
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
        $workerEvents = @(Get-GmTrajectoryWorkerEvents -SinceUtc $StartedAtUtc)
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
        $realmResolution = Get-GmTrajectoryRealmResolution -RequestObject $RequestObject
        $record = [ordered]@{
            recordId = "gmtraj_" + [guid]::NewGuid().ToString("N")
            kind = $Kind
            sessionId = if ($null -ne $RequestObject.sessionId) { [string]$RequestObject.sessionId } else { "" }
            turnId = $requestId
            requestId = $requestId
            turnNumber = if ($null -ne $RequestObject.turnNumber) { [int]$RequestObject.turnNumber } else { -1 }
            realm = [string]$realmResolution.realm
            realmResolution = $realmResolution
            mode = $Mode
            actionSummary = ConvertTo-GmTrajectoryActionSummary -RequestObject $RequestObject
            contextPackPath = "game_state/control/gm_context_pack"
            templateVersions = [ordered]@{
                turnOutput = "v1"
                validationRepair = "v1"
                progressionReport = "v1"
                actorReasoning = "v1"
                tempoAdvantage = "v1"
                mortalExperience = "v1"
                mortalCombat = "v1"
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
                diagnostics = @($ValidationDiagnostics)
                repairPacketDiagnostics = @($RepairPacketDiagnostics)
            }
            repair = [ordered]@{
                attempts = $RepairAttempts
                status = $RepairStatus
            }
            workerEvents = @($workerEvents)
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
        Update-GmLiveTestNoteRecordLinks -RequestId $requestId -RecordId ([string]$record.recordId)
    }
    catch {
        Write-Log "  Failed to write GM trajectory ledger: $_" -Level "WARN" -Color Yellow
    }
}

function Update-GmLiveTestNoteRecordLinks {
    param(
        [string]$RequestId,
        [string]$RecordId
    )

    if ([string]::IsNullOrWhiteSpace($RequestId) -or [string]::IsNullOrWhiteSpace($RecordId)) {
        return
    }

    $notesPath = Join-Path $ControlDir "gm_live_test_notes.jsonl"
    if (!(Test-Path $notesPath)) {
        return
    }

    try {
        $changed = $false
        $updatedLines = @()
        foreach ($line in @(Get-Content -Path $notesPath -Encoding UTF8 -ErrorAction SilentlyContinue)) {
            if ([string]::IsNullOrWhiteSpace($line)) {
                $updatedLines += $line
                continue
            }

            try {
                $note = $line | ConvertFrom-Json
                $noteRequestId = if ($note.requestId) { [string]$note.requestId } else { "" }
                $noteRecordId = if ($note.recordId) { [string]$note.recordId } else { "" }
                $needsBackfill = [string]::Equals($noteRequestId, $RequestId, [System.StringComparison]::OrdinalIgnoreCase) -and
                    ([string]::IsNullOrWhiteSpace($noteRecordId) -or
                     [string]::Equals($noteRecordId, "unknown", [System.StringComparison]::OrdinalIgnoreCase) -or
                     [string]::Equals($noteRecordId, "gmtraj_unknown", [System.StringComparison]::OrdinalIgnoreCase))

                if ($needsBackfill) {
                    $note.recordId = $RecordId
                    $line = $note | ConvertTo-Json -Depth 8 -Compress
                    $changed = $true
                }
            }
            catch {
                # Preserve malformed manual notes; validation/audit can report them separately.
            }

            $updatedLines += $line
        }

        if ($changed) {
            Set-Content -Path $notesPath -Value $updatedLines -Encoding UTF8
        }
    }
    catch {
        Write-Log "  Failed to backfill GM live-test note record links: $_" -Level "WARN" -Color Yellow
    }
}

function Get-TurnRequestKey {
    param([psobject]$TurnRequest)

    $sessionId = [string]$TurnRequest.sessionId
    $requestId = [string]$TurnRequest.requestId
    $turnNumber = [int]$TurnRequest.turnNumber
    return "$sessionId|$requestId|$turnNumber"
}

function Get-Sha256HexFromBytes {
    param([byte[]]$Bytes)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha.ComputeHash($Bytes)
        return [System.BitConverter]::ToString($hashBytes).Replace("-", "")
    }
    finally {
        $sha.Dispose()
    }
}

function Get-Sha256HexFromText {
    param([string]$Text)

    return Get-Sha256HexFromBytes -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Text))
}

function Get-JsonPropertyValue {
    param(
        [object]$Object,
        [string[]]$Names
    )

    if ($null -eq $Object) {
        return $null
    }

    foreach ($name in $Names) {
        foreach ($property in @($Object.PSObject.Properties)) {
            if ([string]::Equals($property.Name, $name, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $property.Value
            }
        }
    }

    return $null
}

function Get-JsonStringValue {
    param(
        [object]$Object,
        [string[]]$Names
    )

    $value = Get-JsonPropertyValue -Object $Object -Names $Names
    if ($null -eq $value) {
        return ""
    }

    return ([string]$value).Trim()
}

function Get-JsonIntValue {
    param(
        [object]$Object,
        [string[]]$Names
    )

    $value = Get-JsonPropertyValue -Object $Object -Names $Names
    if ($null -eq $value) {
        return 0
    }

    return [int]$value
}

function Get-JsonObjectProperties {
    param([object]$Object)

    if ($null -eq $Object) {
        return @()
    }

    return @($Object.PSObject.Properties | Where-Object { $null -ne $_.Value })
}

function Test-JsonObjectPropertiesEqual {
    param(
        [object]$Expected,
        [object]$Actual,
        [bool]$CompareValuesIgnoreCase = $true
    )

    $expectedProperties = @(Get-JsonObjectProperties -Object $Expected)
    $actualProperties = @(Get-JsonObjectProperties -Object $Actual)
    if ($expectedProperties.Count -ne $actualProperties.Count) {
        return $false
    }

    foreach ($expectedProperty in $expectedProperties) {
        $actualValue = Get-JsonPropertyValue -Object $Actual -Names @($expectedProperty.Name)
        if ($null -eq $actualValue) {
            return $false
        }

        $comparison = if ($CompareValuesIgnoreCase) {
            [System.StringComparison]::OrdinalIgnoreCase
        }
        else {
            [System.StringComparison]::Ordinal
        }

        if (-not [string]::Equals([string]$expectedProperty.Value, [string]$actualValue, $comparison)) {
            return $false
        }
    }

    return $true
}

function Test-JsonStringListEqual {
    param(
        [object]$Expected,
        [object]$Actual
    )

    $expectedItems = @($Expected | ForEach-Object { ([string]$_).Replace("\", "/").Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)
    $actualItems = @($Actual | ForEach-Object { ([string]$_).Replace("\", "/").Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)
    if ($expectedItems.Count -ne $actualItems.Count) {
        return $false
    }

    for ($index = 0; $index -lt $expectedItems.Count; $index++) {
        if (-not [string]::Equals($expectedItems[$index], $actualItems[$index], [System.StringComparison]::OrdinalIgnoreCase)) {
            return $false
        }
    }

    return $true
}

function Test-PendingTurnSnapshotRollbackBackupHashes {
    param(
        [object]$Payload
    )

    $rollbackBackups = Get-JsonPropertyValue -Object $Payload -Names @("rollbackBackups")
    $rollbackBackupHashes = Get-JsonPropertyValue -Object $Payload -Names @("rollbackBackupHashes")
    $hashProperties = @(Get-JsonObjectProperties -Object $rollbackBackupHashes)
    if ($hashProperties.Count -eq 0) {
        return $true
    }

    foreach ($hashProperty in $hashProperties) {
        $backupRelativePath = Get-JsonStringValue -Object $rollbackBackups -Names @($hashProperty.Name)
        if ([string]::IsNullOrWhiteSpace($backupRelativePath)) {
            return $false
        }

        $backupPath = Join-Path $GameSessionPath ($backupRelativePath.Replace("/", "\"))
        if (!(Test-Path -LiteralPath $backupPath)) {
            return $false
        }

        $content = Get-Content -LiteralPath $backupPath -Raw -Encoding UTF8
        $actualHash = Get-Sha256HexFromText -Text $content
        if (-not [string]::Equals($actualHash, [string]$hashProperty.Value, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $false
        }
    }

    return $true
}

function Test-PendingTurnSnapshotAuthorityEnvelope {
    param(
        [psobject]$Manifest,
        [psobject]$TurnRequest
    )

    try {
        $authority = Get-Content -Path $PendingTurnSnapshotAuthorityFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ([int](Get-JsonIntValue -Object $authority -Names @("formatVersion")) -ne 2) {
            Write-Log "  Pending turn snapshot authority has unsupported formatVersion." -Level "WARN" -Color Yellow
            return $false
        }

        if (-not [string]::Equals(
                (Get-JsonStringValue -Object $authority -Names @("integrityAlgorithm")),
                "SHA256-PAYLOAD-JSON",
                [System.StringComparison]::Ordinal)) {
            Write-Log "  Pending turn snapshot authority has unsupported integrityAlgorithm." -Level "WARN" -Color Yellow
            return $false
        }

        $payloadBase64 = Get-JsonStringValue -Object $authority -Names @("payloadJsonBase64")
        $expectedPayloadHash = Get-JsonStringValue -Object $authority -Names @("payloadSha256")
        if ([string]::IsNullOrWhiteSpace($payloadBase64) -or [string]::IsNullOrWhiteSpace($expectedPayloadHash)) {
            Write-Log "  Pending turn snapshot authority is missing payloadJsonBase64/payloadSha256." -Level "WARN" -Color Yellow
            return $false
        }

        $payloadBytes = [System.Convert]::FromBase64String($payloadBase64)
        $actualPayloadHash = Get-Sha256HexFromBytes -Bytes $payloadBytes
        if (-not [string]::Equals($actualPayloadHash, $expectedPayloadHash, [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Log "  Pending turn snapshot authority payloadSha256 mismatch." -Level "WARN" -Color Yellow
            return $false
        }

        $payloadJson = [System.Text.Encoding]::UTF8.GetString($payloadBytes)
        $payload = $payloadJson | ConvertFrom-Json
        $requestSessionId = [string]$TurnRequest.sessionId
        $requestId = [string]$TurnRequest.requestId
        $turnNumber = [int]$TurnRequest.turnNumber
        $manifestHash = Get-JsonStringValue -Object $Manifest -Names @("manifestPayloadHash")

        if (-not [string]::Equals((Get-JsonStringValue -Object $payload -Names @("sessionId")), $requestSessionId, [System.StringComparison]::OrdinalIgnoreCase) -or
            -not [string]::Equals((Get-JsonStringValue -Object $payload -Names @("requestId")), $requestId, [System.StringComparison]::OrdinalIgnoreCase) -or
            (Get-JsonIntValue -Object $payload -Names @("turnNumber")) -ne $turnNumber -or
            -not [string]::Equals((Get-JsonStringValue -Object $payload -Names @("manifestPayloadHash")), $manifestHash, [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Log "  Pending turn snapshot authority does not match current turn metadata." -Level "WARN" -Color Yellow
            return $false
        }

        if (-not (Test-JsonObjectPropertiesEqual -Expected (Get-JsonPropertyValue -Object $Manifest -Names @("files")) -Actual (Get-JsonPropertyValue -Object $payload -Names @("files")) -CompareValuesIgnoreCase $true) -or
            -not (Test-JsonObjectPropertiesEqual -Expected (Get-JsonPropertyValue -Object $Manifest -Names @("snapshotFileHashes")) -Actual (Get-JsonPropertyValue -Object $payload -Names @("snapshotFileHashes")) -CompareValuesIgnoreCase $true) -or
            -not (Test-JsonObjectPropertiesEqual -Expected (Get-JsonPropertyValue -Object $Manifest -Names @("clientOwnedValidationHashes")) -Actual (Get-JsonPropertyValue -Object $payload -Names @("clientOwnedValidationHashes")) -CompareValuesIgnoreCase $true) -or
            -not (Test-JsonObjectPropertiesEqual -Expected (Get-JsonPropertyValue -Object $Manifest -Names @("rollbackBackups")) -Actual (Get-JsonPropertyValue -Object $payload -Names @("rollbackBackups")) -CompareValuesIgnoreCase $true) -or
            -not (Test-JsonStringListEqual -Expected (Get-JsonPropertyValue -Object $Manifest -Names @("rollbackBaselineFiles")) -Actual (Get-JsonPropertyValue -Object $payload -Names @("rollbackBaselineFiles"))) -or
            -not [string]::Equals((Get-JsonStringValue -Object $Manifest -Names @("sourceLabel")), (Get-JsonStringValue -Object $payload -Names @("sourceLabel")), [System.StringComparison]::Ordinal)) {
            Write-Log "  Pending turn snapshot authority payload does not match manifest." -Level "WARN" -Color Yellow
            return $false
        }

        if (-not (Test-PendingTurnSnapshotRollbackBackupHashes -Payload $payload)) {
            Write-Log "  Pending turn snapshot authority rollback backup hashes are not usable." -Level "WARN" -Color Yellow
            return $false
        }

        return $true
    }
    catch {
        Write-Log "  Pending turn snapshot authority is unreadable: $_" -Level "WARN" -Color Yellow
        return $false
    }
}

function Get-MissingHarnessToolFromTerminalError {
    param([object]$TerminalSignal)

    if ($null -eq $TerminalSignal -or
        $TerminalSignal.Kind -ne "error" -or
        $null -eq $TerminalSignal.Signal) {
        return $null
    }

    $signal = $TerminalSignal.Signal
    $harnessSource = Get-JsonStringValue -Object $signal -Names @("harnessSource")
    if (-not [string]::IsNullOrWhiteSpace($harnessSource)) {
        return $harnessSource
    }

    $errorMessage = Get-JsonStringValue -Object $signal -Names @("error")
    if ($errorMessage.IndexOf("Pending turn snapshot authority", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        return "pending_turn_snapshot_authority_recovery_gap"
    }

    return $null
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
            $manifestTurnNumber -eq $turnNumber -and
            (Test-PendingTurnSnapshotAuthorityEnvelope -Manifest $manifest -TurnRequest $TurnRequest)
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

function Load-ObservedTerminalRequestKeys {
    if (!(Test-Path $ObservedTerminalRequestKeysFile)) {
        return
    }

    try {
        $root = Get-Content -Path $ObservedTerminalRequestKeysFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $keys = @()
        if ($root -is [System.Array]) {
            $keys = @($root)
        }
        elseif ($null -ne $root.keys) {
            $keys = @($root.keys)
        }

        foreach ($key in $keys) {
            $normalized = [string]$key
            if (![string]::IsNullOrWhiteSpace($normalized)) {
                [void]$script:ObservedTerminalRequestKeys.Add($normalized)
            }
        }
    }
    catch {
        Write-Log "  Failed to load observed terminal request keys: $_" -Level "WARN" -Color Yellow
    }
}

function Save-ObservedTerminalRequestKeys {
    try {
        $controlDir = Split-Path $ObservedTerminalRequestKeysFile -Parent
        if (!(Test-Path $controlDir)) {
            New-Item -ItemType Directory -Path $controlDir -Force | Out-Null
        }

        $payload = [ordered]@{
            schemaVersion = 1
            updatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
            keys = @($script:ObservedTerminalRequestKeys | Sort-Object)
        }

        Set-Content -Path $ObservedTerminalRequestKeysFile -Value ($payload | ConvertTo-Json -Depth 4) -Encoding UTF8
    }
    catch {
        Write-Log "  Failed to save observed terminal request keys: $_" -Level "WARN" -Color Yellow
    }
}

function Add-ObservedTerminalRequestKey {
    param([string]$Key)

    if (![string]::IsNullOrWhiteSpace($Key)) {
        if ($script:ObservedTerminalRequestKeys.Add($Key)) {
            Save-ObservedTerminalRequestKeys
        }
    }
}

Load-ObservedTerminalRequestKeys

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
        [switch]$ReturnDetails,
        [int]$MaxWaitSeconds = 0
    )

    $attempts = 0
    $busyRetries = 0
    $startedAt = Get-Date

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
            $elapsedSeconds = ((Get-Date) - $startedAt).TotalSeconds
            if ($MaxWaitSeconds -gt 0 -and $elapsedSeconds -ge $MaxWaitSeconds) {
                Write-Log "  -> GM bridge dispatch timeout after $([math]::Round($elapsedSeconds, 1))s; bridge did not accept the prompt." -Level "ERROR" -Color Red
                if ($ReturnDetails) {
                    return (New-GmDispatchDiagnostics -Status "bridge-dispatch-timeout" -Attempts $attempts -BusyRetries $busyRetries -Timeout $true)
                }
                return "bridge-dispatch-timeout"
            }

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

        $script:TurnCount++
        $turnStart = Get-Date

        $playerAction = if ($turnRequest.playerAction.Length -gt 80) {
            $turnRequest.playerAction.Substring(0, 77) + "..."
        } else { $turnRequest.playerAction }

        Write-Host ""
        Write-Log "Turn #${turnNumber}: $playerAction" -Level "TURN" -Color Green

        $null = Write-GmExperienceLessons
        $experiencePrompt = Get-GmExperiencePromptDigest
        $firstMortalBootstrapPrompt = Get-FirstMortalBootstrapPrompt -TurnRequest $turnRequest

        # Send processing command to CLI window
        $requestId = if ($turnRequest.requestId) { $turnRequest.requestId } else { "<missing-requestId>" }
        $message = $null
        if (-not [string]::IsNullOrWhiteSpace($firstMortalBootstrapPrompt)) {
            $message = Build-FirstMortalBootstrapDispatchMessage `
                -TurnRequest $turnRequest `
                -TurnNumber $turnNumber `
                -RequestId $requestId `
                -ExperiencePrompt $experiencePrompt `
                -FirstMortalBootstrapPrompt $firstMortalBootstrapPrompt
        }

        if ($null -eq $message) {
        $message = "Process turn #$turnNumber (requestId=$requestId).$($script:GmContextPackDirective)$($script:GmDocPathDirective)$($script:GmSafeProbeDirective)$($script:GmSourceFallbackDirective)$($script:GmCompactTemplateDirective)$($script:GmExperienceLessonsDirective)$($experiencePrompt)$($firstMortalBootstrapPrompt)$($script:GmLiveTestRubricDirective)$($script:GmTurnHelperDirective) Read $GameSessionPath\input\turn_request.json and follow CLI_Agent_Daemon_Specification.md phases 0-4. You MUST read '$($script:CompactTurnOutputTemplatePath)', '$($script:CompactProgressionReportTemplatePath)', '$($script:CompactActorReasoningTemplatePath)', '$($script:CompactMortalNpcTemplatePath)', '$($script:CompactMortalFactionTemplatePath)', '$($script:CompactMortalLocationTemplatePath)', '$($script:CompactMortalSkillTemplatePath)', '$($script:CompactMortalExperienceTemplatePath)', '$($script:CompactAfterlifeChronicleTemplatePath)', and '$($script:CompactTempoAdvantageTemplatePath)' before opening large copied examples. Read '$($script:TaskGuideMainPath)' for phase rules; use '$($script:ExampleMainPath)' only when compact templates do not cover a route-specific shape.$($script:AfterlifeRealmGateDirective)$($script:AfterlifeExamplesDirective)$($script:AfterlifeCombatConditionsDirective)$($script:AfterlifeSpecialArtCombatEffectDirective) $($script:WeatherContractDirective) If this turn uses any GM-side [INK_FEATHER_ACTION: TAG], you MUST also read '$($script:InkFeatherExamplePath)' and write output/ink_feather_action_result.json with exact metadata, actionTag, resolved=true, costInFeathers, resolutionType, summary, and stateEvidence. The client validates correlated metadata, valid JSON, realm restrictions, progressionControl/progression report, gm_thoughts_markdown scope/reasoning, and structured actor coverage. Relevant actors in NPC scope MUST cover any structured actor updates such as UpdateNPCs, NPCGoalUpdates, or UpdateGuardians. If a Mortal World turn creates or updates NPCs, use '$($script:CompactMortalNpcTemplatePath)' before editing game_state/npcs/npc_core.json. If a Mortal World turn creates or updates factions, ranks, branches, chronicles, projects, faction relations, or faction sidecars, use '$($script:CompactMortalFactionTemplatePath)' before editing game_state/factions/*. If a Mortal World turn creates a durable location, changes current_location, edits world_map, or moves NPCs between map locations, use '$($script:CompactMortalLocationTemplatePath)' before editing game_state/world/* or NPC location ids. If a Mortal World turn teaches, practices, unlocks, or uses a concrete player skill, use '$($script:CompactMortalSkillTemplatePath)' before writing output/state; do not leave learned skills or active-skill mastery as prose-only text. If a Mortal World turn grants XP, resolves combat rewards, or crosses a level-up threshold, use '$($script:CompactMortalExperienceTemplatePath)' before editing game_state/player/experience.json; do not leave level progress prose-only. Use preGeneratedDices1d20 from the FIRST die for normal checks; afterlife spiritual conflicts use visible d20 values through diceAudit on contested exchange/resolve; gachaBaseResult is separate and does not consume visible dice. If playerAction contains [CHAOS_SEA_DIRECT_GACHA], treat it as a neutral direct pull from the Chaos Sea, not a Guardian-mediated pull, and preserve the exact cost phrase '<N> Чернильных Перьев' or '<N> Ink Feathers' because validation extracts prepaid cost from it. Guardian-mediated gacha is limited per Guardian per return from mortal life: Hostile=0, Wary/Neutral=1, Friendly=2, Devoted/Legendary=3. Guardian-mediated rarity upgrades are limited to Abode Power rarity ceiling bonus and completed relic_forging project bonus; Guardian reputation does not improve rarity odds. Charges reset only when the Soul returns to the Chaos Sea after a new mortal life. If a Guardian has no remaining charges this return, do NOT emit UpdateGuardians.processGacha for that Guardian. Direct /gacha remains neutral and does NOT consume Guardian charges. progressionControl in the request is authoritative. If progression is processed, write game_state/control/progression_report.json with exact sessionId/requestId/turnNumber copied from the CURRENT turn_request.json plus exact bounded processed cycle counts and new last-* markers. If progressionControl.afterlifeCatchupRequired=true, process only afterlifeCatchupSummaryEventsRequired summary outcomes and do NOT simulate raw elapsed cycles one by one. TERMINAL CHECKLIST: write EXACTLY ONE terminal signal for this request; use either ready/turn_complete.json OR ready/turn_error.json, never both; copy exact sessionId/requestId/turnNumber from the CURRENT turn_request.json; never delete or rewrite input/turn_request.json; write the terminal signal as the LAST step. If you write both terminal files or wrong metadata, the client will reject the terminal phase as protocol failure and write game_state/control/terminal_protocol_failure_request.json. validation_repair_request.json is only for accepted terminal completion with invalid resulting state."

        $message += " If a Mortal World turn resolves open combat, enemy exchange, combat XP, active-skill combat mastery, or combat resource changes, you MUST read '$($script:CompactMortalCombatTemplatePath)' before writing output/state and leave /бой useful through game_state/combat/combat_log.json plus enemies/allies when relevant."
        }
        $completionPath = Join-Path $ReadyDir "turn_complete.json"
        $errorPath = Join-Path $ReadyDir "turn_error.json"
        $terminalSignal = Get-CorrelatedTerminalSignal -TurnRequest $turnRequest -CompletionPath $completionPath -ErrorPath $errorPath
        $dispatchDiagnostics = New-GmDispatchDiagnostics -Status "preexisting-terminal"
        $missingHarnessTool = $null

        if ($null -eq $terminalSignal) {
            $dispatchMaxWaitSeconds = if ($TurnTimeout -gt 0 -and $TurnTimeout -lt $script:BridgeDispatchMaxWaitSeconds) { $TurnTimeout } else { $script:BridgeDispatchMaxWaitSeconds }
            $dispatchDiagnostics = Dispatch-WithRetry -Message $message -PendingPath $RequestPath -ReturnDetails -MaxWaitSeconds $dispatchMaxWaitSeconds
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

            if ($dispatchDiagnostics.Status -eq "bridge-dispatch-timeout") {
                $script:ErrorCount++
                Write-Log "  GM bridge did not accept dispatch before the dispatch timeout; emitting daemon terminal error." -Level "ERROR" -Color Red
                $missingHarnessTool = "gm_bridge_dispatch_unavailable"
                $dispatchTerminalSignal = @{
                    sessionId = $turnRequest.sessionId
                    requestId = $turnRequest.requestId
                    turnNumber = $turnNumber
                    status = "error"
                    harnessSource = "gm_bridge_dispatch_unavailable"
                    timestamp = (Get-Date).ToUniversalTime().ToString("o")
                    error = "GM bridge did not accept dispatch before the dispatch timeout."
                }
                Set-Content -Path $errorPath -Value ($dispatchTerminalSignal | ConvertTo-Json -Depth 4) -Encoding UTF8
                Write-GmTrajectoryRecord `
                    -Kind "turn" `
                    -Mode "ordinary" `
                    -RequestObject $turnRequest `
                    -Dispatch $dispatchDiagnostics `
                    -ValidationStatus "rejected" `
                    -TerminalSignal ([pscustomobject]@{
                        Path = $errorPath
                        Kind = "error"
                        Signal = [pscustomobject]$dispatchTerminalSignal
                    }) `
                    -StartedAtUtc $turnStart.ToUniversalTime() `
                    -DurationSeconds ((Get-Date) - $turnStart).TotalSeconds `
                    -MissingHarnessTool $missingHarnessTool
                Add-ObservedTerminalRequestKey -Key $turnRequestKey
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
        $artifactWriteStallWatchState = @{}
        $outputWithoutTerminalWatchState = New-GmOutputWithoutTerminalWatchState

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

            if ($null -eq $terminalSignal -and $elapsed % 15 -eq 0 -and (Test-GmBridgeReturnedIdleWithoutTerminalSignal -ElapsedSeconds $elapsed)) {
                $script:ErrorCount++
                Write-Log "  GM bridge returned to idle without a correlated terminal signal; emitting daemon terminal error instead of waiting for full timeout." -Level "ERROR" -Color Red
                $missingHarnessTool = "gm_bridge_idle_without_terminal_signal"
                $idleTerminalSignal = @{
                    sessionId = $turnRequest.sessionId
                    requestId = $turnRequest.requestId
                    turnNumber = $turnNumber
                    status = "error"
                    harnessSource = "gm_bridge_idle_without_terminal_signal"
                    timestamp = (Get-Date).ToUniversalTime().ToString("o")
                    error = "GM bridge returned to idle without a correlated terminal signal."
                }
                Set-Content -Path $errorPath -Value ($idleTerminalSignal | ConvertTo-Json -Depth 4) -Encoding UTF8
                $terminalSignal = [pscustomobject]@{
                    Path = $errorPath
                    Kind = "error"
                    Signal = [pscustomobject]$idleTerminalSignal
                }
                break
            }

            if ($null -eq $terminalSignal -and $elapsed % 15 -eq 0) {
                $payloadStall = Test-GmOutputWithoutTerminalSignal -ElapsedSeconds $elapsed -WatchState $outputWithoutTerminalWatchState
                if ($null -ne $payloadStall -and $payloadStall.isStalled) {
                    $script:ErrorCount++
                    Write-Log "  GM wrote turn payload files without a correlated terminal signal; emitting daemon terminal error before indefinite wait." -Level "ERROR" -Color Red
                    $missingHarnessTool = "gm_output_without_terminal_signal"
                    $payloadCleanup = Stop-GmBridgeAfterTurnTimeout -TurnRequest $turnRequest -ElapsedSeconds $elapsed -Reason "gm_output_without_terminal_signal"
                    $payloadStall | Add-Member -NotePropertyName timeoutBridgeCleanup -NotePropertyValue $payloadCleanup -Force
                    [void](Write-DaemonJsonFileBestEffort -Path $OutputWithoutTerminalReportFile -Payload $payloadStall -Depth 10)
                    $payloadTerminalSignal = @{
                        sessionId = $turnRequest.sessionId
                        requestId = $turnRequest.requestId
                        turnNumber = $turnNumber
                        status = "error"
                        harnessSource = "gm_output_without_terminal_signal"
                        timestamp = (Get-Date).ToUniversalTime().ToString("o")
                        error = "GM wrote turn payload files without a correlated terminal signal."
                        changedFiles = $payloadStall.changedFiles
                        outputWithoutTerminal = $payloadStall
                    }
                    Set-Content -Path $errorPath -Value ($payloadTerminalSignal | ConvertTo-Json -Depth 12) -Encoding UTF8
                    $terminalSignal = [pscustomobject]@{
                        Path = $errorPath
                        Kind = "error"
                        Signal = [pscustomobject]$payloadTerminalSignal
                    }
                    break
                }

                $artifactStall = Test-GmBridgeArtifactWritingStall -ElapsedSeconds $elapsed -WatchState $artifactWriteStallWatchState
                if ($null -ne $artifactStall -and $artifactStall.isStalled) {
                    $script:ErrorCount++
                    Write-Log "  GM bridge appears stalled while preparing turn artifacts; emitting daemon terminal error before full timeout." -Level "ERROR" -Color Red
                    $missingHarnessTool = "gm_bridge_artifact_write_stall"
                    $artifactCleanup = Stop-GmBridgeAfterTurnTimeout -TurnRequest $turnRequest -ElapsedSeconds $elapsed -Reason "gm_bridge_artifact_write_stall"
                    $artifactStall | Add-Member -NotePropertyName timeoutBridgeCleanup -NotePropertyValue $artifactCleanup -Force
                    [void](Write-DaemonJsonFileBestEffort -Path $ArtifactWriteStallReportFile -Payload $artifactStall -Depth 10)
                    $artifactTerminalSignal = @{
                        sessionId = $turnRequest.sessionId
                        requestId = $turnRequest.requestId
                        turnNumber = $turnNumber
                        status = "error"
                        harnessSource = "gm_bridge_artifact_write_stall"
                        timestamp = (Get-Date).ToUniversalTime().ToString("o")
                        error = "GM bridge appears stalled while preparing turn artifacts."
                        artifactWriteStall = $artifactStall
                    }
                    Set-Content -Path $errorPath -Value ($artifactTerminalSignal | ConvertTo-Json -Depth 12) -Encoding UTF8
                    $terminalSignal = [pscustomobject]@{
                        Path = $errorPath
                        Kind = "error"
                        Signal = [pscustomobject]$artifactTerminalSignal
                    }
                    break
                }
            }

            if ($elapsed % 60 -eq 0) {
                Write-Log "  Waiting... (${elapsed}s)" -Color DarkGray
                Update-DaemonProcessingHeartbeat -TurnNumber $turnNumber -ElapsedSeconds $elapsed
            }
        }

        if ($TurnTimeout -gt 0 -and $elapsed -ge $TurnTimeout -and $null -eq $terminalSignal) {
            $script:ErrorCount++
            Write-Log "  Timeout after ${elapsed}s" -Level "ERROR" -Color Red
            $dispatchDiagnostics.Timeout = $true
            $missingHarnessTool = "gm_turn_timeout"
            $timeoutBridgeCleanup = Stop-GmBridgeAfterTurnTimeout -TurnRequest $turnRequest -ElapsedSeconds $elapsed
            $timeoutSignal = @{
                sessionId = $turnRequest.sessionId
                requestId = $turnRequest.requestId
                turnNumber = $turnNumber
                status = "error"
                harnessSource = "gm_daemon_timeout"
                timestamp = (Get-Date).ToUniversalTime().ToString("o")
                error = "Timeout after ${elapsed}s"
                timeoutBridgeCleanup = $timeoutBridgeCleanup
            }
            Set-Content -Path $errorPath -Value ($timeoutSignal | ConvertTo-Json -Depth 8) -Encoding UTF8
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
            $repairGraceMs = if ($dispatchDiagnostics.Status -eq "preexisting-terminal") { 0 } else { $script:CorrelatedRepairGraceMilliseconds }
            $correlatedRepair = Wait-CorrelatedValidationRepairRequest -TurnRequest $turnRequest -GraceMilliseconds $repairGraceMs
            if ($null -ne $correlatedRepair) {
                Write-Log "  Done, but correlated validation repair is pending ($([math]::Round($duration, 1))s)" -Level "TURN" -Color Yellow
                Write-GmTrajectoryRecord `
                    -Kind "turn" `
                    -Mode "ordinary" `
                    -RequestObject $turnRequest `
                    -Dispatch $dispatchDiagnostics `
                    -ValidationStatus "rejected" `
                    -IssueKinds (Get-GmTrajectoryIssueKinds -RequestObject $correlatedRepair) `
                    -RepairPacketRefs (Get-GmTrajectoryRepairPacketRefs -RequestObject $correlatedRepair) `
                    -ValidationDiagnostics (Get-GmTrajectoryValidationDiagnostics -RequestObject $correlatedRepair) `
                    -RepairPacketDiagnostics (Get-GmTrajectoryRepairPacketDiagnostics -RequestObject $correlatedRepair) `
                    -RepairAttempts (Get-GmValidationRepairAttempt -RepairRequest $correlatedRepair) `
                    -RepairStatus "requested" `
                    -TerminalSignal $terminalSignal `
                    -StartedAtUtc $turnStart.ToUniversalTime() `
                    -DurationSeconds $duration
            }
            else {
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
            if ([string]::IsNullOrWhiteSpace($missingHarnessTool)) {
                $missingHarnessTool = Get-MissingHarnessToolFromTerminalError -TerminalSignal $terminalSignal
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
        Write-DaemonStatus -Status "running" -Reason "turn_processing_finished"
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

        $repairPacketKinds = @()
        foreach ($packetCollectionName in @("harnessRepairPackets", "repairPackets")) {
            $packetCollection = $repair.PSObject.Properties[$packetCollectionName]
            if ($null -ne $packetCollection -and $null -ne $packetCollection.Value) {
                foreach ($packet in @($packetCollection.Value)) {
                    if ($null -ne $packet.kind -and -not [string]::IsNullOrWhiteSpace([string]$packet.kind)) {
                        $repairPacketKinds += [string]$packet.kind
                    }
                }
            }
        }

        $issueCodes = @()
        if ($repair.errors) {
            foreach ($err in @($repair.errors)) {
                if ($err.code) { $issueCodes += [string]$err.code }
            }
        }

        $hasAcceptedTurnOutputArtifactRepair = @($repairPacketKinds | Where-Object { [string]::Equals($_, "accepted_turn_output_artifact_repair", [System.StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
        $hasGuardianPendingCreationRepair = @($repairPacketKinds | Where-Object { [string]::Equals($_, "guardian_pending_creation_materialization_repair", [System.StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
        $hasGuardianTradeInventoryRepair = @($repairPacketKinds | Where-Object { [string]::Equals($_, "guardian_trade_inventory_resolution_repair", [System.StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
        $hasStalePlayerFacingOutputRepair = @($issueCodes | Where-Object { [string]::Equals($_, "accepted_turn_stale_player_facing_output_after_canonical_repair", [System.StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
        $hasGuardianPendingCreationIssue = @($issueCodes | Where-Object {
            [string]::Equals($_, "guardian_materialized_without_create_surface", [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($_, "stale_pending_guardian_creation_after_materialization", [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($_, "pending_guardian_creation_missing_materialized_guardian", [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($_, "pending_guardian_creation_unresolved_after_startup_turn", [System.StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0
        $hasGuardianTradeInventoryIssue = @($issueCodes | Where-Object {
            [string]::Equals($_, "guardian_trade_request_missing_guardian_resolution", [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($_, "guardian_trade_request_missing_inventory_resolution", [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($_, "guardian_trade_request_missing_receipt_resolution", [System.StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0
        $outputArtifactRepairDirective = if ($hasAcceptedTurnOutputArtifactRepair -or $hasStalePlayerFacingOutputRepair) {
            " You MUST read '$($script:CompactOutputArtifactRepairTemplatePath)' before any broad repair examples. For accepted_turn_output_artifact_repair, this is output-only repair: rewrite only output/narrative_response.json, output/interface_updates.json, and output/debug_logs.json as listed by validation_repair_request.json; do not mention JSON, validation, repair, canonical state, arrays, file paths, field names, or storage mechanics inside narrative_response.response; do not touch canonical game_state files unless the current request still lists canonical errors."
        } else {
            ""
        }
        $guardianPendingCreationRepairDirective = if ($hasGuardianPendingCreationRepair -or $hasGuardianPendingCreationIssue) {
            " For startup Guardian creation repairs, use validation_repair_request.json.harnessRepairPackets[] kind guardian_pending_creation_materialization_repair before broad Guardian examples. Read game_state/meta/guardians.json.pendingGuardianCreation as startup authority; for a New Game freeform startup request, write UpdateGuardians.create as the UpdateGuardians[] JSON array with command=create and data=<full canonical Guardian> as the authority. Start from harnessRepairPackets[].canonicalCreateSkeleton and allowedEnums, then mirror that result into guardians[] + activeGuardian + chaosSeaNavigation and remove pendingGuardianCreation. Do not repair only materialized mirrors, keep pendingGuardianCreation as a pending-only fallback, delete it alone, or leave the Guardian only in prose."
        } else {
            ""
        }
        $guardianTradeInventoryRepairDirective = if ($hasGuardianTradeInventoryRepair -or $hasGuardianTradeInventoryIssue) {
            " For Guardian trade inventory repairs, use validation_repair_request.json.harnessRepairPackets[] kind guardian_trade_inventory_resolution_repair before broad examples. Read game_state/control/pending_guardian_trade_request.json as read-only authority, keep it unchanged, patch only the matching guardian.tradeInventory and tradeInventoryReceipts/UpdateGuardianTradeInventoryReceipts in game_state/meta/guardians.json, and finish the repair; do not create a new turn or leave the vitrine in prose."
        } else {
            ""
        }

        $readyPath = "$GameSessionPath\game_state\control\validation_repair_ready.json"
        $message = "REPAIR MODE for rejected turn #$turnNumber (requestId=$requestId, attempt=$attempt).$($script:GmContextPackDirective)$($script:GmDocPathDirective)$($script:GmSafeProbeDirective)$($script:GmSourceFallbackDirective)$($script:GmCompactTemplateDirective)$($script:GmExperienceLessonsDirective)$($script:GmLiveTestRubricDirective)$($script:GmTurnHelperDirective) You MUST reread $GameSessionPath\game_state\control\validation_repair_request.json and '$($script:CompactValidationRepairTemplatePath)' before opening large copied examples.$outputArtifactRepairDirective$guardianPendingCreationRepairDirective$guardianTradeInventoryRepairDirective Also use '$($script:CompactActorReasoningTemplatePath)' for actor coverage repairs; use '$($script:CompactMortalNpcTemplatePath)' for any repair touching game_state/npcs/npc_core.json or NPC validation errors; use '$($script:CompactMortalFactionTemplatePath)' for any repair touching game_state/factions/* or faction validation errors; use '$($script:CompactMortalLocationTemplatePath)' for any repair touching game_state/world/current_location.json, game_state/world/world_map.json, or unknown location ids; use '$($script:CompactMortalSkillTemplatePath)' for any repair touching activeSkillChanges, passiveSkillChanges, skillMasteryChanges, or player skill files; use '$($script:CompactMortalExperienceTemplatePath)' for any repair touching game_state/player/experience.json, experienceGained, level-up, or stat-point level progression; and prefer validation_repair_request.json.harnessRepairPackets over source-code archaeology. Read '$($script:TaskGuideMainPath)' for repair phase rules; use '$($script:ExampleMainPath)' only when compact templates do not cover a route-specific shape.$($script:AfterlifeRealmGateDirective)$($script:AfterlifeExamplesDirective)$($script:AfterlifeCombatConditionsDirective)$($script:AfterlifeSpecialArtCombatEffectDirective) Fix only the listed validation errors in the already written files IN PLACE. Do NOT create a new turn. Do NOT run unrelated git or repository tasks. Do NOT wait for another prompt after files are fixed; finish the repair protocol immediately. never write ready/turn_complete.json for repair."
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

        $null = Write-GmExperienceLessons

        $message += " If validation_repair_request.json includes mortal_combat_state_repair or mortal_combat_state_missing, you MUST read '$($script:CompactMortalCombatTemplatePath)' and repair the existing combat scene by writing combat_log.json and relevant enemies/allies; do not delete XP/mastery/status changes."
        if ($hasDiagnosticOnlyMetadata) {
            Write-Log "  -> Skipping diagnostic-only validation repair request (metadataDiagnosticOnly=true); waiting for client-owned snapshot/metadata resolution." -Level "WARN" -Color Yellow
            $dispatchDiagnostics = [pscustomobject]@{
                Status = "skipped-diagnostic-only"
                Attempts = 0
                BusyRetries = 0
                Timeout = $false
            }
            Write-GmTrajectoryRecord `
                -Kind "repair" `
                -Mode "validation_repair" `
                -RequestObject $repair `
                -Dispatch $dispatchDiagnostics `
                -ValidationStatus "rejected" `
                -IssueKinds (Get-GmTrajectoryIssueKinds -RequestObject $repair) `
                -RepairPacketRefs (Get-GmTrajectoryRepairPacketRefs -RequestObject $repair) `
                -ValidationDiagnostics (Get-GmTrajectoryValidationDiagnostics -RequestObject $repair) `
                -RepairPacketDiagnostics (Get-GmTrajectoryRepairPacketDiagnostics -RequestObject $repair) `
                -RepairAttempts $attempt `
                -RepairStatus "diagnostic-only-skipped" `
                -MissingHarnessTool "client_owned_diagnostic_repair_resolution" `
                -StartedAtUtc $fileInfo.LastWriteTimeUtc
            return
        }

        $dispatchDiagnostics = Dispatch-WithRetry -Message $message -PendingPath $RepairPath -ReturnDetails
        if ($dispatchDiagnostics.Status -eq "sent" -or $dispatchDiagnostics.Status -eq "clipboard") {
            $script:ActiveValidationRepairWatch = New-GmValidationRepairArtifactWatchState -RepairRequest $repair -DispatchStatus ([string]$dispatchDiagnostics.Status)
        }
        Write-GmTrajectoryRecord `
            -Kind "repair" `
            -Mode "validation_repair" `
            -RequestObject $repair `
            -Dispatch $dispatchDiagnostics `
            -ValidationStatus "rejected" `
            -IssueKinds (Get-GmTrajectoryIssueKinds -RequestObject $repair) `
            -RepairPacketRefs (Get-GmTrajectoryRepairPacketRefs -RequestObject $repair) `
            -ValidationDiagnostics (Get-GmTrajectoryValidationDiagnostics -RequestObject $repair) `
            -RepairPacketDiagnostics (Get-GmTrajectoryRepairPacketDiagnostics -RequestObject $repair) `
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

        $message = "Terminal protocol failure for turn #$turnNumber (requestId=$requestId).$($script:GmContextPackDirective)$($script:GmDocPathDirective)$($script:GmSafeProbeDirective)$($script:GmSourceFallbackDirective)$($script:GmCompactTemplateDirective)$($script:GmExperienceLessonsDirective)$($script:GmLiveTestRubricDirective)$($script:GmTurnHelperDirective) You MUST reread $GameSessionPath\game_state\control\terminal_protocol_failure_request.json and '$($script:CompactValidationRepairTemplatePath)' before opening large copied examples. Read '$($script:TaskGuideMainPath)' for terminal protocol phase rules; use '$($script:ExampleMainPath)' only when compact templates do not cover a route-specific shape.$($script:AfterlifeRealmGateDirective)$($script:AfterlifeExamplesDirective)$($script:AfterlifeCombatConditionsDirective)$($script:AfterlifeSpecialArtCombatEffectDirective) This is NOT validation_repair_request.json and NOT a repair loop. The client already closed the current wait cycle. Do NOT create validation_repair_ready.json. Do NOT create a new turn on your own. Fix your terminal-signal discipline for the NEXT correct client request: exactly one terminal signal, terminal signal written last, never both turn_complete and turn_error for one request."
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

        $null = Write-GmExperienceLessons

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
        $resolvedTimeoutConflict = Resolve-DaemonTimeoutTerminalConflict -MatchedSignals $matchedSignals
        if ($null -ne $resolvedTimeoutConflict) {
            return $resolvedTimeoutConflict
        }

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

function Test-DaemonTimeoutTerminalSignal {
    param([psobject]$SignalEntry)

    if ($null -eq $SignalEntry -or $SignalEntry.Kind -ne "error" -or $null -eq $SignalEntry.Signal) {
        return $false
    }

    $signal = $SignalEntry.Signal
    return [string]$signal.harnessSource -eq "gm_daemon_timeout" -or
        ([string]$signal.error).StartsWith("Timeout after ", [System.StringComparison]::OrdinalIgnoreCase)
}

function Resolve-DaemonTimeoutTerminalConflict {
    param([object[]]$MatchedSignals)

    $successSignals = @($MatchedSignals | Where-Object { $_.Kind -eq "success" })
    $timeoutSignals = @($MatchedSignals | Where-Object { Test-DaemonTimeoutTerminalSignal -SignalEntry $_ })

    if ($successSignals.Count -eq 1 -and $timeoutSignals.Count -gt 0 -and ($successSignals.Count + $timeoutSignals.Count) -eq $MatchedSignals.Count) {
        foreach ($timeoutSignal in $timeoutSignals) {
            $fileName = Split-Path $timeoutSignal.Path -Leaf
            Write-Log "  Removed stale daemon timeout terminal signal artifact: $fileName" -Level "WARN" -Color Yellow
            Remove-Item $timeoutSignal.Path -Force -ErrorAction SilentlyContinue
        }

        return $successSignals[0]
    }

    return $null
}

# ═══════════════════════════════════════════════
# FileSystemWatcher & Main Loop
# ═══════════════════════════════════════════════

Assert-SingleDaemonInstance

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
        try {
            Start-Sleep -Milliseconds $PollingInterval
            Update-DaemonHeartbeat

            if ((Test-Path $TurnRequestFile) -and !$script:IsProcessing) {
                Process-Turn -RequestPath $TurnRequestFile
            }
            if (Test-Path $RepairRequestFile) {
                Process-RepairRequest -RepairPath $RepairRequestFile
            }
            if (Test-Path $TerminalProtocolFailureRequestFile) {
                Process-TerminalProtocolFailureRequest -FailurePath $TerminalProtocolFailureRequestFile
            }
            Watch-ActiveValidationRepairProgress

            # Status every 5 minutes
            $statusTimer += $PollingInterval
            if ($statusTimer -ge 300000) {
                $uptime = ((Get-Date) - $script:StartTime)
                Write-Log "Status: ${script:TurnCount} turns, ${script:ErrorCount} errors, uptime $([math]::Floor($uptime.TotalHours))h$($uptime.Minutes)m" -Color DarkGray
                $statusTimer = 0
            }
        }
        catch {
            $script:ErrorCount++
            $script:DaemonLastLoopError = New-DaemonErrorPayload -ErrorRecord $_ -Phase "main_loop"
            Write-Log "Main loop error recovered: $($_.Exception.Message)" -Level "ERROR" -Color Red
            Write-DaemonStatus -Status "running" -Reason "main_loop_error_recovered"
            Start-Sleep -Milliseconds ([Math]::Max(250, $PollingInterval))
        }
    }
}
catch {
    $script:DaemonFatalError = $_
    $script:ErrorCount++
    Write-Log "Fatal daemon error: $($_.Exception.Message)" -Level "ERROR" -Color Red
    Write-DaemonFatalReport -ErrorRecord $_ -Phase "fatal"
}
finally {
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
    $repairWatcher.EnableRaisingEvents = $false
    $repairWatcher.Dispose()
    $terminalProtocolFailureWatcher.EnableRaisingEvents = $false
    $terminalProtocolFailureWatcher.Dispose()
    if ($null -ne $script:DaemonFatalError) {
        Write-DaemonStatus -Status "failed" -FatalError $script:DaemonFatalError
    }
    else {
        Write-DaemonStatus -Status "stopped"
    }
    Write-Host ""
    Write-Log "Daemon stopped. Turns: $($script:TurnCount), Errors: $($script:ErrorCount)" -Level "SHUTDOWN" -Color Yellow
}
