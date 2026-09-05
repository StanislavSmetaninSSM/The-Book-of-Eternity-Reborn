# Skill-Scoped Roll Modifier Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the common `roll_modifier` effect profile with an explicit broad-or-exact-skill scope, bind exact skills to canonical target authority, and use one scope-aware reducer for wound treatment and every future roll consumer.

**Architecture:** `EffectComponentProfiles` owns the closed JSON union, while an immutable `EffectRollSkillScopeAuthority` composes the GM-offered and final accepted skill catalogs from canonical player/NPC roots. The accepted effect planner validates every new materialization against both catalogs and seals their fingerprint; runtime snapshots retain the current catalog. One shared `EffectRollContributionResolver` captures a minimal versioned `EffectDetachedRollSourceAuthority` before filtering and uses the same validation/filter/reduction core for live, fresh, and detached treatment paths. Wound materialization reuses these authorities and maps binding failures back into its existing atomic repair loop.

**Tech Stack:** .NET/C#; `System.Text.Json` and `JsonNode`; xUnit; PowerShell 7; repository test-lane runner `scripts/test-csharp.ps1`; GitHub Spec Kit artifacts.

## Global Constraints

- GitHub issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536) remains the tracked implementation task; #1535 is a completed dependency whose durable effect contract must be amended, not reopened as a second runtime.
- Work only in `E:\Games\worktrees\boe-1536-wound-materialization` on branch `1536-complete-wound-materialization`.
- Preserve the existing uncommitted Task 7 wound documentation changes and never read, modify, stage, or delete `.serena/`.
- Use direct pre-alpha cutover: every `roll_modifier` requires explicit `scope`; add no migration, compatibility reader, fallback, or implicit `all` default.
- The closed scope union is exactly `{ "kind": "all" }` or `{ "kind": "skill", "skillId": "..." }`; `skill` is legal only with exactly `operations: ["skill_check"]`.
- The GM authors the effect. The client provides the bounded selectable catalog, resolves exact permanent identity, and rejects unknown, idless, inactive, stale, wrong-owner, exact-duplicate, or Unicode-confusable selections atomically.
- A skill introduced by the same GM response was not offered and is not selectable. A previously offered skill removed or disabled before publication makes the new effect invalid.
- Later loss of a skill never deletes or heals an accepted effect/wound. The exact component becomes derived dormant and only the same permanent `skillId` can reactivate it.
- One skill-scoped `roll_modifier` is one component and one wound-consequence slot. Multiple skills require multiple components and slots.
- Filter by realm, actor, operation, and scope before applying the unchanged reducer: advantage alone wins, disadvantage alone wins, both cancel, repeated equal contributions do not escalate.
- `resolved_skill_tier` supplies the exact selected canonical `skillId`; `fixed_zero` supplies no skill identity and therefore receives only broad modifiers.
- Detached procedure replay persists only normalized ordered roll mechanics, never a full effect snapshot or narrative/carrier payload. Detached validation proves source/result semantic agreement; fresh accepted-state validation is the origin trust anchor and must restore all tentative claim registries on mismatch.
- Player-facing projection never exposes `skillId`; it shows a readable broad/specific scope and labels a currently unavailable exact skill as inactive.
- Synchronize Mortal and afterlife prompts, contracts, examples, manifests, and source/documentation guards in the same change. Include worked GM examples for both broad and exact-skill wound consequences and a non-wound source.
- Use TDD. Run only `pwsh -NoProfile -File .\scripts\test-csharp.ps1`; never run raw or unbounded `dotnet test`.
- Keep pure parser/authority/reducer/projection tests in Fast. Keep canonical-file, cache, restart, replay, rollback, and lifecycle tests in Integration. Use the smallest Focused selection during implementation, one meaningful Fast checkpoint, conditional FullValidation for the documentation boundary, and one PreMerge control immediately before merge.
- Test deadlines are protective defaults. If one coherent Focused selection is measured beyond five minutes, rerun it with explicit headroom up to `-TimeoutMinutes 15` and record the measured reason; do not weaken coverage or micro-optimize merely to beat an obsolete limit.

---

## File Responsibility Map

### New production units

- `BookOfEternityClient/Services/EffectRollSkillScopeAuthority.cs` — immutable offered/current skill catalogs, exact/confusable resolution, new-binding validation, runtime availability, fingerprint, and safe GM catalog projection.
- `BookOfEternityClient/Services/EffectDetachedRollSourceAuthority.cs` — immutable typed normalized roll rows, direct-cutover schema, bounds, structural/identity validation, semantic equality, detached cloning, and deterministic fingerprinting.
- `BookOfEternityClient/Services/EffectRollContributionResolver.cs` — trusted roll context, normalized source capture, live/detached skill proof adapters, scope filtering, evidence projection, and the single advantage/disadvantage reducer.

### Existing production units

- `BookOfEternityClient/Services/EffectComponentProfiles.cs` — structural closed-union validation only; it never reads canonical state.
- `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs` — composes pre-turn/offered and accepted/final skill roots using the existing accepted-skill merge semantics.
- `BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs` — carries detached skill-scope authority on `EffectAcceptedTurnInput`.
- `BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs` — seals the skill-catalog fingerprint into the existing input fingerprint.
- `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs` — validates bound component scopes immediately after parameter binding and before stack/lifecycle mutation, including reaction applications.
- `BookOfEternityClient/Services/Validation/ValidationService.EffectMaterialization.cs` — supplies final accepted roots and attaches wound-specific scope failures to repair contexts.
- `BookOfEternityClient/Services/WoundResponseInputComposer.cs` and `BookOfEternityClient/Services/WoundRepairPacketBuilder.cs` — preserve precise `payload.scope.skillId` coordinates as repairable wound errors.
- `BookOfEternityClient/Services/WoundAcceptedTurnPlan.cs` — includes skill authority in the already-existing wound/effect input seal; it does not add a parallel top-level authority.
- `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs` — supplies final skill after-images for treatment consequence rematerialization.
- `BookOfEternityClient/Services/EffectMechanicsSnapshot.cs` — loads and retains the current skill catalog under the same quiescent read lease as active effects.
- `BookOfEternityClient/Services/MortalWoundTreatmentAuthority.cs` and `BookOfEternityClient/Services/MortalWoundTreatmentAcceptedCanonicalProjection.cs` — preserve both selected `skillId` and capability reference in skill-tier requirement evidence.
- `BookOfEternityClient/Services/MortalWoundTreatmentRequirementAuthorityBundle.cs` — seals the selected permanent skill identity in the success witness.
- `BookOfEternityClient/Services/MortalWoundProcedureCheckAuthority.cs`, `BookOfEternityClient/Services/MortalWoundProcedureCheckAuthority.FreshValidation.cs`, and `BookOfEternityClient/Services/MortalWoundTreatmentDetachedSealValidator.cs` — carry nullable `RollSkillId` plus normalized roll-source authority, derive the exact detached skill proof, call the common resolver, and protect live/replay/fresh seals.
- `BookOfEternityClient/UI/EffectPlayerProjection.cs` — renders broad, focused, and dormant scope text without technical identifiers.
- `BookOfEternityClient/Models/TurnRequest.cs` — exposes the bounded advisory `effectSkillScopeCatalog` to the GM.
- `BookOfEternityClient/Services/LiveTurnPreparationService.cs` and `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` — attach the pre-turn catalog to live-helper and ordinary player requests; afterlife-only special requests retain an explicit empty catalog.
- `BookOfEternityClient/Services/WoundConsequenceEnvelopeCatalog.cs`, `BookOfEternityClient/Services/WoundMaterializationContract.cs`, and `BookOfEternityClient/Services/WoundPersistedConsequenceEnvelopeAdapter.cs` — accept the new payload without treating scope as an extra consequence or power term.

### Test and fixture units

- `BookOfEternityClient.Tests/EffectRollSkillScopeAuthorityTests.cs` — pure offered/final catalog and identity resolution matrix.
- `BookOfEternityClient.Tests/EffectRollContributionResolverTests.cs` — pure normalized-source capture, validation, clone/fingerprint, scope filtering, and reducer matrix.
- `BookOfEternityClient.Tests/EffectMaterializationContractTests.cs` and `BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs` — closed structural union.
- `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs` and `BookOfEternityClient.Tests/EffectAcceptedTurnPlanCacheTests.cs` — application binding and cache invalidation.
- `BookOfEternityClient.Tests/EffectPlayerProjectionTests.cs` — readable scope and visibility behavior.
- `BookOfEternityClient.Tests/WoundConsequenceEnvelopeTests.cs`, `BookOfEternityClient.Tests/WoundEffectBatchPlannerTests.cs`, and `BookOfEternityClient.Tests/WoundRepairPacketBuilderTests.cs` — slot accounting, shared binding, and repair coordinates.
- `BookOfEternityClient.TestSupport/EffectMaterializationTestFixture.cs` and `BookOfEternityClient.Tests/WoundContractTestData.cs` — explicit broad-scope defaults for existing test data.
- `BookOfEternityClient.IntegrationTests/EffectSkillScopeLifecycleTests.cs` — ordinary/wound materialization, same-turn final validation, dormancy/reactivation, cache/replay/rollback, and turn-request staging.
- `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests*.cs` — focused versus broad treatment contribution, typed detached replay, joint-tamper fresh rejection, and claim-registry rollback.
- `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs` — explicit ownership of the new Integration source.

### Durable contracts and GM surfaces

