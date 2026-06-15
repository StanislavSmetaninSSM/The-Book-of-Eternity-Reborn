# Requirements Checklist: Daren Approach Manor Fail Literary Aftermath

**Feature**: `specs/990-daren-approach-fail/`
**Source issue**: [#990](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/990)

## Scope

- [x] Source issue #990 identified and linked.
- [x] Parent #955 and scene #969 context identified.
- [x] Same-scene completed siblings #988/#989 identified as out of scope.
- [x] Downstream completed result surfaces #991-#1008 identified as out of scope.
- [x] Contract classified as client-owned authored showcase prose; no GM-facing/runtime contract change intended.

## Acceptance

- [x] `approach_manor_action` fail text is substantial Russian literary aftermath prose. Evidence: final insert is 1,790 characters, 293 words, and 13 scene sentences.
- [x] Daren remains active POV. Evidence: final insert includes 5 `Дарен` mentions and focused guard checks Daren body-control motifs under danger.
- [x] Fail grade is concrete: exposed light, guard/patrol/dog/lantern/witness, evidence, pursuit, or suspicion pressure escalates. Evidence: focused guard checks grouped light exposure, patrol/guard/dog/witness pressure, and pursuit/evidence escalation motifs.
- [x] Text bridges toward `informant_parley` / Mira without changing route order. Evidence: focused guard checks `approach_manor` still routes success/partial/fail to `informant_parley` and the text names `Мира Ночная Нить`.
- [x] Text avoids implementation/mechanic/debug terminology. Evidence: `AssertNoPlayerFacingTechnicalTerms` passes for the fail aftermath, and production prose scan returned `NO_FORBIDDEN_TERMS`.
- [x] #988 success, #989 partial, and #991-#1008 downstream result texts remain unchanged. Evidence: focused guard pins success/partial constants and downstream result sentinels.
- [x] QTE mechanics, routing, score deltas, rewards/profile/New Game behavior, endpoints, runtime state, and frontend boundaries remain unchanged. Evidence: focused guard verifies `BranchChoice`, action ids/labels, routing, and score deltas; no frontend/runtime/endpoint files changed.

## Verification Evidence

- [x] Baseline focused Daren test before implementation: 77 total, 77 passed, 0 failed, 0 skipped.
- [x] Baseline affected Daren/QTE/docs/browser slice before implementation: 346 total, 346 passed, 0 failed, 0 skipped.
- [x] Spec Kit prerequisite check resolves `specs/990-daren-approach-fail/`: `FEATURE_DIR=E:\\Games\\worktrees\\boe-990-daren-approach-fail\\specs\\990-daren-approach-fail`, `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
- [x] RED focused guard fails against old one-sentence fail text for the expected reason: 78 total, 77 passed, 1 failed, 0 skipped; failure message `Daren approach_manor fail should reject the old one-sentence result notification.`
- [x] GREEN focused Daren test passes after rewrite: 78 total, 78 passed, 0 failed, 0 skipped.
- [x] Affected Daren/QTE/docs/browser slice passes after rewrite: 347 total, 347 passed, 0 failed, 0 skipped.
- [x] Client and test-project builds pass: both completed with 0 warnings and 0 errors.
- [x] `git diff --check origin/main...HEAD` passes. Evidence: working-tree `git diff --check` passed before commit; final commit diff check is part of Codex's required pre-final gate.
- [x] Added-line static scan returns `NO_MATCHES` or every finding is explicitly classified as a false positive with evidence. Evidence: one documentation-only command-line finding for the required Spec Kit prerequisite command inside verification docs; no code/test shell execution, hardcoded secrets, standalone `exec`/`eval`, unsafe deserialization, or SQL string-formatting findings.
- [ ] Independent review approves before PR/merge.
