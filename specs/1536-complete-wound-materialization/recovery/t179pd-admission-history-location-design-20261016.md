# T179-Pd admission-history location correction — 2026-10-16

DESIGN ONLY. Assigned Astra/High; backend unknown. Source baseline 1b73649fb718ccca15b7fe47a465997ee0947c2a. Repository read-only; no build, discovery, tests, application/runtime, network, fetch/push or delegation. This new /tmp pair is the only write. All qualification checks below are FUTURE/UNRUN.

## Ruling and correction

Prefer private hot OwnedAdmissionHistory before P0, frozen into the existing M3 immutable source/common proof and committed by the existing taken common/native publisher. There is no demonstrated need for a separate durable filename. The previous report's proposed game_state/control/mortal_joint_admission_history.json was an unnecessary design expansion, NOT an accepted path grant. Its separate-file recommendation and claimed need for an original-time durable seed are superseded by this addendum for the bounded hot-origin P0 contour.

A separate self-hashed file supplies no stronger provenance than equally owner-produced nested M3 evidence. The legitimate boundary is actual private production, exact source/generation/original binding, one-time candidate sealing, native atomic ownership, and cold semantic verification. Existing trusted-local storage is sufficient for recording irrecoverable observations; no external credential/signature authority is needed.

The earlier pair remains byte-exact:
- MD SHA256 38d4d925026748391dc3b7b99aa8bbd5656516b479f0f6781f7ecec2c9316fb7
- JSON SHA256 8129b2d77dfb2de95c06abcf97fd95bfb2d055c2da19089fd9e26aa44061e8c2

The new location does not discharge the earlier semantic/source-closure obligations. It reduces the persistence surface, not the required evidence.

## Existing source supports nesting

Paths in this section are under BookOfEternityClient unless otherwise specified.

1. MortalQteDecisionPublication.StatePath is the existing game_state/wounds/mortal_qte_decision_publication.json (Services/MortalQteDecisionPublication.cs:16).
2. Services/MortalQteDecisionPublication.MortalJointRepair.cs:154–200 creates the current v2 marker ONLY from an actual probed taken receipt, actual plan/resolution/receipts, exact prepared footprint, consumed wave and progression source. It is the model for a separate v3 writer, not a permissive v2 extension.
3. Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs:292–316 builds exact changes, obtains the owner-produced completion, adds the SAME StatePath, binds all intended members, publishes the complete list through PublishLocalFilesAsync and observes committed batch. Nested immutable bytes do not require another path or transaction.
4. Services/MortalJointRepairPublicationPreparation.cs:61–80 already includes StatePath in the complete plan-owned cohort and requires touched/before coverage. Services/MortalJointRepairPublicationTransaction.cs:27–34 allows the genuine publisher to freeze intended members once; :61–79 verifies full readback plus actual marker reader. This must remain the same candidate/take/fence with no side marker write.
5. Current cold v2 reader (MortalQteDecisionPublication.MortalJointRepair.cs:203–302) recovers native debt, rereads the exact marker, checks strict shape, actual G/original and physical cohort plus named semantics, then brackets the proof with a marker reread. V3 needs a dedicated complete cold checker using this ownership pattern.
6. FileSystemManager.TrustedLocalStorage.cs:167–241 provides existing declared-member exact publication. The journal's cleanup (TrustedLocalFilePublication.cs:498–510) does not remove the marker itself; durable observations nested in the committed marker survive that cleanup.
7. Canonical design lawful-active-joint-tail-b.md:71–81 names existing M and COMMON = existing accepted common set + M schema3, with membership not expanded by that row. Implementation plan T179-Pd step7 expressly calls for complete immutable common proof in the existing M pathname, same prepared/take/normalizer/native/readback chain.

Hence nested storage has an actual source-backed writer seam. There is no current structural or transaction requirement to add the proposed control file.

## Actual owner and hot lifetime

Proposed private APIs; none are implemented or qualified by this report:

