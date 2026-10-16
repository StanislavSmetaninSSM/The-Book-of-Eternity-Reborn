# T179-Pd admission → S → same candidate → M3/cold semantic proof — 2026-10-16

DESIGN consultation only. Assigned Astra/High; serving backend unknown. Baseline 3d262e0303169f2cefb17384e898967e85290e26. Repository read-only; no build, tests, discovery, application/runtime, network/fetch/push or delegation. Only this new /tmp pair written; previous consultations immutable. Exact historical source/blob/SHA pins and completion status are in the companion JSON. Every proposed check is FUTURE/UNRUN.

## Decision

Adopt a two-part verifier architecture:

1. Shared deterministic, read-only validation kernels and owner-specific transition/correspondence checkers independently verify semantics against authenticated original, actual G, phase-current cohort and retained source evidence. They do not execute source mechanics, allocate identities or trust saved “passed” results.
2. Historical observations that affect acceptance require a genuine durable observation authority, or proof that the observation cannot affect any policy result. A captured timestamp/temporary GUID plus a self-hash is not that authority.

The first part has concrete extractable seams. The second requires a new narrow owner-record implementation for canonical-repair observations and some guardian comparisons; existing source does NOT already provide that record. The existing trusted-local storage model is sufficient in principle: no secret signature or new credential authority is needed. This design specifies that missing owner/schema below, rather than treating historical facts as independently recoverable from current bytes. Keep active take/M3 publication blocked until the required owner records and cold semantic checks are implemented and reviewed. No repeated permission is needed for the already-authorized staged work.

The former report’s “retain first-pass results” is sufficient for hot one-attempt reuse, not cold semantic authority. This report makes that distinction explicit.

## Existing evidence and why it stops short

Source paths below are under BookOfEternityClient.

- Services/WoundAcceptedTurnPlanner.MortalJointRepairCommonCalculation.cs:10–64 retains C privately on the genuine resource owner, permits one attempt, rechecks source before/after calculation and retains actual input/artifacts. This is the correct hot ownership model; it has no durable reconstruction API.
- Services/MortalJointRepairPublicationPreparation.cs:8–97 binds complete before/member inventory, exact plan/binding/generation, private common owner and sealed candidate. It expressly is not a durable completion proof.
- Services/MortalJointRepairPublicationTakeReceipt.cs:18–76 and AcceptedTurnAuthorityRegistry.MortalJointRepairPublication.cs bind the actual take slots/fences/owners. Reference identity, issuer and one-time consumption must not be deserialized.
- Services/MortalJointRepairPublicationTransaction.cs:13–98 binds intended members once, requires known committed batch, reads full before/after inventory and invokes actual joint reader before minting its private ReadbackProof. ReadbackProof is process-local.
- Services/MortalQteDecisionPublication.MortalJointRepair.cs:203–302 is the useful cold pattern: recover native debt, strict marker shape, current G, genuine original/header/quartet, exact physical footprint, consumed command set, unique treatment history suffix, recomputed guaranteed finalization, source opportunity/receipt partition, output binding, inactive tail and progression semantics. Its hash checks are only part of the proof. This is v2; it does not prove full submitted-output admission or a D9 lifecycle ledger.
- Services/MortalQteDecisionPublication.MortalJointProgression.cs:164–210 rederives frozen calculation, checks actual original schedule/header/control and phase-current schedule/report images. Recomposition is allowed for this named pure calculation; it does not authorize replay of source mechanics or make arbitrary stored clock values authentic.
- Services/EffectLifecycleExecutionLedger.cs:10–78 has private hot issuance and two-pass binding. It exports data but has no cold semantic verifier or import-to-authority method. Fingerprint and IsComplete are not cold proof.
- Core/TrustedLocalFilePublication.cs:16–20 publication result is a returned record. Its journal is a process-crash recovery mechanism, explicitly not protection against concurrent owner edits (:94–100); cleanup removes scratch, intent/commit stage and active journal (:498–510). No durable per-commit observation attestation remains.
- Core/FileSystemManager.cs:2019–2021 CanonicalFileReadSnapshot contains bytes and LastWriteTimeUtc. This proves a current physical observation when read through the real owner; after a file is replaced, it does not prove an earlier observation.

Therefore do not serialize a hot issuer token, receipt.ReferenceEquals result, observation log, or transaction-shaped JSON and call it “cold owner proof”.

## Exact proposed boundaries and files

These are proposed names, not existing APIs.

A. Core/GameEngine/GameEngine.MortalJointSubmittedAdmission.cs
- Private OwnedSubmittedAttempt retains actual engine operation, lease, G, original frame, resource reservation/continuation, source session and lifecycle state.
- CaptureAndValidateSubmittedAsync() can run only once in Capturing. It invokes shared kernels through the finite source reader and returns a private HotSubmittedAdmission.
- States: Capturing → Captured → CommonCalculated → Sealed → Taken → Settled; failures before take → Aborted; storage-debt state delegates to existing native recovery. No second source validation, C reduction or allocator run on the same attempt.
- Reentry consults actual retained state; it cannot replace source/session/C. Abort releases only the owner’s still-unattempted slot. A cold reader never manufactures this hot attempt.

