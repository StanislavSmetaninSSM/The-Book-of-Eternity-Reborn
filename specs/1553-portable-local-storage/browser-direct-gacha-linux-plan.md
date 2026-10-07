# T031-BROWSER-DIRECT-GACHA-LINUX — minimal implementation plan

Issue: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Branch: `codex/1553-load-filesystem`; accepted base `23a5b6695a34752005ffaff6f5d5aedc2ccca797`.
Status: amended design checkpoint after independent Sol6.1/xhigh identified an adoption gap; focused amendment review required before implementation.
Date: 2026-10-07 UTC. Writer is the sole implementation writer.

## Intent and retained contract

Enable the existing browser direct Chaos Sea gacha on Linux using the existing trusted-local player publisher. No new gacha rules, odds, rewards, currency, GM text, request format, UX, journal, provider, or active game data. The existing pre-spend soul backup remains at its original namespace, retains exact bytes through pending-turn lifetime, and is handed to the existing pending snapshot authority and original turn backup/rollback/cleanup consumers. Main + worker + generation and original browser/UI leases remain conjunctive. Known commit/rollback remains distinct from uncertainty and cleanup debt. No new authority is minted from a backup path or JSON.

This is a bounded port of an existing flow. User explicitly authorized implementation after independent review of this minimal plan; no further product decision or approval is required inside these bounds. Standalone Daren remains the next separate task. Historical four F2 Windows-only IDs stay unpassed and are not selected.

## Source-backed defect and boundaries

- `WebUi/BrowserAfterlifeWriteService.cs:2141–2266`: real `TryApplyAsync` → held `ExecuteAtomicWithinTransactionAsync` → `StageFileAsync(fs, originalLease, soul_state, "browser_direct_gacha")` before spend. Queue/payload logic and declared cleanup directories already exist.
- `Services/ExplorerLocalTurnRollbackArtifacts.cs:339–358`: actual Stage writes exact before bytes beneath `Root/browser_direct_gacha/<ticks>_<guid>/<safe-soul-name>.rollback.<guid>`. That root differs from the current schema7 transaction root.
- `Core/FileSystemManager.TrustedLocalStorage.cs:59–74`: Linux writer selects portable publication for current BrowserLocalAccess artifacts, but excludes other rollback-root members. Stage therefore enters Windows physical publication. Merely routing Stage is insufficient: later acquisition rejects the retained backup as unsupported legacy/schema7 evidence.
- `Services/ExplorerLocalTurnRollbackArtifacts.TrustedLocalRecovery.cs`: original schema7 preflight/recovery owns transaction manifests and cleanup intents, not the long-lived pending-turn backup. Preserve unknown/unsupported schema and exact evidence on refusal.
- `WebUi/BrowserAfterlifeTurnRequestQueue.cs:66–165`: existing rollback mapping, hash and detached authority bind pre-spend backup to the same queued request. Do not replace this mechanism.
- `Core/GameEngine/GameEngine.SessionAndSnapshots.cs:1881–1910`: original `DiscoverBackups` overlays the pre-spend before-image on a later post-spend turn capture. Existing validated pending authority supplies exact rollback mapping/hashes. Original cleanup preserves explorer backups while refreshing pending snapshots. Keep original paths to retain these semantics.
- `WebUi/BrowserLocalWriteCoordinator.cs:278–400`: original transaction closes/cleans schema7 evidence on Committed, restores declared dynamic directories on failure, and preserves typed decision/debt. The existing browser result captures decision before later closing.

## Chosen technical delta

1. Keep the existing path/name and APIs. Add one narrow Linux direct-gacha namespace recognizer in the existing rollback artifacts partial class. Recognize the original ticks/GUID transaction directory and original soul backup name/GUID; retain unknown types/names. Namespace recognition grants no original live pin or destructive pending-turn authority.
2. In `UsesTrustedLocalWriter`, route the exact original direct-gacha directory/subtree through the existing publisher on Linux; Windows body remains unchanged. Writes/deletes still require original active canonical lease, existing generation, main/worker purpose admission, strict local scope and publisher receipt. No second journal, chmod/root/security/settings change.
3. Original schema7 read-only preflight separates recognized long-lived direct-gacha files from transaction evidence. It never recovers or deletes the pending backup merely on acquiring a lease. Existing B1 journal scratch for this root is admitted only through the existing publisher scratch reader, so interrupted publication settles before transaction recovery. Malformed/unknown backup or transaction evidence still refuses before mutation.
4. `EnsureNoLegacyStorageEvidence` permits only recognized direct-gacha backups plus artifacts of the actual current browser transaction, after supported preflight/recovery. Old/unknown evidence remains refused. Main and worker pre-recovery gates run first as before. The backup namespace cannot bypass generation/purpose checks.
5. Structural recognition is never turn adoption. Before the Linux original DiscoverBackups/overlay returns a direct-gacha candidate, reuse existing typed pending manifest/detached authority verification, exact mapped path/hash, and current request context (session/request/turn/action binding). Refuse and retain consumed/unmapped/mismatched residue; current schema7 staged evidence grants only its original rollback/cleanup, not adoption. This narrow guard preserves original APIs and formats and prevents cleanup debt from re-authorizing stale pre-spend soul for a new turn. Do not put full authority validation into every lease acquisition, because existing sequential cleanup may already delete one mapped file.
6. Keep actual Stage/queue/coordinator/GM text unchanged unless causal tests identify a narrowly related defect. Actual queue failure cleans its own pending state and the original transaction restores exact soul/profile/dice/absence; Committed schema7 cleanup never deletes the retained direct-gacha before-image. No pending-turn replay or automatic acceptance.

