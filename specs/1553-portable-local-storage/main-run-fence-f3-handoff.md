# T041-RUN-FENCE-F3 bounded neutral handoff

Accepted F2 `8b0c416e1ec5c132d82776b4fdc348ee90cfdbe5`, F1 `f4e7621f`, design `0658e440`. Independent actual Sol6.1/xhigh plan PASS at `36fb4f9e`; bounded A/B/C review PASS. Final source/evidence review is pending on this candidate. Latest runtime/test source `2fb042eee6e6cce2c793ddd9446e5e2c37423cde`. Only this neutral F3 qualification can close; full T041, production/backend/live and Linux browser storage debt remain open. No merge/issue closure or subsequent implementation is authorized here.

## Result and preserved contract

One existing schema1 main record, original owned terminal/coordinator and F2 typed pin/connection. Original application/client processes were killed at actual cuts; new processes consumed real filesystem/Load/launch admission. A valid terminal record permits only a fresh epoch after sealed scoped stop, actual I/O/disposal and publication; PID, EOF, JSON/status and guardian ECHILD never revive old authority. Warm pending Stopped ACK still holds the original guard. Genuine pre-seal output failure retains logical Uncertain after physical cleanup and owner death.

Original receipt controls operation retirement: death before receipt stays Unresolved/Uncertain; death after ClosedObserved cannot undo that receipt or repeat the callback. Actual new-process Load preserves typed Committed/RolledBack/Uncertain with exact canonical file inventory/absence and generation. Storage Uncertain preserves the specifically observed private archive candidate (exact seven archive images) as well as every old authority byte. Worker NotLoaded disposes its candidate. Failed launch changes neither, publishes no Prepared and creates no root. Worker registration is explicit cooperating GmWorkerRootContext; metadata-only inventory seeding reuses R1, not worker execution qualification or future production admission.

Clear after confirmed stop/death changes exactly the selected files and generation, leaving main stop and archive. A distinctive immutable T042 operation was actually queued on the original binding through the real pipe accept loop; original stop revoked it, returned queued-cancelled, joined drain/stop ACK, then owner death/fresh epoch produced no old text or RESULT. No second dispatcher, retry, salvage or reconnect capability was introduced. No core coordinator/persistence/pin/terminal/storage correction was needed. New internal fresh fixture admission had causal2RED→2GREEN; ordinary Create/NextEpoch behavior stays unchanged.

## Exact evidence

| Cohort | Frozen source | Latest PASS |
|---|---|---|
| admission-green | `9542eb31` | 2/2 |
| launch-a-pass | `cf123da8` | 5/5 |
| pins-b-pass | `d26f6761` | 3/3 |
| stop-c-pass | `082ba6f1` | 6/6 |
| replacement-d-pass | `3199d127` | 3/3 |
| conjunction-d-pass | `2fb042ee` | 2/2 |
| cold-boundary-d-pass | `7f53a1de` | 2/2 |

23 distinct latest PASS =2 portable admission +21 actual Linux native cases; no skips. This is source-backed reuse across checkpoints, not a final-HEAD full-union rerun. All tests ran through scripts/test-csharp.ps1 in7 narrow categories; no full suite/Fast/PreMerge or old F1/F2/R1–R3 mass repeats.

Historical41executions =27PASS/14FAIL. Two failures were causal absent fixture admission RED; twelve were separately retained fixture/oracle failures (exclusive lock reads, generation casing, BOM/binary cleanup scan, complete incoming Load namespace/private preparation). Failed iterations never count as acceptance. All37 historical actual guardians reached ECHILD with0emergency signals/failures/deadlines. Latest21 native guardians also meet those limits. Narrow post-guardian cleanup verified all37 own case roots unchanged and all known bootstrap/pipe resources absent; no unrelated PID1 zombies were touched or declared cleaned. Logical Uncertain/retained journal evidence remains in isolated roots.

