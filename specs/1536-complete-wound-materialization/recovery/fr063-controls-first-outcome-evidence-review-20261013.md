# FR063 first remainder: independent evidence and fixture classification

Reviewed checkpoint: `2f670f4fd33c726520808c1dc0bb7269163c8338`.
Runtime/source-pin carrier: `2d193effa00185c02a5b9b7aac5085fc19cd847f`.
Compiled code: `9532e386e8fa66a84a964f57a49900cc9a0e562e`.

**EVIDENCE / STATUS: ACCEPTED.** The record faithfully reports an incomplete selection with one passing component and nine fixture-prerequisite failures. **The controls cohort remains unqualified.** None of the nine exceptions proves the intended production refusal or a production regression. No further diagnostic execution is needed to establish these three fixture causes.

This review used read-only source, immutable objects and existing artifacts. No project execution, repository changes, new agents or provider calls were performed.

## Evidence verified independently

Packet: `specs/1536-complete-wound-materialization/recovery/fr063-guaranteed-ordinary-owner-remainder-first-failure-20261013/manifest.json`.

- All **24** compressed lengths/hashes and decompressed lengths/hashes match the manifest. Every decompressed artifact matches its actual scratch/native original, and every stored artifact matches the published checkpoint bytes.
- All **242 source pins** match immutable runtime carrier `2d193eff`. Comparison against compile source `9532e386` finds only `plan.md` and `tasks.md` differ; all other 240 pinned paths are identical. All **11 retained fresh binaries** still match their recorded lengths and SHA-256 values. Current source differences from the runtime pins are those same two metadata files.
- Exact PlanOnly run `20261013-173827-825-681715-74813b734c9544328902818bcbf7f755-categories`: **8 methods / 14 cases / 3 descriptors**, 2.6659213 seconds, exit 0, zero tests executed.
- Actual run `20261013-174038-122-681934-58c63380cbee4880a3e4357ff0c2bab4-categories`: 88.5548184 seconds, exit 1, no timeout, skipped result or duplicate test ID. Full TRX contains **10 executed / 1 PASS / 9 FAIL** from the six-method component descriptor. The selection completed **1/3 descriptors and 10/14 cases**. The reflection three cases and warm-fence one case were not executed.
- All ten raw result attributes, messages and stacks match the frozen root readback. The saved full stdout is exactly the ten TRX stdout blocks joined in their retained order. Its **11 boundary observations** match the structured companion: ten pre-original NPC-shape witnesses and the passing post-seal observation.
- All **10 distinct strict cleanup receipts** come from native stdout, report removed=true/failure=null, and their exact physical roots are currently absent. Runner owned-process/runtime cleanup also reports success.
- The identical current 38-line insertion in active plan/tasks can be removed to recover each prior file exactly. Checkboxes and prior history remain unchanged. The protected QTE causal Fact retains SHA-256 `ce955dcb177fd609c240cfe0705a45b5cb792cf4887ba6b6832494d72d543e6f`. Other new carrier paths preserve read-only reports/design supplements; they are not production or fixture changes.

The native summary's category `Completed=false` denotes the failed category outcome. Its selection independently reports the component descriptor completed all ten cases. Neither that distinction nor planned root-readback counts should be represented as fourteen executed cases.

## Confirmed fixture blockers

All locations below refer to the reviewed immutable code. `OrdinaryOwner.cs` abbreviates `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.OrdinaryOwner.cs`.

### Important: capture and equality reads do not participate in the held lease

**Locations:** OrdinaryOwner.cs:91,120,237,329–338; `GameEngineTurnLifecycleTests.QtePhysicalInjuryConsumerArchive.cs:614–618`; `Core/FileSystemManager.cs:1401–1427,1615–1625`; `Core/FileSystemManager.MainRunFence.cs:56–89`.

The nested-request two rows, current-capability row and concurrent resource/effect/party three rows all fail in the pre-call capture. The caller still owns `held.Lease`, but the shared archive capture receives only FileSystemManager and calls its unbound read. When that read needs publication quiescence, it enters a competing main admission and attempts the already-held `gm-main-owner.lock`. Every one of the six native stacks reaches this exact acquisition chain.

These failures occur before `ValidateResourceAndMortalGuaranteedTreatmentAsync` in those rows. Their negative or preservation assertions did not pass. The equality helper at line338 has the same unbound read problem after lease reacquisition even though this run did not reach it.

**Minimum correction:** keep the fix local to the new ordinary-owner fixture. Capture and assertion helpers should accept the held context and use its existing `ReadFileBytesAsync`, whose implementation at `MortalWoundTreatmentResourcePublicationLifecycleTests.cs:5130–5133` explicitly uses the active lease when present and the ordinary read when released. Preserve the original lease turnover and all existing assertions. No lock bypass, early release workaround, raw filesystem fallback or production change is needed.

### Important: pending-duplicate fixture assumes an optional file was produced

**Locations:** OrdinaryOwner.cs:22–35; `MortalWoundTreatmentResourcePublicationLifecycleTests.cs:3117–3177`; `IO/StateDistributor.cs:118–127,163–181,303–360`; `Services/MortalWoundTreatmentPersistence.cs:104–226,238–404,2701–2786`.

