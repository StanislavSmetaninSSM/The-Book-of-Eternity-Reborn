# Implementation Plan: Browser Inventory and Document Detail Paths

**Branch**: `work/1089-browser-inventory-details` | **Date**: 2026-06-19 | **Spec**: `specs/1089-browser-inventory-document-details/spec.md`

**Source issue**: [#1089](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1089)

## Summary

Add read-only Browser Client detail paths for inventory items and readable documents/books. C# remains the command/result authority; React renders existing `ExplorerCommandResult` blocks/actions and submits commands through existing shell APIs.

## Technical Context

**Language/Version**: C#/.NET 8, React + TypeScript + Vite
**Primary Dependencies**: xUnit, `System.Text.Json`, existing browser DTO/action rendering, Vite/Vitest
**Storage**: Existing file-backed `game_state/inventory/*` and readable-document sidecars; no new runtime storage contract intended
**Testing**: C# xUnit filters, frontend `npm run verify`, source guards, optional dependency-light visual smoke artifact
**Platform**: Local Windows desktop client with browser frontend
**Project Type**: Hybrid C# client + local web frontend
**Performance Goals**: Detail rendering stays local/read-only and proportional to selected item/document, not full inventory dumps
**Constraints**: Preserve Russian player-facing default UI; no raw JSON/paths/API/DTO/debug wording in default mode; no React-side gameplay rules
**Scale/Scope**: One Browser UX parity slice for item and document/book details

## Constitution Check

- **GitHub Issue Traceability**: Satisfied by #1089 links in spec/plan/tasks.
- **Player-Facing Game Client Integrity**: Default browser output must be Russian/in-world and no raw diagnostics.
- **Contract and State Authority**: Reuse existing item/readable-document authority. If schema/validation/prompt contract changes become necessary, update GM-facing docs or create a tracked follow-up before closure.
- **Test-First Verification**: Codex must add RED tests/source guards before production changes.
- **Agent Orchestration Discipline**: Hermes launches Codex with this spec/plan/tasks; Hermes owns review/PR/merge/issue closure.

## File/Component Map

Expected areas for Codex to inspect and modify as needed:

- `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` — browser `/инв` and `/книги` command-result projections.
- `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs` — existing prompt/action patterns for inventory commands; inspect only unless reusable read-only helpers belong here.
- `BookOfEternityClient/Services/ReadableInventoryDocumentAuthority.cs` — existing readable-document source of truth; reuse, do not duplicate.
- `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs` — command aliases/action metadata if selected detail command descriptors are missing.
- `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs` — primary C# browser command-result regression tests.
- `BookOfEternityClient.Tests/ExplorerModeCommandTests.GeneralPanels.cs` — console parity/readable-document guard if shared behavior changes.
- `BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs` or browser source guards — raw-output/action routing guard coverage.
- `BookOfEternityClient.WebFrontend/src/components/CommandResultView.tsx` and `BlockRenderer.tsx` — render command blocks/actions; keep `executeCommand`/prompt-session routing.
- `BookOfEternityClient.WebFrontend/test/*` — frontend guards if React rendering changes.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/*` — update only if backend browser contract fixtures intentionally change.
- `specs/1089-browser-inventory-document-details/*` — keep evidence in sync.

## Data Flow

1. Player enters `/инв` or `/книги` through the Browser Client composer/action surface.
2. C# command service builds an `ExplorerCommandResult` with safe player-facing blocks and actions.
3. Summary actions execute selected-detail commands through the same browser command execution path.
4. Detail command resolves canonical item/readable-document authority and renders safe blocks for exactly the selected item/document.
5. React displays blocks/actions through `CommandResultView`/`BlockRenderer`; it does not compute inventory or document semantics.

## Phase 0: Baseline and Handoff

- Confirm worktree `E:/Games/worktrees/boe-1089-browser-inventory-details` on branch `work/1089-browser-inventory-details` is clean except this Spec Kit setup.
- Run focused baseline before implementation and record exact counts in `tasks.md`.
- If `node_modules/` is absent and frontend changes are expected, run `npm ci --prefix BookOfEternityClient.WebFrontend` before frontend verification.

## Phase 1: RED Tests / Source Guards

- Add C# tests that fail on current `main` for:
  - `/инв` summary exposes detail action for a seeded item.
  - selected item detail renders description, bonuses/effects, structured bonus metadata, combat effect, and properties using Russian labels.
  - `/книги` summary exposes shelf/list actions and selected document detail renders only one document's content.
  - default output excludes raw JSON, `game_state/`, `.json`, DTO/API/protocol/debug/spec wording.
- Add frontend/source guard tests only if React rendering/action routing needs changes.

## Phase 2: Implementation

- Prefer shared C# helper/projection methods for item detail and document detail blocks.
- Keep selected-detail commands read-only.
- Preserve existing console/browser command aliases; add descriptors only when missing for browser actions.
- Keep advanced diagnostics behind existing advanced mode; do not use CSS hiding as the only protection.
- Avoid broad inventory mutation work; link follow-up issues for drop/split/merge/storage/transport if discovered.

## Phase 3: Verification and Review

- Run focused C# tests with non-zero counts.
- Run broader browser/readable/inventory filter.
- Run frontend verify if any frontend/fixtures/contracts changed.
- Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity minimal` after restore/build prerequisites are available.
- Run `git diff --check origin/main...HEAD`.
- Run added-line static/security and raw-player-copy scans over production/test diff, excluding Spec Kit docs for false-positive recipes.
- Preserve visual smoke evidence when UI rendering changes materially.
- Hermes performs independent review before PR/merge.

## Documentation Impact

Expected: no GM prompt/example update if the implementation only changes browser presentation over existing authority. If Codex changes supported item/document schema, validation rules, or GM-authored fields, update GM-facing docs/examples/tests or create a tracked follow-up before closure.

## Risks

- The issue overlaps older inventory management parity work; avoid mutating inventory state in this read-only detail slice.
- Readable-document authority touches Mortal World GM-authored content; schema/validation changes require documentation follow-through.
- Active PR #1116 changes many WebFrontend files but is unrelated and must not be used as the base or dependency for this issue.