- `specs/1535-complete-effect-materialization/` and `specs/1536-complete-wound-materialization/` — amended common profile, target binding, treatment, replay, and execution tasks.
- `OtherGuides/Effect_Materialization_Contract.md` and `OtherGuides/Wound_Materialization_Contract.md` — authoritative GM instructions.
- `Examples/E_CLI_Effect_Materialization.txt`, `Examples/E_CLI_Wound_Materialization.txt`, and `Examples/E_CLI_Afterlife_Turns.txt` — valid common, Mortal wound, and afterlife examples.
- `Examples/example_validation_manifest.json` — validation coverage registration.
- `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`, `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs`, `BookOfEternityClient.Tests/EffectMaterializationSourceGuardTests.cs`, `BookOfEternityClient.Tests/WoundMaterializationSourceGuardTests.cs`, `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`, and `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs` — executable synchronization guards.

---

### Task 1: Amend the approved Spec Kit contracts

**Files:**
- Modify: `docs/superpowers/specs/2026-09-05-roll-modifier-skill-scope-design.md`
- Modify: `specs/1535-complete-effect-materialization/spec.md`
- Modify: `specs/1535-complete-effect-materialization/plan.md`
- Modify: `specs/1535-complete-effect-materialization/data-model.md`
- Modify: `specs/1535-complete-effect-materialization/contracts/effect-command-and-envelope.md`
- Modify: `specs/1535-complete-effect-materialization/contracts/effect-player-projection.md`
- Modify: `specs/1535-complete-effect-materialization/quickstart.md`
- Modify: `specs/1535-complete-effect-materialization/tasks.md`
- Modify: `specs/1536-complete-wound-materialization/spec.md`
- Modify: `specs/1536-complete-wound-materialization/plan.md`
- Modify: `specs/1536-complete-wound-materialization/data-model.md`
- Modify: `specs/1536-complete-wound-materialization/contracts/wound-effects-and-atomicity.md`
- Modify: `specs/1536-complete-wound-materialization/contracts/mortal-wound-treatment.md`
- Modify: `specs/1536-complete-wound-materialization/quickstart.md`
- Modify: `specs/1536-complete-wound-materialization/tasks.md`

**Interfaces:**
- Consumes: approved design `docs/superpowers/specs/2026-09-05-roll-modifier-skill-scope-design.md` and tracked issue #1536.
- Produces: one aligned durable contract and explicit #1536 task IDs for Tasks 2–12 below.

- [ ] **Step 1: Load governance and preserve the existing feature artifacts**

Read `.specify/memory/constitution.md` and use `spec-kit-superpowers-bridge` to govern a controlled in-place amendment. Do not invoke the generative `speckit-specify`, `speckit-plan`, or `speckit-tasks` workflows: their new-feature/template behavior can create another branch or replace the mature #1535/#1536 artifacts. Treat the approved design as the clarification result and ask no new product question unless the existing artifacts reveal a genuine contradiction.

- [ ] **Step 2: Amend #1535 as a completed-foundation extension**

Add an amendment headed `2026-09-05 — exact skill scope extension from #1536` to the listed #1535 files. Use this exact payload contract:

```json
{
  "operations": ["skill_check"],
  "contribution": "disadvantage",
  "scope": { "kind": "skill", "skillId": "skill_lockpicking" }
}
```

State that historical #1535 tasks remain complete and the new implementation work is owned by #1536.

- [ ] **Step 3: Add the scope sub-slice to #1536**

Add requirements and task rows covering structural validation, offered/final binding, runtime dormancy, shared reduction, treatment `RollSkillId`, one-component/one-slot accounting, direct cutover, GM catalog, projection, lifecycle/replay/rollback, documentation, and lane placement. Use new sequential task IDs after the current highest ID; do not renumber or uncheck completed work.

- [ ] **Step 4: Run cross-artifact analysis**

Invoke the read-only `speckit-analyze`. Under the user's 2026-09-05 approval, apply controlled remediation only inside the listed artifacts, then rerun the read-only analysis until every relevant inconsistency is resolved.

Expected: no unresolved ERROR or HIGH finding; #1535 and #1536 agree on the three-field payload and exact-skill semantics.

- [ ] **Step 5: Scan for stale two-field contract statements**

Run:

```powershell
rg -n 'roll_modifier|operations.*contribution|contribution.*operations' specs/1535-complete-effect-materialization specs/1536-complete-wound-materialization
```

Expected: every normative `roll_modifier` payload includes explicit `scope`; any historical quotation is visibly labeled non-current.

- [ ] **Step 6: Commit the artifact amendment**

```powershell
git add docs/superpowers/specs/2026-09-05-roll-modifier-skill-scope-design.md specs/1535-complete-effect-materialization specs/1536-complete-wound-materialization
git commit -m "docs(effects): specify exact skill roll scope (#1536)"
```

Expected: the commit contains only approved design/Spec Kit files; the pre-existing Task 7 documentation changes remain unstaged.

---

### Task 2: Enforce the closed structural scope union

**Files:**
- Modify: `BookOfEternityClient/Services/EffectComponentProfiles.cs`
- Modify: `BookOfEternityClient.Tests/EffectMaterializationContractTests.cs`
- Modify: `BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs`
- Modify: `BookOfEternityClient.TestSupport/EffectMaterializationTestFixture.cs`

**Interfaces:**
- Consumes: common `roll_modifier` payload and existing `effect_materialization_invalid_component` issue family.
- Produces: `EffectComponentProfiles.ValidateComponent(...)` accepting only the explicit discriminated union; existing fixture-created modifiers use broad scope.

- [ ] **Step 1: Add RED contract rows**

Add theory cases proving these exact outcomes:

```csharp
yield return ValidScope(new JsonObject { ["kind"] = "all" });
yield return ValidScope(new JsonObject
{
    ["kind"] = "skill",
    ["skillId"] = "skill_lockpicking"
});
yield return InvalidScope(null, "payload.scope");
yield return InvalidScope(new JsonObject(), "payload.scope.kind");
yield return InvalidScope(new JsonObject
{
    ["kind"] = "all",
    ["skillId"] = "skill_lockpicking"
}, "payload.scope.skillId");
yield return InvalidScope(new JsonObject
{
    ["kind"] = "skill"
}, "payload.scope.skillId");
```

Also assert that `kind=skill` rejects any operation set other than exactly one ordinal `skill_check`, and that unknown fields at payload/scope levels reject.

- [ ] **Step 2: Run the smallest RED selections**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectMaterializationContractTests&FullyQualifiedName~RollModifier"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectSourceDefinitionContractTests&FullyQualifiedName~RollModifier"
```

Expected: FAIL because `scope` is currently unknown or missing scope is still accepted.

- [ ] **Step 3: Implement structural validation**

Replace `ValidateRollModifier` with the following control flow and add `TryReadClosedStringArray` as the returning counterpart of the existing validator:

```csharp
private static void ValidateRollModifier(
    JsonElement payload,
    string path,
    List<ValidationIssue> issues)
{
    ValidateClosedObject(
        payload,
        path,
        Set("operations", "contribution", "scope"),
        issues);
    var operations = TryReadClosedStringArray(
        payload,
        path,
        "operations",
        RollOperations,
        issues);
    RequireClosedString(payload, path, "contribution", Contributions, issues);

    if (!payload.TryGetProperty("scope", out var scope) ||
        scope.ValueKind != JsonValueKind.Object)
    {
        Add(issues, path + ".scope", "effect_materialization_invalid_component",
            "closed roll scope object", Describe(payload, "scope"));
        return;
    }

    var scopePath = path + ".scope";
    var kind = RequireClosedString(
        scope,
        scopePath,
        "kind",
        Set("all", "skill"),
        issues);
    switch (kind)
    {
        case "all":
            ValidateClosedObject(scope, scopePath, Set("kind"), issues);
            break;
        case "skill":
            ValidateClosedObject(scope, scopePath, Set("kind", "skillId"), issues);
            RequireExactIdentifier(scope, scopePath, "skillId", issues);
            if (operations is not ["skill_check"])
            {
                Add(issues, path + ".operations",
                    "effect_materialization_invalid_component",
                    "exactly [\"skill_check\"] for scope.kind=skill",
                    Describe(payload, "operations"));
            }
            break;
    }
}
```

`TryReadClosedStringArray` must return `string[]?`, retain the current non-empty/unique/registered checks, and return `null` whenever it adds an issue.

- [ ] **Step 4: Cut the shared fixture to explicit broad scope**

For the `roll_modifier` branch of `EffectMaterializationTestFixture.CreateComponent`, produce:

```csharp
["payload"] = new JsonObject
{
    ["operations"] = new JsonArray("attack_roll"),
    ["contribution"] = "disadvantage",
    ["scope"] = new JsonObject { ["kind"] = "all" }
}
```

Do not add a helper that silently supplies scope in production parsing.

- [ ] **Step 5: Run the two GREEN selections**

Run the two Step 2 commands again.

Expected: PASS; valid broad/focused rows succeed and every malformed union row reports `effect_materialization_invalid_component` at the exact scope/operations path.

- [ ] **Step 6: Commit the structural cutover**

```powershell
git add BookOfEternityClient/Services/EffectComponentProfiles.cs BookOfEternityClient.Tests/EffectMaterializationContractTests.cs BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs BookOfEternityClient.TestSupport/EffectMaterializationTestFixture.cs
git commit -m "feat(effects): require explicit roll scope (#1536)"
```

---

### Task 3: Build canonical offered/current skill-scope authority

**Files:**
- Create: `BookOfEternityClient/Services/EffectRollSkillScopeAuthority.cs`
- Create: `BookOfEternityClient.Tests/EffectRollSkillScopeAuthorityTests.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`

**Interfaces:**
- Consumes: canonical roots `game_state/player/skills_active.json`, `game_state/player/skills_passive.json`, and `game_state/npcs/npc_core.json`; exact target coordinates use existing `EffectTargetKey`.
- Produces: `EffectRollSkillScopeAuthority.Build(EffectRollSkillScopeAuthorityInput)`, `ResolveForNewBinding`, `ResolveCurrent`, `ValidateNewComponents`, `Fingerprint`, and `CreateGmCatalog`.

- [ ] **Step 1: Write the RED authority matrix**

Create pure tests for player active/passive and NPC active/passive rows. Cover offered+current success, not-offered same-turn addition, final removal, inactive/terminal final state, idless row, wrong owner, exact duplicate, confusable competitor, exact case mismatch, unknown ID, missing catalog, detached inputs, deterministic ordering/fingerprint, and bounds.

Use these canonical fixtures:

```csharp
private static JsonObject Skill(string id, string name, bool active = true) => new()
{
    ["skillId"] = id,
    ["skillName"] = name,
    ["lifecycle"] = active ? "active" : "inactive",
    ["active"] = active
};

