# FR063 confirmed-claim read seam — bounded proposal, UNWRITTEN/UNRUN

2026-10-13. ROOT's clean metadata checkpoint b9bd24197f4a1ff694fe2b4125801cd5a020103f; code14d037d remains unchanged. Proposal follows c49b525b7f96e5e07d96455fa45b44d2f9267d25b0ce38377c64d3af1b4796fb readonly diagnosis. No repository/metadata/Git/project/build/test/runtime/provider/agent call or mutation. Only this external proposal was written. Requires explicit authoring release and independent SOURCE before execution.

## Exact read API and result

Add ONE internal registry-owned scalar reader in AcceptedTurnAuthorityRegistry.cs:

```csharp
internal static MortalWoundTreatmentPublicationProbeResult
    InspectConfirmedMortalWoundTreatmentResourceClaim(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourceFinalization finalization);
```

Reuse the EXISTING detached `MortalWoundTreatmentPublicationProbeResult` (TakeReceipt45–96): IsValid, immutable Issues, State, operation/attempt/request/resource/finalization/agreement fingerprints, actual session generation/revision. It contains no live capability/request/plan/receipt and grants no admission, settlement or replay authority. Its legacy type name does not change the new method's explicitly documented meaning: only exact CURRENT confirmed resource claim, independent of cancelled publication lineage. GenerationFingerprint remains null: this reader does not assert a live publication receipt/lineage hash. ChangedCount stays existing0 for a read; exact State/identities must still be asserted. No new result class/file/fingerprint/schema is required.

Do NOT change existing ProbeMortalWoundTreatmentResourcePublicationHold, CurrentTreatmentPublicationAcceptedStateAgrees, take/settlement or any production caller. The selected old publication probe's invalid state after cancellation remains correct. This new method does not contain `probe` in its name, so old ProbeLiveRegistryAsync's static-name filter3399–3464 cannot add it to the unique candidate set.

## Closed read contour, lease, generation and locks

1. Null-check the four arguments and call existing `fileSystem.EnsureCanonicalWriteLeaseActive(writeLease)` BEFORE physical/state reads. Foreign/disposed lease retains existing InvalidOperationException; do not catch it into successful/absent state. This verifies exact FS owner and active lease (FileSystemManager4100–4116). No lease acquisition/replacement is performed inside the reader.
2. Read actual physical G only through `ReadExistingSessionGeneration(writeLease)` (5880–5882 / GenerationSnapshot12–45). Missing G returns an invalid detached read result with null claim fields and a dedicated `mortal_wound_treatment_confirmed_claim_generation_absent` issue. Malformed generation/path authority remains the reader's InvalidData/IO failure, not absence fallback. No GetOrCreate, raw path read or generation mint.
3. Use actual `CanonicalRootAuthorityIdentity` and its SessionGenerationRevision; `RootSlots.TryGetValue(identity,out slot)` ONLY. No RootSlots.GetValue/GetState/GetForGeneration, state constructor, BindRootGeneration or root-cache reconstruction. Missing existing slot/state returns dedicated invalid `..._owner_absent` result, never inferred held/not-required state.
4. Add a small private RootAuthoritySlot inspection method patterned after the existing read-only CanBeginSpiritualOriginalIntake1649–1681. Hold slot._gate and require existing _state, exact physical G == slot._generation and revision == slot._revision. Missing/stale slot refuses; NEVER call GetForGeneration. It then delegates to that SAME existing state. Lock order remains slot._gate then state._gate, matching existing CanBeginSpiritualOriginalIntake. The participating canonical lease keeps physical replacement out while the two observations are checked. No reverse lock or async work is added.
5. The state's new inspection helper holds its existing _gate; require _rootGenerationBound, physical G/revision equal its immutable bound fields, else dedicated invalid `..._owner_stale`. Delegate exactly once to EXISTING `_treatmentResources.ProbeConfirmed(TreatmentResourceRegistryCapability, request, finalization)` (ReservationRegistry733–825). The capability remains private inside its current owner; no caller supplies or reflects it.
6. Return detached existing probe fields and actual inspected G/revision. Copy the underlying Issues without hiding codes. A failed underlying probe stays invalid with its existing missing/finalized/conflict/not-confirmed semantics. Never infer success from durable `held` spelling or publication marker. In particular, no accepted-state object/old lease/factory is used to bypass generation or handoff ownership. The reader needs no publication lineage because it grants no publication authority.

New read failure diagnostics should use treatmentAttempt.resources scope and say inspection refused; they must not manufacture a transaction receipt or label the root accepted. For no-owner/absent-G, claim fields and G provenance may be null; for known physical G/stale-owner, diagnostic G/revision may be included with IsValid=false. Existing publication diagnostics retain their old codes/semantics.

`ProbeConfirmed` already validates complete detached request seal/rehydrated resource authority and exact registered operation/attempt agreement, then ConfirmedHeld, request/resource/reservation/finalization fingerprints. RequestBatch951–1048 rejects missing/released/finalized/conflicting records. This is a delegation of the existing invariant, not a second implementation/reducer. Read only dictionaries/state; no EnsureHealthy/Export/Restore/Confirm/Reserve/Take/Release/Commit/rearm, no field writes or cache invalidation. Do not use ordinary GetState1600–1614, which can create G/slot and replace stale state.

## Guard Fact and finite controls

