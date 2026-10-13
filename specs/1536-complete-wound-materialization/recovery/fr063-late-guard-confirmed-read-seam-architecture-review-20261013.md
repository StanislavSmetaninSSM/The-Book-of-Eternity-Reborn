# FR063 confirmed-claim inspection seam — independent architecture consultation

2026-10-13. **ACCEPTED as the bounded proposed architecture. No blocking source finding.** This is read-only consultation, not authoring release, final independent SOURCE acceptance, runtime qualification, or FR063 closure. Only this external report was written; no repository/Git/build/discovery/test/provider/gameplay operations were performed.

## Provenance

Reviewed proposal `/workspace/boe-cloud-tools-20261013/fr063-late-guard-confirmed-read-seam-proposal.md`, SHA256 `3930f837cab7268615ce08eccf96b6db801aad645e16fa999c55183263325d77`, against diagnosis `fr063-late-guard-live-probe-readonly-diagnosis.md`, SHA256 `c49b525b7f96e5e07d96455fa45b44d2f9267d25b0ce38377c64d3af1b4796fb`, and actual source bytes. Both document hashes and all seven proposal source pins match.

ROOT reported clean metadata carrier `b9bd24197f4a1ff694fe2b4125801cd5a020103f`, then metadata-only `03b11cf968dd83d101e991bb21dc835853a42451`; code remains `14d037d82198659c509066759dcbb02e32b2b5f7`. No Git/remote verification was repeated. Separate four-case EVIDENCE acceptance reported by ROOT is not re-audited or transferred to this unwritten reader. Its corrected positive and new negatives remain UNRUN.

## Honest retained-claim observation

`Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs:1184–1205` invalidates validated authority on the exact before-image mismatch. Registry `InvalidateCommonValidated` (`AcceptedTurnAuthorityRegistry.cs:3271`) and `InvalidateEffectValidated` (`:3627`) clear validated publication state through `ClearValidatedTreatmentPublicationCore` (`:5480`). With no taken receipt/restart blocker this clears accepted-state publication lineage. These paths do not themselves release or commit the confirmed resource reservation.

The old publication probe (`AcceptedTurnAuthorityRegistry.cs:3958–4015`) first requires `CurrentTreatmentPublicationAcceptedStateAgrees` (`:5190`). Its refusal after cancellation is correct and must remain. The first runtime did not print its issue/state and did not reach subsequent hold assertions. Static reasoning is not evidence that the resource hold was lost or survived.

The proposed `InspectConfirmedMortalWoundTreatmentResourceClaim` can measure a retained claim without reviving publication. It validates the exact active FS-owned lease, reads existing physical G, finds only an existing root slot, checks slot/state G and revision, and calls the same owner's private `ProbeConfirmed`. That function (`MortalWoundTreatmentResourceReservationRegistry.cs:733–825`, request validation `:951–1048`) checks the detached request seal, rehydrated resource authority, existing operation/attempt agreement, ConfirmedHeld state, reservation identity and request/resource/finalization fingerprints. Rehydration validates detached data; it does not reserve or register a new agreement.

Success means the existing registry currently holds that exact confirmed resource claim for the inspected physical generation. It does not confer publication admission. The existing detached result is adequate: expose scalars/diagnostics only, retain underlying invalid issues, and leave `GenerationFingerprint` null because no publication-lineage hash is asserted. `ChangedCount == 0` is merely a read-result property. No capability, plan, authority state or receipt escapes. The Issues collection is read-only; avoid describing it as a deep immutable authority object.

## Required implementation contour

