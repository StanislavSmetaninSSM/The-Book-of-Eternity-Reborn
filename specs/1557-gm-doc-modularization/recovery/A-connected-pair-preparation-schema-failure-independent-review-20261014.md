# GM1557 pair preparation schema failure — independent review

**EVIDENCE/status ACCEPTED. Confirmed Important catalog defect; corrected SOURCE/SELECTION closure remains required before any retry.**

Carrier `0ad14e2fad02408d3337d3c7ea741e81b2b19685`; attempted preparation source `5091bdff75ee3277f35c83d7f5b53453ce5e1014`. Review is read-only apart from this external report. No Git, project, PowerShell parser, build, discovery, test, runtime, provider, environment-management or agent operation was performed. Requested retained configuration is Astra XHigh; no substitution/delegation, and no independent model/effort telemetry is exposed.

## Evidence and classification

All **five** compressed lengths/hashes, decompressed lengths/hashes and retained raw originals match. Manifest SHA256 is `8243b238ab281899e07745ab396a6548a6e39ccfe062bf09e6c1c98f6a444b27`.

The frozen console reports `Read-TestCategoryCatalog` at `scripts/test-csharp.ps1:57`: property `id` not found. The retained wrapper readback records no new run directory and then `AssertionError: ('Expected one run directory', set())`, wrapper exit **1**. The native PowerShell process exit was not retained and remains **UNKNOWN**. The wrapper exit must not be substituted for it.

This is a **preparation/setup failure before compilation, discovery, catalog validation or test execution**. There is no fresh binary qualification, native summary, plan or TRX. Pair2 and writer1 remain UNRUN. It is neither an intended mutation/source RED nor a runtime/ownership failure. No application fixture or fixture-cleanup qualification arose from this attempt. No security refusal is evidenced.

All **2332** frozen source pins were checked: **2330** currently match directly; removing only the new preparation-failure status insertion from each active plan/tasks file restores the remaining two pinned lengths and hashes exactly. Current scenario and fixture hashes still match the accepted source block (`333994d5…` / `b84a50ed…`). Active metadata faithfully preserves the missing native exit, zero executions, historical actual packet, and pending correction; no reporting blocker was found.

## Confirmed source cause and minimum correction

`tests/categories.json`, category `gm-doc-afterlife-connected-failed-source-pair`, contains:

```json
"related": ["gm-doc-afterlife-delivery-source-guards"]
```

`Read-TestCategoryCatalog` runs under strict mode and stopping error preference (`scripts/testing/TestCategoryCatalog.psm1:1–2`). Its second catalog pass dereferences each `related.id` and requires nonempty `related.when` at lines **76–80**. A string has no such `id` property. The exact inspected loader hash is `5d465225bde76c5286a1b4a25cdef16a3e891bc8993c975f1252a538917be45d`; catalog hash is `0cdb7317f1ca320e08bdcfd2dac98fd98a1032c4386a316212196e43502e7f64`.

A read-only structural scan of all **257** related entries finds exactly **one** malformed entry: this new pair link. Its target category exists. This source cause matches the native diagnostic, and the call at script line 57 precedes result-directory setup and build/discovery. No further diagnostic execution is necessary to establish it.

I missed this `related` element schema in the prior `53edbe4d…` static review. JSON parsing, exact selector membership and count checks did not establish conformance to this loader. The unchanged process-fixture analysis remains applicable; the catalog prevents its execution until corrected.

The narrow correction is either:

- Preferably replace that string with one object containing the same `id` and a nonempty `when` explaining the intentional overlap and separate narrow retry; or
- Use `related: []` while retaining the relationship and non-combination constraint in responsibility/exclusion text.

Keep category ID, selectors, membership, case counts, budgets and both fixture bodies unchanged. Do not relax the loader, add a compatibility fallback or change the runner. Preserve the frozen failed packet and historical SOURCE report; record this correction as a subsequent checkpoint.

After a published catalog-only correction, focused SOURCE/SELECTION closure must verify the actual object/empty-array form, existing target, preserved selection and unchanged code. Only then retry the failed fresh BOTH build/catalog preparation. There was no successful fresh compilation here, so old binaries cannot justify `-NoBuild`. Expected catalog **732/11585** remains provisional until discovery. Follow a successful preparation with exact pair PlanOnly **1 method / 2 cases / 1 descriptor**, budget2/default7, and only the already scoped pair actual after release. Freeze/EVIDENCE before the still-unrun writer. This diagnosis itself releases no retry or broader work.

## External orchestration observation

The current external `gm1557-root-run.py:52–56` now writes `nativeProcessExitCode`, `newRunDirectories` and completion time to a post-process file **before** asserting one new run directory. This is appropriate future evidence preservation and does not relax the official runner. It was read, not executed or independently syntax-tested here. It cannot reconstruct the old native exit or change the immutable five-chain packet.

No passing-case repeat, daemon/content/Block 5 change, full A/B qualification or primary task release follows from this review.

**FINAL STOPPED.**
