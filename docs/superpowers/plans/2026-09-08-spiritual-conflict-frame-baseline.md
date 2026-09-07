# Signed spiritual-conflict publication baseline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish a genuine signed, zero-error resource-publication and complete owning conflict-validation control for #1536 T081-A before extracting detached validation.

**Architecture:** Reuse the existing resource cutover fixture's validated capacity/history and real common publisher. A focused partial owns the complete duel scenario; no production validator, normalizer, gameplay rule or authority is changed. Both the raw resource phase and the entire selected final conflict phase must contain zero Error issues.

**Tech Stack:** C#, .NET, xUnit, System.Text.Json.Nodes, PowerShell 7 bounded test lanes.

## Global Constraints

- Source issue: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536).
- Governance: `.specify/memory/constitution.md`; design: `specs/1536-complete-wound-materialization/contracts/spiritual-wound-live-turn-boundary.md`.
- Same worktree `E:/Games/worktrees/boe-1536-wound-materialization`, branch `1536-complete-wound-materialization`.
- Only this task's implementer owns C# execution and source/test edits while its bounded run is active. No parallel C# controls.
- No changes to `.serena/`, runtime contracts, accepted arithmetic, snapshot verification or unrelated files. No remote operations.
- This test-only baseline is not wound-source admission and closes neither T081-A nor any top-level task. Detached-frame implementation has its own subsequent complete-code execution plan.
- Use `apply_patch`; keep signed fixture setup in test code. Never forge a common accepted plan, receipt or publication.
- This owns a resource-backed scenario, so its partial stays in Integration alongside `AfterlifeResourceCutoverTests`. Later detached-frame tests may live in `AfterlifeSpiritualConflictValidationTests.Wounds.cs` as already tracked.
- GM synchronization assessment: no capability, field, validation rule, command, normalizer or prompt behavior changes. No Mortal/afterlife prompt, example, matrix or manifest update is required for this test-only task.

### Task 1: Prove the complete signed pressure exchange survives common publication

**Files:**
- Modify: `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.cs` (class declaration only).
- Modify: `BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContext.cs` (optional signed dice fixture input only).
- Create: `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrame.cs`.

**Interfaces:**
- Consumes existing private `BuildActionPointState`, `Profiles`, `PlayerSoulProfile`, `GuardianProfile`, `ActiveConflict`, `SoulState`, `WriteComposedAuthorityAsync`, `CostAudit`, `PeekPlanAsync` and `ResolvePlannedActionPoints` through the same partial class.
- Consumes existing `ResourceMaterializationTestContext`, `AfterlifeSpiritualConflictState.ApplyUpdate` and `NormalizeAcceptedMechanicsAsync` APIs.
- Extends fixture `CaptureValidatedPendingSnapshotAsync(int turn = 42, string currentRealm = "Mortal World", IEnumerable<string>? additionalTrackedPaths = null, int[]? preGeneratedDices1d20 = null)`; existing null callers retain their exact previous request/manifest members.
- Produces a private fixture `CreateCompleteConflictFrameContextAsync()` and `WriteCompleteConflictFrameExchangeAsync(ResourceMaterializationTestContext context)` for this partial's follow-on tests; no public production API.

- [X] **Step 1: Add the zero-error characterization and genuine fixture**

Make the existing class declaration exactly:

```csharp
public sealed partial class AfterlifeResourceCutoverTests
```

Add `preGeneratedDices1d20` as the last optional parameter to the test context capture method. Replace its initial request write with:

```csharp
        var request = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turn,
            ["playerAction"] = playerAction,
            ["currentRealm"] = currentRealm
        };
        if (preGeneratedDices1d20 != null)
            request["preGeneratedDices1d20"] = JsonSerializer.SerializeToNode(preGeneratedDices1d20);
        await WriteExactJsonAsync("input/turn_request.json", request.ToJsonString());
```

Build the manifest in the typed reader's serialization order, then retain the
existing payload-hash assignment and authority synchronization. Supplied dice
belong between `playerAction` and `progressionControl`; null callers must retain
the previous field sequence exactly:

