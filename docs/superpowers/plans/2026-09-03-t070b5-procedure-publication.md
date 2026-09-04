# T070-B.5 Procedure Publication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish the first complete Mortal procedure-treatment contour through the existing six-argument accepted-plan API, including exact wound/history state, natural-one Fate Shield expiration, resource/item settlement, and dice/Fate reservation lifecycle.

**Architecture:** Replace the guaranteed-only wound successor with a typed outcome-publication planner selected from the sealed T067 resolution. B.5 admits two complete scalar singleton procedure outcomes—`stabilize` and `no_improvement`—while preserving the guaranteed stabilization path through the same planner and failing closed for later effect-bearing, severity-changing, course, recovery, and heal operations. A sealed critical-reaction projector derives the accepted effect lifecycle event only from `MortalWoundCriticalReactionIntent`. The existing accepted-turn resource transaction becomes a coordinated treatment transaction for every procedure, even when no consumable resource is required: it validates exact live dice/Fate claims before publication, retains them as spent through successful publication, restores and rearms them on retryable compensation, and releases them only after terminal durable-command quarantine.

**Tech Stack:** C#/.NET 8, `System.Text.Json.Nodes`, file-backed canonical JSON, xUnit, PowerShell 7 bounded test lanes.

## Global Constraints

- Tracked authority is GitHub issue #1536 and open Spec Kit task T070.
- Work only in `E:\Games\worktrees\boe-1536-wound-materialization` on `1536-complete-wound-materialization`.
- Preserve the public six-argument `ComposeMortalWoundTreatmentPublication`, the sealed request/resolution DTOs, and the single `MortalWoundTreatmentResourceComposer.Finalize(resolution)` entry point.
- Treat `acceptedState`, `request`, `resolution`, the existing skill/item envelope, and internally recomposed resource finalization as the only publication inputs. Never reparse route JSON, raw dice, caller after-images, or GM-authored effect reports.
- B.5 supports guaranteed singleton `stabilize`, procedure singleton `stabilize`, and procedure singleton `no_improvement`. It does not claim `reduce_severity`, recovery, complication effect batches, courses, heal/legacy, or T069-C.
- Natural-one Fate mitigation selects the failed-attempt band; it is therefore proved with singleton `no_improvement`, not stabilization.
- Fate expiration enters #1535 only through `EffectAcceptedTurnInputComposer.Compose(... acceptedReportedLifecycleEvents:)`, derived from the typed sealed reaction intent. `GameResponse.EffectEventReports` remains forbidden.
- A procedure with `Finalization.Disposition == "not_required"` still requires the coordinated publication transaction because dice and optional Fate are resources of the attempt. Its zero-claim resource agreement is still persisted, confirmed, probed, and finalized; `not_required` does not bypass resource lifecycle authority.
- Successful publication must not make a used die/Fate claim reusable in the same accepted turn. Keep the exact reservations occupied as spent until accepted-state rebind reconstructs them from durable history.
- Retryable post-write failure restores every governed byte and rearms the exact same plan while retaining dice/Fate/resource claims. Terminal rejection removes the exact durable command and pending request first, then releases all relevant claim families.
- Composition and planning remain write-free. The sole canonical publisher remains `CanonicalStateNormalizer` under the top-level accepted-turn coordinator.
- Do not widen `GameResponse`, add migration/compatibility code, add a raw writer, or introduce a second canonical state authority.
- Preserve and do not stage the untracked `.serena/` directory.
- Use only `pwsh .\scripts\test-csharp.ps1`; never invoke raw or unbounded `dotnet test`.
- During implementation run the smallest owning `Focused` filter, then one meaningful `Fast` checkpoint. Reserve `PreMerge` for an explicitly requested integration/merge.
- This slice changes client-owned Mortal accepted-publication mechanics already covered by the treatment contract. It adds no GM-authored field, player command, browser/console surface, or afterlife contract; record the no-documentation-update rationale at closure.

---

### Task 1: Freeze the B.5 RED oracle

**Files:**

- Create: `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs`
- Modify: `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.cs`
- Modify: `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ResourcePublication.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs`

**Interfaces:**

- Consume only production-created accepted state, procedure request, resolver result, persistence/rehydration, common plan, normalizer, and transaction coordinator.
- Do not mint authority, hand-write history/after-images, or publish by calling a low-level normalizer shortcut.

- [x] **Step 1: Add a legal procedure publication fixture**

