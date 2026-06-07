# Tasks: Map and Locations Command Semantics

**Input**: Design documents from `/specs/880-881-map-locations/`

**Prerequisites**: `spec.md`, `plan.md`, GitHub issues #880 and #881, constitution 1.1.0, `AGENTS.md`, `references/map-locations-command-semantics.md`.

**Tests**: Behavior changes require strict TDD: write focused failing C# and frontend/source-guard tests before production code, run them to confirm RED, then implement minimal GREEN and rerun affected suites.

**Organization**: Tasks are grouped by independently testable user stories. Codex may combine adjacent tasks in one commit only when RED/GREEN evidence remains explicit.

## Phase 1: Setup and Baseline

**Purpose**: Establish the active issues, source-of-truth artifacts, code paths, and clean baseline.

- [x] T001 Confirm worktree `E:/Games/worktrees/boe-880-881-map-locations`, branch `fix/880-881-map-locations`, source issues #880/#881, and `git status --short --branch`.
- [x] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, `references/map-locations-command-semantics.md`, `specs/880-881-map-locations/spec.md`, `plan.md`, and this file before implementation.
- [x] T003 Inspect current code paths: `ExplorerMode.WorldAndStatus.cs::ShowMap`, `ExplorerMode.MetaLoreAndTravel.cs::ShowLocations`, `ExplorerMortalWorldCommandResultBuilder.BuildMap` and `CommandKind.Locations`, `LocalMapViewService`, `LocalMapViewerLauncher`, `CommandResult.tsx`, `BlockRenderer.tsx`, and existing map/location tests.
- [x] T004 Record baseline verification before production edits: focused C# filter, frontend verify (after `npm ci` if needed), and current RED failures for added tests/source guards.

---

## Phase 2: RED Tests and Guards

**Purpose**: Capture the reported swapped/no-op behavior before changing implementation.

- [x] T005 [P] [US1] Add a RED C# regression/source-guard proving Mortal console `/карта` uses the visual map launcher path and no longer owns the current/adjacent/discovered location selector.
- [x] T006 [P] [US2] Add a RED C# regression/source-guard proving console `/локации` / `/locations` owns the current/adjacent/discovered/updated location list/details flow.
- [x] T007 [P] [US3] Add a RED frontend test/source guard proving `UiMapBlock` rendering is not text-only (`карта содержит N точек`) and creates a visual map surface.
- [x] T008 [P] [US4] Add RED C# browser command-result tests proving `/локации` unwraps `worldMapUpdates.newLocations[]` / `locationUpdates[]` and root-level `newLocations[]` / `locationUpdates[]`.
- [x] T009 [P] [US4] Add RED C# browser command-result tests proving `/локации` includes current location and adjacent locations from `current_location.json`.
- [x] T010 [P] [Cross] Add alias coverage for Russian and English command names: `/карта`, `/map`, `/локации`, `/locations`.

**Checkpoint**: RED tests fail for the expected reported reasons, not because of typos or fixture setup.

---

## Phase 3: Console Command Semantics

**Purpose**: Make console `/карта` visual and `/локации` list/details.

- [x] T011 [US1] Refactor `ExplorerMode.WorldAndStatus.cs::ShowMap` so Mortal World uses `LocalMapViewService.BuildCurrentRealmMapAsync`, `ExplorerCommandResultConsoleRenderer`, and `LocalMapViewerLauncher.WriteAndOpenAsync`, matching the existing afterlife visual-map path.
- [x] T012 [US2] Move or share the old Mortal location selector/list implementation so it is reached from console `/локации` / `/locations`, not `/карта`.
- [x] T013 [US2] Ensure the location list unwraps `worldMapUpdates` and preserves root-level world-map update support.
- [x] T014 [Cross] Escape dynamic location names, descriptions, directions, distances, link states, and map paths before Spectre.Console markup rendering.
- [x] T015 [US1/US2] Run the RED tests from T005-T006/T010 and confirm they now pass.

