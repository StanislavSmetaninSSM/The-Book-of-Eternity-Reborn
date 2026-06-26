# Spec: GM Bridge Auto Trust And Ready

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1279

## Goal

Make hidden Codex GM bridge startup usable without manual `sendEnter` and `ready`
steps when the GM is launched inside the generated session-local context pack.

## Requirements

- The launcher starts the bridge hidden by default without redirecting stdout or
  stderr in a way that breaks Codex terminal detection.
- A visible bridge fallback remains available for manual debugging.
- The bridge auto-accepts Codex workspace trust only when the hosted shell
  working directory is the generated `game_state/control/gm_context_pack`.
- The bridge auto-marks itself ready only when the visible screen is an idle
  Codex prompt.
- The bridge must not mark ready while Codex is working, starting MCP servers,
  showing the workspace trust prompt, or sitting at a non-Codex shell prompt.

## Non-Goals

- Do not change the GM turn output contract.
- Do not change worker-agent task dispatch.
- Do not auto-trust arbitrary repository or user directories.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmBridgeDiagnosticsContractTests"
dotnet build BookOfEternityGMBridge\BookOfEternityGMBridge.csproj --no-restore
```

Manual/live:

- Start a Chaos Sea session with hidden bridge.
- Confirm diagnostics show trust prompt auto-accepted.
- Confirm status becomes `Ready` without manual `ready`.
- Confirm Codex does not fail with `stdout is not a terminal`.