private static EffectTargetKey Player =>
    new("mortal_world", "player", "player_current");
```

- [ ] **Step 2: Run the new class and observe RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectRollSkillScopeAuthorityTests"
```

Expected: FAIL to compile because the authority types do not exist.

- [ ] **Step 3: Define the immutable authority API**

Create these exact public-to-assembly types:

```csharp
internal enum EffectRollSkillScopeState
{
    Usable,
    Unavailable,
    Missing,
    InvalidAuthority
}

internal sealed record EffectRollSkillScopeRow(
    EffectTargetKey Target,
    string SkillId,
    string DisplayName,
    string Lifecycle,
    bool Active,
    string SourcePath);

internal sealed record EffectRollSkillScopeResolution(
    EffectRollSkillScopeState State,
    string? DisplayName,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => State != EffectRollSkillScopeState.InvalidAuthority;
    internal bool IsUsable => State == EffectRollSkillScopeState.Usable;
}

internal sealed record EffectRollSkillScopeAuthorityInput(
    IReadOnlyDictionary<string, JsonNode?> OfferedRoots,
    IReadOnlyDictionary<string, JsonNode?> CurrentRoots);
```

`EffectRollSkillScopeAuthority` must deep-clone input JSON, freeze rows, and expose no mutable graph.

- [ ] **Step 4: Compose canonical player/NPC rows**

Recognize player arrays `activeSkillChanges|skills` and `passiveSkillChanges|skills`. Enumerate NPCs through an internalized `EffectAcceptedTurnInputComposer.EnumerateCanonicalNpcActors` so identical mirrored actors are deduplicated exactly as effect target authority already does; read their `activeSkills` and `passiveSkills`.

Use permanent `skillId` only. Display fallback order is `displayName`, `skillName`, `name`, then `"Навык"`. A row is usable unless `active=false`, `isActive=false`, `isInactive=true`, or `lifecycle|status|state|availability` is terminal. Bound catalog projection to 128 targets, 128 selectable skills per target, and 2048 selectable rows total; exceeding a bound marks only the affected catalog authority invalid and never broad-scope mechanics.

- [ ] **Step 5: Implement exact/confusable resolution**

Use `StringComparer.Ordinal` for identity and `MortalLocationIdentityState.BuildConfusableKey` only to detect competitors. Apply this table:

```text
new binding: exactly one offered usable row AND exactly one current usable row,
             with no exact/confusable competitor in either catalog
runtime:     one exact current usable row -> Usable
             one exact current inactive/terminal row -> Unavailable
             no exact current row -> Missing
             duplicate exact row or an exact row plus confusable competitor -> InvalidAuthority
```

A merely similar/confusable row with no exact selected row is `Missing`, so it cannot inherit an old effect.

- [ ] **Step 6: Implement component validation and GM projection**

Expose:

```csharp
internal IReadOnlyList<ValidationIssue> ValidateNewComponents(
    EffectTargetKey target,
    JsonArray components,
    string path,
    string section = "effect_materialization");

internal EffectRollSkillScopeResolution ResolveForNewBinding(
    EffectTargetKey target,
    string skillId,
    string path,
    string section = "effect_materialization");

internal EffectRollSkillScopeResolution ResolveCurrent(
    EffectTargetKey target,
    string skillId,
    string path);

internal JsonObject CreateGmCatalog();
```

`CreateGmCatalog()` emits only offered, usable, exact/confusable-unique rows in ordinal order:

```json
{
  "schemaVersion": 1,
  "targets": [{
    "realm": "mortal_world",
    "kind": "player",
    "targetId": "player_current",
    "skills": [{ "skillId": "skill_lockpicking", "displayName": "Взлом" }]
  }]
}
```

- [ ] **Step 7: Add accepted-root composition without authorizing new rows**

Add:

```csharp
internal static EffectRollSkillScopeAuthority ComposeSkillScopeAuthority(
    IReadOnlyDictionary<string, JsonNode?> preTurnRoots,
    IReadOnlyDictionary<string, JsonNode?>? acceptedRoots = null)
```

Use pre-turn roots as `OfferedRoots`. Build `CurrentRoots` by applying existing `ComposeSkillAcceptedRoot` to the two player roots and selecting the accepted NPC root when supplied, otherwise the pre-turn NPC root. Never copy a newly accepted row into `OfferedRoots`.

- [ ] **Step 8: Run GREEN and commit**

Run the Step 2 command.

Expected: PASS for the complete pure matrix with deterministic fingerprints and no leaked mutable JSON.

```powershell
git add BookOfEternityClient/Services/EffectRollSkillScopeAuthority.cs BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs BookOfEternityClient.Tests/EffectRollSkillScopeAuthorityTests.cs
git commit -m "feat(effects): bind exact target skill authority (#1536)"
```

---

### Task 4: Seal scope authority into accepted effect and wound planning

**Files:**
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`
- Modify: `BookOfEternityClient/Services/AcceptedEffectBoundaryTranscript.cs`
- Modify: `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- Modify: `BookOfEternityClient/Services/EffectSourceDefinitionContract.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlan.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.EffectMaterialization.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.WoundMaterialization.cs`
- Modify: `BookOfEternityClient/Services/WoundResponseInputComposer.cs`
- Modify: `BookOfEternityClient/Services/WoundRepairPacketBuilder.cs`
- Modify: `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs`
- Modify: `BookOfEternityClient.Tests/EffectAcceptedTurnPlanCacheTests.cs`
- Modify: `BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs`
- Modify: `BookOfEternityClient.Tests/WoundEffectBatchPlannerTests.cs`
- Modify: `BookOfEternityClient.Tests/WoundRepairPacketBuilderTests.cs`

**Interfaces:**
- Consumes: `EffectRollSkillScopeAuthority` from Task 3.
- Produces: scope-bound, fingerprinted, atomic ordinary/reaction/wound applications and repairable wound selector failures.

- [ ] **Step 1: Add RED planner and cache tests**

Prove all of the following:

```text
scope=all applies without a target skill catalog
scope=skill applies for one offered+current exact target skill
same-turn new skill rejects
same-turn removed/disabled selected skill rejects
wrong-target, duplicate, confusable, idless, and no-catalog selections reject
reaction apply_definition uses the same binding gate
changing only skillId or offered/current catalog state changes the input fingerprint
failed binding allocates/publishes no effect, component, transition, wound, or partial carrier
```

- [ ] **Step 2: Run RED planner selections**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectAcceptedTurnPlannerTests&FullyQualifiedName~SkillScope|FullyQualifiedName~EffectAcceptedTurnPlanCacheTests&FullyQualifiedName~SkillScope|FullyQualifiedName~WoundEffectBatchPlannerTests&FullyQualifiedName~SkillScope"
```

Expected: FAIL because accepted input and planner do not carry or enforce skill authority.

- [ ] **Step 3: Extend accepted input and fingerprint**

Append this optional member so existing direct constructors continue compiling while focused tests migrate explicitly:

```csharp
internal sealed record EffectAcceptedTurnInput(
    // existing members unchanged
    EffectCarrierCatalogInput? AcceptedCarrierBaselines = null,
    EffectRollSkillScopeAuthority? SkillScopeAuthority = null);
```

In `EffectAcceptedTurnPlanCache.CreateFingerprint`, bump `schemaVersion` from `3` to `4` and add:

```csharp
["skillScopeAuthorityFingerprint"] =
    input.SkillScopeAuthority?.Fingerprint ?? "none"