**Checkpoint**: Console command meanings are separated and covered.

---

## Phase 4: Browser `/локации` Location Result

**Purpose**: Make browser `/локации` meaningful instead of a generic empty/root-only bundle.

- [x] T016 [US4] Replace `CommandKind.Locations` generic `BuildBundle` behavior with a dedicated player-facing result builder for current, adjacent, discovered, and updated locations.
- [x] T017 [US4] Support both `worldMapUpdates.newLocations[]` / `worldMapUpdates.locationUpdates[]` and root-level `newLocations[]` / `locationUpdates[]`, avoiding duplicate rows when the same stable id/name appears in both shapes.
- [x] T018 [US4] Include graceful player-facing empty-state copy when current/world map data is missing or empty.
- [x] T019 [US4] Keep raw JSON/advanced details behind existing advanced behavior; default browser blocks must be meaningful without raw JSON.
- [x] T020 [US4] Run the RED tests from T008-T010 and confirm they now pass.

**Checkpoint**: Browser `/локации` displays player-facing location rows/details for wrapped and root-level shapes.

---

## Phase 5: Browser Visual Map Rendering

**Purpose**: Render `UiMapBlock` as a real visual map in browser command results.

- [x] T021 [US3] Add a browser map component or shared renderer integration that consumes the existing map DTO and renders an SVG/visual map surface with nodes and links.
- [x] T022 [US3] Use player-facing labels and accessible structure; avoid raw JSON, DTO, API, or debug terminology in default map UI.
- [x] T023 [US3] Support Mortal World, Chaos Sea, and Shining Abode map DTOs without realm-specific crashes.
- [x] T024 [US3] Replace text-only `UiMapBlock` rendering in `CommandResult.tsx` and any separate `BlockRenderer.tsx` map path.
- [x] T025 [US3] Run the RED frontend/source-guard test from T007 and confirm it now passes.
- [x] T026 [US3] Produce a Browser Client screenshot or deterministic HTML visual smoke artifact for `/карта` under `TestResults/` and record the path for PR/issue evidence.

**Checkpoint**: Browser map rendering has both automated guard coverage and visual evidence.

---

## Phase 6: Docs/Prompt Reconciliation

**Purpose**: Keep documented player-facing command behavior synchronized.

- [x] T027 Inspect `docs/web-ui/local-web-host.md`, command help/catalog docs/tests, and player-facing command metadata for `/карта` and `/локации` semantics.
- [x] T028 Update docs/tests only if implementation changes documented command behavior or if current docs falsely claim visual browser map rendering that is now implemented differently.
- [x] T029 If docs change, run relevant docs/source-guard tests; if docs do not change, record the no-docs/no-GM-contract-drift rationale in this tasks file, PR body, and final report.

**Checkpoint**: Documentation impact is explicit.

---

## Phase 7: Final Verification, Review, PR, Merge

**Purpose**: Verify, review, merge, close #880 and #881.