GameEngine.OwnedAdmissionHistory:
- BeginFromIssuedOriginal(actual original-preparation owner result): private factory invoked at genuine original admission/dispatch, not by parsing an existing manifest later. The production source seam is GameEngine.SessionAndSnapshots.cs:513–569, where actual original manifest/authority are created. Hook the actual successful preparation/dispatch lifecycle; no durable original rewrite is necessary for a hot-only seed.
- RecordValidationRoute(actual route enum), RecordRepairStarted(actual issues/packets and actual existing start sample), RecordRepairSubmission(actual Ready OR worker owner outcome), and RecordValidationObservation(actual finite source-reader observations) are private calls from real owners.
- Records cover the whole relevant operation from genuine seed to this admission. No caller NoPreviousRepair, upstreamAccepted, synthetic frame or “missing record means empty” branch.
- History survives actual repair waits and lease turnover as engine-private operation state. It does NOT retain an active canonical lease across GM/user waits. Each recording/capture boundary checks the actual current G/original/operation; S→C→B capture later retains its actual participating lease and owner identity.
- FreezeForSubmittedSource(actual lease, actual original, actual source session) returns a privately issued FrozenAdmissionEvidence once. It owns detached exact data and a source seal. It does not grant take/publication.
- Hot reentry reuses the same frozen evidence/C/candidate. If a new repair is required before a seal, follow actual attempt abort/renewal semantics; do not mutate a frozen evidence object or append after seal.
- Cold reads never reconstruct this private hot object. They derive a different lease-local VerifiedActiveP0 after physical and semantic proof.

The hot seed is meaningful because it is actual retained control flow from original issuance; its serialized origin row alone is not proof that a random imported history is complete. Once published, the named client-owned marker protocol is the supported durable owner observation record. The cold reader checks all independently derivable relations and relies on that owner's recorded observations only where history is intrinsically irrecoverable.

## Closed codec placement and acyclic sealing

The existing planned schema is CLOSED: lawful-active-joint-tail-b.md:217–249 lists v3 root = JointFields v2 plus activeTail and a closed basis. R2 ordinary-outcomes.md:64–74 requires immutable common proof but does not currently spell out a full serialized admission-evidence slot. Do not hide new fields in arbitrary inputImages/path maps, actionBoundarySource refs, or an old encoder.

Concrete recommendation for the next exact schema amendment:
- retain v3 root shape and existing M path;
- explicitly add basis.admissionEvidence as a closed immutable source-evidence member, updating the canonical closed basis field list and encoder version together;
- include it in the genuine S/raw-basis/common input/prepared seals and final basisFingerprint;
- retain the already required immutable C/common-proof representation for actual result certificates; it references this pre-existing admission evidence, not vice versa.

This is a PROPOSED explicit codec refinement for review, not a claim that the current closed schema already accepts this field. An equivalent specifically named immutable common-proof admission slot is valid if the final schema chooses it. The critical requirement is one explicit fixed slot captured before seals, not optional unknown-field acceptance. No root/schema2, issued original or v2 hash grammar changes.

Suggested exact admissionEvidence data union:
{
 version, policyVersion, sourceEncoderVersion,
 binding: {generation,sessionId,requestId,snapshotToken,turn,realm,originalManifestHash,originalAuthorityHash},
 origin: {kind:issued_original,sourceLabel,originalRequestTimestamp},
 route: [closed ordered owner route/repair transition rows],
 sources: [finite owner-derived source role/image/disposition rows],
 observations: [closed clock/metadata/comparison observation rows],
 finalOrdinal
}

No validated flag, empty-errors claim, imported issuer, arbitrary paths or arbitrary clock delegate. Image records preserve exact bytes/absence; read errors preserve the existing policy's actual branch, not a successful empty substitute. Ordinals are owner call order, not newly allocated receipt IDs. sourceEncoderVersion identifies exact finite policy coverage. Full reasoning remains required.

DAG:
hot original/repair history + admitted sources → frozen admissionEvidence/S → one retained C → raw B → CommonAuthority/input/prepared/cache seals → taken owner → exact M3 bytes + data cohort → native commit/readback → fresh P0 proof.
The evidence contains no final M/root/basis hash or native commit ack. Do not embed M recursively in itself. Mutable later phase receipts do not alter admissionEvidence or recalculate original samples. P0 common commit evidence is projected only at the accepted owner boundary and stays excluded from earlier seals.

The v3 writer receives only the genuine taken plan's already-sealed FrozenAdmissionEvidence (or actual owner-linked immutable typed projection), never a separate caller DTO. Before preparing bytes, recheck source/evidence seal, same resource/continuation/reservation owner and same candidate/fence. After take, serialization may encode known sealed data, but cannot add missing history, sample UTC or mint IDs.

## Cold algorithm and historical trust

