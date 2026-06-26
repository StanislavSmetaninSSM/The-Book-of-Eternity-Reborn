# Tasks: GM Compact Turn And Repair Templates

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1280

- [x] Add RED source-guard tests for compact template generation and prompt
  ordering.
- [x] Generate compact templates under session-local `gm_context_pack/Templates`.
- [x] Add manifest roles and README routing for compact templates.
- [x] Add `GmCompactTemplateDirective`.
- [x] Update turn and repair prompts to use compact templates before large
  examples.
- [x] Route terminal-protocol failure prompts through the compact repair
  template path too.
- [x] Run focused and harness regression tests.
- [x] Run daemon context-pack smoke and record generated templates.
- [ ] Run live Chaos Sea turn and record diagnostics/duration.

Verification notes:

- RED: `GmTurnHelperContractTests` failed on missing `Templates/*` and missing
  `GmCompactTemplateDirective`.
- GREEN: focused harness/afterlife suite passed, 161/161.
- Build: `BookOfEternityGMBridge` built with 0 warnings, 0 errors.
- Smoke: temporary session
  `C:\Temp\boe-gm-compact-template-smoke-20260626-153444\game_session`
  generated all five compact templates and manifest roles.
