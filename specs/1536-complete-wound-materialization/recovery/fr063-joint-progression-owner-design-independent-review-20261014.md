# FR063 joint progression owner — independent bounded DESIGN review

**DESIGN ACCEPTED. No confirmed Critical or Important finding against this proposed amendment.** This accepts the specified architecture only. It is not an implementation release, whole-SOURCE approval, or compilation/runtime/qualification claim.

Reviewed source carrier supplied by ROOT: `4185891591d93f4e79bdcadba6e15ef59a97074a`. Proposal SHA256: `cbd92b5d14b1bdf5b33e08065842c9689d0c9dcdaa03d4f1c79fe8e60be462ea`. Consultation SHA256: `e3588cd511fe62d8873d30dbf878d410a13ded20d76aabbcf534df6ff0b0b6fa`. Both file hashes and all eight proposal production pins independently match the inspected local bytes. No Git/ref operation was performed; remote/clean/recovery status is ROOT's supplied evidence. Configuration: same retained reviewer, requested Astra XHigh; this turn has no independent model/effort telemetry, and used no override or substitute agent.

## Concrete feasibility

The real dependency is present: `GameEngine.TurnLifecycle.cs:1545–1573` supplies genuine progression control, and `GameEngine.MortalJointRepairCompletion.cs:97–102` deliberately holds completion. The old mutation's filesystem/lease/service capability and consumed flag (`ProgressionScheduleService.cs:66–126`) cannot establish cold application. The homogeneous transaction's live agreement/flag at `MortalWoundTreatmentResourcePublicationTransaction.cs:490–631` cannot be transferred to the joint family.

The selected same-marker phase closes the relevant write/acknowledgement window without another mechanics reduction or reservation. `CoordinatedStateWriteHelper.cs:156–188,228–267` checks exact baselines and sends ordinary mutations together to `PublishLocalFilesAsync`, distinguishing committed, rolled-back and uncertain outcomes. Thus the fixed schedule/report/marker cohort has a concrete existing atomic publisher. The proposal correctly excludes the standalone mutation's separate restoration tail (`ProgressionScheduleService.cs:799–869`). A guard-only unchanged report remains subject to exact precondition and final readback; it need not be a physical mutation.

The existing joint proof independently checks original/G, complete footprint, typed treatment history/resource finalization and QTE partition (`MortalQteDecisionPublication.MortalJointRepair.cs:197–270`). Schedule/report are not current `JointFootprintPaths` members (`:17–21` and `MortalQteDecisionPublication.cs:24–34`), so the new closed phase must supply their exact proof while retaining that full mechanical proof. Extending the same private v2 schema, with no older-v2 default and unchanged v1, is bounded and feasible. No new public grammar, generic mixed-command grant, signing key or same-user anti-owner security claim is needed.

## Required implementation interpretation of the accepted design

These are concrete obligations of the proposal's specified phases and source proof, not additional release authority:

1. **Freeze the complete calculation, not merely its result.** `ProgressionScheduleService.cs:679–687,721–797,1364–1420,1601–1677` reads/sanitizes schedule, realm, report, request context and world time. World-time resolution has different fallbacks during sanitization and final calculation, including incremental and unresolved-absolute forms. Freeze actual bytes/presence and typed inputs, detach mutable schedule objects, preserve those branches, and reproduce the final timestamp, serializer/BOM and mutation-fingerprint semantics. Cold recomputation uses the captured UTC; it must not call today's Prepare. Old callers retain their existing behavior. Original schedule/realm/control authority and the validated submitted report/current world-time inputs must remain distinguishable; a legitimate current GM output is not automatically byte-identical to its original baseline.

2. **Validate phase before choosing report-validation inputs.** The current joint post-validator calls live `ValidateAcceptedTurnOutcomeAsync` at `GameEngine.MortalJointRepairCompletion.cs:84–85`. Once a nonzero matching report is consumed, that unchanged call reports `progression_report_missing_for_required_mortal_progression` (`ProgressionScheduleService.cs:939–955`). The proposed applied-phase reader must instead establish the original control/context and strict semantics from the frozen, independently checked report basis, recompute the pair, and require current after-images. Unapplied validates current before-images/report. Preserve ordinary canonical/narrative checks in both phases. Neither skipping progression validation entirely nor demanding the consumed live report implements the accepted design.

