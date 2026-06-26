# Tasks: GM Session-Local Context Pack

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1277

- [x] Add red tests/source guards for context-pack generation and prompt wiring.
- [x] Implement context-pack generation in the daemon.
- [x] Route ordinary turn, validation repair, and terminal protocol prompts to context-pack paths.
- [x] Adjust bridge/session working directory defaults or generated guidance for live GM.
- [x] Update GM-facing docs/examples and documentation coverage tests.
- [x] Run focused tests and bridge build.
- [x] Repeat Chaos Sea live turn/repair test and record whether implementation-code reads stop.

Live note 2026-06-26:
- Chaos Sea live turn ran from `game_state/control/gm_context_pack`.
- Codex GM did not inspect `BookOfEternityClient/**/*.cs` during turn or repair.
- Follow-up harness gaps remain: hidden bridge startup must avoid stdout redirection, trust/ready detection is still manual, and the GM spends too long reading large GM examples instead of compact turn/repair templates.
