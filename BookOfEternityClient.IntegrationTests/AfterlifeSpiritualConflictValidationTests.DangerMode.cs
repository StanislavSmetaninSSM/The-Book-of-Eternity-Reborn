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
        var issue = Assert.Single(issues, IsDangerModeIssue);
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
