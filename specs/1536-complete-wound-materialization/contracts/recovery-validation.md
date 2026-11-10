# Provisional cached DATA admission — revision54 contract

LEASEDATA1..8/T453–457. r53 exact byte cache remains the sole DATA store. Only a
genuine stored snapshot/absence can bypass repeated filesystem admission at a
selected declared read wrapper. Current physical lease/manager/entry/registration/
ancestry/rootsegment checks remain live/pure. First binding and any miss retain
original full checks. Recorded successful normal-reader dispatch selects exact
family; no path IO/routing guess before lookup. Each hit clears the mutation receipt.

Cached DATA is provisional, never a grant/capability. Original full MainAdmission/
worker/physical checks and dependency closure remain at sources, effects, success
and adapter close. Arbitrary callback/checkpoint must fence before dropping cache
and calling outward after fast results; independent recovery may discard old DATA
only with its own unchanged fresh proof. Generic service lookup distinguishes
Found(null) and NotFound without computation or poisoning. Strict scopes/passive
suppression/failed or retired lifetimes cannot take this route. This explicit
amendment supersedes per-wrapper full admission ONLY for these genuine DATA hits,
not MainAdmission.Validate generally or standalone namespace/authority APIs.

# Whole-operation source DATA — revision53 contract

FILEOP1..8 / T448–452. Existing ValidationOperation stores detached file snapshots
under operation-interned ordinal(path, reader-family, explicit-mode) key objects.
Operation root is actual recovery/completion/story owner; no JsonObject-root cache
identity and no second payload dictionary. Lease/capability facts remain local.
Only keys/order/version in fresh-dependency ledger. Every original algorithm and
explicit live/source check remains; nested pure proofs may consume provisional
DATA until mandatory take/write/delete/callback/lease/runtime/success fence.

Fresh closure obtains expected payloads from existing service before clearing,
reads originals uncached, compares bytes/absence/time/generation and verifies live
adapter/root version. Missing expected entry fails; root epoch is not a raw drift
detector. No old adapter/capability survives lease close. Actual writes invalidate;
successful fresh no-evidence recovery alone does not. Unknown/unsafe evidence and
failed admission close reuse, preserving original recovery disposition.

Default strict readset and standalone verified helper remain unchanged; selected
owning route omits duplicate per-proof exits. All no-write success branches and
turn advancement still fence. Pre-effect contract is explicit awaited
owner fences plus synchronous mutation refusal for dirty DATA and one-use exact
adapter/root-version receipt atomically checked at mutation Begin. Synchronous
read adapters accept immediate completed sync computation only. Independent
journal recovery/compensation retire DATA and retain original owning recovery
authority; stale closure equality must not block rollback. Existing independent
fresh take/callback protocols discard/bypass source DATA and retain complete
physical source checks. No global sync-over-async fence is added.
Full source diagnostics are unbounded andD-only. Same original600/820/15 retained.

Revision52 PROOFREAD3b: strict per-request read-set admission remains the default.
Only the stable post-recovery read helper selects original-reader admission.
That mode adds no duplicate full lease check at read-set dispatch: original
explicit entrypoints and physical readers remain unchanged. Exact retained flow,
pending decisions, cancellation, declared lease, overlap/failure guards still
apply to every request. Full fresh authority remains at Open/Complete, with every
source/generation freshly checked before any plan result escapes. No cached
authority/namespace observations, no cross-proof cache or relaxed exit proof.

Revision52 actual-flow clarification PROOFREAD3a: retain exact entry MainAdmission
separately from the physical lease owner. It may be a participating descendant
only with an uninterrupted acquired/live same-root/same-access chain to that
owner and the original exact ambient lease registration. No extra physical or
metadata frame; no closed/closing/bound-closing frame. Reserve using entry ancestry,
but retain original physical lease/admission. Every read/exit must still have that
exact captured top and live chain; new descendants/siblings/late closed flows are
not interchangeable. Existing atomic MainAccess reservation predicates and all
source/fresh exit checks remain unchanged. No authority result is cached.

# Full-proof reserved-read integration — revision52