```

Add the same fingerprint field to `WoundAcceptedTurnFingerprints.ComputeEffectInput`; do not create a second wound plan authority field. Also bind the retained `plan.SkillScopeAuthority` fingerprint into `WoundAcceptedTurnFingerprints.ComputeEffectPlan`, the accepted-boundary transcript authority stamp/fingerprint, `AcceptedMechanicsPlanner.CreateEffectPlanAuthority`, and `FinalEffectAuthorityAgrees`. A detached plan with a dropped or changed retained authority must fail both wound candidate acceptance and transcript completion before reaction allocation.

- [ ] **Step 4: Build scope authority in every production composer path**

Append `IReadOnlyDictionary<string, JsonNode?>? acceptedSourceRoots = null` to `EffectAcceptedTurnInputComposer.Compose`, create the authority through `ComposeSkillScopeAuthority(preTurnSourceRoots, acceptedSourceRoots)`, and assign it to the input.

Pass `acceptedSources` from both `ComposeIdentityInput` and `ComposeInput` closures in `ValidationService.EffectMaterialization.cs`. Leave QTE deferred planning on pre-turn roots for both halves. Task 6 will pass final treatment skill after-images.

- [ ] **Step 5: Validate after parameter binding and before mutation**

Thread `input.SkillScopeAuthority` into all three `ApplyApplication` call sites. Immediately after:

```csharp
var components = definition["components"]!.DeepClone().AsArray();
BindParameters(components, application.Parameters);
```

Re-run the registered component-profile structural validator over every bound component before scope resolution. This is required because scalar source parameters replace top-level payload members; malformed post-binding `scope`, `operations`, or another closed member must fail before any lifecycle or application mutation. The source-definition contract must reject `parameterBounds.scope`: the closed scope object is source-authored authority and is not a scalar application parameter.

execute:

```csharp
var scopeIssues = skillScopeAuthority?.ValidateNewComponents(
    application.Target,
    components,
    application.Path,
    application.Section) ?? Array.Empty<ValidationIssue>();
issues.AddRange(scopeIssues);
if (scopeIssues.Count != 0)
    return null;
```

Extend the private `Application` record with `string Path` and `string Section`. Ordinary commands use their exact `effectChanges[n].apply` coordinate, reactions use `effect.reactions[eventRef]`, and wound roots use the original proposal component coordinate described in Step 6.

Treat only a typed, authenticated `treat` rematerialization as continuation of an already accepted definition graph: it preserves the exact sealed selector and must not re-run new-binding availability after later skill loss. Create, worsen, ordinary, and reaction applications remain new-binding gated. Existing source/fingerprint agreement must reject any selector rewrite.

- [ ] **Step 6: Preserve wound proposal coordinates for repair**

Before calling `WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated`, preflight every referenced wound definition against the exact `ProposedAfter.Owner` target with the same `ValidateNewComponents` method. Project failures to:

```text
woundDecisions[N].proposal.consequenceDefinitions[D].definition.components[C].payload.scope.skillId
```

Normalize their code to `wound_materialization_effect_binding_invalid`, keep `section=wound_materialization`, then call `WoundResponseInputComposer.AttachRepairContexts(...)`. Extend `TryProjectRepairCoordinate` so a path under `.definition.components[...].payload.scope` is accepted directly; retain the existing link-specific path logic.

- [ ] **Step 7: Add RED/GREEN repair tests**

Add a wound with a valid exact selector, then variants for wrong-owner, unknown, duplicate/confusable, not-offered, and final-disabled skill. Assert one repair packet, exact selector path, sanitized GM-safe context, original rejected proposal, no permanent IDs, and no carrier/index/history write.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundRepairPacketBuilderTests&FullyQualifiedName~SkillScope|FullyQualifiedName~WoundEffectBatchPlannerTests&FullyQualifiedName~SkillScope"
```

Expected: PASS; every selector authority error is repairable and atomic.

- [ ] **Step 8: Run planner GREEN and commit**

Run the Step 2 command again.

Expected: PASS with cache invalidation on catalog-only changes.

Before committing, add RED/GREEN tamper regressions for both retained-authority seals and a post-binding regression proving that a scalar `scope` override cannot bypass the closed structural union or allocate/publish state.

```powershell
git add BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs BookOfEternityClient/Services/AcceptedEffectBoundaryTranscript.cs BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs BookOfEternityClient/Services/EffectSourceDefinitionContract.cs BookOfEternityClient/Services/WoundAcceptedTurnPlan.cs BookOfEternityClient/Services/Validation/ValidationService.EffectMaterialization.cs BookOfEternityClient/Services/Validation/ValidationService.WoundMaterialization.cs BookOfEternityClient/Services/WoundResponseInputComposer.cs BookOfEternityClient/Services/WoundRepairPacketBuilder.cs BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs BookOfEternityClient.Tests/EffectAcceptedTurnPlanCacheTests.cs BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs BookOfEternityClient.Tests/WoundEffectBatchPlannerTests.cs BookOfEternityClient.Tests/WoundRepairPacketBuilderTests.cs
git commit -m "feat(effects): seal skill scope in accepted plans (#1536)"
```

---

### Task 5: Introduce the single scope-aware roll reducer

**Files:**
- Create: `BookOfEternityClient/Services/EffectRollContributionResolver.cs`
- Create: `BookOfEternityClient.Tests/EffectRollContributionResolverTests.cs`
- Modify: `BookOfEternityClient/Services/EffectMechanicsSnapshot.cs`

**Interfaces:**
- Consumes: accepted mechanical components and current `EffectRollSkillScopeAuthority`.
- Produces: `EffectRollContributionResolver.Resolve(EffectMechanicsSnapshot, EffectRollContext)` and immutable evidence.

- [ ] **Step 1: Write the RED reducer matrix**

Cover broad match, focused exact match, focused mismatch, null skill identity, dormant missing/inactive skill, exact restoration, similar-ID non-inheritance, malformed/ambiguous current authority fail-closed, wrong actor/realm/operation filtering, repeated advantages, repeated disadvantages, and advantage+disadvantage cancellation.

- [ ] **Step 2: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectRollContributionResolverTests"
```

Expected: FAIL to compile because the resolver types do not exist.

- [ ] **Step 3: Define the trusted context and result**

```csharp
internal sealed record EffectRollContext(
    string Realm,
    string ActorKind,
    string ActorId,
    string Operation,
    string? SkillId);

internal sealed record EffectRollContributionEvidence(
    string EffectId,
    string ComponentId,
    string Contribution);

internal sealed record EffectRollContributionResolution(
    bool IsValid,
    string RollMode,
    IReadOnlyList<EffectRollContributionEvidence> Contributions,
    IReadOnlyList<ValidationIssue> Issues);
```

- [ ] **Step 4: Implement filtering before unchanged reduction**

For every active `roll_modifier` component, require exact realm/target/operation first. Parse the structurally validated scope. `all` contributes. `skill` contributes only when `context.SkillId` is an ordinal match and `ResolveCurrent` is `Usable`; `Missing`/`Unavailable` skips as dormant; `InvalidAuthority` returns `IsValid=false` and no roll mode authority.

Reduce only accepted evidence:

```csharp
var hasAdvantage = accepted.Any(x => x.Contribution == "advantage");
var hasDisadvantage = accepted.Any(x => x.Contribution == "disadvantage");
var mode = hasAdvantage == hasDisadvantage
    ? "normal"
    : hasAdvantage ? "advantage" : "disadvantage";
```

- [ ] **Step 5: Retain current skill authority in mechanics snapshots**

Append `EffectRollSkillScopeAuthority? SkillScopeAuthority = null` to `EffectMechanicsInput`. Add an init-only `SkillScopeAuthority` property to the snapshot, defaulted to an empty current authority. In `LoadAsync`, read the three skill roots under the existing `PublicationReadQuiescence` lease and build an authority whose offered/current roots are identical. Do not acquire a second lease.

- [ ] **Step 6: Run GREEN and commit**

Run the Step 2 command.

Expected: PASS; dormancy is derived and cancellation semantics are unchanged.

```powershell
git add BookOfEternityClient/Services/EffectRollContributionResolver.cs BookOfEternityClient/Services/EffectMechanicsSnapshot.cs BookOfEternityClient.Tests/EffectRollContributionResolverTests.cs
git commit -m "feat(effects): centralize scoped roll reduction (#1536)"
```

---

### Task 6: Bind Mortal treatment rolls to the exact selected skill ID

**Files:**
- Create: `BookOfEternityClient/Services/EffectDetachedRollSourceAuthority.cs`
- Modify: `BookOfEternityClient/Services/EffectRollContributionResolver.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentAuthority.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentAcceptedCanonicalProjection.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentAcceptedStateAuthority.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentRequirementAuthorityBundle.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundProcedureCheckAuthority.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundProcedureCheckAuthority.FreshValidation.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentDetachedSealValidator.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs`
- Modify: `BookOfEternityClient.Tests/EffectRollContributionResolverTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedureAuthority.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.DetachedRequirementAuthority.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.PrerequisiteAuthorities.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ColdClaimRecovery.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.cs`

**Interfaces:**
- Consumes: accepted `EffectMechanicsSnapshot`, the exact canonical skill row selected by an existing `skill_tier` requirement, and the T170 contribution semantics.
- Produces: required sealed `EffectDetachedRollSourceAuthority`, nullable sealed `RollSkillId`, and one common live/fresh/detached reduction core; `fixed_zero` remains null and has no skill proof.
- Produces these exact common types:

```csharp
internal sealed class EffectDetachedRollSourceAuthority
{
    public int SchemaVersion { get; }
    public IReadOnlyList<EffectDetachedRollSourceRow> Rows { get; }
    public string AuthorityFingerprint { get; }

    internal static EffectDetachedRollSourceAuthority Create(
        IReadOnlyList<EffectDetachedRollSourceRow> rows);
    internal bool HasValidSeal(out IReadOnlyList<ValidationIssue> issues);
    internal bool SemanticallyEquals(EffectDetachedRollSourceAuthority other);
    internal EffectDetachedRollSourceAuthority CloneDetached();
}