Rejected alternatives: moving backup into pending snapshot directory would let existing unconditional snapshot-directory cleanup destroy it; introducing another manifest/ledger would duplicate existing transaction + pending authority. Broadly enabling all rollback-root writes would lose unsupported-evidence refusal. The narrow original namespace is sufficient.

## Test-first sequence and categories

Publish/review the plan first. Create isolated bootstrapped current-schema fixtures, own mutable root per case; no stubs replace the browser consumer. Exact new categories are `browser-direct-gacha-linux` (positive/lifetime) and `browser-direct-gacha-linux-boundaries` (fault/admission). Split further only if measured coherence/budgets require it. Select only implicated old regressions after source review; no full suite or T031/F1–F3/M1/Load UX rerun.

First causal RED must execute real `BrowserAfterlifeWriteService.TryApplyAsync("/gacha", ...)` through original coordinator and held lease, witness actual Stage boundary, and fail its success/lifetime oracle. A fixture/compiler/precondition failure is preparation, not causal RED. Fresh build through `scripts/test-csharp.ps1` precedes NoBuild.

Positive/lifetime tests:
- Real browser pull: exact pre-spend backup, spend once, unchanged seeded rarity/dice/rules, actual queued request + manifest + detached authority, immutable history; no local relic creation.
- Fresh FileSystemManager read/mutation lease and browser pending inspector retain the same before-image after schema7 cleanup; repeated pull is blocked before another spend.
- Real original turn capture discovers/overlays pre-spend bytes, validated pending reader resolves original mapping, original rollback restores exact soul, and scoped cleanup retires only consumed backups. Actual existing methods may be invoked reflectively by the fixture; no alternate implementation.
- Real committed schema7 cleanup fault: storage remains Committed with debt; recovery cleans only transaction evidence and retains pending backup/request/history.

Fault/admission tests:
- Controlled nontransient Stage publication cut and actual queue publication cut: exact rollback and original runtime/profile/dice/absence, no false success or leftover admitted pending turn.
- Publisher scratch/recovery at the real Stage path explicitly covers MemberStaged with scratch present/destination absent, MemberPublished, and committed cleanup debt. Scratch is admitted only by the existing authenticated journal reader; an unauthenticated lookalike is refused and retained. Interrupted decisions resolve through existing B1 mechanism; unknown bytes remain uncertain/evidence retained.
- Real consumed-backup deletion failure followed by original pending manifest/authority cleanup and fresh console capture must refuse, retain debt and never overlay stale soul. Also reject a second valid-shaped unmapped backup and mapped bytes whose authority hash differs. All start with actual browser-created evidence; no stub consumer or fabricated successful handoff.
- Original generation revoked after publication, failed cleanup/closing, malformed/unknown retained evidence: no false rollback/commit, no automatic cleanup or replay.
- Actual browser consumer refuses cold main Running/Stopping, real unresolved Prepared worker, stale/original UI guard and generation loss before side effects. Reuse original accepted pin/admission mechanisms; no invented owner.

Regression selection rationale: changes affect portable writer routing, schema7 preflight/recovery, artifact cleanup, original turn capture and generic original lease admission. Choose exact current successful tests covering those boundaries, rather than old Windows-only fixture creation or all gameplay cohorts. No frontend source is proposed; payload/state contract unchanged, so frontend execution is needed only if a causal consumer change reaches that layer.

## Verification and recovery

Use `pwsh -NoProfile -File scripts/test-csharp.ps1 -Category <exact selected IDs>` for build/RED/GREEN. Record source Git blobs/SHA256, exact commands/expected-completed counts/TRX/build provenance and own root cleanup in evidence manifests. Tests use no live browser/provider or GM model request. No process fixtures are currently needed; if actual owned process is necessary, use independent bounded guardian before qualification. Do not count unavailable Windows bodies as passed.

Verified current tools: SDK10.0.401, runtimes8.0.31 and10.0.12, PowerShell7.5.4, gcc14.2.0 (Debian14.2.0-19). `specify` global CLI is absent; existing `.agents/skills/speckit-*` and `.specify` are present and applied without installing/configuring anything. Preserve feature #1553; do not run setup-plan over existing artifacts or initialize another feature. Optional Git hooks are satisfied by ordinary checkpoints; existing agent-context pointer remains correct.

Spec Kit consistency: accepted trusted-local player contract, client-owned storage port, original pending/accepted-turn/history ownership, no gameplay/GM-authored changes. Mortal World/afterlife prompts/examples/source guards require no new text because odds/rules/payload/authority format are unchanged; record this rationale in final qualification. User-approved Load UX is accepted and outside this slice. Standalone Daren/systemd/nativeWindows/Q1Q2/liveGM/saves/cold/full-game acceptance remain open.

After bounded blocks: ordinary commit/push, remote SHA and independent byte readback. Separate actual Sol6.1/xhigh source/selection and final evidence review. Exact final fresh GitHub-only clone with all tracked bytes/tree/parent/ancestry/clean/noalternates/fullfsck; source restoration executes no extra tests. Handoff and stop before another slice.

## Independent design finding and amendment

Sol6.1/xhigh review of13e6b8b returned BLOCKED for one concrete adoption gap: existing CleanupBackup logs/swallow delete failures; following CleanupPendingTurnSnapshot preserves explorer backups while removing manifest/authority, and later DiscoverBackups selects/rehashes newest matching before-image. Shape-only storage admission would expose that stale adoption path on Linux. Amended step5 separates storage recognition from exact existing-authority/current-request adoption; adds consumed-cleanup/unmapped/hash-conflict negatives and explicit journal-authenticated scratch-only/unauthenticated-scratch tests. No implementation/tests performed yet; focused amendment review next. This is turn/receipt consistency, not protection from player-owned save editing.