```csharp
        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turn,
            ["requestTimestamp"] = "2026-08-15T00:00:00Z",
            ["playerAction"] = playerAction
        };
        if (preGeneratedDices1d20 != null)
            manifest["preGeneratedDices1d20"] = JsonSerializer.SerializeToNode(preGeneratedDices1d20);
        manifest["progressionControl"] = JsonSerializer.SerializeToNode(
            new ProgressionControl { CurrentRealm = currentRealm });
        manifest["files"] = files;
        manifest["snapshotFileHashes"] = snapshotFileHashes;
        manifest["clientOwnedValidationHashes"] = new JsonObject();
        manifest["rollbackBackups"] = new JsonObject();
        manifest["rollbackBaselineFiles"] = rollbackBaselineFiles;
        manifest["sourceLabel"] = "Unified resource materialization integration test";
        manifest["manifestPayloadHash"] = string.Empty;
```

Do not change the established hash/authority helpers, byte snapshots or rollback membership. Add this complete new partial:

```csharp
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Fact]
    public async Task ConflictFrame_SignedPressureExchangePublishesWithNoOwningPhaseErrors()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);

        var rawIssues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoConflictFrameErrors(rawIssues);
        var plan = await PeekPlanAsync(context);
        Assert.Equal(3m, ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeActor).Current);
        Assert.Equal(3m, ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeConflictSide).Current);

        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Same(plan, await context.Normalizer.BindTo(lease)
                .NormalizeAcceptedMechanicsAsync(backups: null));
        }

        var published = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath));
        Assert.False(published.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        var active = Assert.IsType<JsonObject>(published["activeConflict"]);
        Assert.Equal("strained", active["oppositionSideStrain"]!.GetValue<string>());
        var exchange = Assert.IsType<JsonObject>(Assert.Single(active["exchangeLog"]!.AsArray()));
        Assert.Equal("exchange_conflict_frame_42", exchange["exchangeId"]!.GetValue<string>());
        Assert.Equal(10, exchange["diceAudit"]!["margin"]!.GetValue<int>());

        var finalIssues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState));
        AssertNoConflictFrameErrors(finalIssues);
    }

    private static void AssertNoConflictFrameErrors(IEnumerable<ValidationIssue> issues)
    {
        var errors = issues.Where(issue => issue.Severity == IssueSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine,
            errors.Select(issue => $"{issue.Code}: {issue}")));
    }

    private static async Task<ResourceMaterializationTestContext> CreateCompleteConflictFrameContextAsync()
    {
        var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
        var arts = new JsonObject();
        foreach (var artId in AfterlifeEntityProfileState.StandardArtIds.OrderBy(id => id, StringComparer.Ordinal))
            arts[artId] = 0;
        var player = PlayerSoulProfile();
        player["standardArts"] = arts.DeepClone();
        player["specialArts"] = new JsonArray();
        var guardian = GuardianProfile("guardian_frame");
        guardian["standardArts"] = arts.DeepClone();
        guardian["specialArts"] = new JsonArray();
        var profiles = Profiles(player, guardian);
        var soul = SoulState(spiritFocusTier: 0);
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"] = arts.DeepClone();
        var conflict = ActiveConflict("conflict_resource_cost");
        conflict["sideModel"] = "direct_duel";
        conflict["playerSideStrain"] = "clear";
        conflict["oppositionSideStrain"] = "clear";
        conflict["conflictPosition"] = "contested";
        conflict["playerSide"] = new JsonObject
        {
            ["leadContestant"] = new JsonObject
            {
                ["actorType"] = "player",
                ["actorId"] = "player_soul",
                ["displayName"] = "Душа игрока"
            },
            ["supporters"] = new JsonArray()
        };
        conflict["oppositionSide"] = new JsonObject
        {
            ["leadContestant"] = new JsonObject
            {
                ["actorType"] = "guardian",
                ["actorId"] = "guardian_frame",
                ["displayName"] = "Хранитель",
                ["actorArtTierSnapshot"] = arts.DeepClone(),
                ["artAuthoritySource"] = "afterlife_entity_profiles"
            },
            ["supporters"] = new JsonArray()
        };
        var (state, history) = BuildActionPointState(definitions, profiles, conflict, soul, 6m, 6m);
        await context.WriteExactJsonAsync(ResourceMaterializationTestContext.DefinitionsPath, definitions.ToCanonicalJson());
        await context.WriteExactJsonAsync(ResourceMaterializationTestContext.StatePath, state.ToCanonicalJson());
        await context.WriteExactJsonAsync(ResourceMaterializationTestContext.HistoryPath, history.ToCanonicalJson());
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["activeConflict"] = conflict,
            ["recentConflicts"] = new JsonArray()
        }.ToJsonString());
        await WriteComposedAuthorityAsync(context, definitions, state, history);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8]);
        return context;
    }

    private static async Task WriteCompleteConflictFrameExchangeAsync(ResourceMaterializationTestContext context)
    {
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var before = new JsonObject
        {
            ["playerSideStrain"] = "clear",
            ["oppositionSideStrain"] = "clear",
            ["conflictPosition"] = "contested"
        };
        var after = before.DeepClone().AsObject();
        after["oppositionSideStrain"] = "strained";
        var exchange = new JsonObject
        {
            ["exchangeId"] = "exchange_conflict_frame_42",
            ["turnNumber"] = 42,
            ["operationType"] = "pressure",
            ["outcome"] = "success",
            ["before"] = before,
            ["after"] = after,
            ["matchupAudit"] = new JsonObject
            {
                ["playerOperation"] = "pressure",
                ["oppositionOperation"] = "pressure",
                ["primaryResolutionLane"] = "pressure",
                ["matchupRationale"] = "Давление души преодолевает встречное давление хранителя.",
                ["riskProfile"] = "offensive_pressure"
            },
            ["actionCostAudit"] = new JsonObject
            {
                ["player"] = CostAudit("pressure", 3m, 6m, 3m),
                ["opposition"] = CostAudit("pressure", 3m, 6m, 3m)
            },
            ["diceAudit"] = new JsonObject
            {
                ["formulaVersion"] = "afterlife_spiritual_conflict_v1",
                ["diceSource"] = "input/turn_request.json.preGeneratedDices1d20",
                ["diceUsed"] = new JsonArray
                {
                    new JsonObject { ["side"] = "player", ["sourceIndex"] = 0, ["sides"] = 20, ["value"] = 15 },
                    new JsonObject { ["side"] = "opposition", ["sourceIndex"] = 1, ["sides"] = 20, ["value"] = 5 }
                },
                ["playerTotal"] = 15,
                ["oppositionTotal"] = 5,
                ["margin"] = 10,
                ["outcomeBand"] = "decisive_player_success",
                ["modifierBreakdown"] = new JsonObject { ["player"] = new JsonArray(), ["opposition"] = new JsonArray() }
            }
        };
        var update = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
            ["exchange"] = exchange
        };
        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);
        var activeAfter = Assert.IsType<JsonObject>(projected["activeConflict"]).DeepClone().AsObject();
        activeAfter.Remove("combatConditions");
        update["activeConflictAfter"] = activeAfter;
        root[AfterlifeSpiritualConflictState.ResponseField] = update;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
    }
}
```

