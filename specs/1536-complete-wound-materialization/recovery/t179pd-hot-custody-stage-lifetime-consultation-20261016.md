# T179-Pd.3b Stage lifetime consultation — 2026-10-16

Read-only author consultation; not independent implementation acceptance. Frozen writer/source commit cc8caa6ec2d1dd50c965b2cab9acbd3df6a3bfcd. Configured Astra/High; backend unknown. No repository changes, runtime, tests, discovery, network or delegation. Only this new /tmp pair is emitted; prior reports remain immutable. All proposed checks are UNRUN.

## Findings

1. StagePlayerTurnAsync opens browserOriginalScope at TL:1565, then runs actual health, backup/request/reminder/dice/progression/original staging, final request projection and validated current context. Its return expression at TL:1742 is evaluated before the using declaration is disposed. Original-producing leases close inside their existing wrappers before Stage can return (SS:398–407; TL:1641–1652).
2. BrowserOriginalOperationScope.Dispose (BO:190–194) currently only marks disposed and restores the AsyncLocal parent. It has no IO, validation, callback or asynchronous close. Do not invent a production test hook for a hypothetical disposal fault. Nonetheless place any final local success transition after the lexical using block so future disposal behavior cannot leave success behind.
3. SessionOperationContext is a later, different boundary. RunBoundBodyAsync finalizes G under a real SessionFinalization lease and retains established results in typed continuation failures (SO:247–310). RunBoundCoreAsync and RunParticipatingSessionAsync call Main CompleteAsync, then their await-using disposal still executes. The existing observeOriginalClose callback is explicitly before outer disposal (SO:147–158; TL:1511–1518). It is observation DATA, not successful-return authority. MainAdmission.DisposeAsync can await remote disposal after local release (MF:190–200).
4. Console ProcessPlayerTurn keeps Stage→wait→Finalize within one continuous participating operation (TL:1492–1505). Browser exits the staging operation before waiting, then enters a new actual expected-G/browser-original operation for finalization (TL:1509–1555). Do not split console ownership, await a close that cannot happen until after finalization, or retain the old browser scope/lease as capability.
5. SS:419 performs CleanupPendingTurnSnapshot while creating the new original. SS:1058–1088 removes old snapshot/rollback artifacts and calls ClearMortalQteRepairState. That method currently clears only two QTE repair fields (QD:265–269). Adding unconditional HOT invalidation there would destroy a new Beginning owner during its own original producer. Other direct calls are session rebind (SS:205) and verified joint terminal completion (JT:112); other producer calls include life evaluation, incarnation and first Chaos Sea description. A source-label string is not permission to preserve a HOT owner.

## Chosen minimal handshake (new private APIs; names are proposals)

Keep three facts distinct: original-producer return, Stage body return after its local browser scope closes, and the actual enclosing route's disposition. Do not make CompleteHotOriginalStaging inside the current try block mean all three.

**Stage-local structure.** Retire any previous HOT origin at genuine ordinary Stage entry. Preserve current browser Claim-before-health ordering. Create the new private Beginning ticket after successful entry health, before this Stage's preparation work; retain actual request/backup associations when those objects are really produced. Enclose the entire ticket lifetime, including the using block's disposal, in an outer catch that marks this exact ticket unavailable and rethrows unchanged. Existing inner rollback/catch/uncertainty behavior remains unchanged.

Use an explicit inner lexical block for browserOriginalScope. Inside it, existing work produces the actual PlayerTurnPreparation and records ProducerReturned only after the actual original producer wrapper has returned. At the bottom, record the immutable preparation data, not Staged. Exit the block, then call RecordHotStageBodyReturned(ticket, preparation) and return the preparation. The ticket must match the same engine/FS/G, actual request object/binding, original tuple/token and returned context. No new timestamp/GUID, no parsed-original seeding, no retained disposed scope. Null/exception never reaches this transition. Historical producer/body facts may remain diagnostic after failure; custody availability does not.

**Browser route.** In the existing ProcessPlayerTurn browser branch, retain the exact ticket associated with the returned preparation. Promote AwaitingStagingOwnerReturn to Staged only on the normal continuation immediately AFTER await RunParticipatingCurrentSessionAsync(...) has returned. The existing close callback may copy its real close fields; it must never promote state. A catch around that await invalidates the matching ticket for any failure, including SessionReplacedException, publication uncertainty, MainOperationContinuationException or remote DisposeAsync failure. Do not recover a successful HOT origin from the exception's EstablishedResult. Existing durable browser preparing/staged recovery stays unchanged. Any later consumption needs its new actual lease/MainAdmission/browser condition, never the old scope.

