# G01 whole SOURCE — HOLD

Reviewed `9d8d08093bb769f85ec79a88d98669f1570e0d57` against `4e379a92`. 49 read-only checks verify current161 pins, explicit3 D01 deltas,395 input hashes, unchanged unit source graph, selection/assertions and independent recovery. They also reproduce two lifecycle defects with exact-source AST and inert callbacks; no process/API was launched.

Four corrections are required:

1. **G01-S01:** successful collector Popen followed by pidfd failure leaves an untracked live child; cleanup only waits and records zero launches. Account immediately and reuse the exact direct-child fallback within the shared5s remainder. Reconcile late target-createdump children without signaling the resumer or claiming unknown cleanup complete.
2. **G01-S02:** the parent writes `capture/cancel` on observer failure, but the final PowerShell guard ignores that positive cancellation source. A fresh old ADMITTED image can still pass. Check cancellation immediately before API admission; retain the unavoidable final-check/API race as UNKNOWN.
3. **G01-S03:** an already naturally exited collector7 observed in finally is stored only as cleanupExit. With nativeExit still None, FINALIZE returns2. Preserve known natural native failure and distinguish forced cleanup; native0 plus observer fault remains HOLD.
4. **G01-S04:** the mapper checks IL only against int.MaxValue. Out-of-body IL can be attributed to the last sequence point. Require a valid method body/range or report an unmappable limitation; add a bounded negative without weakening the existing three proofs.

The integration-only fresh build plus separately retained unit439 closure is sufficient in principle: the entire46787-entry unit source/dependency tree is unchanged. Fresh actual target/assets/import checks, complete catalog, exact singleton PlanOnly, tool/clock proofs and independent PREP are still required. Original220/900, assertions, category membership and existing D01/policy bytes remain unchanged. Option1 resolves the former semantic blocker; CapturePhase and source-emission completeness remain UNKNOWN.

No tools/ZERO/game/API admission is granted. Preserve the original HOLD, apply the bounded fixes, repin/save/recover and obtain focused SOURCE re-review. Do not repeat unchanged historical proofs. Private dump/raw report retention and no-publication requirements remain. L0152/57, old66529UNKNOWN and all broader holds are unchanged.
