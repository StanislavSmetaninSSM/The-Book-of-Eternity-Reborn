# F2 participating operation pins implementation plan

> **For agentic workers:** use Superpowers executing-plans and causal TDD. Root is the only writer; independent actual Sol6.1/xhigh reviews source delta and final evidence.

**Goal:** a separate client/daemon operation can use the original neutral main owner until actual finalization, without reopening ordinary writes after durable Stopping.

**Architecture:** extend the real BridgeHost pipe with one retained typed operation connection. Keep schema1 main.json, F1 persistence, canonical/worker/storage gates and T042 dispatch. A common C# participating adapter is the only filesystem writer for the affected PowerShell control consumers; no PowerShell journal or lock.

**Tech stack:** existing .NET8 C#, NamedPipe streams, real inert PowerShell functions, existing Linux owner/neutral fixture/guardian.

**Spec:** [accepted design](main-run-fence-design.md), F2 only, accepted F1 f4e7621fe85c7aa0393552708d28eec4bfe36d8f.

## Constraints and source delta

- No F3, public launch, live provider, actual saves, systemd setup, native Windows claim, environment changes, replay, cold guarantee or final stop/load/fresh-start UX decision.
- Original live object/connection grants access. Record/status JSON and PID discover/refuse only; they cannot restore a lost operation.
- Obtain pin/quiescent guard before lifecycle/canonical locks. Do not hold filesystem locks across bridge IPC, pin drain or process waits. Durable Stopping revokes ordinary writes; original closing is no-recovery/no-mutation, exact generation only. Earlier held decisions settle under their original lease.
- Main AND independent worker-purpose/inventory AND storage/generation remain required. No global all-writer claim.
- This is client-owned coordination: no game mechanics, GM model, prompts, rules or GM-created content changes.
- `GmSessionRunCoordinator.RunOperationAsync` currently admits only in-process pins. `BridgeHost.RunServerLoopAsync` currently closes after one request. `SessionOperationContext.RunBoundCoreAsync` finalizes after its callback. `FileSystemManager.MainRunFence` supplies the real pre-recovery and held-write gates.
- Actual launcher RPC is `BookOfEternityClient/Launcher/bookofeternity.ps1`; daemon is `BookOfEternityClient/game_master_daemon.ps1`. Their status/ready/error/key writes/deletes currently use raw PowerShell filesystem commands. Status JSON is diagnostic and raw stale-PID deletion must disappear.
- Actual browser Load UI entries are `GameLauncher.tsx` and `SettingsView.tsx`; both use the shared load workflow and `/api/saves/load` -> `LocalWebUiMainMenuService.LoadSaveAsync`. `/api/saves/load-state` -> `BrowserLoadStateService.BuildAsync` is the subsequent exact-generation refresh. The original quiescent guard must cover acquisition, replacement, refused/rolled-back release and required refresh. A new request is not that original guard.

## Review focus

1. Grant/reply loss before ClosedObserved retains an unresolved owner pin; no retry/mint.
2. Valid immutable close receipt retires the pin even if its reply is lost; diagnostic lookup never recreates access.
3. Stopping between callback and SessionOperationContext finalization permits only original bounded closing, with typed failure/decision preserved.
4. Real daemon/launcher writes and deletes cannot bypass admission or infer authority from status/PID; T042 body/identity remain unchanged.
5. Both browser Load callers retain exact typed result and original quiescent admission through required refresh and old UI-token release.

## Task 1 — real retained pipe and common main admission

Files: new focused `Services/GmRuntime/GmMainOperationProtocol.cs`, `GmMainOperationClient.cs`; extend coordinator, real BridgeHost peer handling and `Core/FileSystemManager.MainRunFence.cs`.

Interfaces: owner Begin(root, operationId) returns immutable original identity + opaque pin/close identity; one connection owns PreparedGrant -> Active -> Closing -> ClosedObserved, or Unresolved. Client owns one stream and ref-counted live admission, never constructed from JSON alone. Close is one immutable frame after local irreversible closure; response/lookup describes the owner's existing close state. Bound32 retained pins per root, one per connection, and bounded short-request capacity remains available. No replay of any mutation/dispatch.

