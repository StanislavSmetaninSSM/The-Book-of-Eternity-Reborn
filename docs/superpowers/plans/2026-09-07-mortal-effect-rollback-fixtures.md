# Mortal Effect Rollback Fixture Cutover Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the existing generic effect publication/rollback tests so they reach their injected failure boundaries under the current wound-source contract.

**Architecture:** Use the already-registered materializable player skill as the source of ordinary periodic/action-control effects in this one test partial. Keep wound-owned effects and production validators unchanged. Source, definition and identity-index fixtures remain aligned before the signed snapshot is captured.

**Tech Stack:** C#, xUnit, System.Text.Json.Nodes, existing file-backed test context, PowerShell 7 bounded C# lanes.

**Status (2026-09-07):** Accepted at `dcbe766a..14d2dfd6`: fixture correction
`5235d413` and analyzer follow-through `14d2dfd6`, independent Spec compliant /
Quality Approved with zero findings. This does not close full T177 or #1536.

## Global Constraints

- Tracked source: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), T177 verification/fixture correction; specs/1536-complete-wound-materialization/spec.md, plan.md and tasks.md remain feature authority.
- **SC-004**: Injected failure at every publication boundary leaves either the complete before-state or complete after-state in 100% of rollback scenarios.
- **FR-100**: Fast pure tests MUST own structural validation, skill authority, reduction, slot accounting, and projection; file/cache/restart/replay/rollback and treatment lifecycle evidence MUST remain in Integration or other appropriate lifecycle lanes, followed by focused documentation guards, one meaningful Fast checkpoint, conditional FullValidation, review, and PreMerge only at the actual merge boundary.
- The core correction modifies only BookOfEternityClient.IntegrationTests/EffectMaterializationLifecycleTests.cs. The explicit analyzer follow-through below permits one assertion-form change in BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.DangerMode.cs. No production, shared fixture, category, runner, timeout, project, dependency, user-state or archive changes.
- Use EffectMaterializationTestContext.SeedPlayerSkillSourceAsync and MaterializableSkillId for the ordinary Mortal effect source; source.kind is skill, sourceId is the exact registered ID, and definitionKey remains EffectMaterializationTestFixture.DefinitionKey.
- Preserve all existing assertions, test names, MemberData rows, injected failure paths, command consumption checks, canonical byte comparisons, output rollback and operator-diagnostic preservation. Do not bypass source validation or accept an earlier unrelated exception as a successful probe.
- Keep actual afterlife profile/conflict source overrides, signed snapshots and explicit danger declarations unchanged. Do not convert their spiritual_art source into a skill.
- This is a generic effect rollback fixture correction, not wound gameplay implementation or a migration. The shared legacy wound helper remains untouched for separately owned tests.
- Work only in E:/Games/worktrees/boe-1536-wound-materialization on 1536-complete-wound-materialization. Preserve unrelated .serena/. No new branch, push, PR, merge, issue closure or child-session cleanup.
- One C# lane owner; use PowerShell 7 and scripts/test-csharp.ps1, drain every returned session before source edits or another run. Run the exact coherent 14-row Focused selection RED then GREEN, and one Fast control at the stable checkpoint. No unbounded test command, complete LifecycleIntegration, FullValidation, RegressionIntegration or PreMerge is requested.
- No GM-facing capability, command, state, validation, response, pending/control, receipt, prompt or example changes. Mortal and afterlife prompts, examples, manifests, matrix and source guards need no update for this test-only slice.

## Evidence and scope rationale

Current generic effect fixtures still seed an old array-form physical wound.
The current canonical wound source collector consumes typed object roots and
therefore cannot resolve mortal_world/wound/wound_test_torn_side/bleeding_consequence.
Three owner-carrier rows fail raw source validation; the pending-resolution row
throws InvalidDataException from ValidateEffectPlanPublicationBindingAsync before
its armed write hook. All four persist with danger-declaration validation disabled.
The two afterlife owner rows pass.

These tests assert publication/rollback, not creation of a wound-owned source.
Modern wounds have their own stricter binding/lifecycle; attaching a player's
physical wound to an NPC/combatant would change the scenario's ownership rather
than repair its generic setup. Existing neighboring effect validation/lifecycle
tests use the complete registered skill with the same periodic/action-control
definitions. The source fixture helper writes the skill before snapshot capture;
CreateIdentityIndex reads the adjusted canonical effect source.

## File map

- Modify BookOfEternityClient.IntegrationTests/EffectMaterializationLifecycleTests.cs: four Mortal source seeds, matching apply/canonical source selectors, two small private source/apply helpers.
- Modify BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.DangerMode.cs: one semantics-preserving predicate-overload assertion, under the analyzer follow-through below.
- Parent owns this plan, feature plan/tasks, ledger, independent review and acceptance.
- No new test or source file is required: the existing four methods / fourteen dynamic rows are executable regressions and remain intact.

### Task 1: Restore valid generic sources in effect publication rollback fixtures

