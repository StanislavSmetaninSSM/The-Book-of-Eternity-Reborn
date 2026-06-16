# Implementation Plan: Afterlife Profile and Inbox Follow-Through Drill-Downs

**Branch**: `work/1066-afterlife-profile-inbox-drilldowns` | **Date**: 2026-06-16 | **Spec**: `specs/1066-afterlife-profile-inbox-drilldowns/spec.md`

## Source Issue

- GitHub issue #1066 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1066
- Origin audit: #949 AFD-005 from `docs/audits/afterlife-drilldown-audit.md`.
- Sibling follow-up: #1067 spiritual conflict exchange/art drill-downs remains out of scope.

## Summary

Add shared C# browser command-result detail/follow-through actions for afterlife profile, threat, chronicle, and inbox/support rows. Preserve overview output, keep default copy Russian/in-world and no-raw/no-debug, and avoid runtime/write/GM contract changes.

## Technical Context

- **Language/runtime**: C# / .NET 8 command protocol, Explorer web command service, afterlife command-result builders, Spectre.Console/browser command-result DTO tests.
- **Primary source areas**: `BookOfEternityClient/UI/ExplorerAfterlifeCombatCommandResultBuilder.cs`, `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.Chronicles.cs`, `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`, command catalog/action metadata, and focused tests under `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests*.cs`, `ExplorerModeCommandTests.Afterlife.cs`, `AfterlifeDrilldownAuditTests.cs`, or source-guard tests if appropriate.
- **Governance**: `AGENTS.md`, `.specify/memory/constitution.md`, #1066 issue body, #949 AFD-005 audit row, afterlife drill-down child launch guidance, Browser action result/detail surface guidance, Browser Afterlife relationship-gate boundary notes for `/afterlife_profiles`.
- **Precedent**: #1063-#1065 added read-only browser selected details for sibling afterlife surfaces while keeping runtime contracts untouched; #1072 cleaned default Shining diagnostics behind advanced mode.

## Constitution Check

- **Issue traceability**: All repo edits are tied to #1066; spec/plan/tasks/contract/checklist link #1066 and #949.
- **Player-facing integrity**: Default browser output must be Russian/in-world and must not expose API, DTO, endpoint, protocol, debug, raw JSON, parser exception, hidden/gm-only data, or local path copy outside advanced mode.
- **Contract/state authority**: Intended as presentation/read-only follow-through only. Runtime state schemas, pending/control files, validation, normalizers, write/prompt services, GM prompts/examples, and manifests are not planned to change.
- **Test-first verification**: Add failing tests/source guards before production code for selected-detail action exposure, detail rendering, missing/stale target handling, and no-mutation boundaries.
- **Agent orchestration**: Hermes owns final acceptance/PR/merge/closure; Codex implements and verifies in the issue worktree.

## Project Structure and Responsibilities

- `specs/1066-afterlife-profile-inbox-drilldowns/spec.md`: product requirements, boundaries, and verification plan.
- `specs/1066-afterlife-profile-inbox-drilldowns/plan.md`: technical approach and gates.
- `specs/1066-afterlife-profile-inbox-drilldowns/tasks.md`: executable task list and evidence log.
- `specs/1066-afterlife-profile-inbox-drilldowns/contracts/browser-afterlife-profile-inbox-drilldowns.md`: read-only detail/follow-through presentation contract.
- `specs/1066-afterlife-profile-inbox-drilldowns/checklists/requirements.md`: requirements quality checklist.
- `BookOfEternityClient.Tests/`: focused RED/GREEN coverage for overview action exposure, selected details, inbox follow-through, missing/stale targets, and no-raw default output.
- `BookOfEternityClient/`: shared C# command-result/action metadata changes; keep mutation/write authority in existing services.
- `BookOfEternityClient.WebFrontend/`: avoid production changes unless existing React rendering is proven to be the only missing presentation layer; React must remain presentation-only.
- GM-facing docs/examples: update only if runtime contracts or GM-authored schemas/behavior change; otherwise record no-impact rationale.

## Implementation Approach

1. Inspect current `/afterlife_profiles`, `/afterlife_threats`, `/afterlife_chronicles`, and `/afterlife_inbox` command-result builders, browser action metadata, and existing argument/selected-detail command patterns from #1063-#1065.
2. Map AFD-005 to exact row types and stable IDs: profile, threat, chronicle/event, notification, and supported target references in inbox/support rows.
3. Add RED tests for missing overview actions and selected-detail/follow-through behavior before production code. Include missing/stale target and read-only/no-auto-read cases.
4. Implement the smallest shared C# presentation changes: overview results expose safe detail/follow-through actions; selected detail commands/actions reuse canonical state and existing context renderers; unsupported targets produce player-facing unavailable results.
5. Preserve existing overview output and mutating prompt/write flows. Do not implement new pending/control/write contracts.
6. Re-run focused tests, broad afterlife/browser slice, C# builds, Spec Kit prerequisite, diff/static scans, and docs/frontend gates only if touched.
7. Commit with `[skip ci]`. Hermes will run independent review, PR, squash merge, issue comment/closure, label transition, and cleanup.

## Risk Controls

- Do not hide overview summaries while adding details.
- Do not auto-mark inbox notifications read from read-only follow-through actions.
- Do not leak raw IDs, hidden/gm-only evidence, local `game_state/` paths, API/DTO/debug/protocol wording, or raw JSON in default output.
- Do not add React-side command filtering as gameplay authority; C# command-result surfaces must be safe by default.
- Do not close #1067 or implement spiritual conflict/art selected details under #1066.
- If a desired link requires a new runtime/GM contract, stop that sub-surface and create/link a follow-up rather than expanding #1066.

## Verification Commands

Baseline and post-change commands should include real non-zero counts:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeProfiles|FullyQualifiedName~AfterlifeThreats|FullyQualifiedName~AfterlifeChronicles|FullyQualifiedName~AfterlifeInbox|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"

dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore

dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true

git diff --check origin/main...HEAD
```

If afterlife docs/contracts change:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"
```

If frontend files change:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

## Expected PR / Closure Evidence

- PR body uses `Closes #1066` only for the source issue.
- PR references #949 AFD-005 as origin context and #1067 as sibling non-closing reference.
- Issue evidence comment includes changed files, local-gated verification commands/counts, independent review verdict, docs/prompts impact, and `GitHub Actions: not used/not required`.