3. **Seal before take and reread after phase publication.** Exact fixed guards and the immutable basis belong in joint input/cache/preparation before the common take (`GameEngine.MortalJointRepairCanonicalPublication.cs:60–82`; `MortalJointRepairPublicationPreparation.cs:39–121`). Authenticate required coverage before minting future originals; missing signed coverage cannot be invented retrospectively. Hash the closed basis and phase in the marker, while keeping the immutable common-input binding stable across the phase transition. The old diagnostic witness binds the prior marker fingerprint (`MortalQteDecisionPublication.MortalJointRepair.cs:26–58`); do not reuse it as applied authority. Issue a fresh capability only after the current original/G/full-union proof, exact basis recomputation and phase-specific physical images all pass.

4. **Keep the owned boundary fixed through failure and completion.** Forward publication and any known compensation cover only schedule, report and this marker, with exact ownership/current-image checks. No generic pre-turn rollback, hold rearm, second common plan, or mechanical restoration may follow phase uncertainty or foreign bytes. Resolve existing journal debt before classification. Preserve original/ready/backups/submitted output until the successful ordinary lifecycle owns cleanup. The applied phase establishes schedule/report application, not story publication or the separate stats/level-up work (`GameEngine.TurnLifecycle.cs:2375–2383`). Actual normal/late-idle integration must still prevent a second turn/story and preserve completed mechanics across downstream refusal; the current callers at `:1017–1075,1794–1885` are not qualified merely by this amendment.

The proposed finite proof responsibilities at amendment line 41 are sufficient as a design inventory. The nonzero case must actually consume the matching report and then cold-continue from applied without recreating it; the zero-cycle case must prove exact bytes rather than infer idempotence. The post-phase/pre-story case must reach one story completion and an inert subsequent continuation. Mixed images, changed basis/original/G, compensation and uncertainty need physical whole-union preservation oracles. Fresh-root success must authenticate the complete durable original/G through real readers. Counts, selectors and fault seams remain for the connected SOURCE/fixture gate; none are invented or released here.

## Limits and next boundary

No new runtime evidence was created, and no hypothetical crash/rollback failure is reported as observed. The existing source still holds joint completion. The pure extraction, authenticated coverage, new schema/seal, phase APIs, caller ordering, lifecycle and controls are proposed/unimplemented or otherwise unqualified by this review. Accepted DESIGNv2 and its legacy exclusions remain in force. No optional framework or unrelated suite is required by this ruling.

ROOT/coordinator must separately authorize the bounded implementation. The sole writer's connected diff then requires SOURCE/FIXTURE/SELECTION review before any project execution. Whole FR063/T070, actual corrected live completion, cold/new-root and UI responsibilities remain OPEN. No repository, production, metadata, Git, build, discovery, test, provider or fault operation was performed; only this external report was written.

## Read-time pins

- `BookOfEternityClient/Services/ProgressionScheduleService.cs` — `cae67d35831c5cd67df85fc049657fb51ed400ba1749b984f0e4c6331afbeaf9`
- `BookOfEternityClient/Services/MortalWoundTreatmentResourcePublicationTransaction.cs` — `5508ec06e2baecbcab25ae3e3fffc3f88a83d57ed3cfd18e70258c26990e2177`
- `BookOfEternityClient/Services/CoordinatedStateWriteHelper.cs` — `5b79e2b7d42839ff6dadc0a67040e40ac4b2bf0a18fd589d2b66ffd22cff97b4`
- `BookOfEternityClient/Services/MortalJointRepairPublicationPreparation.cs` — `4b63871bb085508cfb27e68ab1cc46097c84d03f43926cec8e11b6ae676f28b7`
- `BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointRepair.cs` — `f88f79f92a42a78735b0addf0a1a036f1562d5c10feed5b1fd1ff8b22d772133`
- `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs` — `e5a5bd4432522a8699882dea1c6b91868646f4faa2770eeb3967a9a0e902d085`
- `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCompletion.cs` — `f7de271ee654d2facfd5e80e7cabc33bba94beb14de2183dc6ac18de3524c7f1`
- `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` — `85ad957b7fa801930a7d4f13d6abd24e96b2e877d00544c5e74aad4440be6b1e`
- `BookOfEternityClient/Services/MortalQteDecisionPublication.cs` — `3ac7e761c514262ddff97158e02fb8646d911cebedc9300e2477e4c7558f9895`
- `BookOfEternityClient/Services/CanonicalResourceOwnerAuthorityComposer.cs` — `0ebf495c393c9dc7108b0a1c03071336dafcf4f0c4d3cda5006184f84be38900`
- `BookOfEternityClient/Services/PendingTurnSnapshotReader.cs` — `11a6fe03846c74145329ad557917f8bf1bad693d1f40e3654efe0577404a5b6c`
- `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` — `79017c9ca9dc93247956088510b5f167e470b00746c7084be79df81116a911e1`

**FINAL STOPPED.** No pending tools.
