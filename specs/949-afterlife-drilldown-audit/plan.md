# Implementation Plan: Afterlife Detail Drill-Down Audit

**Branch**: `949-afterlife-drilldown-audit` | **Date**: 2026-06-16 | **Spec**: `specs/949-afterlife-drilldown-audit/spec.md`

## Source Issue

- GitHub issue #949 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949

## Summary

Produce the afterlife analogue of the closed #948 mortal drill-down audit. The branch should audit Chaos Sea and Shining Abode read-only/local-action overview surfaces, preserve existing overviews, add only small safe read-only detail fixes if they are clearly bounded, and create/link follow-up issues for larger gaps. Console/browser parity and afterlife contract documentation impact must be explicit.

## Technical Context

- **Language/runtime**: C# / .NET 8 client and tests; React/Vite frontend only if existing renderer support is insufficient.
- **Primary source areas**: `BookOfEternityClient/`, `BookOfEternityClient.Tests/`, `docs/audits/`, `Examples/`, `OtherGuides/` when contract docs change.
- **Existing governance**: `AGENTS.md`, `.specify/memory/constitution.md`, Book project skills/references, #949 issue body.
- **Relevant precedent**: `docs/audits/mortal-readonly-drilldown-audit.md`, `specs/1057-mortal-reference-detail-actions/`, and closed PRs #1058-#1062.
- **Expected audit artifact**: `docs/audits/afterlife-drilldown-audit.md` unless existing repo patterns indicate a better path.

## Constitution Check

- **Issue traceability**: All changes are tied to #949; spec/plan/tasks/contract/checklist link the issue.
- **Player-facing integrity**: Default console/browser output must stay in-world/Russian and avoid raw/API/DTO/debug copy unless advanced mode is active.
- **Contract/state authority**: Afterlife state/pending/control/validation/GM docs are contract-sensitive. Contract changes are not planned; if needed, update docs/tests in the same PR or create a focused follow-up.
- **Test-first verification**: Any behavior change requires RED/GREEN evidence before production code. Pure audit/source-guard work should still add guards before modifying production behavior.
- **Agent orchestration**: Hermes owns final acceptance/PR/merge/closure; Codex may implement and verify in the issue worktree.

## Project Structure and Responsibilities

- `specs/949-afterlife-drilldown-audit/spec.md`: durable product requirements and scope boundaries.
- `specs/949-afterlife-drilldown-audit/plan.md`: technical approach and verification strategy.
- `specs/949-afterlife-drilldown-audit/tasks.md`: executable task list and evidence log.
- `specs/949-afterlife-drilldown-audit/contracts/afterlife-drilldown-audit.md`: audit classification contract and follow-up policy.
- `specs/949-afterlife-drilldown-audit/checklists/requirements.md`: requirements quality checklist.
- `docs/audits/afterlife-drilldown-audit.md`: product-facing audit output, classifications, parity notes, follow-up links.
- `BookOfEternityClient.Tests/*`: focused source/audit guards and any RED/GREEN regression tests for small fixes.
- `BookOfEternityClient/*`: only small bounded C# command-result/detail fixes if the audit proves they are safe for #949.
- `BookOfEternityClient.WebFrontend/*`: avoid unless existing rendering cannot preserve shared safe command-result details.
- `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`, and afterlife docs/source-guard tests: update only if runtime/GM-facing afterlife contracts change.

## Implementation Approach

1. Inspect current afterlife command/result builders, console handlers, command catalog/aliases, browser command-result service paths, existing tests, and docs/audits.
2. Draft the audit artifact with all #949 candidate categories and initial classifications.
3. Add a focused source/audit guard that fails if the audit omits required categories, classifications, parity notes, severity, or follow-up links for `follow-up required` rows.
4. For any small safe in-PR fix, add a focused failing test first, then implement the minimal shared C# command-result/detail change and rerun the focused tests.
5. For larger gaps, create linked GitHub follow-up issues with exact scope and paste links into the audit artifact. Do not over-implement #949.
6. Run local verification: focused afterlife/audit tests, broader afterlife/browser/console slice, builds if C# source changed, docs coverage tests if afterlife contract docs changed, Spec Kit prerequisite check, diff check, and added-line static scan.
7. Commit with `[skip ci]`. Hermes will run independent review, PR, squash merge, issue evidence comment, closure, and cleanup.

## Data Model / Audit Row Shape

Each audit row should include:

- surface/command name and aliases;
- realm/category (`Chaos Sea`, `Shining Abode`, `Afterlife profiles`, `Spiritual conflict`, etc.);
- current console detail path;
- current browser detail path;
- classification: `adequate`, `fixed in #949`, `follow-up required`, or `not applicable`;
- severity: `P0 blocker`, `P1 high`, `P2 medium`, or `P3 low`;
- follow-up issue URL/number when classification is `follow-up required`;
- docs/contract impact: `none`, `docs follow-up`, or named files/tests if changed.

## Risk Controls

- Do not remove or replace existing overview outputs.
- Do not add React-only gameplay/selection rules.
- Do not mutate state or pending/control files.
- Do not change afterlife runtime contracts without same-PR docs/tests.
- Do not close #949 with unlinked confirmed gaps.
- Do not wait for GitHub Actions; use local gates.

## Verification Commands

Baseline and post-change commands should include real non-zero counts:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Shining|FullyQualifiedName~Chaos|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"

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

- PR body uses `Closes #949` only for the source issue.
- PR lists created follow-up issues as non-closing references unless a follow-up is also fully satisfied.
- Issue evidence comment includes audit artifact path, follow-up links, verification commands/counts, independent review verdict, docs impact, and `GitHub Actions: not used/not required`.
