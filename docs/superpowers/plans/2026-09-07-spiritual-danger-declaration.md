# Spiritual Conflict Danger Declaration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Require a declared spiritual-conflict danger mode and preserve it through ordinary exchange, terminal proof and validated-turn checks.

**Architecture:** Share one small exact-token/cap policy, carry the accepted declaration in the existing reducer, and reuse the already-parsed signed pre-turn root for same-identity continuity. Current canonical objects are cut over explicitly; no runtime fallback or new authority transport is added.

**Tech Stack:** C#/.NET 8, System.Text.Json.Nodes, existing file-backed ValidationService, xUnit, PowerShell 7 bounded lanes.

**Status (2026-09-07):** Final-state implementation accepted at
`a234fbe0..1576aa57`, independent Spec compliant / Quality Approved. Step 2
retains a documented historical sequencing exception; it is not pending work
to rerun or retroactively relabel. All broader feature tasks remain open.

## Global Constraints

- Tracked source: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), bounded T077/T083/T085 declaration prerequisite and T089-T092 GM synchronization; specs/1536-complete-wound-materialization/spec.md, plan.md and tasks.md remain feature authority.
- **FR-029**: Every spiritual conflict MUST fix one danger mode at start, and any escalation MUST be explicit and accepted before its higher cap applies.
- **FR-030**: Training MUST forbid spiritual wounds; controlled conflict MUST cap them at II; hostile and annihilation conflict MAY permit I-IV.
- Exact dangerMode strings are training, controlled, hostile, annihilation: JSON string, ordinal lowercase, no trim, no case folding, no implicit default or save migration.
- New starts declare the selected seed's dangerMode. Ordinary exchanges preserve it; omitted partial-replacement fields inherit the accepted declaration, while explicit null or different echoes fail. Resolve/repair_cancel carry it into recent proof and cannot override it.
- Same-ID retained active/recent entries compare against all signed pre-turn declarations, with existing case-insensitive conflict identity semantics. Ambiguous/invalid retained baseline fails closed; every current duplicate is checked. Legal removal of old recent entries remains unchanged.
- Reuse the already-parsed pre-turn root in the existing terminal-integrity method. Preserve all existing missing/malformed-state, removal, terminal-proof, capstone, reward, resource-prefix and dissipation checks. No new snapshot loader, public DTO, authority cache or pending file.
- This prerequisite does not implement accepted escalation evidence, per-side seals, wound production, full defeat/dissipation semantics, art schema/progression, healing or player UI. All broader T077/T083/T085/T086/T088/T089-T092/T177/#1536 tasks remain open.
- Work only in E:/Games/worktrees/boe-1536-wound-materialization on 1536-complete-wound-materialization. Preserve unrelated .serena/. No new branch, push, PR, merge or issue closure.
- Existing fixture declarations are explicit test scaffolding: hostile for generic current canonical state, training for the evidenced training seed, annihilation for dissipation proofs and their signed baseline, and the existing caller-selected dangerMode for the wound scenario. No fixture meaning is promoted to production authority.
- Synchronize GM turn/API/daemon guidance, afterlife matrix/glossary, worked start example, runtime manifest persistence assertion and source guard. Mortal contracts and commands are unchanged; existing daemon entrypoints load these guides.
- C# has one owner, PowerShell 7 and scripts/test-csharp.ps1 only. Drain every returned session completely before another lane, build, formatting or source edit. No overlapping lanes or edits during tests.
- Use bounded Focused RED/GREEN/owners, one unchanged five-minute Fast and one conditional fifteen-minute FullValidation. Do not run PreMerge or the complete RegressionIntegration suite at this checkpoint; do not narrow assertions or recategorize tests to obtain green.

## Design and boundaries

The full #1536 specification is already approved for autonomous execution. This
is a bounded delivery of its existing four-mode declaration, not a new gameplay
choice. Three approaches were considered: infer danger from prose/strain (cannot
prove the pre-roll declaration); invent a separate danger registry (duplicates
existing conflict identity and snapshot authority); extend the existing canonical
conflict/reducer and signed snapshot (selected).

Start accepts the first existing seed alias in the established order:
conflictState, activeConflict, conflictSeed. All current canonical active and
recent objects require the declaration. Ordinary replacement omission preserves
the previous value, unlike missing declaration on a new start. Root/exchange/
before/after/both replacement aliases and terminal proof echoes cannot change it.

The continuity check walks current objects against pre-turn identity groups.
Each group must contain one valid exact mode across every occurrence. Current
invalid tokens get the canonical error, not a duplicate continuity error; invalid
or conflicting retained pre-turn authority gets a distinct error. Dropped
pre-turn rows do not need to remain, preserving the existing 20-row window.
This is not a global conflict-ID uniqueness or history-retention design:
renaming/removing historical identities is outside same-ID continuity.
Existing terminal proof qualification is deliberately independent and unchanged.

The accepted escalation event, full per-side seal and bounded defeat registry
remain open T083/T088 work. A submitted boolean is not such authority. The art
scalar-versus-tier/experience decision and the unanswered legacy-preparation
question are not decided here. Requiring a declaration is not the full T086
player-visible pre-first-exchange workflow.

## File map

- Create BookOfEternityClient/Services/SpiritualConflictDangerPolicy.cs: one closed cap/token policy shared by state, validation and existing pure math.
- Modify BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs: start/exchange/terminal declaration admission and carry-forward.
- Modify BookOfEternityClient/Services/SpiritualWoundOpportunityMath.cs: delegate only the unchanged danger-cap lookup.
- Modify BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs: private integrity rename and narrow declaration hooks.
- Create BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.Danger.cs: strict raw/canonical admission and signed-prestate continuity.
- Create BookOfEternityClient.Tests/SpiritualConflictDangerModeTests.cs: 66 deterministic reducer cases in Fast.
- Create BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.DangerMode.cs: 50 real response/file/snapshot cases in the existing Integration partial.
- Create BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.SpiritualDangerDeclaration.cs: one deterministic GM/example/manifest guard.
- Modify BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.cs: explicit current fixture declarations only, with the shown dissipation baseline correction.
- Modify BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictBalanceTests.cs: explicit current fixture declarations only, with the shown dissipation baseline correction.
- Modify BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.cs: explicit current fixture declarations only, with the shown dissipation baseline correction.
- Modify BookOfEternityClient.IntegrationTests/ResourceAfterlifeOwnerTests.cs: explicit current fixture declarations only, with the shown dissipation baseline correction.
- Modify BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Conditions.cs: explicit current fixture declarations only, with the shown dissipation baseline correction.
- Modify BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Projection.cs: explicit current fixture declarations only, with the shown dissipation baseline correction.
- Modify BookOfEternityClient.IntegrationTests/EffectMaterializationLifecycleTests.cs: explicit current fixture declarations only, with the shown dissipation baseline correction.
- Modify BookOfEternityClient.Tests/EffectMechanicsSnapshotTests.Afterlife.cs: explicit current fixture declarations only, with the shown dissipation baseline correction.
- Modify BookOfEternityClient.Tests/LiveTurnPreparationServiceTests.cs: explicit current fixture declarations only, with the shown dissipation baseline correction.
- Modify BookOfEternityClient.IntegrationTests/WoundMaterializationTestFixtures.cs: explicit current fixture declarations only, with the shown dissipation baseline correction.
- Modify BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.LightIncarnateHistoricalAuthority.cs: explicit current fixture declarations only, with the shown dissipation baseline correction.
- Modify CLI_API_Specification.md, CLI_Agent_Daemon_Specification.md, TaskGuides/CLI_Step_Main.txt, OtherGuides/Afterlife_Contract_Matrix.md, OtherGuides/Afterlife_Combat_Terminology_Glossary.md and Examples/E_CLI_Afterlife_Turns.txt: precise declaration guidance and worked persistence result.
- Modify Examples/example_validation_manifest.json: retain the existing runtime start scenario and add a canonical persisted-value assertion.
- Parent alone owns this plan, feature spec/plan/tasks and progress/review metadata; implementer owns the 26 listed runtime/test/GM paths and its supplied report.

### Task 1: Admit and preserve explicit danger declarations

**Interfaces:**
- Consume existing AfterlifeSpiritualConflictState.ApplyUpdate(JsonObject, JsonObject), immutable snapshot lookup/readers, TryReadConflictId(JsonObject), TryGetObject(JsonElement, string, out JsonElement), TryGetString(JsonElement, string), and current test snapshot helpers.
- Produce internal SpiritualConflictDangerPolicy.SeverityCap(string?) returning -1 for invalid or 0/2/4 for valid modes, ReadDeclaration(JsonObject?) returning a valid exact token or null, and IsOmittedOrExactEcho(JsonObject?, string).
- Rename only the private async terminal-integrity method to ValidateAfterlifeConflictPreTurnIntegrityAsync(JsonObject?, List<ValidationIssue>); three call sites change with it.
- New private validation hooks and helpers are completely defined below. Do not alter dice-context DTOs, grant math or the resource publisher.

- [x] **Step 1: Add the complete RED regression tests.**