internal sealed class EffectDetachedRollSourceRow
{
    public int Ordinal { get; }
    public string EffectId { get; }
    public string ComponentId { get; }
    public string Realm { get; }
    public string TargetKind { get; }
    public string TargetId { get; }
    public IReadOnlyList<string> Operations { get; }
    public string Contribution { get; }
    public string ScopeKind { get; }
    public string? ScopeSkillId { get; }

    internal static EffectDetachedRollSourceRow Create(
        int ordinal,
        string effectId,
        string componentId,
        string realm,
        string targetKind,
        string targetId,
        IReadOnlyList<string> operations,
        string contribution,
        string scopeKind,
        string? scopeSkillId);
    internal EffectDetachedRollSourceRow CloneDetached();
}

internal sealed record EffectRollSkillUsabilityProof(
    string Realm,
    string ActorKind,
    string ActorId,
    string SkillId);

internal sealed record EffectRollSourceCaptureResult(
    bool IsValid,
    EffectDetachedRollSourceAuthority? Authority,
    IReadOnlyList<ValidationIssue> Issues);
```

- `EffectRollContributionResolver` exposes `Capture(EffectMechanicsSnapshot)`, retains `Resolve(EffectMechanicsSnapshot, EffectRollContext)`, and adds source overloads for either trusted current `EffectRollSkillScopeAuthority` or detached `EffectRollSkillUsabilityProof?`; every resolve overload delegates to one private core.
- The exact resolver signatures are:

```csharp
internal static EffectRollSourceCaptureResult Capture(EffectMechanicsSnapshot snapshot);
internal static EffectRollContributionResolution Resolve(
    EffectMechanicsSnapshot snapshot,
    EffectRollContext context);
internal static EffectRollContributionResolution Resolve(
    EffectDetachedRollSourceAuthority source,
    EffectRollContext context,
    EffectRollSkillScopeAuthority currentSkills);
internal static EffectRollContributionResolution Resolve(
    EffectDetachedRollSourceAuthority source,
    EffectRollContext context,
    EffectRollSkillUsabilityProof? selectedSkill);
```

- `MortalWoundProcedureCheckAuthority` exposes required `public EffectDetachedRollSourceAuthority RollSourceAuthority { get; }` and carries it through both constructors, clone/restore, live fingerprint, detached fingerprint, and typed JSON.

The initial T171 pass at `887de325` already added `RollSkillId`, exact `SkillId`/`CapabilityRef` separation in canonical rows, live/fresh resolver calls, final skill roots, and the first Integration matrix. The following review correction is authoritative and completes the task.

- [x] **Step 1: Add RED pure normalized-source tests**

Add tests whose names and assertions cover this matrix:

```text
Capture_AllRollRowsBeforeContextFiltering_PreservesOrderAndMechanicalFieldsOnly
Capture_RejectedSnapshot_ReturnsItsDiagnosticsAndNoAuthority
Resolve_SnapshotAndDetachedAuthority_ProduceExactParity
Resolve_DetachedAuthority_FiltersForeignRealmActorOperationAndSkillInsideSharedCore
Resolve_DetachedAuthority_MalformedDuplicateOrConfusableRowsFailClosed
Authority_FingerprintChangesForEveryFieldOperationOrderAndNullSkillPosition
Authority_CloneIsDetachedAndSemanticallyEqual
```

Construct at least one accepted snapshot containing broad/focused rows for multiple actors,
realms, operations, and skill IDs. Assert that `Rows` retains every active
`roll_modifier`, its ordinal is contiguous, its operation order is exact, and serialized
authority text contains none of the fixture's display name, description, owner ID,
carrier field, or arbitrary non-roll payload sentinel.

- [x] **Step 2: Run the pure RED selection**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~EffectRollContributionResolverTests"
```

Expected: FAIL because the detached source authority and replay overload do not exist.

- [x] **Step 3: Implement the immutable normalized source authority**

Create the exact public-getter shape above with private JSON construction and production-only
creation. `SchemaVersion` is exactly `1`; cap `Rows` at `10_000`; require ordinals to equal
`0..Count-1`; require exact identifiers and exact/confusable uniqueness for each
`(EffectId, ComponentId)`; require one to six unique registered operations from exactly
`attack_roll|defense_roll|skill_check|saving_throw|damage_roll|initiative_roll`; accept only
`advantage|disadvantage`; enforce the exact closed `all|null` versus `skill|SkillId`
cross-field union. `AuthorityFingerprint` uses this exact canonical object, including
explicit JSON null for broad scope:

```csharp
new JsonObject
{
    ["schemaVersion"] = 1,
    ["rows"] = new JsonArray(rows.Select(row => (JsonNode?)new JsonObject
    {
        ["ordinal"] = row.Ordinal,
        ["effectId"] = row.EffectId,
        ["componentId"] = row.ComponentId,
        ["realm"] = row.Realm,
        ["targetKind"] = row.TargetKind,
        ["targetId"] = row.TargetId,
        ["operations"] = new JsonArray(row.Operations.Select(JsonValue.Create).ToArray()),
        ["contribution"] = row.Contribution,
        ["scopeKind"] = row.ScopeKind,
        ["scopeSkillId"] = row.ScopeSkillId
    }).ToArray())
};
```

Hash UTF-8 canonical JSON with SHA-256 using the existing effect-authority uppercase-hex
format. `HasValidSeal` independently validates every row before comparing the fingerprint;
`SemanticallyEquals` compares schema, row count/order, every scalar, every ordered operation,
nullable skill position, and both independently valid fingerprints. Constructors and clone
must freeze every input collection and allocate fresh row/operation instances.

- [x] **Step 4: Refactor the resolver to capture once and reduce through one core**

`Capture` rejects a non-accepted snapshot with the snapshot's issues. For an accepted
snapshot it visits every active `Profile == "roll_modifier"` component before any context
filter, parses the complete ordered operations/contribution/scope, and either returns one
valid authority or fails closed; it never silently skips a malformed roll row. It omits all
non-roll profiles and every narrative/owner/carrier/arbitrary payload field.

The snapshot overload captures and calls the source/current-skill-authority overload. That
overload converts the current scope-authority result into either one exact
`EffectRollSkillUsabilityProof`, no proof for missing/unavailable/null skill, or an invalid
resolution for ambiguous authority, then calls the same core as detached replay.
The core validates the complete normalized authority before filtering exact realm, target,
operation, and scope. A focused row contributes only when context and proof agree exactly on
realm/actor/skill. Reduction remains:

```csharp
var mode = hasAdvantage == hasDisadvantage
    ? "normal"
    : hasAdvantage ? "advantage" : "disadvantage";
```

Run the Step 2 command. Expected: PASS.

- [x] **Step 5: Add RED procedure persistence, detached replay, and trust-boundary tests**

Extend the existing `SkillScopedRoll` Integration matrix and typed-authority guards to prove:

```text
resolved_skill_tier broad/matching/other and fixed_zero broad/focused behavior remains exact
extension CapabilityRef != SkillId binds focused scope only by SkillId
case-only and Unicode-confusable RollSkillId tamper rejects after reseal
missing RollSourceAuthority or RollSkillId rejects under direct cutover
fixed_zero and opposing-contribution cancellation survive typed round trip
delete/add/change compact contribution, mode, or dice plus outer reseal rejects when source is unchanged
jointly changed source/result plus complete reseal reaches fresh mismatch, restores prior registries, and exposes no restored request/claim
procedure authority/clone/fingerprint changes for every source row field and operation order
serialized procedure authority contains no display/description/owner/carrier/full-payload sentinel
```

Add the extension identity fixture with both identities explicitly:

```csharp
new MortalWoundTreatmentAuthority.Skill(
    "skill_field_medicine_01",
    "field_medicine",
    "Field medicine",
    3,
    "active",
    true)
```

Run these RED controls separately:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&FullyQualifiedName~SkillScopedRoll"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&FullyQualifiedName~DetachedModeAuthority"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&FullyQualifiedName~ColdClaimRecovery"
```

Expected: new rows FAIL because the authority is not persisted and detached validation still
reduces only the submitted compact result.

- [x] **Step 6: Carry source authority through procedure creation, restore, and fresh validation**

At creation, call `Capture(acceptedState.EffectMechanics)`, fail when capture is invalid, and
resolve that captured authority with `acceptedState.EffectMechanics.SkillScopeAuthority`
before reserving dice. Persist the same required authority next to `RollSkillId`, clone it in
`AttachRestoredReservations`,
mark nullable `RollSkillId` with `[JsonRequired]` so an absent property differs from explicit
`null`, reject a null/missing source authority in the JSON constructor, and bind its
`AuthorityFingerprint` immediately after `RollSkillId` in both procedure
fingerprint implementations. Change the procedure fingerprint version from `"1"` to `"2"`;
there is no compatibility path for missing authority or the old domain.

Fresh validation must independently capture current accepted mechanics, require
`RollSourceAuthority.SemanticallyEquals(fresh.Authority)`, rerun the common source overload
using the fresh snapshot's current skill authority, and compare exact compact evidence, mode,
`RollSkillId`, and required dice shape. This check
must execute before a recovered authority can be returned. Keep the existing registry
transaction: any mismatch restores `previousDice`, `previousReactions`, and
`previousResources` before failure.

The shared Mortal treatment fixture is also part of this direct cutover: both its
materialized `roll_modifier` payload and the mirrored skill effect definition must carry
explicit broad `scope: { kind: "all" }`. This keeps existing procedure scenarios valid
without weakening production capture or postponing a required fixture correction.

- [x] **Step 7: Replace the detached local reducer with the common replay overload**

Delete `HasValidDetachedProcedureContributions`. Extend
`TryResolveDetachedProcedureModifier` to return nullable
`EffectRollSkillUsabilityProof`: fixed-zero returns null; resolved-skill requires the already
recursive requirement validation plus exact realm/actor/SkillId, current tier at least the
authored minimum, actor present/active/reachable/nonterminal, and skill active/nonterminal.
Call:

```csharp
var resolution = EffectRollContributionResolver.Resolve(
    value.RollSourceAuthority,
    new EffectRollContext(
        request.Coordinates.Realm,
        expectedActorKind!,
        expectedActorId!,
        "skill_check",
        expectedRollSkillId),
    skillProof);
