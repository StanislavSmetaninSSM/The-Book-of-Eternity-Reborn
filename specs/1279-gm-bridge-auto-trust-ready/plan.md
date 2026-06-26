# Plan: GM Bridge Auto Trust And Ready

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1279

## Approach

- Add bridge-side screen probes for Codex workspace trust, active working/MCP
  screens, and idle prompt screens.
- Auto-accept trust only when `_status.ShellWorkingDirectory` resolves to the
  session-local `game_state/control/gm_context_pack`.
- Auto-set bridge `Ready` when an idle Codex prompt is visible.
- Tighten dispatch readiness so a PowerShell prompt or any non-idle Codex screen
  is not treated as ready.
- Change launcher `start-bridge` to hidden-by-default startup with a `visible`
  fallback argument.

## Risks

- Codex UI text can change. Keep checks conservative: auto-trust only for a
  known context-pack directory, and auto-ready only on a clear idle Codex prompt.
- Non-Codex bridge users should keep manual ready behavior.

## Tests

- Source guards for auto-trust method, context-pack path guard, idle prompt
  recognition, and hidden launcher startup.
- Existing bridge diagnostics tests remain in the same suite.
