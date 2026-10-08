# Native Windows HTTP diagnosis — T054-WINDOWS-HTTP

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Owner approved separate diagnosis and a minimal causally established fix on 2026-10-08.
Branch `codex/1553-windows-http-20261008` was published at base
`5ff403ed998f7e04dadf6179fba614252bcfd2cf` before changes.

Relay runtime remains frozen at0067874e; its independently reviewed Windows
checkpoint5ff403ed restored29 changed files and14 evidence payloads from GitHub.
Linux regression is parent-owned and must retain its actual source revision.

## Accepted scope and plan

- [ ] Compare an empty root, an ordinary newly initialized game and a separate
  copy of the original owned synthetic Save/Load/restart root. No user data.
- [ ] Capture actual server exceptions/stacks through the existing ILoggerFactory
  on the real LocalWebUiHost, with actual loopback Kestrel and original DI.
  Diagnostic provider only; no replacement services or response rewriting.
- [ ] Establish the smallest causal failing regression before runtime changes.
  A malformed fixture alone does not justify a product fix or an empty200.
- [ ] Make only a proven correction, select affected catalog categories, preserve
  parallel/repeated HTTP reads, Save/Load/restart and normal owned shutdown.
- [ ] Publish bounded WIP before long checks/review, obtain independent Astra
  XHigh review, verify exact remote SHA and fresh GitHub-only source recovery.

Original evidence: three500 responses (/api/session,/api/game-screen,
/api/audio/settings) under parallel browser/HTTP reads after synthetic Save/Load
and restart. Serialized audio returned200. No stack or clean-game comparison was
available, so the cause is unproven. Preserve that distinction.

No model requests, installs, WSL, new network permissions, security/auth changes,
CI enabling, merge, force push, branch deletion or issue closure. Bind loopback
only; retain ownership and await each diagnostic host's StopAsync/DisposeAsync.
No game/GM schema or prompt change is planned; reassess if a proven cause requires it.

## Checkpoint

Diagnostic harness prepared for real server logging and bounded HTTP batches.
No runtime change or diagnosis run yet; test selection follows the actual cause.
