# T179-Pd observed-reader barrier TDD sequencing — 2026-10-16

DESIGN ONLY. Assigned Astra/High; backend unknown. Source HEAD 7c9529bb36d1d16db8436c37f7da49a0f412cd80. Read-only source/Git/hash inspection only; no repository edits, build/discovery/tests/app/runtime/network/OS investigation/delegation. This new /tmp pair is the only write. Previous consultation pairs remain immutable. All proposed cases are UNRUN.

## Ruling

The proposed TEST-ONLY fallback is sound for a bounded causal barrier RED, with the assertions below. It requires no temporary production observed API and no production success path that knowingly issues authority before its barrier.

Name and document it as a missing observed-read barrier specification using the current reader as the absent-feature behavior. It is not a claim that default ReadCurrent has adopted observed semantics, or a RED through an already-existing observed owner. Default ReadCurrent remains unchanged.

Prefer two distinct tests/controls:
1. Existing-reader characterization: genuine original + controlled late drift → current ReadCurrent succeeds from previously captured bytes. This is an expected positive characterization of the old contract.
2. Observed-barrier specification: invoke the genuine ReadObservedCurrent if it exists; otherwise invoke exactly the old ReadCurrent only in the test adapter. Require rejection, source_changed and no returned authority/evidence, AND require that the genuine new API was present before the test can pass.

On absent-API source, the second test's expected causal RED is result.Success == true at the first rejection assertion. It must not fail first on missing-method reflection, missing generation, malformed fixture, wrong type, injected exception or unrelated source failure. GREEN must require the actual observed API; a fallback can never qualify it.

## Source basis and precise injection

PendingTurnSnapshotReader.cs:455–497 preserves selected required-path order; :311–358 reads/hash-validates each selected snapshot and retains detached bytes; :365 constructs the current authority without a final closure recheck. Current contexts are all consumed at :239–241 before selected originals.

FileSystemManager.OrdinaryReads.cs:190–205 invokes the EXISTING AfterCanonicalReadInitialValidationAsync hook after opening the selected file and before copying its bytes. Its completion validates that opened file. The hook is already part of FileSystemManagerHooks (FileSystemManager.cs:40–42); no new hook or source-injection API is needed.

Use two actually prepared selected originals A then B. At B's first post-open hook, mutate the already-completely-read A, not B. A's hash check has already finished; B's bytes and handle remain valid. Default ReadCurrent therefore has a causal route to Success=true while the physical A differs from the retained original bytes. This is cleaner than a context mutation for the first case because it exercises the exact original-byte closure without introducing lifecycle correlation interpretation. A later independent context-presence/text case remains useful.

Derive A/B physical coordinates from the genuine emitted manifest; never fabricate the manifest, detached payload or hashes. Use existing PendingTurnSnapshotReaderTests.PrepareAsync (:821–830), which calls the real LiveTurnPreparationService, and the fixture pattern ReadCurrent_ReturnsTheExactSignedPreTurnBytesNotTheChangedLiveRoot (:127–150). Prepare existing G through the genuine original/session fixture, not a hand-authored generation document.

The post-open hook must be armed only after genuine preparation and baseline checks. It performs a one-shot test fault injection on A's physical bytes using the existing fixture's direct mutation mechanism; it must not call an atomic writer that tries to acquire another canonical lease from inside the read hook. Do not mutate B, G, detached authority or the selected path mapping.

## Test adapter and assertion order

A small test-local adapter may normalize ONLY observable results:
{ApiPresent, InvokedApi, Success, SnapshotObject, EvidenceObject, Issues}.

- Detect ReadObservedCurrent by its exact static signature: actual FileSystemManager, actual CanonicalWriteLease, IReadOnlyCollection<string>. If present but malformed, fail the API contract; do not silently fall back.
- When absent, invoke current ReadCurrent directly and expose its real Success/Snapshot/Issues. EvidenceObject=null means this API has no evidence result; it is not a fabricated evidence object.
- When present, invoke the real method and read its actual result. Do not catch an implementation exception and switch to fallback; unwrap invocation exceptions so they remain visible.
- Never construct PendingTurnSnapshotReadAuthority, OriginalReadEvidence or issuer tokens. The adapter cannot manufacture diagnostic failure results.
- Do not add a test option to production methods or use a production mode switch to disable the barrier.

Order for the barrier specification:
1. With hook disarmed, prove default genuine-reader success, exact A/B original bytes and usable original/G. Capture exact source baseline.
2. Arm one-shot mutation; invoke the selected behavior once under the actual participating lease.
3. Assert fault injection actually ran once at B after A's earlier read, physical A changed as intended, and B/original authority/manifest/G remained exact. Do not assert total hook-call count: the correct new reader legitimately adds rechecks.
4. Assert.False(result.Success, causal message). This is the required absent-API RED point.
5. Assert.Null(result.SnapshotObject); Assert.Null(result.EvidenceObject).
6. Require the specific observed source_changed diagnostic for A (with its source role/coordinate where exposed), not any validation failure.
7. Assert.True(result.ApiPresent) and exact InvokedApi == ReadObservedCurrent. These assertions must execute before any GREEN. They must not be conditional on ApiPresent.

This order prevents default ReadCurrent being changed into a rejecting implementation from making the fallback pass: even if its result later became a rejection, step7 still fails without the new API. Keep the default characterization test too, so unintended default contract changes fail independently.

The genuine new API's unchanged-source positive test remains mandatory. An implementation that always refuses or throws cannot pass on a negative test alone. Existing RED1 establishes the missing-surface prerequisite; its actual evidence must be qualified separately.

## Sequence without an unsafe issuer

1. Finish the current unchanged first-Late2 and RED1 EVIDENCE gate. This consultation does not release it.
2. Add only the bounded test specification/characterization after the parent's actual allowed source window. Source/fixture/selection review must confirm the intended mutation happens after A was read and does not invalidate setup.
3. Run the designated causal barrier RED. Expected failure is step4 Success=true on the old behavior. Record that it is the explicitly absent-feature fallback, not an observed API invocation.
4. Implement ReadObservedCurrent as a complete vertical: real lease-explicit bytes, private session, same validation order, sticky faults, complete closure recheck BEFORE the authority constructor, and evidence issuance only after the barrier. There is no need to checkpoint a provisional unsafe successful issuer.
5. Run both genuine unchanged-source positive and barrier rejection plus the finite affected controls under the required review/evidence sequence. On GREEN the adapter must have selected the actual observed method.
6. After acceptance, remove the fallback in a later test-only cleanup or retain its unconditional API-present guard and explanation. Removing it avoids future method deletion producing a less direct regression diagnostic; it must not change the actual mutation/assertion scenario or manufacture new runtime evidence.

A simpler alternative is to add both tests now and leave the observed test failing solely on missing API, then implement the complete safe vertical. That preserves ordinary TDD but does not demonstrate the requested causal missing-barrier RED. Given the explicit goal, the guarded test-only fallback is the better choice. Neither option justifies unsafe temporary production authority.

## Limits

This proves the selected end-of-capture drift boundary only after actual GREEN through the real observed reader. It does not prove rollback/context/G/worker/browser coverage, all read closure, subsequent RequireCurrent, main submitted admission, C/B integration, M3/cold proof or public activation.

No test was executed here. All new cases remain UNRUN. Default v2/GM bytes and old reader contract remain unchanged; full admission/M3 and all existing activation gates remain open.

FINALSTOP.
