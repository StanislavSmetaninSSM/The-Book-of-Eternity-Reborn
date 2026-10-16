# T179-Pd.3a affected default-fixture SOURCE / FIXTURE / SELECTION review — 2026-10-16

**ACCEPTED for FIRST protected Late2 only.** No residual material finding in this bounded correction. NA-01 is resolved by source inspection; the corrected default negative has not yet run. This is not a final observed-reader integration decision.

The reviewed clean preparation carrier is `e79e6c239f3461f6c7542986279ff6dd99796062`; the fresh compiled source is `8c098487398830c20bd3705261c2a2c507bce5d8`. The completed finite audit examined source `d50d1a9a98e087c8fc5a8908745dfd1e03aaac48` at frozen failed carrier `ad5859121253d7972dca51ced6f77927e9e953b4`. These identities are distinct. Requested and coordinator-confirmed actual reviewer configuration remains independent `gpt-6-astra` / `xhigh`; backend telemetry is unavailable. Root remains sole repository writer. This review used only source, Git objects, stored artifacts and read-only analysis; no build, discovery, test, application, network operation or delegation was performed.

## Correction and causal boundary

The exact runtime/test/catalog/runner delta from audited d50 to source8c is one method in `BookOfEternityClient.Tests/PendingTurnSnapshotReaderTests.cs`: `ReadCurrent_RejectsRawDuplicateManifestProperties`, lines 323–357, 15 additions / 3 deletions. Exact prefix and suffix equality proves all other methods, shared setup and `Dispose` remain unchanged. Production has zero diff from accepted implementation `00aa1c2d70e539805bd6ea72941f4c09c0c00c31`; other fixtures, selection, catalog and scripts have zero diff from d50. Source8c to carrier e79 has zero scoped runtime diff.

The old frozen failure was a fixture admission-order error: `MainAdmission.AcquireAsync` reached strict manifest classification before returning a real lease, and the already duplicated `$.files` property threw there. It never reached the default reader assertion. The completed 59-method / 131-case audit found no other current issue in this family; that audit and both failed packets remain immutable.

The correction preserves the genuine prepared original. It takes the baseline whole-tree image outside any lease, acquires the real canonical lease while the manifest is still valid, and requires the actual default reader to succeed with empty issues and a non-null Snapshot. Only then does direct `File.WriteAllBytes` inject the original duplicate-property string under that same live lease. A second actual `ReadCurrent` uses that lease; no publisher reacquisition, synthetic lease, guard bypass or production test hook is introduced. The explicit `Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json))` concatenates the same bytes as `FileSystemManager.EncodeUtf8WithPreamble` at 4337–4346. This preserves the old text writer's malformed input bytes, rather than changing the fault to a different encoding or document.

The reader's existing duplicate-property check returns `pending_turn_snapshot_reader_manifest_invalid`. The old false-result and exact issue-code assertions remain; null Snapshot is now explicit. The expected tree changes only the manifest's raw byte image, and the complete file set and every file's bytes are compared after the lease is disposed. This also checks that baseline/refusal processing did not write other state. Both full-tree captures stay outside the physical lock. Existing per-test root isolation and cleanup policy remain unchanged. This old default fixture has no strict per-case cleanup JSON; later evidence must use its actual runner/scoped-root witness and must not claim a stronger receipt.

## Fresh preparation and selection

All twelve fresh raw/local/stored/committed artifact chains were independently verified, including complete BOTH build logs, discovery logs, native argv/exit records, catalog audit, PlanOnly and summaries. Both project builds have zero errors; recorded existing warnings are retained. Catalog: native 0, `135.7541749s`, 11,718 methods, 772 categories, valid with no unmapped methods or stale selectors. PlanOnly: native 0, `7.7086587s`, exactly 8 descriptors, 59 unique methods and 131 cases. Both summaries record clean source8c and **zero executed tests**. PlanOnly's zero completed descriptors and underfilled list are expected planning output, not executed coverage.

Fresh argv are exactly:

- `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog`
- `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile tests/selection.json -PlanOnly -NoBuild`

Every descriptor, exact filter, project, method count, category and budget equals the audited d50 plan; there is no selected-method overlap. Seven finite category IDs comprise Late2 (5 min), reader1 (3 min), barrier2 (3 min), conformance35 (5 min), browser8 (5 min), default67 (5 min), and ordinary16 (two descriptors: integration1 and unit15, each 4 min). Selection remains sufficient for this one-method correction plus the unfinished bounded implementation validation. The changed default owner needs its complete 67-case retry after the required gates; ordinary16 remains unrun. The earlier completed finite audit is reused, not replaced by passing-case counts.

