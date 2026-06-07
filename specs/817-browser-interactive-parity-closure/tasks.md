# Tasks: Browser Interactive Parity Epic Closure (#817)

**Source issue:** https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817

## T001 — Verify parent and child issue state

- [x] Confirm #817 is open before the closure pass.
- [x] Confirm child issues #801, #802, #803, #804, #805, #806, #807, #808, #809, #810, #811, #812, #813, #814, #815, and #816 are `CLOSED` / `COMPLETED` on GitHub.
- [x] Confirm `main` is fast-forwarded to `origin/main` before implementation.

Evidence from Hermes preflight: #801-#816 are all `CLOSED` / `COMPLETED`; #817 is `OPEN`; `main` and `origin/main` both point to `2e4b39560fdefecddaa28a611e05c1871385764e`.

## T002 — Add RED parent-closure coverage guard

- [x] Add a focused test in `BookOfEternityClient.Tests/BrowserApiContractTests.cs` that asserts command coverage contains no `FollowUpIssue`, `Reason`, `GapSummary`, or `ParityNotes` references to `#817`, `remaining umbrella`, or `remaining interactive scope` for commands and subcommands.
- [x] Run the focused test and record the RED failure caused by existing stale #817 coverage overrides.

Evidence:

- Environment note: the first no-restore RED attempt failed before test execution because `BookOfEternityClient.Tests/obj/project.assets.json` was absent in the fresh worktree. `dotnet restore BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true` restored the test/client projects.
- RED command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserCommandCoverageContract_DoesNotExposeIssue817ParentClosureFollowUps" --logger "console;verbosity=minimal"`.
- RED result: expected failure, 1 failed / 0 passed / 0 skipped / 1 total. Failure showed coverage entries still contained `#817`, `remaining umbrella`, and `remaining interactive scope` in parent coverage metadata.

## T003 — Remove stale #817 coverage overrides and update child assertions

- [x] Update `BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs` so already-covered commands no longer report #817 as `tracked-follow-up`.
- [x] Update child parity tests that currently assert `Assert.Contains("#817", ...)` or `UmbrellaRemainsOpen` so they assert no stale parent follow-up remains while preserving child-specific coverage checks.
- [x] Regenerate/update tracked generated browser coverage fixtures if the repository tracks command coverage JSON changes.

Evidence:

- Removed the parent audit override table from `BrowserCommandCoverageService.cs`; covered command descriptors now use default `covered` audit metadata.
- Updated child parity tests for #808, #812, #813, #814, #815, #816, and inventory cross-checks to reject stale `#817` follow-up metadata while keeping child issue assertions.
- Regenerated tracked fixture `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/command-coverage.json` from the C# `BrowserCommandCoverageService.Build()` DTO.

## T004 — GREEN verification

- [x] Rerun the focused parent/coverage tests and record non-zero pass counts.
- [x] Run the #817 parent closure C# parity filter and record pass/fail/skip/total counts.
- [x] Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record TypeScript/player-facing/Vitest/build results.
- [x] Run `git diff --check origin/main...HEAD` and the added-line static scan excluding Spec Kit docs.

Evidence:

- Focused GREEN command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserCommandCoverageContract_DoesNotExposeIssue817ParentClosureFollowUps|FullyQualifiedName~BrowserCommandCoverage_Issue|FullyQualifiedName~FrontendContractFixtures_MatchRepresentativeCSharpDtos" --logger "console;verbosity=minimal"`.
- Focused GREEN result: passed, 0 failed / 23 passed / 0 skipped / 23 total.
- `git diff --check origin/main...HEAD`: passed, exit 0, no output.
- Parent closure C# parity filter: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserCommandCoverageServiceTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~BrowserAfterlifeWriteService|FullyQualifiedName~BrowserPlayerCommandMenuBuilderTests|FullyQualifiedName~BrowserInventoryManagementTests|FullyQualifiedName~BrowserInkFeatherFateParityTests|FullyQualifiedName~BrowserAfterlifeArchiveParityTests|FullyQualifiedName~BrowserStorageTransportParityTests|FullyQualifiedName~BrowserShiningRelicForgeParityTests|FullyQualifiedName~BrowserShiningIncarnationGatesParityTests|FullyQualifiedName~BrowserShiningActionsParityTests|FullyQualifiedName~BrowserShiningPoliticsParityTests|FullyQualifiedName~BrowserResidentInteractionsParityTests|FullyQualifiedName~BrowserGuardianSocialParityTests|FullyQualifiedName~BrowserTradeParityTests|FullyQualifiedName~BrowserNpcSocialParityTests" --logger "console;verbosity=minimal"` passed, 0 failed / 238 passed / 0 skipped / 238 total.
- `npm ci --prefix BookOfEternityClient.WebFrontend`: installed 54 packages, audited 55 packages, 0 vulnerabilities because `node_modules` was absent.
- `npm run verify --prefix BookOfEternityClient.WebFrontend`: typecheck passed; player-facing command/layout checks passed; Vitest 2 files / 29 tests passed; production build succeeded.
- Added-line static security scan excluding `specs/**`: `NO_MATCHES`.

## T005 — Review, PR, merge, close

- [ ] Obtain independent review against #817 acceptance criteria and the diff.
- [ ] Create a PR linked to #817 with local verification evidence and `[skip ci]` in the title/commit message where appropriate.
- [ ] Squash-merge after local gates and review pass.
- [ ] Post a #817 closure comment with child issue summary, verification, review, Spec Kit, docs/prompts impact, and GitHub Actions policy.
- [ ] Verify #817 is closed as `completed`.
