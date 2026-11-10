# US5-RO1 corrected preparation and exit-reporting qualification

**Preparation qualified. Release `NoBuild` catalog, exact Plan, then the two owning categories on the fresh `193dc5890` outputs. No rebuild is justified by this reporting discrepancy alone. Corrected runtime GREEN remains UNRUN.**

Scope: independent read-only review of the new preparation packet, exact recorded harmless probes, source/output pins and equivalence to accepted `60574090`. Current clean carrier: `1074c330089cdbca240b5d09e5fa5ed921eab42f`. No code, tests, category/selection or runner changed across that interval; its 15 changed paths are recovery evidence and plan/tasks fronts. Requested Astra XHigh configuration retained; actual backend telemetry unavailable.

The original observations remain distinct and immutable: preparation child stdout captured **NATIVE_EXIT=0**, official summary reported **exit 0 / 156.6964535 s**, but outer completion chunk `06a9bc` (session `90681`) reported **exit 1**. The freeze's outer `nativeExit:1` is not relabeled.

Independent verification establishes actual build success:

- All **eight compressed/raw/original chains** match their SHA256 values and original bytes; JSON was parsed with duplicate-object-key rejection.
- All **2,551 source pins** match immutable tested Git `193dc5890d4582d68ece897f64189af65e73d81b` using `git cat-file --batch`, and current bytes. All **eight output pins** match current files.
- Both raw compiler logs say **Build succeeded**, **0 errors**: integration 131 warnings / 117.26 s, unit 35 warnings / 34.49 s. Official build phases both completed (117.3825319 s and 34.6048316 s).
- Raw official Plan and summary match the frozen copies: **25 methods / 32 cases** projection, **8 methods / 33 cases** reader; two descriptors, unchanged three-minute budgets; **zero tests executed**, no timeout. PlanOnly selection `Complete=false` is expected because execution was not requested; it is not a runtime PASS.

Recorded harmless probe receipts show:

| Exact situation | Outer exit |
|---|---:|
| Plain explicit `exit 0` | 0 |
| `set -e`, print, explicit `exit 0` | 1 |
| Activation without errexit, explicit `exit 0` | 0 |
| Guarded activated setup, capture `true` status, `set +e` before explicit captured exit | 0 |

The matching printed markers and exact structured tool results isolate an **errexit-sensitive outer reporting interaction**, independent of C# build behavior. They do not diagnose the harness internals or prove general nonzero-exit propagation. The original discrepancy remains recorded. Reviewer executed none of these probes and no native build/test work.

The proposed invocation precision is accepted: guard setup; disable errexit for the child invocation and immediate status capture; retain that exact child code while logging and checking artifacts; leave errexit disabled through explicit `exit "$captured_code"`. Preserve explicit setup/logging failure handling, timeout controls and all selection/evidence checks. Never replace the child status with a logging/pipeline status, coerce it to zero, or disregard a later nonzero result.

Released order, with existing source gates retained:

1. Official `NoBuild` catalog on the unchanged source and eight qualified outputs; require successful discovery-only validation and zero test execution.
2. Exact official `NoBuild` Plan for the same **33 methods / 65 cases / two descriptors**, zero execution and unchanged budgets.
3. Once these checks succeed, actual `NoBuild` execution of only `wounds-player-projection` and `wounds-player-read`. Preserve full native/TRX/stdio/pins and all 33 genuine reader cleanup receipts. Any material discrepancy freezes before dependent work; final independent evidence acceptance remains necessary.

Evidence anchors:

- Preparation freeze: `d2361412a681dd316a87f341b5b7e09687e5a924bc56bcff2f24d003c478043c`.
- Original wrapper-discrepancy JSON: `7250f436edfffbe9eaed751779db4b9cb37eb93e2f239ddc16b920b27fa13e96`.
- Exact harmless probe receipts: `fbaa1d311788f86f64b37f119dd60ecc0feef63aab77a4e7b12e745d9b0c4e63`.

No behavioral acceptance, aggregate grant or historical rerun follows from this qualification. RO2 existing public status consumers remains tracked next work after RO1 evidence; full US5/FR063/T070/#1536/lawful ACTIVE remain open. Parent reports remote `1074c330` verified; reviewer checked local identity and equivalence without repeating a remote read.

**FINALSTOP — only this external MD/JSON pair written; no source/native execution and no pending reviewer writes.**