B. Services/Validation/ValidationService.MortalJointSubmittedSource.cs
- Explicit owner-created read session from the prior 73-leaf consultation; finite typed Current/Original/Control/Rollback/Metadata IDs, authenticated original callback core, complete read-failure latch, per-invocation caches.
- Ordinary and new internal paths share the same policy kernels; their source adapters differ. No mutable global overlay, caller map, generic virtual filesystem or arbitrary time delegate.
- CloseCapture() freezes exact consumed roles/bytes/presence, original lookup statuses, branch selections, metadata observations and detached comparison observations.
- Source errors cannot enter a successful certificate. Preserve the distinction between fatal authority/content read failure and the existing freshness policy’s explicitly handled metadata-unobservable branch; the latter still needs genuine historical provenance.

C. Services/Validation/AcceptedOutputValidationKernels.cs and narrow existing partial extractions
- Closed SubmittedOutputInputs built only by the actual source reader, with actual source family structs. Internal pure kernels consume typed values, not a replaceable filesystem.
- ValidateSubmittedOutputSemantics(inputs, comparisonContext) returns the same ordered issue codes/severities and semantic facts as the existing roots; always invokes Full reasoning and preserves real conditional early returns.
- Individual pure helpers are shared by old ordinary wrappers and cold checking. A cold capsule is untrusted input to a verifier, never directly a SubmittedOutputInputs authority.

D. Services/Validation/AcceptedOutputComparisonSemantics.cs
- Owner-specific non-emitting projections for observable fields, with explicit semantic observation definitions. No whole-root “ignore all timestamps/IDs” rule.
- Where equivalence is not proved, return ComparisonProofUnavailable. This blocks M3; it is not an ordinary gameplay rejection rule.
- Hot cached results remain diagnostics/reuse data. Cold executes the shared pure predicate over authenticated inputs and independently checks any necessary witness.

E. Services/MortalJointSubmittedAdmissionEvidence.cs
- Strict DATA codec only; ParseUntrusted() returns ParsedAdmissionEvidence, which cannot issue any capability.
- Writes only through the actual taken common publisher’s v3 marker preparation; no side M write.
- The proposed evidence is an immutable part of the v3 common proof core, referenced by raw source/basis as described below. Updating that closed v3 schema requires the corresponding tracked design/codec/docs synchronization; v2 field sets and hash grammar remain exact.

F. Services/MortalJointActiveCommonVerifier.cs plus owner-local verifier partials
- VerifyCompletedCommonAsync(actualOriginal, actualGeneration, actualPhaseCohort, untrustedCommonProof) returns VerifiedCommonP0 only after resource/effect/wound/companion and lifecycle checks all succeed.
- A named owner predicate receives before/input/claimed result, checks its relation, coverage and published corroboration; it never calls DiscoverCommonMechanics, CompleteCommonMechanics, CalculateUnboundedCommonArtifacts, AdvanceLifetime, a mutation reducer, scheduler, normalization writer, or identity factory.
- Reuse immutable parsing, authority catalogs, arithmetic/eligibility predicates by factoring them from producer code. Do not fork a second policy implementation.

G. Services/MortalQteDecisionPublication.MortalJointActiveProof.cs
- ReadVerifiedActiveP0Async(files, lease) is the only cold issuer. It obtains actual G/original/current cohort itself, performs the steps below, then issues a distinct lease-local VerifiedActiveP0Capability.
- No constructor from a parsed marker, imported phase receipt, hot diagnostic witness or accepted boolean.
- Capability is P0 retained/held only. Public v2 dispatch remains default; no QTE offer, cleanup or later-phase progression follows from its existence.

## Acyclic source, calculation, proof and commit binding

Maintain four separate values:

1. S: exact admitted original/current/guard inputs plus validated action/time source and finite source roles. Hot admission binds actual owner/lease/G/reservation; its public fingerprint is comparison data.
2. C: same retained actual computation, including completed effect plan, resource boundary transcript, both lifecycle passes, ordered histories/identities/carrier/companion outputs. No C recomputation for B.
3. Raw B: automatic/story/actionBoundarySource plus references to immutable S/C evidence. No final common, marker hash or commit receipt in an earlier seal.
4. Immutable common proof core: closed source evidence + complete typed result certificates. CommonAuthority/input/prepared/cache/take bind S/C/raw B and exact full inventory once. Final basisFingerprint incorporates that immutable core. The committed P0 receipt is derived after actual native commit, never fed back into S/C/raw B.

A stored copy of the source does not authenticate itself. Its cold role disposition is derived from genuine owners:
- authenticated_original: exact logical path/presence and bytes authenticated by the actual original authority;
- guard_current: exact actual phase-current file/absence, unchanged by the qualified cohort;
- submitted_current retained unchanged: actual phase-current bytes plus original/control/admission semantics;
- submitted_current consumed or transformed by common: retained before image plus a named owner’s independently checked exact transformation/correspondence to the actual common after image and command/history source. If the relation loses information that remains relevant to validation, require a separate genuine retained source artifact; do not assume that any possible preimage was the one submitted;
- common_after: exact named owner output, matched against actual phase-current cohort and semantic certificate;
- later_phase_replaced: requires the specific later owner’s independently verified before/after relation and receipt; currently outside P0 acceptance. Transaction-shaped phase JSON cannot provide this by itself.

No recursive M/prior-marker embedding. Current G must already exist; never GetOrCreate it during cold proof. Retained source artifacts are not deleted until the actual owning cleanup gate.

## Proposed closed evidence codec

Place this data under the new v3 immutable common proof, not in v2 and not as a mutable phase acknowledgement. It is DATA until verified:

submittedAdmission = {
  version: 1,
  policyVersion: exact compiled version,
  binding: {generation, sessionId, requestId, snapshotToken, turn, realm, incarnation},
  sourceClosure: ordered array of SourceEvidence,
  validations: fixed PolicyEvidence slots,
  comparisonEvidence: ordered typed ObservationEvidence,
  freshnessEvidence: not_applicable | observed,
  upstreamSubmission: exact corrected-submission owner evidence reference
}

SourceEvidence is a closed tagged union; each branch has exact fields:
- original: {kind, logicalId, originalProofRef, imageRef, lookupStatus};
- current/control: {kind, logicalId, imageRef, dispositionRef};
- rollback: {kind, logicalId, originalProofRef, backupCoordinateRef, imageRef};
- metadata: {kind, logicalId, observedImageRef, metadataProofRef}.
No arbitrary path keys. Each logicalId is accepted only in the production-derived finite family; dynamic physical original/backup coordinates come from the genuine original owner. Source roles and conditional read coverage are rederived; unknown/missing/duplicate/aliased refs fail.

Each exact image has closed exists/bytesBase64/digest fields; null bytes iff absent, canonical base64, strict UTF8/BOM policy where JSON, duplicate/confusable/extra fields rejected. Hashes index data and detect mismatch; they never establish provenance.

PolicyEvidence slots are the seven named roots plus the already required raw/player/level/time checks. Fields: {policy, version, inputRefs, comparisonRefs}. Do NOT encode authoritative “validated:true”, “zeroErrors”, skippedRoot, or a caller phase selection. Cold kernels derive issues and branch decisions themselves. An optional saved diagnostic digest is not an authority input and cannot suppress checks.

ObservationEvidence branches:
- source_value: exact original/current typed scalar with source ref; cold rereads and checks it.
- non_observable_projection: a fixed kernel/observation version with source refs; cold uses the shared non-emitting kernel. Caller cannot choose arbitrary ignored paths.
- owner_observed: {role, invocationOrdinal, value, causeRefs, durableOwnerProofRef}; the proof ref must resolve through a genuine named owner verifier. This branch is NOT implementable from existing self-hashed M alone.
Do not serialize random temporary IDs merely to make the verifier reproduce their equality. Either their exact value is observationally irrelevant under a proved kernel or it has independent source/publication identity evidence.

Freshness observed branch:
{policyVersion, repairBindingRef, repairedIssueRefs, repairStartedProofRef,
 targetObservations, fallbackProofRef, resolvedBoundaryUtc, outputObservations}.
Each target/output observation is typed present(time,imageRef) | absent | metadata_unobservable(reasonClass, proofRef), preserving actual policy; arbitrary exception text is not proof. The checker derives target list, fallback/max selection and strict output > boundary test. The fields above are claims unless their refs have independent provenance.
not_applicable requires the actual upstream repair-owner proof showing no qualifying retained canonical repair errors; a caller “no repair” flag is invalid.

The deterministic codec closes syntax and dependency graph. It does not solve the historical provenance gap described below.

## Cold verification algorithm and finite dispositions

1. Use existing actual storage recovery admission to settle its own journal; do not infer absence from unresolved debt. Read stable marker bytes and version first. v3 never falls through v2/ordinary continuation.
2. Strict-parse untrusted v3. Read current physical G and genuine original/header/artifact chain with same-lease source-aware reader. Match full tuple/source label/control lineage and original presence rules.
3. Derive the exact fixed-plus-owner-contribution cohort and source-role dispositions from the named owners, not receipt maps. Read actual full cohort and unmodified guards; reject mixed/extra/missing members, foreign source and command debt.
4. Independently verify submitted source mapping and all seven policy kernels against authenticated/reconstructed values. Preserve Full reasoning, special actions, memory, combat and real no-offer controls; use the same 73-leaf source families. Do not treat the saved source closure as an executable read allowlist.
5. Verify C owner semantics and complete event/history/identity/companion correspondence, including exhaustive roster and two-pass lifecycle coverage; compare actual published results, not only plan hashes. This is checking a claimed transition, not planning or executing it again.
6. Resolve historical observation witnesses and metadata freshness. Unavailable required provenance produces HistoricalProofUnavailable. It cannot be repaired by taking a new clock sample, replaying a normalizer or changing a warning/error.
7. Recompute deterministic automatic/story/raw basis data, closure/source fingerprints and immutable common seals using genuine derived inputs. Verify exact initial D9 receipt: consumed owner-turn requires complete owner proof plus common commit; P1 progression remains pending with frozen inputs and no phase receipt. P0 does not acknowledge P1.
8. Recheck physical original/G/marker/cohort/read dependencies after awaits. Only then privately issue VerifiedActiveP0Capability; classify known committed P0 as RetainedP0Verified and HOLD later phases.

Closed reader results: NoV3Marker (not a successful prepublication authority), NativeDebtUnresolved, InvalidSchema, SourceConflict, UnsupportedPolicyVersion, HistoricalProofUnavailable, SemanticProofUnavailable, RetainedP0Verified. Every nonverified result retains source/debt and offers no ordinary fallback, rewrite, cleanup or new allocator work. A known committed P0 whose proof is incomplete is retained but unverified, not generic publication uncertainty.

## Owner semantic checks without repeating mechanics

The completed ledger is evidence to CHECK, not a script to execute. EffectLifecycleExecutionLedger supplies input/authority/event/initial/completed refs and passes; EffectAcceptedTurnPlanner:6314–6500 records event ordinals, matched roster, causal_created/uses_mode/other_pass/evaluated dispositions, effective events and actual reduction outputs. A cold verifier must independently derive roster and exclusions from authenticated carrier/target/source state, prove exact input/pass coverage, and verify claimed local transitions.

