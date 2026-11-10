# FR063/T070/#1536: proposed no-bootstrap joint generation veto

**PROPOSED. Independent DESIGN review required before source changes. No runtime release.**
Basis: independently accepted G2 failure packet `93a5544369b0535dde2e6665cbc4923e2136b7976673e80b049e9783ff0866f9` and EVIDENCE/causal report114125. Current source is381393bdd3e56f57bff8b6c3e70360a015c35627; compiled5c66 is historical. Restore the already accepted same-existing-G/no-new-G preservation contract (mortal-wound-treatment1120–1128 and accepted progression amendment); do not enlarge authority or storage membership.

## Concrete placement and ownership

Add an optional internal asynchronous **veto**, returning no value/capability, to `SessionOperationContext.RunParticipatingCurrentSessionAsync`: `Func<FileSystemManager.CanonicalWriteLease,Task>? beforeCurrentGeneration = null`. Thread it only into the existing private participating wrapper. Expected-generation callers pass null. All existing callers retain null except the actual Late/idle GameEngine owner.

In the current unbound branch, immediately after its existing `AcquireCanonicalWriteLeaseAsync` and inside the existing typed release try/finally, invoke the veto **before** `GetOrCreateSessionGeneration`. The same lease remains held through generation selection. Do not return a raw claimed generation or bypass ordinary main admission, worker/browser checks, storage recovery, canonical lease admission, generation binding/finalization or close handling. Existing-bound calls continue to use the existing bound fence, which already rejects missing/replaced G before writes; this proposal adds no raw claim to that fence. Main admission remains owned across generation selection and the actual callback; no independently admitted precheck/reacquisition gap is introduced.

The veto uses normal admitted byte readers on that lease. Ordinary existing storage admission/recovery still precedes it; this is not a new below-admission reader or generic recovery redesign. Earlier existing admission refusals remain refusals. The concrete observed G2 namespaces are quiescent, with no console cohort or unsettled publication; no claim of preservation across arbitrary recoverable debt is added.

## Closed negative applicability

In `GameEngine.MortalJointNullFrameHold.cs`, share the current strict private candidate/header reader with the existing null-frame hold, without changing its classification: schemaVersion integer2; jointTreatment/progression objects; unambiguous nonempty session/request/positive integer turn; either actual fixed manifest or input header with matching session/request/turnNumber. Both absent/foreign makes a stale marker inert. A conflicting other header cannot remove protection. No Ready/fingerprint/full marker proof is required to earn protection.

For this identified candidate only, read existing physical G with `ReadLocalGenerationSnapshot(lease)`; never call GetOrCreate/CaptureCurrent in this veto. Missing G throws `InvalidDataException("Active joint continuation generation is missing; evidence retained.")`. A missing/non-string/noncanonical lowercase N-Guid marker generation, or any difference from independently read physical G, throws `InvalidDataException("Active joint continuation generation is invalid or differs; evidence retained.")`. Existing malformed physical-G read failure remains typed refusal, not a fabricated generation. No game/runtime fields are changed on this early refusal.

The raw marker comparison is **negative applicability only**. Marker.G is never passed to RunExpected/RunBound, returned, cached or installed as authority. Returning normally from the veto grants nothing: the original wrapper still obtains the actual physical generation by its unchanged reader; the actual owner must still freshly validate signed original, whole mechanical union, completion output binding, progression phase, story and owned cleanup. Equal raw claims cannot substitute for any proof. Missing/foreign/forged original or altered other evidence retains its existing later refusal/hold. An unidentifiable family stays under the existing ordinary owner; this proposal does not quarantine arbitrary bytes.

Read instability after identifying a candidate must propagate a refusal, never return NotApplicable and permit bootstrap. The existing null-frame helper retains its hold-on-identified-instability behavior and never derives a generation grant. SessionReplacedException remains unchanged.

## Fixture correction and bounded verification

For the two existing actual G2 cases, retain genuine preflight, tamper after all leases close, actual outer Late invocation, full target raw names/bytes equality, missing-G absence/foreign exact donor, entire donor preservation, story/turn/inGame equality, inner checkpoint false, handled null and real native/cleanup evidence. Replace only the two false console-cohort message expectations with the reviewed dedicated early-veto messages. Do not add a console cohort, alter the producer, transfer a parent generation capability, restore G or accept current inner handled=true. Cases remain failures until actual retry qualifies them.

Propose a small unit category for the changed wrapper boundary: missing and existing G veto failures must skip operation, preserve absent/exact G and allow a subsequent ordinary operation after scope closes; default no-veto fresh bootstrap must remain valid. Use per-test owned roots with real cleanup receipts. Exact method/case/category counts will be derived and independently reviewed from authored tests, not assumed accepted here.

SOURCE/FIXTURE/SELECTION must assess the generic wrapper's default/expected/bound/close dependencies and the Late-only caller. Fresh BOTH and discovery/catalog, exact selected preparation plus user-required first Late2, immutable EVIDENCE before corrected G2 release. New production code touches Late entry: independently assess transfer of prior positive/scene/binding/draft/live/cold/retained/new-root evidence; do not silently call historical PASS current-source PASS. A limited recheck of affected successful entry contours may be necessary; do not repeat unrelated categories.

GM grammar, costs, mechanics, authored output, member sets and successful lifecycle proof stay unchanged. This is a client-owned preservation correction already required by the accepted contract; no GM prompt/example update is proposed. Record the final no-update rationale. Full FR063, ACTIVE, initial9, partial cleanup and erased-owner scope remain open; no issue/task closure.

## Mandatory review questions

1. Is the no-value veto at the existing unbound generation lease sufficient to close this observed hole without promoting raw G or introducing a race? Does existing bound admission preserve the equivalent negative boundary?
2. Does comparing marker.G only to refuse satisfy the accepted preservation contour without adding admission authority? If not, identify the exact violated contract and a narrower existing mechanism; do not imply a raw marker grant.
3. Is keeping the existing storage recovery/admission order within this bounded quiescent correction justified, or does the actual supported contour require an earlier existing mechanism? A new recovery/authority/member policy requires a separately accepted decision.
4. What exact affected positive/negative selection is necessary before successors, with historical evidence dependencies stated explicitly?