In ONLY `GuaranteedTreatment_ComponentOrdinaryOwnerGuardsAbsentInputAcrossLeaseTurnoverBeforePublication`, after its already-qualified exact preflight refusal and physical-byte equality, replace the inapplicable call to shared AssertConfirmedHeldLiveRegistryProbeAsync with direct typed new inspection. Pure Finalize(context.Resolution) derives the existing finalization. Require IsValid/Issues empty, ConfirmedHeld, exact operation AND attempt ID, request fingerprint, resource authority fingerprint, finalization fingerprint, nonempty agreement fingerprint and original actual G/revision. Keep existing command/persisted-request, energy2, create-only-history, all source/admission/absence/turnover/raw mismatch/capture and cleanup assertions in the same order. Retain the actual inspection fields in the existing guard observation. No shared-helper change, no accepting invalid publication probe and no recreated hold.

Author ONE additional five-row component theory, using real held-request/finalization fixture inputs and separately owned negative roots/leases:

| Row | Setup and strict oracle |
| --- | --- |
| generation_absent | Actual empty owned FS target with no G; invoke new reader using active target lease and the genuine held request/finalization only as negative inputs. Refuse generation_absent; exact files/G absence remain, no slot/recovery/claim created. |
| owner_absent | Ordinary initialized owned target has actual G but NO accepted-turn RootSlot. Refuse owner_absent; generation bytes/inventory exact, never create a slot. A second read must give the same refusal. |
| owner_stale | On genuine held fixture, release old canonical lease; use existing lifecycle + session-replacement lease and real RotateSessionGeneration(clearGameState:false), then acquire fresh canonical lease without ANY registry GetState/Export/Restore call. Actual G/revision changes while old slot remains. Refuse owner_stale; inventory/old request unchanged and old claim not rebound to new G. |
| foreign_lease | Another independently owned FS's active lease passed with original FS; exact InvalidOperationException BEFORE any read/state lookup. Preserve both roots and G bytes. |
| disposed_lease | Original held fixture's released/disposed lease passed; exact InvalidOperationException BEFORE read/lookup; acquire a fresh owned lease only afterward to assert complete bytes unchanged. |

Generation/ordinary bootstrap or supported rotation is ONLY an explicit NEGATIVE fixture precondition; the inspector itself cannot mint G or recover anything. Do not manually construct a resource reservation, source authority or private registry. These negative components are not new-root native completion proof. Missing/malformed/null generation remains covered by strict existing reader; do not add a broad parser matrix. Wrong request/finalization continues existing ProbeConfirmed's exact guards, not a caller-blessing route.

Expected author inventory: existing affected positive guard1 + one new theory5 =2methods6cases. Add one exact owner for ONLY new negative theory/budget2, keep old late1 owner budget2 unchanged. One new narrow task-local selection joins these two owners:2methods6cases2descriptors/default9; old native/reflection/warm/full selectors remain exact and qualified controls excluded. Static expected catalog727/11568 methods (current726/11567 +one owner/+one method), contingent on authored discovery. No alias semantic-count inflation or old membership redistribution. ROOT chooses first negative/control actual then existing failed guard1 after SOURCE/preparation/EVIDENCE; no repeat of qualified six/native/postseal or now completed reflection/warm4 without a concrete changed dependency.

## Minimal files and synchronization

Production: ONLY `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs` (new non-authorizing outer reader + private slot/state delegation). Existing ResourceReservationRegistry and result type remain unchanged. Test: ONLY OrdinaryOwner.cs (one call-site oracle replacement + one new negative theory/private local assertion as needed); old lifecycle/shared probe helpers untouched. Metadata: catalog prepend, one new narrow selector, active plan/tasks exact counts/status. Dated plan may receive only a status append if ROOT requires it; no UI/GM/prompt/contract/schema/archive/newroot pipeline edits.

No GM-facing capability/gameplay/authoring, afterlife rule, durable record or public draft changes: private owner inspection returns diagnostics/scalars only. Record no-GM synchronization rationale in active checkpoint. Preserve old publication candidate set via name filter and statically verify all old shared source bodies/old selectors/catalog suffix/ce955/checkbox history. No runtime qualification is implied by this proposal. Full FR063/original15/pending2/newroot/T070/#1536 remains OPEN.

## Exact source pins relied on

- `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs`: `3bacf2c2294c88b10acd0d137c153ba2e219acc93903e74cad155081474d4705`.
- `BookOfEternityClient/Services/MortalWoundTreatmentResourceReservationRegistry.cs`: `9bf40c73b2cdc08e2f913d841ff95bb45313f6a30d938546bad38c2a47d3ecac`.
- `BookOfEternityClient/Services/MortalWoundTreatmentPublicationTakeReceipt.cs`: `cd4a90670295cf55b7ef128ee47fd6c38891e6ae2a5b1dbb5768d46d73ec3143`.
- `BookOfEternityClient/Core/FileSystemManager.cs`: `14dcb900d73951bf5228d474255c197324ddb32df74a075ce22f4dec64600ec9`.
- `BookOfEternityClient/Core/FileSystemManager.GenerationSnapshot.cs`: `8e78385fd23440ec87fa0d6843fe578b5e31d129af1ec51792e8701ec644c384`.
- `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.OrdinaryOwner.cs`: `f0b582d3563aed9e1c6f9bc067abe04a7351c639da1ec99465f47c2603ba5a8d`.
- `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs`: `06a3377ad0f0574e6d6bb501c446719b03998fda628082a54a90526534f6cee7`.

Actionable bounded seam proposed; no authoring or execution performed. FINAL STOPPED.