Concrete seam: factor effect lifetime predicates into EffectLifetimePolicyRelations, shared with EffectLifecycleScheduler:217–300 and :660–845. Proposed CheckLifetimeTransition(before,event,processedRefs,claimedOutcome,claimedAfter) checks all immutable fields plus the exact permitted field delta without constructing a result or issuing events/IDs. Example turns mode: validate positive/non-999 remainingTurns and exact phase; different phase means no_change+identical image; matching phase with one remaining means expire+null; matching phase >1 means advance with exactly remainingTurns−1 and all other fields identical. Target/realm-loss precedence and all other modes must share the existing predicates. Calling AdvanceLifetime again is forbidden.

Likewise resource/wound/companion verifiers check actual source authority, exact arithmetic/materialized IDs, unique history suffixes, source-to-output one-to-one correspondence and full unaffected field preservation. Existing v2 treatment finalization/receipt/source logic can be shared. General Pc C has no complete cold verifier yet; neither the private retained C nor lifecycle ledger replaces it.

Temporary identity proof is distinct from canonical identity proof. Canonical IDs are read from actual committed typed indexes/history and checked for uniqueness, provenance and correspondence; no allocator is invoked and no dummy IDs are substituted. Omitted/duplicated event, all-filtered/no-eligible claim, unmatched history row or extra output must fail even with every hash recomputed.

## Nondeterministic comparison equivalence: exact limits

Define the observation of a validation helper as its returned diagnostics (code, severity, policy-relevant data), branch decisions, actor/authority facts consumed downstream, and source dependencies. Removing a generated field is valid only when that complete observation is unchanged for all allowed inputs, not merely for one fixture. Two different emitted JsonObjects need not be equivalent.

A useful proved source slice:
- NpcWorldAndMeta:944–985 normalizes residents while extracting actor updates.
- BuildResidentEntryFingerprints :1788–1805 normalizes the whole root but hashes only resident entries; alias/structured update helpers read entries and authored update arrays.
- GuardianAbodeResidentState.NormalizeShape:408–516 unnecessarily normalizes receipt/history sibling arrays, whose missing timestamps use UTC at :1202/:1213/:1234/:1242.
An entries-only detached normalization helper can serve the actual entry/alias/fingerprint consumers without constructing those unused timestamp fields. Share it with ordinary validation and preserve raw authored update arrays. Tagged resident outcomes also require a consumer-by-consumer observation slice; do not globally replace NormalizeShape for all callers.

A counterexample to blanket timestamp/ID erasure:
ValidationService.LifecycleControlAndStateFiles:4738–4748 MaterializedGuardianMatchesAuthorityOutsideTradeResolutionSurface removes only tradeInventory and receipts, then compares the rest of the guardian semantically. Guardian authority projections can populate reputationHistory.timestamp/completionDate and abodePower history/lastUpdatedAt. Those values may reach the whole-object comparison. Ignoring them, existentially choosing a timestamp to fit current output, using a dummy GUID or alpha-renaming all IDs would change policy.

Other explicit volatile branches remain in the prior consultation: GuardianProjectHelpers GUIDs/journal power events; GuardianPowerEventState fallback appliedAt; AbodePowerRules fallback lastUpdatedAt; relationship projection UTC; normalized soul memoryLegacyGrant ID/UTC; DTO initializers. Some outputs (discarded journal arrays, soul fields other than returned realm/incarnation, overwritten DTO defaults) may be removable after precise dataflow/equivalence proof. Others influence whole comparison results. Do not declare all of them non-observable without that proof.

For an observable historical value, hot capture+retention prevents repeat work but does not prove cold provenance. Either an existing independently verifiable source value determines it, a genuine named durable owner witness supplies it, or M3 remains blocked. The cold verifier cannot use a saved “projection equal” assertion.

## Historical clock/freshness provenance audit

Existing candidates and their actual limits:

- Original LiveTurnPreparationService manifest stores RequestTimestamp and is bound by original manifest/authority. It is the original request timestamp, NOT repairStartedAtUtc, validation UTC or the last canonical-repair boundary.
- GameEngine.PrivateImplementation:87–108 ValidationRepairRequest stores DetectedAtUtc, attempt/errors and resubmission requirements. It has no start/fallback/boundary/target-output observation receipt.
- ValidationAndRepair:354 samples repairStartedAtUtc BEFORE WaitForContractRepair. Request.DetectedAtUtc is separately sampled later (:4078). Replacing the former with the latter changes policy.
- ValidationRepairReady:206–213 carries UpdatedAtUtc and tuple; this is correlation/GM submission metadata, not a client mtime attestation.
- ValidationAndRepair:374–389 holds merged repaired errors, start, fallback and resolved boundary in locals. Metadata-unobservable fallback can be DateTime.UtcNow. Current serialized repair request does not reproduce merged prior-attempt history or the exact fallback observation.
- Native common transaction/readback verifies bytes and known hot commit; its durable recovery journal is later deleted. Existing M v2 does not retain/prove these historical observations.
- Current unchanged physical source metadata can be checked while still present at P0. This independently supports only the observation of those CURRENT files. If common replaces a target, later phases replace it, a target was absent/error, or a fallback/start observation is required, present bytes+mtime do not reconstruct the historical condition. Even untouched target mtimes do not identify which earlier start/fallback was actually used.
- Existing pending original snapshots authenticate preturn bytes, not later submitted file mtimes. Reusing their timestamps or resetting file mtimes is not evidence.