- [x] T030 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` after restore/build artifacts exist.
- [x] T031 Run `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal` when C# tests are touched.
- [x] T032 Run focused C# map/location/browser command-result verification and record exact pass/fail/skip counts.
- [x] T033 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record typecheck/test/build evidence.
- [x] T034 Run visual screenshot/smoke verification and record the artifact path with the statement required by #880: `Verified visually in Browser Client screenshot: /карта renders the visual map` or an explicit equivalent visual artifact if screenshot automation is not available.
- [x] T035 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm `FEATURE_DIR` resolves to `specs/880-881-map-locations`.
- [x] T036 Run `git diff --check origin/main...HEAD`.
- [x] T037 Run added-line static scan excluding plan docs and inspect any token/secret/shell/eval/pickle/SQL matches manually.
- [x] T038 Reconcile `spec.md`, `plan.md`, and this `tasks.md` against the final diff. Do not mark tasks complete unless code/tests/docs and verification evidence exist.
- [x] T039 Obtain independent review before PR/merge. Critical/Important findings must be fixed and re-reviewed before merging.
- [ ] T040 Create/update PR closing #880 and #881 with local verification evidence, visual artifact path, and `GitHub Actions: not required`; squash-merge with `[skip ci]` after local-gated approval.
- [ ] T041 Verify PR is `MERGED`, issues #880 and #881 are `CLOSED`/`COMPLETED`, `main` fast-forwards, focused post-merge check passes, and the issue worktree/branch are cleaned up.

## Completion Evidence

- T001-T003 context confirmed: worktree `E:/Games/worktrees/boe-880-881-map-locations`, branch `fix/880-881-map-locations`, GitHub issues #880/#881, `AGENTS.md`, constitution, spec, plan, and tasks were read before edits. `references/map-locations-command-semantics.md` was listed by this task file but was not present in the repository; no conflict with the issue text or Spec Kit artifacts was found.
- T004-T010 RED evidence:
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~ExecuteAsync_Locations" --logger "console;verbosity=minimal"`: 42 total, 37 passed, 5 failed for the expected pre-fix map/location ownership and wrapped-update gaps.
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~TryProcessCommand_MapRussian_InMortalRealm_OpensVisualMapViewerInsteadOfLocationList" --logger "console;verbosity=minimal"`: 1 total, 0 passed, 1 failed because `output/map_viewer.html` was not produced.
  - `npm exec --prefix BookOfEternityClient.WebFrontend -- vitest run test/playerCopyRobustness.test.ts`: 22 total, 21 passed, 1 failed because browser map rendering was still text-only.
- Review-regression RED evidence:
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerMode_LocationDetailPanel_MustUseSharedLocationNameFallbacks" --logger "console;verbosity=minimal"`: 1 total, 0 passed, 1 failed because location details did not use the shared location-name fallback chain.
  - `npm exec --prefix BookOfEternityClient.WebFrontend -- vitest run test/playerCopyRobustness.test.ts -t "resets browser map selection controls"`: 1 failed, 22 skipped because `MapBlock` did not reset layer/z/node selection when a different map block was reused.
- T011-T025 GREEN evidence:
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~ExecuteAsync_Locations|FullyQualifiedName~TryProcessCommand_MapRussian_InMortalRealm_OpensVisualMapViewerInsteadOfLocationList" --logger "console;verbosity=minimal"`: 43 total, 43 passed, 0 failed, 0 skipped.
  - `npm exec --prefix BookOfEternityClient.WebFrontend -- vitest run test/playerCopyRobustness.test.ts`: 22 total, 22 passed, 0 failed.
- Review-regression GREEN evidence:
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerMode_LocationDetailPanel_MustUseSharedLocationNameFallbacks" --logger "console;verbosity=minimal"`: 1 total, 1 passed, 0 failed, 0 skipped.
  - `npm exec --prefix BookOfEternityClient.WebFrontend -- vitest run test/playerCopyRobustness.test.ts -t "resets browser map selection controls"`: 1 passed, 22 skipped.
- T026/T034 visual evidence:
  - Codex-generated deterministic HTML visual smoke artifact: `TestResults/browser-smoke/map-command-visual-artifact.html`.
  - Hermes reconciliation launched the real Browser Client at `http://127.0.0.1:8793` against disposable session root `E:/Games/test-sessions/boe-880-881-browser-visual`, executed `/карта` through the React Shell command flow, and captured a real screenshot at `TestResults/browser-screenshots/issue-880-map-command-browser-client.png` (copied from Hermes screenshot cache `C:/Users/Ёж/AppData/Local/hermes/cache/screenshots/browser_screenshot_952ad718e28841ce8f3985aa7b433c77.png`). Verified visually in Browser Client screenshot: `/карта` renders the visual map, with map controls, parchment map surface, and node markers visible.
  - The same Browser Client session executed `/локации` through the React Shell command flow and rendered a location table with current and nearby locations, not the visual map.
