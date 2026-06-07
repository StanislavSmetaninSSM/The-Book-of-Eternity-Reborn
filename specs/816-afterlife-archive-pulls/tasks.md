# Tasks: Browser Afterlife Archive Actions and Direct Pull

**Input**: GitHub issue [#816](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/816), umbrella [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817), [spec.md](spec.md), [plan.md](plan.md)
**Prerequisites**: Existing console archive/direct-pull flows, archive services, direct gacha write/queue service, constitution, repository `AGENTS.md`

## Phase 1: Setup & Spec Kit

- [x] T001 Confirm source branch `codex/816-afterlife-archive-pulls`, tracked worktree, and open GitHub issue #816.
- [x] T002 Inspect constitution, `AGENTS.md`, issue #816/#817 body, console archive/direct-pull source, existing archive services, direct gacha write/queue path, and browser local-turn/prompt patterns.
- [x] T003 Create #816 Spec Kit artifacts linked to issues #816/#817 and scoped to existing archive/direct-gacha contract reuse.
- [x] T004 Run focused baseline before implementation and record exact counts.

## Phase 2: RED Tests First

- [x] T005 Add browser parity test for opening archive consultation with an eligible archive entry and friendly Guardian choices.
- [x] T006 Add archive consultation submit test proving existing pending request shape, reserved archive entry, GM action tag/evidence, and Russian player-facing result copy.
- [x] T007 Add browser parity test for opening archive project fuel only when a friendly Guardian has an active project.
- [x] T008 Add archive project fuel submit test proving `targetProjectId`, existing pending request shape, reserved archive entry, and Russian player-facing result copy.
- [x] T009 Add guard coverage for cancellation/unconfirmed submit, no eligible Guardian, no active project, existing/malformed pending request, wrong realm, local write/GM-turn blockers, stale prompt entry/Guardian/project changes, and no-write failures.
- [x] T010 Audit direct gacha tests/coverage. If #816 direct-pull behavior is missing, add RED tests before fixes; if already complete, add evidence tests/coverage assertions proving it is supported.
- [x] T011 Add/update command coverage, command menu/help, API fixture, and source guard tests proving #816 actions are browser-supported and player-facing while #817 remains open.
- [x] T012 Run focused RED command and record expected failing tests before production implementation.

## Phase 3: Minimal Implementation

- [x] T013 Add #816 archive/direct-pull command descriptor(s), aliases, mutation mode, browser handler kind, and/or player-facing action metadata.
- [x] T014 Add prompt-session local UI lock coverage for mutating archive actions.
- [x] T015 Implement browser prompt builders for archive entry selection, Guardian/project selection, availability, blockers, and confirmation.
- [x] T016 Reuse or narrowly wrap existing C# archive services for request creation/commit and result shaping.
- [x] T017 Implement browser write handlers that re-read current state, re-check realm/entry/Guardian/project/pending-request eligibility, validate confirmation, serialize local writes, and update only existing `soul_state.json` plus existing archive pending request files.
- [x] T018 Audit/fix direct `/gacha` only where true #816 gaps remain; do not duplicate an already-working direct-gacha path or invent unsupported banners.
- [x] T019 Update browser command coverage/help/menu/game-screen/API fixtures so #816 archive/direct-pull parity is treated as covered while #817 remains open.
- [x] T020 Keep runtime contract shapes unchanged; if this becomes impossible, update contract matrix/examples/manifest/docs tests before continuing.

## Phase 4: GREEN & Verification

- [x] T021 Run focused RED/GREEN test filter and record exact result counts.
- [x] T022 Run final focused browser/API/afterlife parity sweep and record exact result counts.
- [x] T023 Run documentation-sensitive tests if any runtime contract/doc-impacting surface changes, or record why not required.
- [x] T024 Run C# build commands for touched projects and record results.
- [x] T025 Run `git diff --check origin/main...HEAD` and added-line security scan excluding Spec Kit docs; record results.
- [x] T026 Run frontend verification if frontend files or fixtures change; otherwise record why it was not run.
- [x] T027 Update this task list with completed task statuses and verification evidence.

## Phase 5: Commit & Handoff

- [x] T028 Inspect final `git diff --stat origin/main...HEAD` and `git status --short` for accidental run artifacts/caches.
- [x] T029 Create a local focused commit with `[skip ci]`.
- [ ] T030 Final Codex report includes summary, files changed, Spec Kit artifacts, exact verification results/counts, RED test evidence, commit SHA, remaining risks/blockers, and note that PR/merge/issue closure remain with Hermes.

## Baseline Evidence

- Created by Hermes on 2026-06-08 after #816 was reopened because PR #891 had accidentally auto-closed it with a negated closing phrase.
- Worktree: `E:/Games/worktrees/boe-816-archive-pulls`.
- Branch: `codex/816-afterlife-archive-pulls` tracking `origin/main` at `801fd0e`.
- Source issue: #816 open after reopen; umbrella #817 open.
- Focused baseline command before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "AfterlifeArchive|DirectGacha|Gacha|ExplorerWebPromptSession|BrowserAfterlifeWriteService|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`.
- Baseline result: passed, 0 failed / 452 passed / 0 skipped / 452 total. Restore/build ran first in the fresh worktree and produced test binaries normally.
- Frontend dependency setup: `npm ci --prefix BookOfEternityClient.WebFrontend`; result: added 54 packages, audited 55 packages, 0 vulnerabilities.
- Frontend baseline: `npm run verify --prefix BookOfEternityClient.WebFrontend`; result: typecheck passed, player-facing JS checks passed, Vitest 2 files / 29 tests passed, production build succeeded.

## RED Evidence

- Focused RED command before production implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserAfterlifeArchiveParity" --logger "console;verbosity=minimal"`.
- RED result: expected failure, 7 failed / 0 passed / 0 skipped / 7 total. Failures proved `/archive_consultation`, `/архивная_консультация`, `/archive_project_fuel`, `/архивная_подпитка_проекта`, and #816 command coverage were missing before implementation.
- Direct gacha audit before production changes: existing browser `/gacha` path already used `BrowserAfterlifeWriteService.ApplyGachaPullAsync`, `BrowserAfterlifeTurnRequestQueue.QueueDirectChaosSeaGachaAsync`, pending `gachaBaseResult`, rollback/snapshot staging, and `[CHAOS_SEA_DIRECT_GACHA]`; no duplicate direct-gacha implementation was added.

## GREEN & Verification Evidence

- Focused archive parity GREEN command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserAfterlifeArchiveParity" --logger "console;verbosity=minimal"`.
- Focused archive parity GREEN result: passed, 0 failed / 12 passed / 0 skipped / 12 total.
- Final browser/API/afterlife parity sweep: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "AfterlifeArchive|DirectGacha|Gacha|ExplorerWebPromptSession|BrowserAfterlifeWriteService|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`.
- Final browser/API/afterlife parity result: passed, 0 failed / 464 passed / 0 skipped / 464 total.
- Documentation-sensitive afterlife command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|AfterlifeContractRegistryTests" --logger "console;verbosity=minimal"`.
- Documentation-sensitive afterlife result: passed, 0 failed / 105 passed / 0 skipped / 105 total.
- Production build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal`; result: succeeded, 0 warnings / 0 errors.
- Test project build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal`; result: succeeded, 0 warnings / 0 errors.
- Frontend verification: `npm run verify --prefix BookOfEternityClient.WebFrontend`; result: typecheck passed, player-facing JS checks passed, Vitest 2 files / 29 tests passed, production build succeeded.
- Working diff whitespace check before commit: `git diff --check`; result: exit 0, no whitespace errors, CRLF normalization warnings only.
- Working added-line security scan before commit, excluding `specs/816-afterlife-archive-pulls/*`: result `NO_MATCHES`.
- Post-commit exact diff whitespace command: `git diff --check origin/main...HEAD`; result: exit 0, no whitespace errors.
- Post-commit exact added-line security scan command: `git diff --unified=0 origin/main...HEAD -- . ':(exclude)specs/816-afterlife-archive-pulls/*' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|(^|[^.])\bexec\(|\beval\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES`; result: `NO_MATCHES`.
- Post-commit status/stat inspection: `git status --short` returned no entries; `git diff --stat origin/main...HEAD` showed 16 changed files, 1717 insertions, 16 deletions, with no run artifacts.
- Runtime contract shape: unchanged. Browser archive consultation/project fuel reuses existing pending request files, `AfterlifeArchiveActionState` modes/tags, and archive services. No new GM-authored pending/control contract was introduced.

## Verification Commands To Use During/After Implementation

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "AfterlifeArchive|DirectGacha|Gacha|ExplorerWebPromptSession|BrowserAfterlifeWriteService|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|AfterlifeContractRegistryTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal
npm run verify --prefix BookOfEternityClient.WebFrontend
git diff --check origin/main...HEAD
git diff --unified=0 origin/main...HEAD -- . ':(exclude)specs/816-afterlife-archive-pulls/*' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|(^|[^.])\bexec\(|\beval\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES
```

## Notes

- Direct gacha may already be implemented; inspect before changing and prefer evidence/coverage updates over duplicate code.
- Archive consultation/project fuel reuse existing afterlife contracts. If implementation changes contract shape, update GM-facing docs/examples/tests in the same branch.
- PR creation, merge, issue closure, cron edits, or user delivery are not part of the Codex implementation run.
