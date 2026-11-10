Focused corrected review: **PASS for the pure T179-Pa/T180a calculation prerequisite.** T179PA-F01 and T179PA-F02 are resolved; no remaining material or minor findings in the correction scope.

Reviewed corrected source `f679ea25f70112addc15ea76fab1df0db9dfd01b` against initial source `89e4d7252e072000e7d49c043abc998d0bcd5f34`. The evidence carrier is `407243bc589bed165c2af883763aaa400fedda60`; its code, tests, category and selection are byte-identical to the tested source. Requested/configured by the parent: `gpt-6-astra`, `xhigh`, isolated `fork:none`; actual backend telemetry remains unknown. The initial report pair is preserved byte-identically.

- **F01 resolved.** `CharacteristicsService.MortalJointAutomatic.cs:123` now uses the actual `FileSystemManager.EncodeUtf8WithPreamble` and existing serializer options for changed roots. The encoder is pure byte allocation. Untouched bytes remain untouched. The all-three-root preamble assertion and genuine old-owner whole-byte comparisons passed after first reproducing the defect.
- **F02 resolved.** Recursive case-insensitive uniqueness still runs through `StrictJsonAuthority`. Deserializing that validated JsonElement with the ordinary JsonObject options restores case-sensitive access, and explicit checks reject lone aliases of every consumed field on the optional and required roots. This avoids both accepting an alias and silently treating it as an absent field. Four existing regression cases now pass.

The fixture retains independent literal expected values before its whole-byte assertion. Level2/strength3 proves the training cap at4; level100/strength99 proves the independent100 ceiling while training cap is200. Both execute the genuine old training owner on unique fresh roots, and both roots were removed. The revised JSON test helper accepts the owner's permitted BOM; independent raw-byte assertions supply the encoding oracle.

Only the calculator and its test file changed on the production/test/catalog/selection surfaces.29 of32 initial pins are unchanged; the changed pins are the calculator, tests and plan ledger. Unchanged owner/decoder/uniqueness/immutable-data dependencies were checked by exact hashes. No-work absence/bytes, metadata policy, status/points siblings, arithmetic, source priorities, dedup and immutability remain intact. The pinned production symbol search finds no new runtime caller.

Preserved parent evidence was parsed and hash-verified without executing runtime:

- Regression RED `bda2747cdf341d8d6d462edab01c846838bba399`:39/39 executed,32PASS/7FAIL, exit1, no timeout/duplicates, complete cleanup,41.2342220s. The failures are exactly three encoding failures and four aliases accepted without an exception. Both real-owner controls reached correct mechanics before failing their byte assertions. Unit build succeeded with0errors.
- Corrected GREEN `f679ea25f70112addc15ea76fab1df0db9dfd01b`:39/39PASS,0FAIL, exit0, no timeout/duplicates, complete selection/cleanup,42.5708988s. Plan contains14methods/39cases; TRX contains39 distinct test IDs and39 distinct execution IDs. Both real-owner receipts report Removed=true and both roots are absent. Unit build succeeded with0errors.
- Corrected catalog at the same clean source: fresh unit+integration builds with0errors; Valid=true,11682methods,760categories,0unmapped/0stale, exit0,31.9812738s. It executed0tests and is inventory evidence only.
- All11 new regression/corrected artifact stored/raw hashes and byte lengths match their pinned indexes. The two initial review hashes also match exactly. Detailed source/evidence pins and exact diff command argument arrays are in the adjacent JSON.

`fr063-automatic-calculation` remains a sufficient finite selection for this corrected pure prerequisite. **No first-Late2 prerequisite is introduced:** the new FileSystemManager reference is a static encoder, with no filesystem operation, and no protected original/header/G/source/common/output/publisher owner changed. Actual integration must apply its own gates.

Exact principal diff commands (there is **no `--full-index` flag**):

```text
git diff --binary 89e4d7252e072000e7d49c043abc998d0bcd5f34 f679ea25f70112addc15ea76fab1df0db9dfd01b
SHA256 c28b85e84b30fae657c7ac4b9a3f7becdf43ad85e48c2a8bda1a4a29a741c732
git diff --binary f679ea25f70112addc15ea76fab1df0db9dfd01b 407243bc589bed165c2af883763aaa400fedda60
SHA256 97d9da8b709c83100494bbce8562146ddf9720750f9377b931721624bd636fec
```

`git diff --check 89e4d7252e072000e7d49c043abc998d0bcd5f34 f679ea25f70112addc15ea76fab1df0db9dfd01b` exited0. Full argv, cwd and hashes for all four recorded diffs are in JSON.

This acceptance covers only detached automatic calculation. Full T179/B01, T180/B02, genuine source/C/B/M3 integration and F1 remain open; T197/DEP-C/I/L and newQTE activation remain blocked. No separate binary-hash, native Windows/storage, publication or gameplay qualification is claimed. Continue genuine integration under its stage gates; do not rerun this unchanged passing category without new impact or failure.

Repository remained read-only. New reports were written under `/tmp`; initial reports were not rewritten. No reviewer builds, discovery, tests, runtime, commits, pushes or subagents. **FINALSTOP.**