- Null-check all arguments; call `EnsureCanonicalWriteLeaseActive` before generation or slot reads. Foreign/disposed leases retain the existing InvalidOperationException (`FileSystemManager.cs:4100–4116`). Do not translate them into a valid or absent claim.
- Use only `ReadExistingSessionGeneration` (`:5881`, `FileSystemManager.GenerationSnapshot.cs:12`). Missing G yields the dedicated invalid result; malformed/path/IO failure does not become absence fallback or generation creation.
- Use `RootSlots.TryGetValue`, never `GetState`, `GetValue` or `GetForGeneration`. `GetState` (`AcceptedTurnAuthorityRegistry.cs:1600–1614`) can create G/slot or replace stale authority.
- Lock slot then its existing state, as already done by `CanBeginSpiritualOriginalIntake` (`:1675–1682`, `:1841–1847`). Require exact bound G/revision at both levels. The canonical lease excludes participating physical replacement. Add no async or reverse locking inside this contour.
- Invoke existing private `ProbeConfirmed` once and preserve invalid/missing/finalized/conflicting/not-confirmed results. No Export/Restore, Reserve/Confirm, Take/Release/Commit, rearm, cache repair, durable-spelling inference or state writes.
- Leave old publication probe/admission/take/settlement and all existing callers unchanged. The new name lacks `probe` and is excluded by the shared static reflection filter (`MortalWoundTreatmentResourcePublicationLifecycleTests.cs:3400`). Do not accidentally add a matching static helper. Private slot/state instance helpers are outside that static candidate set.

These are supported constraints of the proposed seam, not new gameplay/storage architecture. Relaxing them would change this verdict's scope.

## Five negative constructors

| Row | Supported setup and exact oracle |
| --- | --- |
| `generation_absent` | Separate owned FS root; acquire normal canonical lease without generation/registry initialization. The lease may create runtime lock structure, so capture inventory after lease acquisition. Invoke with the genuine held request/finalization only as negative inputs; require generation-absent and unchanged inventory/G absence. Do not assert that the acquired-lease root has literally zero files. |
| `owner_absent` | Explicitly call normal `GetOrCreateSessionGeneration(lease)` during fixture preparation, then capture bytes. `EnsureDirectoryStructure` alone does not guarantee G. Never call an accepted-turn registry API on that target. Require owner-absent twice and exact G/inventory. Two refusals alone cannot expose an invisible slot allocation; TryGetValue-only source proves that part. |
| `owner_stale` | Use a separate genuine held fixture with `withRollbackAuthority:false`, retaining the same FS/root identity and old slot. Release canonical lease, acquire actual lifecycle and session-replacement leases, call `RotateSessionGeneration(replacementLease)`, dispose those leases, acquire a fresh canonical lease without registry recovery. Assert real G/revision changed; reader must return owner-stale, not rebind. |
| `foreign_lease` | Another independently owned FS's active lease passed to original FS. Expect existing InvalidOperationException before G/slot lookup; preserve both roots relative to post-preparation captures. |
| `disposed_lease` | Retain and dispose/release original held fixture's real lease, then pass that exact object. Expect existing InvalidOperationException before lookup. Acquire a fresh lease only afterward for readback, distinguishing acquisition effects from the inspector. |

The stale row must not rotate the positive late-guard fixture with its actual rollback backup. `FileSystemManager.SessionReplacement.cs:24–32` rejects unresolved storage evidence; `FileSystemManager.TrustedLocalStorage.cs:24–90` includes the rollback namespace. Default `CreateHeldTreatmentPipelineContextAsync` (`MortalWoundTreatmentResourcePublicationLifecycleTests.cs:3011`) skips the `withRollbackAuthority` block at `:3060–3083`, while retaining a genuine confirmed claim. Pending snapshot/input alone is not that rollback namespace. Do not delete evidence, edit raw G or relax guards to satisfy setup.

Actual `RotateSessionGeneration` takes only a lease (`FileSystemManager.cs:5894`); `clearGameState:false` is its implementation, not another argument. Normal replacement publishes a new G and advances revision (`SessionReplacement.cs:80–89`) without replacing the registry slot. Capture the no-write comparison after rotation; rotation itself intentionally changes G and may remove worker/control artifacts. Separately assert the old request remains. Do not call shared recovery/composition afterward, as it can bind new state and destroy this control's premise.

These are concrete fixture interpretations, not blockers or new authority rules. All five rows have supported construction; actual execution remains UNRUN.

## Positive oracle and finite selection