The four freshly built DLLs match the committed source8c manifest and current files:

| Artifact | SHA-256 |
| --- | --- |
| Client DLL in unit output | `9b81b0f87f8617606d538c928b3d746ffb326cd0bfdfc59d29e6b848454b3507` |
| Unit test DLL | `61e2f28d365c34abd8b0e913405848250dc83bd5b96e5fe79c1bacf00ca9d2e2` |
| Client DLL in integration output | `9b81b0f87f8617606d538c928b3d746ffb326cd0bfdfc59d29e6b848454b3507` |
| Integration test DLL | `25d2d61aa1397baae54b13ed1ae81f53b108ed3d190924bee92a4b5888aa4b73` |

## Historical evidence and recovery

The finite audit pair, inspection and prior affected SOURCE/EVIDENCE pairs remain byte exact. All 51 historical artifact pins recorded by that audit still match their Git blobs and hashes. The earlier conformance 34 PASS / 1 FAIL and default 66 PASS / 1 FAIL packets remain failed history; they are not relabeled causal RED or completed GREEN.

Unchanged reader1 and barrier2 PASS may be retained with their original **e522** source/binaries/execution provenance; unchanged conformance35 and browser8 PASS may be retained with their original **d50** provenance. Their actual execution SHAs and packet pins are in the JSON/inspection. The relevant production and fixture sources are unchanged, and the sole edited method belongs to the separate default class. These 46 cases are not executions of source8c or its new DLLs. No repeat is justified merely by recompilation. The new protected Late2 remains mandatory; historical Late2 does not replace it.

The held selected restore was independently checked at **source8c**, not newer carrier e79: 873/873 file bytes and SHA-256 values equal both writer Git blobs and clone Git blobs; the clone is clean and has no alternates. The receipt records GitHub-only sparse/filtered transport, task branch source8c and unchanged main `d0241e71e349fbe2020e4a41b6be7c627c81cbb4`; transport and remote readback are receipt-supported and were not repeated over the network by this reviewer. No restored build/runtime was performed. This source restore and the newer preparation carrier are explicitly distinguished.

Latest plan/tasks correctly record fresh preparation with zero actuals, SOURCE pending, first Late2 then separate EVIDENCE, retained original provenance and open final gates. Earlier UNBUILT/UNRUN entries are historical checkpoints superseded by the new preparation entry. Adjacent purpose/T197 DESIGN content is excluded. This test-only correction changes no GM-authored, emitted or public contract, so the existing no-GM-update rationale applies.

## Exact release and stop conditions

Release **only** `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -Category fr063-joint-late-completion -NoBuild` using the pinned fresh source8c binaries, exact two existing methods, one descriptor and unchanged 5-minute category budget, after preserving this review and verifying a clean metadata-only carrier.

Freeze full native/stdout/log/TRX/plan/summary/hash evidence, exact source and DLL identities, complete Late semantic oracles and genuine cleanup. Commit/push/readback and verify clean selected recovery, then obtain a **separate independent EVIDENCE decision**. That decision, not this report, may release default67 followed by ordinary16. No reader/barrier/conformance/browser reruns, other actuals, aggregate SelectionFile execution or source changes are granted here. Any unexpected result, missing evidence, cleanup failure or source/binary drift stops the sequence and preserves the packet.

A final bounded observed-reader integration review remains required after 131 distinct selected PASS cases with correctly identified mixed provenance. Full Pd/T192b, real engine admission, S/C/B/same-candidate/M3/cold P0/F1/T197/activation, natural alias-original production, legacy physical branch, Windows, D4/guided US5/actual DOM visual/D8 and T178–T198 closure remain unqualified.

## Reproducible supporting artifacts

The accompanying JSON contains exact revision/blob/SHA pins, all six diff argv and hashes, six source projections, twelve preparation chains, descriptor membership, original retained execution provenance, recovery pins and this Markdown hash. The independent inspection is `/tmp/t179pd-observed-green-default-fixture-source-independent-inspection-20261016.json`, SHA-256 `de1e5ab293185012ce13113013d4897925b95397f902b862546e3ed1cac34e07`. Full repository diffs include historical evidence/doc carriers and are archival boundaries; only the explicitly scoped correction and current status entries receive this acceptance. Lossless gzip copies accompany the raw diff files. No prior report was rewritten.

**FINALSTOP — affected SOURCE accepted; only first protected Late2 released.**