- [ ] Write causal tests against the actual pipe handler: old one-request protocol refuses begin; lost connection before close cannot retire; lost close reply after receipt does not poison retirement; malformed/wrong/repeated close never mints authority.
- [ ] Run only new `gm-main-operation-pins` through scripts/test-csharp.ps1; preparation/compilation failure is not causal RED.
- [ ] Implement the typed owner/client protocol. Admission checks actual original owner, Running/release/no debt/live terminal. Disk validation below canonical locks binds exact root/run/epoch/generation/backend/host/boot. Remote loss freezes the same admission; nested scopes cannot reconnect.
- [ ] Extend real main admission to borrow original typed client pin or original quiescent guard. Active pins refuse SessionReplacement; no-recovery closing remains SessionFinalization only. No IPC below filesystem locks.
- [ ] GREEN, source/evidence checkpoint and ordinary push/remote bytes.

## Task 2 — finalization and participating consumers

Files: `Core/SessionOperationContext.cs`, new focused participating C# control adapter/early CLI entry, shared PowerShell operation helper; actual daemon and launcher; bridge status publication; affected browser load service/workflow only.

Interfaces: a main admission scope acquired before callback/prebinding filesystem work spans SessionOperationContext finalization and all canonical lease disposal. A retained helper connection executes fixed canonical control writes/deletes through FileSystemManager; PowerShell owns only inert stdin/stdout process control. Each actual turn/QTE/repair/terminal consumer borrows one original operation scope, retains T042 immutable operation, and closes once after established result/failure. Independent status operations refuse canonically when no authority; volatile diagnostics remain available.

- [ ] Write causal tests: stopped boundary between operation and actual finalization; cancellation does not cancel settlement; exact generation/worker/storage refusal; inert real PowerShell status, launcher, QTE/repair/turn consumers create no files before admission and cannot write/delete after Stopping; no ambiguous dispatch replay. Browser original guard spans both actual Load entrypoints' required refresh, and exact NotLoaded/RolledBack/Committed/Uncertain survives follow-up failure.
- [ ] Run new `gm-main-participating-consumers`, and narrowly selected affected prior regression methods only. Any required frontend check goes through a category declaring frontendBuild.
- [ ] Implement scope/closing outcome preservation and common adapter. No raw participating canonical writes/deletes, no stale PID-driven canonical deletion. Bridge snapshots under its synchronization lock and publishes after releasing it, with actual publication task settlement retained at stop.
- [ ] Keep browser low-level refusal/messages and shared workflow. Complete required refresh while original guard is held; do not exchange it for JSON/status authority on a subsequent request. No new public stop/restart policy.
- [ ] GREEN, independent source review, fix findings with focused RED/GREEN, checkpoint push/readback.

## Task 3 — controlled end-to-end qualification and final handoff

Files: focused TestSupport driver and new `gm-main-operation-native-linux` category, existing guardian unmodified; Spec Kit tasks/plan/status and exact qualification/review/handoff carriers.

- [ ] Actual fixed neutral BridgeHost and separate C# client/helper through real accept loop: retained operation, durable Stopping, original closing/disposal, exact stop; independent guardian ECHILD/no emergency. Negative connection loss retains logical owner/Uncertain; physical fixture cleanup is independent, never fake logical retirement.
- [ ] Only these new cases and actually affected regression methods through scripts/test-csharp.ps1. Record source hashes, manifests, executed counts, commands and cleanup; reuse unchanged accepted F1/input/terminal evidence by source identity.
- [ ] Independent actual Sol6.1/xhigh final source/evidence review; ordinary non-force final push, exact remote SHA/byte readback and fresh GitHub-only restoration (no new tests in restore). Handoff stops before F3.

## Codex CLI safe diagnostic (completed, independent of F2)

2026-10-06: `/opt/codex/bin/codex`; `codex --version` = `codex-cli 0.159.0-alpha.3`; help exit0; `codex login status` exit0, `Logged in using ChatGPT`. Warning: read-only filesystem prevents creating PATH aliases. No secrets/config read, authorization/install/settings change, interactive/model request or paid call. Actual TUI/live GM remains unqualified.