- T027-T029 docs impact: updated `docs/web-ui/browser-parity-checklist.md` and `docs/web-ui/local-web-host.md` for `/карта` visual map and `/локации` location-list ownership. No GM prompt/example update was required because no GM-authored command contract, pending/control file, validation rule, normalizer side effect, lifecycle, receipt, report, or afterlife authority path changed.
- T030-T038 verification evidence:
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Map|FullyQualifiedName~Location|FullyQualifiedName~ExplorerWebCommandService|FullyQualifiedName~LocalMapViewerService|FullyQualifiedName~CommandResult" --logger "console;verbosity=minimal"`: 167 total, 167 passed, 0 failed, 0 skipped.
  - `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore --verbosity:minimal`: succeeded with 0 warnings and 0 errors.
  - `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal`: succeeded with 0 warnings and 0 errors.
  - `npm run verify --prefix BookOfEternityClient.WebFrontend`: TypeScript typecheck passed, Vitest 29/29 passed, Vite build succeeded.
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"`: 12 total, 12 passed, 0 failed, 0 skipped.
  - `pwsh -NoProfile -File .specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`: `FEATURE_DIR=E:\Games\worktrees\boe-880-881-map-locations\specs\880-881-map-locations`, `AVAILABLE_DOCS=["tasks.md"]`.
  - `git diff --check origin/main...HEAD` and `git diff --check`: no whitespace errors; Git reported expected CRLF conversion warnings on modified text files.
  - Added-line static risk scan excluding plan docs: no token/secret/shell/eval/pickle/SQL matches.
- T039 independent review: read-only subagent review found one Critical packaging issue and two Important behavior risks. Follow-up RED/GREEN guards now cover map state reset and shared console location detail naming. Re-review confirmed both behavior risks resolved; the remaining packaging issue is resolved by including `BookOfEternityClient.WebFrontend/src/components/MapBlock.tsx` in the implementation commit. Hermes reconciliation independent review then found an additional Important map reset edge case: a new map result with the same default layer/z/current node could preserve a stale user-selected node because `BlockRenderer` reuses `map-0` keys. Added RED guard requiring `mapResetKey`, fixed `MapBlock` to include the incoming map object identity in the reset effect dependencies, and reran: `npm exec --prefix BookOfEternityClient.WebFrontend -- vitest run test/playerCopyRobustness.test.ts -t "resets browser map selection controls"` (1 passed, 22 skipped), `npm run verify --prefix BookOfEternityClient.WebFrontend` (typecheck, Vitest 29/29, build), focused C# map/location slice (167/167), and `git diff --check`. Focused re-review `E:/Games/codex-runs/20260607-1047-boe-880-881-map-locations-rereview` approved the fix with no Critical/Important/Minor findings.
- T040-T041 intentionally remain unchecked for Hermes-owned PR creation, merge, issue closure, post-merge verification, and cleanup.

## Dependencies & Execution Order

- Phase 1 must complete before edits.
- Phase 2 RED tests/guards must be written and observed failing before Phase 3/4/5 production implementation.
- Phase 3 and Phase 4 are coupled by the location-list helper/shape handling.
- Phase 5 can proceed after map DTO shape is understood; it must not wait until after PR for visual evidence.
- Phase 7 requires independent review and visual evidence before PR merge/closure.

## Parallel Opportunities

- T005-T010 RED tests can be drafted in parallel if they touch different test files.
- T016-T020 and T021-T025 may be implemented in parallel only if they do not edit the same frontend files; otherwise serialize to avoid conflicting map/location renderer changes.

## Notes

- Keep #882 console column helper and #879 HUD alignment out of this PR unless a tiny helper is directly necessary for map/location output and independently verified.
- Do not close Browser Client umbrella #680 or other roadmap issues from this PR.
- Do not introduce remote map services or web dependencies beyond the existing local frontend stack.
- If real browser screenshot automation is blocked, produce a deterministic visual smoke HTML artifact and record the limitation; do not claim a screenshot unless an actual screenshot exists.