ReadVerifiedActiveP0Async(files, actual lease), proposed:
1. Resolve native publication debt; read existing G without creating it; strict version-first M dispatch. V1/v2 stay on their existing exact paths. Unknown/incomplete v3 holds.
2. Parse nested evidence as DATA only. Reject missing/extra/duplicate/aliased fields, unknown policies, invalid ordinals/route transitions, malformed exact image encodings and recursive marker references.
3. Obtain genuine original authority/manifest/control and full physical cohort independently. Check all binding coordinates, original source hashes, original-derived branch facts, strict initial source roles and phase-specific dispositions.
4. Verify route completeness according to the named owner protocol and immutable original/repair-source relationships. Derive canonical-freshness applicability using the SAME policy predicates. The marker's claim of no prior generic repair is not a skip instruction. A random checkpoint/Ready or absence of history is never a seed.
5. Reconstruct typed validation inputs from authenticated original, actual unchanged current bytes, named common transformations and retained owner-observed submitted images. Verify exact read closure and conditional roots. Failed required reads cannot become absent or valid. Full reasoning and actual special-action/combat/pending-memory/no-offer policies remain.
6. Pure shared validators recompute semantic decisions and diagnostics. For metadata freshness, use the genuine owner's recorded historical start/metadata/error/fallback samples, recompute the existing target selection/max/fallback/output comparison, and verify each referenced source image/disposition. Do not claim these historical samples are current physical source facts.
7. For comparisons, use proven no-emission projections where equivalent. Observable existing temporary GUID/time values may be inputs only via the same named owner's recorded actual invocation/ordinal; verify all semantic consequences. No scheduler, source reducer, allocation or new UTC calls during cold proof.
8. Verify complete C/resource/effect/wound/companion/lifecycle relations, D9 exact initial receipt and source/time distinction, pure automatic/story plans, final immutable seals and full phase cohort. Missing owner semantics remains HOLD.
9. Reread marker, G and guarded source/cohort to close races under the actual supported lease. Issue distinct VerifiedActiveP0 only after all checks. P1/later work stays pending; no new QTE or player turn advancement.

Portable original integrity is SHA256-PAYLOAD-JSON (PendingTurnSnapshotAuthority.cs:971–988); constitution:124–130 defines trusted local use. A separate file cannot cryptographically prove a historical clock either. Under this model, an actual named owner's persisted observation is legitimate input; a public success boolean or self-consistent hashes are insufficient because cold must still verify actual original/G/cohort/source and all computable owner semantics. Deliberate rewriting of all local authorities is not the threat model. This does not excuse GM/caller-injected evidence or mixed/stale sources.

## Before-P0 crashes: honest bounded scope

Hot-only history is lost before P0. If process restart finds no committed M3, the cold reader cannot conjure the missing seed or earlier observations from current Ready/original/checkpoint. Missing genuine history means internal HOLD/no P0 claim. Do not rerun mechanics/IDs to reconstruct a committed prefix, fabricate no-repair history, silently upgrade v2, or treat current timestamps as historical samples.

Native attempted publication is different:
- native recovery settles the actual existing exact batch;
- known committed full M3 contains its nested evidence and enters the cold proof;
- known rollback/no M3 provides no accepted P0 history proof;
- unresolved debt remains held by its actual recovery owner.

This is sound for a bounded internal unactivated P0 contour. It is NOT complete pre-P0 crash-resume behavior for all lawful F1 outcomes. The full lifecycle must choose and qualify a recovery strategy before player activation. Loss of ephemeral proof cannot silently become a final public refusal policy.

## Alternatives for later pre-P0 durable lineage

| Option | What it provides | Exact changes and limitations |
|---|---|---|
| Hot history → nested M3 (recommended now) | Finite genuine end-to-end internal P0 without new durable path | Hot original issuance required; loss before P0 holds. Existing M only; explicit closed evidence slot and complete cold semantics required. |
| Versioned existing repair request owner | Earlier durable repair observations on an existing pathname | Current ValidationRepairRequest is not this proof: fields :87–108 omit actual start/boundary/merged history; writer :4032–4179 deletes/replaces artifacts and worker success may return without legacy request. Need an explicit owner-generated versioned history member, continuity-preserving replacement/deletion rules, exact-before/after native publication and both Ready/worker branches. Same-change GM/docs/examples/source guards. Not currently implemented or accepted merely because path exists. |
| New-version original preparation/control contract for future originals | Durable initial seed tied to actual new original issuance | Extend actual producer/validator only for newly issued versions. Never rewrite an already-issued original. An immutable seed alone does not preserve later repair observations; those still require a real existing control-owner update or eventual nested M3. Requires exact original/browser/console/source-guard review, not a parser fallback. |
| Separate new control path | Can retain pre-P0 history if separately designed | No stronger provenance; adds member/cleanup/recovery/schema obligations and a new path grant. Not necessary for bounded nested P0 and not assumed accepted. |
| Earlier write of existing M before common | Could be durable on same pathname | Changes M lifecycle from common publication evidence to pre-common staging; would require explicit phase/version/transaction owner design. Not part of this recommendation; do not side-write M or reuse completed-v3 semantics. |