Persist the production-created request, rebuild the accepted-state/catalog authority from durable bytes, rehydrate the exact request, reproduce the exact resolution through the ordinary procedure resolver, compose through the six-argument API, and publish through `AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync`. If a transaction is returned, advance published agreement and complete it exactly as the runtime does.

The helper must support both held-resource and `not_required` attempts and a true copied-root cold restart with fresh process-local registries. Remove or stop using any test helper that directly calls `NormalizeAcceptedMechanicsAsync` for coordinated treatment publication.

- [x] **Step 2: Add exact scalar outcome RED cases**

Add these focused tests:

```text
ProcedureSingletonStabilization_PublishesWoundAnchorsRouteAndHistoryOnce
ProcedureNoImprovement_PublishesAttemptHistoryWithoutInventingImprovement
ProcedureNoResourceAttempt_StillRequiresCoordinatedTransaction
ProcedureSuccessfulPublication_KeepsUsedDiceUnavailableUntilAcceptedStateRebind
ProcedureColdReplay_DoesNotRepublishTransitionHistoryOrSpend
```

For stabilization assert the accepted wound is stabilized at the canonical minute, the recovery anchor is rebased to the exact transition, the condition blocker/anchor is cleared as specified, route completion follows only the sealed `RouteCompletion`, and exactly one ordinary `treat` history row exists. For no-improvement assert wound severity/components/care remain unchanged except accepted-attempt metadata and exact history; do not invent a stabilization or recovery receipt.

- [x] **Step 3: Retain the existing resource/Fate REDs**

Drive these existing specifications through the legal fixture:

```text
ProcedureFailedAttempt_ConsumesExactlyItsDeclaredQuantityOnce
ProcedureFinalization_ConsumesOnlySelectedSupplyAndReleasesEveryOtherHeldClaim
ProcedureFateReaction_ConsumesOldestAtomicallyThenExposesNextShield
ProcedureFateReaction_TypedAndLegacyReportDuplicateRejectsBeforePublication
```

Only the failed/no-improvement row of `ProcedureFinalization_...` is a B.5 GREEN gate. Its successful `reduce_severity` row remains an explicit next-contour RED.

- [x] **Step 4: Add compound-transaction failure RED cases**

Add exact coverage for:

```text
ProcedurePostWriteFailure_RestoresEveryRootAndRearmsSameClaimsAndPlan
ProcedureTerminalQuarantine_RemovesDurableAuthorityBeforeReleasingEveryClaim
ProcedureChangedDiceOrFateReservation_RejectsBeforeAnyCanonicalWrite
ProcedureFatePublicationFailure_DoesNotExpireShieldOrConsumeItem
ProcedureExactRetry_DoesNotDuplicateDiceFateResourceOrHistory
```

Capture byte-for-byte before-images for wound carrier/index/history, effect carrier/index/source roots, item/resource roots, output, treatment command, and pending request. Verify atomicity across all three claim families.

- [x] **Step 5: Run the smallest RED selection**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&FullyQualifiedName~Procedure"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentResourcePublicationLifecycleTests&FullyQualifiedName~Procedure"
```

Expected: the new and retained cases fail only at the currently explicit `mortal_wound_treatment_publication_slice_unsupported`, generic duplicate-response rejection, or absent coordinated-procedure transaction gates. Build must remain warning-free with complete cleanup.

- [x] **Step 6: Commit the RED oracle**

```powershell
git add -- BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.cs BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ResourcePublication.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs
git commit -m "test(wounds): specify procedure publication transaction (#1536)"
```

### Task 2: Generalize typed scalar treatment outcome publication

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentCapabilityAuthority.cs`
- Modify: `BookOfEternityClient/Services/WoundTransitionReducer.cs`
- Modify: `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.cs`
- Test: `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs`

**Interfaces:**

- Input: exact route-source wound, sealed resolution, current canonical minute, deterministic transition ID.
- Output: detached canonical after-wound plus closed transition metadata/issues/fingerprint; no filesystem reads or writes.

- [x] **Step 1: Introduce a typed outcome planner**

Create an internal planner that independently verifies the resolution seal, `DeclaredResult`, ordered `OutcomeIntents`, selected category/index, mode evidence, and route-completion agreement. Dispatch by typed outcome kind through an explicit closed handler registry. Implement handlers for singleton `stabilize` and singleton `no_improvement`; preserve an explicit unsupported result for every later kind rather than silently ignoring it.

The planner must be mode-agnostic: guaranteed stabilization and procedure stabilization use the same handler. It derives after-state only from the exact before-wound and sealed intent, normalizes it through `WoundMaterializationContract`, and returns a recomputable fingerprint.

