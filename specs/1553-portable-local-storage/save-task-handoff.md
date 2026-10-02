# Ordinary save continuation — OPEN

## Current caller checkpoint — 2026-10-03

Tracked work is [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), [approved spec](spec.md), [plan](plan.md) and unchecked T032-A2/A3 in [tasks](tasks.md). The published caller implementation checkpoint is `dafd37e589eaf6fd4e05a996fd3ecf00e74e4a97` on `codex/1553-save-windows`. Restore normal Git source; no patch carrier is active. The Windows storage prerequisite is independently accepted. Caller verification and independent caller review are still pending; this is not overall save or platform acceptance.

The source now implements ordinary SaveGame and its immediate callers:

- One continuously held snapshot lease prepares and closes a create-only ZIP; its file image is published once through shared B1. Exact archive schema, manifest and payload exclusions remain preserved, and only owned private staging is cleaned.
- Listing uses validated leased seekable streams, raw ZIP preflight and existing metadata/expansion bounds. Ordinary profile refresh uses Task-only mirror publication and byte-exact no-op handling; original LoadGame physical receipts and load callbacks retain their original authority.
- Console/browser/autosave callers retain committed, rolled-back and uncertain decisions through follow-up work. Retention uses bounded file images and stops when storage is unresolved. Browser DTO/HTTP/UI retain the exact committed destination, including committed follow-up at an HTTP error status. Uncertainty or a lost response stops refresh and further shell actions; already dispatched requests are not claimed cancelled. The blocked screen retains any known created save ID and calls for reconciliation before reloading the window.

## Verification and unresolved finalization boundary

At frozen `dafd37e5` binaries, the separate [core Windows GREEN](recovery/evidence/save-core-windows-green-20261003/manifest.json) completed **19/19** cases in **27.566 seconds**: ordinary reads/profile refresh 9 and save outcomes 10. Both descriptors completed, no timeout occurred, and owned/runtime cleanup succeeded. This confirms that bounded core cohort only.

The separate [caller boundary Windows GREEN](recovery/evidence/save-caller-boundaries-windows-green-20261003/manifest.json) completed **13/13** at frozen `dafd37e5`: browser prepared/HTTP/source guard 7, console presentation 4 and public entry 2, **1:03.504**, all three descriptors complete with no timeout and complete owned/runtime cleanup. Engine continuation and frontend verification are separate.

A different fresh-build invocation at the same named source is preserved as [player Windows RED](recovery/evidence/save-engine-player-windows-red-20261003/manifest.json). Unit and integration builds succeeded, but the player category exceeded its **two-minute category budget**; command wall time was **4:31.458**. Buffered output proves the first case reached the actual save cut once, then unchanged bound-operation finalization masked the typed primary save outcome with `InvalidDataException`. There was **no final TRX**: formal summary counts are **0 executed / 0 completed descriptors**, not a completed failing cohort. The core descriptors were unrun in that invocation; their separate GREEN is not attributed to it.

A shared `SessionOperationContext` correction is underway. Preserve the typed primary outcome through actual unchanged finalization while retaining session-replacement priority; a bounded independent Astra consultation is recommended for that concrete boundary. Do not bypass real finalization or redefine its acceptance to match a fixture. Caller acceptance remains open until the corrected boundary, related console/browser/engine owners and independent Astra XHigh review are complete. Four new frontend tests drive the actual settings save handler and continuation latch; they **await execution**. Backend response serialization/component checks do not establish deferred live browser GUI acceptance. No injected native lease-dispose fault proof is claimed.

## Linux handoff and preserved scope

Windows and Linux paths are implemented under the owner's explicit authorization to implement locally and defer native Linux execution. **These new save prerequisite/caller paths have not run on Linux.** Windows results cannot close native Linux FIFO, cold-process, generation/admission, resource or caller qualification. Full T032-A1/T032-A, T031/T032, accepted-turn/gameplay, provider and whole-game/platform acceptance remain open. Load/library-preserving replacement and B4/B5 keep their separate [load scope](save-load-cutover.md) and [admission](save-load-admission.md).