Both pending-duplicate rows fail at line24 because the pending file bytes are null. The genuine guaranteed setup prepares and resolves the request, strictly parses/recomposes its full command, distributes that command and confirms its treatment resource hold. This distributor path does not manufacture a repair/pending file. The persisted catalog explicitly accepts a nullable pending root and collects the genuine request from the command.

**Minimum lawful correction boundary:** first assert the expected initial absence. A complete copied request alone is insufficient: `ParsePending` at Persistence.cs:287–301 requires a valid repair wave whenever `submittedTreatmentRequests` is present. `WoundRepairPacketBuilder.PersistenceValidation.cs:50–65` requires at least one packet with a matching receipt. `ComposePendingRoot` at Persistence.cs:2770–2786 deliberately copies treatment requests only for a nonempty wave. Existing tests `PersistedPending_RejectsSubmittedTreatmentWithoutARepairWave` (ResolverTests.Persistence.cs:1420 onward) and `RepairPending_DoesNotCopySubmittedTreatmentWithoutANonEmptyRepairWave` (:1699 onward) confirm this contract.

Therefore, do **not** add submitted requests to an empty root or treat absent-file initialization as the complete fix. The fixture must obtain a supported nonempty packet/receipt wave under the actual binding through existing construction APIs, use the actual strictly parsed treatment command to compose its full pending root, and prove that the unchanged command-plus-pending-plus-history catalog is valid and coalesces to one original request before making the deliberate changed-graph mutation. The packet's hidden-source authority cannot be invented from public hashes. The exact lawful wave-construction seam remains a SOURCE-review dependency, not approved implementation in this report.

Keep original request/operation/attempt identity, confirmed resource hold, snapshot and request fingerprint unchanged. No fabricated packet, new authority, original rewrite, parser relaxation or resealed request is warranted. The positive exact copy and the negative quantity mutation must remain distinct; all original downstream oracles remain mandatory. This is component-input preparation, not evidence that production automatically wrote a pending file.

Review correction: an initial suggested empty-wave copy was withdrawn after tracing the nonempty-wave predicate above. It is not part of this final ruling.

### Important: combined capture inventory repeats canonical paths

**Locations:** OrdinaryOwner.cs:265–284,329–334; `Services/AcceptedMechanicsWoundCommonInputComposer.cs:21–40`; archive capture line618.

The late-guard case did reach successful ordinary admission and obtained a plan with an absent NPC-skills before-image. After actual lease release and absent-to-present mutation, its capture fails because `GuaranteedNativeMechanicalPaths` and `TreatmentOrdinaryGuardPaths` both contain `game_state/effects/effect_identity_index.json`. Dictionary.Add throws before raw revalidation or the normalizer take/refusal.

**Minimum correction:** deduplicate the complete combined logical path inventory using ordinal equality before capturing it. Preserve its entire union and exact null-versus-byte representation; do not remove a guard root to avoid collision. This local helper correction also prevents the next otherwise inevitable failure after resolving the six lease-read failures. The late-guard's real mutation, later expected mismatch issue, failing take and unchanged-state/hold assertions must remain intact.

## Retained positive evidence and retry scope

The passing `GuaranteedTreatment_ComponentOrdinaryOwnerPostSealCompensationRetainsFullHeldRequestAndExactRetry` is valid scoped evidence. Its native observation records the actual `wound_post_seal` injected fault. Its passed assertions cover RetryablePublicationRearmed, exact transaction restoration, the same admitted plan and original fence, confirmed held resources/full request, and a successful ordinary retry with one current energy spend, one treatment transition, stabilized care, finalized request and consumed command.

That Fact uses `CaptureExactTreatmentTransactionBytesAsync(held)` and its matching assertion helper, not the defective ordinary capture/equality helpers. The two pending-duplicate rows alone need pending-root preparation. Therefore a correction confined to those rows and the new local capture/equality helpers does **not** create a reason to repeat the passing post-seal Fact or the accepted same-root native Fact.

If the pending-wave fixture is made lawful and independently SOURCE-accepted, the narrow next actual inventory can be **7 methods / 13 cases / 3 descriptors**: five affected component methods / nine failed cases, plus the unchanged reflection method / three previously unrun cases and warm-fence Fact / one previously unrun case. Preserve category ownership and use a reviewed exact category/selection; do not bypass the runner with retired ad hoc row filtering. The original complete component owner may retain all six methods/ten cases.

If the two pending rows remain unresolved, do not fabricate a wave to reach thirteen. Isolate them as blocked and proceed, after source/selection review, with **6 methods / 11 cases / 3 descriptors**: four capture-affected methods / seven cases plus the same four previously unrun cases. That narrower outcome must explicitly leave pending coalescing/divergence unqualified.

This is a conditional scope ruling, not SOURCE acceptance of an unwritten correction or a runtime release. Review the actual fixture/selection diff, rebuild the changed integration assembly with the required selected-project preparation, and verify exact discovery before execution. If the eventual diff changes the passing Fact's setup, authority helpers, transaction capture, common publication or finalization assertions, reassess that concrete dependency; do not assume either thirteen or fourteen unconditionally.

**Disposition:** evidence/status accepted; bounded test-only fixture correction may proceed to its mandatory SOURCE/FIXTURE/SELECTION gate. Nine failed controls and four unrun cases remain unqualified. The accepted same-root native and the passing post-seal component retain their separate proof. New-root restoration, UI presentation, GM implementation and full FR063/T070/#1536 completion remain open.

FINAL STOPPED.
