# Project-Wide Audit Findings

Tracking epic: `epic: project-wide-audit`

Task order: `docs/audits/project-wide-audit-task-order.md`

## Rules For This File

- Record every project-wide audit finding here before implementing a fix.
- If a finding requires code, test, prompt, documentation, example, contract, or UI changes, create or link a dedicated GitHub issue before making those changes.
- If a finding is too broad for one fix, split it into smaller GitHub issues and link all of them in the `Issue` column.
- If an audit slice finds no actionable issue, add an audit checkpoint instead of inventing a finding.
- Keep implementation status factual: do not mark a finding `fixed` without verification evidence.

## Finding ID Format

- Use `PWA-001`, `PWA-002`, and so on for project-wide audit findings.
- Use the specialized audit ledger id if a finding belongs entirely to an existing specialized audit file.
- Cross-link specialized ledgers when they already cover the area.

## Severity

- `P1` - build break, data loss, security/safety risk, invalid accepted-turn state, or blocker for normal play.
- `P2` - functional correctness bug, contract drift, validation/runtime mismatch, or serious UX confusion.
- `P3` - documentation gap, test gap, localization inconsistency, minor UI issue, or low-risk maintainability problem.

## Status

- `open` - finding recorded, fix not started.
- `split` - finding moved into one or more dedicated GitHub issues.
- `fixed` - implementation completed and verification evidence recorded.
- `wontfix` - explicitly accepted as not worth changing, with rationale.
- `checkpoint` - no discrete finding in the audited scope.

## Finding Template

| ID | Status | Issue | Area | Severity | Summary | Source / Evidence | Expected Behavior | Proposed Fix | Verification |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PWA-000 | open | #NNN | Runtime / UI / Prompt / Tests | P2 | Short defect summary. | Exact files, commands, examples, or mental experiment. | What should happen. | Concrete fix direction or split issue. | Test/manual command to prove the fix. |

## Findings

| ID | Status | Issue | Area | Severity | Summary | Source / Evidence | Expected Behavior | Proposed Fix | Verification |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PWA-001 | split | #637 | Tests / Build hygiene | P3 | Clean test build passes but emits 169 warnings, making new warning regressions easy to miss. | `dotnet clean BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --nologo`, then `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --verbosity minimal`: 2821 tests passed, 0 failed, `TOTAL_WARNINGS=169`; warning families include `CS8600`, `CS8601`, `CS8602`, `CS8603`, `CS8604`, `CS8625`, `xUnit2030`, and `xUnit2031`. | Clean verification should either be warning-clean or have an explicit warning baseline/budget so new warnings cannot disappear into existing noise. | Triage warning clusters, fix low-risk analyzer/nullability warnings, and add a warning budget/gate if full zero-warning cleanup is too large for one branch. | Re-run clean `dotnet test` and compare warning count/budget. |
| PWA-002 | split | #638 | CI / Repository automation | P2 | Repository has GitHub issue templates but no GitHub Actions workflow to run restore/build/test on pushes or pull requests. | `.github/ISSUE_TEMPLATE/*` exists, but `Get-ChildItem -LiteralPath '.github\workflows' -Force -ErrorAction SilentlyContinue` returns no directory (`NO_WORKFLOWS_DIR`). Local full test command passes only when run manually. | GitHub should automatically run the .NET restore/build/test verification on PRs and pushes to `main`. | Add `.github/workflows` CI for .NET 8 restore, build, and `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj`. | Workflow run should pass on the branch introducing it; local equivalent command remains green. |
| PWA-003 | fixed | #639 | Runtime / Schema / Normalization | P1 | Command-only `sarefMainStoryUpdate` could project against an empty default root because `main_story_saref_state.json` was not captured as an optional pre-turn canonical baseline. | `CanonicalStateNormalizer.SarefMainStory.cs` reads `ReadBackupObjectAsync(SarefMainStoryState.StatePath, backups)` for wrapper projection; `GameEngine.SessionAndSnapshots.cs` optional-added only afterlife conflict and entity profile canonical states, not `SarefMainStoryState.StatePath`; `CanonicalAccumulatedFiles` also omits it because absent legacy Saref state is valid. | If `main_story_saref_state.json` existed pre-turn, accepted-turn snapshots should preserve it as an optional canonical baseline so command-only Saref updates project over existing revelations, route fragments, oath/bond state, endings, and faction links. | Added `SarefMainStoryState.StatePath` to optional canonical baseline capture/loading and normalizer backup inputs when present; added regression coverage for backup input coverage and optional snapshot registration. | `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "CanonicalStateNormalizerTests|ValidationSourceGuardTests|SarefMainStoryStateValidationTests"` passed 240/240. |