- [X] **Step 2: Run the exact characterization and inspect all errors**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_"
```

Expected: the genuine scenario passes with zero raw and final owning-phase errors. This is a characterization of existing behavior, not a fabricated semantic RED for a new runtime feature. If initial fixture data is incomplete, record setup failures truthfully and correct only that data using existing rules. Never weaken assertions or production validation to make the scenario pass. Escalate a genuine production incompatibility to the controller with exact issues.

- [X] **Step 3: Prove sensitivity and retain a signed-dice rejection control**

Add this test to the partial:

```csharp
    [Fact]
    public async Task ConflictFrame_FinalValidationRejectsDieOutsideSignedPool()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var update = Assert.IsType<JsonObject>(root[AfterlifeSpiritualConflictState.ResponseField]);
        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);
        projected.Remove(AfterlifeSpiritualConflictState.ResponseField);
        var exchange = projected["activeConflict"]!["exchangeLog"]![0]!;
        exchange["diceAudit"]!["diceUsed"]![0]!["value"] = 14;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, projected.ToJsonString());
        var issues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState));
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "afterlife_conflict_dice_value_not_authorized");
    }
```

First run this negative control before its `value = 14` line is added: its positive input must fail the expected-error assertion. Then add the mutation and rerun the same two-method filter. This proves fixture/negative-control sensitivity without changing production code. Record the first run as test sensitivity RED, not implementation RED. Final expected result: 2/2 passing.

Parent correction during execution: the pure reducer preserves response fields
unless its caller removes them. The projected negative-control root therefore
explicitly removes that field before validation. Its clean sensitivity RED must
have no unrelated Error issue; retain the earlier wrapper-contaminated trial
truthfully in evidence rather than using it as the sole positive-input control.

- [X] **Step 4: Verify neighbors, self-review and commit**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests"
```