- [x] **Step 2: Model accepted attempt metadata exactly**

Apply `RouteCompletion` only when the resolution says `AppendOnce`. Write the exact last-attempt/result/category/transition evidence needed by the existing reducer and history contract. `no_improvement` preserves care, severity, complications, recovery/condition anchors, and active effect roots. `stabilize` sets stabilized care at the canonical minute, removes `not_stabilized`, clears the satisfied condition/deterioration anchor, and rebases the independent recovery anchor to this exact transition/minute.

- [x] **Step 3: Route the common continuation through the planner**

Replace `ValidateGuaranteedStabilizationShell` and `CreateGuaranteedStabilizationAfter` with general publication admission plus the typed planner result. Rename guaranteed-specific diagnostics/fingerprint domains to treatment-publication domains only where internal and safe; preserve stable external codes unless a test explicitly freezes the corrected procedure-specific diagnostic. Keep the existing prepared/effect/final/common bundle and sole normalizer path.

- [x] **Step 4: Prove deterministic write-free behavior**

Call the planner twice with detached equivalent inputs and assert equal after-images, transition metadata, issues, and fingerprint. Assert canonical files are byte-unchanged during planning and that copied/tampered intent, result, route completion, minute, or transition ID fails before plan registration.

- [x] **Step 5: Run scalar outcome controls and commit**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ProcedureSingletonStabilization_|FullyQualifiedName~ProcedureNoImprovement_|FullyQualifiedName~GuaranteedStabilization"
git add -- BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs BookOfEternityClient/Services/MortalWoundTreatmentCapabilityAuthority.cs BookOfEternityClient/Services/WoundTransitionReducer.cs BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.cs BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs
git commit -m "feat(wounds): publish typed scalar treatment outcomes (#1536)"
```

### Task 3: Publish sealed Fate reactions through the effect plan

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundCriticalReactionPublicationPlanner.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentSkillPublication.cs`
- Test: `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs`
- Test: `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.cs`

**Interfaces:**

- Input: exact accepted state, request, resolution, and optional `MortalWoundCriticalReactionIntent`.
- Output: zero or one detached accepted lifecycle event plus a sealed fingerprint; never a GM response payload.

- [x] **Step 1: Recompose the reaction intent independently**

Verify procedure mode evidence, selected natural roll, roll actor, oldest-shield reservation/agreement, accepted effect fingerprint, event/causal/trigger/effect IDs, turn, realm, player target, request fingerprint, and intent fingerprint. A null intent must agree with the absence of a selected reaction.

- [x] **Step 2: Project the typed lifecycle event**

Build the exact event shape consumed by the existing #1535 effect reducer and pass it through `acceptedReportedLifecycleEvents`. Do not call the legacy GM report parser and do not accept an `EffectEventReports` field from `GameResponse`.

- [x] **Step 3: Add the exact cross-surface duplicate preflight**

Before the generic closed-envelope rejection, detect typed reaction plus non-null legacy `EffectEventReports` and return `wound_treatment_fate_reaction_cross_surface_duplicate`. The rejection must leave the complete canonical tree unchanged and safely roll back only claims newly created by the unpersisted attempt; persisted/confirmed claims follow terminal quarantine instead.

- [x] **Step 4: Prove order and replay**

Publish two natural-one attempts over two turns and prove the oldest applicable shield expires first, the next shield becomes visible only after accepted-state refresh, the item/resource settlement is atomic with effect expiration, and cold exact replay performs no second expiration or spend.

- [x] **Step 5: Run Fate controls and commit**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ProcedureFateReaction_"
git add -- BookOfEternityClient/Services/MortalWoundCriticalReactionPublicationPlanner.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentSkillPublication.cs BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.cs
git commit -m "feat(wounds): publish sealed treatment Fate reactions (#1536)"
```

### Task 4: Coordinate dice, Fate, item, and resource settlement

**Files:**

- Modify: `BookOfEternityClient/Services/MortalWoundProcedureCheckAuthority.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentResourcePublication.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentPublicationTakeReceipt.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentResourcePublicationTransaction.cs`
- Modify: `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs`
- Modify: `BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.MortalItemMaterialization.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs`
- Test: `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs`
- Test: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs`

**Interfaces:**

- Extend the private publication authority with `RequiresProcedureSettlement` and `RequiresCoordinatedSettlement`; do not expose a new public DTO.
- Bind the exact request-owned procedure authority and its current live-agreement proof into the one-use take receipt and transaction seal.