Indistinguishability argument: two executions can have the same original, final physical cohort, G and copied M fields but different prior repaired-target mtime/fallback clock observations, one satisfying the original freshness policy and the other not. Once the distinguishing source is overwritten and no independently trusted observation remains, a deterministic cold reader of that same state cannot tell the histories apart. More hashes or cross-referenced JSON do not restore the missing fact.

This is not a demand for protection against an arbitrary malicious machine owner; it identifies an information/provenance hole in the requested independent cold check. The existing trusted physical-storage boundary can be the root of an owner witness ONLY if its durable issuance/immutability/read contract actually preserves that witness. No inspected API currently does so for these observations.

## Smallest missing owner/schema extension and alternatives

Recommended direction: implement deterministic non-emitting kernels first, and design a narrow durable observation witness in the EXISTING repair/storage owner for the remaining actually observable historical facts. It must preserve the existing policy, not replace it with a timestamp heuristic.

Necessary witness contract:
- Actual repair owner captures exact start, selected/merged issue lineage, target image+metadata/error observation, actual fallback sample and output image+metadata at the validation point under the live source session.
- Its private preparation binds existing G/original/repair tuple and exact source bytes; no caller date/map or DTO issuance.
- Named native publisher commits witness with the accepted source/common action in the same exact owner cohort, or a genuine earlier retained repair-owned publication whose lineage and unchanged artifact the cold reader independently verifies.
- Durable reader authenticates issuance/immutability through the existing actual owner/physical source chain, then supplies a typed VerifiedHistoricalObservation. Merely storing an observation object in M or another existing control with a self-hash is NOT an implementation of this contract.
- Witness survives journal cleanup and all later transformations that need it; phase-specific before/after correspondence checks preserve its lineage. Final cleanup retires it only under actual terminal proof.
- Original snapshot and G are not rewritten/rotated to retroactively attest a later observation.

The closed schema addition plus genuine named owner/publisher/reader is a D11 specialization of complete source/result evidence, preserving existing freshness/XP/time semantics. The trusted-local native publisher is the existing persistence primitive; the missing part is an explicit versioned repair/admission observation owner using it. No new credential, secret, signer, privileged service or protection against deliberate offline save editing is required. A schema alone is insufficient: implement the private producer, exact mutation ownership, continuity rules, shared semantic checker, native settlement and cold reader together. The precise concrete extension is below.

Alternatives:
1. Retain physically verifiable source artifacts and metadata untouched through all dependent phases, with actual existing owner coverage of their lineage. No new credential is intrinsically necessary if this existing trust can be demonstrated. Costs: storage/retention/cleanup and exact metadata source contract. It does not solve independent clock/error observations unless they also have genuine provenance. Existing original artifacts do not already provide this for submitted repair state.
2. Prove a comparison value non-observable and remove its generation from shared validation kernels. Best for discarded projection values. Does not remove an explicitly observed repair mtime/error/fallback policy.
3. Re-run current freshness or source mechanics after restart: rejected; changes historical question/clock or duplicates work/IDs.
4. Trust hot saved verdict, receipt self-hash, transaction-shaped map or marker pair cross-hash: rejected.
5. Qualify a bounded internal initial P0 contour with proven inapplicable freshness/volatile branches: possible only through complete genuine owner route/history and semantic proof as specified below. A fixture alone or missing metadata is insufficient. Such qualification is not whole F1, player activation, or permission to globally reject other lawful outcomes.

The unresolved implementation obligation is the new narrow client-owned record and its semantic verifier, not a missing cryptographic primitive. Without that implementation or a demonstrated policy-inapplicable contour, the general cold guarantee is unavailable. Historical clock/read-error observations must be trusted as observations of this real owner within the existing local contract; their downstream acceptance consequences must still be independently recomputed. No design can independently recover an erased historical clock sample from final bytes alone.


## Actual entry applicability and smallest trusted-local owner extension

### Trust model correction

The constitution (.specify/memory/constitution.md:124–130) explicitly excludes deliberate offline save editing and concurrent external editing from the supported threat model, while retaining schema/source/generation/recovery integrity. PendingTurnSnapshotAuthority.cs:11–14, 428–436, 971–988 uses portable version 4 SHA256-PAYLOAD-JSON. The comment explicitly says this is local lifecycle integrity, not a same-user cryptographic security boundary. “Authenticated original” in this report means this exact supported owner/source contract. Do not impose a new signature or external witness requirement.

A durable client-owned observation record can therefore be an authority for the fact “the genuine owner observed this time/error/value”, just as the original owner records its request timestamp. It cannot certify “validation succeeded”. Its legitimacy comes from the named private producer, correct source binding, versioned client-owned lifecycle, native publication and independent semantic reader—not merely from an attacker-model claim about a self-hash. Coordinated malicious rewriting of every local authority is outside this task. Arbitrary caller records, GM-provided observation records, accidental mixed tuples and stale/forked records remain invalid.

### Exact existing route, and what correctedReady proves

There is no method named TryPublishMortalJointRepair in inspected source. The actual entry is GameEngine.MortalJointRepairCanonicalPublication.cs:18 PublishCurrentMortalJointRepairAsync, called at ValidationAndRepair.cs:2284.