Use the ordinary five-minute cap; the initial owning-class run measured 1:47
for 47 rows, so there is no evidence requiring a larger limit. The initial run
used the plan's unnecessarily large 15-minute cap; retain its actual limit and
duration honestly. No repeat is needed just to change that limit. Do not run
Fast here: the controller will run its one meaningful Fast checkpoint with the
subsequent production extraction, avoiding duplicate whole-lane work. Inspect
`summary.json` and TRX totals, errors, skips, duplicates and cleanup. No runtime
or GM contract changed, so FullValidation is not required for this test-only step.

Commit only the three owned test files with `test: prove signed spiritual conflict publication (#1536)`. Write a detailed report under the controller-provided worktree git-dir `sdd` path. Include initial fixture failures, sensitivity RED/GREEN, exact commands/artifact directories and self-review. Do not mark tracked tasks complete; the controller checks the diff and review gate.

## Controller self-check

- This bounded baseline directly supports T081-A and preserves the approved same-turn design without claiming the remaining source/continuation work.
- All new interfaces are test-owned and defined above. Existing private fixture methods are reused unchanged through a partial.
- No new production behavior requires a GM example; no wound contract is omitted from a runtime change because there is no runtime change here.
- Parent acceptance and extracted-frame equivalence remain separate gates after genuine fixture evidence.

## Acceptance — 2026-09-08

The test-only task is accepted at `a54fc3d5fe17c97098fe1f3a01ee74a1c23d02d2`
from BASE `31c1940ab6d8e93766b5885a3d5ead7a5329fc36`. Parent inspected all three
actual changed files and raw summary/log/TRX evidence. Independent task-scoped
review found Spec compliant / Quality Approved, no Critical/Important/Minor
findings. Its only cannot-verify item was test artifacts, resolved by the parent.
T081-A and all top-level spiritual source/decision tasks remain open.

Artifacts below live under `TestResults/test-lanes/`:

| Artifact directory | Actual result |
| --- | --- |
| `20260908-022233-023-19856-8630ba6f614e4f9cb37fb3186a414ca0-focused` | Setup RED, 0/1: signed dice appended in the wrong manifest member order; no production change was needed. |
| `20260908-022645-520-50272-6fa2060a8dad40a3aa7ab9e6711019eb-focused` | Build-only failure, CS1061 for unavailable JsonObject.Insert; no tests/TRX. |
| `20260908-022756-932-40524-7e4e7190bb5a4eada771489624af66c5-focused` | Genuine zero-error characterization 1/1 PASS in 56.711s. |
| `20260908-022927-067-33072-b40d2e45775e4c23af025ce8cf6861ed-focused` | Contaminated sensitivity trial, 1 PASS/1 FAIL; an unrelated response-wrapper Error remained. Not the accepted clean RED. |
| `20260908-023055-199-33660-5e39aa999f754b0297bab62057f93a90-focused` | Preliminary mutation 2/2 PASS before wrapper isolation. |
| `20260908-023200-888-48144-838f3b57d15444dc805d7e2b4d64e84b-focused` | Owning class 47/47 PASS in 1:47.087, initially overlarge 15-minute cap. |
| `20260908-023418-248-9684-7931c1cca25e4ba8a454f026864180bc-focused` | Clean sensitivity RED, 1 PASS/1 expected FAIL; `Assert.Contains` saw `Collection: []` before the die mutation. |
| `20260908-023536-357-45696-3419382b782a4dab817de706d8b0ba83-focused` | Final corrected 2/2 PASS in 51.999s, build zero warnings/errors. |

All eight runners completed owned-tree cleanup, with no timeout or duplicate
test IDs. Parent reconciled each available TRX row/ID count with its summary;
the build-only trial correctly has zero tests and no TRX. Final narrow evidence
covers the one-line negative-control correction after the 47-row owner run;
unchanged neighbors are not rerun solely for a timeout argument. No Fast or
FullValidation was added to this test-only prerequisite. No GM-authored contract
changed, so neither Mortal nor afterlife prompts/examples/manifests need edits.