- [x] **Step 1: Generalize transaction admission**

Replace every `RequiresConfirmedHold`-only coordinator gate with `RequiresCoordinatedSettlement`. Every coordinated transaction probes and commits the exact persisted resource agreement, including a procedure's zero-claim `not_required` agreement; procedure liveness is additionally mandatory when `RequiresProcedureSettlement`. A guaranteed no-resource publication may stay outside the transaction; a procedure no-resource publication may not.

- [x] **Step 2: Seal and probe procedure claims**

Add private registry operations that atomically validate the exact dice reservation and nullable Fate reservation/agreement from the request's `MortalWoundProcedureCheckAuthority`. The receipt generation fingerprint must bind their immutable claim fingerprints and mode authority fingerprint. Any stale, foreign, released, changed, or overlapping claim rejects before the first canonical write.

- [x] **Step 3: Finalize success without reopening spent claims**

After the complete accepted-turn pipeline, published readback/agreement, validation, cleanup, and final refresh succeed, commit the exact resource finalization (including `not_required`) and close the receipt while leaving exact procedure reservations occupied. The next semantically changed accepted-state bind invalidates process-local state and reconstructs both held and finalized procedure claims from durable request/history evidence. Extend finalized natural-one recovery so it can restore the spent Fate agreement from the persisted reaction evidence even though the now-inactive shield is no longer an eligible current candidate. Prove the same-turn pool cannot reuse the spent indices and cold recovery cannot require an already-expired shield to remain active.

- [x] **Step 4: Preserve retry compensation**

On retryable failure restore all before-images byte-exactly, keep durable request/confirmed hold, keep procedure reservations live, and rearm only the receipt-owned plan. A changed procedure/resource claim or changed published agreement cannot be rearmed.

- [x] **Step 5: Make terminal release atomic in ordering**

For terminal rejection/quarantine, take the same coordinated receipt, remove the exact command and pending rows durably, verify their absence, then under the registry gate release the confirmed resource hold if present and the exact dice/Fate claims. If durable removal or any claim agreement fails, do not partially release; retain/restart-block the authoritative state and emit the existing transaction failure family with a procedure-specific issue.

- [x] **Step 6: Fence direct normalizer entry**

Every direct accepted-mechanics normalizer path must reject a coordinated treatment plan without its current one-use receipt. Validation retention logic must preserve a valid no-resource procedure plan and its claims instead of discarding it because no resource hold exists.

- [x] **Step 7: Run transaction controls and commit**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentResourcePublicationLifecycleTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ProcedurePostWriteFailure_|FullyQualifiedName~ProcedureTerminalQuarantine_|FullyQualifiedName~ProcedureChangedDiceOrFateReservation_|FullyQualifiedName~ProcedureExactRetry_"
git add -- BookOfEternityClient/Services/MortalWoundProcedureCheckAuthority.cs BookOfEternityClient/Services/MortalWoundTreatmentResourcePublication.cs BookOfEternityClient/Services/MortalWoundTreatmentPublicationTakeReceipt.cs BookOfEternityClient/Services/MortalWoundTreatmentResourcePublicationTransaction.cs BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs BookOfEternityClient/Services/CanonicalStateNormalizer.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs BookOfEternityClient/Services/Validation/ValidationService.MortalItemMaterialization.cs BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs
git commit -m "feat(wounds): coordinate procedure publication claims (#1536)"
```

### Task 5: Close the bounded B.5 contour

**Files:**

- Modify: `specs/1536-complete-wound-materialization/tasks.md`
- Modify: this plan
- Test: all B.5 owning and retained B.1-B.4/T067/T068 controls selected below.

- [x] **Step 1: Run the complete B.5 owning contour**

Run separate bounded Focused selections for scalar procedure outcomes, Fate reaction, compound transaction lifecycle, procedure cold replay, and both player and combatant-member carriers. Confirm no timeout, duplicate test IDs, warning/error, or cleanup leak.

- [x] **Step 2: Run retained boundaries**

Run the smallest relevant retained selections for guaranteed stabilization, B.3 resource publication, B.4 selected item publication, T067 procedure resolution/persistence/cold claims, T068 resource finalization, and #1535 effect event publication. Do not require the intentionally deferred `reduce_severity`, recovery, course, or heal rows to pass.

- [x] **Step 3: Run exactly one meaningful Fast checkpoint**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Fast
```