The concrete call order is:
- ValidateAcceptedTurnOutcomeWithRepairLoopCoreAsync :510–543 initializes this invocation's repair locals and enters its raw loop.
- :550–578 performs actual upstream raw validation and the resource/guaranteed-treatment joint intake.
- MortalQteWoundDecision.cs:66–82 sets correctedReady only from actual Owned joint intake and absence of resource-stage errors.
- First rejected source capture and subsequent correction are in MortalJointRepairAdmission.cs:100–170. The private checkpoint must exist, packet-owned severity changes only, scene must match, and the genuine adapter must revalidate. It is rejected-source provenance, not full submitted-output or history acceptance.
- Raw errors at ValidationAndRepair.cs:660–799 cause real WaitForContractRepair; the loop stores lastCriticalRepairErrors, repairStartedAtUtc and a resolved boundary before continuing.
- When raw errors clear, :807–813 enters RefreshAcceptedTurnCanonicalStateForValidationAsync. That method loads the actual original baseline at :2276, chooses the joint route at :2280–2285, BEFORE generic RefreshCanonicalStateAsync at :2288.
- The returned JointPublication causes an immediate dedicated return at :849–858, BEFORE generic baseline/postseal/pending/canonical repair and ValidateCurrentGameStateOrShowErrorsAsync at :1095.
- Existing dedicated completion invokes real output checks at MortalJointRepairCompletion.cs:90–115, but does not call the generic merged freshness helper.

Therefore a first joint correction can reach the intended entry without entering the generic canonical repair loop. Wound correctedReady does not inherently require generic merged canonical history. Equally, the existing flag/checkpoint/manifest do not prove that no earlier repair path ran: raw errors may be mixed or repeated, source might be reconstructed, and the checkpoint carries only wound rejected-source provenance. Earlier generic iterations before a later joint capture are not ruled out by the current branch flag. “No previous repair” is also inaccurate because the wound correction itself is a repair.

The minimum bounded contour is “actual joint rejected-source correction, complete owner-recorded repair lineage, no prior generic canonical publication/repair, and no applicable canonical-freshness issue”, not “absence of repair metadata”. Classify each actual record with the SAME IsCanonicalStateRepairIssue/IsPlayerFacingNeutralActorMemoryRepairIssue predicates (ValidationAndRepair.cs:2809–2842). Wound severity logical coordinates alone do not satisfy game_state/lore or effect/resource materialization predicates. BindMortalQteDraftRepairTarget (MortalQteWoundDecision.cs:286–303) changes editable-file targeting, not the original issue FilePath. Do not assume all WoundRepairContext issues are excluded; derive from complete exact returned issues/packet families. If every recorded repair is noncanonical under this predicate, CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues returns no issues (:2692–2701); a computed but unused raw fallback clock cannot affect that policy. If any applicable issue exists, use the full witnessed historical policy. This preserves the current policy and does not invent timestamp equality.

Current original/Ready/checkpoint cannot certify that COMPLETE lineage after restart. A real owner record is required even for a durable “inapplicable” claim unless a genuine earlier original-admission publication explicitly anchors the entire bounded history. No current file inspected supplies that whole route record.

### Concrete proposed owner/codec and API

Add Core/GameEngine/GameEngine.SubmittedAdmissionHistory.cs, Services/SubmittedAdmissionHistoryState.cs and Services/Validation/AcceptedOutputObservationSemantics.cs (names proposed). This is a single named owner protocol, not generic filesystem virtualization or an arbitrary logging platform.

Private GameEngine OwnedAdmissionHistory:
- Created at actual new-turn/original-admission lifecycle, before accepting the first GM submission. Seed binds actual G, sessionId/requestId/turn, original manifest/authority hashes and actual initial dispatch source; no caller-supplied NoPreviousRepair flag.
- Real production seam: GameEngine.SessionAndSnapshots.cs:513–569 builds/writes actual pending manifest and detached authority under its lease. Extend the actual original publication completion path, not merely LiveTurnPreparationService's helper. That helper and browser/console original owners must use the same seed contract where applicable.
- For bounded initial implementation, maintain actual private lifecycle from that genuine preparation through the same operation. A cold legacy original without this versioned seed is not upgraded to a new-history proof. It remains on v2/default route or explicitly held WIP.
- EnterValidationRoute(actual owner route enum), BeginRepair(actual ordered errors/packets), CompleteRepair(actual observed Ready/resubmitted source), RecordFreshnessObservation(actual source reader), RecordComparisonObservation(actual shared projection) are private producers. They receive actual objects/reads, not caller history JSON.
- FreezeForSubmittedAdmission(actual lease, original, sourceSession) returns immutable PreparedAdmissionHistory only after validating complete route and current sources. Reentry reads the retained object; it does not resample times or regenerate projection IDs.
- Reader APIs: ParseUntrustedHistory(bytes) returns data only; VerifyHistoryAgainstOriginalAndPhaseAsync(files, lease, original, phaseCohort, parsed) derives a private VerifiedAdmissionHistory. ValidateHistoricalFreshness(verifiedHistory, exact images) is a pure shared kernel. Current source readers remain the 73-leaf finite families; no injected clock delegate.

New fixed client-owned path proposed: game_state/control/mortal_joint_admission_history.json. It is not an existing source fact and is not emitted by this consultation. Use an explicit new schema/domain, not additions tolerated by old v2 closed markers. Keep old original authority/manifest bytes and old marker grammar immutable; do not retrofit the seed into an already issued original. The seed may be a separate new owner record bound to immutable original, issued as part of the genuine original preparation's declared completion batch. Avoid recursive original-hash/seed-hash references: original core first, seed refers to that immutable core, publication contract covers both. Native debt blocks dispatch until known settlement.