Create BookOfEternityClient.Tests/SpiritualConflictDangerModeTests.cs exactly:
```csharp
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualConflictDangerModeTests
{
    [Theory]
    [InlineData("training", "conflictSeed")]
    [InlineData("controlled", "conflictSeed")]
    [InlineData("hostile", "conflictSeed")]
    [InlineData("annihilation", "conflictSeed")]
    [InlineData("training", "conflictState")]
    [InlineData("controlled", "conflictState")]
    [InlineData("hostile", "conflictState")]
    [InlineData("annihilation", "conflictState")]
    [InlineData("training", "activeConflict")]
    [InlineData("controlled", "activeConflict")]
    [InlineData("hostile", "activeConflict")]
    [InlineData("annihilation", "activeConflict")]
    public void Start_PreservesEachExactDeclaration(string mode, string seedProperty)
    {
        var baseline = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        var update = new JsonObject { ["mode"] = "start", [seedProperty] = Active(mode) };
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.False(result.ContainsKey("lastInvalidUpdate"));
        Assert.Equal(mode, result["activeConflict"]!["dangerMode"]?.GetValue<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"Training\"")]
    [InlineData("\"training \"")]
    [InlineData("\" hostile\"")]
    [InlineData("\"unknown\"")]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Start_RejectsMissingOrNonExactDeclaration(string? raw)
    {
        var active = Active("training");
        SetRaw(active, raw);
        var baseline = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        var result = ApplyWithoutMutatingInputs(
            baseline, new JsonObject { ["mode"] = "start", ["conflictSeed"] = active });
        Assert.Equal("start_invalid_danger_mode", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Null(result["activeConflict"]);
        Assert.Empty(result["recentConflicts"]!.AsArray());
    }

    [Fact]
    public void Start_RejectsAConflictingRootEcho()
    {
        var result = ApplyWithoutMutatingInputs(
            AfterlifeSpiritualConflictState.CreateDefaultRoot(),
            new JsonObject
            {
                ["mode"] = "start",
                ["dangerMode"] = "hostile",
                ["conflictSeed"] = Active("training")
            });
        Assert.Equal("start_danger_mode_mismatch", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.Null(result["activeConflict"]);
    }

    [Theory]
    [InlineData("patch")]
    [InlineData("no_effect")]
    [InlineData("activeConflictAfter")]
    [InlineData("conflictStateAfter")]
    public void Exchange_OmissionPreservesTheAcceptedDeclaration(string route)
    {
        var baseline = Root("controlled");
        var update = Exchange();
        if (route == "no_effect")
            update["exchange"]!["outcome"] = "no_effect";
        else if (route != "patch")
        {
            var replacement = Active("controlled");
            replacement.Remove("dangerMode");
            update[route] = replacement;
        }
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.False(result.ContainsKey("lastInvalidUpdate"));
        Assert.Equal("controlled", result["activeConflict"]!["dangerMode"]?.GetValue<string>());
        Assert.Single(result["activeConflict"]!["exchangeLog"]!.AsArray());
    }

    [Theory]
    [InlineData("root", "\"hostile\"")]
    [InlineData("root", "null")]
    [InlineData("exchange", "\"hostile\"")]
    [InlineData("exchange", "null")]
    [InlineData("before", "\"hostile\"")]
    [InlineData("before", "null")]
    [InlineData("after", "\"hostile\"")]
    [InlineData("after", "null")]
    [InlineData("activeConflictAfter", "\"hostile\"")]
    [InlineData("activeConflictAfter", "null")]
    [InlineData("conflictStateAfter", "\"hostile\"")]
    [InlineData("conflictStateAfter", "null")]
    public void Exchange_RejectsChangedOrNullDeclarationOnEveryCarrier(string carrier, string raw)
    {
        var baseline = Root("training");
        var update = Exchange();
        Carrier(update, carrier)["dangerMode"] = JsonNode.Parse(raw);
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.Equal("exchange_danger_mode_change_without_authority", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(baseline["activeConflict"], result["activeConflict"]));
        Assert.True(JsonNode.DeepEquals(baseline["recentConflicts"], result["recentConflicts"]));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("exchange")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("activeConflictAfter")]
    [InlineData("conflictStateAfter")]
    public void Exchange_AcceptsExactEchoOnEveryCarrier(string carrier)
    {
        var update = Exchange();
        Carrier(update, carrier)["dangerMode"] = "training";
        var result = ApplyWithoutMutatingInputs(Root("training"), update);
        Assert.False(result.ContainsKey("lastInvalidUpdate"));
        Assert.Equal("training", result["activeConflict"]!["dangerMode"]?.GetValue<string>());
    }

    [Fact]
    public void Exchange_NoEffectDoesNotHideAChangedDeclaration()
    {
        var update = Exchange();
        update["exchange"]!["outcome"] = "no_effect";
        update["exchange"]!["after"]!["dangerMode"] = "annihilation";
        var baseline = Root("training");
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.Equal("exchange_danger_mode_change_without_authority", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(baseline["activeConflict"], result["activeConflict"]));
    }

    [Theory]
    [InlineData("training", false)]
    [InlineData("controlled", false)]
    [InlineData("hostile", false)]
    [InlineData("annihilation", false)]
    [InlineData("training", true)]
    [InlineData("controlled", true)]
    [InlineData("hostile", true)]
    [InlineData("annihilation", true)]
    public void TerminalClosure_CarriesDeclarationIntoTheProof(string mode, bool repair)
    {
        var result = ApplyWithoutMutatingInputs(Root(mode), Terminal(repair));
        Assert.False(result.ContainsKey("lastInvalidUpdate"));
        Assert.Null(result["activeConflict"]);
        var proof = Assert.Single(result["recentConflicts"]!.AsArray())!;
        Assert.Equal(mode, proof["dangerMode"]?.GetValue<string>());
        Assert.Equal(repair ? "repair_cancelled" : "resolved", proof["resolutionState"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false, false, "\"hostile\"")]
    [InlineData(false, false, "null")]
    [InlineData(false, true, "\"hostile\"")]
    [InlineData(false, true, "null")]
    [InlineData(true, false, "\"hostile\"")]
    [InlineData(true, false, "null")]
    [InlineData(true, true, "\"hostile\"")]
    [InlineData(true, true, "null")]
    public void TerminalClosure_RejectsConflictingOrNullEcho(bool repair, bool atRoot, string raw)
    {
        var baseline = Root("training");
        var update = Terminal(repair);
        (atRoot ? update : update["resolution"]!.AsObject())["dangerMode"] = JsonNode.Parse(raw);
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.Equal("resolve_danger_mode_change_without_authority", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(baseline["activeConflict"], result["activeConflict"]));
        Assert.Empty(result["recentConflicts"]!.AsArray());
    }

    [Theory]
    [InlineData("exchange")]
    [InlineData("resolve")]
    [InlineData("repair_cancel")]
    public void ExistingConflictWithoutDeclarationHasNoFallback(string lifecycle)
    {
        var baseline = Root("training");
        baseline["activeConflict"]!.AsObject().Remove("dangerMode");
        var update = lifecycle == "exchange" ? Exchange() : Terminal(lifecycle == "repair_cancel");
        var result = ApplyWithoutMutatingInputs(baseline, update);
        Assert.Equal("active_conflict_invalid_danger_mode", result["lastInvalidUpdateReason"]?.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(baseline["activeConflict"], result["activeConflict"]));
    }

    private static JsonObject Root(string mode)
    {
        var root = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        root["activeConflict"] = Active(mode);
        return root;
    }

    private static JsonObject Active(string mode) => new()
    {
        ["conflictId"] = "danger_mode_conflict",
        ["dangerMode"] = mode,
        ["realm"] = "Chaos Sea",
        ["sideModel"] = "direct_duel",
        ["status"] = "active",
        ["resolutionState"] = "active",
        ["oppositionSide"] = new JsonObject
        {
            ["leadContestant"] = new JsonObject
            {
                ["actorType"] = "guardian",
                ["actorId"] = "guardian_danger"
            }
        },
        ["exchangeLog"] = new JsonArray(),
        ["combatConditions"] = new JsonArray()
    };

    private static JsonObject Exchange() => new()
    {
        ["mode"] = "exchange",
        ["exchange"] = new JsonObject
        {
            ["exchangeId"] = "danger_exchange",
            ["outcome"] = "success",
            ["before"] = new JsonObject { ["conflictPosition"] = "contested" },
            ["after"] = new JsonObject { ["conflictPosition"] = "player_advantaged" }
        }
    };

    private static JsonObject Terminal(bool repair) => new()
    {
        ["mode"] = repair ? "repair_cancel" : "resolve",
        ["resolution"] = new JsonObject
        {
            ["resolvedAtTurn"] = 7,
            ["operationType"] = "guard",
            ["guardianId"] = "guardian_danger",
            ["playerOutcome"] = "won"
        }
    };

    private static JsonObject Carrier(JsonObject update, string carrier)
    {
        if (carrier == "root")
            return update;
        if (carrier == "exchange")
            return update["exchange"]!.AsObject();
        if (carrier is "before" or "after")
            return update["exchange"]![carrier]!.AsObject();
        update[carrier] = Active("training");
        return update[carrier]!.AsObject();
    }

    private static void SetRaw(JsonObject target, string? raw)
    {
        if (raw is null)
            target.Remove("dangerMode");
        else
            target["dangerMode"] = JsonNode.Parse(raw);
    }

    private static JsonObject ApplyWithoutMutatingInputs(JsonObject baseline, JsonObject update)
    {
        var before = baseline.ToJsonString();
        var input = update.ToJsonString();
        var result = AfterlifeSpiritualConflictState.ApplyUpdate(baseline, update);
        Assert.Equal(before, baseline.ToJsonString());
        Assert.Equal(input, update.ToJsonString());
        return result;
    }
}
```

