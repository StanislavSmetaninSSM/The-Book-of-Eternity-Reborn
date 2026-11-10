# FR063 late-guard live probe — read-only diagnosis

2026-10-13. ROOT-published checkpoint7c48fdbae764e3b0faec3a9d02f6c19d93501170; actual source/compile14d037d82198659c509066759dcbb02e32b2b5f7. Read only source and the frozen/external native readback; no repository/metadata/Git/project/build/test/runtime/diagnostic/provider/agent operation. Only this requested external report was written. Production and fixture correction are not released by this report.

## Observed, not inferred

Actual20261013-191200-939-16725-77daa990886440dca6a5716d57e268f8-categories:1executed0PASS1FAIL/5planned,11.4567491s wall, no timeout. The corrected test passed the exact npc_skills path-bearing coordinator-preflight InvalidDataException assertion AND its complete physical before/after byte-preservation assertion. Failure is then shared lifecycle3376 `Assert.True(typed.IsValid)`. Later Issues/State/ChangedCount/request/energy/history assertions and the existing guard outcome observation were UNREACHED. Native evidence does not contain the typed probe's returned Issues or State. There is one pre-original NPC witness and strict owned cleanup; no measured resource-hold release or reservation-state change follows.

[Immutable first-outcome manifest](../1553-storage-migration-cloud-20261008-recovery-61d3a7f/specs/1536-complete-wound-materialization/recovery/fr063-late-guard-preflight-first-outcome-20261013/manifest.json) retains the failed stack and root readback. Source244/binary11 POST precedes metadata; one strict emitted root was removed/absent. Reflection3 and warm1 are still UNRUN in this packet. Independent classification remains ROOT-owned.

## Exact selected probe, not a reflection ambiguity

Shared `ProbeLiveRegistryAsync` at lifecycle3399–3464 scans only static probe methods and binds from actual fs/lease/acceptedState/request/resolution/finalization/plan/originalBinding. `TryBindLiveRegistryProbeArguments`3466–3508 rejects instance/generic/ref/out or any required unbindable parameter, and requires a unique type match. The candidate-count assertion passed before the native IsValid failure; no invocation/binding exception occurred.

The single source-supported match is:

```csharp
AcceptedTurnAuthorityRegistry.ProbeMortalWoundTreatmentResourcePublicationHold(
    FileSystemManager fileSystem,
    FileSystemManager.CanonicalWriteLease writeLease,
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    MortalWoundTreatmentAttemptRequest request,
    MortalWoundTreatmentResourceFinalization finalization)
```

Registry315–328 delegates to3957–4015. `ProbeTakenMortalWoundTreatmentPublication` and `ProbeMortalWoundTreatmentCoordinatedPublicationClaims` need a real taken publication receipt; the context has none before take, so neither can bind. ReservationRegistry.ProbeConfirmed is an instance method and cannot enter this static reflection set. Transaction.ProbeAsync is likewise an instance/taken-receipt contour. The fixture Finalize step is a pure derivation from the actual resolution and passed before probe.

## Publication-handoff proof and confirmed resource claim differ

The selected probe first calls `CurrentTreatmentPublicationAcceptedStateAgrees` at Registry3970, before `_treatmentResources.ProbeConfirmed` at3978. The former (5190–5201) requires ALL of:

- a current accepted-state reference;
- some current validated publication handoff, open take receipt, or restart blocker;
- current and expected accepted-state objects both present in publication lineage.

It is a live publication-lineage proof which then checks a resource hold, not an unconditional independent reservation inspection. After lineage cancellation it returns transaction_token_mismatch and null state/fingerprints at3973–3975/5553–5579 before reading the resource registry. `ChangedCount` on this probe type is always0 (TakeReceipt89), so it also cannot independently establish preservation after IsValid=false.

The actual preflight's exact before-image mismatch at AcceptedMechanics1184–1205 calls `InvalidateValidated` at1198, then its enclosing prevalidation catch582–585 calls common and effect handoff invalidation. Registry3271–3277 clears common validation and validated treatment-publication metadata; Registry3627–3637 invalidates effect/wound-final/common-cache slots and clears the same publication metadata. `ClearValidatedTreatmentPublicationCore`5480–5496 removes handoff/fingerprint/publication reservation and, with no open take receipt/restart blocker, clears publication accepted-state lineage. This pre-take normal contour has no receipt or post-publication compensation blocker. Therefore expecting the OLD publication probe to remain valid after intentional handoff invalidation is inapplicable by source contract.

These exact invalidation methods do NOT call `_treatmentResources.InvalidateAll`, release/commit/confirm resources, rewrite a request, or set `_mortalWoundTreatmentAcceptedState=null`. The unrelated registry-wide `InvalidateAllCore`5648–5664 and fresh accepted-state binding path1865–1978 CAN clear/recover reservations; those must not be conflated with the preflight's exact path. RegistryState `_commonPlan.InvalidateAll()` is a plan-cache operation, not RegistryState.InvalidateAllCore. No fresh Export/Restore or generation rotation occurs between the preflight and the observed probe call.

Thus source proves cancellation of the publication oracle and gives no release mechanism on this path. Native evidence still does NOT independently measure the retained resource agreement. The earlier preflight fix corrected the entry but retained an inapplicable publication-handoff assertion. Do not call this established production hold loss, and do not weaken IsValid to false-as-PASS or remove confirmed-hold coverage.

## Existing claim inspection and precise remaining API limitation

The real nonmutating exact-resource implementation already exists as `MortalWoundTreatmentResourceReservationRegistry.ProbeConfirmed`733–825. It validates the private registry capability, complete request batch/seals/rehydrated resource authority, exact active operation agreement, ConfirmedHeld state, persisted request fingerprint, reservation/resource authority and finalization fingerprints; returns state/operation/attempt/request/resource/finalization/agreement. Its request validation951–1048 rejects released/finalized/missing/conflicting agreements. This is the correct underlying claim proof.

