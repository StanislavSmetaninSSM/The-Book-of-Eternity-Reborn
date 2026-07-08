# Tasks: In-Place Vitrine Preparation Wait

**Source Issue**: #1469 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1469

- [x] T001 Create GitHub issue #1469 and link it to this spec.
- [x] T002 Inspect current training/trade pending-action flow and identify the ordinary-turn handoff root cause.
- [x] T003 Add failing console tests that `/обучение` and NPC trade missing-vitrine commands stay local and show wait copy instead of returning a normal GM action directly to callers.
- [x] T004 Add or update engine-level tests for in-place vitrine refresh metadata and single automatic command re-render after GM lifecycle completion.
- [x] T005 Implement in-place pending metadata in `ExplorerMode` and game-loop dispatch.
- [x] T006 Update training and trade waiting copy to the approved wording.
- [x] T007 Check whether GM-facing docs/examples need wording updates for the harness timing change.
- [x] T008 Run focused tests and documentation/source-guard tests.
- [x] T009 Comment verification evidence on #1469 and commit initial in-place wait changes.
- [x] T010 Reopen #1469 from live-test evidence: vitrine preparation still became a visible service scene/turn after returning from the command screen.
- [x] T011 Add guard coverage that in-place vitrine preparation uses a non-player-facing GM lifecycle.
- [x] T012 Prevent accepted vitrine-preparation service responses from overwriting `_lastResponse`, appending a normal story turn, or advancing the player-visible turn.
- [x] T013 Verify focused tests and repeat the live `/обучение` wait/regression path.
- [x] T014 Comment final verification evidence on #1469 and commit the reopened fix.
