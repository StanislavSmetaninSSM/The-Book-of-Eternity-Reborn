# Tasks: Browser imagegen asset catalog and generated visuals

**Tracked issue**: [#929](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/929)
**Parent epic**: [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680)

## Phase 1: Setup and baseline

- [x] T001 Create isolated ASCII worktree `E:/Games/worktrees/boe-929-browser-imagegen-assets` on branch `work/929-browser-imagegen-assets` from `origin/main`.
- [x] T002 Create Spec Kit artifacts under `specs/929-browser-imagegen-assets/` linked to #929 and #680.
- [x] T003 Run Spec Kit prerequisite/discoverability check and record evidence.
- [x] T004 Run frontend baseline `npm run verify --prefix BookOfEternityClient.WebFrontend` and record counts.
- [x] T005 Run focused C# browser/local web smoke/source guard baseline and record counts.

## Phase 2: RED / guard coverage

- [x] T006 Add or extend focused source guards for catalog/provenance/local-runtime asset behavior before production wiring.
- [x] T007 Capture RED or mutation evidence for the new guard(s).

## Phase 3: Implementation

- [x] T008 Add asset catalog/provenance documentation for generated/development assets.
- [x] T009 Add first local generated/development asset batch under a repository-local frontend asset path.
- [x] T010 Wire assets into Browser UI surfaces with responsive crop, readability overlay, and fallback behavior.
- [x] T011 Add/update local visual smoke artifact(s), clearly labelled as local/offline and not automated screenshots if screenshot automation is not used.

## Phase 4: Verification

- [x] T012 Run frontend verify and record exact typecheck/test/build output.
- [x] T013 Run focused C# browser/local web smoke/source guards and record exact counts.
- [x] T014 Run `git diff --check origin/main...HEAD`.
- [x] T015 Run added-line static/security scan over changed non-Spec/non-doc code.
- [x] T016 Verify default Browser UI remains player-facing and exposes no raw API/DTO/JSON/debug/imagegen/Codex/file-path/agent meta-language.
- [x] T017 Verify console behavior, game runtime contracts, GM docs/examples, and dynamic scene/entity image authority are unchanged unless explicitly tracked.

## Phase 5: Hermes-owned orchestration

- [x] T018 Independent review of implementation against #929/spec acceptance.
- [x] T019 Create PR with local verification evidence and safe closing reference to #929 only.
- [ ] T020 Squash-merge PR after local gates and review approve; do not wait for GitHub Actions unless explicitly requested.
- [ ] T021 Verify PR merged and #929 closed; update labels if needed.
- [ ] T022 Clean up issue worktree/branch when safe.
- [ ] T023 Send Russian closure report naming local verification and next target.

## Evidence log

- T001: Worktree created from `origin/main` at `7f528bdc683180564e8bbfaf8f4276e319c539b3` on branch `work/929-browser-imagegen-assets`.
- T002: Hermes created `spec.md`, `plan.md`, `tasks.md`, and `contracts/browser-imagegen-assets.md` before implementation because #929 is broad player-facing Browser Client visual-asset work requiring durable Spec Kit handoff.
- T003: Spec Kit prerequisite command `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR=E:\\Games\\worktrees\\boe-929-browser-imagegen-assets\\specs\\929-browser-imagegen-assets` with `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
- T004: After `npm ci --prefix BookOfEternityClient.WebFrontend` (52 packages; existing 1 high severity npm audit advisory), baseline `npm run verify --prefix BookOfEternityClient.WebFrontend` passed: TypeScript app/node typecheck passed; player-facing compiled guards passed; Vitest reported 11 files / 77 tests passed; Vite build transformed 51 modules.
- T005: Fresh-worktree `dotnet test --no-restore` first failed because NuGet `project.assets.json` was absent; rerun without `--no-restore` restored packages and the focused C# browser/local web guard filter passed: 31 passed / 0 failed / 0 skipped / 31 total.
- T006: Added focused guards in `BrowserFrontendWorkspaceTests.BrowserImagegenAssetCatalog_HasTrackedLocalAssetsAndFallbackWiring` and `LocalWebUiDocumentationTests.LocalWebHostDocs_DocumentBrowserImagegenAssetCatalogWorkflow`, plus built-frontend smoke assertions for serving `/browser-ui-assets/*.png` and generating `browser-imagegen-assets.html`.
- T007: RED evidence captured before implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserImagegenAssetCatalog_HasTrackedLocalAssetsAndFallbackWiring" --logger "console;verbosity=minimal"` failed with missing `BookOfEternityClient.WebFrontend/src/browserUiAssets.ts`; documentation RED then failed with missing `#929` docs coverage.
- T008: Added `BookOfEternityClient.WebFrontend/public/browser-ui-assets/catalog.md` with asset ids, target surfaces, paths, aspect ratios, prompt/art direction, deterministic provenance, usage constraints, runtime boundary, and fallback behavior.
- T009: Added local deterministic generated/development PNG assets under `BookOfEternityClient.WebFrontend/public/browser-ui-assets/`: `scene-hero-fallback.png` 110,752 bytes, `gallery-empty-archive.png` 117,027 bytes, and `status-soul-vignette.png` 204,661 bytes.
- T010: Added `src/browserUiAssets.ts`; wired scene hero fallback into `SceneView`/`SceneHero`, gallery/image fallback into `BlockRenderer`, and status ambient art into `StatusView` with CSS overlay/readability rules. `useSceneImage` and dynamic media generation/service behavior were not changed.
- T011: Updated `LocalWebUiBuiltFrontendSmokeTests` to write `TestResults/browser-smoke/browser-imagegen-assets.html`, explicitly labelled as a dependency-light local visual-smoke artifact and not an automated screenshot, with desktop/mobile scene, gallery, and status frames.
- T012: `npm run verify --prefix BookOfEternityClient.WebFrontend` passed: app/node typecheck passed; player-facing tests passed; Vitest reported 11 files / 77 tests passed; Vite build transformed 52 modules and produced `dist/index.html`, CSS, and JS bundles.
- T013: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"` passed: 33 passed / 0 failed / 0 skipped / 33 total.
- T014: Pre-commit `git diff --check` passed with exit code 0 and only line-ending normalization warnings from Git; final `git diff --check origin/main...HEAD` is part of the post-commit verification report.
- T015: Added-line static/security scan over changed `BookOfEternityClient.WebFrontend/src` code passed: no added hits for remote URLs, `fetch`, `dangerouslySetInnerHTML`, storage access, API-key terms, Pollinations, Codex, or imagegen.
- T016: Added-line default player-facing copy/meta scan over changed Browser UI components passed: no added hits for Codex, imagegen, Pollinations, Spec Kit, agent, DTO, raw JSON, `/api/`, endpoint, debug, file path, or asset-path wording.
- T017: Runtime/console/GM boundary scan passed: no C# runtime, console, GM prompt/example, or game-contract files changed; dynamic `ImageService`, `game_session/images`, and console behavior were not modified.
- Hermes reconciliation after Codex completion: Codex run `E:/Games/codex-runs/20260617-113604-boe-929-browser-imagegen-assets` exited `0` with `final.md`; branch `work/929-browser-imagegen-assets` was clean/ahead 1 at `20e2a10`, no PR existed, and no run artifacts were in the repo diff.
- Hermes fresh gates after Codex completion: Spec Kit prerequisite resolved this feature dir; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed (11 files / 77 Vitest tests, Vite 52 modules); focused C# browser/local-web guard filter passed (33/33); client and test-project builds completed with 0 warnings / 0 errors; `git diff --check origin/main...HEAD` was clean; refined production runtime/default UI scans returned `NO_MATCHES` after classifying broad matches as test guard/provenance-doc-only.
- T018: Independent detached Codex review `E:/Games/codex-runs/20260617-120630-review-boe-929-browser-imagegen-assets` returned `VERDICT: APPROVED` with no Critical/Important/Minor findings. Reviewer verified diff-check, production runtime/default UI scan, PNG dimensions, no embedded generator/tool/URL strings, and visually inspected all three PNGs for no text/watermark/logo/high-contrast clutter.
- T019: PR #1077 was created for `work/929-browser-imagegen-assets`; GitHub readback reported `state=OPEN`, `mergeStateStatus=CLEAN`, empty status-check rollup, and closing reference only #929. PR body uses `Closes #929` as the only closing keyword; #680 appears only as a non-closing reference.