Strict DATA shape:
{
  schemaVersion, domain, policyVersion,
  binding: {G, sessionId, requestId, snapshotToken, turn, originalManifestHash, originalAuthorityHash},
  origin: {kind: original_admission, sourceLabel, originalRequestTimestamp},
  sequence: [
    validation_entry {ordinal, route},
    repair_started {ordinal, parentOrdinal, exactIssues, exactPacketRefs, startedAtUtc},
    repair_submitted {ordinal, parentOrdinal, submissionKind, requestImageRef, submissionEvidenceRef, submittedSourceRefs},
    freshness_observed {ordinal, parentOrdinal, mergedIssueRefs, targetObservations,
                        fallbackObservation, outputObservations},
    comparison_observed {ordinal, invocationRole, inputRefs, volatileObservations}
  ],
  frozenSourceRef, finalSequenceOrdinal, integrity
}

Each row is a closed tagged union with fixed roles, no arbitrary path maps. Ordering/parent indices derive actual call order, no generated receipt ID. submissionKind is a closed ordinary Ready versus actual worker-accepted direct-revalidation union: OtherGuides/GM_Worker_Bridges.md:700–709 documents that successful worker apply with failed Ready publication proceeds by actual worker owner proof, without a fabricated Ready. The implementation must bind the real dispatch/apply disposition and native settlement; unsupported capture of that branch holds internal P0, never rewrites ordinary worker policy. This branch is not yet source-closure-qualified by this consultation and requires its owning source audit before enabling it. Exact issue fields used by classification and diagnostics remain available; never substitute just codes if path/actor/category/repair context affects policy. OriginalRequestTimestamp copies actual original solely as binding, not as repair time. startedAtUtc is the existing sample at the existing callsite; request DetectedAtUtc remains its separately sampled value; Ready.UpdatedAtUtc remains GM correlation. Metadata-unobservable branch records the actual existing caught observation category and exact policy fallback. Physical source bytes/presence tie each observation to the validation source image; mtime is an owner observation, not retroactively promoted to current file metadata.

No serialized route row itself becomes an execution permission. Verification checks closed route transitions, contiguous ordinals, exact tuple/original/retained repair source, authentic original/current/phase disposition of each referenced source, complete applicable issue lineage, every conditional policy read, and pure classification/boundary/freshness/semantic outcomes. Recompute max/fallback policy from observations; never trust a saved boundary or pass flag. The cold reader relies on the genuine local owner record ONLY for irrecoverable observations (historical clock/metadata read outcome/actually used volatile value); all consequences remain independently checked.

Persistence:
- Use existing FileSystemManager.TrustedLocalStorage.cs:167–241 PublishLocalFilesAsync/native exact-before/after batch and settlement. Require existing G first; cold never calls this method, because it can GetOrCreate G.
- Publish repair-start/history update with the actual repair request at the existing WriteValidationRepairRequestForSessionAsync/WriteValidationRepairFileForSessionAsync boundary. Preserve exact original sample timing: thread the sampled start into this named owner internally; do not substitute DetectedAtUtc. The current request writer does NOT already implement this atomic record; it needs adaptation.
- Complete/observation records may be held privately until frozen admission and written with actual common P0, provided a crash before that write gives no P0 claim. If a pre-P0 restart must resume a historical observation, persist that observation with its real repair-owner transition first. Never reconstruct missing rows from Ready or a current mtime.
- When common P0 commits, the complete frozen history/observation artifact is a named fixed member of the same prepared inventory, take fence, native batch and full readback. M3 references its exact immutable content; later phases preserve that content or use an independently checked owner transition. Do not rely on the deleted native journal for witness retention.
- Generic request cleanup must not delete frozen history needed by retained P0; only the actual final owner cleanup may retire it. This is a specific lifecycle change with tests/docs obligations.
- Frozen history is never accepted from the GM response, imported DTO, arbitrary path list, fake private issuer or mutable global service. Client-owned source guards and exact-before-image checks reject altered live records.

The smallest first bounded route can record only the actual joint raw severity correction plus its original-admission seed and validation entry, provided the owner refuses this INTERNAL route if another route/repair already occurred. That refusal stages unimplemented coverage; it cannot become the final public F1 behavior for lawful mixed histories. Whole F1 later records every applicable raw/generic merged repair route using the same source policy, and qualifies those cases before activation.

### Volatile comparison observations

Prefer the demonstrated non-emitting entries-only projection where observationally equivalent. Where a real existing validation projection's GUID/time affects a whole comparison, the same genuine hot invocation must record each allocation/sample under a fixed invocation role and ordinal, with exact input refs, in this owner artifact. Cold pure comparison consumes VERIFIED observation values and checks the same relation; it never invokes Guid.NewGuid, DateTime.UtcNow, schedulers or emitting normalizers. This is replay of a pure predicate over recorded inputs, not reexecution of the source work or allocation.

This can preserve existing policy without guessing IDs/times, but each observable operation still needs explicit extraction and conformance coverage. Do not feed the same frozen value into distinct original live samples, change sample order, or accept a value selected to make equality succeed. The producer must record the actual original samples in order. All new clock-taking/ID-taking hooks remain private production dependencies tied to actual first-pass work, never a caller time delegate or dummy-ID factory. The prior list of unresolved whole-root projections remains a finite implementation inventory, not a proof of qualification.

