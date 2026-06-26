# Tasks: GM Turn Helper Harness

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1276

- [x] Add red tests for helper execution and daemon prompt wiring.
- [x] Implement `GM_Turn_Helper.ps1`.
- [x] Generate session-local helper bootstrap from daemon.
- [x] Add helper directive to turn/repair/protocol prompts.
- [x] Update GM-facing docs.
- [x] Run focused tests.
- [x] Repeat live Chaos Sea bridge test.
  - First live run after helper: turn completed through `gm_turn_helper.bootstrap.ps1`; previous stale `turn_request.json` terminal-signal failure did not recur.
  - Follow-up live run found bridge `DispatchFailed` could stay stale after busy-guard rejection; fixed with automatic prompt-idle recovery and regression coverage.
  - Follow-up live run found Guardian authority repair packets needed concrete Guardian actor hints, `guardian_projects.json` target coverage, and explicit no-implementation-code guidance; fixed with regression/doc coverage.
  - Remaining follow-up: run the GM from a session-local context pack instead of repo root so the GM is not tempted to inspect implementation code during play/repair.