```

Require valid resolution and exact ordered equality with compact contributions and mode,
then validate dice from the recomputed mode. Remove the five-argument `Skill` constructor;
keep native `(skillId, skillId, ...)` and extension `(skillId, CapabilityRef, ...)` calls
explicit. Preserve the already implemented final treatment skill after-images.

- [x] **Step 8: Run GREEN controls and commit the correction**

Run the Step 2 pure command and all three Step 5 Integration commands. If the coherent
`ColdClaimRecovery` selection exceeds five minutes, record measured wall time and rerun that
selection with `-TimeoutMinutes 15`; do not move it into Fast or reduce coverage.

Expected: all selected tests PASS; detached compact tamper fails semantically, joint reseal
fails at fresh canonical comparison, and all prior registries remain exactly observable.

```powershell
git add BookOfEternityClient/Services/EffectDetachedRollSourceAuthority.cs BookOfEternityClient/Services/EffectRollContributionResolver.cs BookOfEternityClient/Services/MortalWoundTreatmentAuthority.cs BookOfEternityClient/Services/MortalWoundTreatmentAcceptedCanonicalProjection.cs BookOfEternityClient/Services/MortalWoundTreatmentAcceptedStateAuthority.cs BookOfEternityClient/Services/MortalWoundTreatmentRequirementAuthorityBundle.cs BookOfEternityClient/Services/MortalWoundProcedureCheckAuthority.cs BookOfEternityClient/Services/MortalWoundProcedureCheckAuthority.FreshValidation.cs BookOfEternityClient/Services/MortalWoundTreatmentDetachedSealValidator.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs BookOfEternityClient.Tests/EffectRollContributionResolverTests.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedureAuthority.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.DetachedRequirementAuthority.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.PrerequisiteAuthorities.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ColdClaimRecovery.cs
git commit -m "fix(wounds): verify detached roll source authority (#1536)"
```

Closure (2026-09-05): implemented through `887de325`, `ba633ab1`, and
`c1f455ee`. Final Focused controls are pure 46/46, SkillScopedRoll 13/13,
DetachedModeAuthority 35/35, ColdClaimRecovery 20/20, and restored-source clone
1/1, all within the default five-minute limit with clean owned-process cleanup.
Independent final review found 0 Critical, 0 Important, and 0 Minor findings.

---

### Task 7: Project broad, focused, and dormant scopes safely

**Files:**
- Modify: `BookOfEternityClient/UI/EffectPlayerProjection.cs`
- Modify: `BookOfEternityClient.Tests/EffectPlayerProjectionTests.cs`

**Interfaces:**
- Consumes: `EffectMechanicsSnapshot.SkillScopeAuthority` and each component's accepted target coordinate.
- Produces: Russian readable scope text with no technical `skillId` leakage.

- [x] **Step 1: Add RED projection rows**

Assert exact output semantics for:

```text
all + skill_check + disadvantage -> "Помеха на все проверки навыков"
skill + usable exact row -> "Помеха на проверки навыка «Взлом»"
skill + inactive exact row -> same readable name plus "сейчас не действует"
skill + missing row -> "Помеха на проверки конкретного недоступного навыка — сейчас не действует"
hidden effect -> absent exactly as before
all outputs -> do not contain "skill_" or the actual skillId
```

- [x] **Step 2: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectPlayerProjectionTests&FullyQualifiedName~RollScope"
```

Expected: FAIL because projection currently renders only the operations array and contribution token.

- [x] **Step 3: Thread target and authority into component projection**

Pass the accepted `EffectTargetKey` and snapshot skill authority from `BuildEntry` into `ProjectComponent`. Replace the `roll_modifier` arm with `DescribeRollModifier(payload, target, skillAuthority)`.

`DescribeRollModifier` must translate contribution first, use `ResolveCurrent` only for `kind=skill`, never include the ID, and return neutral text when the row is missing. If resolution reports `InvalidAuthority`, return the same neutral inactive wording and add no gameplay authority from display code.

- [x] **Step 4: Run GREEN and commit**

Run the Step 2 command.

Expected: PASS; existing hidden-effect rows remain unchanged.

```powershell
git add BookOfEternityClient/UI/EffectPlayerProjection.cs BookOfEternityClient.Tests/EffectPlayerProjectionTests.cs
git commit -m "feat(ui): describe scoped roll effects safely (#1536)"
```

Closure (2026-09-05): implemented in `2bed5c4c` and privacy correction
`e33d8a32`. The original RED failed 0/5 on the absent scope-aware copy; the
expanded final RollScope contour passes 10/10 and the complete projection class
passes 46/46, with clean owned-process cleanup and no warnings, errors, timeouts,
or duplicate IDs. Independent final review found 0 Critical, 0 Important, and
0 Minor findings.

---

### Task 8: Supply the bounded selectable catalog to the GM

**Files:**
- Modify: `BookOfEternityClient/Models/TurnRequest.cs`
- Modify: `BookOfEternityClient/Services/LiveTurnPreparationService.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- Modify: `BookOfEternityClient/Services/EffectMechanicsSnapshot.cs`
- Modify: `BookOfEternityClient.IntegrationTests/EffectSkillScopeLifecycleTests.cs`

**Interfaces:**
- Consumes: pre-turn `EffectRollSkillScopeAuthority.CreateGmCatalog()`.
- Produces: `turn_request.json.effectSkillScopeCatalog`, advisory to the GM and never accepted as authorization.

- [x] **Step 1: Add RED request-staging tests in Integration**

Create player and nearby-NPC skills, stage one live-helper request and one ordinary GameEngine request, and assert the exact bounded catalog. Also assert idless/inactive/duplicate/confusable rows are omitted, afterlife-only direct requests contain `{schemaVersion:1,targets:[]}`, and editing the request catalog cannot authorize an invalid effect during accepted-state validation.

- [x] **Step 2: Run RED Integration selection**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectSkillScopeLifecycleTests&FullyQualifiedName~TurnRequestCatalog"
```

Expected: FAIL because `TurnRequest` has no catalog property.

- [x] **Step 3: Add the request property with an explicit empty default**

```csharp
[JsonPropertyName("effectSkillScopeCatalog")]
public JsonObject EffectSkillScopeCatalog { get; set; } = new()
{
    ["schemaVersion"] = 1,
    ["targets"] = new JsonArray()
};
```

The default guarantees a closed explicit empty catalog on afterlife-only or non-effect request routes.

- [x] **Step 4: Attach canonical catalogs at the two ordinary staging boundaries**

In `LiveTurnPreparationService.PrepareBoundAsync`, build from the same `writeLease` before the pending snapshot is hashed. In `GameEngine.ProcessPlayerTurn`, load the current catalog immediately before `CreateCanonicalBaselineSnapshotAsync` and assign a detached `JsonObject`. Acceptance must continue recomputing authority from signed canonical roots; it must never trust `TurnRequest.EffectSkillScopeCatalog`.

- [x] **Step 5: Run GREEN and commit**

Run the Step 2 command.

Expected: PASS for live-helper, ordinary, empty-afterlife, filtering, and tamper non-authority rows.

```powershell
git add BookOfEternityClient/Models/TurnRequest.cs BookOfEternityClient/Services/LiveTurnPreparationService.cs BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs BookOfEternityClient.IntegrationTests/EffectSkillScopeLifecycleTests.cs
git commit -m "feat(gm): expose selectable effect skill scopes (#1536)"
```

Closure (2026-09-05): implemented in `6b511724` and review correction
`ba1d8f31`; the latter isolates catalog loading to the three canonical skill
roots so unrelated malformed effect carriers cannot erase valid choices, while
a malformed skill root fails closed to an explicit empty catalog. Initial
request-staging RED was 1/4 (`20260905-141703-069-18520-1180617fd3e74fce9471b2b90a2e549e-focused`);
the isolation regression then produced the intended 4/5 RED
(`20260905-144710-065-19280-4d6440f17ad041ec886522d94ab273fd-focused`).
Final Integration evidence is 6/6
(`20260905-145410-716-40120-da6b7f88ee13406082f2498a272e8e2e-focused`),
with adjacent snapshot/resolver 93/93 and live-preparation 5/5 controls, all
warning/error/timeout/duplicate-free. The tamper row crosses the real validated
pending-snapshot and accepted-state boundary and proves byte-exact zero
publication plus no retained handoff. Independent final review found 0
Critical, 0 Important, and 0 Minor findings.

---

### Task 9: Complete direct-cutover fixtures and wound slot accounting

