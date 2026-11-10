# L01 D01 live diagnostic delivery — bounded implementation plan

Task #1536. Branch `1536-wound-b2-cloud-20261012`; base
`1817ae5814f06a45cd6df975a9211c380a256df8`. Root sole writer, independent Astra XHigh.
User explicitly authorizes independent PLAN → test-only helper → SOURCE/PREP and
owned synthetic, checkpoint/recovery; GAMEPLAY NONE until future run plan considered.
This user-selected workflow governs execution; no new human design gate or full suite.
S01 accepted synthetic capability remains separate. D01 is not another stack capture.

## Purpose and selected scope

Deliver existing flushed diagnostic JSON from the parent-owned test resumer while
that process is still alive, without changing production, child diagnostic schema,
gameplay branches/assertions,220s child or900s category budgets. Do not promise zero
observer overhead or a continuously known phase. Recoverable observer failures
must not replace a known native0/7/timeout/primary failure or control the game.

Existing reader: `GameEngineTurnLifecycleTests.JointInterfaceCrash.cs:128,144–150`
uses one ReadToEndAsync and publishes after join. Diagnostic events already contain
Boundary/Stage/Detail/Pid/BirthTick/Root/Nonce/Cut/Utc/ElapsedMilliseconds.
The parent independently obtains the live child's PID/starttick and owns root/nonce.

Approaches considered: (1) selected bounded in-memory queue in the owning test parent;
(2) append-only file/background writer, rejected here for extra I/O/teardown/latency
failure surfaces; (3) second stdout reader/external parent-pipe stealing, rejected.
There is no new game runtime protocol, environment flag, production hook or arbitrary
PID attachment. Future collector orchestration belongs to the owning test parent;
this block does not implement/release snapshot collection.

## Files and contract

- New `BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs`,
  BCL-only internal test utility, no Fact/Theory and no game references. Holds an
  immutable expectation (PID/kernel starttick/root/nonce/cut), bounded Channel and
  bounded health counters. Drains the existing TextReader once, accumulating the
  identical decoded stdout string (including CR/LF, Unicode, blank/trailing text).
  It is decoded-text equivalence to existing StreamReader, not new raw-byte fidelity.
- Minimal conditional wiring in `GameEngineTurnLifecycleTests.JointInterfaceCrash.cs`:
  enable only for diagnostic=true + resume role. Ordinary original/crash/current
  non-diagnostic paths retain ReadToEndAsync. Instantiate only after live independent
  identity; retain stdout task, original WaitForExit/220s, finally/native/join/EOF,
  primary exceptions and all assertions. Expose LIVE Reader and health through a
  parent-owned helper variable/API before waiting for child exit, usable by a future
  separately reviewed observer. Optional
  delivery health/minimal events in diagnostic output must be guarded; no Assert
  on observer state changes the game/native result.
- External task-local synthetic source/controller under recovery/l01-delivery-d01/:
  compiles the SAME helper source with installed csc/net8 refs/runtime8.0.32,
  no install/restore/network tool acquisition, no game references/saves/diagnostics API.
- Update existing #1536 spec/plan/tasks and catalog diagnostic changeHints/responsibility;
  current tests/selection selects diagnostic category for PREP ONLY, not actual.
  No new xUnit method/category or semantic target. Preserve catalog220/900 bounds.
- New versioned current delivery packet/control and recovery helper. Original L01
  and M01 packets/ledgers remain byte-identical, at their original runtime provenance.
  Explicit current-only source pin deltas: L01 crash helper +catalog+selection;
  M01 catalog+selection only. Do not repin old evidence or claim cross-build equality.
  Verify other historical pins,11 S01 pins and production closure unchanged.

## Delivery and observation semantics

One stdout reader; observer publication uses TryWrite only, never await/callback/I/O
on the raw-drain path. Channel: FullMode=Wait, AllowSynchronousContinuations=false,
SingleWriter=true/SingleReader=true; TryWrite(false) is known local loss, never silent
drop-mode success. Fixed queue capacity64 and observer line limit8192 chars;
raw stdout is still retained whole, as before. Complete frames have ordinal and
character offset, parent monotonic receive time, original source fields and minimal
stage/detail (only known control detail; other detail may be hashed). No reordering.
JSON parse/framing/publication errors are observer health, caught separately from
raw reader errors. Construction/publication/health-serialization errors must be
contained: construction failure falls back to the sole original raw reader before
any read; observer processing failure latches health and continues raw draining.
No observer summary exception may escape native finalization. A raw I/O error
remains the original drain failure. Sticky health/gap/EOF are available independently
of the possibly full Channel; future admission must recheck health even if a
cancellation marker was dropped. Fatal process/
resource failure cannot be promised survivable; no result may claim otherwise.

