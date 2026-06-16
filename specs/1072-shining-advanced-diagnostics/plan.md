# Implementation Plan: Shining Advanced Diagnostics Boundary

**Branch**: `work/1072-shining-advanced-diagnostics` | **Date**: 2026-06-16 | **Spec**: `specs/1072-shining-advanced-diagnostics/spec.md`

## Source Issue

- GitHub issue #1072 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1072
- Origin review: #1065 independent review, non-blocking minor finding.
- Origin audit: #949 AFD-004 from `docs/audits/afterlife-drilldown-audit.md`.

## Summary

Move inherited Shining treasury and Source of Light raw diagnostics out of default browser/player command-result output and behind explicit advanced/debug mode. Preserve useful Russian/in-world summaries, keep advanced troubleshooting data available where supported, and avoid runtime/write/GM-contract changes.

## Technical Context

- **Language/runtime**: C# / .NET 8 command protocol, Explorer web command service, Shining Abode command-result builders, test project source guards.
- **Primary source areas**: `BookOfEternityClient/UI/ExplorerShiningAbodeCommandResultBuilder.cs`, `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`, command protocol/result block types if advanced gating needs metadata, and `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests*.cs` or nearby source-guard tests.
- **Governance**: `AGENTS.md`, `.specify/memory/constitution.md`, #1072 issue body, #1065 review run verdict, #949 AFD-004 audit context, Browser player-vs-advanced UI separation, Browser action result surfaces, Browser Reborn panel closure notes, and afterlife drill-down child launch guidance.
- **Precedent**: #1065 added selected-detail flows while keeping default output free of raw JSON/path leakage; #1072 applies the same boundary to inherited `/shining_treasury` and `/source_of_light` default surfaces.

## Constitution Check

- **Issue traceability**: All repo edits are tied to #1072; spec/plan/tasks/contract/checklist link #1072, #1065, and #949.
- **Player-facing integrity**: Default browser output must be Russian/in-world and must not expose API, DTO, endpoint, protocol, debug, raw JSON, parser exception, or local path copy outside advanced mode.
- **Contract/state authority**: Intended as presentation-only cleanup. Runtime state schemas, pending/control files, validation, normalizers, write/prompt services, GM prompts/examples, and manifests are not planned to change.
- **Test-first verification**: Add failing tests/source guards before production code for the default leakage and advanced boundary.
- **Agent orchestration**: Hermes owns final acceptance/PR/merge/closure; Codex implements and verifies in the issue worktree.

## Project Structure and Responsibilities

- `specs/1072-shining-advanced-diagnostics/spec.md`: product requirements, boundaries, and verification plan.
- `specs/1072-shining-advanced-diagnostics/plan.md`: technical approach and gates.
- `specs/1072-shining-advanced-diagnostics/tasks.md`: executable task list and evidence log.
- `specs/1072-shining-advanced-diagnostics/contracts/browser-shining-advanced-diagnostics.md`: default-vs-advanced presentation contract.
- `specs/1072-shining-advanced-diagnostics/checklists/requirements.md`: requirements quality checklist.
- `BookOfEternityClient.Tests/`: focused RED/GREEN coverage for default vs advanced `/shining_treasury` and `/source_of_light` behavior, including malformed/sparse-state copy guards.
- `BookOfEternityClient/`: shared C# command-result builder/service changes to gate diagnostics and preserve player-facing output.
- `BookOfEternityClient.WebFrontend/`: avoid production changes unless existing React rendering is the proven source of leakage; React must remain presentation-only.
- GM-facing docs/examples: update only if runtime contracts or GM-authored schemas/behavior change; otherwise record no-impact rationale.

## Implementation Approach

1. Inspect current `/shining_treasury` and `/source_of_light` command handling, browser command-result DTO construction, and advanced/debug-mode flags.
2. Identify the exact source of inherited `UiRawJsonBlock`, raw JSON, state-file path, and malformed JSON warning path text in default output.
3. Compare with #1065 selected-detail and existing advanced/debug gating patterns; do not invent new frontend gameplay logic.
4. Add focused RED tests that reproduce default leakage for treasury/source and malformed/sparse states, and tests/source guards that define advanced-mode retention when applicable.
5. Implement the smallest shared C# presentation-boundary change: default mode returns safe Russian/in-world blocks; explicit advanced mode may retain diagnostics.
6. Re-run focused tests, broad Shining/browser slice, C# builds, Spec Kit prerequisite, diff/static scans, and docs/frontend gates only if touched.
7. Commit with `[skip ci]`. Hermes will run independent review, PR, squash merge, issue comment/closure, label transition, and cleanup.

## Risk Controls

- Do not remove useful default treasury/source summaries; replace raw diagnostics with safe player-facing explanations.
- Do not hide player-visible error states entirely; surface actionable in-world/unavailable copy.
- Do not change Shining write/prompt authority, pending/control files, validation, normalizers, or runtime schemas.
- Do not add React-side command filtering as the primary fix; default output must be safe from shared C# authority.
- Do not close #1066 or #1067; mention only as sibling non-closing references.
- If an advanced-mode pathway does not exist, prefer a clear source-guarded no-leak default and documented limitation over broad architecture churn.

## Verification Commands

Baseline and post-change commands should include real non-zero counts:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Shining|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"

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

- PR body uses `Closes #1072` only for the source issue.
- PR references #1065 and #949 as origin context and #1066/#1067 as sibling non-closing references.
- Issue evidence comment includes changed files, local-gated verification commands/counts, independent review verdict, docs/prompts impact, and `GitHub Actions: not used/not required`.
