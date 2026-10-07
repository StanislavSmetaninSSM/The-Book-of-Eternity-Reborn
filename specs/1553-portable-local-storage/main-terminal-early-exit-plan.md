# Original main terminal early exit — bounded causal diagnosis

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Task: T043-EARLY-EXIT. Base: b4994b8ef41306c334ff8b2270d5e0f208baf4fa.
Method: Spec Kit / Superpowers systematic debugging, TDD, independent Sol6.1/xhigh review.

The failed live receipt remains immutable: one allowed y, zero model/game turns,
SQLite14 on a read-only mount, driver aborted before shutdown, guardian emergency
cleanup, retained logical Uncertain. Do not repeat that Codex startup. Systemd WIP
63e99009 remains deferred. No GM/game schema, economy, prompt or model contract changes.

## Known facts and hypothesis

- The executed driver wrongly requires Running after observing CLI failure and
  therefore sends no original shutdown. This is a proven driver defect; update
  its future cleanup path without rewriting the executed receipt.
- `LinuxPtyStream` already converts read EIO to EOF. Leave it unchanged.
- `BridgeHost.StatusPublication` holds an original operation pin while awaiting
  `BeforeStatusPublication`; a later canonical write rejects actual RootExited.
  Before explicit BeginStop, that generic rejection currently becomes Uncertain.
  This is a source-backed production hypothesis, requiring an actual causal RED.
- `GmSessionRunPersistence.Invalid()` also represents metadata/CAS/fsync faults.
  Never classify all marked failures during root exit as harmless revocation.

## Small implementation sequence

1. Add two isolated real production-route Linux scenarios using the retained
   original main/worker inventory, configured inert CLI and actual pipe accept
   loop. A bounded fixed `exit-error` command exits the CLI with17; separately
   observe original pwsh-leader RootExited (its exit code is not assumed to be17).
   Enter a selected status publication while Running, hold its existing hook,
   send that inert command through the pipe, observe original RootExited, release
   the hook, and **seal/drain that selected publisher before any BeginStop**.
   This exact settlement must prove the positive RED without stop masking it.
2. Positive case expects lifecycle-only withdrawal without Uncertain, followed
   by exactly one original identity shutdown: actual scoped stop, I/O settlement,
   disposal and durable Stopped/ACK. Root exit/EOF alone never proves stop.
   Negative case throws the **generic marked persistence Invalid IOException**
   from the held hook after RootExited; it must retain Uncertain/owner and refuse
   later writes/input. Original scoped cleanup still must reach guardian ECHILD
   with zero emergency signals, without logical success or metadata clearing.
3. Only after causal RED, introduce an original-owner-specific root-exit
   admission refusal and catch only that classification for status withdrawal.
   Independent guard, identity, metadata bytes/debt, worker/storage and authority
   checks precede it. Generic marked metadata faults remain Uncertain. Cover both
   pinned canonical access and post-exit pin acquisition; no new write right.
4. Retire the historical one-off live driver, keeping executed bytes immutable.
   The new neutral causal driver attempts one same-original pipe shutdown even
   when publication observation failed/record ceased Running, before assertions.
   Future live diagnostics must use this cleanup rule. Unknown replies remain
   unknown; never replay or automatically restart. No duplicate Codex probe.
5. Run only new `main-terminal-early-exit-linux` and precisely affected status
   retirement regressions (fault/stall, and selected admission if membership
   permits exact selection), through `scripts/test-csharp.ps1`. Structural catalog
   update, discovery validation, fresh build, planned/completed counts, native
   compiler provenance and original cleanup evidence. No old full cohorts.
6. Independent source/evidence/metadata review, ordinary checkpoint push and
   remote SHA/byte readback, fresh GitHub-only restore. Keep live acceptance open.

## CLI feasibility boundary

Installed Codex0.159.0-alpha.3 supports `sqlite_home` / `CODEX_SQLITE_HOME`, but
pinned official commit3b01b36fa5eb96ba82a776bd3c2fc57f8969181f initializes a new DB
by importing existing rollout history; new rollouts still use CODEX_HOME/sessions.
SQLite-only relocation therefore conflicts with the owner's no-hidden-history
copy/no-protected-write limits. No override, doctor full run, login or private
storage read is authorized here. Capture public source pins and two help-only
invocation receipts; a safe fresh authorized writable CLI environment is needed.

OpenCode fallback is now authorized if already installed, with separate test
profile/model and at most two genuine turns. Activated PATH plus installation
metadata under /opt, /usr/local/bin, /workspace/.onboarding/tools and child bin
locations currently finds no OpenCode. Do not install or invent an available
model. Official `opencode models [provider]` inventory is the next read-only step
once an installed executable is supplied. Public provider listings do not prove
account access. Main user GM command/model stays unchanged. If no installed
fallback exists, finish this bounded defect checkpoint and return the exact
environment/install/login decision required before live play.
