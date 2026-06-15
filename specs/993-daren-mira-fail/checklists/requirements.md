# Requirements Checklist: Daren Mira Whisper Fail Literary Aftermath

Source issue: [#993](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/993)

## Scope and Traceability

- [x] Source issue #993 is linked in `spec.md`, `plan.md`, `tasks.md`, and this checklist.
- [x] Parent #955 and source scene #970 are linked.
- [x] Same-scene sibling boundaries are explicit: completed #991 success and #992 partial must remain unchanged.
- [x] Previous #988/#989/#990 and downstream #994-#1008 result surfaces are out of scope and pinned.
- [x] Contract scope says this is client-owned authored showcase prose, not a GM-facing/runtime-state contract change.

## Acceptance Requirements

- [x] `informant_parley_action` fail result is rewritten as a substantial Russian literary aftermath insert. Evidence: rewritten fail result is 2485 characters, 23 scene sentences, and 8 `Дарен` mentions.
- [x] The rewrite is clearly dangerous outcome: Daren's failed threat/social pressure collapses trust, while witness/guard/pursuit/evidence/source pressure escalates.
- [x] Daren remains the active protagonist and point of view.
- [x] Mira remains personified through body language, pressure, anger/caution, and witness/source-protection stakes.
- [x] The prose includes wet-awning/social atmosphere and bridges toward `gadget_infiltration_action` / `Крюк и леска`.
- [x] Default prose has no implementation/debug/meta terminology.
- [x] #991 success, #992 partial, #988-#990 previous-result, and #994-#1008 downstream result surfaces remain unchanged by focused sentinels and affected-slice verification.
- [x] Mechanics, route order, action ids, choice config, score deltas, rewards, endpoints, runtime state, and frontend files remain unchanged by focused mechanics assertions and affected-slice verification.

## Verification Evidence

- [x] Baseline focused Daren tests recorded before implementation: Hermes baseline passed 74/74.
- [x] Baseline affected Daren/QTE/docs/browser slice recorded before implementation: Hermes baseline passed 343/343.
- [x] Spec Kit prerequisite check resolves `FEATURE_DIR=E:\\Games\\worktrees\\boe-993-daren-mira-fail\\specs\\993-daren-mira-fail` with `contracts/` and `tasks.md`.
- [x] RED focused guard observed failing for the old fail text: 75 total, 74 passed, 1 failed; expected old-text rejection message.
- [x] GREEN focused Daren tests pass after implementation: 75 total, 75 passed, 0 failed, 0 skipped.
- [x] Affected Daren/QTE/docs/browser C# slice passes after implementation: 344 total, 344 passed, 0 failed, 0 skipped.
- [x] Client build and test-project build pass after implementation: both 0 warnings/0 errors.
- [x] `git diff --check origin/main...HEAD` passes after implementation commit. Codex also recorded pre-commit `git diff --check origin/main` as clean before commit.
- [x] Added-line static scan returns `NO_MATCHES`.
- [x] Independent review approves literary quality, scope control, and verification evidence. Evidence: detached review run `E:/Games/codex-runs/20260615-170348-review-boe-993-daren-mira-fail` on exact head `69160ad` exited `0` with verdict `APPROVED` and no Critical/Important/Minor findings.

## Lifecycle Evidence

- [x] PR created and closes only #993. Evidence: PR #1044 readback showed `closingIssuesReferences=[#993]` and safe non-closing sibling/parent references.
- [ ] PR merged by local-gated squash merge.
- [ ] #993 issue evidence comment posted.
- [ ] #993 confirmed `CLOSED` / `COMPLETED`.
- [ ] Label moved to `status: verified`.
- [ ] Implementation/review worktrees and local/remote branches cleaned up.
