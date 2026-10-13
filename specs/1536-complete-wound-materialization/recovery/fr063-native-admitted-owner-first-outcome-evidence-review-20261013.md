# FR063 Task2 first connected outcome — independent EVIDENCE review

**EVIDENCE/status ACCEPTED at `ce83cdb6a8b639c053247a87610e276c2371df57`.** The actual test failed because its energy-spend selection includes an older fixture turn. The captured histories establish one current treatment publication, not duplicate current spending. This remains **1 executed / 0 PASS / 1 FAIL** and does not qualify the complete native path.

Runtime/compile source: `d54ab6b465b68232728b66840f1a97d3488429a1`. Reviewed the immutable packet `specs/1536-complete-wound-materialization/recovery/fr063-guaranteed-native-admitted-owner-first-outcome-20261013/`, the native originals, exact compiled-source assertion and the two metadata changes. No project execution, repository edit, Git mutation, provider call or new agent was performed. Only this external report was written.

## Provenance and counts

Independently verified all **41 artifact chains**: compressed bytes/length/SHA256, decompressed bytes/length/SHA256, and exact agreement with their retained scratch or native-run original files. All **241 source pins** match immutable Git `d54ab6b4`; all **11 binary pins** match the current retained files. The packet's PRE/POST records precede metadata edits. The source-review and private preparation-selection hashes match the previously reviewed artifacts.

Native preparation is 12 methods / 18 cases / 5 descriptors, 165.2850647 seconds, integration 96 warnings and unit 35 warnings, both zero errors. Exact native discovery is 1/1/1, 2.5425283 seconds. Catalog audit is valid, 724 categories / 11567 methods, no unmapped methods or stale selectors, 5.5109168 seconds. All three preparation operations executed zero tests. Their uncompleted test descriptors in discovery-only summaries are not runtime underfill.

The first actual run `20261013-170229-589-662973-8b0692ef2db449fe9f6cf7b6885fb560-categories` completed exactly one planned descriptor/case: 1 executed, 0 passed, 1 failed, exit1, 16.4847370 seconds. The full TRX contains that one failed result, no skipped/aborted/timed-out test, and the runner reports no duplicate test IDs or descriptor timeout. Its complete stdout is byte-identical to the separately frozen stdout artifact. This is not an incomplete run or a production-duplicate RED.

## Actual boundary and independent raw reconstruction

Parent PID663089 and child PID663125 share the captured physical generation `3d52d3ffa47b474d94c326926982b017` and original tuple: session `session_t070b3_game_engine`, request `request_t070b3_game_engine`, turn42, Mortal realm, snapshot token `74C66A0F42EDB01EC0092BAE67E6C0BB31C24C79D302667FE6C31735CAFB30E8`. The managed result and nested native observations agree with the parent context. Genuine prerequisites and actual caller entry are true; the pre-entry cache is empty. Five declared child dependency pins and three actual loaded Default-context bindings agree with the fresh pinned integration, TestSupport and Immutable assemblies.

I independently decoded the native before-caller and managed after-caller resource/wound images, rather than relying on the derived companion's classification:

* Before resource history contains three rows. All three remain semantically exact afterward: energy initialization on turn1, health initialization on turn1, and **energy Spend turn2 amount8, 10→2**.
* After history contains precisely those three rows plus **one health Damage turn42 amount3, 10→7**, execution sequence0, and **one energy Spend turn42 amount2, 2→0**, execution sequence1. Both new transitions have applied outcomes.
* Wound history contains one original `create` and one turn42 `treat` for `wound_test_torn_side` / `operation_t070b3_game_engine`. Captured care is stabilized at turn42, with the treatment attempt ID. One story row has turn42. The wound command is absent and the actual loop is42.
* The repair observer has no failure and captured no PRESENT repair request. Its recorded deletions are not repair refusals.

These observations support the active metadata's narrow statement that treatment spending/history/care and the periodic companion were published. They do not retroactively turn unreached assertions into passing assertions.

## Confirmed fixture defect and minimal correction

At compiled `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.NativeCold.cs:380–384`, energy0 and `CountHeldTreatmentEnergySpends(resources) == 1` passed. The next `Assert.Single` selects `Spend` + player energy across **all turns**, so it matches both the unchanged turn2 fixture row and the single new turn42 row. Native `SingleException` and its stack identify this exact assertion at line383. It is an oracle-scope defect, not evidence of a second turn42 spend.

The minimum correction is to add **`row.Turn == 42`** to that existing `Single` predicate. Preserve its amount/before/after/applied assertions, the current-spend count, all periodic/history/receipt/care/finalized-hold/original cleanup checks and second-idle checks. Do not remove the old fixture spend, change history, choose an arbitrary last row, weaken `Single`, or alter production. A diagnostic rerun is unnecessary: the retained raw evidence already resolves this failure's cause.

This correction may be authored and submitted to the narrow source gate, followed by fresh matching preparation and one affected native retry. It is not a source verdict on an as-yet unpublished correction. The fourteen remainder cases stay unrun until native completion and its evidence gate permit them.

## Scope, cleanup and recording

The native helper stopped at line383. Subsequent amount/periodic, typed treatment receipt/correlation, finalized-hold, story/cleanup and second-idle assertions were not reached. Some associated states are independently observable in the retained images, as listed above; full assertion coverage and finalized-hold/second-idle qualification remain open. `accepted` is false because its derived completion assignment follows the entire helper; the separate checkpoint hook is observational and also false. Neither is evidence that the observed treatment publication did not occur.

Both emitted strict cleanup receipts report removed=true/failure=null, and I verified both roots are physically absent: the held-treatment source root and the child auxiliary fixture root. Runner owned-tree/runtime cleanup also succeeded. No wider cleanup claim is needed.

The checkpoint changes only the 41-artifact packet plus manifest and identical inserted status blocks in plan/tasks. Reversing each status insertion recovers the prior document exactly; checkbox lines are unchanged. The active status correctly preserves the failed outcome, distinguishes zero-execution preparation from PASS, marks later assertions and remainder checks unrun, and keeps historical packets and full FR063/T070/#1536 acceptance open. Separate new-root mapping and recommendation/GM work receive no source or runtime approval from this evidence review.

No blocking evidence or reporting defect found. Proceed with the bounded oracle-only correction; do not label this run native GREEN.

FINAL STOPPED.
