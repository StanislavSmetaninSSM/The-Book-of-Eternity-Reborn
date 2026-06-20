# Console Afterlife Output Audit

Source issues: #1164, #1163, #1167, #1168, #1169.

Date: 2026-06-20.

## Scope

This audit covers non-browser console afterlife output for Chaos Sea and Shining
Abode command flows. The goal is to keep normal player views readable and keep
raw contract payloads in explicit audit, repair, or authoring-preview contexts.

## Fixed In This Pass

- `/chaos_sea` normal overview now shows a player-facing "Море Хаоса" summary
  and "Ожидающие решения посмертия" instead of the "Chaos Sea Audit" panel.
- `/chaos_sea` pending-contract overview no longer dumps full payloads, pending
  file paths, raw request ids, or closure fields such as
  `residentInteractionLogUpdates` in the normal overview.
- `/help` in Chaos Sea and Shining Abode no longer presents full/canonical JSON
  panels as a normal player route. It now points players to summaries, tables,
  and "Подробно" actions, with technical audit reserved for repair/GM authoring.
- `/status` in afterlife realms now defaults to a player summary. Full state
  payloads, canonical field names, pending paths, and malformed raw data are
  reserved for `/status audit` / `/статус аудит`.
- `/shining_abode` now separates normal gate/package/core-action details from
  explicit `🔧 Аудит ...` menu choices. The default detail views show what the
  player can understand and do next; raw pending/core receipt payloads stay in
  the audit choices.
- Chaos Sea action confirmations now use player previews by default. The same
  confirmation prompt exposes `🔧 Показать технический контракт` when a GM
  authoring payload exists, so request ids, pending paths, receipts, and raw
  JSON remain available without being normal gameplay text.
- Existing pending resident roster screens now summarize "waiting for GM"
  status instead of dumping the live pending bundle.

## Reviewed Commands

- `/chaos_sea` / `/море_хаоса`: player overview is now summary-first. Remaining
  detail entry points should stay readable and avoid raw payloads by default.
- `/help` / `/помощь`: player-facing afterlife copy no longer advertises raw
  JSON as ordinary gameplay.
- `/status` / `/статус`: player summary by default; `/status audit` keeps the
  dense diagnostic state view for repair, validator investigation, and GM
  authoring checks.
- `/shining_abode` / `/сияющая_обитель`: player overview and details by
  default; `🔧 Аудит Врат и пакета`, `🔧 Аудит исходов Обители`, and
  `🔧 Аудит ожидающих действий Обители` keep full technical payloads.
- `/shining_politics` / `/сияющая_политика`: summary and tables exist, but
  pending political contract previews still use audit-heavy copy in some flows.
- `/guardian_trade`, `/abode_residents`, `/resident_interaction`,
  `/resident_transfer`, `/archive`, `/abode_offering`: normal confirmations are
  player previews. Technical payloads are reachable through the explicit
  technical-contract choice on the confirmation prompt or through `/status
  audit` for already-written pending state.
- `/spiritual_conflict`, `/spiritual_combat_log`, `/spiritual_combat_help`:
  existing tests already assert no default "Полный JSON" leakage for the main
  player-facing conflict/help/log views.

## Deliberately Retained Audit Surfaces

The following output classes may still contain raw payloads:

- explicit repair or malformed-state diagnostics, where raw damaged data helps
  recover the session;
- explicit audit/debug choices, including `/status audit`, `🔧 Аудит ...`
  Shining Abode entries, and `🔧 Показать технический контракт` in Chaos Sea
  action confirmations;
- GM-facing `_pendingGmAction` text and pending/control files, which are not
  directly shown as ordinary player command output.

Raw payloads in these surfaces are deliberate diagnostics, not finished player
UX. New ordinary player commands should not introduce raw JSON, pending file
paths, request ids, receipt field names, or canonical contract language without
an explicit audit/debug route.

## Verification

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TryProcessCommand_ChaosSeaOverviewSummarizesPendingContractsWithoutRawAuditPayload"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TryProcessCommand_Help_ChaosSeaUsesPlayerFacingRussianWording|TryProcessCommand_Help_ShiningAbodeUsesPlayerFacingRussianWording"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TryProcessCommand_Status"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ShiningAbode|ShiningTrade|ShiningPolitics|SourceOfLight|ShiningTreasury"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TryProcessCommand_GuardianTradeWithoutInventory|TryProcessCommand_AfterlifeArchive_Consultation|TryProcessCommand_Guardians_AbodeResidentsMissing|TryProcessCommand_Guardians_ResidentTransfer|TryProcessCommand_Guardians_AbodeResidentsExistingPending"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Afterlife|Shining|Chaos" --logger "console;verbosity=minimal"`