**Files:**
- Modify/test: BookOfEternityClient.IntegrationTests/EffectMaterializationLifecycleTests.cs.

**Interfaces:**
- Consume EffectMaterializationTestContext.SeedPlayerSkillSourceAsync(JsonObject? definition = null, string skillId = MaterializableSkillId), MaterializableSkillId, existing CreateApplyCommand(string), CreateCanonicalEffect(string,string) and CreateIdentityIndex(params JsonObject[]).
- Produce private CreateMortalRollbackEffectSource(): JsonObject and CreateMortalRollbackEffectApply(string targetKind = "player"): JsonObject in the same GameEngineTurnLifecycleTests partial.
- No public API or production type changes.

- [x] **Step 1: Run the existing failing regression cohort before editing.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~GameEngineTurnLifecycleTests.EffectMaterializationLifecycleTests_"
```

Expect exactly four methods / fourteen rows: tracked-surface fact1,
player-publication theory7, owner-carrier theory5, pending-resolution fact1.
Record actual failures and exceptions. Four known failures are explained above;
the seven player rows use the same legacy seed and must also be classified from
the actual result. No new tests are needed to manufacture RED. Empty discovery,
build errors or null-reference setup failures are not the requested semantic RED.
If membership differs, report exact names and stop before editing.

- [x] **Step 2: Apply only the following fixture-source patch.**

```diff
*** Update File: BookOfEternityClient.IntegrationTests/EffectMaterializationLifecycleTests.cs
@@
-        await context.SeedPlayerWoundSourceAsync();
+        await context.SeedPlayerSkillSourceAsync();
@@
             EffectMaterializationTestFixture.CreateCommandRoot(
-                EffectMaterializationTestFixture.CreateApplyCommand()));
+                CreateMortalRollbackEffectApply()));
@@
         var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
             ownerKind: "player",
             profile: "periodic_damage");
+        effect["source"] = CreateMortalRollbackEffectSource();
+        effect["display"]!["sourceLabel"] = "Кровавый след";
         effect["triggers"]![0]!["resolutionMode"] = "bounded_receipt";
         effect["lifetime"]!["remainingTurns"] = 2;
