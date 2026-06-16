# Implementation Plan: Spiritual Conflict Exchange and Art Drill-Downs

**Branch**: `work/1067-spiritual-conflict-art-drilldowns` | **Date**: 2026-06-17 | **Spec**: `specs/1067-spiritual-conflict-art-drilldowns/spec.md`

## Source Issue

- GitHub issue #1067 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1067
- Origin audit: #949 AFD-006 from `docs/audits/afterlife-drilldown-audit.md`.
- Sibling follow-ups #1063-#1066 were separate children; do not close or modify their scope from this feature.

## Summary

Add shared C# browser command-result detail actions for spiritual-conflict active exchanges, combat-log/recent-conflict events, and spiritual-art rows. Preserve overview/help output, keep default copy Russian/in-world and no-raw/no-debug, and avoid runtime/write/GM contract changes.

## Technical Context

- **Language/runtime**: C# / .NET 8 command protocol, Explorer web command service, afterlife command-result builders, Spectre.Console/browser command-result DTO tests.
- **Primary source areas**: `BookOfEternityClient/UI/ExplorerAfterlifeCombatCommandResultBuilder.cs`, `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs`, `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`, `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`, browser command/action metadata, and focused tests under `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests*.cs`, `ExplorerModeCommandTests.Afterlife.cs`, `AfterlifeDrilldownAuditTests.cs`, or source-guard tests if appropriate.
- **Governance**: `AGENTS.md`, `.specify/memory/constitution.md`, #1067 issue body, #949 AFD-006 audit row, afterlife drill-down child launch guidance, Browser action result/detail surface guidance, and afterlife contract documentation guardrails.
- **Precedent**: #1063-#1066 added read-only browser selected details/follow-through for sibling afterlife surfaces while keeping runtime contracts untouched; use their tests and command-result helper patterns before creating new patterns.

## Constitution Check

- **Issue traceability**: All repo edits are tied to #1067; spec/plan/tasks/contract/checklist link #1067 and #949.
- **Player-facing integrity**: Default browser output must be Russian/in-world and must not expose API, DTO, endpoint, protocol, debug, raw JSON, parser exception, hidden/gm-only data, or local path copy outside advanced mode.
- **Contract/state authority**: Intended as presentation/read-only detail only. Spiritual-conflict state schemas, dice/reward/validation mechanics, pending/control files, normalizers, write/prompt services, GM prompts/examples, and manifests are not planned to change.
- **Test-first verification**: Add failing tests/source guards before production code for selected-detail action exposure, detail rendering, missing/stale/malformed target handling, and no-mutation boundaries.
- **Agent orchestration**: Hermes owns final acceptance/PR/merge/closure; Codex implements and verifies in the issue worktree.

## Project Structure and Responsibilities

- `specs/1067-spiritual-conflict-art-drilldowns/spec.md`: product requirements, boundaries, and verification plan.
- `specs/1067-spiritual-conflict-art-drilldowns/plan.md`: technical approach and gates.
- `specs/1067-spiritual-conflict-art-drilldowns/tasks.md`: executable task list and evidence log.
- `specs/1067-spiritual-conflict-art-drilldowns/contracts/browser-spiritual-conflict-art-drilldowns.md`: read-only selected-detail presentation contract.
- `specs/1067-spiritual-conflict-art-drilldowns/checklists/requirements.md`: requirements quality checklist.
- `BookOfEternityClient.Tests/`: focused RED/GREEN coverage for overview action exposure, selected exchange/log/art details, missing/stale/malformed targets, and no-raw default output.
- `BookOfEternityClient/`: shared C# command-result/action metadata changes; keep mutation/write authority in existing services.
- `BookOfEternityClient.WebFrontend/`: avoid production changes unless existing React rendering is proven to be the only missing presentation layer; React must remain presentation-only.
- GM-facing docs/examples: update only if runtime contracts, GM-authored schemas, command mechanics, validation, normalizers, pending/control, or write authority change; otherwise record no-impact rationale.

## Implementation Approach

1. Inspect current `/spiritual_conflict`, `/spiritual_combat_log`, `/spiritual_arts`, and `/spiritual_combat_help` command-result builders, browser action metadata, and selected-detail command patterns from #1063-#1066.
2. Map AFD-006 to exact row types and stable identifiers: active exchange entries, combat-log/recent-conflict entries, and spiritual-art rows. Prefer existing stable IDs; use safe indices only when existing state has no durable ID and tests prove stale-index handling.
3. Add RED tests before production code for missing overview actions and selected-detail behavior. Include missing/stale/sparse/malformed target cases and no-mutation behavior for read-only detail actions.
4. Implement the smallest shared C# presentation changes: overview results expose safe detail actions; selected detail commands/actions reuse canonical state and existing renderers; unsupported targets produce player-facing unavailable results.
5. Preserve existing overview/help output and spiritual-arts local upgrade/write flows. Do not implement new pending/control/write contracts.
6. Re-run focused tests, broad afterlife/browser slice, C# builds, Spec Kit prerequisite, diff/static scans, and docs/frontend gates only if touched.
7. Commit with `[skip ci]`. Hermes will run independent review, PR, squash merge, issue comment/closure, label transition, and cleanup.

## Risk Controls

- Do not hide overview summaries while adding details.
- Do not treat `/spiritual_arts` read-only inspect as an upgrade/write operation.
- Do not mutate spiritual-conflict state, mark notifications, or create pending/control files from read-only detail actions.
- Do not leak raw IDs, hidden/gm-only evidence, local `game_state/` paths, API/DTO/debug/protocol wording, parser exception details, or raw JSON in default output.
- Do not add React-side command filtering as gameplay authority; C# command-result surfaces must be safe by default.
- Do not broaden into #1063-#1066 sibling areas or runtime spiritual-combat mechanics.
- If a desired detail requires a new runtime/GM contract, stop that sub-surface and create/link a follow-up rather than expanding #1067.

## Verification Commands

Baseline and post-change commands should include real non-zero counts:

```bash
unset PYTHONHOME UV_INTERNAL__PYTHONHOME PYTHONPATH
specify version

powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SpiritualConflict|FullyQualifiedName~SpiritualCombat|FullyQualifiedName~SpiritualArts|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"

dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore

dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true

git diff --check origin/main...HEAD
```

If afterlife docs/contracts change:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

If React/Vite files change:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

## Expected No-Docs-Impact Rationale

If the implementation remains limited to command-result/detail-action presentation and tests, record this rationale in `tasks.md`, PR body, issue evidence comment, and closure report:

> No afterlife runtime contract, pending/control file, validation/normalizer, write service, GM prompt/example/manifest, or daemon/launcher prompt entrypoint changed. The diff is presentation/read-only browser detail parity only, so afterlife docs/examples coverage gates are not required.

## Complexity Tracking

No constitution violations are expected. If implementation requires runtime/GM contract changes, update this plan and the contract/docs gates before continuing.