It is private-owner capability-gated. Registry's actual state owns `_treatmentResources`; only registry has `TreatmentResourceRegistryCapability` (29/46). There is no independent exposed registry-owned read entry which invokes it without demanding the cancelled publication lineage. Do not reflect the private capability or registry field, instantiate a parallel registry, fabricate a receipt, reuse old factory/lease authority, or create/recover a new claim to make the test pass.

Existing alternatives are not substitutes:

- AcceptedState.HasCurrentAdmissionAuthority / IsCurrentMortalWoundTreatmentAcceptedState proves exact current accepted-state+lease, not a confirmed claim; a former lease-bound authority also fails after real turnover.
- ConfirmPersistedTreatmentResources is a lifecycle mutation, not a read (Composer605–624).
- ExportCurrent/RestorePersistedTreatmentRequests can bind a fresh owner, clear/recover reservations; it would prove replacement instead of preservation (Registry1927–1978).
- Existing AssertConfirmedHeldBlocksCompetingTreatment exports/restores and invokes PrepareGuaranteedRequest, so it is not an independent nonmutating post-cancellation proof.
- Parsing `reservationDisposition:"held"` from durable bytes proves a sealed persisted declaration only, not current process-local reservation state.

**Minimum actual diagnostic if ROOT requires observed classification before an API release:** in this ONE guard Fact, directly call the SAME supported typed publication probe and emit an explicit anonymous projection BEFORE the existing IsValid assertion: declaring method/signature, IsValid, Issues(code/path/expected/actual), State, ChangedCount, operation/attempt/request/resource/finalization/agreement/G/revision fingerprints. Preserve the assertion and all later oracles. Internal result properties must be projected explicitly; serializing the internal DTO alone can omit them. This only captures the current failure and changes no authority. No diagnostic was run by me. It cannot establish a confirmed hold because that existing probe exits before its underlying read.

**Minimum substantive source seam, only if ROOT separately authorizes:** add a thin non-authorizing registry-owned read of the EXISTING ProbeConfirmed under the actual current lease/physical generation, returning the existing exact resource probe plus generation provenance. It must inspect the EXISTING root slot/state (no new generation/slot, no Export/Restore/Confirm/Reserve/Take/rearm), lock and delegate with the registry's own capability, and refuse absent/stale generation/state. Existing read-only root-slot inspection pattern is Registry1623–1681; ordinary GetState1600–1614 can create a slot/generation, so do not accidentally call that a universally nonmutating lookup. This is an API necessity proposal, not an available API or authorized patch. Keep publication lineage checks and all old shared probes untouched. A new read entry name must also avoid joining the old generic static `probe` reflection candidate set, or existing helpers would become ambiguous. The one guard Fact would then require exact actual ConfirmedHeld state/unique request+operation+finalization+agreement/G proof after invalidation, together with all unchanged byte/request/energy/history oracles. No acceptance/planning authority is granted by the result.

ROOT may instead retain this case blocked pending that supported seam. Another blind fixture rerun or generic exception/status relaxation is unjustified. Qualified six scoped negatives, native and postseal remain protected; UNRUN4 can progress separately. Full ordinary13/original15/pending2/new-root/fullFR063/T070/#1536 remains OPEN.

## Read source pins

- `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs`: `06a3377ad0f0574e6d6bb501c446719b03998fda628082a54a90526534f6cee7`, 311741 bytes.
- `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.OrdinaryOwner.cs`: `f0b582d3563aed9e1c6f9bc067abe04a7351c639da1ec99465f47c2603ba5a8d`, 23178 bytes.
- `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs`: `3bacf2c2294c88b10acd0d137c153ba2e219acc93903e74cad155081474d4705`, 256008 bytes.
- `BookOfEternityClient/Services/MortalWoundTreatmentResourceReservationRegistry.cs`: `9bf40c73b2cdc08e2f913d841ff95bb45313f6a30d938546bad38c2a47d3ecac`, 48752 bytes.
- `BookOfEternityClient/Services/MortalWoundTreatmentPublicationTakeReceipt.cs`: `cd4a90670295cf55b7ef128ee47fd6c38891e6ae2a5b1dbb5768d46d73ec3143`, 15543 bytes.
- `BookOfEternityClient/Services/MortalWoundTreatmentAcceptedStateAuthority.cs`: `2c02b391f1cef80e115bc66f2d0c2ada92bc3d694403912bee47b9beb556f535`, 96631 bytes.
- `BookOfEternityClient/Services/MortalWoundTreatmentResourceComposer.cs`: `a4521aac38e0855dcd6c15171e5f23d72271c7df1a12a7704656898914056718`, 80921 bytes.
- `BookOfEternityClient/Services/MortalWoundTreatmentResourcePublicationTransaction.cs`: `5508ec06e2baecbcab25ae3e3fffc3f88a83d57ed3cfd18e70258c26990e2177`, 65490 bytes.
- `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs`: `7395c42f57a5e230cc5fbf3964d3bdd50afca8fedd8ed61decb028bf7854ce92`, 57490 bytes.
- `BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs`: `5befc8c24573d03c211e0bab7b857c90071519f3627f655e2da209b3a7efe675`, 57541 bytes.
- `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.NativeCold.cs`: `8e98458265939f41e0d46317859bfc3729835263bcbc54d45a9b28fb5868e716`, 33108 bytes.
- `BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.QtePhysicalInjuryFinalization.cs`: `ce955dcb177fd609c240cfe0705a45b5cb792cf4887ba6b6832494d72d543e6f`, 17914 bytes.

No source edit or execution made. FINAL STOPPED.
