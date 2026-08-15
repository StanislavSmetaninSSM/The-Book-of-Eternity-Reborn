# Complete Effect Materialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make every durable active effect in the Mortal World and afterlife a complete, source-authorized, client-identified runtime instance with deterministic mechanics, stacking, lifetime, rollback, and one safe player projection.

**Architecture:** Static `combatEffect`, `structuredBonuses`, Fate Card effects, wound consequences, skills, arts, items, quests, locations, factions, events, hazards, and combat actions remain source templates. The GM submits only transient `effectChanges[]` and bounded `effectResolutionReceipts[]`. One cached accepted-turn plan resolves exact source and target authority, allocates random client-owned identities once, advances stacking/lifetime/triggers, and produces complete owner-carrier, identity-index, pending-resolution, mechanics-snapshot, and player-projection after-images. The effect normalizer runs after every source-owning normalizer inside the existing accepted transaction. Afterlife profiles carry persistent actor effects; spiritual `combatConditions[]` keep their specialized fields through one shared identity/lifecycle adapter. No runtime migration or compatibility reader is added.

**Tech Stack:** C# 12, .NET 8, `System.Text.Json` / `JsonNode`, existing `ValidationService`, `CanonicalStateNormalizer`, `FileSystemManager.CanonicalWriteLease`, accepted-turn snapshot/repair/rollback services, Spectre.Console, browser C# DTO builders, xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1, PowerShell 7 bounded test lanes, and the existing React/Vite frontend only if a typed DTO boundary actually changes.

## Global Constraints