**Files:**
- Modify: `specs/1536-complete-wound-materialization/spec.md`
- Modify: `BookOfEternityClient.Tests/WoundConsequenceEnvelopeTests.cs`
- Modify: `BookOfEternityClient.Tests/WoundMaterializationContractTests.cs`
- Modify: `BookOfEternityClient.Tests/WoundContractTestData.cs`
- Modify: `BookOfEternityClient.Tests/WoundEffectBatchPlannerTests.cs`
- Modify: `BookOfEternityClient.Tests/MortalWoundTreatmentSeverityReductionPlannerTests.cs`
- Modify: `BookOfEternityClient.Tests/EffectPlayerProjectionTests.cs`
- Modify: `BookOfEternityClient.Tests/EffectRollContributionResolverTests.cs`
- Modify: `BookOfEternityClient.Tests/EffectRollSkillScopeAuthorityTests.cs`
- Modify: `BookOfEternityClient.Tests/EffectMaterializationSourceGuardTests.cs`
- Modify: `BookOfEternityClient.Tests/WoundMaterializationSourceGuardTests.cs`
- Modify: `BookOfEternityClient.TestSupport/EffectMaterializationTestFixture.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectRollModifierFixtureInventoryTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedureAuthority.cs`
- Modify: `BookOfEternityClient/Services/WoundConsequenceEnvelopeCatalog.cs`
- Modify: `BookOfEternityClient/Services/WoundMaterializationContract.cs`
- Modify: `BookOfEternityClient/Services/WoundPersistedConsequenceEnvelopeAdapter.cs`
- Modify: `Examples/E_CLI_Effect_Materialization.txt` (schema cutover only; expanded GM guidance remains Task 11)
- Modify: `Examples/E_CLI_Afterlife_Turns.txt` (schema cutover only; expanded GM guidance remains Task 11)
- Modify: every active test/source fixture returned by `rg -l 'roll_modifier' BookOfEternityClient.Tests BookOfEternityClient.IntegrationTests BookOfEternityClient.TestSupport`

**Interfaces:**
- Consumes: explicit structural scope and existing wound consequence power rules.
- Produces: no old two-field payloads in executable data; one whole `roll_modifier`
  component consumes one slot regardless of its operation list; scope is identity
  metadata, not a new slot or numeric power input.

- [x] **Step 1: Add RED slot and rematerialization rows**

Assert that one broad multi-operation component and one focused component each bind
exactly one slot. Keep duplicate-coordinate validation separate from slot counting: the
closed applicability coordinate is `(operation, scope.kind, exact skillId when focused)`,
so two focused components for two different skills have distinct coordinates and require
two slots, while a repeated component for the same operation and selector is rejected as
a duplicate. Assert that changing `skillId` changes the wound/effect semantic fingerprint.
Assert retained consequence rematerialization preserves the selector byte-for-byte while
runtime effect and transition IDs remain fresh and the stable source-definition
`componentId` remains unchanged through materialization.

- [x] **Step 2: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundConsequenceEnvelopeTests&FullyQualifiedName~SkillScope|FullyQualifiedName~WoundEffectBatchPlannerTests&FullyQualifiedName~SkillScope|FullyQualifiedName~WoundMaterializationContractTests&FullyQualifiedName~RollComponent|FullyQualifiedName~MortalWoundTreatmentSeverityReductionPlannerTests&FullyQualifiedName~SkillScopedRoll"
```

Expected: at least one assertion FAIL until scope is explicitly accounted for and preserved.

- [x] **Step 3: Keep scope out of power and slot expansion**

Where the three wound services inspect `roll_modifier`, continue counting the component once and reading only registered `operations`/`contribution` for power classification. Parse or clone `scope` as semantic payload, but do not iterate its fields as consequences and do not add a second slot for `skillId`.

- [x] **Step 4: Convert every executable payload**

Run:

```powershell
rg -n -C 5 'roll_modifier' BookOfEternityClient.Tests BookOfEternityClient.IntegrationTests BookOfEternityClient.TestSupport
```

For intentionally broad existing cases add:

```csharp
["scope"] = new JsonObject { ["kind"] = "all" }
```

Only existing or new intentionally focused cases use `kind=skill`. Do not change expected
mechanics of unrelated cases. Also cut over the imported executable profile matrix in
`Examples/E_CLI_Effect_Materialization.txt` and the executable modifier in
`Examples/E_CLI_Afterlife_Turns.txt`; Task 11 still owns their expanded narrative guidance
and the remaining synchronized examples.

- [x] **Step 5: Prove no executable legacy payload remains**

Route positive C# fixture payload construction through one closed helper, validate both
helper variants semantically, and use the Integration project's existing Roslyn boundary
to inventory all three executable test projects so a new direct constructor, JSON-literal
constructor, or unlisted missing-scope mutation fails the guard regardless of equivalent
C# syntax. Parse the two
Task-9-owned executable examples and fail when a `roll_modifier` lacks `scope`. Do not
use a regex-only assertion as the semantic proof. Task 11 remains responsible for the
complete synchronized GM-documentation/example guard set.

- [x] **Step 6: Run GREEN and commit**

Run the Step 2 command and focused source guard.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectMaterializationSourceGuardTests|FullyQualifiedName~WoundMaterializationSourceGuardTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectRollModifierFixtureInventoryTests|FullyQualifiedName~IntegrationTestBoundaryTests.FileBackedRegressionIntegrationSources_MatchReviewedManifest"
```

Expected: PASS; one component equals one slot and all executable payloads use the new schema.

```powershell
git add -- specs/1536-complete-wound-materialization/spec.md specs/1536-complete-wound-materialization/tasks.md docs/superpowers/plans/2026-09-05-roll-modifier-skill-scope.md BookOfEternityClient/Services/WoundConsequenceEnvelopeCatalog.cs BookOfEternityClient/Services/WoundMaterializationContract.cs BookOfEternityClient/Services/WoundPersistedConsequenceEnvelopeAdapter.cs BookOfEternityClient.TestSupport/EffectMaterializationTestFixture.cs BookOfEternityClient.Tests/WoundConsequenceEnvelopeTests.cs BookOfEternityClient.Tests/WoundMaterializationContractTests.cs BookOfEternityClient.Tests/WoundContractTestData.cs BookOfEternityClient.Tests/WoundEffectBatchPlannerTests.cs BookOfEternityClient.Tests/MortalWoundTreatmentSeverityReductionPlannerTests.cs BookOfEternityClient.Tests/EffectPlayerProjectionTests.cs BookOfEternityClient.Tests/EffectRollContributionResolverTests.cs BookOfEternityClient.Tests/EffectRollSkillScopeAuthorityTests.cs BookOfEternityClient.Tests/EffectMaterializationSourceGuardTests.cs BookOfEternityClient.Tests/WoundMaterializationSourceGuardTests.cs BookOfEternityClient.IntegrationTests/EffectRollModifierFixtureInventoryTests.cs BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedureAuthority.cs Examples/E_CLI_Effect_Materialization.txt Examples/E_CLI_Afterlife_Turns.txt
git commit -m "test(effects): cut fixtures to explicit roll scope (#1536)"
```

Before committing, verify `git diff --cached --name-only` contains only the intended tracked files and no unrelated path.

Task 9 completion evidence: the complete affected pure selection passed 674/674
(`20260905-163006-052-14080-38da855683794e5591e3d3e592f72538-focused`),
the focused treatment scope selection passed 13/13
(`20260905-163042-912-14228-d126603ccdcb4eb5a50e4c135020a41d-focused`),
the final semantic source guards passed 10/10
(`20260905-165252-089-55152-f76dec3b99714c86b0d3773710c088d1-focused`),
and the Roslyn Integration inventory plus boundary control passed 3/3
(`20260905-164943-834-49448-5f5eb6b907a544bcacf4c94d6b2c53f9-focused`).
Every build was warning/error-free with complete lane cleanup. Independent final review
found 0 Critical, 0 Important, and 0 Minor findings and marked the slice Ready.

---

### Task 10: Prove lifecycle, dormancy, replay, and rollback in Integration

**Files:**
- Modify: `BookOfEternityClient/Services/WoundResponseInputComposer.cs`
- Modify: `BookOfEternityClient.Tests/WoundRepairPacketBuilderTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/EffectSkillScopeLifecycleTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedureAuthority.cs`

**Interfaces:**
- Consumes: complete structural, planning, snapshot, resolver, treatment, and projection implementation.
- Produces: file-backed evidence at the correct non-Fast boundary.

- [x] **Step 1: Add ordinary and wound materialization lifecycle cases**

Use real canonical files and pending snapshots to prove ordinary source and wound source applications both publish exact scope, carrier/index agreement, and changed-selector rejection. Include wrong-owner and final same-turn disable cases; assert zero partial publication.

If the real wound path exposes a mismatch between GM-local `definitionRef` values and their canonical response-local identifiers, repair that mapping in `WoundResponseInputComposer` and cover it in `WoundRepairPacketBuilderTests`; do not weaken exact decision-authority or definition-key matching.

- [x] **Step 2: Add dormancy/reactivation cases**

Materialize an exact-skill modifier, then across later accepted states: remove the skill, add a similar/confusable different ID, and restore the original permanent ID. Assert contribution sequence `active -> dormant -> dormant -> active`, while effect ID, wound ID, severity, history, and lifetime are unchanged by availability alone.

- [x] **Step 3: Add restart/replay/cache/rollback cases**

Restart services between materialization and resolution. Prove exact replay accepts the sealed selector, changing only `kind` or `skillId` is not exact replay, cache input changes with skill authority, and a forced publication failure restores carriers/index/wounds/history/output without a partially rebound component.

- [x] **Step 4: Register Integration ownership**