PROOFREAD1..6 / T444–447. New internal generic FileSystemManager helper
ReadStableRecoveryDataAsync<TResult>(CanonicalWriteLease,Func<Task<TResult>>)
owns only an optional existing ReservedValidationReadSet: TryBegin, Open,
original read plan, Complete, then return; finally DisposeAsync. Preserve primary
failure when disposal also fails. It stores no proof/capability itself and adds
no alternate cache. Nested/unsupported routes retain existing reader semantics
and enclosing owner's completion responsibility.

ReadJointCompletedProofAsync retains recovery+stable marker comparison, then
dispatches the unchanged remaining validator through this helper. Extracted
private body consumes the same fs/lease/inspection/root/stableMarker and returns
the original CompletedProof only after all original checks; helper finalizes fresh
physical exit validation before caller receives it. No live authority check or
recovery is memoized; no bytes/absence/timestamps/generation check is skipped at
the fresh exit. Existing QTE readset behavior and all negative controls remain.

# Progression DATA integration — revision51 addition

PROGDATA1..6 / T439–443: add typed result support to the accepted r50 adapter,
using the same general service, source counters, root dependency, failure closure
and fresh scopes. Source result and every returned mutable image are detached.
The only new semantic key is the private root object shared by the two progression
calls inside a phase-reader invocation; it is never exposed/mutated on that path.
A later parsed root is a new key, even for equal bytes. No authority is retained.

Both recovery calls remain before the first reusable DATA computation. The outer
phase owns a new operation only if no operation is already registered. Borrowed
contexts retain ownership, dependency eligibility and fresh state. Any phase
failure closes reusable state. No-evidence/failed/compensated/other-manager
mutations invalidate; unsupported/active mutation routes run source without reuse.

Keep all original checks before/between/after the two DATA sites. Raw external
editing is still observed at fresh publication boundaries, not detected by epoch.
No new valid-save/GM/player/schema/native behavior; no prompt/example updates.
Diagnostics are uncapped and D:-only. Diagnostic-off is a separate owner-authorized
observation experiment, not changed gameplay assertions or increased budget.

# Recovery validation adapter — revision50 exact contract

Issue1536 / RECACHE1..6. DefaultOperationValidationService remains the actual storage/coordinator; no alternate cache algorithm or fake repository.

- Root CanonicalValidationEpoch: Begin(object mutation), End(object mutation), TryCapture(out object? version). Distinct reference identities; rotate object epoch at both actual begin/end; no capture with active tokens. No IO/callback. Root identity owns one epoch shared across FileSystemManager instances. Foreign/duplicate end cannot restore old version.
- MainAccess.BeginMutation installs its exact root token before effects under state; ReleaseMutation removes root token last after local token/cleanup. MainAdmission.TryCaptureRecoveryValidationVersion(CanonicalWriteLease lease,ValidationReadReservation? compatibleRead,out object? version) checks original active retained physical frame, no foreign/sibling closing/remote/worker/browser/pending/legacy decision, coherent root capture under WorkerContextGate -> state -> epoch. No physical admission/probe per capture. Lease/source live wrappers retain their prior full guards independently.
- FileSystemManager.BeginRecoveryValidation(CanonicalWriteLease lease,bool reuseEnabled=true): synchronous caller-flow registration of one operation scope; no nested scope; exact owner/lease retained. Unsupported routing selects original fresh work, never invents eligible version. Dispose drops operation payload references and closes late descendants. Optional reuseEnabled is a meaningful typed baseline seam selecting fresh work on the same production path, not authority bypass.
- FileSystemManager.ValidateRecoveryDataAsync(OperationValidationRule rule,object inputIdentity,CanonicalWriteLease lease,Func<Task> validate): actual successful DATA validation fact; request/source/reuse names include stable rule.Name. Scope dependency is complete root/lease identity. Ineligible or diagnostic calls invoke original body without enrolling in coordinator; eligible serial calls use ValidationOperation.ValidateAsync. Cancellation/fault/late-close does not grant cached success. Inputs and rule remain scoped/private; immutable descriptors capture no payload.
- FileSystemManager.ForceFreshRecoveryValidation(): restoring IDisposable? scope clearing reuse before entering and after exiting an external/fresh boundary; nested validators run original bodies. Diagnostics continue. InvalidateRecoveryValidationAfterFailure closes reuse while preserving primary failure. Passive diagnostic routing uses existing suppression mechanism with new operation scope activation; observer descendants do not poison or join functional cache.
- Mechanical rule encloses original evidence.RequireCurrent, ReadSnapshot/frame equality and exact before-image comparison; RequireOwner identity of evidence and original owner/generation/fingerprint/binding remain live. Continuation rule encloses original snapshot/frame, before-images and ReadPublicationBaselines agreement; HasValidSeal/current owner/generation before and confirmed-held resource claim afterward remain live. Exact private proof instance supplies input identity, all retained DATA private/copied. Fact results hold no authority object.
- Fresh registry wrapper covers ValidateInventoryCurrentAsync+Authority.ValidateCurrentAsync before both in-memory takes. Later taken-input/transaction source checks also force fresh. Arbitrary finalization callback is bracketed before/after for its full await. Read hooks that may mutate exclude reuse; passive documented diagnostics do not disable source optimization.
- Coverage:40 direct existing BeginValidationMutation sites, plus public EnsureDirectoryStructure delegation and outer PublishSessionReplacement/PublishLoadReplacementNamespace/PublishLoadReplacementImages envelopes. Other raw external writes have no epoch and are not declared tracked: original fresh publication fences must detect them; ineligible callback routes never reuse across them.

