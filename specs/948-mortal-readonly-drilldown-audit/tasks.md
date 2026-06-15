# Tasks: Mortal Read-Only Detail Drill-Down Audit

**Source GitHub issue:** #948 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/948
**Spec:** `specs/948-mortal-readonly-drilldown-audit/spec.md`
**Plan:** `specs/948-mortal-readonly-drilldown-audit/plan.md`

## Implementation Tasks

- [x] T001 Preflight: confirm branch/worktree status, read AGENTS.md, and inspect the current open issue #948 body.
- [x] T002 Enumerate all commands handled by `ExplorerMortalWorldCommandResultBuilder` and matching console command registrations/handlers.
- [x] T003 Inspect existing reference patterns for adequate detail flows (`/квесты`, `/навыки`, `/фракции`, `/локации`, `/инв`) and record the criteria used to classify audited commands.
- [x] T004 Audit each initial candidate from #948 (`/эффекты`, `/бой`, `/новости_мира`, `/транспорт`, `/взаимодействия`) plus any additional mortal read-only command discovered in T002.
- [x] T005 Write the tracked audit artifact with command, console behavior, browser behavior, gap status, severity, recommended action, and follow-up/fix decision.
- [x] T006 Add a failing focused test/source guard that proves the audit artifact covers the mortal command set and/or catches raw-only/all-in-one-only regression for a confirmed small gap.
- [x] T007 Implement the minimal production/test/doc change needed to make T006 pass without rewriting unrelated command flows.
- [x] T008 Create GitHub follow-up issues for confirmed larger gaps that are not fixed in this PR; each follow-up must link back to #948 and state console/browser parity expectations.
- [x] T009 Reconcile Spec Kit artifacts with implementation decisions; update `spec.md`, `plan.md`, or this file if scope changes.
- [x] T010 Run focused verification and `git diff --check`; record exact command output/counts in the final Codex report.
- [x] T011 Commit the focused branch changes with `[skip ci]` in the commit message.

## Review and Closure Tasks

- [x] T012 Hermes performs or launches independent review before PR/merge.
- [ ] T013 Hermes verifies local gates, creates PR, squash-merges, posts issue evidence comment, and confirms #948 is closed.

## Notes

- T008 may be skipped only if the audit finds no larger confirmed gaps or all gaps are fixed in this branch.
- Do not check off T012/T013 inside the implementation commit unless Hermes has already completed those lifecycle steps before merge.
- T005 artifact: `docs/audits/mortal-readonly-drilldown-audit.md`.
- T006 RED evidence: the focused audit/transport filter initially failed as expected with 2 failed, 9 passed, 11 total because the audit artifact was missing and `/транспорт` did not render canonical `vehicles[]`.
- T007 fix: browser `/transport` now reads `game_state/misc/vehicles.json`, renders a vehicle table, and preserves route/current-location summary plus raw state blocks.
- T008 follow-ups created: #1054 (`/combat`), #1055 (`/world_news`), #1056 (`/interactions`), #1057 (browser detail actions for reference-style mortal read-only commands).
- Independent-review fix evidence on 2026-06-16:
  - RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExecuteAsync_Transport_TranslatesCanonicalTypeAndAvailabilityInTableCells" --logger "console;verbosity=minimal"` failed as expected with 1 failed, 0 passed, 0 skipped because browser `/транспорт` table cell still contained canonical `mount` instead of `Ездовое животное`.
  - GREEN: the same focused test passed after translating browser transport type/status cells: 1 passed, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "MortalReadOnlyDrilldownAudit_CoversEveryMortalWorldReadOnlyDescriptor|ExecuteAsync_MortalReadOnlySummaries_ReadCanonicalStateKeys|Transport" --logger "console;verbosity=minimal"`: 32 passed, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExplorerMortalWorldCommandResultBuilder|ExplorerModeCommandTests|ExplorerWebCommandServiceTests|ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`: 450 passed, 0 failed, 0 skipped.
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`: succeeded with 0 warnings, 0 errors.
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`: succeeded with 0 warnings, 0 errors.
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`: `FEATURE_DIR` resolved to `specs/948-mortal-readonly-drilldown-audit`.
  - `git diff --check`: no whitespace errors.
  - Added-line static/security scan over changed C# code for secrets, shell execution, eval, unsafe deserialization, and SQL string formatting: `NO_MATCHES`.
- T012 independent review evidence:
  - Initial read-only Codex review run `E:/Games/codex-runs/20260616-055600-review-boe-948-mortal-drilldown-audit` returned `CHANGES_REQUIRED` for browser `/transport` raw enum leakage in default player-facing table/details.
  - Fix run `E:/Games/codex-runs/20260615-200449-fix-boe-948-transport-enums` translated default browser transport type/status cells and added focused table-cell regression coverage.
  - Read-only Codex re-review run `E:/Games/codex-runs/20260615-202008-rereview-boe-948-mortal-drilldown-audit` returned `APPROVED`; no blocking findings; previous raw enum finding fixed; follow-ups #1054-#1057 remain the intended larger-work split.
- T013 PR evidence before merge: PR https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1058 was created against `main` with `Closes #948`, local-gated verification evidence, independent review/re-review evidence, and safe non-closing references for #1054-#1057. Merge, post-merge gates, issue evidence comment, label hygiene, and cleanup remain Hermes-owned until after the PR lands.
