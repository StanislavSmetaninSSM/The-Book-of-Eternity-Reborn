# Pending effect fixture source cutover Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the eleven existing pending-effect/resource integration checks required to accept #1536 T081-B1.

**Architecture:** Replace obsolete array-form wound registration in these generic effect fixtures with the existing registered skill source. Keep source selectors, canonical effects and identity indexes aligned before the original snapshot, without changing runtime rules or any behavioral assertion.

**Tech Stack:** C#, xUnit Integration, System.Text.Json.Nodes, PowerShell 7 bounded lanes.

## Global Constraints

- Tracked issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), T177 fixture correction required by T081-B1 acceptance. Constitution and `specs/1536-complete-wound-materialization/{spec,plan,tasks}.md` remain authority.
- Modify ONLY `BookOfEternityClient.IntegrationTests/ResourcePendingResolutionIntegrationTests.cs` and `BookOfEternityClient.IntegrationTests/EffectPendingWaveIntegrationTests.cs`. No production, shared helper, runner, timeout, category, project or dependency change.
- Preserve every existing assertion, method name, row, receipt binding/wave, definition/component/trigger/lifetime, effect/transition identity, snapshot timing, publication and byte-exact replay/rollback check.
- Use the already registered materializable player skill: kind `skill`, sourceId `EffectMaterializationTestContext.MaterializableSkillId`, definitionKey `EffectMaterializationTestFixture.DefinitionKey`. Seed before signed snapshot; assign canonical source before identity-index creation.
- These eleven rows test ordinary pending effects, not wound lineage or a wound consequence profile. Keep wound-specific sources and the shared legacy wound helper untouched.
- Same worktree `E:/Games/worktrees/boe-1536-wound-materialization`, branch `1536-complete-wound-materialization`. Use apply_patch with absolute worktree file paths. No remote/new branch/task closure/session cleanup; preserve unrelated `.serena/` without inspection or staging.
- One fixer owns both test files and bounded C# execution. PowerShell 7 plus `scripts/test-csharp.ps1`, exact Focused Integration selection, default five-minute bound. Do not repeat the already captured RED; parent alone runs one combined B1 Fast afterward.
- B1 stays unaccepted until the actual eleven-row GREEN, parent evidence audit, Fast and independent task review. Source diagnosis alone does not prove non-regression.
- This test-only correction changes no Mortal/afterlife GM capability, source rule, pending contract or UI. No GM prompt/example/matrix/manifest/source-guard update is required.

### Task 1: Restore valid source fixtures and prove the original eleven behaviors

**Files:** The two Integration files named above. Parent owns this plan, B1/T177 tracking and acceptance.

**Interfaces:** Existing SeedPlayerSkillSourceAsync(definition), MaterializableSkillId, DefinitionKey and CreateIdentityIndex(effect); new private CreatePendingEffectSource(): JsonObject in each separate test class.

- [x] **Step 1: Inspect the existing semantic RED and source diagnosis.**

The parent read actual summary/log/TRX evidence for `20260908-041114-524-51468-64a73eccf03f48ec9e39b7859c5737ab-focused`: zero of eleven pass; ten publication-binding exceptions and one raw apply-source failure. Build0/0, no skip/timeout/duplicate execution, complete cleanup, wall0:35.2524547. The source path was traced through the shared array-writing helper, current object-only wound collector and final effect binding; working rollback fixtures use the existing skill helper. This is the executable RED for this correction, not a failure to repeat.

- [ ] **Step 2: Apply only the complete two-file source correction.**

Rebase the two patch headers to the absolute worktree before apply_patch. All supplied bodies are complete; no runtime or shared-fixture change is authorized.

