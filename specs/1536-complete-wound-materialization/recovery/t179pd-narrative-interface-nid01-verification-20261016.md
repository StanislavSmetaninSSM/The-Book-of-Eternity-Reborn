NI-D01 affected wording verification — 2026-10-16

**NI-D01 is CLOSED. No new finding.** Frozen candidate `f186abee075654859d536073d8a582f213af0529` now states the exact distinction requested by the prior independent review of `d474cdd8546f8157b6ffd71840f268062ee05ed0`. The bounded shared-kernel/ordinary-consumer/source-aware no-write design acceptance remains unchanged. This verification adds no 3b, category/runtime, full S/B/M3, cold or activation release.

Canonical implementation-plan 3d line 347 and the new plan/tasks entries restrict `EncodeUtf8WithPreamble(projected.Json)` comparison to **Changed=true AND an actual ordinary public write that returned after its owning lease release**. NotNeeded/CurrentNoWriteEvaluation instead retains and rechecks exact observed raw presence/bytes, including BOM-less UTF-8 and UTF-16 BOM; it performs no forced re-encoding or normalization write. Fault/Uncertain cannot become Returned merely because current bytes match the proposed image. The future finite tests explicitly contrast these branches. This closes the earlier sentence’s ambiguity without changing an ordinary output policy.

The affected source confirms that both ordinary wrappers write only when the shared projector reports Changed. The projector returns exact decoded text on the unchanged path. Existing StreamReader BOM detection accepts the cited encodings; the UTF-8-with-preamble encoder belongs to the real writer. The public byte writer awaits owned lease release with completed:false before returning, while the existing release helper preserves publication uncertainty and does not convert a failed close into successful return. Existing ordinary projection test source retains unchanged raw bytes and mtime. These are static source checks; no tests were executed.

The immutable author MD/JSON and original reviewer MD/JSON were checked byte-for-byte against their original `/tmp` files and expected SHA-256 values. No original report was edited. The actual source/test/category/selection diff from prior reviewed d474 to this candidate is empty; this proves continuity only, not runtime or binary requalification. General retained first-pass/source/write/history association, ambient timestamp cold equivalence, repair metadata and all earlier full-stage limits remain open.

This assignment does **not** verify the new recovery helper/profile, its 145-path union, clone contents or network provenance. Those are separately reviewed recovery work. It does not broaden or delay the independent 3b release process. The scoped documentation diff also contains separate 3b/recovery status text; only the NI-D01/3d wording is assessed here.

Evidence: 12 current Git/blob/byte/SHA pins, four immutable artifact comparisons, 11 limited exact projections and three exact Git diffs. Candidate HEAD/status were exact and clean at readback. Diff base for the wording is `22e31d09999ff9cd8616116ff0e19f517a192426` → `f186abee075654859d536073d8a582f213af0529`; source/author continuity is `d474cdd8546f8157b6ffd71840f268062ee05ed0` → candidate. Exact argv, projections and hashes are in the paired JSON.

- `scoped-wording`: 15286 bytes; SHA-256 `4ae3ee989d0699b950caec2dcbfd11586a34067144e8710e166bf22a47e88b25`.
- `source-tests-catalog-continuity`: 0 bytes; SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
- `immutable-author-continuity`: 0 bytes; SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.

Configured independent GPT-6 Astra / XHigh; serving backend telemetry unknown. Root remains sole repository writer. Only local read-only source/Git/hash inspection and new `/tmp` reports were used. No repository edits, build, discovery, tests, application/native runtime, network/fetch/push or delegation.

**FINALSTOP.**
