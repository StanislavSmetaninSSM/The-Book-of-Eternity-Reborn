# Feature Spec: GM Session-Local Context Pack

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1277

## Problem

Live Chaos Sea bridge tests showed that the Codex GM can complete turns through
the session-local turn helper, but during validation repair it may read
repository implementation files such as `BookOfEternityClient/**/*.cs` instead
of using GM-facing docs, session state, repair packets, and helper tools.

This is a harness problem: the GM bridge currently runs in a repo-root context,
which makes implementation code visually and operationally available during
normal play.

## User Stories

1. As the player, I want the GM to resolve turns from game-facing state and
   guidance, not from implementation code, so live play is faster and less
   brittle.
2. As the GM harness, I want each live session to expose a local context pack
   with exactly the docs/examples/helpers needed for the turn, so prompts can
   point to stable session-owned paths.
3. As a maintainer, I want tests/source guards proving normal turn, repair, and
   terminal-protocol prompts route through the context pack.

## Acceptance Criteria

- The daemon creates or refreshes a session-local GM context pack under
  `game_state/control/` or another session-owned folder.
- The context pack includes the GM-facing docs/examples required by ordinary
  turns, validation repair, terminal protocol failure, afterlife contracts, and
  the session-local turn helper bootstrap.
- Normal turn, repair, and terminal-protocol prompts reference context-pack
  paths first instead of repo-root implementation paths.
- The bridge default for live GM work can use a session/context-pack working
  directory rather than the repo root.
- GM-facing instructions state that repository implementation code is off-limits
  during normal play and repair; any needed mechanics must be surfaced through
  repair packets, validators, helpers, or GM-facing docs.
- Tests/source guards cover the context-pack generation and prompt wiring.
- A follow-up live Chaos Sea turn/repair test verifies that the GM does not
  inspect `BookOfEternityClient/**/*.cs` while resolving the turn/repair.

## Out Of Scope

- Security sandboxing against a malicious local process.
- Rewriting Codex itself or changing the model.
- Removing repo-root access for developer agents; this applies to the live GM
  runtime path.

