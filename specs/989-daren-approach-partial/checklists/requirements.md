# Requirements Checklist: Daren Approach Manor Partial Literary Aftermath

Source issue: [#989](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/989)

## Scope and Traceability

- [x] Source issue #989 is linked in `spec.md`, `plan.md`, `tasks.md`, and this checklist.
- [x] Parent #955 and source scene #969 are linked.
- [x] Same-scene sibling boundaries are explicit: #988 success must remain unchanged and #990 fail must remain unchanged.
- [x] Downstream #991-#1008 result surfaces are out of scope and pinned.
- [x] Contract scope says this is client-owned authored showcase prose, not a GM-facing/runtime-state contract change.

## Acceptance Requirements

- [x] `approach_manor_action` partial result is rewritten as a substantial Russian literary aftermath insert. Evidence: final insert is 1,898 characters, 313 words, and 14 sentences.
- [x] The rewrite is clearly mixed/costly outcome: Daren keeps moving, but a trace, sound, delay, wound, doubt, witness suspicion, or later evidence pressure remains plausible. Evidence: focused motif guard checks broken branch/noise, trace/evidence/suspicion, delay, wound/body cost, guard/patrol/dog/lantern pressure, and continued route progress.
- [x] Daren remains the active protagonist and point of view. Evidence: focused guard requires at least 3 `Дарен` mentions; final insert has 6.
- [x] The prose includes old-linden/manor-wall/lantern-shadow approach atmosphere and bridges toward Mira / `informant_parley_action`. Evidence: focused grouped motif checks cover linden, manor wall, lantern/shadow, and direct Mira/informant continuity.
- [x] Default prose has no implementation/debug/meta terminology. Evidence: focused guard runs `AssertNoPlayerFacingTechnicalTerms`; added-line static scan returned `NO_MATCHES`.
- [x] #988 success, #990 fail, and #991-#1008 downstream result surfaces remain unchanged by focused sentinels and affected-slice verification. Evidence: focused guard asserts #988 success, #990 fail, and downstream sentinels; affected slice passed 346/346.
- [x] Mechanics, route order, action ids, choice config, score deltas, rewards, endpoints, runtime state, and frontend files remain unchanged by focused mechanics assertions and affected-slice verification. Evidence: focused guard asserts route ids/order/action/check/routing/score deltas; no frontend files changed; affected slice passed 346/346.

## Verification Evidence

- [x] Baseline focused Daren tests recorded before implementation: Hermes baseline passed 76/76.
- [x] Baseline affected Daren/QTE/docs/browser slice recorded before implementation: Hermes baseline passed 345/345.
- [x] Spec Kit prerequisite check resolves `FEATURE_DIR=E:\\Games\\worktrees\\boe-989-daren-approach-partial\\specs\\989-daren-approach-partial` with `contracts/` and `tasks.md`.
- [x] RED focused guard observed failing for the old partial text. Evidence: focused Daren run returned 77 total, 76 passed, 1 failed, 0 skipped; failure was `DarenApproachManorPartial_ReadsAsCostlyStealthAftermathWithoutMechanicDrift` rejecting the old one-sentence result notification.
- [x] GREEN focused Daren tests pass after implementation. Evidence: focused Daren run returned 77 total, 77 passed, 0 failed, 0 skipped.
- [x] Affected Daren/QTE/docs/browser C# slice passes after implementation. Evidence: affected slice returned 346 total, 346 passed, 0 failed, 0 skipped.
- [x] Client build and test-project build pass after implementation. Evidence: both `dotnet build` commands completed with 0 warnings and 0 errors.
- [x] `git diff --check origin/main...HEAD` passes after implementation commit. Evidence: pre-commit `git diff --check` exited 0; Codex final verification reruns the `origin/main...HEAD` form after the implementation commit.
- [x] Added-line static scan returns `NO_MATCHES`. Evidence: scan covered added code/test lines for hardcoded secrets, shell execution, `eval`/standalone `exec`, unsafe deserialization, and SQL string formatting and returned `NO_MATCHES`.
- [ ] Independent review approves literary quality, scope control, and verification evidence.

## Lifecycle Evidence

- [ ] PR created and closes only #989.
- [ ] PR merged by local-gated squash merge.
- [ ] #989 issue evidence comment posted.
- [ ] #989 confirmed `CLOSED` / `COMPLETED`.
- [ ] Label moved to `status: verified`.
- [ ] Implementation/review worktrees and local/remote branches cleaned up.
