# Requirements Checklist: Daren Cabinet Lock Partial Literary Aftermath

**Feature**: `specs/1004-daren-cabinet-partial/`
**Issue**: [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004)
**Created**: 2026-06-14

## Scope and Traceability

- [x] Source issue #1004 is linked in `spec.md`, `plan.md`, `tasks.md`, and contract docs.
- [x] Parent #955 and source scene #974 are linked.
- [x] Completed sibling #1003, remaining sibling #1005, and completed downstream results #1006/#1007/#1008 are explicitly out of scope and use non-closing references only.
- [x] Spec Kit feature directory name matches the issue/branch identity: `specs/1004-daren-cabinet-partial/`.

## Product Requirements

- [x] `lock_pick_action` partial result is rewritten as substantial Russian literary aftermath prose.
- [x] Daren remains the active point-of-view/protagonist.
- [x] Partial grade semantics are visible: the lock opens or Daren enters, but a scratch/trace/delay/doubt/wound/evidence pressure remains.
- [x] The result includes concrete sensory/action detail around pins, pick/tension, bronze plate/keyhole, breath/hands, silence or small sound, dust/scratch, and cabinet movement.
- [x] The result bridges toward `rune_memory` / "Руны на дверце" without rewriting that scene.
- [x] Default prose avoids technical/meta terms (`GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`).

## Contract and Scope Guards

- [x] Existing success result prose for `lock_pick_action` from #1003 remains unchanged.
- [x] Existing fail result prose for `lock_pick_action` remains unchanged for #1005.
- [x] Downstream `rune_memory_action` success/partial/fail prose from #1006/#1007/#1008 remains unchanged.
- [x] Route id, beat order, action id, label, check type, characteristic, difficulty, config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, and frontend/browser files remain unchanged.
- [x] No GM-facing docs/examples update is required because no GM-authored capability or runtime contract changes are planned.

## Verification

- [x] Focused Daren guard is observed RED against the old one-sentence partial result.
- [x] Focused Daren guard is observed GREEN after implementation.
- [x] Affected Daren/QTE/docs/browser C# slice passes locally.
- [x] Client and test-project builds pass locally.
- [x] Spec Kit prerequisite check resolves this feature directory.
- [x] `git diff --check origin/main...HEAD` passes.
- [x] Added-line static security scan returns `NO_MATCHES` or findings are resolved.
- [ ] Independent review approves literary quality, scope control, and verification evidence before PR/merge.

## Local Evidence

- Baseline focused Daren test command before implementation: 61 passed / 0 failed / 0 skipped / 61 total.
- Baseline affected Daren/QTE/docs/browser C# slice before implementation: 330 passed / 0 failed / 0 skipped / 330 total.
- RED focused Daren guard: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 61 passed / 1 failed / 0 skipped / 62 total, failing as expected on the old exact `lock_pick_action` partial sentence.
- New `lock_pick_action` partial aftermath measurement from built route data: 2416 characters, 25 scene sentences, 7 `Дарен` mentions, 386 words.
- GREEN focused Daren guard: same focused command => 62 passed / 0 failed / 0 skipped / 62 total.
- Affected Daren/QTE/docs/browser C# slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 331 passed / 0 failed / 0 skipped / 331 total.
- Builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` both succeeded with 0 warnings / 0 errors.
- Pre-commit added-line static scan over production/test code returned `NO_MATCHES`.
- Post-commit `git diff --check origin/main...HEAD` passed with no output.
- Post-commit added-line static scan over production/test code returned `NO_MATCHES`.
- Diff/scope review found no frontend/browser, endpoint, runtime state, reward/profile/New Game grant, route mechanics, #1003 success prose, #1005 fail prose, or #1006/#1007/#1008 rune-memory prose changes.
- Hermes-owned evidence still pending: independent review, PR/merge, issue closure, parent #955 reconciliation, and cleanup.