Add exact source path `EffectSkillScopeLifecycleTests.cs` to `IntegrationTestBoundaryTests.RegressionIntegrationSources` and keep `[Trait("Category", "RegressionIntegration")]` at class level. Add no file-backed row to `BookOfEternityClient.Tests`.

- [x] **Step 5: Run the coherent Integration class**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectSkillScopeLifecycleTests"
```

Expected: PASS. If measured beyond five minutes, record the first run and repeat with `-TimeoutMinutes 15`; do not split one lifecycle assertion merely to game the limit.

- [x] **Step 6: Run focused treatment scope lifecycle rows**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&FullyQualifiedName~SkillScopedRoll" -TimeoutMinutes 15
```

Expected: PASS with exact replay and publication evidence.

- [x] **Step 7: Commit Integration evidence**

```powershell
git add -- docs/superpowers/plans/2026-09-05-roll-modifier-skill-scope.md specs/1536-complete-wound-materialization/tasks.md BookOfEternityClient/Services/WoundResponseInputComposer.cs BookOfEternityClient.Tests/WoundRepairPacketBuilderTests.cs BookOfEternityClient.IntegrationTests/EffectSkillScopeLifecycleTests.cs BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedureAuthority.cs
git commit -m "test(effects): prove skill scope lifecycle (#1536)"
```

Task 10 completion evidence (verified 2026-09-06): lifecycle 16/16 in 1:19.658
(`20260905-185327-418-29756-57f34ed39be740db8aad93040c34acc2-focused`),
treatment SkillScopedRoll 14/14 in 0:57.475
(`20260905-185635-191-21872-60a477632d4d4f1fb44d50e7a70130f7-focused`),
repair packets 31/31
(`20260905-185751-161-26656-ed7e0a39f84b4fac98f2311381441c5c-focused`),
and Integration manifest 1/1
(`20260905-185837-769-57600-cd9b3a9c81864fb79e8fc4bd6a61b570-focused`).
All builds had zero warnings/errors; no timeout/duplicate IDs and complete cleanup.
The real wound path exposed and repaired raw-to-canonical definition-ref mapping.
Negative wound cases exercise exact ownership, final disable, and changed selectors;
rollback fails after physical wound-history publication and restores every before-image.
Independent final review found zero Critical/Important/Minor findings and marked T175 Ready.
GM guide/example synchronization continues in T176 before feature completion.

---

### Task 11: Synchronize GM contracts, examples, manifests, and guards

**Files:**
- Modify: `OtherGuides/Effect_Materialization_Contract.md`
- Modify: `OtherGuides/Wound_Materialization_Contract.md`
- Modify: `Examples/E_CLI_Effect_Materialization.txt`
- Modify: `Examples/E_CLI_Wound_Materialization.txt`
- Modify: `Examples/E_CLI_Afterlife_Turns.txt`
- Modify: `Examples/example_validation_manifest.json`
- Modify: `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`
- Modify: `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs`
- Modify: `BookOfEternityClient.Tests/EffectMaterializationSourceGuardTests.cs`
- Modify: `BookOfEternityClient.Tests/WoundMaterializationSourceGuardTests.cs`
- Modify: `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.Wounds.cs`
- Inspect, modify only if direct wording is required: `BookOfEternityClient/game_master_daemon.ps1`
- Inspect, modify only if the afterlife matrix states the common payload shape: `OtherGuides/Afterlife_Contract_Matrix.md`

**Interfaces:**
- Consumes: final scope-aware schema and the already-uncommitted approved Task 7 wound documentation changes.
- Produces: one GM-authorable, executable, Mortal/afterlife-synchronized contract with source guards.

- [ ] **Step 1: Turn documentation expectations RED**

Add guards requiring all of these literal concepts:

```text
effectSkillScopeCatalog
scope.kind=all
scope.kind=skill
skillId must come from the exact target row
one focused component consumes one consequence slot
same-response new skills are not selectable
later missing skill makes the component inactive without healing/removing the wound
```

Require examples for a broad physical wound, an exact-skill physical wound, and a non-wound exact-skill effect. Require player-facing example text without a technical ID.

- [ ] **Step 2: Run RED documentation guards**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests&FullyQualifiedName~RollScope|FullyQualifiedName~AfterlifeDocumentationCoverageTests&FullyQualifiedName~RollScope"
```

Expected: FAIL because current guides/examples teach the old two-field payload.

- [ ] **Step 3: Update the two authoritative GM guides**

Document the exact JSON union, cross-field restrictions, target catalog, repair behavior, slot accounting, dormancy, and no-fuzzy/no-name/no-multiple-ID rules. State explicitly that physical wounds remain free GM-authored setting-specific entities; this selector does not introduce a wound catalog.

- [ ] **Step 4: Update all three examples and manifest**

Convert existing broad modifiers to `scope:{"kind":"all"}`. Add one Mortal wound using `skill_lockpicking` selected from `effectSkillScopeCatalog`, and one ordinary curse/Fate/structural source using the same profile to prove it is common. Keep afterlife broad unless a canonical Mortal skill target is actually present; do not reinterpret spiritual arts as skills. Register every changed example in `example_validation_manifest.json`.

- [ ] **Step 5: Record prompt-entrypoint rationale**

Verify that `BookOfEternityClient/game_master_daemon.ps1` already loads both authoritative guides and examples into the GM context pack. If those entrypoints remain unchanged, record in the PR summary: “No daemon path change: existing mandatory context-pack entries load the updated effect/wound guides and examples.” If the prompt contains inline old schema wording, replace only that wording and add a source guard.

- [ ] **Step 6: Run GREEN documentation guards**

Run the Step 2 command and:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests&FullyQualifiedName~Effect|FullyQualifiedName~ExampleDocumentationValidationTests&FullyQualifiedName~Wound"
```

Expected: PASS; examples parse and the GM/player contract contains no legacy payload.

- [ ] **Step 7: Commit the synchronized documentation**

```powershell
git add OtherGuides/Effect_Materialization_Contract.md OtherGuides/Wound_Materialization_Contract.md Examples/E_CLI_Effect_Materialization.txt Examples/E_CLI_Wound_Materialization.txt Examples/E_CLI_Afterlife_Turns.txt Examples/example_validation_manifest.json BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs BookOfEternityClient.Tests/EffectMaterializationSourceGuardTests.cs BookOfEternityClient.Tests/WoundMaterializationSourceGuardTests.cs BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs
git add BookOfEternityClient/game_master_daemon.ps1 OtherGuides/Afterlife_Contract_Matrix.md
git commit -m "docs(effects): teach scoped roll modifiers (#1536)"
```

Before committing, unstage either inspected optional file if it has no diff. Preserve and incorporate the existing Task 7 edits; never overwrite them wholesale.

---

### Task 12: Complete verification and review

**Files:**
- Modify only for corrections found by the checks below.

**Interfaces:**
- Consumes: Tasks 1–11.
- Produces: review-ready #1536 branch evidence without pushing, opening a PR, merging, or closing the issue.

- [ ] **Step 1: Run semantic legacy scans**

```powershell
rg -n -C 4 'roll_modifier' BookOfEternityClient BookOfEternityClient.Tests BookOfEternityClient.IntegrationTests BookOfEternityClient.TestSupport Examples OtherGuides specs/1535-complete-effect-materialization specs/1536-complete-wound-materialization
rg -n 'implicit.*scope|missing.*scope.*all|skillName.*scope|multiple.*skillId' OtherGuides Examples specs/1535-complete-effect-materialization specs/1536-complete-wound-materialization
```

Expected: all active payloads have explicit scope; no text authorizes implicit broad scope, name selection, or multiple IDs.

- [ ] **Step 2: Run one meaningful Fast checkpoint**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Expected: exit `0`, no timeout, no duplicate IDs, complete owned-tree cleanup, and all discovered Fast tests pass inside five minutes.

- [ ] **Step 3: Run required documentation validation**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
```

Expected: both controls pass; FullValidation is required here because shared GM examples and the afterlife documentation boundary changed.

- [ ] **Step 4: Run the related regression integration lane if focused lifecycle evidence exposed broader coupling**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane RegressionIntegration
```

Expected: PASS. Skip this broad diagnostic only when the exact Task 10 selections plus FullValidation already cover every changed file-backed boundary; record that evidence explicitly.

- [ ] **Step 5: Inspect diff and request code review**

Invoke `requesting-code-review`. Review the complete diff from `6bf45c38` through `HEAD`, with special attention to authority trust boundaries, offered/current separation, `RollSkillId` replay sealing, repair coordinates, hidden-effect projection, mutable JSON detachment, and Integration/Fast placement. Fix every confirmed finding and rerun its smallest owning Focused selection.

- [ ] **Step 6: Run formatting/status safety checks**

```powershell
git diff --check
git status --short
git log --oneline --decorate -15
```

Expected: no whitespace errors; only intentional changes are tracked; `.serena/` remains untracked and untouched.

- [ ] **Step 7: Run PreMerge only at the actual merge boundary**

When the user requests push/PR/merge, invoke `verification-before-completion`, then run exactly once:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Expected: exit `0`, no timeout, zero failures, zero duplicate IDs, complete owned-tree cleanup. Do not run an adjacent duplicate Fast control because PreMerge already includes the full Fast project.

- [ ] **Step 8: Commit any review-only corrections**

Stage only the exact corrected files, verify `git diff --cached --name-only`, and use:

```powershell
git commit -m "fix(effects): harden scoped roll authority (#1536)"
```

If review required no correction, create no empty commit. Stop with local branch evidence; pushing, PR creation, merge, and issue closure require a separate explicit user request.