### GM-facing contracts and versioning

Internal unactivated leaf extraction, private attempts and pure predicate refactors with identical emitted request/Ready/original/v2 bytes may use an explicit no-update rationale, subject to source guards proving those contracts unchanged.

The proposed durable protocol is a different case. If emitted, the same change must:
- add the named client-owned history artifact to ownership/path/rollback/original-preparation and cleanup/recovery contracts; state the GM must preserve it and cannot author observation rows;
- synchronize any repair request/Ready field, version, timing or resubmission obligation that the GM reads; preserve existing field meanings and show an actual corrected-submission example;
- update applicable main/agent/daemon guidance, validation manifest/example and source-guard/docs tests, and applicable Mortal/afterlife coverage if a shared repair owner changes;
- explicitly document the internal no-offer routing and version dispatch; do not present it as available ACTIVE gameplay;
- retain old closed v2 and already-issued original schemas byte-for-byte. No permissive legacy parse, silent raw upgrade, or absent-history-as-empty fallback. New original/observation schema dispatch requires actual new owner issuance.

Concrete existing surfaces include TaskGuides/CLI_Step_Main.txt:695–717 (repair/full resubmission and client-owned write prohibition), Examples/E_CLI_Step_Main.txt:853–921 (request/Ready worked contract), OtherGuides/GM_Worker_Bridges.md:690–709 (dispatch/worker-owned direct revalidation), Examples/example_validation_manifest.json, and BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs. Shared afterlife effects require OtherGuides/Afterlife_Pending_Control_Surface_Inventory.json:702–706, OtherGuides/Afterlife_Contract_Matrix.md, Examples/E_CLI_Afterlife_Turns.txt and the corresponding documentation/example source guards. Extend the actual Write-BoeJson/Complete-BoeTurn client-owned-path guard for an emitted history artifact. Exact additional filenames follow the impacted emitted contract, not a claim that all docs always change. AGENTS.md requires same-change synchronization for GM-read control contracts even if fields are client-owned. This design grants no agent implementation permission and reports no runtime result.

## Finite TDD and acceptance sequence — all UNRUN

1. Preserve actual existing v2 path and genuine original→corrected submitted source fixture. Add genuine original-owner history seed and exercise raw severity correction versus mixed/raw/generic prior repair; absent/stale/truncated/forked history never proves inapplicability. Retain old request/Ready schemas and bytes when using internal extraction only. Establish causal RED at missing submitted-owner/cold proof, not invalid setup or fabricated frame.
2. Shared reader/root extraction: seven-root Full coverage, source-error latch, exact original/current roles and every 73-leaf conditional family; unsupported-read and drift controls; ordinary diagnostics unchanged.
3. Per-kernel no-emission equivalence: same inputs, ordered diagnostics/actor/authority facts across missing/present/invalid fields and current/original branches. Vary clocks/temporary IDs and prove independence ONLY for the proposed observable slice. Counterexample tests retain full guardian comparison sensitivity. Zero calls to normalizer writers/scheduler/identity factories.
4. Private hot attempt S→actual once-retained C→raw B→same candidate. Two genuine owners/reservations, source drift after C, after-seal attachment, abort/reentry and clone isolation negatives.
5. Cold owner checkers before publication: feed real completed owner plans/history to checkers, then alter each claimed disposition/transition/ID/history/ref and recompute all data hashes; semantics must still fail. Cover both lifecycle passes, no-eligible/all-filtered, nonplayer roster, intermediate carrier chain and full common companions.
6. Historical provenance test gate: real repair start/request timestamps differing, multiple repair attempts, absent/unobservable target, equal output boundary, same bytes changed mtime, common-overwritten target, later phase replacement, copied foreign observation receipt and whole-marker rehash. No P0 until an actual durable owner proof passes these cases in a fresh process/distinct root without the warm owner.
7. Only then integrate whole common+M3 batch and fresh-reader P0 hold. Test every member/marker cut, lost reply/native recovery, exact repeat, mixed before/after, malformed/unsupported policy, source/G/header/control mismatch and semantic forgery with recomputed hashes. No source reducers/schedulers/allocators or new UTC samples in the cold trace.
8. Verify automatic/progression/projection/story/cleanup remain pending at P0, no new QTE offered, runtime turn unchanged and original/control retention intact. Known committed P0 must stay retained on later invocation, never fallback to ordinary/v2 work.
9. Independent actual SOURCE/FIXTURE/SELECTION and existing Late2/EVIDENCE gates precede each execution boundary as parent policy requires. This report contains no runtime qualification.

## Final limits

A complete codec plus hot private retention is not yet a complete cold proof. The concrete missing contracts are owner-specific C semantic checkers, precise non-emission equivalence for observable comparison consumers, and independently anchored historical observations. The last one has no implemented general record in current source; the proposed trusted-local owner extension supplies a concrete route, with source closure for worker-direct and all later phase dispositions still requiring owner audit. It is not a demand for a new cryptographic authority.

No fake context, empty response, allocator replay, timestamp substitution, whole-root equality exemption, imported receipt authority, skipped Full scope, new reward/level/alias policy or broad afterlife permission is proposed. Default v2 stays INACTIVE; public ACTIVE/QTE/F1/T197 implementation/activation and full T179/T192, D4/guided/visual/Windows/D8 remain open. Full P0 emission stays blocked until the cold semantic proof is real.

FINALSTOP.
