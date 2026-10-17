# T179-Pd.3d ordinary-A independent preflight — 2026-10-17

At `a5a41cfad227ad316da96071bd7ad9c8a467c0f1`, the proposed shared prefix/tail extraction consumed by the real ordinary owners is implementable within the accepted design. NI-D01 remains closed; no new design approval or HOT prerequisite is introduced. This is static preflight, not SOURCE/PREP or runtime acceptance.

**NI-PREFLIGHT-01 — selected source-fence prerequisite.** `GameEngineTurnLifecycleTests.WorldReadPhase.cs:532–575` has **9 stale literal hashes out of 33 already on a5**. Its first failing comparison would be `FileSystemManager.cs`, before the narrative file. The mismatched paths and exact expected/current hashes are in the accompanying JSON. Updating only the future narrative hash is insufficient. Reconcile these nine against retained accepted changes and actual affected source before selecting the Fact as qualified coverage; preserve the old guard/history and all its assertions. Do not treat this as new 3d causal RED or automatically bless current hashes.

The proposed split should keep the actual JsonDocument/catch owner and the ambient timestamp call between prefix and tail. Preserve exact issue ordering, the achievements current-read outside its broad catch, original/read-delta short circuits and repeated IO, and the separate freshness/existence/read operations. The generic timestamp helper has other callers and should remain unchanged. Existing projector/serializer, single-pass tag policy, conditional UTC sample and public changed-only writer remain unchanged; NI-D01 no-write bytes/BOM/mtime and failed/uncertain-write distinctions remain binding.

The two proposed SP ordinary methods really call these ordinary entrypoints. Their spiritual filename does not justify replaying the unrelated source/clock family. The concrete tests-only packet must supply actual method/case identities, real fixture ownership and hooks, guard provenance reconciliation, and fresh preparation before runtime release. Full HOT/history/cold/3d/F1/Windows/ZIP qualification stays open.

Only repository reads, static hashes and this `/tmp` note were produced.