Discovery-only catalog:350 categories/10968methods-files; F3 plan7descriptors/23cases, both0executions. Manifests preserve exact source GitBlob/size/SHA256, commands/TRX, actual compiler source/binary provenance, log and decompressed hashes. [Qualification](recovery/main-run-fence-f3-qualification.json), [evidence audit](recovery/main-run-fence-f3-evidence-audit.json), [execution ledger](main-run-fence-f3-plan.md), [own fixture cleanup](recovery/main-run-fence-f3-final-fixture-cleanup.json). Actual environment: SDK10.0.401, runtimes8.0.31/10.0.12, PowerShell7.5.4, Debian cc14.2.0-19. No setup/install/security/network/auth/model changes.

## Four unavailable Windows IDs and real Linux debt

These four F2 tests remain **not passed/unqualified**, with0F3executions:

- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAsync_SessionReplacementWaitsForWholeLegacyTransaction`
- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_ConcurrentReplacementWaitsForCompleteTransaction`
- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_LockReleaseFailureDoesNotRollbackCommittedMutation`
- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_ExplicitLeaseWritesWithoutAmbientAuthority`

Original F2 Linux18executed/14PASS/4FAIL: first two time out before callback; last two refuse `Canonical atomic write create-only publication requires a descriptor-bound relative publication backend.` BrowserLocalWriteCoordinator actually stages rollback backups/manifest (and recorder writes) outside UsesTrustedLocalWriter, reaching the unchanged Windows-only descriptor backend. This is a real remaining Linux ordinary browser mutation/rollback consumer, not only a legacy fixture limitation. [T031-BROWSER-ROLLBACK-LINUX](tasks.md) remains explicitly open outside F3: migrate these cooperating rollback/manifest/cleanup writes to the existing trusted local player contract, retain typed outcomes and main+worker+generation conjunction, with no save protection from the player. Portable actual browser Load/refresh acceptance does not qualify that transaction path.

## Practical path to live Codex CLI; stop now

1. Next authorized design/implementation must connect production main admission and ordinary launcher to this same original terminal/run fence/T042, replacing fixed-neutral-only admission with the intended CLI profile and game-root ownership. Package the native helper as a supported asset; do not make users compile it. The declared bounded native fallback can be the explicitly selected qualified backend; primary existing-manager systemd-user remains a required separate adapter/qualification stage. There is no user manager here, so no positive systemd claim or setup workaround. Keep Windows ConPTY/Job and qualify native Windows in a suitable later environment.
2. Qualify the actual Codex CLI terminal/profile against the supported VT subset, reliable ready/paste→fresh observation→submit, manual draft/takeover, two inputs in one process and exact scoped stop/fence. Then connect ordinary console/daemon/QTE/repair launch and game-writing flow in a separately authorized isolated root. Neither Codex TUI/provider nor production/public/game-writing launch is accepted by neutral F3. Do not change the user's GM model or authorization/settings implicitly.
3. Enabled production workers still require their independent production admission; disabled helpers do not block a no-worker main path. Preserve T031-BROWSER-ROLLBACK-LINUX and native Windows/systemd qualification as real remaining work. Product stop/load/fresh-start UX remains a later owner decision; existing low-level refusal suffices here. Real saves, power-loss/reboot, cold exactly-once and unknown-command replay remain outside this acceptance.

Prior F2 diagnostic only: `/opt/codex/bin/codex`, `codex-cli 0.159.0-alpha.3`, help/login status exit0 (`Logged in using ChatGPT`), read-only PATH-alias warning. F3 did not rerun it or issue any model request. This is availability evidence, not interactive/live GM acceptance.

## Writer closure

Record the actual independent final verdict, publish ordinary non-force final carrier and verify exact remote SHA/changed bytes; perform a fresh GitHub-only clone with exact SHA/tree/history/clean/fsck/all tracked bytes and0restoration tests. These closure actions are pending here; the final response supplies the executed final SHA/proof, avoiding a self-referential commit. Stop before any production/backend/live implementation.