The existing Fact in `MortalWoundTreatmentResourcePublicationLifecycleTests.OrdinaryOwner.cs:257` already uses a real confirmed treatment, normal preparation/validation, initially absent `npc_skills` before-image, lease turnover, a newly present physical file, exact preflight rejection and whole-footprint byte comparison. Replace only the post-refusal call to the inapplicable shared publication probe. Keep the shared helper and old controls unchanged.

Require new inspection IsValid/empty Issues, ConfirmedHeld, exact operation and attempt, exact request/resource/finalization fingerprints, nonempty agreement fingerprint, original actual G/revision and null publication GenerationFingerprint. Derive finalization through the existing pure Finalize path. Preserve persisted request, energy **2**, create-only history and cleanup assertions; emit actual inspection scalars. To substantiate no writes by the new reader, extend the existing whole-footprint comparison through inspection with one post-inspection readback. This is a local assertion, not another scenario/run.

Exact private agreement validation plus these identities makes this a genuine current-claim measurement. Do not replace it with a label inferred from persisted `held`, or reconstruct/re-reserve an equivalent claim. No publication authority is needed for this diagnostic.

One affected positive Fact plus one five-row theory is a coherent finite selection for the new contour. The rows cover new generation/slot/lease gates; existing ProbeConfirmed semantics are delegated unchanged. There is no changed source dependency requiring broad qualified native/warm/reflection reruns. Proposed 2 methods / 6 cases / 2 owners and catalog totals remain prospective until authored discovery. Final independent SOURCE must verify exact selection, old reflection candidate set, shared bodies/membership unchanged and absence of registry reconstruction. No discovered counts or PASS results are claimed here.

## Read-time source pins

First seven match the proposal; the others resolve fixture feasibility. These are byte checks, not runtime evidence.

| Repository-relative file | SHA256 |
| --- | --- |
| `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs` | `3bacf2c2294c88b10acd0d137c153ba2e219acc93903e74cad155081474d4705` |
| `BookOfEternityClient/Services/MortalWoundTreatmentResourceReservationRegistry.cs` | `9bf40c73b2cdc08e2f913d841ff95bb45313f6a30d938546bad38c2a47d3ecac` |
| `BookOfEternityClient/Services/MortalWoundTreatmentPublicationTakeReceipt.cs` | `cd4a90670295cf55b7ef128ee47fd6c38891e6ae2a5b1dbb5768d46d73ec3143` |
| `BookOfEternityClient/Core/FileSystemManager.cs` | `14dcb900d73951bf5228d474255c197324ddb32df74a075ce22f4dec64600ec9` |
| `BookOfEternityClient/Core/FileSystemManager.GenerationSnapshot.cs` | `8e78385fd23440ec87fa0d6843fe578b5e31d129af1ec51792e8701ec644c384` |
| `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.OrdinaryOwner.cs` | `f0b582d3563aed9e1c6f9bc067abe04a7351c639da1ec99465f47c2603ba5a8d` |
| `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs` | `06a3377ad0f0574e6d6bb501c446719b03998fda628082a54a90526534f6cee7` |
| `BookOfEternityClient/Core/FileSystemManager.SessionReplacement.cs` | `beac7a4733bc8de2816c3acedc365b69be19a048efff513cb47ee60c82ef1d21` |
| `BookOfEternityClient/Core/FileSystemManager.TrustedLocalStorage.cs` | `7154852b8ef29afa1262cef0e4bb3a0ff28f14afeebe98c2a0834676e4c5cf5b` |
| `BookOfEternityClient/Core/TrustedLocalFileScope.cs` | `b2f5fd598a1d728bc133944492d36b3e600e5c4293704eb88b26861b4e5c55d2` |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs` | `7395c42f57a5e230cc5fbf3964d3bdd50afca8fedd8ed61decb028bf7854ce92` |

## Disposition

No blocking architectural change is required. ROOT can use this consultation for its bounded authoring instruction. The separate actual Astra XHigh final SOURCE gate and subsequent selected runtime evidence remain necessary. FR063 and the larger feature remain OPEN.

**FINAL STOPPED.**