- Exact PID/starttick/root/nonce/cut mismatch: REJECTED_IDENTITY, never trigger.
- Channel overflow: known locally dropped frames/ordinals, health gap; never block.
- Oversize/incomplete EOF/malformed candidate: explicit framing/parse gap, raw retained.
- Same-identity duplicate or regressing child elapsed: DUPLICATE/STALE_ORDER,
  retained/labeled, never fresh trigger. Equal ElapsedMilliseconds on different
  events is legitimate; duplicate uses exact bounded fingerprint, not millisecond
  equality. Track bounded state; exhausted tracking
  must itself become an uncertainty, not silently stop detection.
- Parent enqueue→dequeue residence can be measured monotonically and labeled DELAYED
  against a named local threshold. Source→receive latency is UNKNOWN: child relative
  elapsed and wall Utc do not share a proven monotonic epoch; wall skew is not age.
- Missing expected stage at EOF is UNOBSERVED, not proof the child never emitted it.
  Existing child events have no gap-free source sequence. Parent queue/drop counters
  prove only local loss; never claim completeness before first receipt or between
  emissions. Delayed delivery and source completeness remain separate concepts.
- Observed custody/accepted events are discrete source-return markers; no continuous
  phase guarantee. CapturePhase stays UNKNOWN unless actual ordering evidence proves
  its position. Observer health/freshness failure invalidates observation admission,
  without killing/retrying/changing the native child or replacing its outcome.

## Execution tasks and verification gates

1. Save/push/recover this candidate, obtain independent PLAN before implementation.
2. Before helper implementation, establish a source-bound in-memory async TextReader/
   EOF witness: a flushed logical custody line is available while EOF is held, but
   old ReadToEndAsync has no consumable event until EOF. Record the expected delivery
   failure as helper baseline only, not product RED/gameplay. No real child/attach/
   diagnostic API in this baseline. All genuine native0/native7 children belong only
   to the finite post-SOURCE/PREP selection in task5.
3. Implement smallest helper and diagnostic-only wiring. Synthetic checks exercise
   exact decoded stdout across chunk/Unicode/CRLF/trailing boundaries, real live
   delivery before STOP/exit, identity rejects, duplicate/time regression, overflow,
   malformed/oversize/incomplete frames, measured queue delay, raw read failure,
   observer faults and preservation of native0/native7 and primary failure priority.
   Use isolated roots/processes, nonce/PID/starttick/private stdin/join/EOF cleanup.
   Deterministic parser cases are source-bound; at least real native0 and native7
   child cases establish live delivery, no game assemblies or callbacks substituted.
4. Save/push/recover source and source packet. Independent SOURCE first; affected
   fresh integration/dependency build plus catalog discovery and diagnostic PlanOnly
   (1 method/1 descriptor/1 case, zero tests), independent PREP before synthetic
   release. May combine source-bound non-live parser checks with preparation.
   Preserve first actual failures/unknown, no automatic retry; source fix needs
   affected prep/review. External owned watchdog and native/observer separate record.
5. One named finite D01 synthetic selection after SOURCE/PREP admission. Safe minimal
   results/code/test inputs only: no memory dumps/API calls, no game fixture raw.
   Preserve stdout/records/hashes/identities privately; publish reviewed sanitized
   synthetic data. Independent EVIDENCE, final exactremote/recovery/readback.
6. Deliver reviewed future single-game-run plan only, not its execution. Include
   exact owned diagnostic cut1, source/build identities, event health/freshness gates,
   single60s timer after exact finalization-return/
   MortalJointCustodyCaptureCompletedBeforePreparation receipt; cancellation already
   on finalization-enter/AcceptedOutcomeValidatedBeforeMaterialization, its return,
   late-first-return/exit or health gap/stale/delay/unknown,
   identity revalidation, at most one normal+crashreport request and bounded owned
   cleanup. Do not pause/reset/extend220/900. Raw core stays private; publish only
   inspected minimal stack metadata/source mapping, health and native outcome.
   If ordering cannot place capture, phase UNKNOWN. Stop after first original:
   useful named operation/wait-site evidence OR exact limitation; no retry/sampling,
   budget inference/deadlock claim/crash5 requalification. New capture implementation
   and SOURCE/PREP/user gameplay decision remain a gate, never implicit permission.

## Review focus / retained boundaries

Independent PLAN/SOURCE/PREP must examine blocking/backpressure/teardown, stdout
fidelity, identity ownership and stale-source distinction, native/observer priority,
latency vs wall clock, bounded tracking, source-pin provenance and selection sufficiency.
No C#/frontend/full suite or game actual is selected here beyond named synthetic;
catalog/PlanOnly execute zero tests. No product or GM contract change, so no prompt/
GM example update. No Windows, artifact403 retry, CI, main/merge/force/delete/issues.
L0152/57 original provenance, original crash5 unqualified, old66529 UNKNOWN/unavailable/
recoveryClaim=false, all failures/M01/fullNI/HOT/F1/M3/T197/E01/T148/Phase10 holds and
A3 NO-PROCEED remain. Future Windows-five/native identity and artifact403 are separate.