Create BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.DangerMode.cs
exactly. These tests assert the named declaration boundary plus non-mutation;
they are not full wound publication tests. No mock or alternate snapshot reader
is used. Existing helper signatures remain unchanged.
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualConflictValidationTests
{
    [Theory]
    [InlineData("training", false)]
    [InlineData("controlled", false)]
    [InlineData("hostile", false)]
    [InlineData("annihilation", false)]
    [InlineData("training", true)]
    [InlineData("controlled", true)]
    [InlineData("hostile", true)]
    [InlineData("annihilation", true)]
    public async Task T083a_DangerMode_CanonicalExactTokensAreAccepted(string mode, bool recent)
    {
        await WriteSoulStateAsync();
        await AssertDangerModeStateAsync(DangerRoot(mode, recent));
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "null")]
    [InlineData(false, "\"Training\"")]
    [InlineData(false, "\"training \"")]
    [InlineData(false, "\"unknown\"")]
    [InlineData(false, "1")]
    [InlineData(false, "{}")]
    [InlineData(true, null)]
    [InlineData(true, "null")]
    [InlineData(true, "\"Training\"")]
    [InlineData(true, "\"training \"")]
    [InlineData(true, "\"unknown\"")]
    [InlineData(true, "1")]
    [InlineData(true, "{}")]
    public async Task T083a_DangerMode_CanonicalMissingOrInvalidTokenIsRejected(bool recent, string? raw)
    {
        await WriteSoulStateAsync();
        var root = DangerRoot("training", recent);
        SetDangerRaw(DangerEntry(root, recent), raw);
        await AssertDangerModeStateAsync(root, "afterlife_conflict_danger_mode_invalid", DangerPath(recent));
    }

    [Theory]
    [InlineData("conflictSeed", null)]
    [InlineData("conflictState", "null")]
    [InlineData("activeConflict", "\"Training\"")]
    [InlineData("conflictSeed", "\"training \"")]
    [InlineData("conflictState", "{}")]
    public void T083a_DangerMode_RawStartRequiresExactDeclaration(string seedProperty, string? raw)
    {
        var seed = DangerEntry(DangerRoot("training"), false).DeepClone().AsObject();
        SetDangerRaw(seed, raw);
        var response = new JsonObject
        {
            [AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
            {
                ["mode"] = "start",
                [seedProperty] = seed
            }
        };
        using var document = JsonDocument.Parse(response.ToJsonString());
        var issues = _validator.ValidateResponse(document.RootElement);
        var issue = Assert.Single(issues.Where(IsDangerModeIssue));
        Assert.Equal("afterlife_conflict_danger_mode_invalid", issue.Code);
        Assert.EndsWith("." + seedProperty + ".dangerMode", issue.FilePath, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("training", "conflictSeed")]
    [InlineData("controlled", "conflictSeed")]
    [InlineData("hostile", "conflictSeed")]
    [InlineData("annihilation", "conflictSeed")]
    [InlineData("controlled", "conflictState")]
    [InlineData("controlled", "activeConflict")]
    public void T083a_DangerMode_RawStartAcceptsExactDeclaration(string mode, string seedProperty)
    {
        var response = new JsonObject
        {
            [AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
            {
                ["mode"] = "start",
                [seedProperty] = DangerEntry(DangerRoot(mode), false).DeepClone()
            }
        };
        using var document = JsonDocument.Parse(response.ToJsonString());
        Assert.DoesNotContain(_validator.ValidateResponse(document.RootElement), IsDangerModeIssue);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task T083a_DangerMode_SameIdentityCannotChangeAcrossActiveAndRecent(
        bool beforeRecent, bool afterRecent)
    {
        await WriteSoulStateAsync();
        await SnapshotDangerRootAsync(DangerRoot("training", beforeRecent));
        var current = DangerRoot("hostile", afterRecent);
        DangerEntry(current, afterRecent)["conflictId"] = "AFTERLIFE_CONFLICT_TEST_001";
        var issues = await AssertDangerModeStateAsync(
            current, "afterlife_conflict_danger_mode_changed_without_authority", DangerPath(afterRecent));
        Assert.DoesNotContain(issues, issue => issue.Code == "afterlife_conflict_active_removed_without_terminal_proof");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T083a_DangerMode_TerminalProofPreservesTheSeparateRemovalCheck(bool repair)
    {
        await WriteSoulStateAsync();
        await SnapshotDangerRootAsync(DangerRoot("controlled"));
        var current = DangerRoot("controlled", recent: true);
        if (repair)
        {
            DangerEntry(current, true)["mode"] = "repair_cancel";
            DangerEntry(current, true)["resolutionState"] = "repair_cancelled";
        }
        var issues = await AssertDangerModeStateAsync(current);
        Assert.DoesNotContain(issues, issue => issue.Code == "afterlife_conflict_active_removed_without_terminal_proof");
    }

    [Fact]
    public async Task T083a_DangerMode_LaterDuplicateCannotHideBehindAnExactFirstProof()
    {
        await WriteSoulStateAsync();
        await SnapshotDangerRootAsync(DangerRoot("training"));
        var current = DangerRoot("training", recent: true);
        var changed = DangerEntry(current, true).DeepClone().AsObject();
        changed["dangerMode"] = "annihilation";
        current["recentConflicts"]!.AsArray().Add(changed);
        await AssertDangerModeStateAsync(
            current, "afterlife_conflict_danger_mode_changed_without_authority",
            AfterlifeSpiritualConflictState.StatePath + ".recentConflicts[1].dangerMode");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task T083a_DangerMode_DuplicateBaselineMustHaveOneUnambiguousDeclaration(bool conflict)
    {
        await WriteSoulStateAsync();
        var baseline = DangerRoot("training", recent: true);
        var second = DangerEntry(baseline, true).DeepClone().AsObject();
        second["dangerMode"] = conflict ? "hostile" : "training";
        baseline["recentConflicts"]!.AsArray().Add(second);
        await SnapshotDangerRootAsync(baseline);
        await AssertDangerModeStateAsync(
            DangerRoot("training", recent: true),
            conflict ? "afterlife_conflict_danger_mode_invalid_pre_turn_authority" : null,
            conflict ? DangerPath(true) : null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("\"Training\"")]
    [InlineData("\"training \"")]
    public async Task T083a_DangerMode_InvalidBaselineNeverCreatesAnImplicitDefault(string? raw)
    {
        await WriteSoulStateAsync();
        var baseline = DangerRoot("training");
        SetDangerRaw(DangerEntry(baseline, false), raw);
        await SnapshotDangerRootAsync(baseline);
        await AssertDangerModeStateAsync(
            DangerRoot("hostile"), "afterlife_conflict_danger_mode_invalid_pre_turn_authority", DangerPath(false));
    }

    [Fact]
    public async Task T083a_DangerMode_RecentTruncationAndFreshStartDoNotRequireRemovedIdentities()
    {
        await WriteSoulStateAsync();
        var baseline = DangerRoot("training", recent: true);
        var retained = DangerEntry(baseline, true).DeepClone().AsObject();
        retained["conflictId"] = "retained_conflict";
        retained["dangerMode"] = "controlled";
        baseline["recentConflicts"]!.AsArray().Add(retained);
        await SnapshotDangerRootAsync(baseline);

        var current = DangerRoot("hostile");
        DangerEntry(current, false)["conflictId"] = "fresh_conflict";
        current["recentConflicts"] = new JsonArray(retained.DeepClone());
        await AssertDangerModeStateAsync(current);
    }

    [Fact]
    public async Task T083a_DangerMode_EmptySignedBaselineAllowsFirstDeclaredStart()
    {
        await WriteSoulStateAsync();
        await SnapshotDangerRootAsync(AfterlifeSpiritualConflictState.CreateDefaultRoot());
        await AssertDangerModeStateAsync(DangerRoot("controlled"));
    }

    [Fact]
    public async Task T083a_DangerMode_ChangedDeclarationDoesNotSupplyMissingTerminalProof()
    {
        await WriteSoulStateAsync();
        await SnapshotDangerRootAsync(DangerRoot("training"));
        var current = DangerRoot("hostile", recent: true);
        DangerEntry(current, true).Remove("resolvedAtTurn");
        var issues = await AssertDangerModeStateAsync(
            current, "afterlife_conflict_danger_mode_changed_without_authority", DangerPath(true));
        Assert.Contains(issues, issue => issue.Code == "afterlife_conflict_active_removed_without_terminal_proof");
    }

    private static JsonObject DangerRoot(string mode, bool recent = false)
    {
        var root = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
        if (recent)
        {
            root["activeConflict"] = null;
            root["recentConflicts"] = new JsonArray(new JsonObject
            {
                ["conflictId"] = "afterlife_conflict_test_001",
                ["dangerMode"] = mode,
                ["realm"] = "Chaos Sea",
                ["sideModel"] = "direct_duel",
                ["mode"] = "resolve",
                ["resolutionState"] = "resolved",
                ["resolvedAtTurn"] = 7,
                ["operationType"] = "guard",
                ["guardianId"] = "guardian_liora",
                ["playerOutcome"] = "won"
            });
        }
        else
            root["activeConflict"]!["dangerMode"] = mode;
        return root;
    }

    private static JsonObject DangerEntry(JsonObject root, bool recent) =>
        (recent ? root["recentConflicts"]![0] : root["activeConflict"])!.AsObject();

    private static string DangerPath(bool recent) =>
        AfterlifeSpiritualConflictState.StatePath + (recent ? ".recentConflicts[0].dangerMode" : ".activeConflict.dangerMode");

    private static void SetDangerRaw(JsonObject entry, string? raw)
    {
        if (raw is null)
            entry.Remove("dangerMode");
        else
            entry["dangerMode"] = JsonNode.Parse(raw);
    }

    private async Task SnapshotDangerRootAsync(JsonObject root)
    {
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await WriteValidatedConflictSnapshotFromCurrentAsync("Сохранить объявленную опасность духовного конфликта.");
    }

    private async Task<IReadOnlyList<ValidationIssue>> AssertDangerModeStateAsync(
        JsonObject root, string? expectedCode = null, string? expectedPath = null)
    {
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        var before = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        var issues = await ValidateAfterlifeSpiritualConflictAsync();
        var dangerIssues = issues.Where(IsDangerModeIssue).ToArray();
        if (expectedCode is null)
            Assert.Empty(dangerIssues);
        else
        {
            var issue = Assert.Single(dangerIssues);
            Assert.Equal(expectedCode, issue.Code);
            Assert.Equal(expectedPath, issue.FilePath);
        }
        Assert.Equal(before, await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath));
        return issues;
    }

    private static bool IsDangerModeIssue(ValidationIssue issue) =>
        issue.Code?.StartsWith("afterlife_conflict_danger_mode_", StringComparison.Ordinal) == true;
}
```

Create BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.SpiritualDangerDeclaration.cs exactly:
```csharp
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeDocumentationCoverageTests
{
    [Fact]
    public void SpiritualDangerDeclarationDocumentation_RequiresExactModesAndPersistedExample()
    {
        const string declaration =
            "The spiritual conflict danger declaration is mandatory: put `dangerMode` in the selected " +
            "`conflictState`/`activeConflict`/`conflictSeed` of `mode=start`, canonical `activeConflict`, and every " +
            "`recentConflicts[]` proof. Use exactly `training`, `controlled`, `hostile`, or `annihilation`: " +
            "lowercase JSON strings without surrounding whitespace. There is no implicit mode or old-save fallback. " +
            "Ordinary exchanges and partial `activeConflictAfter`/`conflictStateAfter` replacements may omit the " +
            "field and preserve the accepted declaration; explicit echoes must match exactly, including in the " +
            "update root and exchange `before`/`after`. `resolve` and `repair_cancel` copy that declaration into " +
            "the terminal proof and cannot replace it. During a validated turn, every retained same-ID " +
            "active/recent occurrence is compared with the signed pre-turn declarations; a missing, invalid or " +
            "conflicting baseline fails closed, and a later duplicate cannot hide a change. Legal removal from the " +
            "bounded recent-history window is unchanged. Do not infer escalation after a roll or submit a boolean " +
            "as escalation authority. This declaration/persistence stage does not implement accepted escalation, " +
            "wound production or healing. The declared wound ceilings remain training 0, controlled II, " +
            "hostile/annihilation IV; wounds are optional GM choices within validated limits, and soul dissipation " +
            "remains separately authorized and always optional.";
        foreach (var text in new[]
        {
            ReadRepoFile("TaskGuides", "CLI_Step_Main.txt"),
            ReadRepoFile("CLI_API_Specification.md"),
            ReadRepoFile("CLI_Agent_Daemon_Specification.md"),
            ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"),
            ReadRepoFile("OtherGuides", "Afterlife_Combat_Terminology_Glossary.md"),
            ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt")
        })
            Assert.Contains(declaration, text, StringComparison.Ordinal);

        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        Assert.Contains("Danger declaration worked result:", examples, StringComparison.Ordinal);
        Assert.Contains("not a wound opportunity or permission to dissipate a soul", examples, StringComparison.Ordinal);
        using var manifest = JsonDocument.Parse(ReadRepoFile("Examples", "example_validation_manifest.json"));
        var scenario = Assert.Single(
            manifest.RootElement.GetProperty("runtimeScenarios").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "afterlife_spiritual_conflict_start_response");
        Assert.Equal("gameResponseDistribution", scenario.GetProperty("runner").GetString());
        Assert.Contains(scenario.GetProperty("requiredText").EnumerateArray(),
            token => token.GetString() == "\"dangerMode\": \"hostile\"");
        var persisted = Assert.Single(scenario.GetProperty("expectedFileContains").EnumerateArray(),
            item => item.GetProperty("path").GetString() == "game_state/meta/afterlife_spiritual_conflict_state.json");
        Assert.Contains(persisted.GetProperty("requiredText").EnumerateArray(),
            token => token.GetString() == "\"dangerMode\": \"hostile\"");
    }
}
```

- [ ] **Step 2: Run bounded RED and inspect the actual failures before production edits.**

**Recorded exception:** Fast semantic RED ran before implementation, but the
prescribed Integration RED50 did not. Later controlled mutation proves 31
negative cases fail and 19 positive cases remain passing; restored code passes
all50. Independent review accepts final-state behavior with this Minor process
deviation. This unchecked historical step must not trigger a repeat implementation
or be described as completed test-first evidence.

Run sequentially from the worktree with PowerShell 7:
```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~SpiritualConflictDangerModeTests|FullyQualifiedName~SpiritualDangerDeclarationDocumentation_"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~T083a_DangerMode_"
```

Expected: existing exact-token carry controls may already pass; missing/changed
mode admission, replacement/terminal carry and signed-prestate cases fail their
assertions because this boundary is absent. The GM guard fails because the
contract/persisted example is absent. Confirm 67 Fast and 50 Integration rows
were actually selected. A build error or unrelated setup failure is not semantic
RED; repair only transcription/setup and rerun the affected selection.

- [x] **Step 3: Add the shared policy and exact runtime implementation.**

Create BookOfEternityClient/Services/SpiritualConflictDangerPolicy.cs exactly:
```csharp
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class SpiritualConflictDangerPolicy
{
    internal static int SeverityCap(string? mode) => mode switch
    {
        "training" => 0,
        "controlled" => 2,
        "hostile" or "annihilation" => 4,
        _ => -1
    };

    internal static string? ReadDeclaration(JsonObject? conflict) =>
        conflict?["dangerMode"] is JsonValue value &&
        value.TryGetValue<string>(out var mode) &&
        SeverityCap(mode) >= 0
            ? mode
            : null;

    internal static bool IsOmittedOrExactEcho(JsonObject? candidate, string mode) =>
        candidate is null ||
        !candidate.ContainsKey("dangerMode") ||
        string.Equals(ReadDeclaration(candidate), mode, StringComparison.Ordinal);
}
```

In BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs replace only
the complete existing ApplyStart, ApplyExchange and ApplyResolve methods with
the following implementations. All neighboring helpers remain unchanged.
```csharp
    private static JsonObject ApplyStart(JsonObject root, JsonObject update)
    {
        if (root.TryGetPropertyValue("activeConflict", out var activeConflict) && activeConflict != null)
            return MarkInvalidUpdate(root, update, "start_while_conflict_active");

        var conflict = CloneObject(update["conflictState"] as JsonObject) ??
                       CloneObject(update["activeConflict"] as JsonObject) ??
                       CloneObject(update["conflictSeed"] as JsonObject);
        if (conflict == null)
            return MarkInvalidUpdate(root, update, "missing_conflict_state");

        if (string.IsNullOrWhiteSpace(GetNodeString(conflict["status"])))
            conflict["status"] = "active";
        if (string.IsNullOrWhiteSpace(GetNodeString(conflict["resolutionState"])))
            conflict["resolutionState"] = "active";
        var realm = GetNodeString(conflict["realm"]) ?? GetNodeString(update["realm"]);
        if (string.IsNullOrWhiteSpace(realm))
            return MarkInvalidUpdate(root, update, "start_missing_realm");
        if (!IsAfterlifeRealm(realm))
            return MarkInvalidUpdate(root, update, "start_invalid_realm");
        var dangerMode = SpiritualConflictDangerPolicy.ReadDeclaration(conflict);
        if (dangerMode is null)
            return MarkInvalidUpdate(root, update, "start_invalid_danger_mode");
        if (!SpiritualConflictDangerPolicy.IsOmittedOrExactEcho(update, dangerMode))
            return MarkInvalidUpdate(root, update, "start_danger_mode_mismatch");

        conflict["realm"] = realm;
        if (conflict["exchangeLog"] is not JsonArray)
            conflict["exchangeLog"] = new JsonArray();
        conflict["combatConditions"] = new JsonArray();
        NormalizeSupporterRoles(conflict["playerSide"] as JsonObject);
        NormalizeSupporterRoles(conflict["oppositionSide"] as JsonObject);

        root["activeConflict"] = conflict;
        ClearInvalidUpdateMarkers(root);
        return root;
    }


    private static JsonObject ApplyExchange(JsonObject root, JsonObject update)
    {
        if (root["activeConflict"] is not JsonObject active)
            return MarkInvalidUpdate(root, update, "exchange_without_active_conflict");

        var exchange = CloneObject(update["exchange"] as JsonObject);
        if (exchange == null)
            return MarkInvalidUpdate(root, update, "exchange_missing_exchange_object");

        var activeConflictId = GetNodeString(active["conflictId"]);
        var exchangeConflictId = GetExchangeConflictIdentity(exchange);
        if (!string.IsNullOrWhiteSpace(exchangeConflictId) &&
            (string.IsNullOrWhiteSpace(activeConflictId) ||
             !string.Equals(exchangeConflictId, activeConflictId, StringComparison.OrdinalIgnoreCase)))
        {
            return MarkInvalidUpdate(root, update, "exchange_conflict_id_mismatch");
        }

        var dangerMode = SpiritualConflictDangerPolicy.ReadDeclaration(active);
        if (dangerMode is null)
            return MarkInvalidUpdate(root, update, "active_conflict_invalid_danger_mode");
        if (!SpiritualConflictDangerPolicy.IsOmittedOrExactEcho(update, dangerMode) ||
            !SpiritualConflictDangerPolicy.IsOmittedOrExactEcho(exchange, dangerMode) ||
            !SpiritualConflictDangerPolicy.IsOmittedOrExactEcho(exchange["before"] as JsonObject, dangerMode) ||
            !SpiritualConflictDangerPolicy.IsOmittedOrExactEcho(exchange["after"] as JsonObject, dangerMode) ||
            !SpiritualConflictDangerPolicy.IsOmittedOrExactEcho(update["activeConflictAfter"] as JsonObject, dangerMode) ||
            !SpiritualConflictDangerPolicy.IsOmittedOrExactEcho(update["conflictStateAfter"] as JsonObject, dangerMode))
        {
            return MarkInvalidUpdate(root, update, "exchange_danger_mode_change_without_authority");
        }

        var log = active["exchangeLog"]?.DeepClone() as JsonArray ?? new JsonArray();
        var isNoEffectExchange = string.Equals(GetNodeString(exchange["outcome"]), "no_effect", StringComparison.OrdinalIgnoreCase);

        var replacement = CloneObject(update["activeConflictAfter"] as JsonObject) ??
                          CloneObject(update["conflictStateAfter"] as JsonObject);
        if (replacement != null)
        {
            if (isNoEffectExchange)
                return MarkInvalidUpdate(root, update, "exchange_no_effect_state_replacement");

            var replacementConflictId = GetNodeString(replacement["conflictId"]);
            if (string.IsNullOrWhiteSpace(activeConflictId) ||
                (!string.IsNullOrWhiteSpace(replacementConflictId) &&
                 !string.Equals(replacementConflictId, activeConflictId, StringComparison.OrdinalIgnoreCase)))
            {
                return MarkInvalidUpdate(root, update, "exchange_conflict_id_mismatch");
            }

            if (string.IsNullOrWhiteSpace(replacementConflictId))
                replacement["conflictId"] = active["conflictId"]?.DeepClone();

            replacement["dangerMode"] = dangerMode;
            ApplyExchangeControlStateToReplacement(replacement, exchange, active);
            log.Add(exchange.DeepClone());
            replacement["exchangeLog"] = MergeExchangeLogs(log, replacement["exchangeLog"] as JsonArray);
            replacement["combatConditions"] =
                active["combatConditions"]?.DeepClone() ?? new JsonArray();
            root["activeConflict"] = replacement;
            ClearInvalidUpdateMarkers(root);
            return root;
        }

        log.Add(exchange.DeepClone());
        active["exchangeLog"] = log;
        if (isNoEffectExchange)
        {
            ClearInvalidUpdateMarkers(root);
            return root;
        }

        if (exchange["after"] is JsonObject exchangeAfter)
        {
            CopyConflictStateFields(exchangeAfter, active);
        }

        CopyIfPresent(update, active, "conflictPosition");
        CopyIfPresent(update, active, "playerSideStrain");
        CopyIfPresent(update, active, "oppositionSideStrain");
        CopyIfPresent(update, active, "resolutionState");
        CopyIfPresent(update, active, "status");
        ClearInvalidUpdateMarkers(root);
        return root;
    }


    private static JsonObject ApplyResolve(JsonObject root, JsonObject update, bool repairCancel)
    {
        var active = root["activeConflict"] as JsonObject;
        if (active == null)
        {
            if (repairCancel)
            {
                root["activeConflict"] = null;
                ClearInvalidUpdateMarkers(root);
                return root;
            }

            return MarkInvalidUpdate(root, update, "resolve_without_active_conflict");
        }

        var dangerMode = SpiritualConflictDangerPolicy.ReadDeclaration(active);
        if (dangerMode is null)
            return MarkInvalidUpdate(root, update, "active_conflict_invalid_danger_mode");
        if (!SpiritualConflictDangerPolicy.IsOmittedOrExactEcho(update, dangerMode) ||
            !SpiritualConflictDangerPolicy.IsOmittedOrExactEcho(update["resolution"] as JsonObject, dangerMode))
        {
            return MarkInvalidUpdate(root, update, "resolve_danger_mode_change_without_authority");
        }

        var resolution = CloneObject(update["resolution"] as JsonObject);
        if (!repairCancel)
        {
            if (resolution == null)
                return MarkInvalidUpdate(root, update, "resolve_missing_resolution");

            var operationType = GetNodeString(resolution["operationType"]);
            if (!string.IsNullOrWhiteSpace(operationType) &&
                OperationTypes.Contains(operationType) &&
                RequiresGuardianResolveEvidence(active, operationType) &&
                HasAnyGuardianResolveReference(resolution) &&
                !ResolutionReferencesGuardianOpponent(resolution, active))
            {
                return MarkInvalidUpdate(root, update, "resolve_guardian_id_mismatch");
            }

            if (!HasCompleteResolveResolution(resolution, active))
                return MarkInvalidUpdate(root, update, "resolve_incomplete_resolution");
        }

        resolution ??= new JsonObject();
        var activeConflictId = GetNodeString(active["conflictId"]);
        var resolutionConflictId = GetNodeString(resolution["conflictId"]);
        if (!string.IsNullOrWhiteSpace(resolutionConflictId) &&
            !string.Equals(resolutionConflictId, activeConflictId, StringComparison.OrdinalIgnoreCase))
        {
            return MarkInvalidUpdate(root, update, "resolve_conflict_id_mismatch");
        }

        if (string.IsNullOrWhiteSpace(resolutionConflictId))
            resolution["conflictId"] = active["conflictId"]?.DeepClone();
        resolution["dangerMode"] = dangerMode;
        resolution["realm"] ??= active["realm"]?.DeepClone();
        resolution["sideModel"] ??= active["sideModel"]?.DeepClone();

        resolution["resolutionState"] = repairCancel ? "repair_cancelled" : "resolved";
        resolution["resolvedAtUtc"] ??= DateTime.UtcNow.ToString("o");
        resolution["mode"] = repairCancel ? ModeRepairCancel : ModeResolve;

        var recent = root["recentConflicts"] as JsonArray ?? new JsonArray();
        recent.Add(resolution);
        while (recent.Count > 20)
            recent.RemoveAt(0);

        root["recentConflicts"] = recent;
        root["activeConflict"] = null;
        ClearInvalidUpdateMarkers(root);
        return root;
    }
```

Create BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.Danger.cs exactly:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static void ValidateConflictDangerDeclaration(
        JsonObject conflict, string context, List<ValidationIssue> issues)
    {
        if (SpiritualConflictDangerPolicy.ReadDeclaration(conflict) is not null)
            return;
        AddInvalidConflictDangerDeclaration(
            context, conflict["dangerMode"]?.ToJsonString() ?? "missing/null", issues);
    }

    private static void ValidateStartDangerDeclaration(
        JsonElement update, string context, List<ValidationIssue> issues)
    {
        foreach (var property in new[] { "conflictState", "activeConflict", "conflictSeed" })
        {
            if (!TryGetObject(update, property, out var seed))
                continue;
            if (SpiritualConflictDangerPolicy.SeverityCap(TryGetString(seed, "dangerMode")) < 0)
            {
                AddInvalidConflictDangerDeclaration(
                    context + "." + property,
                    seed.TryGetProperty("dangerMode", out var value) ? value.GetRawText() : "missing",
                    issues);
            }
            return;
        }
    }

    private static void AddInvalidConflictDangerDeclaration(
        string context, string actual, List<ValidationIssue> issues) =>
        issues.Add(new ValidationIssue(
            context + ".dangerMode",
            IssueSeverity.Error,
            "Опасность духовного конфликта должна быть объявлена точным значением dangerMode.",
            code: "afterlife_conflict_danger_mode_invalid",
            section: "AfterlifeSpiritualConflict",
            expected: "training/controlled/hostile/annihilation; exact lowercase JSON string",
            actual: actual,
            repairHint: "Укажи dangerMode в seed нового конфликта. Не выводи режим из броска и не меняй объявленную опасность существующего боя."));

    private static void ValidateDangerModePreTurnIntegrity(
        JsonObject? preTurnRoot, JsonObject? currentRoot, List<ValidationIssue> issues)
    {
        if (preTurnRoot is null || currentRoot is null)
            return;

        var declarations = new Dictionary<string, List<string?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (conflict, _) in EnumerateDangerConflicts(preTurnRoot))
        {
            var id = TryReadConflictId(conflict);
            if (string.IsNullOrWhiteSpace(id))
                continue;
            if (!declarations.TryGetValue(id, out var modes))
            {
                modes = new List<string?>();
                declarations.Add(id, modes);
            }
            modes.Add(SpiritualConflictDangerPolicy.ReadDeclaration(conflict));
        }

        foreach (var (conflict, context) in EnumerateDangerConflicts(currentRoot))
        {
            var id = TryReadConflictId(conflict);
            if (string.IsNullOrWhiteSpace(id) || !declarations.TryGetValue(id, out var modes))
                continue;

            var expected = modes[0];
            if (expected is null || modes.Any(mode => !string.Equals(mode, expected, StringComparison.Ordinal)))
            {
                issues.Add(new ValidationIssue(
                    context + ".dangerMode",
                    IssueSeverity.Error,
                    "Pre-turn записи одного духовного конфликта не доказывают единую допустимую опасность.",
                    code: "afterlife_conflict_danger_mode_invalid_pre_turn_authority",
                    section: "AfterlifeSpiritualConflict",
                    expected: "one valid exact dangerMode across every accepted occurrence of this conflictId",
                    actual: "missing, invalid or conflicting pre-turn declaration",
                    repairHint: "Восстанови корректное принятое состояние до хода; текущий ответ не может задать опасность задним числом."));
                continue;
            }

            var current = SpiritualConflictDangerPolicy.ReadDeclaration(conflict);
            if (current is null || string.Equals(current, expected, StringComparison.Ordinal))
                continue;

            issues.Add(new ValidationIssue(
                context + ".dangerMode",
                IssueSeverity.Error,
                "Опасность принятого духовного конфликта изменена без отдельного подтверждённого перехода.",
                code: "afterlife_conflict_danger_mode_changed_without_authority",
                section: "AfterlifeSpiritualConflict",
                expected: expected,
                actual: current,
                repairHint: "Сохрани pre-turn dangerMode. Обычный exchange, замена activeConflict, resolve и repair_cancel не разрешают эскалацию."));
        }
    }

    private static IEnumerable<(JsonObject Conflict, string Context)> EnumerateDangerConflicts(JsonObject root)
    {
        if (root["activeConflict"] is JsonObject active)
            yield return (active, AfterlifeSpiritualConflictState.StatePath + ".activeConflict");
        if (root["recentConflicts"] is not JsonArray recent)
            yield break;
        for (var index = 0; index < recent.Count; index++)
        {
            if (recent[index] is JsonObject proof)
                yield return (proof, AfterlifeSpiritualConflictState.StatePath + $".recentConflicts[{index}]");
        }
    }
}
```

Apply these exact hooks and the unchanged cap-lookup extraction. The integrity
hook runs immediately after the existing parse/catch and before its active-null
early return; the old terminal predicate and branches are not rewritten.
```diff
*** Update File: BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs
@@
-            await ValidateActiveConflictRemovalHasTerminalProofAsync(null, issues);
+            await ValidateAfterlifeConflictPreTurnIntegrityAsync(null, issues);
@@
-            await ValidateActiveConflictRemovalHasTerminalProofAsync(null, issues);
+            await ValidateAfterlifeConflictPreTurnIntegrityAsync(null, issues);
@@
-        await ValidateActiveConflictRemovalHasTerminalProofAsync(root, issues);
+        await ValidateAfterlifeConflictPreTurnIntegrityAsync(root, issues);
@@
-    private async Task ValidateActiveConflictRemovalHasTerminalProofAsync(JsonObject? currentRoot, List<ValidationIssue> issues)
+    private async Task ValidateAfterlifeConflictPreTurnIntegrityAsync(JsonObject? currentRoot, List<ValidationIssue> issues)
@@
-        if (preTurnRoot?["activeConflict"] is not JsonObject)
+        ValidateDangerModePreTurnIntegrity(preTurnRoot, currentRoot, issues);
+
+        if (preTurnRoot?["activeConflict"] is not JsonObject)
@@
-        if (string.Equals(mode, AfterlifeSpiritualConflictState.ModeExchange, StringComparison.OrdinalIgnoreCase) &&
+        if (string.Equals(mode, AfterlifeSpiritualConflictState.ModeStart, StringComparison.OrdinalIgnoreCase))
+            ValidateStartDangerDeclaration(update, context, issues);
+
+        if (string.Equals(mode, AfterlifeSpiritualConflictState.ModeExchange, StringComparison.OrdinalIgnoreCase) &&
             !TryGetObject(update, "exchange", out _))
@@
     private void ValidateActiveAfterlifeConflict(
         JsonObject conflict,
         string context,
         List<ValidationIssue> issues,
         AfterlifeConflictDiceContext diceContext,
         AfterlifeActionCostAuthorityContext actionCostAuthority)
     {
+        ValidateConflictDangerDeclaration(conflict, context, issues);
         RequireNodeString(conflict, context, issues, "conflictId");
@@
-        var combatConditionIds = ValidateCombatConditions(proof["combatConditions"], $"{context}.combatConditions", issues);
+        ValidateConflictDangerDeclaration(proof, context, issues);
+        var combatConditionIds = ValidateCombatConditions(proof["combatConditions"], $"{context}.combatConditions", issues);
*** Update File: BookOfEternityClient/Services/SpiritualWoundOpportunityMath.cs
@@
-        var modeCap = ModeCap(input.DangerMode);
+        var modeCap = SpiritualConflictDangerPolicy.SeverityCap(input.DangerMode);
@@
-    private static int ModeCap(string? mode) => mode switch
-    {
-        "training" => 0,
-        "controlled" => 2,
-        "hostile" or "annihilation" => 4,
-        _ => -1
-    };
```

- [x] **Step 4: Cut over current fixtures and synchronize the GM contract.**

Apply the following exact fixture patch. It adds 66 explicit declarations in
11 files and makes the dissipation snapshot's matching pre-turn mode explicit.
Generic hostile declarations are test scaffolding, not a runtime default.
Keep omitted fields in the existing partial replacement payloads: they now
exercise carry-forward. Existing assertions and deliberately malformed
noncanonical/presence-only fixtures are not weakened or bulk-rewritten.
```diff
*** Update File: BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.cs
@@
         [
           {
             "mode": "resolve",
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_old_control_001",
             "resolutionState": "resolved",
             "resolvedAtTurn": 7,
             "operationType": "negotiate",
@@
           "activeConflict": null,
           "recentConflicts": [
             {
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_historical_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 6,
               "operationType": "guard",
@@
           "activeConflict": null,
           "recentConflicts": [
             {
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_historical_no_turn_001",
               "resolutionState": "resolved",
               "operationType": "guard",
               "playerOutcome": "won",
@@
           "activeConflict": null,
           "recentConflicts": [
             {
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_pre_turn_no_turn_001",
               "resolutionState": "resolved",
               "operationType": "guard",
               "playerOutcome": "won",
@@
         await WriteSoulStateAsync();
         await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "playerSide": {
               "leadContestant": {
                 "actorType": "player",
@@
         await WriteValidatedConflictSnapshotFromCurrentAsync("Я продолжаю духовный бой после старого обмена.");
         await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "playerSide": {
               "leadContestant": {
                 "actorType": "player",
@@
           "activeConflict": null,
           "recentConflicts": [
             {
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_changed_no_turn_001",
               "resolutionState": "resolved",
               "operationType": "guard",
               "playerOutcome": "won",
@@
           "activeConflict": null,
           "recentConflicts": [
             {
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_changed_no_turn_001",
               "resolutionState": "resolved",
               "operationType": "pressure",
               "playerOutcome": "won",
@@
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_voluntary_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "operationType": "surrender",
@@
           "afterlifeSpiritualConflictUpdate": {
             "mode": "start",
             "conflictState": {
+              "dangerMode": "training",
               "conflictId": "afterlife_conflict_support_role_projection",
               "realm": "Chaos Sea",
               "sideModel": "direct_duel",
               "status": "active",
@@
         {
           "mode": "start",
           "conflictSeed": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_focus_start_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "playerSide": {
@@
     public void ApplyUpdate_SummaryOnlyExchange_MarksInvalidInsteadOfAppendingMalformedExchange()
     {
         var root = JsonNode.Parse("""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "exchangeLog": []
           },
           "recentConflicts": []
         }
@@
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_existing_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "exchangeLog": [
@@
         {
           "mode": "start",
           "conflictState": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_new_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "exchangeLog": []
@@
         {
           "mode": "start",
           "conflictState": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_missing_realm_001",
             "sideModel": "direct_duel",
             "exchangeLog": []
           }
@@
           "mode": "start",
           "realm": "Shining Abode",
           "conflictState": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_shining_001",
             "sideModel": "direct_duel",
             "exchangeLog": []
           }
@@
           "mode": "start",
           "realm": "MortalWorldProfile",
           "conflictState": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_wrong_realm_001",
             "sideModel": "direct_duel",
             "exchangeLog": []
           }
@@
     public void ApplyUpdate_ExchangeWithReplacementPreservesAppendedExchange()
     {
         var root = JsonNode.Parse("""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "exchangeLog": [
               {
                 "exchangeId": "exchange_000",
                 "operationType": "maneuver",
@@
     public void ApplyUpdate_ExchangeWithReplacementDuplicateId_PrefersExplicitExchangePayload()
     {
         var root = JsonNode.Parse("""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "exchangeLog": []
           },
           "recentConflicts": []
         }
@@
     public void ApplyUpdate_ExchangeWithMismatchedReplacement_MarksInvalidAndPreservesActiveConflict()
     {
         var root = JsonNode.Parse("""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "exchangeLog": [
               {
                 "exchangeId": "exchange_000",
                 "operationType": "maneuver",
@@
     public void ApplyUpdate_ExchangeWithMismatchedExchangeConflictIdentity_MarksInvalidAndPreservesActiveConflict(string identityProperty)
     {
         var root = JsonNode.Parse("""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "exchangeLog": [
               {
                 "exchangeId": "exchange_000",
                 "operationType": "maneuver",
@@
     public void ApplyUpdate_ExchangeWithReplacementMissingConflictId_PreservesCurrentConflictId()
     {
         var root = JsonNode.Parse("""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "exchangeLog": []
           },
           "recentConflicts": []
         }
@@
           "activeConflict": null,
           "recentConflicts": [
             {
+              "dangerMode": "hostile",
               "conflictId": "mortal_world_illegal_repair_cancel",
               "resolutionState": "repair_cancelled"
             }
           ]
@@
         return JsonNode.Parse("""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "playerSide": {
               "leadContestant": {
@@
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_test_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "operationType": "negotiate",
@@
           "recentConflicts": [
             {
               "mode": "repair_cancel",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_test_001",
               "resolutionState": "repair_cancelled"
             }
           ]
@@
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_test_001"
             }
           ]
         }
@@
           "recentConflicts": [
             {
               "mode": "repair_cancel",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_test_001"
             }
           ]
         }
@@
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_other_001",
               "resolutionState": "resolved"
             }
           ]
@@
         """;
         const string preTurnConflict = """
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "playerSide": {
               "leadContestant": { "actorType": "player", "actorId": "player_soul", "displayName": "Асуран" },
               "supporters": []
@@
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "resolvedActorId": "guardian_liora",
@@
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "operationType": "force_incarnation",
@@
     public async Task ValidateGameStateAsync_ForcedIncarnation_RequiresDiceBackedConflictProof()
     {
         const string currentConflict = """
         {
           "schemaVersion": 1,
           "activeConflict": null,
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "guardianId": "guardian_liora",
               "operationType": "force_incarnation",
               "playerOutcome": "lost"
             }
           ]
         }
         """;
@@
           "activeConflict": null,
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "guardianId": "guardian_liora",
               {{JsonSerializer.Serialize(genericActorField)}}: "player_soul",
@@
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "actorId": "player_soul",
@@
     public async Task ValidateGameStateAsync_ForcedIncarnation_UsesPreTurnGuardianReputationForConflictProof()
     {
         const string currentConflict = """
         {
           "schemaVersion": 1,
           "activeConflict": null,
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "guardianId": "guardian_liora",
               "operationType": "force_incarnation",
               "playerOutcome": "lost"
             }
           ]
         }
         """;
@@
     public async Task ValidateGameStateAsync_ForcedIncarnation_RequiresPreTurnActiveGuardianForConflictProof()
     {
         const string currentConflict = """
         {
           "schemaVersion": 1,
           "activeConflict": null,
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "guardianId": "guardian_liora",
               "operationType": "force_incarnation",
               "playerOutcome": "lost"
             }
           ]
         }
         """;
@@
           "schemaVersion": 1,
           "activeConflict": null,
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "guardianId": "guardian_other",
               "operationType": "force_incarnation",
               "playerOutcome": "lost",
@@
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_liora_pressure_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "oppositionSide": {
@@
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_liora_pressure_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "guardianId": "guardian_liora",
@@
     public async Task ValidateGameStateAsync_ForcedIncarnation_RejectsUnboundConflictIdProofWithoutProvocationTag()
     {
         const string soul = """
         {
           "soulName": "Асуран",
           "currentRealm": "Chaos Sea"
         }
         """;
         const string preTurnConflict = """
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "oppositionSide": {
               "leadContestant": {
                 "actorType": "guardian",
                 "actorId": "guardian_liora",
                 "displayName": "Лиора"
               },
               "supporters": []
             },
             "exchangeLog": []
           },
@@
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_fabricated_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "guardianId": "guardian_liora",
@@
     public async Task ValidateGameStateAsync_ForcedIncarnation_RejectsWrongGuardianConflictProofWithoutProvocationTag()
     {
         const string soul = """
         {
           "soulName": "Асуран",
           "currentRealm": "Chaos Sea"
         }
         """;
         const string preTurnConflict = """
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "oppositionSide": {
               "leadContestant": {
                 "actorType": "guardian",
                 "actorId": "guardian_liora",
                 "displayName": "Лиора"
               },
               "supporters": []
             },
             "exchangeLog": []
           },
@@
         {
           "schemaVersion": 1,
           "activeConflict": null,
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 7,
               "guardianId": "guardian_other",
               "operationType": "force_incarnation",
               "playerOutcome": "lost"
             }
@@
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_stale_001",
               "resolutionState": "resolved",
               "resolvedAtTurn": 6,
               "guardianId": "guardian_liora",
@@
         var preTurnGuardians = preTurnGuardiansOverride ?? guardians;
         const string preTurnConflict = """
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "playerSide": {
               "leadContestant": { "actorType": "player", "actorId": "player_soul", "displayName": "Асуран" },
               "supporters": []
@@
           "recentConflicts": [
             {
               "mode": "{{mode}}",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_test_001",
               "realm": {{JsonSerializer.Serialize(realm)}},
               "sideModel": "direct_duel",
               "resolutionState": "{{resolutionState}}",
@@
         string resolutionKind = "player_victory")
     {
         return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
         {
           "schemaVersion": 1,
           "activeConflict": null,
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "annihilation",
               "conflictId": "afterlife_conflict_test_001",
               "realm": "Chaos Sea",
               "sideModel": "direct_duel",
               "resolutionState": "resolved",
               "operationType": "pressure",
               "playerOutcome": "{{playerOutcome}}",
               "resolutionKind": "{{resolutionKind}}",
               "resolvedAtTurn": 7,
               "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePower().ToJsonString()}},
               "summary": "The spiritual conflict was resolved.",
@@
         string resolutionKind = "player_victory")
     {
         return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
         {
           "schemaVersion": 1,
           "activeConflict": null,
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "annihilation",
               "conflictId": "afterlife_conflict_test_001",
               "realm": "Chaos Sea",
               "sideModel": "direct_duel",
               "resolutionState": "resolved",
               "operationType": "pressure",
               "playerOutcome": "{{playerOutcome}}",
               "resolutionKind": "{{resolutionKind}}",
               "resolvedAtTurn": 7,
               "diceAudit": {{BuildPlayerSuccessDiceAuditWithoutAbodePower().ToJsonString()}},
               "summary": "The spiritual conflict was resolved without final soul dissipation."
@@
           "entries": []
         }
         """;
-        var preTurnConflict = BuildActiveConflictRootJson();
+        var preTurnConflictRoot = JsonNode.Parse(BuildActiveConflictRootJson())!.AsObject();
+        preTurnConflictRoot["activeConflict"]!["dangerMode"] = "annihilation";
+        var preTurnConflict = preTurnConflictRoot.ToJsonString();

         await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardians);
         await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, journal);
@@
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": {{JsonSerializer.Serialize(realm)}},
             "sideModel": "direct_duel",
             "playerSide": {
@@
             : $",\n            \"controlState\": {activeControlStateJson}";

         return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "playerSide": {
               "leadContestant": {
                 "actorType": "player",
                 "actorId": "player_soul",
@@
     private Task WriteConflictStateWithRawExchangeLogAsync(string exchangeLogJson)
     {
         return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "playerSide": {
               "leadContestant": {
                 "actorType": "player",
                 "actorId": "player_soul",
@@
     private Task WriteConflictStateWithRawPlayerSupportersAsync(string supportersJson)
     {
         return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_test_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "playerSide": {
               "leadContestant": {
                 "actorType": "player",
                 "actorId": "player_soul",
*** Update File: BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictBalanceTests.cs
@@
           "recentConflicts": [
             {
               "mode": "resolve",
+              "dangerMode": "hostile",
               "conflictId": "afterlife_conflict_balance_001",
               "realm": {{JsonSerializer.Serialize(scenario.Realm)}},
               "sideModel": {{JsonSerializer.Serialize(scenario.SideModel)}},
               "resolutionState": "resolved",
@@
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_balance_001",
             "realm": "Chaos Sea",
             "sideModel": {{JsonSerializer.Serialize(scenario.Name == "weak_player_aided_by_strong_champion" ? "champion_duel" : "direct_duel")}},
             "playerSide": {
@@
         {
           "schemaVersion": 1,
           "activeConflict": {
+            "dangerMode": "hostile",
             "conflictId": "afterlife_conflict_balance_001",
             "realm": "Chaos Sea",
             "sideModel": "direct_duel",
             "playerSide": {
@@
     {
       "schemaVersion": 1,
       "activeConflict": {
+        "dangerMode": "hostile",
         "conflictId": "afterlife_conflict_balance_001",
         "realm": {{JsonSerializer.Serialize(scenario.Realm)}},
         "sideModel": {{JsonSerializer.Serialize(scenario.SideModel)}},
         "playerSide": {
*** Update File: BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.cs
@@
     private static JsonObject ActiveConflict(string conflictId) =>
         new()
         {
+            ["dangerMode"] = "hostile",
             ["conflictId"] = conflictId,
             ["realm"] = "Chaos Sea",
             ["status"] = "active",
             ["resolutionState"] = "active",
*** Update File: BookOfEternityClient.IntegrationTests/ResourceAfterlifeOwnerTests.cs
@@
     private static JsonObject ActiveConflict(string conflictId, string realm) =>
         new()
         {
+            ["dangerMode"] = "hostile",
             ["conflictId"] = conflictId,
             ["realm"] = realm,
             ["status"] = "active",
             ["resolutionState"] = "active",
*** Update File: BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Conditions.cs
@@
             ["schemaVersion"] = 1,
             ["activeConflict"] = new JsonObject
             {
+                ["dangerMode"] = "hostile",
                 ["conflictId"] = conflictId,
                 ["realm"] = "Shining Abode",
                 ["sideModel"] = "direct_duel",
                 ["status"] = "active",
*** Update File: BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Projection.cs
@@
                 ["schemaVersion"] = 1,
                 ["activeConflict"] = new JsonObject
                 {
+                    ["dangerMode"] = "hostile",
                     ["conflictId"] = conflictId,
                     ["realm"] = "Shining Abode",
                     ["sideModel"] = "direct_duel",
                     ["status"] = "active",
*** Update File: BookOfEternityClient.IntegrationTests/EffectMaterializationLifecycleTests.cs
@@
             ["schemaVersion"] = 1,
             ["activeConflict"] = new JsonObject
             {
+                ["dangerMode"] = "hostile",
                 ["conflictId"] = conflictId,
                 ["realm"] = "Shining Abode",
                 ["sideModel"] = "direct_duel",
                 ["status"] = "active",
*** Update File: BookOfEternityClient.Tests/EffectMechanicsSnapshotTests.Afterlife.cs
@@
             ["schemaVersion"] = 1,
             ["activeConflict"] = new JsonObject
             {
+                ["dangerMode"] = "hostile",
                 ["conflictId"] = conflictId,
                 ["realm"] = "Shining Abode",
                 ["sideModel"] = "direct_duel",
                 ["status"] = "active",
*** Update File: BookOfEternityClient.Tests/LiveTurnPreparationServiceTests.cs
@@
         };
         var activeConflict = new JsonObject
         {
+            ["dangerMode"] = "hostile",
             ["conflictId"] = "afterlife_conflict_live_001",
             ["realm"] = "Chaos Sea",
             ["sideModel"] = "direct_duel",
             ["playerSide"] = new JsonObject
*** Update File: BookOfEternityClient.IntegrationTests/WoundMaterializationTestFixtures.cs
@@
         var conflict = AfterlifeSpiritualConflictState.CreateDefaultRoot();
         conflict["activeConflict"] = new JsonObject
         {
+                ["dangerMode"] = dangerMode,
                 ["conflictId"] = conflictId,
                 ["realm"] = "Chaos Sea",
                 ["sideModel"] = "direct_duel",
                 ["playerSide"] = CreateConflictSide("player", playerId),
*** Update File: BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.LightIncarnateHistoricalAuthority.cs
@@
               "schemaVersion": 1,
               "activeConflict": null,
               "recentConflicts": [{
+                "dangerMode": "hostile",
                 "conflictId": "afterlife_conflict_light_history_006",
                 "resolutionState": "resolved",
                 "operationType": "guard",
                 "playerOutcome": "won",
```

Apply the exact GM documentation/worked-example additions:
```diff
*** Update File: CLI_API_Specification.md
@@
 #### **AFTERLIFE SPIRITUAL CONFLICT**
+
+The spiritual conflict danger declaration is mandatory: put `dangerMode` in the selected `conflictState`/`activeConflict`/`conflictSeed` of `mode=start`, canonical `activeConflict`, and every `recentConflicts[]` proof. Use exactly `training`, `controlled`, `hostile`, or `annihilation`: lowercase JSON strings without surrounding whitespace. There is no implicit mode or old-save fallback. Ordinary exchanges and partial `activeConflictAfter`/`conflictStateAfter` replacements may omit the field and preserve the accepted declaration; explicit echoes must match exactly, including in the update root and exchange `before`/`after`. `resolve` and `repair_cancel` copy that declaration into the terminal proof and cannot replace it. During a validated turn, every retained same-ID active/recent occurrence is compared with the signed pre-turn declarations; a missing, invalid or conflicting baseline fails closed, and a later duplicate cannot hide a change. Legal removal from the bounded recent-history window is unchanged. Do not infer escalation after a roll or submit a boolean as escalation authority. This declaration/persistence stage does not implement accepted escalation, wound production or healing. The declared wound ceilings remain training 0, controlled II, hostile/annihilation IV; wounds are optional GM choices within validated limits, and soul dissipation remains separately authorized and always optional.
*** Update File: OtherGuides/Afterlife_Contract_Matrix.md
@@
 ## Afterlife Spiritual Conflict Contract
+
+The spiritual conflict danger declaration is mandatory: put `dangerMode` in the selected `conflictState`/`activeConflict`/`conflictSeed` of `mode=start`, canonical `activeConflict`, and every `recentConflicts[]` proof. Use exactly `training`, `controlled`, `hostile`, or `annihilation`: lowercase JSON strings without surrounding whitespace. There is no implicit mode or old-save fallback. Ordinary exchanges and partial `activeConflictAfter`/`conflictStateAfter` replacements may omit the field and preserve the accepted declaration; explicit echoes must match exactly, including in the update root and exchange `before`/`after`. `resolve` and `repair_cancel` copy that declaration into the terminal proof and cannot replace it. During a validated turn, every retained same-ID active/recent occurrence is compared with the signed pre-turn declarations; a missing, invalid or conflicting baseline fails closed, and a later duplicate cannot hide a change. Legal removal from the bounded recent-history window is unchanged. Do not infer escalation after a roll or submit a boolean as escalation authority. This declaration/persistence stage does not implement accepted escalation, wound production or healing. The declared wound ceilings remain training 0, controlled II, hostile/annihilation IV; wounds are optional GM choices within validated limits, and soul dissipation remains separately authorized and always optional.
*** Update File: OtherGuides/Afterlife_Combat_Terminology_Glossary.md
@@
 # Afterlife Combat Terminology Glossary
+
+The spiritual conflict danger declaration is mandatory: put `dangerMode` in the selected `conflictState`/`activeConflict`/`conflictSeed` of `mode=start`, canonical `activeConflict`, and every `recentConflicts[]` proof. Use exactly `training`, `controlled`, `hostile`, or `annihilation`: lowercase JSON strings without surrounding whitespace. There is no implicit mode or old-save fallback. Ordinary exchanges and partial `activeConflictAfter`/`conflictStateAfter` replacements may omit the field and preserve the accepted declaration; explicit echoes must match exactly, including in the update root and exchange `before`/`after`. `resolve` and `repair_cancel` copy that declaration into the terminal proof and cannot replace it. During a validated turn, every retained same-ID active/recent occurrence is compared with the signed pre-turn declarations; a missing, invalid or conflicting baseline fails closed, and a later duplicate cannot hide a change. Legal removal from the bounded recent-history window is unchanged. Do not infer escalation after a roll or submit a boolean as escalation authority. This declaration/persistence stage does not implement accepted escalation, wound production or healing. The declared wound ceilings remain training 0, controlled II, hostile/annihilation IV; wounds are optional GM choices within validated limits, and soul dissipation remains separately authorized and always optional.
*** Update File: CLI_Agent_Daemon_Specification.md
@@
 ## Полная процедура afterlife-хода для ГМа
+
+The spiritual conflict danger declaration is mandatory: put `dangerMode` in the selected `conflictState`/`activeConflict`/`conflictSeed` of `mode=start`, canonical `activeConflict`, and every `recentConflicts[]` proof. Use exactly `training`, `controlled`, `hostile`, or `annihilation`: lowercase JSON strings without surrounding whitespace. There is no implicit mode or old-save fallback. Ordinary exchanges and partial `activeConflictAfter`/`conflictStateAfter` replacements may omit the field and preserve the accepted declaration; explicit echoes must match exactly, including in the update root and exchange `before`/`after`. `resolve` and `repair_cancel` copy that declaration into the terminal proof and cannot replace it. During a validated turn, every retained same-ID active/recent occurrence is compared with the signed pre-turn declarations; a missing, invalid or conflicting baseline fails closed, and a later duplicate cannot hide a change. Legal removal from the bounded recent-history window is unchanged. Do not infer escalation after a roll or submit a boolean as escalation authority. This declaration/persistence stage does not implement accepted escalation, wound production or healing. The declared wound ceilings remain training 0, controlled II, hostile/annihilation IV; wounds are optional GM choices within validated limits, and soul dissipation remains separately authorized and always optional.
*** Update File: TaskGuides/CLI_Step_Main.txt
@@
+            - The spiritual conflict danger declaration is mandatory: put `dangerMode` in the selected `conflictState`/`activeConflict`/`conflictSeed` of `mode=start`, canonical `activeConflict`, and every `recentConflicts[]` proof. Use exactly `training`, `controlled`, `hostile`, or `annihilation`: lowercase JSON strings without surrounding whitespace. There is no implicit mode or old-save fallback. Ordinary exchanges and partial `activeConflictAfter`/`conflictStateAfter` replacements may omit the field and preserve the accepted declaration; explicit echoes must match exactly, including in the update root and exchange `before`/`after`. `resolve` and `repair_cancel` copy that declaration into the terminal proof and cannot replace it. During a validated turn, every retained same-ID active/recent occurrence is compared with the signed pre-turn declarations; a missing, invalid or conflicting baseline fails closed, and a later duplicate cannot hide a change. Legal removal from the bounded recent-history window is unchanged. Do not infer escalation after a roll or submit a boolean as escalation authority. This declaration/persistence stage does not implement accepted escalation, wound production or healing. The declared wound ceilings remain training 0, controlled II, hostile/annihilation IV; wounds are optional GM choices within validated limits, and soul dissipation remains separately authorized and always optional.
             - Current `input/turn_request.json.preGeneratedDices1d20` is dice authority only for current/new contested exchanges or resolutions. Preserve accepted historical exchange dice rather than rewriting them to the current pool. Historical exchange-audit exemption requires the same validated pre-turn active conflictId and consumes each snapshot occurrence once; exchangeAtTurn alone is not authority. An old turn marker permits a match differing in only the top-level summary, restricted to missing/null/string, with every other member exact. New, mechanically changed, foreign-conflict or duplicate exchanges receive current dice, matchup and action-cost checks. This is validation compatibility only: resource publication still preserves the exact pre-turn prefix, including summary. Restore an altered accepted prefix when its dedicated diagnostic requests it; do not reroll old dice.
*** Update File: Examples/E_CLI_Afterlife_Turns.txt
@@
 - The GM starts an afterlife spiritual conflict instead of writing Mortal combat state.
+
+The spiritual conflict danger declaration is mandatory: put `dangerMode` in the selected `conflictState`/`activeConflict`/`conflictSeed` of `mode=start`, canonical `activeConflict`, and every `recentConflicts[]` proof. Use exactly `training`, `controlled`, `hostile`, or `annihilation`: lowercase JSON strings without surrounding whitespace. There is no implicit mode or old-save fallback. Ordinary exchanges and partial `activeConflictAfter`/`conflictStateAfter` replacements may omit the field and preserve the accepted declaration; explicit echoes must match exactly, including in the update root and exchange `before`/`after`. `resolve` and `repair_cancel` copy that declaration into the terminal proof and cannot replace it. During a validated turn, every retained same-ID active/recent occurrence is compared with the signed pre-turn declarations; a missing, invalid or conflicting baseline fails closed, and a later duplicate cannot hide a change. Legal removal from the bounded recent-history window is unchanged. Do not infer escalation after a roll or submit a boolean as escalation authority. This declaration/persistence stage does not implement accepted escalation, wound production or healing. The declared wound ceilings remain training 0, controlled II, hostile/annihilation IV; wounds are optional GM choices within validated limits, and soul dissipation remains separately authorized and always optional.
*** Update File: Examples/E_CLI_Afterlife_Turns.txt
@@
     "mode": "start",
     "conflictSeed": {
+      "dangerMode": "hostile",
       "conflictId": "afterlife_conflict_liora_forced_incarnation_001",
@@
+Danger declaration worked result: the client persists `activeConflict.dangerMode = hostile` from this seed. An ordinary replacement which omits `dangerMode` retains hostile; a submitted training/controlled/annihilation value or null is rejected. On resolve or repair_cancel, `recentConflicts[].dangerMode` remains hostile. These checks establish declaration persistence, not a wound opportunity or permission to dissipate a soul.
+
 The player lead in this start example intentionally has no `actorArtTierSnapshot` and no `artAuthoritySource`: the player soul's authority comes from `soul_state.afterlifeCombatProfile`. Non-player leads still need their explicit snapshot/source. If a side has supporters, author their canonical role as `supportRole`, for example:
```

In Examples/example_validation_manifest.json replace only the existing
runtimeScenarios object with id afterlife_spiritual_conflict_start_response
with this object, retaining its surrounding array/comma:
```json
    {
      "id": "afterlife_spiritual_conflict_start_response",
      "file": "E_CLI_Afterlife_Turns.txt",
      "runner": "gameResponseDistribution",
      "requiredText": [
        "afterlife_conflict_liora_forced_incarnation_001",
        "\"afterlifeSpiritualConflictUpdate\"",
        "\"mode\": \"start\"",
        "\"resourceKey\": \"spiritual_action_points\"",
        "\"resourceMaterialization\"",
        "\"dangerMode\": \"hostile\""
      ],
      "expectedModifiedFiles": [
        "game_state/meta/afterlife_spiritual_conflict_state.json"
      ],
      "expectedFileContains": [
        {
          "path": "game_state/meta/afterlife_spiritual_conflict_state.json",
          "requiredText": [
            "\"dangerMode\": \"hostile\""
          ]
        }
      ]
    }
```

No new JSON fence, response field, registry or launcher entrypoint is introduced.
The existing runtime scenario already deserializes the actual response and runs
StateDistributor; expectedFileContains now proves persisted danger, while
requiredText alone would only prove source presence. Existing FullValidation
covers this runtime route; inspect that method's result instead of adding an
unnecessary duplicate manifest run.

- [x] **Step 5: Run GREEN, changed-helper owners, one Fast and conditional FullValidation.**

Run each command sequentially; drain it fully before the next. First:
```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~SpiritualConflictDangerModeTests|FullyQualifiedName~SpiritualWoundOpportunityTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~LiveTurnPreparationServiceTests.PrepareAsync_AfterlifeActiveConflict_AddsCombatPreviewWithAuthorityAndDiceOutcome|FullyQualifiedName~EffectMechanicsSnapshotTests.SpiritualConflictPreview_ProjectsAcceptedConditionMechanicsWithoutMortalStats"
```

Then the real canonical/start/terminal owner selection:
```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~T083a_DangerMode_|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests.NormalizeAccumulatedStateAsync_StartConflict_CopiesSupporterRoleIntoSupportRole|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests.StateDistributor_StartConflict_DoesNotCreateLegacyActionEconomy|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests.ApplyUpdate_StartUsesExplicitUpdateRealm_WhenConflictRealmMissing|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests.ValidateGameStateAsync_NoEffectExchange_AllowsIdenticalBeforeAfter|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests.ValidateGameStateAsync_PreTurnActiveConflictClearedWithResolveProof_DoesNotFailTerminalProofValidation|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests.ValidateGameStateAsync_ValidNpcSoulDissipationProof_DoesNotReportSoulDissipationIssue"
```

Then one witness per changed accepted resource/effect/wound/balance helper:
```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictReducer_StartNeverCreatesLegacyActionEconomy|FullyQualifiedName~ResourceAfterlifeOwnerTests.AcceptedTurn_ConflictStartUsesTheCommonOwnerPlanAndPublication|FullyQualifiedName~EffectAfterlifeAdapterTests.ApplyExchange_PreservesCommonConditionCarrier|FullyQualifiedName~ExplorerModeCommandTests.TryProcessCommand_SpiritualActionUsesAcceptedConditionProjection|FullyQualifiedName~GameEngineTurnLifecycleTests.EffectMaterializationLifecycleTests_OwnerCarrierPublicationFailureRestoresEntireTrackedSet|FullyQualifiedName~WoundMaterializationTestFixturesTests.DangerModes_UseIndependentConflictEvidence|FullyQualifiedName~WoundMaterializationTestFixturesTests.SpiritualConflictSeed_PassesFileBackedAfterlifeConflictValidation|FullyQualifiedName~AfterlifeSpiritualConflictBalanceTests.ValidateGameStateAsync_AfterlifeConflictBalanceMatrix_AcceptsExpectedDiceBands"
```

Expected: every requested method is discovered, all selected rows PASS, zero
unexpected build warnings/errors, no skipped rows, duplicate executions, timeout
or incomplete cleanup. Keep the complete coherent selection if a measured run
proves the five-minute Focused default obsolete; report the evidence before
using an explicit bounded override within the documented ceiling. Do not replace
a missing requested test with a passing unrelated selection.

After the implementation, fixtures and GM changes are stable:
```powershell
.\scripts\test-csharp.ps1 -Lane Fast
.\scripts\test-csharp.ps1 -Lane FullValidation
```

Fast remains the entire Fast assembly under its unchanged five-minute deadline.
FullValidation is conditional here because GM examples/canonical afterlife
documentation changed; it has the existing fifteen-minute ceiling.
Confirm RuntimeManifestScenarios_DistributeThroughClientSurfaces is present and
passes in FullValidation. Do not add a repeated adjacent Fast or PreMerge.
If a control fails, retain its artifacts, diagnose the exact failing boundary,
and report concerns instead of editing unrelated gameplay or changing coverage.

- [x] **Step 6: Self-review, commit the scoped files and report evidence.**

Inspect the actual diff against every constraint and supplied code block; verify
the shared token set/caps, no implicit fallback, unchanged resource/dissipation
authority, cloned reducer inputs, all current occurrences and both terminal
branches. Preserve the source boundary and exact fixture semantics.

Run:
```powershell
git diff --check
git diff --stat
git status --short
```

Stage only the 26 runtime/test/GM files in the File map, inspect staged diff, and
commit with the following subject:
```text
feat: preserve declared spiritual conflict danger (#1536)
```

Write the full supplied report file: commands in actual order, every result
directory, build/test/cleanup outcomes, semantic RED explanation, final case
membership, files changed and any deviations/concerns. Do not claim full
T083, wounds/healing, Fast/Full success without artifacts, or merge readiness.
Return only status, commit, a short test summary, concerns and report path.
The parent inspects source and raw evidence before independent task review.

## Parent self-review

- Spec coverage: FR029 declaration/persistence and a bounded T085 continuity
  prerequisite map to this task; the unchanged FR030 cap function is shared.
  Accepted escalation, player-visible first-exchange sequencing, wound
  opportunities/seals/defeat, arts and actual healing stay explicitly open.
- Type consistency: new helpers are private/internal and defined above; existing
  TryGetObject/TryGetString/TryReadConflictId are static. No constructor or
  snapshot helper signature is extended. State and validators share the same
  exact-token policy.
- Source placement: the integrity hook precedes the existing active-null early
  return, so retained recent history is checked even without a pre-turn active
  conflict. Four rename occurrences and unique hook anchors were verified.
- Fixture inventory: parent checked the actual 66 insertion targets, including
  programmatic resource/effect builders, forced-incarnation baseline at original
  line12751 and the explicit annihilation snapshot correction at line14607.
  Prior malformed tests retain their original failure assertions.
- Test quality: 66 detached reducer rows, 50 real response/file/signed-snapshot
  rows and one GM guard; missing-property assertions fail semantically rather
  than with a null dereference. Controls cover all modes and all seed aliases.
  No assertion grants authority to submitted timestamps, booleans or hashes.
- Governance: #1536 traceability and existing worktree preserved; no migration,
  outside writes, new user-question loop or art/preparation decision.
  GM updates are in the same delivery; no Mortal/UI/launcher capability changes.
- Placeholder/type/constraint scan is required before dispatch. The parent
  supplies the complete Global Constraints and File map with the extracted
  task brief; the helper by itself omits the plan header.

## Acceptance and actual verification — 2026-09-07

Accepted implementation947832f003307c76c8bfbb00fca73215551b1291 plus correction
1576aa571b74c08c881ef2d21dd460c392c717c1; original reviewBASE is
a234fbe07f3d4e2ff485861606604c7225de988d, not the last commit's parent.
Independent task review and clarification are final-state Spec compliant /
Quality Approved, zero Critical/Important and one retained Minor process issue:
Integration RED50 did not run before implementation. No later run is described
as historical test-first evidence. Step2 remains unchecked as an explicit
non-recoverable sequencing exception, not unfinished implementation.

Actual results under TestResults/test-lanes (all date prefix20260907):

| Artifact suffix | Selection/result | Wall |
| --- | --- | --- |
| 152805-080-29176-80cea0b8b2754d76b9de9c8358f77622-focused | Empty discovery before tests existed; not semantic RED | 1:06.163 |
| 153039-582-48288-5e94c742bdc84b97baceed36c8513faa-focused | Fast semantic RED67:20PASS/47FAIL | 32.471s |
| 153308-819-47556-fdd2fe1f8f35451fbddc9823598eb90c-focused | Build-only CS0103, seven errors after accidental helper removal | 34.058s |
| 153439-842-49616-34ea50655e68468c8781ac057f7d63bc-focused | Build-only CS0111, seven errors in the intermediate repair | 8.683s |
| 153527-703-38780-297948358b0e4917828e95c429afa9be-focused | Reducer66+math66+docs124+previews2:258PASS | 1:14.463 |
| 153648-995-34016-cc70f21ff5274f46ba27829a7a9c9a2e-focused | Danger50+core6:56PASS | 1:20.326 |
| 153816-357-39120-7f789e950ba54b4da6d23d552d36b733-focused | Owners/balance20PASS; two intended owner methods absent from wrong filters | 34.214s |
| 153855-372-38952-cd0078142ae6414b810b31d93c83ece9-focused | Exact Light Incarnate/dissipation6PASS | 23.579s |
| 153922-879-35620-d101f2db206a41f88f0800d5ce259603-fast | Official Fast7698/7698PASS,26TRX,3942methods | 4:11.077/5m |
| 154338-888-27584-2c64c88f6ccf49a485d703a3bb458cb3-focused | Mortal pending rollback1FAIL before injected write hook | 20.818s |
| 154436-162-19492-3a6658aaa08f4ebf94c8af33ef270bcf-fullvalidation | Official FullValidation FAILED1622/1623; Shining /валидация leaks null | 9:48.895/15m |
| 155430-105-35956-b13f3f6eb2be4c5baf32b9bb5b5effaa-focused | Correct projection1/afterlife owners2PASS, Mortal owners3FAIL | 35.371s |
| 160739-930-47644-46d693c2375c4b1eb96b08d70883b30c-focused | Mutation diagnostics56:21PASS/35FAIL; wrong pending assertion instrumented | 2:12.337 |
| 161011-513-30128-15a5eb7dc5d647beb73ac375177f7590-focused | Same56:21PASS/35FAIL; another wrong assertion instrumented | 1:34.703 |
| 161210-108-50036-c1187eb006df43ae9f54e58c073c7627-focused | Correctly anchored diagnostic: danger19PASS/31FAIL, afterlife2PASS, Mortal4FAIL | 1:33.039 |
| 162350-670-6028-85997f21994440709839531b13797a21-focused | Archive guard2 semanticRED | 1:24.737 |
| 162729-284-36488-3bb07beb094e441c9d74a6e555137a31-focused | Restored danger50PASS | 39.822s |
| 162815-870-1692-46690f9814ee4fdea5c1dac59edcac85-focused | Archive2+unchanged budget1:3PASS | 13.408s |
| 162836-312-10800-8037fc01b44241308536bc0abc53a540-focused | Chaos command-display76PASS | 1:05.546 |
| 162951-687-46720-58ca8bf905e74c95adec1c3fdf71d421-focused | Shining command-display78PASS, including original /валидация | 1:00.563 |
| 163106-647-18348-3b7b6d3fce744b34882787eca8b8c52a-focused | Missing actor52methods/82rowsPASS | 1:23.528 |
| 163238-995-32924-0f05ae1025e743a5bec64b0e3a2469e8-focused | Missing actor52methods/82rowsPASS | 1:46.188 |
| 163434-419-45332-0bd87cdd393d4a17ad381fef78459230-focused | Missing QTE10methods/70rowsPASS | 39.697s |

Every completed summary has no timeout, duplicate IDs or owned-tree cleanup
failure. Original Integration153648 has one xUnit2031 warning at DangerMode.cs:69,
missed in the initial audit; subsequent incremental builds reporting zero do not
prove it was absent from source. The T177 follow-through14d2dfd6 repairs the
assertion form and fresh171731 reports zero warnings/errors across50passing rows.
Ordinary Git EOL notices are separately recorded. Intermediate compiler failures and repeated
diagnostic attempts are retained, not hidden. Neighboring reducer helpers were
restored; final net reducer diff is30added lines with no helper deletion.

Parent independently reconciled Fast7644discovery+54dynamicrows against all
7698actual rows/3942methods. The failed Full run completed960 of1074methods;
exact original cancelled descriptors supplied missing52+52actor/10QTE methods.
Their234rows have no overlap with completed methods. Corrected budget1/Chaos76/
Shining78 reruns replace exactly the same case membership; archive guard adds
one method/two rows. Combined latest coverage1859rows/1075methods has zero
nonpassing/missing/extra result. This is reconciled evidence, NOT an official
green FullValidation invocation. No repeated Fast, FullValidation or PreMerge
was used to rename the failed original control.

The runtime manifest test RuntimeManifestScenarios_DistributeThroughClientSurfaces
is one passing Fact that executes all scenarios internally; its actual code
checks expectedFileContains, including the worked start's persisted dangerMode.
No additional scenario-row count is claimed.

Correction scope adds exactly FileSystemExampleFixtureIntegrityTests.cs and the
two maintained afterlife command-display ZIPs. Both active and recent entries
needed explicit hostile test scaffolding. Parent compared all uncompressed entry
bytes against947832f0:104Chaos/34Shining names/counts and all other entry bytes
unchanged. Only game_state/meta/afterlife_spiritual_conflict_state.json changed
inside each; removing exactly two inserted lines reproduces its original bytes
and timestamps are preserved. This is a fixture cutover, not a user-save migration.
Unchanged budgets pass because the untouched Mortal archive remains the maximum
expanded/largest-entry fixture. No archive limits were raised.

Temporary mutation touched two C# files. Lifecycle source returned to its exact
original SHA-256; validator returned to identical Git-normalized candidate
content with an acknowledged mixed-EOL physical-hash deviation. The correction
commit contains no runtime source change. Independent review's binary ⚠ is
resolved by the parent all-entry audit; its remote-state ⚠ is resolved by the
bounded task's tool/action record: no push, PR, issue or other remote write ran.

The original owner filter named file names as classes. Step5 now names the actual
ExplorerModeCommandTests projection and GameEngineTurnLifecycleTests owner-carrier
method; their exact reruns are recorded above. The originally selected pending
method exercises a Mortal source, not the changed spiritual helper. Its failure
and the three Mortal owner failures persist with all new declaration hooks
disabled: old array wounds provide no canonical source, causing
effect_source_selector_unresolved for wound_test_torn_side/bleeding_consequence.
These were remaining T177 fixture work at this checkpoint. The later accepted
`docs/superpowers/plans/2026-09-07-mortal-effect-rollback-fixtures.md` repairs the
entire14-row cohort using a registered skill source, with no production legacy
fallback or weaker rollback assertions.

GM synchronization includes API, daemon, turn guide, afterlife matrix/glossary,
worked example, runtime manifest and executable source guard. Mortal commands,
art/progression/healing, player UI and daemon entrypoint paths are unchanged.
The remaining T083/T085/T089-T092/T177/#1536 requirements stay open; top-level
completion remains77/177. No issue closure or remote integration is implied.
