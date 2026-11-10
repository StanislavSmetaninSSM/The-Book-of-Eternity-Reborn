# T318: actual phase/story component evidence

Source512f2c62d288e5ab729e58b4857ac92876e76918; metadata14f5f97e. One actual selected run `20261020-235949-940-28268-52a24a146009409682621ba93e387864-categories`: 2/2PASS, no skip/timeout, selection and owned/runtime cleanup complete. Both fresh roots removed and independently absent. Immutable input and three actual DLLs unchanged. Raw ZIP/index/hash in T318-components-outcome.json; all640timing records match the complete TRX output, sequences contiguous,320intervals complete, none unfinished. No diagnostic quotas.

| Case / operation | Observed milliseconds |
| --- | ---: |
| Positive full case |106411.8076|
| Positive copy+lease setup |1028.9533|
| Initial real phase |3000.3678|
| Initial unappended story |10783.9348|
| Actual append/readback/proof |41476.9287|
| Explicit current proof |11084.3546|
| Repeat append/exactly one row |30708.0666|
| Damaged story proof refusal |7866.4933|
| Separate changed-narrative full case |7305.7569|
| Narrative copy+lease setup |1233.1274|
| Narrative refusal |1880.7077|

These are inclusive actual observations; nested rows must not be added. Positive case has ten story-source reads, nineteen completed phase validations inside them (drift refuses early), and20completed phase completed-proof reads including initial phase. `BindContinuationAsync` ten calls total47297.86ms; `JointPhase.ReadCompletedProofAsync`20calls total45036.53ms. Those occur within the enclosing source/wrapper times. No full GameEngine/source42/Stage43 setup occurred.

Trace: successful new append reads source four times (initial, immediately before append, readback, issued-proof validation), repeat reads it three times. Each successful source read checks applied phase both before and after, binds exact continuation, checks all source images/history inventory and rechecks bytes. This finite repeated validation dominates these components; no deadlock or OS/scanner cause is shown. This evidence does not identify the original unfinished cleanup or qualify recovery/Linux.

Next concrete question: can the fully authenticated post-append readback serve both exact-readback and initial proof-content comparison, with no intervening await/I/O/owner transition? Proposed local reuse removes only the immediately repeated fourth/third source read. It must compare all current proof fields including histories and retain all source bracketing/physical/output checks on the retained readback. No cross-call cache; explicit later ValidateCurrentAsync remains fresh. Independent review must resolve the freshness/failure semantics before this production change. Unresolved until reviewed; no production optimization implemented here.

Original T315 remains FAIL/open, original Windowsfive0/5qualified, LinuxUNRUN. Continue to a written cause-driven correction after review; this report is not a stopping point.