## Audit Checkpoints

| Date | Issue | Scope | Result | Verification |
| --- | --- | --- | --- | --- |
| 2026-05-21 | #626 | Created project-wide audit ledger and task closure order for issues #626-#636. | Ledger, taxonomy, severity/status rules, required finding fields, and checkpoint format are defined. | `git diff --check -- docs/audits/project-wide-audit-findings.md docs/audits/project-wide-audit-task-order.md` |
| 2026-05-21 | #631 | Test coverage, fixtures, encoding, and CI reliability audit. Reviewed test project structure, fixture/test files, `.github` contents, mojibake markers in tracked `cs/md/txt/json/yml/yaml` files, and full clean test execution. | Two findings recorded: PWA-001 warning debt and PWA-002 missing CI workflow. Mojibake marker scan found no tracked matches for `�`, `Ð`, `Ñ`, `Рџ`, or `Р ` outside generated/untracked files. | `dotnet clean BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --nologo`; `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --verbosity minimal` passed 2821/2821 with 169 warnings; `Get-ChildItem .github\workflows` returned no workflow directory. |
| 2026-05-21 | #627 | Runtime lifecycle, persistence, rollback, and save-state integrity audit. Reviewed `GameEngine.SessionAndSnapshots.cs`, `GameEngine.TurnLifecycle.cs`, `/incarnate` local prep rollback in `GameEngine.MainMenu.cs`, Explorer staged rollback storage, `AfterlifeLocalActionGuard`, pending-turn authority tests, and existing specialized afterlife lifecycle checkpoints. | No additional discrete runtime lifecycle defect found. Validated manifest authority is used for destructive rollback/snapshot reads; stale empty pending snapshot directories are ignored by local action guards; explorer local rollback backups are stored under `game_state/control/explorer_local_turn_rollback` and excluded from generic rollback cleanup; Soul Gates/Shining blockers and active conflict blockers are covered by existing tests. Residual warning debt is already tracked as PWA-001/#637. | `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GameEngineTurnLifecycleTests|PendingTurnSnapshotAuthorityTests|AfterlifeLocalActionGuardTests|AfterlifeReturnGuardServiceTests|SaveLoadServiceTests|StateManagerTests|LocalUiSessionLockServiceTests|ValidationSourceGuardTests"` passed 97/97. |
| 2026-05-21 | #633 | Data schema, backward compatibility, and migration audit. Reviewed flexible/strict state-file validators, afterlife optional canonical file initialization, canonical normalizer backup dependencies, malformed pending/control handling, entity profile schema validation, Saref main-story wrapper projection, and targeted schema/normalizer tests. | One data-loss finding recorded as PWA-003/#639. Existing afterlife conflict and entity profile optional baselines are covered; Saref main-story baseline is not. | `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ValidatorFixtureTests|CriticalStateHealthServiceTests|CanonicalStateNormalizerTests|RealmSemanticsValidationTests|ShiningStateValidationTests|InkFeatherRealmValidationTests|AfterlifeEntityProfileValidationTests|SourceOfLightCapstoneValidationTests|SarefMainStoryStateValidationTests|GuardianQuestProgressValidationTests|ShiningTreasuryStateTests|ExplorerCommandMigrationRegistryTests"` passed 445/445. |

## Related Specialized Audit Ledgers

- `docs/audits/afterlife-chaos-shining-audit-findings.md` - existing Chaos Sea / Shining Abode audit findings and checkpoints.