```diff
*** Begin Patch
*** Update File: BookOfEternityClient.IntegrationTests/ResourcePendingResolutionIntegrationTests.cs
@@
         definition["triggers"] = new JsonArray(BoundedResourceDamagedTrigger());
-        await context.SeedPlayerWoundSourceAsync(definition);
+        await context.SeedPlayerSkillSourceAsync(definition);
         await context.WriteJsonAsync(
@@
         var apply = EffectMaterializationTestFixture.CreateApplyCommand();
+        apply["source"] = CreatePendingEffectSource();
         apply["eventRef"]!["authorityId"] = "turn_43";
@@
         var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
             ownerKind: "player",
             profile: "periodic_damage");
+        effect["source"] = CreatePendingEffectSource();
+        effect["display"]!["sourceLabel"] = "Кровавый след";
         effect["triggers"]![0]!["resolutionMode"] = "bounded_receipt";
         effect["lifetime"]!["remainingTurns"] = 2;
-        await context.SeedPlayerWoundSourceAsync(definition);
+        await context.SeedPlayerSkillSourceAsync(definition);
@@
         var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
             profile: "event_reaction");
+        effect["source"] = CreatePendingEffectSource();
+        effect["display"]!["sourceLabel"] = "Кровавый след";
         effect["components"] = definition["components"]!.DeepClone();
         effect["triggers"] = definition["triggers"]!.DeepClone();
         effect["lifetime"]!["remainingTurns"] = 2;
-        await context.SeedPlayerWoundSourceAsync(definition);
+        await context.SeedPlayerSkillSourceAsync(definition);
@@
     private static JsonObject BoundedResourceDamagedTrigger() => new()
     {
         ["triggerId"] = "on_resource_damaged",
@@
         ["resolutionMode"] = "bounded_receipt"
     };
+
+    private static JsonObject CreatePendingEffectSource() =>
+        new()
+        {
+            ["kind"] = "skill",
+            ["sourceId"] = EffectMaterializationTestContext.MaterializableSkillId,
+            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
+        };
 
     private static ResourceDefinitionCatalog ParseDefinitions(JsonNode? root)
*** Update File: BookOfEternityClient.IntegrationTests/EffectPendingWaveIntegrationTests.cs
@@
         var (definition, effect) = CreateTwoWaveEffect(initialPoise.Maximum);
 
-        await context.SeedPlayerWoundSourceAsync(definition);
+        await context.SeedPlayerSkillSourceAsync(definition);
         await context.WriteJsonAsync(
@@
         var effectId = effect["effectId"]!.GetValue<string>();
 
-        await context.SeedPlayerWoundSourceAsync(definition);
+        await context.SeedPlayerSkillSourceAsync(definition);
         await context.WriteJsonAsync(
@@
         var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
             profile: "periodic_damage");
+        effect["source"] = CreatePendingEffectSource();
+        effect["display"]!["sourceLabel"] = "Кровавый след";
         effect["components"] = components.DeepClone();
@@
         var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
             profile: "periodic_damage");
+        effect["source"] = CreatePendingEffectSource();
+        effect["display"]!["sourceLabel"] = "Кровавый след";
         effect["components"] = definition["components"]!.DeepClone();
@@
         var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
             profile: "event_reaction");
+        effect["source"] = CreatePendingEffectSource();
+        effect["display"]!["sourceLabel"] = "Кровавый след";
         effect["components"] = components.DeepClone();
@@
         var history = ParseHistory(
             await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath),
             definitions);
-        await context.SeedPlayerWoundSourceAsync(definition);
+        await context.SeedPlayerSkillSourceAsync(definition);
         await context.WriteJsonAsync(
@@
     private static JsonObject CreateRootSpendCommand() => new()
     {
@@
         })
     };
+
+    private static JsonObject CreatePendingEffectSource() =>
+        new()
+        {
+            ["kind"] = "skill",
+            ["sourceId"] = EffectMaterializationTestContext.MaterializableSkillId,
+            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
+        };
 
     private static JsonObject CreateReceipt(
*** End Patch
```

