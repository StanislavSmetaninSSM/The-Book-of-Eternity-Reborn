# FR063 live pending owner — bounded proposed-design disposition

2026-10-13. **BLOCKED by one owner-integration dependency. No lawful normal entry plus successful correction/handoff can be demonstrated under the unchanged current owner contracts.** Do not implement a capture-only pending writer as a purported complete solution.

This is the requested stopping result, not a production bug reproduced at runtime or a new gameplay-policy question. Sole Sol's test-only genuine pending2 scope remains unchanged. The 13 accepted cases retain their bounded status; original 15 GREEN and full FR063 are not claimed.

Prior [live-writer disposition](fr063-live-pending-writer-readonly-disposition.md), SHA256 `b8f6acfd140dc4f466555d50ffd3a89a8982e5e6c2205e606ffeb94bca4193a4`, already records the caller inventory. This follow-up checks only the missing entrance and correction lifetime. ROOT reports metadata e8bbbf39 / accepted production reader 6f1eb1bf; no Git/remote operation was repeated. The nine relevant source hashes still match that report.

## The single dependency

**The current owner of the single `wound_commands` surface has no admitted repair/handoff protocol that can keep an already-issued homogeneous held treatment request while issuing and completing a different acquisition decision under the same original turn.** Neither a private pending codec nor a checkpoint writer supplies that ownership. A separately scoped extension of this existing owner integration is required before the live FR063 pending path is implementable end to end.

This is narrower than redesigning wound treatment: exact request seals, typed authorities, claim recovery, reducers, common publication and compensation already exist. The unresolved target is the two-family lifetime at the existing single-command boundary, including which current owner may issue the corrected acquisition command and subsequently finish the **same** held request. No such protocol is present in the inspected source.

## Concrete state trace

| Required transition | Existing callsite and actual constraint |
| --- | --- |
| Held treatment + real source occurrence → normal acquisition repair capture | `ValidationService.ResourceMaterialization.cs:503–537` selects the treatment owner first and rejects unconsumed occurrences at 533–536. With any treatment commands but no admitted owner it also refuses at 576–581. `stageMortalQteDecision(writeLease)` is later at 584–587. Thus the normal call does not issue the proposed genuine acquisition repair packet in this state. |
| Read-only acquisition rejection → legal correction | Independently calling `MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse` can produce genuine source-bound diagnostics, as in the component. That does not change normal admission or install an owner. `StageMortalQteDecisionAsync(lease)` (`GameEngine.MortalQteWoundDecision.cs:87–119`) must stage an actual accepted acquisition command after correction. |
| Corrected acquisition → command staging with treatment retained | `StateDistributor.StageAcceptedWoundCommandAsync(files, lease, response, acceptedWoundInput)` (`IO/StateDistributor.cs:67–81`) requires the existing command path to be absent. Lines 74–75 preserve/refuse the held treatment command as a competing before-image. `RevokeOwnedMortalQteCommandAsync` (`GameEngine.MortalQteWoundDecision.cs:301–317`) deletes only its own exact prior QTE bytes; it cannot lawfully clear the treatment command. |
| Corrected response → existing repair-loop success | `HasExactMortalQteRepairResubmissionAsync(obligation)` (`GameEngine.MortalQteWoundDecision.cs:219–235`) requires `_mortalQteOwnedCommand`, `_mortalQteIssuedOpportunity`, and exact physical QTE command bytes. Pure revalidation or a captured packet cannot satisfy it. `GameEngine.ValidationAndRepair.cs:588–605` retains retry errors when these obligations are not met. |
| Put treatment only in pending, then use command slot for acquisition | This would produce command treatment-set size 0 versus pending size 1. The strict persisted catalog checks exact cross-root sets (`MortalWoundTreatmentPersistence.cs:193–200,483–504`) and refuses it on parsing/recovery. This is a source-proven catalog conflict, not a claim that an unexecuted QTE branch emitted that diagnostic. It is not a supported escape from single-slot ownership. |
| Put both families into one command root | Rejected by existing persisted-family guard (`MortalWoundTreatmentPersistence.cs:147–153`), distributor guard (`IO/StateDistributor.cs:321–334`) and ordinary homogeneous treatment owner (`GameEngine.MortalGuaranteedTreatment.cs:27–29`). No relaxation is proposed. |
| Finish acquisition, then move the unchanged held request to a new ordinary turn | `GameEngine.MortalGuaranteedTreatment.cs:51–57` requires exact original SessionId/RequestId/SnapshotToken/turn/realm/G. A genuinely new turn cannot inherit that sealed request by changing only its outer container. Re-signing, rebuilding or reserving another request would violate the requested exact same-request lifetime. |