Classify every failure against the pre-B.5 signature and rerun only a genuinely load-sensitive unrelated failure in isolation. Fast keeps its five-minute hard limit; issue #1551 restored the semantic project boundary instead of extending that limit. Do not spend time shaving seconds from a historically expanded Focused selection: use its documented bounded override when measurement proves the default cannot honestly finish, but never apply that override to Fast.

- [x] **Step 4: Request independent review**

Review the complete B.5 range for authority laundering, same-turn die/Fate reuse, missing no-resource transaction admission, cross-surface legacy reaction ingress, partial terminal release, direct normalizer bypass, cold-replay duplication, and accidental admission of deferred outcome kinds. Resolve every Critical/Important finding and rerun affected controls.

- [x] **Step 5: Record closure without closing T070 or #1536**

Update `tasks.md` with commit range, artifact IDs, exact pass/fail classification, independent-review verdict, deferred contours, and the explicit no-GM/docs/afterlife-update rationale. Keep T070 and issue #1536 open. Run `git diff --check`, stage only exact B.5 files, and commit bookkeeping:

```powershell
git commit -m "docs(wounds): record procedure publication closure (#1536)"
```

No push, PR, merge, PreMerge lane, or issue closure is part of B.5 unless the user explicitly requests it later.

## Closure evidence (2026-09-04)

B.5 is bounded-complete through `509554c2`. The implementation landed in
`e2ecb79b`, `c7bdb05d`, `77616367`, `045bb5de`, `2e3defc0`, and `3dbf0572`;
the bounded-test corrections landed in `92831931`, `a3eea647`, and `667f86b0`;
final-review remediation landed in `0cca886e` and `509554c2`.

The final parent-owned contour executed 39 tests: 38 passed, while exactly one
`reduce_severity` row remained RED at the deliberately deferred
`mortal_wound_treatment_publication_slice_unsupported` boundary. The owning
artifacts are:

- `20260904-111802-251-35456-36e973d8b89b47398bca5ca496dbbec1-focused`
  (11/11 scalar, repeat, and partial-success cases);
- `20260904-112040-020-24580-584075d2043a4684b1c096225c62db82-focused`
  (10/11 resource, Fate, and lifecycle cases; only the deferred
  `reduce_severity` row RED);
- `20260904-112207-664-46008-b61ec756d4f34de9ac9ab29166d157e0-focused`
  (6/6 admission, replay, and restored-duplicate cases);
- `20260904-112426-554-35780-b945a21c74734e0aaa0ba290793774a5-focused`
  (7/7 compensation, quarantine, and drift cases);
- `20260904-112648-390-46848-663010660e4e46ca86fc0b89b99f2f0f-focused`
  (4/4 changed-claim, Fate, and retry cases).

The retained B.1-B.4/T067/T068/#1535 controls passed 25/25 in
`20260904-112823-975-16220-db7d8ce960b34eb59c7dde6e5420e857-focused`,
`20260904-112930-200-50480-f941f162fcc54513a6be33a5e41d9b8d-focused`,
`20260904-113027-183-33000-f7d5315e649f48748b7cf0627e62e09d-focused`,
`20260904-113122-794-38016-5f419a32d71d40b4ae0e8ea6b4821ea3-focused`, and
`20260904-113158-467-58940-2b41d1fd09e245dabcbcbe7396ef30a4-focused`.
The single required Fast checkpoint
`20260904-082214-233-26816-a885b4a8285c4923a09768dc3ed2363f-fast`
completed all 4,759 tests in about 2:17: 4,694 passed and exactly 65 known
diagnosis REDs were classified, with no timeout, duplicate IDs, warning/error,
or cleanup debt.

The cumulative review first found four Important issues. The first remediation
left one Important and one Minor issue; the second remediation closed both. A
fresh independent rereview of the final state reported zero Critical,
Important, or Minor findings and `Ready: YES`.

`reduce_severity`, effect-bearing complications, course, recovery,
heal/legacy, and T069-C remain deferred T070 contours. This slice changed only
private client-owned Mortal publication enforcement: it did not change a
public API/DTO, persisted schema, player command, GM-authored response shape,
or afterlife runtime surface. The ordinary common-plan normalizer now applies
the already-specified client-owned after-images; it adds no new GM-authored
normalizer field or side-effect shape. Existing wound guidance and examples
already assign treatment narration/content to the GM and canonical identity,
history, receipts, and carrier post-state to the client. Therefore no GM
prompt, worked example, manifest, browser/console, migration, or afterlife
documentation update is required. T070 and issue #1536 remain open.
