# Requirements Checklist: Daren Cabinet Lock Fail Literary Aftermath

**Feature**: `specs/1005-daren-cabinet-fail/`
**Issue**: [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005)
**Created**: 2026-06-14

## Scope and Traceability

- [x] Source issue #1005 is linked in `spec.md`, `plan.md`, `tasks.md`, and contract docs.
- [x] Parent #955 and source scene #974 are linked.
- [x] Completed siblings #1003/#1004 and completed downstream results #1006/#1007/#1008 are explicitly out of scope and use non-closing references only.
- [x] Spec Kit feature directory name matches the issue/branch identity: `specs/1005-daren-cabinet-fail/`.

## Product Requirements

- [x] `lock_pick_action` fail result is rewritten as substantial Russian literary aftermath prose.
- [x] Daren remains the active point-of-view/protagonist.
- [x] Fail grade semantics are visible: danger escalates through noise, trace/evidence, guard/house awareness, or pursuit pressure.
- [x] The result includes concrete sensory/action detail around pins, pick/tension, bronze plate/keyhole, breath/hands, lock noise, dust/scratch, guard response, and cabinet movement or blocked entry.
- [x] The result bridges toward `rune_memory` / "Руны на дверце" without rewriting that scene.
- [x] Default prose avoids technical/meta terms (`GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`).

## Contract and Scope Guards

- [x] Existing success result prose for `lock_pick_action` from #1003 remains unchanged.
- [x] Existing partial result prose for `lock_pick_action` from #1004 remains unchanged.
- [x] Downstream `rune_memory_action` success/partial/fail prose from #1006/#1007/#1008 remains unchanged.
- [x] Route id, beat order, action id, label, check type, characteristic, difficulty, config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, and frontend/browser files remain unchanged.
- [x] No GM-facing docs/examples update is required because no GM-authored capability or runtime contract changes are planned.

## Verification

- [x] Focused Daren guard is observed RED against the old one-sentence fail result.
- [x] Focused Daren guard is observed GREEN after implementation.
- [x] Affected Daren/QTE/docs/browser C# slice passes locally.
- [x] Client and test-project builds pass locally.
- [x] Spec Kit prerequisite check resolves this feature directory.
- [x] `git diff --check origin/main...HEAD` passes.
- [x] Added-line static security scan returns `NO_MATCHES` or findings are resolved.
- [x] Independent review approves literary quality, scope control, and verification evidence before PR/merge.

## Local Evidence

- Baseline focused Daren test command before implementation: 62 passed / 0 failed / 0 skipped / 62 total.
- Baseline affected Daren/QTE/docs/browser C# slice before implementation: 331 passed / 0 failed / 0 skipped / 331 total.
- Spec Kit prerequisite check before implementation resolved `FEATURE_DIR=E:\\Games\\worktrees\\boe-1005-daren-cabinet-fail\\specs\\1005-daren-cabinet-fail` with `contracts/` and `tasks.md`.
- RED focused Daren guard: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 62 passed / 1 failed / 0 skipped / 63 total; expected failure was `Assert.NotEqual() Failure: Strings are equal` against the old one-sentence fail result.
- GREEN focused Daren guard: same focused command => 63 passed / 0 failed / 0 skipped / 63 total.
- Affected Daren/QTE/docs/browser C# slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 332 passed / 0 failed / 0 skipped / 332 total.
- Implementation measurements: final fail result is 2252 characters / 350 words / 18 sentences / 7 `Дарен` mentions.
- Builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded, 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded, 0 warnings / 0 errors.
- Diff/static checks: pre-commit `git diff --check` => exit 0 with no whitespace errors; code/test added-line static scan for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting => `NO_MATCHES`. Post-commit `git diff --check origin/main...HEAD` evidence is reported in Codex final handoff.
- Hermes-owned evidence still pending: merge, issue closure, parent #955 reconciliation, and cleanup.
- Independent review: detached Codex review run `E:/Games/codex-runs/20260614-145737-review-boe-1005-daren-cabinet-fail` exited 0 and returned `APPROVED` with no Critical/Important/Minor findings; reviewer inspected the full diff and relevant code/tests/spec artifacts, `git diff --check origin/main...HEAD` passed, and added-line/prose/static-risk scans found no matches.
- PR evidence: PR #1032 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1032`) was created with `Resolves #1005`, local verification evidence, independent-review verdict, and parent/sibling non-closing scope references.