Existing repair specifics:
- actual repairStartedAtUtc and request.DetectedAtUtc are distinct samples;
- Ready.UpdatedAtUtc is GM correlation, not client observation authority;
- generic merged errors/fallback live in locals;
- WriteValidationRepairRequestForSessionAsync deletes old repair files first and may choose worker dispatch before exposing the legacy request;
- WaitForContractRepairAsync :3352–3355 returns true for real worker apply acceptance without Ready creation.
Thus “reuse repair request” is a substantive versioned owner/lifecycle extension, not just reuse of available JSON storage. Publishing real history there must precede any cleanup that could discard it, and all actual consumers must preserve the new owner field. Current diagnostic-only metadata cannot be promoted to authority.

If requirements later simultaneously demand (a) exact pre-P0 crash resume of erased historical observations, (b) unchanged old control/original/M pre-P0 semantics, and (c) no new path, those three cannot all be met with hot-only state. The smallest next decision is an explicit existing-control owner/version/lifecycle extension, or a qualified existing rollback/restart route preserving semantics. Only if those are concretely unsuitable would an additional path become a proposed grant. No such necessity is proved here.

## Documentation, tests and open prerequisites

Internal hot-state/leaf extraction with identical emitted original/request/Ready/v2 bytes: explicit no-update rationale is possible. New emitted M3 nested schema requires canonical design/codec/source-guard synchronization, even though GM cannot author it; user-facing guidance must accurately preserve internal/no-offer status.

A versioned public repair request or original/control extension additionally triggers AGENTS same-change GM/docs/examples synchronization. Existing specific surfaces: TaskGuides/CLI_Step_Main.txt:695–717; Examples/E_CLI_Step_Main.txt:853–921; OtherGuides/GM_Worker_Bridges.md:690–709; Examples/example_validation_manifest.json; ExampleDocumentationValidationTests and owning source guards. Shared afterlife repair changes also require the applicable matrix/control inventory/examples/guards. No proposed new filename should be added to live docs as if implemented.

Finite acceptance sequence, all UNRUN:
1. Prove genuine original-issued hot seed, actual original-first then corrected submission, ordinary route and full seven-root read closure; no retrospective seed from existing bytes.
2. Prove severity-only no-applicable-freshness route versus actual mixed/raw/generic earlier repair. Missing/truncated/foreign/forked histories fail; preserve every lawful route as future F1 obligation.
3. Freeze evidence before C/B/common seals; verify clone immutability, drift after C, foreign genuine owner/reservation, repeated seal, after-seal evidence attachment and v2-upgrade negatives.
4. Cold pure comparison/freshness checks including changed metadata with same bytes, exact boundary equality, actual unavailable-metadata fallback, differing clock samples, observable projection values, and worker-direct branch before enabling it.
5. Existing same common/M native batch cuts, lost reply, retained P0, fresh-process/distinct-root source/G/cohort semantics; mutated semantic fields plus recalculated data hashes still fail. No new storage pathname in prepared set.
6. Crash before freeze/take versus native attempted/committed P0: missing hot history must HOLD; full-M3 cold succeeds only with all independent owner relations. No fallback ordinary publication, new scheduler/IDs/UTC, cleanup or QTE.
7. Independent source/fixture/selection and actual evidence gates. No fullP0 claim until complete cold proof; complete pre-P0 recovery remains an explicit broader activation prerequisite.

Recommendation: adopt nesting as the design correction; drop the separate-path recommendation. Review the precise closed M3 admission-evidence slot and private owner interfaces, then implement the already authorized coherent S→C→B→same-candidate/M3 proof work. No additional permission is inferred or requested here. T197 implementation, new QTE/player activation, full F1/T179/T192, D4/guided/visual/Windows/D8 remain open.

FINALSTOP.