-        await context.SeedPlayerWoundSourceAsync(definition);
+        await context.SeedPlayerSkillSourceAsync(definition);
@@
         if (string.Equals(carrierPath, EffectCarrierCatalog.NpcPath, StringComparison.Ordinal))
         {
-            await context.SeedPlayerWoundSourceAsync(CreateNonResourceEffectDefinition());
+            await context.SeedPlayerSkillSourceAsync(CreateNonResourceEffectDefinition());
@@
         if (string.Equals(carrierPath, EffectCarrierCatalog.EnemiesPath, StringComparison.Ordinal) ||
             string.Equals(carrierPath, EffectCarrierCatalog.AlliesPath, StringComparison.Ordinal))
         {
-            await context.SeedPlayerWoundSourceAsync(CreateNonResourceEffectDefinition());
+            await context.SeedPlayerSkillSourceAsync(CreateNonResourceEffectDefinition());
@@
     private static JsonObject CreateNonResourceEffectApply(string targetKind)
     {
-        var command = EffectMaterializationTestFixture.CreateApplyCommand(targetKind);
+        var command = CreateMortalRollbackEffectApply(targetKind);
         command["parameters"] = new JsonObject();
         return command;
     }

+    private static JsonObject CreateMortalRollbackEffectApply(string targetKind = "player")
+    {
+        var command = EffectMaterializationTestFixture.CreateApplyCommand(targetKind);
+        command["source"] = CreateMortalRollbackEffectSource();
+        return command;
+    }
+
+    private static JsonObject CreateMortalRollbackEffectSource() =>
+        new()
+        {
+            ["kind"] = "skill",
+            ["sourceId"] = EffectMaterializationTestContext.MaterializableSkillId,
+            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
+        };
+
     private static JsonObject CreateSpiritualRollbackDefinition()
```

Use apply_patch. The resident helper later replaces source with its existing
spiritual_art object; the conflict helper builds its own command and is unchanged.
Do not replace exception assertions, probe.Triggered or any expected bytes.
If the supplied patch fails compilation or produces another validation failure,
report exact evidence before inventing a gameplay change or broadening scope.

- [x] **Step 3: Run the exact regression cohort GREEN and one Fast checkpoint.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~GameEngineTurnLifecycleTests.EffectMaterializationLifecycleTests_"
.\scripts\test-csharp.ps1 -Lane Fast
```

Run sequentially and fully drain each. Required GREEN membership is the same
four methods/fourteen rows, no skip/duplicate/timeout/cleanup failure. Every
publication case must reach its original probe. Fast retains its complete
assembly, five-minute bound and host ceiling. Record actual counters, durations,
warnings/errors and artifact paths. On failure diagnose the exact boundary;
do not rerun broad controls, alter categories or weaken assertions.

- [x] **Step 4: Inspect and commit the one-file correction, then report.**

```powershell
git diff --check
git diff --stat
git diff -- BookOfEternityClient.IntegrationTests/EffectMaterializationLifecycleTests.cs
git add -- BookOfEternityClient.IntegrationTests/EffectMaterializationLifecycleTests.cs
git diff --cached --check
git diff --cached --stat
git commit -m "test: restore canonical effect rollback fixture sources (#1536)"
```

Stage this file alone. The unrelated validator EOL-only working-tree deviation
is not part of this task. The full report must include chronological RED/GREEN/
Fast commands/artifacts, exact cohort membership, expected probe coverage, scope,
source rationale and concerns. Return only status, commit, test summary and concerns.
Parent inspects actual evidence, requests independent task review and owns acceptance.
Full T177/T070/#1536 and the top-level77/177 count remain open.

## Parent self-review

SC-004/FR-100 map to Task1: real boundary failures, exact canonical restoration,
and unchanged Integration placement. There is no new wound mechanic, authority
schema, public DTO, shared fixture change or GM surface. The complete patch uses
existing helper signatures and adjusts source before both identity-index creation
and signed snapshot capture. All source strings are exact canonical identifiers.
Full feature gaps remain on the existing tasks; this bounded test correction does
not close them.

## Analyzer follow-through

The original danger test's assertion emitted xUnit2031 in both fresh Integration
builds. It also appears in the earlier declaration153648 log; later incremental
builds reporting zero warnings had hidden that diagnostic from the first audit.
This was not caused by changing the rollback fixture. A fresh bounded fixer was
authorized to change only the following assertion form in DangerMode.cs:

```csharp
var issue = Assert.Single(issues, IsDangerModeIssue);
```

It replaces `Assert.Single(issues.Where(IsDangerModeIssue))`; the predicate,
exact-one cardinality and following issue-code/path assertions are unchanged.
There is no warning suppression, behavior change or new test. Verification:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~T083a_DangerMode_"
```

The previously captured diagnostic is the pre-fix evidence. This Integration-only
syntax correction does not require another Fast or FullValidation control.

## Acceptance and actual evidence — 2026-09-07

Original review BASE `dcbe766ad5924a045cd87bba3e785316ffe88c6d`;
core commit `5235d4130f1554e5fa971c60b9fe39ed1458deae`;
analyzer commit `14d2dfd628cf3f8fc6cb24675c4f71e5d2f7eba8`.
The independent two-commit/two-path review is Spec compliant / Quality Approved
with zero Critical, Important or Minor findings. No external-code check was
required; parent additionally inspected the complete actual diffs and raw evidence.

Results under TestResults/test-lanes:

| Run | Result | Wall |
| --- | --- | --- |
| 20260907-170456-619-19428-efb1660ee3cc46ffabdeb9c782bdba17-focused | RED:14executed,3PASS/11FAIL | 2:09.3245469 |
| 20260907-170727-408-31160-1fc7ca6f68a646bba293c8e08340a640-focused | GREEN:14/14 | 1:41.8830300 |
| 20260907-170912-670-41492-fdaaf3827d5549e89ede44af175b049c-fast | Fast7698/7698 | 4:38.6647286/5m |
| 20260907-171731-376-41356-6b2890c4b64e4847b3d3c388b46490fd-focused | Analyzer correction:12methods/50rowsPASS | 2:04.9056801 |

The RED/GREEN command is Step1's exact coherent filter. Actual RED failures were
seven player raw-source rejections, three Mortal owner unresolved-source errors,
and one pending-resolution probe not reached. The general tracked-surface fact
and two afterlife owner rows passed. Parent compared the exact four-method /
fourteen-row membership before/after: no additions, omissions or changed cases.
All original probes, expected canonical bytes, command consumption and output /
operator-diagnostic assertions remain unchanged and now pass.

Both first Integration builds had one xUnit2031 warning, printed twice, and zero
errors. Fast's actual build had zero warnings/errors; the original implementer
report misplaced the Integration warning under Fast, corrected explicitly in
its appended report. The final fresh Integration build has zero warnings/errors.
The canonical effect sourceLabel changed from Рваная рана to Кровавый след; it
was not preserved as initially worded. No historical diagnostics are erased.

Fast's 26 official TRX files contain7698passing executions versus7644discovery
rows. All3942discovered methods are present, with no missing/extra method. Exactly
seven runtime-enumerated theories expand seven placeholders into61rows (+54).
Their raw theory IDs can repeat within one TRX, and truncated parameter display
names are not unique; there are zero cross-TRX shared IDs and zero duplicate
executionIds. No runner-reported duplicates, skipped cases, timeout or incomplete
owned-tree cleanup occurred in any of the four runs.

No FullValidation, complete lifecycle suite, extra Fast or PreMerge was run.
The two changed files are Integration tests only. Mortal/afterlife prompts,
examples, manifests, contract matrix and source guards need no update: no game
capability, authoring contract, UI or runtime authority changes. Full T177/T070/
#1536 and top-level77/177 remain open. No push, PR, merge or issue closure ran.
