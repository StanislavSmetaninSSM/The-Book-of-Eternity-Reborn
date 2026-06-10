# Tasks: Browser QTE Interactive Mini-Games

**Input**: `specs/918-browser-qte-parity/spec.md`, `specs/918-browser-qte-parity/plan.md`, issue [#918](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/918)  
**Source Issues**: #918, parent #680, QTE v2 parent #911  
**Branch**: `work/918-browser-qte-parity`

## Phase 1: Setup and RED coverage

- [x] **T001 Baseline verification before Spec Kit edits**  
  Evidence: `npm ci --prefix BookOfEternityClient.WebFrontend` completed with 0 vulnerabilities; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with Vitest player-facing slice 44/44 and Vite build success; `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 247/247.

- [ ] **T002 Add failing C# browser QTE config projection tests**  
  Add/adjust tests in `BookOfEternityClient.Tests/BrowserApiContractTests.cs` or a focused browser-QTE test file proving each supported check type projects enough config for the browser mini-game and unknown types are explicit. Run the focused test and record the expected RED failure before production changes.

- [ ] **T003 Add failing frontend default-player tests for no manual grade selector**  
  Add tests under `BookOfEternityClient.WebFrontend/test/` proving supported interactive QTE actions render a mini-game surface and do not show the manual outcome dropdown/quick grade buttons in default player UI. Include BranchChoice and unsupported-type cases. Run the focused tests and record RED failure.

## Phase 2: C# Browser API projection

- [ ] **T004 Extend `QteWebActionDto` with typed check config projection**  
  Modify `BookOfEternityClient/WebUi/QteWebInteractionService.cs` and C# DTO definitions to include read-only normalized config for TimingBar, PromptChain, BalanceMeter, ChargeRelease, MashInput, PatternMemory, RhythmPulse, PrecisionChoice, StealthNoise, LockPinSet, BranchChoice, and unsupported/future checks. Preserve existing endpoint/write semantics.

- [ ] **T005 Update TypeScript API contracts and fixtures**  
  Modify `BookOfEternityClient.WebFrontend/src/api/contracts.ts`, `src/api/contract-fixtures/qte-state.json`, and fixture checks so TypeScript sees the normalized config. Do not expose raw config/debug fields in default UI.

- [ ] **T006 Verify C# config projection GREEN**  
  Run the focused C# browser QTE contract test from T002 and record GREEN evidence.

## Phase 3: Frontend mini-game model and components

- [ ] **T007 Add pure mini-game grade helpers**  
  Create focused TypeScript helpers under `BookOfEternityClient.WebFrontend/src/qte/` or `src/components/qte/` that map deterministic mini-game input state to `success` / `partial` / `fail` for each supported check family. Reuse `qteKeyInput.ts` for key labels/layout normalization where relevant.

- [ ] **T008 Implement mini-game UI components**  
  Add small React components for TimingBar, PromptChain, BalanceMeter, ChargeRelease, MashInput, PatternMemory, RhythmPulse, PrecisionChoice, StealthNoise, LockPinSet, BranchChoice/static handling, and Unsupported. Components must use player-facing Russian copy, keyboard/pointer controls, clear instructions, and deterministic submit behavior.

- [ ] **T009 Refactor `QteScenePanel.tsx` to use mini-game components**  
  Remove the normal player-facing grade dropdown and quick grade buttons for supported checks. Submit computed grades through `browserApi.resolveQteAction({ actionId, grade })`. Keep C# as write authority.

- [ ] **T010 Verify frontend mini-game tests GREEN**  
  Run the focused frontend tests from T003 and helper tests from T007/T008. Record pass counts.

## Phase 4: Documentation, guards, and UX integrity

- [ ] **T011 Update docs/source guards only where behavior/API changed**  
  If browser QTE behavior/API guidance changes, update relevant docs such as `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, browser docs, and source guards. Do not add new GM-authored QTE fields.

- [ ] **T012 Add player-facing leak guard**  
  Add/adjust frontend source/component tests proving default Browser QTE UI does not show raw endpoint names, DTO/config JSON, file paths, or manual grade/debug language for supported checks.

- [ ] **T013 Add responsive/accessibility smoke evidence**  
  Add deterministic tests or a dependency-light `TestResults/browser-smoke/` artifact proving the QTE panel has keyboard/pointer controls and no obvious desktop/mobile overflow for representative checks. Do not claim screenshot evidence unless screenshots were actually captured.

## Phase 5: Verification and review

- [ ] **T014 Run full frontend verification**  
  Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record exact counts/results.

- [ ] **T015 Run C# build and focused QTE/browser/docs gate**  
  Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`, `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`, and `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --no-restore --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`.

- [ ] **T016 Run Spec Kit prerequisites and diff hygiene**  
  Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`, `git diff --check origin/main...HEAD`, and added-line static security scan. Record evidence.

- [ ] **T017 Update this tasks file with implementation evidence**  
  Check off completed implementation/verification tasks only after evidence exists. Leave Hermes-owned PR/merge/closure rows open until those steps happen.

- [ ] **T018 Independent pre-merge review**  
  Obtain an independent review against issue #918 acceptance, `origin/main...HEAD` diff, and Spec Kit artifacts. Fix Critical/Important findings and rerun focused verification/re-review.

## Phase 6: Hermes-owned PR, merge, and closure

- [ ] **T019 Create PR**  
  Push `work/918-browser-qte-parity`, create a PR to `main` that closes #918, and include local verification evidence plus `GitHub Actions: not used / not required`.

- [ ] **T020 Squash-merge and verify closure**  
  After local gates and independent review are clean, squash-merge, delete the remote branch, fast-forward main, verify PR `MERGED` and issue #918 `CLOSED`/`COMPLETED`, and run post-merge focused verification on `main`.

- [ ] **T021 Post issue evidence comment and cleanup**  
  Comment closure evidence on #918, remove the temporary worktree/local branch, prune stale branches, and report in Russian with next target selection rationale.