Performance proof uses the same actual source algorithm with Dictionary and disabled reuse, real files/metadata and minimal signed original DATA. No reflection, fake authority issuance, whole-world setup or diagnostic loss. Original real scenario retains600/820/15 limits and all assertions/native closing. Current JointOperationTimings already records cumulative completed observer gate/serialization/write costs; analyze rather than replace it.

Matching QTE reservation eligibility: ReservedValidationReadSet.TryGetActiveReservation(files,lease,out reservation) checks exact files/lease and successful `_opened` plus healthy/notbusy/notcompleting/notclosed under its own state, then releases that lock before MainAccess capture. `Opening=false` alone is not readiness. Reservation identity admits its existing phase only, never keys or rotates the semantic DATA fact. Three actual resumer acquisition callbacks remain supported through fresh brackets; other arbitrary read/acquisition routes and custom load operations are excluded.

Current-source original package slot is `fixtures/joint-interface-source42-recovery-cache-v1/package`; old source42-readset-v1 and all historical packages remain unchanged. The Windows Fact changes only that immutable input-slot literal. Its existing resumer hook setup also exports complete passive RecoveryValidationMetrics records at each segment close, measured by RecoveryValidation.ExportDiagnostics; no original assertion or runtime budget changes. All Fact identities, assertions,600/820/15 bounds, native lineage and diagnostic collector remain unchanged.

Passive counter contract: each segment exports actual request/source/reuse/fresh-fallback/fresh-boundary/failure-closure totals and root mutation Begin/End transitions, including other managers. The reference epoch remains the authority protocol; numeric diagnostic totals never establish eligibility. Full timing and metadata records remain uncapped. Final common-baseline refusal (including private seal and item slot) closes the operation store and preserves the original exception.

Component candidate fixture: CreateJointRepairCommonPlanFromBoundForComponentAsync accepts only the normal prefix result and calls the same extracted SealBoundJointRepairCommonPlanAsync suffix as production. Registry reservation/stages/input/confirmed-claim checks, capability admission, resource bounds, current sources, private proof issuance, fence and final sealing are unchanged. No caller-supplied proof or replacement authority is accepted. Missing/null input is refused. This avoids redoing a measured44s bound-input prefix when testing the later take fence.
# Revision53 execution boundaries

Source DATA owners complete while their final physical adapter is live. The
standalone progression, story and projection services fence actual no-write
success; selected nested borrowers remain provisional until the outer owner.
Actual runtime advancement calls the tested static GameEngine seam with its real
GameLoop and an atomic verified mutation transition. Raw file changes remain
detectable through fresh original readers, including restored timestamps.

Suspended independent recovery may use the original helper, borrow, fence and
writer routes after a DATA-owner failure; none may restore that failed owner's
reuse afterward. Passive diagnostic descendants bypass this owner. Fixture source
barriers hold actual asynchronous work, never fabricate DATA or authority.

Windows arbitrary legacy acquisition recovery remains independently fresh and
retires the DATA segment; the current measured gain covers admitted stable
segments, not an asserted cross-acquisition cache hit. Trusted-local successful
no-evidence classification preserves its epoch after the original exclusion and
physical evidence check. Real retained journal debt uses its original mutation
and recovery protocol.
