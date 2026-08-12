using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ValidationPhaseSelectionTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fileSystem;
    private readonly ValidationService _validator;

    public ValidationPhaseSelectionTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-validation-phase-selection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        _fileSystem.EnsureDirectoryStructure();
        _validator = new ValidationService(
            _fileSystem,
            NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_EmptyPhaseSelection_FailsClosed()
    {
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _validator.ValidateGameStateAsync(GameStateValidationPhase.None));

        Assert.Equal("phases", exception.ParamName);
    }

    [Fact]
    public async Task ValidateGameStateAsync_UnknownPhaseSelection_FailsClosed()
    {
        var unknownPhase = (GameStateValidationPhase)(1u << 31);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _validator.ValidateGameStateAsync(unknownPhase));

        Assert.Equal("phases", exception.ParamName);
    }

    [Fact]
    public async Task ValidateGameStateAsync_SinglePhase_DoesNotRunUnselectedPhase()
    {
        await _fileSystem.WriteFileAtomicAsync(
            "game_state/misc/phase_selection_invalid.json",
            "{");

        var jsonIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.JsonIntegrity);
        var requiredFileIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.RequiredFiles);

        Assert.Contains(jsonIssues, issue => issue.Code == "invalid_json_file");
        Assert.DoesNotContain(requiredFileIssues, issue => issue.Code == "invalid_json_file");
    }

    [Fact]
    public async Task ValidateGameStateAsync_CombinedSelection_PreservesCanonicalOrder()
    {
        await _fileSystem.WriteFileAtomicAsync(
            "game_state/misc/phase_selection_invalid.json",
            "{");

        var jsonIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.JsonIntegrity);
        var requiredFileIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.RequiredFiles);

        var combinedIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.RequiredFiles |
            GameStateValidationPhase.JsonIntegrity);

        Assert.Equal(
            Snapshot(jsonIssues.Concat(requiredFileIssues)),
            Snapshot(combinedIssues));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CombinedCrossReferenceSelection_DoesNotDuplicateRivalResidentIssues()
    {
        await _fileSystem.WriteFileAtomicAsync(
            "game_state/world/rival_soul_arcs.json",
            "{");

        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.CrossReferences |
            GameStateValidationPhase.RivalAndResidentCrossReferences);

        Assert.Single(
            issues,
            issue => issue.Code == "rival_arc_invalid_current_state");
    }

    [Fact]
    public async Task ValidateGameStateAsync_ConsecutiveSelections_DoNotLeakPhaseState()
    {
        const string path = "game_state/misc/phase_selection_invalid.json";
        await _fileSystem.WriteFileAtomicAsync(path, "{");

        var firstIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.JsonIntegrity);

        await _fileSystem.WriteFileAtomicAsync(path, "{}");
        var secondIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.RequiredFiles);

        Assert.Contains(firstIssues, issue => issue.Code == "invalid_json_file");
        Assert.DoesNotContain(secondIssues, issue => issue.Code == "invalid_json_file");
    }

    [Fact]
    public async Task ValidateGameStateAsync_StateFileSelection_SkipsUnselectedFiles()
    {
        const string selectedPath = "game_state/meta/guardians.json";
        const string unselectedPath = "game_state/meta/soul_state.json";
        await _fileSystem.WriteFileAtomicAsync(selectedPath, "\"invalid root\"");
        await _fileSystem.WriteFileAtomicAsync(unselectedPath, "\"invalid root\"");

        var selection = new GameStateValidationSelection(
            GameStateValidationPhase.MetaMiscStateFiles,
            new[] { selectedPath });

        var issues = await _validator.ValidateGameStateAsync(selection);

        Assert.Contains(issues, issue =>
            issue.FilePath == selectedPath &&
            issue.Code == "flexible_state_invalid_root");
        Assert.DoesNotContain(issues, issue => issue.FilePath == unselectedPath);
    }

    [Fact]
    public async Task ValidateGameStateAsync_GuardianProjectTargetedSelection_RunsProjectRulesWithoutMetaMiscDuplication()
    {
        await _fileSystem.WriteFileAtomicAsync(
            GuardianProjectState.TrackerPath,
            "{\"startGuardianProjects\":{}}");

        var targetedIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.GuardianProjectStateFiles);
        var combinedIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.MetaMiscStateFiles |
            GameStateValidationPhase.GuardianProjectStateFiles);

        Assert.Contains(targetedIssues, issue =>
            issue.FilePath == GuardianProjectState.TrackerPath);
        Assert.Equal(
            combinedIssues.Count(issue => issue.FilePath == GuardianProjectState.TrackerPath),
            combinedIssues
                .Where(issue => issue.FilePath == GuardianProjectState.TrackerPath)
                .DistinctBy(issue => (issue.Code, issue.Message, issue.Expected, issue.Actual))
                .Count());
    }

    [Fact]
    public void FactionMaterializationPhase_IsIncludedInAllAndSelectable()
    {
        Assert.True(
            GameStateValidationPhase.All.HasFlag(
                GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness));
        Assert.True(
            GameStateValidationPhase.Selectable.HasFlag(
                GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness));
    }

    [Fact]
    public void ItemMaterializationPhase_IsIncludedInAllAndSelectable()
    {
        Assert.True(
            GameStateValidationPhase.All.HasFlag(
                GameStateValidationPhase.AcceptedTurnItemMaterializationCompleteness));
        Assert.True(
            GameStateValidationPhase.Selectable.HasFlag(
                GameStateValidationPhase.AcceptedTurnItemMaterializationCompleteness));
    }

    [Fact]
    public void LocationMaterializationPhase_IsIncludedInAllAndSelectable()
    {
        Assert.True(
            GameStateValidationPhase.All.HasFlag(
                GameStateValidationPhase.AcceptedTurnLocationMaterializationCompleteness));
        Assert.True(
            GameStateValidationPhase.Selectable.HasFlag(
                GameStateValidationPhase.AcceptedTurnLocationMaterializationCompleteness));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LocationMaterializationSelection_DoesNotRunJsonIntegrity()
    {
        await _fileSystem.WriteFileAtomicAsync(
            "game_state/misc/phase_selection_invalid.json",
            "{");
        var canonical = MortalLocationTestFixture.CreateCanonicalLocation();
        await _fileSystem.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(
                MortalLocationTestFixture.CreateReceiptlessNegative()).ToJsonString());
        await _fileSystem.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationTestFixture.CreateIdentityIndex(canonical).ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnLocationMaterializationCompleteness);

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_materialization_receipt_required");
        Assert.DoesNotContain(issues, issue => issue.Code == "invalid_json_file");
    }

    [Fact]
    public async Task ValidateGameStateAsync_ItemMaterializationSelection_DoesNotRunJsonIntegrity()
    {
        await _fileSystem.WriteFileAtomicAsync(
            "game_state/misc/phase_selection_invalid.json",
            "{");
        await _fileSystem.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(MortalItemTestFixture.CreateReceiptlessNegative())
            }.ToJsonString());
        await _fileSystem.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnItemMaterializationCompleteness);

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_materialization_receiptless_current_item");
        Assert.DoesNotContain(issues, issue => issue.Code == "invalid_json_file");
    }

    [Fact]
    public async Task ValidateGameStateAsync_CombinedItemSelection_PreservesCanonicalOrder()
    {
        await _fileSystem.WriteFileAtomicAsync(
            "game_state/misc/phase_selection_invalid.json",
            "{");
        await _fileSystem.WriteFileAtomicAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(MortalItemTestFixture.CreateReceiptlessNegative())
            }.ToJsonString());
        await _fileSystem.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());

        var jsonIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.JsonIntegrity);
        var itemIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnItemMaterializationCompleteness);
        var combinedIssues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnItemMaterializationCompleteness |
            GameStateValidationPhase.JsonIntegrity);

        Assert.Equal(
            Snapshot(jsonIssues.Concat(itemIssues)),
            Snapshot(combinedIssues));
    }

    [Fact]
    public void ValidateResponse_RawMortalLocationIssue_AttachesExactStructuredRepairContext()
    {
        var rawLocation = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        rawLocation.Remove("customStates");
        var response = new JsonObject
        {
            ["response"] = "Переход к Чёрному броду.",
            ["currentLocationData"] = rawLocation
        };
        using var document = JsonDocument.Parse(response.ToJsonString());

        var issues = _validator.ValidateResponse(document.RootElement);

        var issue = Assert.Single(issues, candidate =>
            candidate.FilePath == "response.currentLocationData.customStates" &&
            candidate.Code == "mortal_location_materialization_governed_field_missing");
        var repair = Assert.IsType<MortalLocationRepairContext>(issue.MortalLocationRepairContext);
        Assert.Equal("currentLocationData", repair.CarrierPath);
        Assert.Equal("mortal_location", repair.EntityKind);
        Assert.Equal(MortalLocationTestFixture.LocationInitialId, repair.InitialId);
        Assert.Equal(MortalLocationTestFixture.LocationMaterializationId, repair.MaterializationId);
        Assert.Equal(new[] { "customStates" }, repair.RepairableFields);
    }

    [Fact]
    public void ValidateResponse_RawMortalLinkIssue_AttachesExactStructuredRepairContext()
    {
        var rawLink = MortalLocationTestFixture.CreateRawLink("loc_source", "loc_target");
        rawLink.Remove("description");
        var response = new JsonObject
        {
            ["response"] = "Открыта тропа.",
            ["worldMapUpdates"] = new JsonObject
            {
                ["newLocations"] = new JsonArray(),
                ["newLinks"] = new JsonArray(rawLink)
            }
        };
        using var document = JsonDocument.Parse(response.ToJsonString());

        var issues = _validator.ValidateResponse(document.RootElement);

        var issue = Assert.Single(issues, candidate =>
            candidate.FilePath == "response.worldMapUpdates.newLinks[0].description" &&
            candidate.Code == "mortal_location_materialization_governed_field_missing");
        var repair = Assert.IsType<MortalLocationRepairContext>(issue.MortalLocationRepairContext);
        Assert.Equal("worldMapUpdates.newLinks[0]", repair.CarrierPath);
        Assert.Equal("mortal_location_link", repair.EntityKind);
        Assert.Equal(MortalLocationTestFixture.LinkInitialId, repair.InitialId);
        Assert.Equal(MortalLocationTestFixture.LinkMaterializationId, repair.MaterializationId);
        Assert.Equal(new[] { "description" }, repair.RepairableFields);
    }

    [Fact]
    public void ValidateResponse_CurrentCreationWithClientOwnedPermanentId_IsRejected()
    {
        var rawLocation = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        rawLocation["locationId"] = "loc_forged_by_gm";
        var response = new JsonObject
        {
            ["response"] = "Переход к Чёрному броду.",
            ["currentLocationData"] = rawLocation
        };
        using var document = JsonDocument.Parse(response.ToJsonString());

        var issues = _validator.ValidateResponse(document.RootElement);

        Assert.Contains(issues, issue =>
            issue.FilePath == "response.currentLocationData.locationId" &&
            issue.Code == "mortal_location_materialization_identity_conflict");
    }

    [Fact]
    public void AcceptedTurnReasoningSelection_RequiresCoreAndRejectsUnknownScopes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AcceptedTurnReasoningValidationSelection(
            AcceptedTurnReasoningValidationScope.GuardianMusing));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AcceptedTurnReasoningValidationSelection(
            (AcceptedTurnReasoningValidationScope)(1 << 7) |
            AcceptedTurnReasoningValidationScope.Core));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private static IReadOnlyList<ValidationIssueSnapshot> Snapshot(
        IEnumerable<ValidationIssue> issues)
    {
        return issues
            .Select(issue => new ValidationIssueSnapshot(
                issue.FilePath,
                issue.Severity,
                issue.Message,
                issue.Category,
                issue.Code,
                issue.Actor,
                issue.Section,
                issue.Expected,
                issue.Actual,
                issue.RepairHint))
            .ToArray();
    }

    private sealed record ValidationIssueSnapshot(
        string FilePath,
        IssueSeverity Severity,
        string Message,
        IssueCategory Category,
        string? Code,
        string? Actor,
        string? Section,
        string? Expected,
        string? Actual,
        string? RepairHint);
}
