# T360 — actual history consumer after metadata folding

Compiled `34cedb381820f390b033450713b1cad6ff94d329`, accepted release `92f5bad089e1157775eceae4e7ecd456ef8adec9`. One guarded Windows attempt, **2planned/2completed/2PASS**, no skip/timeout/retry. Command77.9214391s, category61.4570736s. Negative TRX23.1913887s, positive31.8825925s. Actual input/current four DLLs/four copies unchanged. SOURCE/PREP was independently accepted and all504release recovery blobs verified clean/exact before execution.

| Measured consumer scope | Positive | Transient conflict |
|---|---:|---:|
| Prior accepted T355 scope |23.6814014s|14.2016943s|
| Current T360 scope |21.1421378s|11.9408827s|
| Current consumer process CPU |21.2812500s|12.2187500s|
| Storage evidence calls |6264|4627|
| Storage evidence inclusive time |5.0136469s|3.1484663s|
| Consumer serializer/write time |1.2050553s|0.4699915s|
| Writer share of separately reported component wall |5.6998%|3.9360%|

Every completed operation count and all ten probe-group counts match accepted T355 exactly: positive109named groups/86502intervals, negative108groups/54076intervals. Timing-scope and independently reported component-wall clocks differ slightly; writer shares use the latter. Inclusive ancestor times overlap and must not be summed. These separate instrumented observations and the independently established structural managed-query reduction support continued investigation; they are not a controlled wall-time benchmark, native/CPU attribution or historical hang-cause proof.

All52,025,025rawJSONLbytes retained without quotas.281156continuous events/140578closed intervals,0open/parseerrors. Same-run writer ledgers reconcile all attempted/stored/measured operations and prefix snapshots. Positive original assertions validate0issues/2audits10footprints; negative injected wave is retained through exact expected refusal then restored. All220gamefiles/60manifestdirectories including empty directories/exact generation match immutable T333input. Actual outer disposal and independent closing roundtrip pass, both new owned roots absent. T345/T357unknownclosing roots and rejected-cleanup ACL root remain retained, metadata-only checks.

Full artifacts: `T360-profile-outcome.json`, `T360-profile-raw.zip` (22indexedfiles, CRC/allhash verified, SHA256 `2F287F39144E75605282D0A16C6571F6BCAF38E40E4EA0FBE27F3CF572F207F9`), `T360-timings.json.gz`, `T360-probe-analysis.json`, `T360-write-cost-analysis.json`, `T360-evidence-ancestry.json.gz`, `T360-evidence-groups.json`, `T360-analysis-summary.json`, `T360-comparison.json`. Seven indexed exact analysis tools archived in `T360-analysis-tools.zip`, SHA256 `E14ED123DFE8AE1F15AD64F219963A5FF414E0E46D052326E087128127494C5B`.

Independent separate actual Sol6.1/xhigh result review accepted without findings at568f22453; `T360-final-review.json`. Everyraw event/interval/ledger/ancestry and all515currentrecoveryworkingtreeblobs independently verified. Next proposed scope is SOURCE/PREP for at most one changed full-owner observation on this accepted current runtime, under the same2minuteTOTAL bound. It requires explicit canonical scope/actual phases/consistency, exact one-case PlanOnly/bindings and independent prep acceptance, then a fresh guard. No actual full-owner retry has been started. No original/five/Linux/source42generation/rebind or NativeLineage change; historical original600second recovery and T357timeout remain unqualified. No diagnostic limits or protected-root cleanup.