This route acknowledgement can be a private engine method/wrapper used by this actual production branch and by close-lifetime fixtures; it is not a general success callback supplied by a test. No SessionOperationContext change is required.

**Console route.** After Stage returns normally inside the existing continuous participating callback, acknowledge StagedUnderLiveOwner for that exact ticket. This state means the body completed and the actual current owner still governs wait/repair; it makes no claim that the outer operation has already closed. It may feed the accepted HOT custody flow under the actual later lease/G checks. Enclose the entire existing participating await in an identity-checked failure handler, invalidating availability on ANY escaping failure, including outer finalization/Main/disposal failure. On normal terminal return retire the matching hot staging availability if it has not already been retired by actual terminal cleanup; retain only detached historical diagnostics. Never erase publication debt, actual receipts or terminal disposition just to invalidate a source proof.

This is necessarily route-specific: a single “wait until outer close, then permit Ready custody” rule would make console custody unreachable. Conversely, a permanent Staged bit before browser close would be unsound. These two states may be internal reasons within the existing owner; do not add public schema or gameplay state.

**Direct Stage fixtures.** A test invoking Stage via reflection inside an arbitrary participating wrapper can prove ProducerReturned/StageBodyReturned DATA. It cannot thereby exercise ProcessPlayerTurn's late-close acknowledgement/invalidation. For final route-lifetime assertions, use the actual production route/wrapper above. Do not add a test-only issuer or infer successful owner close from Ready absence, Stage data, or an outer callback. Existing successful Stage-prefix fixtures need not be relabelled full admission.

## Own cleanup routing

Use the accepted private ordinary original-producer overload to carry the exact Beginning ticket from this Stage through both CreateCanonicalBaselineSnapshotAsync overloads. Ordinary snapshot publication remains the actual SS core, with all existing lease/fence/serializer/restore behavior. No other caller receives that ticket.

Add a separate private original-producer cleanup entry that:
- checks identity with the currently registered Beginning ticket, exact engine/FS/G and actual Stage request/rollback objects;
- consumes its single original-producer-entry/cleanup permission; preservation is limited to this one cleanup call before new original issuance;
- performs the existing cleanup body and QTE repair reset unchanged;
- preserves only that exact Beginning HOT ticket, never an earlier Staged/Ready/attempt owner;
- invalidates it on any cleanup/producer failure, including errors before the old trailing ClearMortalQteRepairState line or errors releasing the producer lease.

All ordinary/general cleanup entrypoints invalidate HOT availability BEFORE potentially failing deletion, while preserving existing filesystem deletion order. Session rebind, verified terminal cleanup, other producers, repeated/reentrant producer entry, mismatched ticket/G/request/backup and replaced engine ownership also invalidate. The existing QTE-only Clear method can remain QTE-only with explicit HOT invalidation at owning callers; if centralized, use two distinct private routes with an exact ticket, never preserveHot=true, sourceLabel equality, ambient “Beginning exists”, or omission-by-default.

The ordinary producer preservation route must still call the real cleanup implementation: this is not permission to keep stale original artifacts. If cleanup fails after partial deletion, retain its genuine typed failure and existing recovery evidence; HOT becomes unavailable and does not certify rollback. Do not automatically settle pending debt or retry Stage/reminder/dice.

## Finite future checks — all UNRUN

- Genuine ordinary Stage creates original and returns body data; its own cleanup preserves only the new ticket. Previous HOT origin is retired; second/foreign producer or terminal/rebind cleanup cannot inherit preservation.
- Actual browser staging outer await succeeds: acknowledgement occurs after finalization, CompleteAsync and DisposeAsync return. Existing original-close observation alone cannot acknowledge.
- Real existing SessionOperationClosing hook replaces G or fails after Stage body: no usable Staged custody survives; preserve actual continuation/replacement diagnostics and retained durable browser state. This hook proves finalization failure, not remote DisposeAsync failure.
- Actual console continuous owner permits Stage→Ready custody while live; an outer failure later invalidates its availability without falsifying any already-issued terminal/publication outcome.
- Producer failure before/after cleanup, original lease release failure where a supported real seam exists, null health result, final context failure and uncertainty all remain unavailable. Do not claim unsupported remote-disposal or browser-scope-disposal fault injection covered; current BO Dispose has no such hook.
- Source/owner checks use genuine Stage, real FS/leases/registry and current browser admission. Pure state tests can cover transition algebra but cannot qualify private issuance.

No changes to ordinary bytes, helper source eligibility, pending/browser recovery, QTE offer, full S, F1, M3/cold, T197 or other open gates. No new approval request follows from these routine lifetime APIs. FINALSTOP.
