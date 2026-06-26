# Tasks: GM Bridge Auto Trust And Ready

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1279

- [x] Add RED diagnostics/source-guard tests for hidden startup, trust prompt
  auto-accept, and idle Codex auto-ready.
- [x] Add bridge automation loop and request-path refresh hook.
- [x] Restrict trust auto-accept to session-local `gm_context_pack`.
- [x] Tighten Codex readiness probe so working/MCP/trust/shell screens are not
  considered ready.
- [x] Make launcher `start-bridge` hidden by default with a visible fallback.
- [x] Run focused tests, bridge build, and a short live hidden-start check.

Live note 2026-06-26:
- Temporary session `C:\Temp\boe-bridge-auto-ready-20260626-150431`.
- `bookofeternity.ps1 start-bridge` launched a hidden bridge without stdout
  redirection.
- Status became `Ready` on poll 2 without manual `sendEnter` or `ready`.
- `shutdown-bridge` stopped the helper/shell processes cleanly.