- [ ] **Step 3: Run the unchanged eleven-row cohort GREEN.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourcePendingResolutionIntegrationTests|FullyQualifiedName~EffectPendingWaveIntegrationTests"
```

Require exactly the eleven fully qualified rows listed in the diagnosis appendix, all passing, zero build warnings/errors, skips, timeout or duplicate execution, complete owned-tree cleanup. Keep all assertions. If a later failure becomes visible after fixing the source, retain its artifact, trace it and report NEEDS_CONTEXT before expanding scope; do not make a gameplay/authority change or weaken expected values.

- [ ] **Step 4: Self-review, commit only both test files and report.**

Inspect git diff --check and the complete two-file diff. Confirm all old assertions, original IDs/receipt waves, definition/trigger/lifetime and snapshot calls are unchanged; the canonical source is adjusted before CreateIdentityIndex. Commit as `test: restore pending effect fixture sources (#1536)`. Report exact command, artifact, discovery/execution membership/counts, duration, warnings/errors, cleanup/timeout, changed files and concerns to the controller-specified report. No extra Fast, FullValidation or PreMerge. Release C# ownership.

Parent verifies the actual artifact and includes this explicitly bounded test-only follow-through in the B1 task review over recorded `e3949168..finalHEAD`. The original B1 production and unit-test code remain at `c9230065` and match the reviewed companion exactly. This restores the required control; it does not close T177 or the full wounds feature.

## Controller self-check

The existing eleven tests are the regressions; no new assertion-only RED is needed. The full patch changes six source seeds, one apply selector, five canonical source/labels and two private source helpers; all definition/effect IDs and behavior assertions remain. The current collector reads wound roots only as JsonObject and ignores the old fixture array, so this correction addresses the observed source rather than changing the validator. Both source helpers exist with the exact signatures. No player-facing or GM-authored behavior changes.

## Source-grounded diagnosis and exact cohort appendix

# #1536 / T177 B1 pending-effect fixture diagnosis

## Verdict

The observed eleven-row RED is caused by stale test fixture source registration, not by evidence about the ordinary reduction split at `c9230065`.

All eleven tests are generic resource/pending-effect lifecycle tests. They create periodic damage, periodic restore, or event-reaction definitions; none creates the `wound_consequence` profile, asserts a wound carrier, or requires wound-owned lineage. Each currently registers its definition through `EffectMaterializationTestContext.SeedPlayerWoundSourceAsync`, whose root is the old array form. Each active effect or apply command keeps the default selector `mortal_world/wound/wound_test_torn_side/bleeding_consequence` from `EffectMaterializationTestFixture`.

The current production source collector cannot consume that fixture shape:

1. `EffectAcceptedTurnInputComposer.SourceAuthorityPaths` includes `game_state/player/wounds.json`.
2. `BuildCanonicalSourceAuthority` calls `CollectCanonicalWoundSources`.
3. `CollectCanonicalWoundSources.ReadRoot` returns only `node as JsonObject`; the array written by `SeedPlayerWoundSourceAsync` therefore becomes `null`.
4. `WoundCarrierCatalog.Build` consequently exports no canonical wound occurrence/definition for `wound_test_torn_side`.
5. `EffectSourceAuthority.AddUnresolvedKeyIssues` emits `effect_source_selector_unresolved` for the exact default wound selector.
6. Ten rows first fail during `CanonicalStateNormalizer.ValidateEffectPlanPublicationBindingAsync` at `CanonicalStateNormalizer.Effects.cs:169`; the same-turn apply row fails earlier in raw validation at `effectChanges[0].source.source`.

This is source-grounded and matches every TRX failure. It also matches the already accepted correction in `docs/superpowers/plans/2026-09-07-mortal-effect-rollback-fixtures.md` and `EffectMaterializationLifecycleTests.cs`: ordinary Mortal effect fixtures use `SeedPlayerSkillSourceAsync`, `MaterializableSkillId`, and an exact `skill` source selector, while production and the shared legacy wound helper remain unchanged.

## Proposed bounded correction

Apply `pending-effect-fixture-code-proposal.patch`. It changes only:

- `BookOfEternityClient.IntegrationTests/ResourcePendingResolutionIntegrationTests.cs`
- `BookOfEternityClient.IntegrationTests/EffectPendingWaveIntegrationTests.cs`

The patch:

- replaces the six old wound-source registrations with `SeedPlayerSkillSourceAsync(definition)`;
- changes only each pre-existing effect/apply source selector to `mortal_world/skill/skill_test_bleeding/bleeding_consequence`;
- aligns the manually seeded active-effect display source label with the registered skill;
- adds one file-local source-construction helper to each test class.

It does not change test names, rows, assertions, receipts, causal wave ordinals, definitions, component payloads, triggers, lifetime, effect IDs, transition IDs, identity-index construction, snapshot timing/content rules, publication assertions, replay checks, protected byte sets, rollback behavior, shared helpers, or production. Identity indexes remain real and are built after the adjusted effect source is assigned. No GM-facing contract changes, so Mortal/afterlife prompts, docs, examples, manifests, and source guards require no update for this test-only correction.

## RED evidence already captured

Command:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourcePendingResolutionIntegrationTests|FullyQualifiedName~EffectPendingWaveIntegrationTests"
```

Artifact:

`E:/Games/worktrees/boe-1536-wound-materialization/TestResults/test-lanes/20260908-041114-524-51468-64a73eccf03f48ec9e39b7859c5737ab-focused`

Observed metadata: Focused; 5-minute bound; 11 total/executed, 0 passed, 11 failed; zero duplicate IDs; no timeout; owned-tree cleanup complete; build 0 warnings/0 errors. All eleven failures carry the same unresolved default wound selector. This existing artifact is the required RED and should not be rerun merely to recreate baseline failure.

## Required GREEN verification

After applying only the proposed patch, run the same coherent selection once:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourcePendingResolutionIntegrationTests|FullyQualifiedName~EffectPendingWaveIntegrationTests"
```

Required membership and result: exactly the following eleven fully qualified rows, each PASS; total/executed/passed `11/11/11`, failed/skipped `0/0`, no duplicate IDs, no timeout, owned-tree cleanup complete, and no build error.

1. `BookOfEternityClient.Tests.EffectPendingWaveIntegrationTests.CausalPendingWaves_RemainAtomicUntilTerminalReplay`
2. `BookOfEternityClient.Tests.EffectPendingWaveIntegrationTests.LastUsePendingFrontier_ReplaysExactTranscriptAndExpiresOnlyOnce`
3. `BookOfEternityClient.Tests.EffectPendingWaveIntegrationTests.TerminalReplay_ChangedOriginalCommandEnvelopeFailsClosedWithoutAfterImagesOrWrites`
4. `BookOfEternityClient.Tests.EffectPendingWaveIntegrationTests.IndependentAcceptedTurn_RestartsAtWaveZeroAndReplaysOnlyItsOwnTerminalBindings`
5. `BookOfEternityClient.Tests.EffectPendingWaveIntegrationTests.BoundedAfterComponent_SharedActivationEventRefWaitsForPredecessorWave`
6. `BookOfEternityClient.Tests.ResourcePendingResolutionIntegrationTests.BoundedTurnEnd_BeforeReceiptPublishesOnlyPendingTechnicalState`
7. `BookOfEternityClient.Tests.ResourcePendingResolutionIntegrationTests.FullTurnReceipt_ResolvesThroughCommonMutationAndAdvancesEffectExactlyOnce`
8. `BookOfEternityClient.Tests.ResourcePendingResolutionIntegrationTests.BoundedAfterComponent_DoesNotApplyDependentReceiptWhenPredecessorMadeNoChange`
9. `BookOfEternityClient.Tests.ResourcePendingResolutionIntegrationTests.BoundedAfterComponent_AppliesDependentReceiptAfterExactPredecessor`
10. `BookOfEternityClient.Tests.ResourcePendingResolutionIntegrationTests.InvalidReceipt_FailsClosedWithoutMechanicalOrPendingWrites`
11. `BookOfEternityClient.Tests.ResourcePendingResolutionIntegrationTests.SameTurnAppliedBoundedEffect_RebindsAcceptedApplicationAcrossReceiptResubmission`

## B1 concern / claim boundary

The fixture incompatibility is proven. It is not yet proven that the ordinary reduction split at `c9230065` is non-regressing for this cohort, because the stale source prevents all eleven rows from reaching and completing their intended behavior. B1 must remain unaccepted until the parent audits the two-file diff, applies the fixture correction, and confirms the exact eleven-row GREEN artifact. A GREEN result would remove this fixture obstruction; it would be the evidence needed for the bounded B1 cohort, not something established by this read-only diagnosis.