The later failures are independent of the first admission refusal. Adding a pre-guard read-only capture branch alone would therefore leave a correction dead end. Suppressing the duplicate-family/competing-command checks, treating a packet as publication authority, deleting the treatment copy, or silently abandoning its hold cannot close the dependency.

## Why no writer signatures are proposed yet

The candidate boundary from b8f6 remains the appropriate place to evaluate future persistence:

`CaptureWoundRepairRetryObligationsAsync(IReadOnlyList<WoundRepairPacket>)` → `WaitForContractRepairAsync(...)` → `RestorePreTurnBaselineForRepairSessionAsync(RollbackSnapshot, string expectedSessionGeneration, byte[]? pendingResolutionRepairCheckpoint = null)` → `WriteValidationRepairRequestForSessionAsync(...)`.

But these existing signatures do not define the missing owner handoff. The baseline method currently restores only an optional **resource** pending checkpoint after rollback (`GameEngine.SessionAndSnapshots.cs:2091–2115`); the public dispatch emits sanitized packets. A new private typed wound-command/pending checkpoint could preserve exact bytes under the current lease/G and coordinated rollback/readback, but it would still not authorize the corrected acquisition command or solve the exact cross-root set constraint. It would be misleading to prescribe its new signature or claim compensation completeness before the ownership transition above exists.

Accordingly, no production state machine, new checkpoint DTO, writer API, public endpoint or speculative correction workflow is selected here. Private path ownership, GM-safe projection and exact readback/compensation remain necessary conditions for a later complete owner design, not substitutes for it.

## Exact next engineering target and stopping boundary

Before releasing any live pending writer, prepare one concrete owner-integration proposal at the existing ResourceMaterialization / GameEngine repair / StateDistributor single-command boundary. It must demonstrate a lawful normal entrance, then a successful corrected-wave handoff and completion of the identical sealed treatment request, with command/pending/history and claim identity intact. It must leave the current ordinary homogeneous owner and raw resource/effect/QTE/mixed-family exclusions unchanged for their existing callers. If a new narrowly admitted ownership branch is required, specify its actual typed inputs and publication/lifecycle authority explicitly; that is additional production scope, not a serializer hookup.

This report does not choose or authorize that branch. Under the task's requirement to stop when no existing lawful route is demonstrable, the investigation ends at this one dependency. There is no new user concept question: FR-012/FR-063 and treatment contract 1004–1018 already require the real-wave complete-request behavior. The component can qualify its representation/recovery responsibility, while live owner integration and full FR063 remain OPEN.

## Read-time source pins

All below match the prior b8f6 source pins. Only file reads and this external report write were performed; no source/metadata/Git/project/runtime/provider/agent mutation or execution.

| Repository-relative file | SHA256 |
| --- | --- |
| `BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs` | `560f146d8210f8347f53f53196054c42ba9406f92cd6ef073b30aa17fb2c7b83` |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalQteWoundDecision.cs` | `7a581df7c052ecee9c4f6943089941a9a5ac957810065890f116b356a743992b` |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalGuaranteedTreatment.cs` | `fdacf8c145a10b9be5f09f1fde28415969d740e6526c84ab4097d4e9093b4642` |
| `BookOfEternityClient/IO/StateDistributor.cs` | `0ceecfb1f5d517673504e413f138bad5593065791f1f85a222238c7f19a3893d` |
| `BookOfEternityClient/Services/MortalWoundTreatmentPersistence.cs` | `ca7b36dd5904e02d42de25b365d0fea597e9c7eee3651960d0e32184682070ef` |
| `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs` | `c967865961aa37163c6460617f6d268c3ff6ed824ae759c9900cd1f3095dde81` |
| `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` | `244d8c232fc29a44e5bf3d86c350282f72ceb849ccbe89699881686e26487ea6` |
| `specs/1536-complete-wound-materialization/contracts/mortal-wound-treatment.md` | `96e03eea7c813eff8af47b6b5c57373bd6c426dda901ec3c4ee9dd38d80a1e8d` |
| `specs/1536-complete-wound-materialization/spec.md` | `394c1c0996f21ac9e4bc98828e240e8bbffd47fc037799b0e6207ce6851c0b69` |

**FINAL STOPPED.**