Use bounded owners from `tests/categories.json`, rebuilding once before `-NoBuild`. Keep resource qualification separate from caller checks:

```powershell
./scripts/test-csharp.ps1 -Category portable-storage-stream-cold-phases,portable-storage-stream-cold-before,portable-storage-stream-cold-conflicts -Parallelism 1
./scripts/test-csharp.ps1 -Category portable-storage-stream-generation,portable-storage-image-admission,portable-storage-stream-linux-fifo -Parallelism 1
./scripts/test-csharp.ps1 -Category portable-storage-stream-resource -Parallelism 1
```

Caller owners are `portable-save-entry`, `portable-save-read-refresh`, `portable-save-outcomes`, `portable-save-consumer-contracts`, `portable-browser-save-creation`, `portable-browser-save-presentation`, and the separate `portable-engine-save-presentation`, `portable-engine-save-player`, `portable-engine-save-waiting`, `portable-engine-save-late` categories. Select related owners in small commands after the pending finalization correction. The Linux FIFO owner must actually run on Linux. The historical whole-byte experiment remains a diagnostic, not the streaming resource gate.

No GM-authored response, game mechanic, Mortal World or afterlife contract changed. These save outcome fields are client-owned transport/presentation; no GM prompt/example capability update is required.

## Historical sources and evidence

The [preserved cutover map](save-creation-cutover.md) describes the original source before caller implementation. Its statements about absent production callers and old SaveGame publication are **historical**, not the current checkpoint.

- Windows prerequisite source `23d94fb7` was independently accepted at evidence `0f4fe247`: [cold 14](recovery/evidence/stream-windows-green-20261002/manifest.json) belongs to `ecf0a923`; [format 29/resource 3](recovery/evidence/stream-windows-resource-green-20261002/manifest.json) to frozen `ec5e5ccf`; [remaining boundary 14](recovery/evidence/stream-windows-boundaries-green-20261002/manifest.json) to `23d94fb7`. The real near-512 MiB archive and all fifteen owned resource children are recorded in the resource bundle. [Fresh GitHub restoration](recovery/stream-windows-restoration-20261002.json) preserves the source/artifact identities. These prerequisite cohorts are not caller executions.
- [Initial public-entry RED](recovery/evidence/save-caller-entry-windows-red-20261002/manifest.json), [helper RED](recovery/evidence/save-helper-windows-red-20261002/manifest.json), [outcome RED](recovery/evidence/save-outcomes-windows-red-20261002/manifest.json), [browser backend RED](recovery/evidence/save-browser-windows-red-20261002/manifest.json) and [browser presentation RED](recovery/evidence/save-browser-presentation-red-20261002/manifest.json) retain distinct original sources and failures. Five frontend notice scenarios executed and failed in the preserved frontend stdout; a failed Node file must not be reported as zero exercised scenarios solely because adapter summary counts are zero.
- Earlier main integration source [b2111b7c](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/b2111b7cd0f66414d69af3d06751f617d8285948), base [ebd808e9](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/ebd808e93ea60b5434396445b18738ae22f09d05), [integration identities](recovery/create-only-main-integration.json) and [4,029-file restoration](recovery/create-only-main-restoration.json) remain historical. Original-v1 and create-only isolated proofs preserve their own runtime/source identities in the [imported manifest](recovery/evidence/create-only-adapter/manifest.json) and [isolated restoration](recovery/create-only-adapter-restoration.json); none becomes execution of later caller source.

An earlier worker's generic security flag had no identified triggering operation; it is not a present blanket project denial or proof that the earlier diagnostic was approved. Do not replay that unidentified diagnostic or historical carriers. If an actual denial occurs, stop and report that exact action. Every return checkpoint must distinguish tested source, evidence-containing source and current source, and include reached boundaries, formal counts/timeouts, owned cleanup, actual OS/toolchain and independent review.