- Source task is GitHub issue [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535). Keep every code, test, fixture, prompt, example, and contract change traceable to it.
- Effect Task 9 trigger/resource execution is blocked by [#1543](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543) and its approved [unified resource authority design](../specs/2026-08-15-unified-resource-authority-design.md). Do not add effect-only legacy field adapters; resume Task 9 only after the canonical ledger and accepted mechanics planner are complete.
- Work only in `E:\Games\worktrees\boe-1535-effect-materialization` on branch `1535-effect-materialization`. Preserve the user's dirty main worktree and unrelated generated `bin/obj` files.
- The game is pre-release. Missing pristine effect roots may initialize empty; any non-empty legacy carrier is invalid. Do not add migration, promotion, compatibility, or fallback readers.
- Do not create, enable, or run GitHub Actions. Verification is local through `pwsh -NoProfile -File .\scripts\test-csharp.ps1`.
- Static definitions never become active merely by existing. Only an accepted `apply` operation can create a runtime instance.
- The GM owns source-authored semantic definitions and bounded story-facing inputs. The client owns permanent effect/combatant/transition/receipt identities, identity history, resolved stack/lifetime state, pending requests, mechanics snapshots, and repair bounds.
- Compare operational identities with `StringComparer.Ordinal`; case, whitespace, Unicode-normalization, confusable, stale, historical, name-based, index-based, and cross-realm aliases never select authority.
- A logical active effect has one owner carrier and one active identity-index entry. Terminal identities remain immutable history and are never reused or reactivated.
- Mechanics are all-or-nothing: one malformed active carrier, component, link, or index relationship contributes zero active-effect mechanics for that governed snapshot.
- Five stack policies and eight lifetime modes are closed catalogs. Prose, magic values such as `999`, and unknown extension behavior never become mechanics.
- Removing, dispelling, suspending, or expiring a wound-derived effect never heals, deletes, or mutates the wound. Wound treatment remains separate authority.
- Shining blessing entitlement is not a generic active-effect carrier. Spiritual `combatConditions[]` are adapted, not duplicated or flattened.
- Every GM-affecting code change must update rules, guides, examples, manifest entries, daemon reminders, and documentation/source guards in the same slice.
- During implementation run the smallest Focused filter first, one meaningful Fast checkpoint, conditional FullValidation and LifecycleIntegration for their changed boundaries, and exactly one final PreMerge without a duplicate final Fast.
- Do not mark Spec Kit tasks complete from an agent report alone. Inspect the diff and the exact `summary.json` evidence first.

## Baseline Evidence

The isolated worktree initially lacked generated NuGet assets. After restoring the two test projects, the unchanged feature base passed local Fast:

| Lane | Result directory | Total | Passed | Failed | Duplicates | Timed out | Cleanup | Wall time |
| --- | --- | ---: | ---: | ---: | ---: | --- | --- | --- |
| `Fast` | `TestResults/test-lanes/20260814-155111-688-51532-c209acdb419149cd906a66a3c36a0158-fast` | 3172 | 3172 | 0 | 0 | no | complete | `00:04:56.3252288` |
| `Focused` fixture RED | `TestResults/test-lanes/20260814-210058-504-47372-b5dd4f879e17400c88cb885d90c7a3e8-focused` | 0 | 0 | build RED | 0 | no | complete | `00:00:50.9158022` |
| `Focused` fixture GREEN | `TestResults/test-lanes/20260814-210323-432-29280-c4c4f652d51d45a5a114e5cec7d042e6-focused` | 6 | 6 | 0 | 0 | no | complete | `00:00:31.7565685` |
| `Focused Integration` context RED | `TestResults/test-lanes/20260814-210502-155-22620-230144b9623a420ba278102cb6dead11-focused` | 0 | 0 | build RED | 0 | no | complete | `00:00:21.1074697` |
| `Focused Integration` context GREEN | `TestResults/test-lanes/20260814-210622-008-44200-76e97ad1b8894f91aa6d898ce20d779b-focused` | 4 | 4 | 0 | 0 | no | complete | `00:00:34.2071216` |
| `Focused` effect-contract RED | `TestResults/test-lanes/20260814-211506-098-19088-f1600182c52a4dbaa9304b85ab610865-focused` | 0 | 0 | build RED | 0 | no | complete | `00:00:47.8860037` |
| `Focused` effect-contract GREEN | `TestResults/test-lanes/20260814-213329-791-14516-ea2ec87d69c34244abc572e63d27a488-focused` | 101 | 101 | 0 | 0 | no | complete | `00:00:25.0347077` |
| `Focused` identity/carrier RED | `TestResults/test-lanes/20260814-213825-580-54224-e84a066a3091429a871a5042a972bb77-focused` | 0 | 0 | build RED | 0 | no | complete | `00:00:31.0908359` |
| `Focused` identity/carrier GREEN | `TestResults/test-lanes/20260814-214311-977-51088-25c1f82d24804eb8904e1a572b837aee-focused` | 26 | 26 | 0 | 0 | no | complete | `00:00:28.5068719` |
| `Focused` foundational control | `TestResults/test-lanes/20260814-214356-410-20460-bf20e324700d4ccc900a8843dfb0714a-focused` | 127 | 127 | 0 | 0 | no | complete | `00:00:11.7326069` |
| `Focused` source/target/cache RED | `TestResults/test-lanes/20260814-214808-735-19056-ca2223a834824bc9903366e513ed7bfe-focused` | 0 | 0 | build RED | 0 | no | complete | `00:00:16.9791643` |
| `Focused` source/target/cache GREEN | `TestResults/test-lanes/20260814-215457-332-5996-1e7b370e730347d7af3f2cc1ab9c4899-focused` | 48 | 48 | 0 | 0 | no | complete | `00:00:28.3723791` |
| `Focused` same-turn target boundary RED | `TestResults/test-lanes/20260814-220355-174-20284-cb940c85a8a24a96a19a2f8f6ca7b3fe-focused` | 22 | 18 | 4 | 0 | no | complete | `00:00:15.2956292` |
| `Focused` same-turn target boundary GREEN | `TestResults/test-lanes/20260814-220445-223-56300-a835a997224c4f4abd40679daa0498be-focused` | 22 | 22 | 0 | 0 | no | complete | `00:00:28.9805420` |
| `Focused` immutable authority RED | `TestResults/test-lanes/20260814-220702-246-52480-89537691dbdc455fa3531fb1551c29f9-focused` | 55 | 52 | 3 | 0 | no | complete | `00:00:15.3208184` |
| `Focused` immutable authority GREEN | `TestResults/test-lanes/20260814-220738-267-20256-a651fffc9d534be1a88065d1acebcc17-focused` | 55 | 55 | 0 | 0 | no | complete | `00:00:27.9265826` |
| `Focused` combined authority control | `TestResults/test-lanes/20260814-220814-083-15604-35e8f01e875947b38cd13d7f5b5211cd-focused` | 176 | 176 | 0 | 0 | no | complete | `00:00:11.9360379` |
| `Fast` foundational phase checkpoint | `TestResults/test-lanes/20260814-215832-804-30660-300e5200e2064f7bbca598f766eb6058-fast` | 3347 | 3347 | 0 | 0 | no | complete | `00:02:46.4211170` |
| `Focused` pure apply planner RED | `TestResults/test-lanes/20260814-221442-928-23352-6c3d2c4e6a234f6c92c8fec7fe45f161-focused` | 0 | 0 | build RED | 0 | no | complete | `00:00:20.1025977` |
| `Focused` pure apply planner GREEN | `TestResults/test-lanes/20260814-222548-108-3708-2d0a52aa9f104e659efc5b047186510e-focused` | 27 | 27 | 0 | 0 | no | complete | `00:00:15.4131956` |
| `Focused` closed transient staging RED | `TestResults/test-lanes/20260814-222205-619-18488-c84d2d50fbfa48e487f61ed97f20e75e-focused` | 1 | 0 | 1 | 0 | no | complete | `00:00:15.1502048` |
| `Focused` closed transient staging GREEN | `TestResults/test-lanes/20260814-222240-514-40488-80d7bd8118b2446a8478ab15a88fff7e-focused` | 1 | 1 | 0 | 0 | no | complete | `00:00:24.8983156` |
| `Focused` accepted-event binding RED | `TestResults/test-lanes/20260814-223158-481-29252-c98d73e7d6814d2d84660866cc34217d-focused` | 31 | 29 | 2 | 0 | no | complete | `00:00:15.1191792` |
| `Focused` accepted-event binding GREEN | `TestResults/test-lanes/20260814-223318-864-27964-98bd016b6a5145e78f7d4407f6c78cf9-focused` | 31 | 31 | 0 | 0 | no | complete | `00:00:29.1165812` |
| `Focused` cached JSON immutability RED | `TestResults/test-lanes/20260814-223515-805-22356-c856c1031d644a419250c5ddf4ee0b8c-focused` | 1 | 0 | 1 | 0 | no | complete | `00:00:14.6559935` |
| `Focused` cached JSON immutability GREEN | `TestResults/test-lanes/20260814-223550-119-46708-98fa3e20173f47d2bd324a48fec24b83-focused` | 1 | 1 | 0 | 0 | no | complete | `00:00:28.1040361` |
| `Focused` multi-event catalog RED | `TestResults/test-lanes/20260814-223937-577-40996-ecd5710492de4afab9ae09e1d5732fac-focused` | 1 | 0 | 1 | 0 | no | complete | `00:00:14.8930462` |
| `Focused` multi-event catalog GREEN | `TestResults/test-lanes/20260814-224059-722-52852-7b4f96066a5d44efba32fe43d803f8e0-focused` | 1 | 1 | 0 | 0 | no | complete | `00:00:29.3001222` |
| `Focused` Task 5 final combined control | `TestResults/test-lanes/20260814-224221-441-33532-ad147e4676194f568d6601290d05a6ec-focused` | 211 | 211 | 0 | 0 | no | complete | `00:00:27.1128659` |
| `Focused` Integration compile/context control | `TestResults/test-lanes/20260814-224351-103-14004-05203a0578e9419e8fc6d34c6cc08a62-focused` | 4 | 4 | 0 | 0 | no | complete | `00:00:34.7856305` |
| `Focused` explicit export boundary RED | `TestResults/test-lanes/20260815-004112-462-55940-a47a252ce01a479984366d67c66a8ae9-focused` | 2 | 0 | 2 | 0 | no | complete | `00:00:40.2088627` |
| `Focused` explicit export boundary GREEN | `TestResults/test-lanes/20260815-004433-206-18488-319db491335f4be6b048a58585315b86-focused` | 2 | 2 | 0 | 0 | no | complete | `00:01:09.2652325` |
| `Focused` canonical publication control | `TestResults/test-lanes/20260815-005735-444-13396-c11397c44c164f36ba6eccd9ae107339-focused` | 8 | 8 | 0 | 0 | no | complete | `00:00:38.4929828` |
| `Focused` raw/composed validation control | `TestResults/test-lanes/20260815-005822-150-51972-18f2208b4f1c4449b6e8531dd75fe95d-focused` | 37 | 37 | 0 | 0 | no | complete | `00:00:39.1469037` |
| `Focused` authority/planner/item control | `TestResults/test-lanes/20260815-005910-154-41368-c3617723ad10477bb871447b0e6a329e-focused` | 155 | 155 | 0 | 0 | no | complete | `00:00:23.6709005` |
| `Focused` phase-capacity RED | `TestResults/test-lanes/20260815-010449-286-52284-76c278ebe12d459d8d24b967fb6466a8-focused` | 28 | 27 | 1 | 0 | no | complete | `00:00:16.4513424` |
| `Focused` phase-capacity GREEN | `TestResults/test-lanes/20260815-010554-687-48404-9d0a13b5632a41318751f496cbd2a966-focused` | 28 | 28 | 0 | 0 | no | complete | `00:01:01.0768762` |
| `Focused Integration` Task 6 final control | `TestResults/test-lanes/20260815-010704-512-30164-a6451d76977d45128f9ca3622d0076da-focused` | 90 | 90 | 0 | 0 | no | complete | `00:01:04.1366021` |
| `Focused` mechanics snapshot API RED | `TestResults/test-lanes/20260815-075556-717-55648-0eb11b14e88a4ab78052e9154854e295-focused` | 0 | 0 | build RED | 0 | no | complete | `00:00:37.6231878` |
| `Focused` runtime characteristic catalog RED | `TestResults/test-lanes/20260815-080459-052-38868-21d39b90a60b4dc8b46f874654563ea7-focused` | 19 | 13 | 6 | 0 | no | complete | `00:00:21.7206293` |
| `Focused` aggregate percentage semantics RED | `TestResults/test-lanes/20260815-080918-525-38028-920b7632a75c417b962340bafee05723-focused` | 1 | 0 | 1 | 0 | no | complete | `00:00:23.9368216` |
| `Focused` hidden-audit privacy RED | `TestResults/test-lanes/20260815-081302-428-38600-50da44b6b81d4a5aa156fd7dd1b44079-focused` | 1 | 0 | 1 | 0 | no | complete | `00:00:21.9786895` |
| `Focused` Task 7 final mechanics control | `TestResults/test-lanes/20260815-081542-563-22808-f1b701fd4663495286672d2744a9036a-focused` | 34 | 34 | 0 | 0 | no | complete | `00:00:58.0718431` |
| `Fast` no-op normalization regression discovery | `TestResults/test-lanes/20260815-081752-158-54468-2dfd8bb4464545179440a0040a4cc1d5-fast` | 855 | 854 | 1 | 0 | no | complete | `00:01:23.6995464` |
| `Focused Integration` no-op normalization RED | `TestResults/test-lanes/20260815-082252-230-38628-f1a5c5bf2ee94423ba39949cc5659838-focused` | 1 | 0 | 1 | 0 | no | complete | `00:00:29.5824176` |
| `Focused Integration` no-op/commandless-plan GREEN | `TestResults/test-lanes/20260815-082449-941-20056-37a2b8a317da444a8595a1a0d380948f-focused` | 2 | 2 | 0 | 0 | no | complete | `00:01:03.5664155` |
| `Focused` original no-op regression GREEN | `TestResults/test-lanes/20260815-082602-698-39028-64f6879400a34df7b2ccfefd7ada4ac4-focused` | 1 | 1 | 0 | 0 | no | complete | `00:00:24.4999251` |
| `Fast` reserved pending-path registry discovery | `TestResults/test-lanes/20260815-082636-432-51948-005858e21a264ab8b8a60a46698e7766-fast` | 1709 | 1708 | 1 | 0 | no | complete | `00:01:28.6646467` |
| `Focused` reserved pending-path registry GREEN | `TestResults/test-lanes/20260815-082903-661-32512-b1a2f1a96df740239dd15f40d8e2f5bf-focused` | 1 | 1 | 0 | 0 | no | complete | `00:00:11.2219777` |
| `Fast` Task 7 final control | `TestResults/test-lanes/20260815-082928-974-55048-67b89b3394654cd2aac547b5ca0e9a65-fast` | 3428 | 3428 | 0 | 0 | no | complete | `00:03:01.1766140` |

The first Integration attempt stopped before the intended RED because NuGet
assets had been cleaned. After the documented project restore, the repeated
run produced the expected missing-context compile failure. That infrastructure
attempt is recorded in `quickstart.md` but is not behavioral RED. The
implementation session appends every inspected RED/GREEN result directory to
this section before marking the corresponding Spec Kit task complete.

The Task 2 RED failed only on the deliberately absent envelope, profile,
source-definition, phase, carrier, and fixture-profile APIs. The first GREEN
also completed twice (`20260814-212802-...` and `20260814-212817-...`, 65/65)
because the command transport returned before the owned lane finished; the
final strengthened fixture-and-contract control above is the recorded result.
A later test-only
compile correction (`20260814-213013-...`) changed an xUnit theory argument
from an internal enum to its string name; production behavior did not change.

## File Responsibility Map

| File | Responsibility |
| --- | --- |
| `BookOfEternityClient/Services/EffectMaterializationContract.cs` | Closed canonical active-instance envelope, lifetime, stacking, trigger, removal, links, and chronology validation. |
| `BookOfEternityClient/Services/EffectSourceDefinitionContract.cs` | Closed static `activeEffectDefinitions[]` contract and source-owned parameter bounds. |
| `BookOfEternityClient/Services/EffectComponentProfiles.cs` | Nine registered mechanical component payload validators, execution metadata, merge rules, and projection descriptors. |
| `BookOfEternityClient/Services/EffectIdentityState.cs` | Client identity index, random IDs, active/terminal entries, transitions, replay evidence, and global exact/confusable uniqueness. |
| `BookOfEternityClient/Services/EffectCarrierCatalog.cs` | One-pass player, NPC, Mortal combatant, afterlife profile, and spiritual-condition carrier catalog. |
| `BookOfEternityClient/Services/EffectCombatantIdentityState.cs` | Stable client combatant anchors and exact same-turn `combatantRef` mapping. |
| `BookOfEternityClient/Services/EffectSourceAuthority.cs` | Exact pre-turn plus accepted same-turn source-definition catalog. |
| `BookOfEternityClient/Services/EffectTargetAuthority.cs` | Exact owner/actor/combatant/profile/conflict target catalog and realm binding. |
| `BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs` | Immutable input/result/after-image records and identity factory seam. |
| `BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs` | One plan instance per accepted snapshot/session/input/source/target fingerprint. |
| `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs` | Parse operations, resolve authority, schedule transitions, and build complete final state without writes. |
| `BookOfEternityClient/Services/EffectLifecycleScheduler.cs` | Deterministic stacking, lifetime, trigger, periodic, event, and terminal reducers. |
| `BookOfEternityClient/Services/EffectPendingResolutionState.cs` | Client-owned bounded request creation and exact receipt consumption. |
| `BookOfEternityClient/Services/EffectMechanicsSnapshot.cs` | Immutable all-or-nothing accepted component/read model for mechanics consumers. |
| `BookOfEternityClient/Services/Validation/ValidationService.EffectMaterialization.cs` | Raw, composed, continuity, direct-mutation, and canonical post-seal validation. |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs` | Publish planned carrier/index/pending/companion after-images and consume transient commands. |
| `BookOfEternityClient/Services/EffectRepairPacketBuilder.cs` | Bounded effect repair candidate binding and protected-authority pre-dispatch stop. |
| `BookOfEternityClient/UI/EffectPlayerProjection.cs` | One accepted, visibility-aware, recursively sanitized projection for both clients. |
| `BookOfEternityClient.TestSupport/EffectMaterializationTestFixture.cs` | Deterministic complete raw/canonical/index/source/target fixtures shared by unit and integration tests. |
| `BookOfEternityClient.IntegrationTests/EffectMaterializationTestContext.cs` | Isolated file-backed context, accepted snapshot, failure injection, exact bytes, and output privacy assertions. |

---

### Task 1: Finish Preflight Inventory and Test Vocabulary (Spec Kit T004–T005)

**Files:**
- Modify: `specs/1535-complete-effect-materialization/research.md`
- Modify: `specs/1535-complete-effect-materialization/plan.md`
- Create: `specs/1535-complete-effect-materialization/effect-surface-inventory.md`
- Create: `BookOfEternityClient.TestSupport/EffectMaterializationTestFixture.cs`
- Create: `BookOfEternityClient.Tests/EffectMaterializationTestFixtureTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectMaterializationTestContext.cs`

**Required fixture surface:**

```csharp
public static class EffectMaterializationTestFixture
{
    public const string DefinitionKey = "bleeding_consequence";
    public const string EffectId = "effect_test_bleeding";
    public const string TransitionId = "effect_transition_test_apply";
    public const string CombatantRef = "combatant_ref_test_raider";
    public const string CombatantId = "combatant_test_raider";

    public static JsonObject CreateDefinition(string profile = "periodic_damage");
    public static JsonObject CreateApplyCommand(string targetKind = "player");
    public static JsonObject CreateCanonicalEffect(
        string ownerKind = "player",
        string profile = "periodic_damage");
    public static JsonObject CreateIdentityIndex(params JsonObject[] effects);
    public static JsonObject CreateCommandRoot(params JsonObject[] changes);
    public static JsonObject CreatePendingResolutionRoot();
}
```

- [ ] **Step 1: Reconfirm owned state**

Run `git status --short --branch`, `git rev-parse origin/main`, and `gh issue view 1535 --json state,labels,url`. Expected: issue open/P1/triaged, branch ahead only by #1535 planning work, repository visibility and Actions settings untouched.

- [ ] **Step 2: Inventory every current effect surface**

Run:

```powershell
rg -n 'playerActiveEffectsChanges|NPCEffectChanges|activeEffects|activeBuffs|activeDebuffs|combatConditions|combatEffect|structuredBonuses|duration|remainingTurns' BookOfEternityClient Rules OtherGuides Examples FileSystemExample -g '*.*'
```

Write one row per active production reader/writer, GM contract, example, and fixture in `effect-surface-inventory.md`. Classify each as `static-source`, `active-carrier`, `legacy-command`, `mechanics-reader`, `player-reader`, `afterlife-adapter`, `positive-fixture`, or `negative-fixture`, and name the later task that owns it.

- [ ] **Step 3: Add fixture-shape tests first**

Create tests asserting complete source, command, active instance, index, and pending roots; deep-clone isolation; no runtime IDs in definitions; and fixed test identities only in canonical fixtures.

- [ ] **Step 4: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectMaterializationTestFixtureTests"
```

Expected: compile failure because the fixture does not exist.

- [ ] **Step 5: Implement fixture and file-backed context**

`EffectMaterializationTestContext` owns a temporary session root, creates fresh `FileSystemManager`, `ValidationService`, and `CanonicalStateNormalizer`, captures the validated pre-turn snapshot, records file existence plus exact text bytes, and deletes only its own resolved root on disposal.

- [ ] **Step 6: Run GREEN and inspect the summary**

Run the same filter. Require exit `0`, all discovered tests passed, zero duplicates, timeout false, and cleanup complete.

- [ ] **Step 7: Update T004–T005 only after inspection and commit**

```powershell
git add -- specs/1535-complete-effect-materialization BookOfEternityClient.TestSupport/EffectMaterializationTestFixture.cs BookOfEternityClient.Tests/EffectMaterializationTestFixtureTests.cs BookOfEternityClient.IntegrationTests/EffectMaterializationTestContext.cs
git diff --cached --check
git commit -m "test: inventory effect materialization surfaces (#1535)"
```

---

### Task 2: Implement Closed Definitions, Components, and Active Envelope (T006–T011)

**Files:**
- Create: `BookOfEternityClient/Services/EffectMaterializationContract.cs`
- Create: `BookOfEternityClient/Services/EffectSourceDefinitionContract.cs`
- Create: `BookOfEternityClient/Services/EffectComponentProfiles.cs`
- Create: `BookOfEternityClient.Tests/EffectMaterializationContractTests.cs`
- Create: `BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs`

**Production interfaces:**

```csharp
internal enum EffectMaterializationPhase { RawDefinition, CanonicalActive }

internal static class EffectMaterializationContract
{
    internal const int SchemaVersion = 1;
    internal static IReadOnlyList<ValidationIssue> Validate(
        JsonElement effect,
        string path,
        EffectMaterializationPhase phase);
}

internal static class EffectSourceDefinitionContract
{
    internal static IReadOnlyList<ValidationIssue> ValidateArray(
        JsonElement definitions,
        string path,
        string realm);
}
```

- [x] **Step 1: Write closed-envelope RED tests**

Cover every required root section, duplicate property, unknown property, wrong scalar/container type, forbidden active identity in definitions, missing canonical identity, active/terminal state mismatch, and missing-pristine versus non-empty legacy carrier behavior.

- [x] **Step 2: Write all nine profile RED tests**

Cover `characteristic_modifier`, `roll_modifier`, `resistance_modifier`, `periodic_damage`, `periodic_restore`, `action_control`, `event_reaction`, `wound_consequence`, and `afterlife_combat_condition`. Assert finite/bounded values, closed enum values, exact registered operands, unknown-field rejection, and no prose-only mechanic.

- [x] **Step 3: Write definition RED tests**

Assert exact/confusable uniqueness of `definitionKey`, complete target/realm/component/parameter/stack/lifetime/trigger/removal policy, source parameter bounds, and rejection of `effectId`, current stacks, remaining counters, transitions, receipts, or terminal history.

- [x] **Step 4: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectMaterializationContractTests|FullyQualifiedName~EffectSourceDefinitionContractTests"
```

Expected: compile failure for the three missing production types.

- [x] **Step 5: Implement exact JSON validation helpers**

Validate raw `JsonElement` before `JsonNode` conversion so duplicate properties remain observable. Use closed field sets per discriminated mode and produce stable `effect_materialization_*` issue codes with exact paths.

- [x] **Step 6: Implement profile registry behavior**

Each profile record supplies `ValidatePayload`, deterministic/receipt resolution metadata, legal merge reducers, and a safe projection descriptor. Do not execute mechanics in the contract layer.

- [x] **Step 7: Run GREEN and one malformed-control mutation**

Re-run the filter, then mutate a valid fixture to include a non-finite number and an unknown nested field; both must fail with bounded issue paths.

- [x] **Step 8: Commit the closed semantic vocabulary**

```powershell
git add -- BookOfEternityClient/Services/EffectMaterializationContract.cs BookOfEternityClient/Services/EffectSourceDefinitionContract.cs BookOfEternityClient/Services/EffectComponentProfiles.cs BookOfEternityClient.Tests/EffectMaterializationContractTests.cs BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: define complete active effect contracts (#1535)"
```

---

### Task 3: Implement Identity and Owner-Carrier Authority (T012–T015)

**Files:**
- Create: `BookOfEternityClient/Services/EffectIdentityState.cs`
- Create: `BookOfEternityClient/Services/EffectCarrierCatalog.cs`
- Create: `BookOfEternityClient.Tests/EffectIdentityStateTests.cs`
- Create: `BookOfEternityClient.Tests/EffectCarrierCatalogTests.cs`

**Production interfaces:**

```csharp
internal sealed class EffectIdentityFactory
{
    internal string CreateEffectId();
    internal string CreateTransitionId();
    internal string CreateResolutionId();
}

internal sealed record EffectCarrierCoordinate(
    string Kind,
    string OwnerId,
    string Path,
    string? Category);

internal sealed class EffectCarrierCatalog
{
    internal static EffectCarrierCatalog Build(EffectCarrierCatalogInput input);
    internal bool TryResolveOne(string effectId, out EffectCarrierOccurrence occurrence);
}
```

- [x] **Step 1: Add identity-index RED tests**

Test the closed root/entry/owner/stack/transition shapes, random ID prefixes, active/terminal agreement, immutable terminal evidence, positive turn/event authority, duplicate exact/case/confusable IDs across entries and transitions, replay IDs, and GM-authored index mutation rejection.

- [x] **Step 2: Add carrier RED tests**

Test player, NPC, Mortal buff/debuff, afterlife profile, and spiritual-condition coordinates; duplicate cross-carrier occurrence; target/carrier mismatch; adjacent NPC wound preservation; map/profile sibling preservation; and unsupported non-empty legacy shapes.

- [x] **Step 3: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectIdentityStateTests|FullyQualifiedName~EffectCarrierCatalogTests"
```

- [x] **Step 4: Implement identity parsing before mutation APIs**

Parse raw JSON with duplicate detection, validate every transition as a closed immutable record, build exact and confusable dictionaries once, and expose serialization only from validated state.

- [x] **Step 5: Implement allowlisted carrier enumeration**

Catalog only the five approved owner families. For NPCs, replace only `activeEffects`; for combatants, distinguish `activeBuffs` and `activeDebuffs`; for spiritual conflicts, adapt `combatConditions` without adding a duplicate generic array.

- [x] **Step 6: Run GREEN plus duplicate-carrier control**

Require all tests green and explicitly verify that two byte-identical instances in different carriers still fail as two logical occurrences.

- [x] **Step 7: Commit identity/carrier authority**

```powershell
git add -- BookOfEternityClient/Services/EffectIdentityState.cs BookOfEternityClient/Services/EffectCarrierCatalog.cs BookOfEternityClient.Tests/EffectIdentityStateTests.cs BookOfEternityClient.Tests/EffectCarrierCatalogTests.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: add effect identity and carrier authority (#1535)"
```

---

### Task 4: Implement Exact Source, Target, Combatant, and Plan-Cache Authority (T016–T022)

**Files:**
- Create: `BookOfEternityClient/Services/EffectSourceAuthority.cs`
- Create: `BookOfEternityClient/Services/EffectTargetAuthority.cs`
- Create: `BookOfEternityClient/Services/EffectCombatantIdentityState.cs`
- Create: `BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs`
- Create: `BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs`
- Create: `BookOfEternityClient.Tests/EffectSourceAuthorityTests.cs`
- Create: `BookOfEternityClient.Tests/EffectTargetAuthorityTests.cs`
- Create: `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs`

**Production interfaces:**

```csharp
internal sealed record EffectSourceKey(
    string Realm,
    string Kind,
    string SourceId,
    string DefinitionKey);

internal sealed record EffectTargetKey(
    string Realm,
    string Kind,
    string TargetId);

internal sealed record EffectAcceptedTurnPlanningResult(
    EffectAcceptedTurnPlan? Plan,
    IReadOnlyList<ValidationIssue> Issues);

internal static class EffectAcceptedTurnPlanAuthority
{
    internal static EffectAcceptedTurnPlanningResult GetOrBuild(
        FileSystemManager fileSystem,
        EffectAcceptedTurnInput input);
}
```

- [x] **Step 1: Add source RED tests**

Cover skill, spiritual art, item, wound, quest, location, hazard, faction, event, Fate Card, and combat adapters; pre-turn and accepted same-turn exports; passive/instantaneous definitions; parameter bounds; exact/case/confusable/historical/cross-realm selectors; and duplicate definitions.

- [x] **Step 2: Add target/combatant RED tests**

Cover player, NPC, Guardian, resident, Shining faction head, radiant actor, persistent afterlife profile, spiritual side, and Mortal combatant. Raw new combatants may expose exact `combatantRef` only; the client allocates `combatantId`, and canonical effect targets store only that permanent ID.

- [x] **Step 3: Add cache RED tests**

Inject a counting identity factory. Assert one random allocation and the same plan object across raw validation, companion validation, mechanics derivation, and commit. Changing session, validated snapshot, raw commands, source catalog, target catalog, or event input must invalidate the cache.

- [x] **Step 4: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectSourceAuthorityTests|FullyQualifiedName~EffectTargetAuthorityTests|FullyQualifiedName~EffectAcceptedTurnPlannerTests"
```

- [x] **Step 5: Build source and target dictionaries once**

Compose validated pre-turn authorities with explicit exports from already-built same-turn item/actor/faction/location/quest/wound/combat/afterlife plans. Never infer authority from raw sibling JSON, names, UI selectors, or file order.

- [x] **Step 6: Implement stable combat-local anchors**

Resolve each exact raw `combatantRef` once, allocate a random `combatantId`, rewrite the accepted combatant object, and export that mapping to the effect plan. Reject submitted permanent IDs and duplicate/confusable refs.

- [x] **Step 7: Implement immutable plan cache**

Fingerprint the accepted input plus validated snapshot/session and serialized source/target catalogs, but never derive permanent IDs from the hash. Store the first random plan result in a `ConditionalWeakTable<FileSystemManager, EffectAcceptedTurnPlanCache>` as the location planner does.

- [x] **Step 8: Run GREEN and assert factory count**

Require exactly one effect ID and one transition ID allocation for one accepted application across all callers.

- [x] **Step 9: Commit exact authority**

```powershell
git add -- BookOfEternityClient/Services/EffectSourceAuthority.cs BookOfEternityClient/Services/EffectTargetAuthority.cs BookOfEternityClient/Services/EffectCombatantIdentityState.cs BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs BookOfEternityClient.Tests/EffectSourceAuthorityTests.cs BookOfEternityClient.Tests/EffectTargetAuthorityTests.cs BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: bind exact effect source and target authority (#1535)"
```

---

### Task 5: Add the Common Command Surface and Pure Planner (T023, T028–T030)

**Files:**
- Modify: `BookOfEternityClient/Models/GameResponse.cs`
- Modify: `BookOfEternityClient/Configuration/FileMapping.cs`
- Create: `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`
- Modify: `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs`

**Command model:**

```json
{
  "effectChanges": [
    {
      "operation": "apply",
      "target": { "kind": "player", "targetId": "player_current" },
      "source": {
        "kind": "wound",
        "sourceId": "wound_exact",
        "definitionKey": "bleeding_consequence"
      },
      "parameters": null,
      "eventRef": { "kind": "accepted_turn", "authorityId": "turn_42" },
      "reason": "Рана снова открылась"
    }
  ],
  "effectResolutionReceipts": []
}
```

- [x] **Step 1: Add planner RED tests**

Assert `apply` builds one complete active instance, correct owner carrier, identity entry, touched paths, and command deletion. Reject submitted post-state fields, effect IDs, stack counters, lifetime remaining state, transition IDs, receipts, missing source parameters, wrong target, and direct carrier/index post-state.

- [x] **Step 2: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectAcceptedTurnPlannerTests"
```

- [x] **Step 3: Replace response fields**

Add only `EffectChanges` and `EffectResolutionReceipts` common response properties. Remove positive mappings for `playerActiveEffectsChanges` and `NPCEffectChanges`; do not retain aliases.

- [x] **Step 4: Map transient staging**

Map both fields to `game_state/effects/effect_commands.json`. The command root is transient, never canonical durable authority, and is deleted after successful consumption.

- [x] **Step 5: Implement pure apply planning**

Parse the closed command, resolve source/target from accepted catalogs, bind source-owned parameters, construct display/components/lifetime/stack/triggers/removal/links, allocate identities once, and deep-clone unrelated carrier fields. Return issues and after-images; perform no filesystem writes.

- [x] **Step 6: Run GREEN plus legacy-route negative tests**

Require valid apply green and old command properties rejected or ignored as explicitly invalid contract input—not silently applied.

- [x] **Step 7: Commit the MVP plan input**

```powershell
git add -- BookOfEternityClient/Models/GameResponse.cs BookOfEternityClient/Configuration/FileMapping.cs BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: plan active effect applications (#1535)"
```

---

### Task 6: Integrate Raw Validation and Canonical Publication (T024–T026, T031–T036)

**Files:**
- Create: `BookOfEternityClient/Services/Validation/ValidationService.EffectMaterialization.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs`
- Modify: source-owning validators named in T033–T034
- Create: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.Sources.cs`
- Create: `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Effects.cs`

- [x] **Step 1: Add raw/composed validation RED tests**

Cover missing sections, malformed components, direct carrier/index mutation, one logical occurrence, missing pristine initialization, non-empty legacy rejection, exact source/target failures, same-turn item/actor/faction/location/quest/event/wound/combatant exports, and zero writes on failure.

- [x] **Step 2: Add normalizer RED tests**

Cover player, NPC with adjacent wound data, combat buff/debuff category, command consumption, identity index, same-plan IDs, source-definition preservation, unrelated sibling preservation, and final carrier/index agreement.

- [x] **Step 3: Run RED integration filters**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectMaterializationValidationTests|FullyQualifiedName~CanonicalStateNormalizerEffectTests"
```

- [x] **Step 4: Add definition validation at every source boundary**

Invoke the shared definition contract from player/item/quest/wound/combat/NPC/faction/location/event/actor/profile validators. Do not copy the profile schema into each validator.

- [x] **Step 5: Insert effect validation after source plans**

Raw validation builds or retrieves the cached effect plan only after all applicable source and target plans are available. Canonical post-validation verifies carriers, identity index, and exact source/target/mechanics agreement from the complete composed after-state; it does not claim the later lifecycle pending-state or consumer-snapshot work.

- [x] **Step 6: Publish after all source-owning normalizers**

Under the existing bound lease, write every planned carrier and identity-index path, preserve unrelated subtrees, consume the command root, and post-validate source/target/mechanics agreement. Pending resolutions, lifecycle scheduling, and mechanics-consumer snapshots remain assigned to T040+ and T027/T037+.

- [x] **Step 7: Run GREEN and inspect exact files**

Verify the command file is absent, the canonical effect and index contain the cached random IDs, and adjacent NPC wound bytes/semantics remain unchanged.

- [x] **Step 8: Commit trustworthy Mortal creation**

```powershell
git add -- BookOfEternityClient/Services/Validation/ValidationService.EffectMaterialization.cs BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs BookOfEternityClient/Services/Validation/ValidationService.MortalLocationMaterialization.cs BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs BookOfEternityClient/Services/MortalItemMaterializationContract.cs BookOfEternityClient/Services/CanonicalStateNormalizer.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.cs BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.Sources.cs BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Effects.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: publish canonical active effects (#1535)"
```

---

### Task 7: Replace Raw Mechanics Reads with One Accepted Snapshot (T027, T037–T039)

**Files:**
- Create: `BookOfEternityClient/Services/EffectMechanicsSnapshot.cs`
- Modify: `BookOfEternityClient/Services/EffectCarrierCatalog.cs`
- Modify: `BookOfEternityClient/Services/CharacteristicsService.cs`
- Modify: `BookOfEternityClient/Services/EffectComponentProfiles.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs`
- Create: `BookOfEternityClient.Tests/EffectMechanicsSnapshotTests.cs`
- Modify: `BookOfEternityClient.Tests/CharacteristicsServiceTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Effects.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.GeneralPanels.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`
- Modify: `OtherGuides/Afterlife_Pending_Control_Surface_Inventory.json`
- Modify: `specs/1535-complete-effect-materialization/data-model.md`

**Snapshot surface:**

```csharp
internal sealed record EffectMechanicsSnapshot(
    bool IsAccepted,
    IReadOnlyList<EffectMechanicalComponent> Components,
    IReadOnlyList<EffectMechanicsAuditEntry> Audit,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal static EffectMechanicsSnapshot Build(EffectMechanicsInput input);
}
```

- [x] **Step 1: Add all-or-nothing RED tests**

Assert a valid modifier applies once; one malformed sibling (including a wrong-typed governed owner/collection), duplicate effect, target mismatch, bad component, bad index entry, or unsupported profile makes the whole governed active snapshot contribute zero. Static `structuredBonuses` and source definitions remain separate and are not double-counted.

- [x] **Step 2: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectMechanicsSnapshotTests|FullyQualifiedName~CharacteristicsServiceTests"
```

- [x] **Step 3: Implement immutable component projection**

Build from the validated carrier catalog and identity state, sort by effect/component identity, and return no partial mechanics on any authority issue. Keep audit metadata player-safe and free of raw DTOs.

- [x] **Step 4: Migrate CharacteristicsService**

Delete raw active-effect alias parsing. Apply only supported characteristic contributions in this consumer; reject finite aggregate values outside `Int32` instead of saturating; leave resistance profiles for their owning future consumer and keep static item/skill/source bonuses on their existing governed route. Keep internal mechanics for GM/QTE consumers while deriving a separate fail-closed player-visible characteristic projection that omits hidden/GM-only implications and never falls back to the internal map.

- [x] **Step 5: Run GREEN and legacy double-count control**

Verify one definition plus one active instance contributes exactly one active mechanic, not two.

- [x] **Step 6: Run one meaningful Fast checkpoint**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Inspect `summary.json`; record total, pass/fail, duplicates, timeout, cleanup, wall, and result path here. Do not rerun Fast merely because subsequent focused work begins.

Earlier Fast attempts were intentionally not accepted as the checkpoint: they
exposed an unconditional effect-normalizer dependency on `turn_request.json`
for unrelated normalization, an undocumented reserved
`pending_effect_resolutions.json` path, and then review found that a successfully
published validated plan remained cached. Dedicated RED→GREEN tests fixed all
three without weakening commandless `combatantRef` publication or activating
the future pending contract. The stale-plan regression failed at
`TestResults/test-lanes/20260815-091945-981-41912-3028ded2fc8e4b18a53cda46b4df66b8-focused`
and passed at
`TestResults/test-lanes/20260815-092031-448-47408-9620e1d76b284a6b8d81bd4634ead2fa-focused`.
The final Fast checkpoint at
`TestResults/test-lanes/20260815-092308-521-57072-f1333ebcd02045bdaab0d3deadefd2ed-fast`
passed 3441/3441 in 3:04.974 with zero duplicates, no timeout, and complete
owned-tree cleanup.

GM synchronization check: this slice adds no new GM-authored field or response
surface. The characteristic profile validator now follows the already documented
Block 5 twelve-characteristic catalog, and the existing quickstart contains the
worked `effectChanges[]` authoring example. The future pending-resolution file
remains explicitly inactive until T043/T049–T050; its GM contract, prompts, and
worked receipt example therefore remain in their owning later slice.
`playerVisibleModifiedCharacteristics` is a client-derived projection field in
the GM-reference computed-characteristics file, not GM-authored authority; GM/QTE
mechanics continue to use internal `modifiedCharacteristics`/`Modified`. Therefore
no GM prompt or worked authoring example changes are required for this privacy-only
projection field.

- [x] **Step 7: Commit the US1 mechanics slice**

```powershell
git add -- BookOfEternityClient/Services/EffectMechanicsSnapshot.cs BookOfEternityClient/Services/EffectCarrierCatalog.cs BookOfEternityClient/Services/CharacteristicsService.cs BookOfEternityClient/Services/EffectComponentProfiles.cs BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs BookOfEternityClient/UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs BookOfEternityClient.Tests/EffectMechanicsSnapshotTests.cs BookOfEternityClient.Tests/CharacteristicsServiceTests.cs BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Effects.cs BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.GeneralPanels.cs BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs OtherGuides/Afterlife_Pending_Control_Surface_Inventory.json specs/1535-complete-effect-materialization/data-model.md specs/1535-complete-effect-materialization/quickstart.md specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: derive accepted active effect mechanics (#1535)"
```

---

### Task 8: Implement Five Stack Policies and Eight Lifetime Modes (T040–T041, T044–T046, T048, T051–T052)

**Files:**
- Create: `BookOfEternityClient/Services/EffectLifecycleScheduler.cs`
- Create: `BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Stacking.cs`
- Create: `BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Lifetime.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.Lifecycle.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`
- Modify: `BookOfEternityClient/Services/EffectMaterializationContract.cs`
- Modify: `BookOfEternityClient/Services/EffectSourceDefinitionContract.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.EffectMaterialization.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs`
- Modify: `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs`
- Modify: `BookOfEternityClient.Tests/EffectMaterializationContractTests.cs`
- Modify: `BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Effects.cs`
- Modify: `specs/1535-complete-effect-materialization/contracts/effect-stacking-and-lifecycle.md`
- Modify: `specs/1535-complete-effect-materialization/data-model.md`
- Modify: `specs/1535-complete-effect-materialization/quickstart.md`
- Modify: `specs/1535-complete-effect-materialization/tasks.md`

- [x] **Step 1: Add stack RED tests**

Cover independent bounded instances, stack max behavior, refresh reset/extend, replace terminal plus new identity, registered merge reducers, policy mismatch, overflow, stale replay, and stable stack coordinates.

- [x] **Step 2: Add lifetime RED tests**

Cover `turns`, `uses`, `until_time`, `scene`, `source_bound`, `condition_bound`, authorized `permanent`, and registered `manual`; equality boundaries; suspension versus expiry; realm transitions; and forbidden `999`, negative, zero, prose, or unknown modes.

`until_time` uses exact `world_time.currentTimeInMinutes`: source definitions
declare positive duration plus that authority token, and the client derives a
checked numeric canonical deadline. Independent source `maxStacks` bounds the
number of simultaneous identities while every instance remains `1/1`.

- [x] **Step 3: Add composed lifecycle RED tests**

Cover apply+remove, apply+expiry, refresh+advance, source loss+reapply, replace, replay, command consumption, session replacement, and exact terminal identity history.

- [x] **Step 4: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectLifecycleSchedulerTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectMaterializationValidationTests"
```

RED evidence: scheduler files first failed to compile at
`TestResults/test-lanes/20260815-093710-932-1808-5cf5850d67304e8b948df51434811e6a-focused`;
the first behavior run at
`TestResults/test-lanes/20260815-094118-362-18720-812fd49d199f4dc9956470a02b36d142-focused`
passed 19/25 and failed the six still-unimplemented reducers. The initial
composed lifecycle run at
`TestResults/test-lanes/20260815-095112-940-30916-c5f5199743414ce3869a763c3220f888-focused`
failed 5/5. Focused boundary REDs also captured missing independent source
bounds at
`TestResults/test-lanes/20260815-101704-820-30024-fd5b5f0ebccd4cdb827bc6732a25e1fb-focused`,
missing numeric `until_time` materialization at
`TestResults/test-lanes/20260815-102252-253-53056-14b3db871bf74f1aaf5986a6f7939d15-focused`,
invalid merge/profile compatibility at
`TestResults/test-lanes/20260815-102611-872-28816-95f18f381ac94b45b005665acbf5f9c4-focused`,
missing production world-time composition at
`TestResults/test-lanes/20260815-102910-672-29100-13eae9d4e8014d0090753a8e5f53eded-focused`,
stale direct-time precedence over an accepted same-turn override at
`TestResults/test-lanes/20260815-113511-595-46168-002f6886b84441d2947168834542f605-focused`,
and the disconnected manual lifetime authority at
`TestResults/test-lanes/20260815-114404-600-51900-1b557de328564621b1d8cc4a0b185c3b-focused`.

- [x] **Step 5: Implement pure stack reducers**

Resolve source-owned stack coordinates. Each reducer returns final active/terminal entries and transitions; none writes files or mutates the input node.

- [x] **Step 6: Implement pure lifetime reducers**

Use accepted chronology/events only. Never ask the GM to decrement counters, choose terminal identity, or restate post-state.

Task 8 integrates bound continuation, remove/dispel, application/stack outcome,
and non-trigger lifetime advancement. Periodic/event trigger execution,
`component_response`, and pending receipts remain explicitly assigned to Task 9
(T042–T043/T047/T049–T050); this slice fails closed instead of approximating
those future phases.

`profile_specific` merge execution also remains in the registered component
phase. The current production adapter supplies Mortal player owner-turn and
exact world-time events; uses, scene, and condition reducers require exact
events from their owning trigger/scene/condition adapters and fail closed when
that authority is absent.

- [x] **Step 7: Add dispel/remove planning**

Require exact existing `effectId` plus an authority permitted by the active instance. Retargeting, stale IDs, cross-owner removal, and repair-based identity changes fail closed.

Manual lifetime authority is read from the source-owned
`lifetime.authorities[]` catalog; it is not required to be redundantly copied
into `removal.manualAuthorities[]`. The focused boundary went GREEN at
`TestResults/test-lanes/20260815-114458-323-50948-2acbd1766a064bcb84c294e07900e490-focused`.

GREEN evidence: same-turn world-time override precedence and unresolved
override fail-closed behavior passed 3/3 at
`TestResults/test-lanes/20260815-113604-531-21828-47a48685bde04049bdd00751cb539c5e-focused`.
The final Task 8 unit filter passed 182/182 at
`TestResults/test-lanes/20260815-115235-521-11188-85f42a78f97d45168ead51a55565086f-focused`;
the combined effect validation/normalizer integration filter passed 175/175 at
`TestResults/test-lanes/20260815-115306-653-42636-dbe01612d3bc430195663116aced8ae2-focused`.
The meaningful local Fast checkpoint passed 3479/3479 with zero failures,
duplicates, timeout, or cleanup failure in `00:03:22.2727046` at
`TestResults/test-lanes/20260815-115648-189-18352-684ba6813920411793285b3f9c6c8718-fast`.

GM synchronization rationale: Task 8 adds no new raw field beyond the common
`effectChanges[]`/source-definition contract already specified by this feature,
and every stack/lifetime after-image remains client-owned. Live GM rules,
prompts, active fixtures, and worked examples intentionally remain inactive
until the tracked Phase 9 work T097–T110 can publish the complete executable
contract rather than a partial one. No Chaos Sea, Shining Abode, pending-control,
or afterlife GM-authored contract changes in this slice, so the afterlife matrix
and examples do not change here.

- [x] **Step 8: Run GREEN and commit**

```powershell
git add -- BookOfEternityClient/Services/EffectLifecycleScheduler.cs BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs BookOfEternityClient/Services/EffectMaterializationContract.cs BookOfEternityClient/Services/EffectSourceDefinitionContract.cs BookOfEternityClient/Services/Validation/ValidationService.EffectMaterialization.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Stacking.cs BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Lifetime.cs BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs BookOfEternityClient.Tests/EffectMaterializationContractTests.cs BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.Lifecycle.cs BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.cs BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Effects.cs specs/1535-complete-effect-materialization/contracts/effect-stacking-and-lifecycle.md specs/1535-complete-effect-materialization/data-model.md specs/1535-complete-effect-materialization/quickstart.md specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: schedule effect stacking and lifetime (#1535)"
```

---

### Task 9: Implement Triggers, Periodic Work, and Bounded Receipts (T042–T043, T047, T049–T050)

**BLOCKED BY #1543**: This task resumes only after the unified resource materialization feature has completed its full no-migration cutover. Periodic damage/restoration and bounded resource receipts must publish through the shared resource ledger and `AcceptedMechanicsPlanner`, not through `currentHealthChange`, percentage fields, combat health arrays, item resource fields, afterlife action-economy fields, or any other effect-only adapter.

**Files:**
- Modify: `BookOfEternityClient/Services/EffectLifecycleScheduler.cs`
- Create: `BookOfEternityClient/Services/EffectPendingResolutionState.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`
- Create: `BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Triggers.cs`
- Create: `BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Resolutions.cs`

- [ ] **Step 1: Add trigger/order RED tests**

Assert phase order, then priority, effect ID, and component ID order; periodic damage/restore; accepted event reaction; duplicate event suppression; cycle detection; dependency validation; and a bounded expansion ceiling.

- [ ] **Step 2: Add pending-resolution RED tests**

Assert exact request and receipt shape, one-time consumption, deterministic components creating no GM request, stale/partial/extra/cross-target/out-of-bound receipt rejection, protected GM edits to pending state, and session replacement cleanup.

- [ ] **Step 3: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectLifecycleSchedulerTests"
```

- [ ] **Step 4: Implement deterministic event graph execution**

Build event/component indexes once. Reject cycles before applying any result. Enforce one accepted event identity and one execution per subscribed component.

- [ ] **Step 5: Implement bounded pending state**

The client writes `game_state/control/pending_effect_resolutions.json`; the GM sees only the declared story-facing fields and replies through `effectResolutionReceipts[]`. Exact IDs, targets, operands, result kind, and bounds must agree.

- [ ] **Step 6: Protect lifecycle-control state**

Add pending effects to validated snapshot, session binding, protected mutation checks, rollback tracking, and cleanup. Missing pristine is empty; GM-authored or stale non-empty state is protected failure.

- [ ] **Step 7: Run GREEN and commit**

```powershell
git add -- BookOfEternityClient/Services/EffectLifecycleScheduler.cs BookOfEternityClient/Services/EffectPendingResolutionState.cs BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Triggers.cs BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Resolutions.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: resolve deterministic effect triggers (#1535)"
```

---

### Task 10: Adapt Persistent Afterlife Effects and Spiritual Conditions (T053–T064)

**Files:**
- Modify: `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`
- Modify: `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs`
- Modify: `BookOfEternityClient/Services/AfterlifeSpiritualConflictTurnPreviewService.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeCombatConditions.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`
- Modify: `BookOfEternityClient/Services/EffectCarrierCatalog.cs`
- Modify: `BookOfEternityClient/Services/EffectComponentProfiles.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Profiles.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Conditions.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Realms.cs`
- Create: `BookOfEternityClient.Tests/EffectMechanicsSnapshotTests.Afterlife.cs`

- [ ] **Step 1: Add profile RED tests**

Cover player soul, Guardian, resident, Shining faction head, radiant actor, exact accepted profile binding, one logical carrier, direct profile-effect mutation, and explicit exclusion of Shining blessing entitlement.

- [ ] **Step 2: Add spiritual-condition RED tests**

Cover all five existing condition kinds, legal axes, exact source/side/actor, finite use/exchange/scene semantics, counterplay/payoff, stack behavior, replay, direct `combatConditions[]` mutation, and terminal history.

- [ ] **Step 3: Add realm-transition RED tests**

Cover suspend, expire, forbidden cross-realm carry, stale conflict/session, and exact same-turn afterlife actor targets.

- [ ] **Step 4: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectAfterlifeAdapterTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectMechanicsSnapshotTests.Afterlife"
```

- [ ] **Step 5: Add profile carrier support**

Persist generic afterlife actor effects only in accepted profile `activeEffects[]`. Preserve every unrelated profile field and require exact actor/profile target agreement.

- [ ] **Step 6: Add the spiritual adapter**

Map common identity/source/target/stack/lifecycle/history into the existing specialized condition object while preserving kind-specific legal axes and counterplay. Never add the same instance to generic `activeEffects[]`.

- [ ] **Step 7: Route mechanics through the accepted snapshot**

Only the registered `afterlife_combat_condition` profile may contribute spiritual axes. Keep generic Mortal characteristic stacking out of the conflict preview unless a registered adapter explicitly authorizes it.

- [ ] **Step 8: Run GREEN and the existing afterlife control**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeSpiritualConflictValidationTests|FullyQualifiedName~AfterlifeEntityProfileValidationTests|FullyQualifiedName~EffectAfterlifeAdapterTests"
```

- [ ] **Step 9: Commit cross-realm authority**

```powershell
git add -- BookOfEternityClient/Services/AfterlifeEntityProfileState.cs BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs BookOfEternityClient/Services/AfterlifeSpiritualConflictTurnPreviewService.cs BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs BookOfEternityClient/Services/Validation/ValidationService.AfterlifeCombatConditions.cs BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs BookOfEternityClient/Services/EffectCarrierCatalog.cs BookOfEternityClient/Services/EffectComponentProfiles.cs BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Profiles.cs BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Conditions.cs BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Realms.cs BookOfEternityClient.Tests/EffectMechanicsSnapshotTests.Afterlife.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: unify afterlife effect identity lifecycle (#1535)"
```

---

### Task 11: Build One Safe Console/Browser Projection and Revalidated Actions (T065–T076)

**Files:**
- Create: `BookOfEternityClient/UI/EffectPlayerProjection.cs`
- Create: `BookOfEternityClient/UI/ExplorerMortalEffectDetailActions.cs`
- Modify: Mortal and afterlife readers/builders named in T070–T074
- Modify: `BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs`
- Create: `BookOfEternityClient.Tests/EffectPlayerProjectionTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.Effects.cs`
- Create: `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.Effects.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Projection.cs`

**Projection surface:**

```csharp
internal sealed record EffectPlayerEntry(
    string Selector,
    string Name,
    string? Summary,
    string State,
    IReadOnlyList<EffectPlayerFact> Facts,
    IReadOnlyList<EffectPlayerAction> Actions);

internal static class EffectPlayerProjection
{
    internal static EffectPlayerProjectionResult Build(EffectPlayerProjectionInput input);
}
```

- [ ] **Step 1: Add projection/privacy RED tests**

Cover visible facts, setting-specific component catch-all, stacks/lifetime/source links, hidden and GM-only omission, malformed all-or-nothing fallback, whole identity/index/pending/repair/request/report DTO shapes, annotated supersets, and adjacent legitimate `{kind,title,steps,turn,route}` semantics.

- [ ] **Step 2: Add console/browser RED tests**

Cover player/NPC/combatant/profile/condition rows and details, Russian in-world copy, parity, opaque selectors, stale/forged submit, accepted write-time re-resolution, malformed authority, hidden output, and serialized `/api/game-screen` privacy.

- [ ] **Step 3: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectPlayerProjectionTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~EffectAfterlifeAdapterTests"
```

- [ ] **Step 4: Implement accepted visible projection**

Read only the accepted carrier/index snapshot. Include safe registered-profile facts and setting-specific non-internal semantics. Do not expose permanent effect IDs, source/target permanent selectors, carrier paths, transitions, receipts, pending requests, repair packets, diagnostic reports, or agent instructions.

- [ ] **Step 5: Implement context-aware whole-DTO suppression**

Extend the existing recursive sanitizer with effect-specific shape signatures rather than globally hiding common semantic keys. Suppress annotated supersets of real internal DTOs; preserve neighboring world objects that merely use `kind`, `title`, `steps`, `turn`, `route`, or `source`.

- [ ] **Step 6: Implement opaque actions and write-time revalidation**

Selectors are short-lived player projection handles. On submit, resolve the handle against the current accepted effect set and re-check removal authority; stale, hidden, forged, moved, terminal, or wound-treatment-confused actions perform no mutation.

- [ ] **Step 7: Decide frontend scope from evidence**

If existing generic DTOs render every required fact/action, record `no frontend source change: generic blocks sufficient` here. If a typed contract changes, update `BookOfEternityClient.WebFrontend/src/` and run its repository-defined verify command.

- [ ] **Step 8: Run GREEN, serialize both clients, and commit**

```powershell
git add -- BookOfEternityClient/UI BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs BookOfEternityClient.Tests/EffectPlayerProjectionTests.cs BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.Effects.cs BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.Effects.cs BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Projection.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: project active effects safely (#1535)"
```

---

### Task 12: Enforce Wound and Effect Independence (T089–T096)

**Files:**
- Create: `BookOfEternityClient.Tests/EffectSourceAuthorityTests.Wounds.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.Wounds.cs`
- Modify: `BookOfEternityClient/Services/EffectSourceAuthority.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs`
- Modify: `BookOfEternityClient/UI/EffectPlayerProjection.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMortalEffectDetailActions.cs`

- [ ] **Step 1: Add wound authority RED tests**

Cover exact wound link and source-bound definition, missing/case/confusable/stale wound, no repair retarget, and explicit absence of wound-write capability from the effect plan.

- [ ] **Step 2: Add composed RED tests**

Cover apply, dispel, remove, expiry, source loss, simultaneous accepted wound treatment, direct effect-side wound mutation, adjacent NPC wound state, and byte/semantic wound preservation after both success and rollback.

- [ ] **Step 3: Add copy/action RED tests**

The player must see that suppressing/removing an effect does not treat the wound. A wound-treatment action and an effect-removal action remain distinct and independently revalidated.

- [ ] **Step 4: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectSourceAuthorityTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectMaterializationValidationTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests"
```

- [ ] **Step 5: Implement read-only wound source linkage**

The adapter may read exact wound identity/state and receive accepted wound-plan source-loss/treatment events. It may not write wound fields or infer treatment from effect terminal state.

- [ ] **Step 6: Preserve wound-owned subtrees**

Effect normalization replaces only the active-effect field and exact effect-owned companions. Assert wound state before/after with deep semantic equality and exact bytes where the containing file can remain byte-identical.

- [ ] **Step 7: Run GREEN and commit**

```powershell
git add -- BookOfEternityClient.Tests/EffectSourceAuthorityTests.Wounds.cs BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.Wounds.cs BookOfEternityClient/Services/EffectSourceAuthority.cs BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs BookOfEternityClient/UI/EffectPlayerProjection.cs BookOfEternityClient/UI/ExplorerMortalEffectDetailActions.cs BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.Effects.cs BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.Effects.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: keep wound and effect lifecycle independent (#1535)"
```

---

### Task 13: Integrate Bounded Repair, Full Retry, and Exact Rollback (T077–T088)

**Files:**
- Create: `BookOfEternityClient/Services/EffectRepairPacketBuilder.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- Create: `BookOfEternityClient.Tests/EffectRepairPacketBuilderTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectMaterializationRepairLifecycleTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectMaterializationRepairLifecycleTests.Privacy.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectMaterializationLifecycleTests.cs`

- [ ] **Step 1: Add repair-builder RED tests**

Allow only one exact source-owned semantic omission with an exact actor/path and bounded correction. Protect identity, source/target selector, realm, stack coordinate/policy, receipt/history, cycle/replay, direct carrier/index/pending mutation, ambiguous target, and any protected client-owned target path.

- [ ] **Step 2: Add real repair-loop RED tests**

Cover baseline-before-dispatch, unavailable snapshot no-dispatch, ready-only no-op rejection, partial-effect-only retry rejection, complete coherent response replay, canonical semantic freshness rather than formatting-only freshness, worker parity, and session replacement.

- [ ] **Step 3: Add failure-injection RED matrix**

Inject after each actual published player/NPC/combat/profile/condition carrier, identity index, pending root, effect-owned companion, narrative/interface output, and post-check. Capture the full tracked set before the turn and require byte/existence-exact restoration.

- [ ] **Step 4: Add full caller privacy RED tests**

Inject diagnostic write, repair-file cleanup, rollback evidence, and restore failures. The player output must contain fixed in-world Russian copy and no exception text, paths, IDs, codes, validation/materialization/repair/rollback/backup/harness/agent vocabulary. Exact detail remains operator-only and survives rollback.

- [ ] **Step 5: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectRepairPacketBuilderTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectMaterializationRepairLifecycleTests|FullyQualifiedName~EffectMaterializationLifecycleTests"
```

- [ ] **Step 6: Implement bounded packet classification**

Bind candidates to the exact accepted effect operation and source/target authority. If any issue or `RepairTargetFiles` entry is protected, ambiguous, historical, confusable, duplicate, or client-owned, return no actionable packet and persist a path-bound operator diagnostic.

- [ ] **Step 7: Restore before dispatch and require full replay**

Restore the validated baseline before any actionable request/worker dispatch. Keep one in-memory obligation covering every changed accepted-turn path, actor, route, and effect operation. A ready signal, formatting-only output rewrite, missing candidate, or partial location/effect response cannot become a successful no-op.

- [ ] **Step 8: Guarantee one caller-owned rollback**

Diagnostic/report/transient cleanup is best-effort for ordinary exceptions and must still return failure; preserve session-replacement propagation. Do not restore inside a helper and then restore again in the caller.

- [ ] **Step 9: Run GREEN and required lifecycle lane**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration -Filter "FullyQualifiedName~EffectMaterializationLifecycleTests"
```

Inspect exact test counts, duplicates, timeout, cleanup, and byte/existence assertions.

- [ ] **Step 10: Commit atomic failure handling**

```powershell
git add -- BookOfEternityClient/Services/EffectRepairPacketBuilder.cs BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs BookOfEternityClient.Tests/EffectRepairPacketBuilderTests.cs BookOfEternityClient.IntegrationTests/EffectMaterializationRepairLifecycleTests.cs BookOfEternityClient.IntegrationTests/EffectMaterializationRepairLifecycleTests.Privacy.cs BookOfEternityClient.IntegrationTests/EffectMaterializationLifecycleTests.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "feat: make effect repair and rollback atomic (#1535)"
```

---

### Task 14: Migrate Active Fixtures and Synchronize Every GM Contract (T097–T110)

**Files:**
- Modify: rules, guides, examples, manifest, daemon, active fixtures, and documentation tests named in T097–T110
- Modify: `FileSystemExample/` and `FileSystemExample/validator_fixtures/_shared/`
- Modify: `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`
- Modify: `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/FileSystemExampleFixtureIntegrityTests.cs`

- [ ] **Step 1: Add documentation/source-guard RED tests**

Require the common command fields, nine profiles, five stacks, eight lifetimes, wound independence, no legacy positive routes/sentinels, afterlife adapter, hidden privacy, Shining entitlement exclusion, and at least one worked example for every required operation/source family.

- [ ] **Step 2: Add fixture-integrity RED tests**

Require current empty or complete player/NPC/combat/profile/condition carriers, valid identity index and pending root, no receipt-less positive active instances, and no non-empty legacy state.

- [ ] **Step 3: Run RED source guards**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests|FullyQualifiedName~FileSystemExampleFixtureIntegrityTests"
```

- [ ] **Step 4: Migrate active fixtures without runtime migration**

Rewrite active new-game/template player, NPC, combat, afterlife profile/conflict, effect index, and pending roots to the current schema. Retain legacy shapes only in explicitly named negative tests.

- [ ] **Step 5: Update Mortal GM rules and CLI contract**

Update Rules Blocks 2, 5, 6, 7, 8, 10, 12, 14, 15, 17, 19.C, 25, 25.A, and CLI Operations. Explain definitions versus active instances, exact source/target selectors, all stack/lifetime policies, triggers, bounded receipts, removal, wound boundary, and forbidden direct post-state.

- [ ] **Step 6: Update afterlife guides and daemon reminders**

Update `OtherGuides/Afterlife_Contract_Matrix.md`, the combat terminology glossary, applicable task guides, CLI/daemon specs, and `game_master_daemon.ps1`. Force the GM to read the common effect contract before authoring profile effects or spiritual conditions.

- [ ] **Step 7: Add worked examples and manifest entries**

Include Mortal buff/debuff, periodic, event reaction, environmental, skill/art/item/Fate Card, quest, wound, stack, refresh, replace, merge, expiry, dispel, bounded repair, persistent afterlife profile effect, and all five spiritual-condition kinds. Every example must be executable against the current validator.

- [ ] **Step 8: Run GREEN and FullValidation**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
```

Record exact evidence. FullValidation is required here because shared examples/manifests and validation-documentation boundaries changed.

- [ ] **Step 9: Commit synchronized contracts**

```powershell
git add -- Rules TaskGuides OtherGuides Examples FileSystemExample CLI_API_Specification.md CLI_Agent_Daemon_Specification.md BookOfEternityClient/game_master_daemon.ps1 BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs BookOfEternityClient.IntegrationTests/FileSystemExampleFixtureIntegrityTests.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "docs: teach complete effect materialization (#1535)"
```

---

### Task 15: Prove Scaling and Complete Local Verification (T111–T115)

**Files:**
- Create: `BookOfEternityClient.Tests/EffectMaterializationPerformanceTests.cs`
- Modify: one-pass catalog/scheduler files only if the RED scan counters prove a missing bound
- Modify: `docs/superpowers/plans/2026-08-14-complete-effect-materialization.md`
- Modify: `specs/1535-complete-effect-materialization/tasks.md`

- [ ] **Step 1: Add scaling RED test**

Construct representative N and 2N effects with components and triggers. Assert the source, target, owner, stack, event, and identity catalogs scan each collection once and measured work remains at or below 2.5x. Prefer explicit scan counters over flaky stopwatch-only thresholds.

- [ ] **Step 2: Run RED then GREEN**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectMaterializationPerformanceTests"
```

RED must fail on a missing counter/one-pass guarantee before the production fix; GREEN must assert exact scan counts and the ratio bound.

- [ ] **Step 3: Run every focused unit filter from `plan.md`**

Run unit and integration filters separately because Focused selects one project at a time. Inspect every `summary.json`; do not infer success from console silence.

- [ ] **Step 4: Run conditional broad controls once**

Run the required effect-filtered LifecycleIntegration if Task 13 evidence is not already fresh after its final changes. Run FullValidation if Task 14 evidence predates its final documentation edits. Run the frontend verifier only if Task 11 changed frontend source or typed contracts.

- [ ] **Step 5: Perform manual Mortal checks**

Exercise visible/hidden player, NPC, and combatant effects; stack/lifetime facts; cure/dispel eligibility; stale action failure; malformed all-or-nothing state; wound unchanged after effect-only removal; and recursive DTO privacy in console and browser.

- [ ] **Step 6: Perform manual afterlife checks**

Exercise one persistent profile effect and all five spiritual condition kinds, including hidden state, lifecycle advancement, terminal removal, stale conflict, and parity. Confirm Shining blessing entitlement remains separate.

- [ ] **Step 7: Record evidence and commit the verification slice**

Record command, result directory, total/passed/failed, duplicates, timeout, cleanup, wall, and manual rationale in this plan. Commit only after inspecting all artifacts.

```powershell
git add -- BookOfEternityClient.Tests/EffectMaterializationPerformanceTests.cs BookOfEternityClient/Services/EffectCarrierCatalog.cs BookOfEternityClient/Services/EffectSourceAuthority.cs BookOfEternityClient/Services/EffectTargetAuthority.cs BookOfEternityClient/Services/EffectIdentityState.cs BookOfEternityClient/Services/EffectLifecycleScheduler.cs specs/1535-complete-effect-materialization/tasks.md docs/superpowers/plans/2026-08-14-complete-effect-materialization.md
git diff --cached --check
git commit -m "test: verify complete effect materialization (#1535)"
```

---

### Task 16: Review, Reconcile Spec Kit, Run One PreMerge, and Integrate (T116–T119)

**Files:**
- Modify only as findings require: feature production/tests/docs, `specs/1535-complete-effect-materialization/`, and this plan

- [ ] **Step 1: Request a fresh merge-gate review**

Use `requesting-code-review` against issue #1535, the approved design, spec, plan, tasks, contracts, full production/test/docs diff, dirty/untracked state, and exact verification artifacts. Reviewers report only evidence-backed Critical/Important/Minor findings.

- [ ] **Step 2: Resolve findings through TDD**

For each accepted Critical or Important, add the smallest failing regression, observe RED, implement the narrow fix, observe GREEN, and rerun the affected broad control only when its boundary changed. Do not patch around an authority failure with prompt-only wording.

- [ ] **Step 3: Re-run read-only Spec Kit analysis**

Use `speckit-analyze`; require no Critical/High/Medium inconsistency, every implemented FR mapped to evidence, no unchecked completed behavior, no placeholders, and explicit Mortal/afterlife documentation synchronization or no-update rationale.

- [ ] **Step 4: Inspect the clean candidate before PreMerge**

```powershell
git status --short
git diff --check
git diff --stat origin/main...HEAD
git log --oneline origin/main..HEAD
```

Expected: only #1535-owned tracked changes plus known unrelated generated artifacts; no production secret, no GitHub Actions workflow, no migration code, and no Arena AI mention.

- [ ] **Step 5: Run exactly one final PreMerge**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Require exit `0`, all discovered tests passed, zero duplicates, timeout false under the approved 20-minute lane, and cleanup complete. Do not run a duplicate Fast immediately beforehand or rerun a green PreMerge without a new candidate-changing fix.

- [ ] **Step 6: Verify completion before claiming it**

Use `verification-before-completion`. Re-open the final `summary.json`, inspect the full diff and commit list, confirm issue #1535 is still open until integration, and ensure every checked T001–T119 item has actual code/test/docs evidence.

- [ ] **Step 7: Prepare the PR/merge summary**

Include source issue, architecture, no-migration rationale, active/static boundary, Mortal and afterlife coverage, wound independence, GM prompt/docs/examples synchronization, focused/Fast/FullValidation/LifecycleIntegration/PreMerge evidence, manual parity/privacy evidence, and residual risks. State explicitly that GitHub Actions remained disabled and unused.

- [ ] **Step 8: Integrate only after user approval**

Use `finishing-a-development-branch` to present the verified integration choices. Do not push, create a PR, merge, close #1535, or mutate repository settings without the user's explicit choice at that point.

## Execution Notes

- `subagent-driven-development` is suitable only if the user explicitly authorizes subagents; otherwise execute inline with `executing-plans`.
- `[P]` markers in `tasks.md` describe dependency independence, not permission to spawn agents or edit shared files concurrently.
- Each execution turn should complete one coherent RED→GREEN slice and leave `tasks.md` plus this evidence log accurate.
- If a planned path or runtime assumption differs from the live tree, stop that slice, use `systematic-debugging`, and update the approved Spec Kit artifact before expanding scope.
- If the GM repeatedly misauthors the contract during manual checks, prefer validator/normalizer/repair/tooling hardening first, then synchronize prompts and examples.
