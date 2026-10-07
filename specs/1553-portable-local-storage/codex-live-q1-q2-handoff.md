# Authorized Codex continuation — environment blocked, no game turn

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), accepted diagnostic `13cdd9aedcacaba62e76859c16940583d285c365`, owner's one-TERM-answer and at-most-two-real-turn approval. [Plan](codex-live-q1-q2-plan.md), [exact qualification](recovery/codex-live-q1-qualification.json), [reviews](recovery/codex-live-q1-review.json).

**Q1 readiness and Q2 are BLOCKED. One authorized `y` was sent; zero model requests and zero game turns occurred.** The original startup lifecycle did not qualify successfully; emergency guardian cleanup is disclosed below.

The ordinary installed M1 `start-bridge visible` route used explicit NativeLineage, original terminal/worker inventory/schema1 fence and exact existing source-default command:

```text
codex -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox
```

This is the configured new isolated profile's project default, not a changed user profile or the executor model. No additional launch flags, auth/config workaround or arbitrary provider API. Installed `codex-cli0.159.0-alpha.3` executable and677runtime-only package files matched the accepted Q1 hashes; no compiler/publish/install was needed. Shell resolution was PowerShell7.5.4 and `/opt/codex/bin/codex`. Official login evidence remains the prior unchanged-binary Q1 receipt, not a new login or secret-store inspection.

## Actual boundary

At1.459s, after complete current trailing TERM warning and live original Running/status checks, the sole keyboard write was `79 0d` (`y` then Enter). Inherited `TERM=dumb` stayed unchanged. No other confirmation, terminal-query reply, paste, model prompt, Ready promotion or retry was sent.

Codex emitted a loading TUI and then this actual error:

```text
Codex couldn't start because its local database appears to be damaged.
Location: /run/codex-environment/codex-home/state_5.sqlite
Cause: (code: 14) unable to open database file
```

The CLI's generic wording does not prove corruption. A subsequent **statvfs mount-flag observation only** confirmed the parent is on a read-only mount. No DB/auth/config/session contents or directory listing were read, copied or edited. No `doctor`, repair, login, CODEX_HOME redirect, settings/security change, remount or install was attempted. Actual safe display/raw3570bytes are retained in [transcript](recovery/evidence/codex-live-q1/startup-readable.txt) / [raw](recovery/evidence/codex-live-q1/startup.raw).

The actual TUI emitted54distinct CSI sequences plus color queries: alternate screen, cursor addressing/erase, SGR, bracketed paste/focus/mouse/keyboard modes, synchronized output and terminal queries. Existing neutral-v1 does not qualify that presentation; default input-profile markers remain unsupported and frozen at production launch. Loading text is not model availability or an idle composer. No arbitrary-TUI or terminal-capability claim was added.

## Failed lifecycle and exact physical cleanup

Before `y`, the original status was OperatorNotReady, readyfalse, owner retained, no Uncertain, run `8bc88d42b525441097bf6b153dd61cd8`. After the CLI failed, its durable record became **Uncertain** with no stop evidence. The diagnostic driver incorrectly required Running after observation and aborted before its intended original shutdown RPC. This is an evidence-driver cleanup defect; the executed artifact remains immutable and its failed outcome is retained.

The only RPC was the pre-answer status. **No original scoped stop/Stopped ACK or foreground termios restoration was proved.** Driver exit1; guardian reached actual ECHILD, reaped3, **emergencySignals1**, failures0, deadlinefalse. Physical child cleanup is confirmed within the owned scope; it does not upgrade logical completion. Own foreground descriptors closed. One orphan own pipe socket was removed only after guardian ECHILD; own empty scratch was removed. The original Uncertain record/identity was retained unchanged, never cleared or replaced, and no new epoch was minted. Active tool sessions are none. [Cleanup receipt](recovery/evidence/codex-live-q1/cleanup-closure.json).

## Remaining path and owner boundary

The installed CLI needs writable local runtime state in an allowed environment with its existing authorization. Changing access/auth/config/security or copying protected state is outside this attempt; return that concrete environment requirement to the owner. The one TERM answer is consumed. Another such acceptance requires a new explicit decision; do not silently repeat it.

After this blocker is resolved, the technical Q1 slice must derive only the necessary pinned VT/profile/composer subset from this transcript, use causal narrow tests and independent source review, and retain unknown-sequence refusal. Before a future probe, fix the diagnostic failure path to attempt same-original expected-identity cleanup once after an early CLI exit, preserving Uncertain and no retry on unknown receipt. Neither task authorizes a new CLI startup here.

Q2 data should come from ordinary current-schema NewGameFlow, as the owner requested. No experimental save or injected history was used; no initialization ran before the startup blocker. Source review found initial wait retains cold main admission and prevents late bridge acquisition; this is **not causally reproduced**. A connected cancellation/Continue test may prove the smaller existing NewGame → cancel first wait → idle menu → ordinary bridge/daemon → Continue path. Otherwise a narrow post-bootstrap/pre-wait handoff must preserve generation and acquire the real original connection; do not weaken general F1/F2/T042 fences or release existing remote authority.

Source-only systemd design WIP is durably preserved at `63e990097570f920dfe615e7fdf1f3542c7a6eb9` in [systemd notes](systemd-main-design-wip.md); deferred, unreviewed and unimplemented. Primary systemd remains obligatory. Native Windows, production workers, live browser, real saves, full live gameplay and cold salvage are unqualified. Gameplay/GM-authored contracts changed none; no GM prompts/examples/rules/economy update is required.

Actual independent Sol6.1/xhigh feasibility PASS at5217f127 and source S1 closure PASS atb9b3aeb3 covered only the one-nextscreen diagnostic. Final evidence/metadata reviews and exact-tip ordinary remote/readback/fresh GitHub-only restoration are closure checks for this **truthful blocked handoff**, not gameplay acceptance. No C#/frontend tests/catalog discovery/old cohorts were run because runtime/tests/catalog did not change. Six inert evidence-driver predicate assertions were preparation only.
