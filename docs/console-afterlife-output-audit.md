# Console Afterlife Output Audit

Source issues: #1164, #1163.

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

## Reviewed Commands

- `/chaos_sea` / `/море_хаоса`: player overview is now summary-first. Remaining
  detail entry points should stay readable and avoid raw payloads by default.
- `/help` / `/помощь`: player-facing afterlife copy no longer advertises raw
  JSON as ordinary gameplay.
- `/status` / `/статус`: still functions as a dense afterlife audit/status
  surface in several tests. It needs a follow-up split into player summary and
  explicit audit/debug mode before raw panels can be removed safely.
- `/shining_abode` / `/сияющая_обитель`: overview is mostly summary-first, but
  gates/core-action/pending-contract inspections still contain technical audit
  panels by design. They need a follow-up detail/audit separation.
- `/shining_politics` / `/сияющая_политика`: summary and tables exist, but
  pending political contract previews still use audit-heavy copy in some flows.
- `/guardian_trade`, `/abode_residents`, `/resident_interaction`,
  `/resident_transfer`: several confirmation and pending-request previews still
  expose contract language or full payloads because they double as GM authoring
  previews. These should be split into player confirmation plus optional audit.
- `/spiritual_conflict`, `/spiritual_combat_log`, `/spiritual_combat_help`:
  existing tests already assert no default "Полный JSON" leakage for the main
  player-facing conflict/help/log views.

## Deliberately Retained Audit Surfaces

The following output classes may still contain raw payloads until follow-up
issues split them:

- explicit repair or malformed-state diagnostics, where raw damaged data helps
  recover the session;
- local mutation previews that are currently used as GM authoring contracts;
- afterlife `/status` audit blocks used by tests to verify canonical state
  surfaces and pending contract integrity.

These should not be treated as finished player UX. They are documented here so
future work can separate them without losing diagnostic coverage.

## Verification

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TryProcessCommand_ChaosSeaOverviewSummarizesPendingContractsWithoutRawAuditPayload"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TryProcessCommand_Help_ChaosSeaUsesPlayerFacingRussianWording|TryProcessCommand_Help_ShiningAbodeUsesPlayerFacingRussianWording"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Afterlife|Shining|Chaos" --logger "console;verbosity=minimal"`
