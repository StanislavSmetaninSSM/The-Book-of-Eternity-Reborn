# GM1557 connected source: first actual evidence and fixture review

Verdict: **EVIDENCE/status ACCEPTED**, with **one intended source RED and two fixture failures**. This is not delivery qualification or an accepted connected GREEN. Further execution through the shared process fixture is held for a focused correction and independent SOURCE/FIXTURE/SELECTION review.

Reviewed carrier: `7c2f961ed2834230771231edf3fa4b90ea109fde`. Tested/compiled source: `4f867b2ae9e9399941b01a455b8c89cc576ffae1`; accepted implementation: `2f4e6d0f685d2f98a64b6474f68f41a8f25aacc5`. This review used local read-only files and wrote only this external report. No Git, project, build, catalog, PowerShell-parser, test, runtime, provider, environment-management or agent operation was performed. Requested configuration was retained Astra XHigh; no model substitution or delegation was used. The tools do not independently expose model/effort telemetry.

## Evidence verification

- All **87** stored gzip length/hash and decompressed length/hash chains match the manifest; all **87** raw originals match. All **59** retained native artifact originals also match their frozen bytes. Manifest SHA256: `b5cf49ad0a947871fc74cf53868c276ca623f6ceea787e92d4c1e1370d49121e`.
- Of **2331** source pins, **2329** still match directly. Removing only the new 48-line outcome insertion from each active plan/tasks file reproduces its frozen pinned bytes, preserving historical content and checkbox lines.
- All **233** current physical binary/package pins match. The preparation initially pinned **10** binaries; **223** app-output/package pins were added afterwards. These are physical-file checks, not an initial 233-pin claim or a loaded-assembly observation.
- Native preparation completed in **133.8056559 s**, exit 0, integration/unit warnings **96/35**, errors 0. Catalog was valid **731 categories / 11584 methods**, unmapped/stale empty, tests executed 0. Exact PlanOnly completed in **2.5093283 s**, **6 methods / 15 cases / 2 descriptors**, tests executed 0.
- Actual `20261014-002814-027-89240-17f30b358c6a4d77b39956cd1e2eda27-categories` completed its first descriptor in **36.5844494 s**, exit 1: **14 executed, 11 PASS, 3 FAIL**, no skips, duplicates or timeout. Selection completion is **1/2 descriptors and 14/15 cases**. The second descriptor, the one real packaged-writer case, was not executed.

## Actual classifications and reached boundaries

**Intended source RED:** `FinalDaemonSourceKeepsScopedDirectionsAtRealDispatch`, fixture line 52, reached the source-rule oracle with exactly `source.router_dependency_missing` and `source.bootstrap_source_exception`. Its inspect child completed, returned exit 0, and retained a completed AST report. This is a bounded source-contract failure in the current daemon, not actual writer/delivery evidence.

**Eleven passing cases:** packaged AST boundary 1; source mutations 5 (`repair_ban`, `receipt_ordinary`, `late_empty_reset`, `late_override`, `receipt_post_builder`); memory controls 4 (`escape`, `missing_copy`, `broken_anchor`, `fenced_anchor`); physical symlink 1. Their original reached assertions provide only those finite scopes.

**Two fixture failures:** source mutations `wrong_boundary` and `read_all` failed at `GmDocumentationDeliveryTests.cs:68` with a null dereference of `changed.Ast["seed"]`. Their native primary failure is earlier: `delivery.retained_identity_changed`, from `GmDocumentationDeliveryScenario.SameAlive:330`, through `Census:349`, called from `RunAsync:184`. Neither case reached its seed/mutation assertions, so neither is a passing negative or intended source RED.

The retained artifacts narrow the diagnosis without replacing the failed test outcome:

- `wrong_boundary`, PID **89466**, retained AST `completed=false`, `ast.startup_boundary`, and a completed seed.
- `read_all`, PID **89411**, retained AST `completed=true` and a completed seed.
- Both returned inspect observations omit the populated AST and native exit code because the identity exception preceded report loading. The final recorded cleanup succeeded. Neither observation records the conflicting argv or process state. A zombie, empty argv, exact exit race, or other particular OS transition is therefore **not an observed fact**.

## Confirmed source boundary and minimum correction

The process loop tests `child.HasExited` at `Scenario.cs:182`, then independently calls `Census` at line 184. `ReadIdentity` at lines 317–323 reads `/proc/.../stat` and then `cmdline` separately; it discards the stat state and does not establish a stable observation across those reads. `SameAlive` at lines 325–331 treats an argv difference for the same birth value as an identity violation. The process can exit between the outer check and these reads. This is a concrete observation window capable of misclassifying an already-owned process's terminal transition; the retained completed AST files and later EOF/exit are consistent with it, but do not prove that it caused these two runs.

The null dereference is independently confirmed: the caller dereferences `Ast` before surfacing the earlier failure. It must report an unmet fixture prerequisite instead of masking it, while retaining the intentional `wrong_boundary` AST-failure contract.

A bounded correction should:

1. Distinguish exact live identity, a conclusively terminal observation of an already-owned identity, and unknown/mismatched identity. Use the retained process and stable birth/state observations around argv acquisition where needed, under the existing deadline. Only an exact live birth/argv match may authorize a signal; terminal recognition must never adopt a process or grant signalling authority.
2. Record the relevant expected/observed owned PID, birth, state and argv evidence, with read ordering and managed exit observation where available. Preserve the original exception and independently retain the AST/log evidence even when the ownership prerequisite fails.
3. Add an explicit fixture-result prerequisite before dereferencing the seed. Do not retroactively turn these two cases into PASS from their retained AST files.

An empty argv alone is not exit proof. Do not waive argv checks, blanket-catch `retained_identity_changed`, signal unknown processes, expand to a recursive tree kill, or erase a mismatch because eventual EOF was observed. An unresolved/live mismatch remains a failure with the required evidence/root-retention behavior. No general supervisor, production change, broader fault suite or diagnostic-only rerun is needed to establish the already captured failure boundary.

## Cleanup and status limits

All **nine** inspect observations record initial identity/pipes/census proof, exit confirmed, both EOFs, no cleanup failures, no unknown descendants, no remaining owned processes, and roots removed. Their **18** package/session roots are physically absent. The separate physical-symlink receipt records `Removed=true`, and its root is absent. The **nine** evidence directories remain retained. Runner-owned-tree and runtime cleanup are true. These facts do not reconstruct the missing argv/state observation or retroactively prove a harmless transition.

All nine process cases were inspect-only; retained AST reports show no prefix runtime execution. There is no actual main-operation producer or published-prefix proof. Active plan/tasks accurately distinguish the intended source RED, the two fixture failures, initial versus appended physical pins, the 11 finite passes, and the unrun writer. No material reporting defect was found.

## Next bounded gate

Do not release the independent real-writer attempt on this shared fixture yet. First review the actual bounded identity-observation and failure-reporting correction, with source/fixture/selection checks and matching fresh preparation as required. Then select the two affected failed cases without repeating unrelated passes, plus the still-unrun real writer under an explicitly reviewed finite selection/order. If the official runner cannot isolate theory rows, any wrapper/extraction must preserve the other rows and their oracles and receive SOURCE review. A passing-case repeat needs a concrete changed dependency, not an automatic full-owner rerun.

This evidence review releases no content move, daemon implementation, delivery/AST completion claim, Block 5 work or full A qualification. Prior pure 44 and primary J1 remain outside this review.

**FINAL STOPPED.**
