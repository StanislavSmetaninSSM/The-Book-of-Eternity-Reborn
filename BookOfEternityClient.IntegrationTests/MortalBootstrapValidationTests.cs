using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class MortalBootstrapValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public MortalBootstrapValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-mortal-bootstrap-validation-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(_rootPath, "game_session"));

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public void MortalBootstrapStateBuilder_BuildsCanonicalBaselineWithoutKnownRepairLoopShapes()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Мирон, молодой архивариус-изгнанник.",
            worldDescription: "Город-государство у болот и старых руин.",
            startingCircumstances: "Мирон приходит в себя ночью в архивной башне после кражи запретной описи.",
            createdAtUtc: DateTimeOffset.Parse("2026-06-29T07:00:00Z"));

        Assert.Contains("game_state/world/current_location.json", files.Keys);
        Assert.Contains("game_state/world/world_map.json", files.Keys);
        Assert.Contains("game_state/factions/faction_core.json", files.Keys);
        Assert.Contains("game_state/quests/regular_quests.json", files.Keys);
        Assert.Contains("game_state/inventory/items.json", files.Keys);
        Assert.Contains(MortalItemIdentityState.StatePath, files.Keys);
        Assert.Contains("game_state/inventory/item_resources.json", files.Keys);
        Assert.Contains("game_state/inventory/item_bonds.json", files.Keys);
        Assert.Contains("game_state/inventory/item_text_updates.json", files.Keys);
        Assert.Contains("game_state/npcs/item_journals.json", files.Keys);
        Assert.Contains("game_state/player/experience.json", files.Keys);
        Assert.Contains("game_state/player/skills_active.json", files.Keys);
        Assert.Contains("game_state/player/skills_passive.json", files.Keys);
        Assert.Contains("game_state/player/skill_mastery.json", files.Keys);
        Assert.Contains("lore/codex_entries.json", files.Keys);
        Assert.DoesNotContain("game_state/npcs/npc_core.json", files.Keys);

        var currentLocation = files[MortalLocationMaterializationContract.CurrentLocationPath];
        Assert.True(JsonNode.DeepEquals(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locationId"] = null,
                ["state"] = "pending_materialization"
            },
            currentLocation));
        Assert.DoesNotContain("knownExits", currentLocation);
        Assert.DoesNotContain("adjacencyMap", currentLocation);

        var worldMap = files[MortalLocationMaterializationContract.WorldMapPath];
        Assert.True(JsonNode.DeepEquals(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            },
            worldMap));
        Assert.DoesNotContain("newLocations", worldMap);
        Assert.DoesNotContain("newLinks", worldMap);
        Assert.DoesNotContain("worldMapUpdates", worldMap);

        Assert.Contains(MortalLocationIdentityState.StatePath, files.Keys);
        Assert.True(JsonNode.DeepEquals(
            MortalLocationIdentityState.CreateEmptyRoot(),
            files[MortalLocationIdentityState.StatePath]));

        Assert.Empty(files["game_state/factions/faction_core.json"]!["factions"]!.AsArray());

        var factionResources = files["game_state/factions/faction_resources.json"];
        Assert.Empty(factionResources["entries"]!.AsArray());

        Assert.Empty(files["game_state/quests/regular_quests.json"]!["quests"]!.AsArray());

        var inventory = files["game_state/inventory/items.json"];
        Assert.Empty(inventory["items"]!.AsArray());
        Assert.Empty(inventory["equippedItems"]!.AsObject());
        Assert.False(inventory.ContainsKey("equipment"));
        Assert.False(inventory.ContainsKey("totalWeight"));
        Assert.False(inventory.ContainsKey("maxWeight"));

        Assert.True(JsonNode.DeepEquals(
            MortalItemIdentityState.CreateEmptyRoot(),
            files[MortalItemIdentityState.StatePath]));
        Assert.Empty(files["game_state/inventory/item_resources.json"]["entries"]!.AsArray());
        Assert.Empty(files["game_state/inventory/item_bonds.json"]["entries"]!.AsArray());
        Assert.Empty(files["game_state/inventory/item_text_updates.json"]["entries"]!.AsArray());
        Assert.Empty(files["game_state/npcs/item_journals.json"]["entries"]!.AsArray());

        var experience = files["game_state/player/experience.json"];
        Assert.Empty(experience);

        var activeSkills = files["game_state/player/skills_active.json"];
        Assert.Empty(activeSkills["activeSkillChanges"]!.AsArray());
        Assert.Empty(activeSkills["removeActiveSkills"]!.AsArray());

        var passiveSkills = files["game_state/player/skills_passive.json"];
        Assert.Empty(passiveSkills["passiveSkillChanges"]!.AsArray());
        Assert.Empty(passiveSkills["removePassiveSkills"]!.AsArray());

        var skillMastery = files["game_state/player/skill_mastery.json"];
        Assert.Empty(skillMastery["skillMasteryChanges"]!.AsArray());

        var codexEntries = files["lore/codex_entries.json"]!["entries"]!.AsArray();
        var currentWorldEntry = Assert.Single(codexEntries.OfType<JsonObject>());
        Assert.Equal("codex_life_001_world", currentWorldEntry["entryId"]!.GetValue<string>());
        Assert.StartsWith(
            "current_world/",
            currentWorldEntry["sourceFile"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void MortalBootstrapLocationScaffold_ReservesExactOrdinaryMaterializationCoordinates()
    {
        var request = MortalBootstrapLocationScaffold.CreatePendingRequest(
            incarnationNumber: 7,
            sessionId: "session_bootstrap_7",
            requestId: "request_bootstrap_7",
            turnNumber: 1);

        Assert.Equal("pending", request["state"]!.GetValue<string>());
        Assert.Equal("request_bootstrap_7", request["requestId"]!.GetValue<string>());
        Assert.Equal(
            "mortal_bootstrap_scaffold",
            request["sourceAuthority"]!["kind"]!.GetValue<string>());
        Assert.Equal(
            "request_bootstrap_7",
            request["sourceAuthority"]!["authorityId"]!.GetValue<string>());

        var start = request["startReservation"]!.AsObject();
        Assert.Equal("locref_life_007_start", start["initialId"]!.GetValue<string>());
        Assert.Equal("loc_life_007_start", start["reservedLocationId"]!.GetValue<string>());
        Assert.Equal("current_scene_creation", start["route"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(
            new JsonObject { ["x"] = 0, ["y"] = 0, ["z"] = 0 },
            start["coordinates"]));
        Assert.True(JsonNode.DeepEquals(
            new JsonObject { ["tier"] = "visited", ["audience"] = "player_known" },
            start["requiredDiscovery"]));

        var neighbor = request["neighborReservation"]!.AsObject();
        Assert.Equal("locref_life_007_neighbor", neighbor["initialId"]!.GetValue<string>());
        Assert.Equal("loc_life_007_neighbor", neighbor["reservedLocationId"]!.GetValue<string>());
        Assert.Equal("world_map_creation", neighbor["route"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(
            new JsonObject { ["x"] = 1, ["y"] = 0, ["z"] = 0 },
            neighbor["coordinates"]));

        var link = request["linkReservation"]!.AsObject();
        Assert.Equal("linkref_life_007_start_to_neighbor", link["initialId"]!.GetValue<string>());
        Assert.Equal("lnk_life_007_start_to_neighbor", link["reservedLinkId"]!.GetValue<string>());
        Assert.Equal("locref_life_007_start", link["sourceInitialId"]!.GetValue<string>());
        Assert.Equal("locref_life_007_neighbor", link["targetInitialId"]!.GetValue<string>());
        Assert.Equal("world_map_link_creation", link["route"]!.GetValue<string>());

        Assert.Equal(
            new[] { "materialized_neighbor_link", "narrative_only_unresolved_exit" },
            request["allowedCompletionBranches"]!.AsArray()
                .Select(static value => value!.GetValue<string>())
                .ToArray());

        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 7,
            turnNumber: 1,
            characterDescription: "Тестовый герой.",
            worldDescription: "Тестовый мир.",
            startingCircumstances: "Начало первой сцены.",
            createdAtUtc: DateTimeOffset.Parse("2026-08-12T00:00:00Z"));
        var gmWritableLocationJson = string.Join(
            "\n",
            files
                .Where(pair => pair.Key is "game_state/world/current_location.json" or
                    "game_state/world/world_map.json" or
                    "lore/current_world/geography.json")
                .Select(pair => pair.Value.ToJsonString()));
        Assert.DoesNotContain("locref_life_007", gmWritableLocationJson, StringComparison.Ordinal);
        Assert.DoesNotContain("loc_life_007", gmWritableLocationJson, StringComparison.Ordinal);
        Assert.DoesNotContain("linkref_life_007", gmWritableLocationJson, StringComparison.Ordinal);
        Assert.DoesNotContain("lnk_life_007", gmWritableLocationJson, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalBootstrapLocationPlan_CompleteStartNeighborAndLinkConsumesExactReservations()
    {
        var scaffold = MortalBootstrapLocationScaffold.CreatePendingRequest(
            7,
            "session_bootstrap_7",
            "request_bootstrap_7",
            1);
        var start = CreateBootstrapLocation(scaffold, "startReservation", "mlocmat_bootstrap_start");
        var neighbor = CreateBootstrapLocation(scaffold, "neighborReservation", "mlocmat_bootstrap_neighbor");
        MarkBootstrapTopologyPopulated(start, neighbor);
        neighbor["discovery"] = new JsonObject
        {
            ["tier"] = "discovered",
            ["audience"] = "player_known",
            ["rumorSummary"] = null
        };
        var link = CreateBootstrapLink(scaffold, "mlinkmat_bootstrap_start_to_neighbor");

        var result = BuildBootstrapLocationPlan(scaffold, start, neighbor, link);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        Assert.Equal("loc_life_007_start", plan.LocationIdsByInitialId["locref_life_007_start"]);
        Assert.Equal("loc_life_007_neighbor", plan.LocationIdsByInitialId["locref_life_007_neighbor"]);
        Assert.Equal("lnk_life_007_start_to_neighbor", plan.LinkIdsByInitialId["linkref_life_007_start_to_neighbor"]);
        Assert.Equal("loc_life_007_start", plan.FinalCurrentLocation!["locationId"]!.GetValue<string>());
        Assert.Equal("settled", plan.FinalBootstrapScaffold!["state"]!.GetValue<string>());
        Assert.Equal(
            MortalBootstrapLocationScaffold.MaterializedNeighborBranch,
            plan.FinalBootstrapScaffold["settlement"]!["branch"]!.GetValue<string>());
        Assert.Equal(2, plan.FinalWorldMap["locations"]!.AsArray().Count);
        Assert.Single(plan.FinalWorldMap["links"]!.AsArray());
    }

    [Fact]
    public void MortalBootstrapLocationPlan_StartOnlyConsumesNarrativeOnlyBranchWithoutFakeNeighbor()
    {
        var scaffold = MortalBootstrapLocationScaffold.CreatePendingRequest(
            8,
            "session_bootstrap_8",
            "request_bootstrap_8",
            1);
        var start = CreateBootstrapLocation(scaffold, "startReservation", "mlocmat_bootstrap_start_only");

        var result = BuildBootstrapLocationPlan(scaffold, start);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        Assert.Equal("loc_life_008_start", plan.FinalCurrentLocation!["locationId"]!.GetValue<string>());
        Assert.Single(plan.FinalWorldMap["locations"]!.AsArray());
        Assert.Empty(plan.FinalWorldMap["links"]!.AsArray());
        Assert.Equal(
            MortalBootstrapLocationScaffold.NarrativeOnlyBranch,
            plan.FinalBootstrapScaffold!["settlement"]!["branch"]!.GetValue<string>());
        Assert.Null(plan.FinalBootstrapScaffold["settlement"]!["neighborLocationId"]);
        Assert.Null(plan.FinalBootstrapScaffold["settlement"]!["linkId"]);
    }

    [Theory]
    [InlineData("missing-start", "mortal_bootstrap_location_start_required")]
    [InlineData("partial-start", "mortal_location_materialization_governed_field_missing")]
    [InlineData("reservation-alias", "mortal_bootstrap_location_reservation_mismatch")]
    [InlineData("duplicate-route", "mortal_location_materialization_duplicate_creation_route")]
    [InlineData("fake-neighbor", "mortal_bootstrap_location_reservation_mismatch")]
    [InlineData("wrong-authority", "mortal_bootstrap_location_authority_mismatch")]
    [InlineData("settled-replay", "mortal_bootstrap_location_reservation_replay")]
    public void MortalBootstrapLocationPlan_InvalidReservationUseFailsClosed(
        string scenario,
        string expectedCode)
    {
        var scaffold = MortalBootstrapLocationScaffold.CreatePendingRequest(
            9,
            "session_bootstrap_9",
            "request_bootstrap_9",
            1);
        JsonObject? start = CreateBootstrapLocation(scaffold, "startReservation", "mlocmat_bootstrap_negative_start");
        JsonObject? neighbor = null;
        JsonObject? link = null;

        switch (scenario)
        {
            case "missing-start":
                start = null;
                break;
            case "partial-start":
                start.Remove("description");
                break;
            case "reservation-alias":
                start["initialId"] = "LOCREF_life_009_start";
                start["materialization"]!["initialId"] = "LOCREF_life_009_start";
                break;
            case "duplicate-route":
                neighbor = start.DeepClone().AsObject();
                neighbor["materialization"]!["route"] = "world_map_creation";
                break;
            case "fake-neighbor":
                neighbor = CreateBootstrapLocation(scaffold, "neighborReservation", "mlocmat_bootstrap_fake_neighbor");
                neighbor["initialId"] = "locref_life_009_fake_neighbor";
                neighbor["materialization"]!["initialId"] = "locref_life_009_fake_neighbor";
                link = CreateBootstrapLink(scaffold, "mlinkmat_bootstrap_fake_neighbor");
                link["targetInitialId"] = "locref_life_009_fake_neighbor";
                break;
            case "wrong-authority":
                start["materialization"]!["sourceAuthority"]!["authorityId"] = "request_other";
                break;
            case "settled-replay":
                scaffold["state"] = "settled";
                scaffold["settlement"] = new JsonObject
                {
                    ["requestId"] = "request_bootstrap_9",
                    ["acceptedTurn"] = 1,
                    ["branch"] = MortalBootstrapLocationScaffold.NarrativeOnlyBranch,
                    ["startLocationId"] = "loc_life_009_start",
                    ["neighborLocationId"] = null,
                    ["linkId"] = null
                };
                break;
        }

        var result = BuildBootstrapLocationPlan(scaffold, start, neighbor, link);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public async Task MortalBootstrapLocationValidationAndNormalizer_SettleNarrativeBranchExactlyOnce()
    {
        var (scaffoldRoot, request, baselineFiles) =
            await WriteBootstrapLocationBaselineAndSnapshotAsync(incarnationNumber: 10);
        var start = CreateBootstrapLocation(
            request,
            "startReservation",
            "mlocmat_bootstrap_validation_start");
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = start }.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["worldMapUpdates"] = new JsonObject
                {
                    ["newLocations"] = new JsonArray(),
                    ["newLinks"] = new JsonArray()
                }
            }.ToJsonString());

        var rawIssues = await _validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.DoesNotContain(rawIssues, issue =>
            issue.Code?.StartsWith("mortal_bootstrap_location_", StringComparison.Ordinal) == true);

        var backups = baselineFiles.ToDictionary(
            static path => path,
            static path => $"game_state/control/pending_turn_snapshot/{path}",
            StringComparer.OrdinalIgnoreCase);
        var normalizer = new CanonicalStateNormalizer(
            _fs,
            NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeMortalLocationsAsync(backups);

        var settledScaffold = JsonNode.Parse(
            (await _fs.ReadFileAsync(MortalBootstrapLocationScaffold.StatePath))!)!.AsObject();
        var settledRequest = settledScaffold["locationMaterializationRequest"]!.AsObject();
        Assert.Equal("settled", settledRequest["state"]!.GetValue<string>());
        Assert.Equal(
            MortalBootstrapLocationScaffold.NarrativeOnlyBranch,
            settledRequest["settlement"]!["branch"]!.GetValue<string>());
        Assert.Equal(
            "loc_life_010_start",
            settledRequest["settlement"]!["startLocationId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(
            scaffoldRoot["locationMaterializationRequest"]!["startReservation"],
            settledRequest["startReservation"]));

        var canonicalIssues =
            await _validator.ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync();
        Assert.DoesNotContain(canonicalIssues, issue => issue.Severity == IssueSeverity.Error);

        var settledBaseline = new List<(string Path, string Json)>();
        foreach (var path in baselineFiles)
        {
            settledBaseline.Add((path, (await _fs.ReadFileAsync(path))!));
        }
        await WriteValidatedSnapshotManifestAsync(settledBaseline.ToArray());

        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = start.DeepClone() }.ToJsonString());
        var replayIssues = await _validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();
        Assert.Contains(replayIssues, issue =>
            issue.Code == "mortal_bootstrap_location_reservation_replay");
    }

    [Fact]
    public async Task MortalBootstrapLocationValidation_InvalidAliasRetainsPendingReservation()
    {
        var (_, request, _) = await WriteBootstrapLocationBaselineAndSnapshotAsync(
            incarnationNumber: 11);
        var start = CreateBootstrapLocation(
            request,
            "startReservation",
            "mlocmat_bootstrap_validation_alias");
        start["initialId"] = "LOCREF_life_011_start";
        start["materialization"]!["initialId"] = "LOCREF_life_011_start";
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = start }.ToJsonString());

        var issues = await _validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_bootstrap_location_reservation_mismatch");
        var currentScaffold = JsonNode.Parse(
            (await _fs.ReadFileAsync(MortalBootstrapLocationScaffold.StatePath))!)!.AsObject();
        Assert.Equal(
            "pending",
            currentScaffold["locationMaterializationRequest"]!["state"]!.GetValue<string>());
        Assert.Null(currentScaffold["locationMaterializationRequest"]!["settlement"]);
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("forged-settled")]
    public async Task MortalBootstrapLocationValidation_CurrentScaffoldMustMatchValidatedPreTurnAuthority(
        string mutation)
    {
        var (scaffoldRoot, request, _) =
            await WriteBootstrapLocationBaselineAndSnapshotAsync(incarnationNumber: 13);
        var start = CreateBootstrapLocation(
            request,
            "startReservation",
            "mlocmat_bootstrap_protected_scaffold");
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = start }.ToJsonString());

        if (mutation == "deleted")
        {
            _fs.DeleteFile(MortalBootstrapLocationScaffold.StatePath);
        }
        else
        {
            var forged = scaffoldRoot.DeepClone().AsObject();
            var forgedRequest = forged["locationMaterializationRequest"]!.AsObject();
            forgedRequest["state"] = "settled";
            forgedRequest["settlement"] = new JsonObject
            {
                ["requestId"] = request["requestId"]!.DeepClone(),
                ["acceptedTurn"] = request["turnNumber"]!.DeepClone(),
                ["branch"] = MortalBootstrapLocationScaffold.NarrativeOnlyBranch,
                ["startLocationId"] = "loc_life_013_start",
                ["neighborLocationId"] = null,
                ["linkId"] = null
            };
            await _fs.WriteFileAtomicAsync(
                MortalBootstrapLocationScaffold.StatePath,
                forged.ToJsonString());
        }

        var issues = await _validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        var issue = Assert.Single(issues, candidate =>
            candidate.Code == "mortal_bootstrap_location_scaffold_mutated");
        Assert.Equal(MortalBootstrapLocationScaffold.StatePath, issue.FilePath);
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(issues));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MortalBootstrapLocationValidation_NewEmptyScaffoldIsProtectedMutation(
        string forgedContent)
    {
        var map = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };
        var index = MortalLocationIdentityState.CreateEmptyRoot();
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            map.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            index.ToJsonString());
        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "ordinary Mortal turn",
            includeSnapshotFilesAsRollbackBaseline: true,
            (MortalLocationMaterializationContract.WorldMapPath, map.ToJsonString()),
            (MortalLocationIdentityState.StatePath, index.ToJsonString()));
        await _fs.WriteFileAtomicAsync(
            MortalBootstrapLocationScaffold.StatePath,
            forgedContent);
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject
            {
                ["currentLocationData"] = MortalLocationTestFixture.CreateRawLocation(
                    "current_scene_creation")
            }.ToJsonString());

        var issues = await _validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_bootstrap_location_scaffold_mutated");
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(issues));
    }

    [Fact]
    public async Task MortalBootstrapLocationCanonicalValidation_DuplicateSettledLocationReportsIssueInsteadOfThrowing()
    {
        var (_, request, baselineFiles) =
            await WriteBootstrapLocationBaselineAndSnapshotAsync(incarnationNumber: 12);
        var start = CreateBootstrapLocation(
            request,
            "startReservation",
            "mlocmat_bootstrap_duplicate_settlement");
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = start }.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["worldMapUpdates"] = new JsonObject
                {
                    ["newLocations"] = new JsonArray(),
                    ["newLinks"] = new JsonArray()
                }
            }.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(
            _fs,
            NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeMortalLocationsAsync(baselineFiles.ToDictionary(
            static path => path,
            static path => $"game_state/control/pending_turn_snapshot/{path}",
            StringComparer.OrdinalIgnoreCase));
        var map = JsonNode.Parse((await _fs.ReadFileAsync(
            MortalLocationMaterializationContract.WorldMapPath))!)!.AsObject();
        var locations = map["locations"]!.AsArray();
        locations.Add(locations[0]!.DeepClone());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            map.ToJsonString());

        var issues = await _validator.ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_bootstrap_location_settlement_invalid");
    }

    private async Task<(JsonObject ScaffoldRoot, JsonObject Request, string[] BaselineFiles)>
        WriteBootstrapLocationBaselineAndSnapshotAsync(int incarnationNumber)
    {
        var request = MortalBootstrapLocationScaffold.CreatePendingRequest(
            incarnationNumber,
            "session_mortal_bootstrap_validation_tests",
            "request_mortal_bootstrap_validation_tests",
            3);
        var scaffoldRoot = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["purpose"] = "fresh_mortal_world_bootstrap",
            ["requestId"] = "request_mortal_bootstrap_validation_tests",
            ["locationMaterializationRequest"] = request.DeepClone()
        };
        var map = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };
        var current = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locationId"] = null,
            ["state"] = "pending_materialization"
        };
        var index = MortalLocationIdentityState.CreateEmptyRoot();
        var baseline = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            [MortalLocationMaterializationContract.WorldMapPath] = map,
            [MortalLocationMaterializationContract.CurrentLocationPath] = current,
            [MortalLocationIdentityState.StatePath] = index,
            [MortalBootstrapLocationScaffold.StatePath] = scaffoldRoot
        };
        foreach (var pair in baseline)
            await _fs.WriteFileAtomicAsync(pair.Key, pair.Value.ToJsonString());

        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "GM-инициированного воплощения",
            includeSnapshotFilesAsRollbackBaseline: true,
            baseline.Select(pair => (pair.Key, pair.Value.ToJsonString())).ToArray());
        return (scaffoldRoot, request, baseline.Keys.ToArray());
    }

    private static MortalLocationAcceptedTurnPlanningResult BuildBootstrapLocationPlan(
        JsonObject scaffold,
        JsonObject? start,
        JsonObject? neighbor = null,
        JsonObject? link = null)
    {
        var newLocations = new JsonArray();
        if (neighbor != null)
            newLocations.Add(neighbor.DeepClone());
        var newLinks = new JsonArray();
        if (link != null)
            newLinks.Add(link.DeepClone());

        return MortalLocationAcceptedTurnPlanner.Build(
            new MortalLocationAcceptedTurnInput(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["realm"] = "mortal_world",
                    ["locations"] = new JsonArray(),
                    ["links"] = new JsonArray()
                },
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["realm"] = "mortal_world",
                    ["locationId"] = null,
                    ["state"] = "pending_materialization"
                },
                MortalLocationIdentityState.CreateEmptyRoot(),
                start == null
                    ? null
                    : new JsonObject { ["currentLocationData"] = start.DeepClone() },
                new JsonObject
                {
                    ["worldMapUpdates"] = new JsonObject
                    {
                        ["newLocations"] = newLocations,
                        ["newLinks"] = newLinks
                    }
                },
                Turn: 1,
                BootstrapScaffold: scaffold));
    }

    private static JsonObject CreateBootstrapLocation(
        JsonObject scaffold,
        string reservationName,
        string materializationId)
    {
        var reservation = scaffold[reservationName]!.AsObject();
        var route = reservation["route"]!.GetValue<string>();
        var initialId = reservation["initialId"]!.GetValue<string>();
        var location = MortalLocationTestFixture.CreateRawLocation(route);
        location["initialId"] = initialId;
        location["coordinates"] = reservation["coordinates"]!.DeepClone();
        location["materialization"]!["initialId"] = initialId;
        location["materialization"]!["materializationId"] = materializationId;
        location["materialization"]!["sourceTurn"] = scaffold["turnNumber"]!.DeepClone();
        location["materialization"]!["sourceAuthority"] = scaffold["sourceAuthority"]!.DeepClone();
        return location;
    }

    private static JsonObject CreateBootstrapLink(JsonObject scaffold, string materializationId)
    {
        var reservation = scaffold["linkReservation"]!.AsObject();
        var link = MortalLocationTestFixture.CreateRawLink("source_placeholder", "target_placeholder");
        link["initialId"] = reservation["initialId"]!.DeepClone();
        link["sourceLocationId"] = null;
        link["sourceInitialId"] = reservation["sourceInitialId"]!.DeepClone();
        link["targetLocationId"] = null;
        link["targetInitialId"] = reservation["targetInitialId"]!.DeepClone();
        link["materialization"]!["initialId"] = reservation["initialId"]!.DeepClone();
        link["materialization"]!["materializationId"] = materializationId;
        link["materialization"]!["sourceTurn"] = scaffold["turnNumber"]!.DeepClone();
        link["materialization"]!["sourceAuthority"] = scaffold["sourceAuthority"]!.DeepClone();
        return link;
    }

    private static void MarkBootstrapTopologyPopulated(params JsonObject[] locations)
    {
        foreach (var location in locations)
        {
            location["materialization"]!["sections"]!["topology"] = new JsonObject
            {
                ["disposition"] = "populated",
                ["reason"] = null
            };
        }
    }

    [Fact]
    public void MortalBootstrapStateBuilder_UnstructuredProseDoesNotMaterializeMechanicsOrActors()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 4,
            characterDescription:
                "A starship marine, knife fighter, cartographer, hunter, merchant apprentice and noble courier.",
            worldDescription:
                "A post-apocalyptic science-fiction station with shops, paid lessons, teachers and traders.",
            startingCircumstances:
                "A mentor offers training, a vendor offers goods, and a sealed letter lies beside a runic glove.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-26T00:00:00Z"));

        Assert.DoesNotContain("game_state/npcs/npc_core.json", files.Keys);
        Assert.Empty(files["game_state/inventory/items.json"]["items"]!.AsArray());
        Assert.Empty(files["game_state/player/skills_active.json"]["activeSkillChanges"]!.AsArray());
        Assert.Empty(files["game_state/player/skills_passive.json"]["passiveSkillChanges"]!.AsArray());
        Assert.Empty(files["game_state/player/skill_mastery.json"]["skillMasteryChanges"]!.AsArray());

        Assert.Empty(files["game_state/player/experience.json"]);
    }

    [Theory]
    [InlineData("missing", true)]
    [InlineData("empty-object", true)]
    [InlineData("prose-only", true)]
    [InlineData("wrong-domain", true)]
    [InlineData("empty-values", true)]
    [InlineData("wrong-values", true)]
    [InlineData("bound", false)]
    public async Task ValidateGameStateAsync_FirstBootstrapMechanicsRequireDomainBoundStructuredGmAuthority(
        string authorityMode,
        bool expectAuthorityIssues)
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 4,
            characterDescription: "Навигатор исследовательской станции.",
            worldDescription: "Научно-фантастическая орбитальная колония.",
            startingCircumstances: "После аварии навигатор приходит в себя в центре связи.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-26T00:00:00Z"));

        files["game_state/player/experience.json"]["playerLevel"] = 1;
        files["game_state/player/experience.json"]["level"] = 1;
        files["game_state/player/experience.json"]["currentExperience"] = 0;
        files["game_state/player/experience.json"]["experience"] = 0;
        files["game_state/player/experience.json"]["totalExperience"] = 0;
        files["game_state/player/experience.json"]["experienceForNextLevel"] = 240;
        files["game_state/player/experience.json"]["experienceGained"] = 0;
        files["game_state/inventory/items.json"]["maxWeight"] = 35;
        files["game_state/inventory/items.json"]["totalWeight"] = 0;

        const string factionId = "faction_orbital_navigation";
        var faction = new JsonObject
        {
            ["factionId"] = factionId,
            ["name"] = "Навигаторы Кольца",
            ["displayName"] = "Навигаторы Кольца",
            ["description"] = "Служба дальней навигации орбитальной колонии.",
            ["type"] = "navigation_service",
            ["status"] = "active",
            ["visibility"] = "known",
            ["ranks"] = new JsonObject
            {
                ["entries"] = new JsonArray(),
                ["hierarchySummary"] = "Служебная иерархия навигаторов."
            },
            ["rankBranches"] = new JsonArray(),
            ["relations"] = new JsonArray(),
            ["controlledTerritories"] = new JsonArray(),
            ["projects"] = new JsonArray(),
            ["chronicle"] = new JsonArray(),
            ["customStates"] = new JsonArray()
        };
        faction["influence"] = 12;
        faction["powerProfile"] = new JsonObject { ["orbitalReach"] = 4 };
        files["game_state/factions/faction_core.json"]["factions"] = new JsonArray(faction);
        files["game_state/factions/faction_resources.json"]["entries"] = new JsonArray(
            new JsonObject
            {
                ["factionId"] = factionId,
                ["signalRelays"] = 2
            });
        files["game_state/world/current_location.json"]["factionControl"] = new JsonArray(
            new JsonObject
            {
                ["factionId"] = factionId,
                ["controlType"] = "Network",
                ["controlLevel"] = 12
            });

        JsonArray AuthorityEntries(string domain) =>
            authorityMode switch
            {
                "missing" => new JsonArray(),
                "empty-object" => new JsonArray(new JsonObject()),
                "prose-only" => new JsonArray(
                    new JsonObject { ["reason"] = $"Setting-defined {domain}." }),
                "wrong-domain" => new JsonArray(
                    new JsonObject
                    {
                        ["canonicalPath"] = "lore/current_world/world_setting.json",
                        ["values"] = new JsonObject { ["summary"] = "Unrelated prose." }
                    }),
                "empty-values" => new JsonArray(
                    new JsonObject
                    {
                        ["canonicalPath"] = domain switch
                        {
                            "progression" => "game_state/player/experience.json",
                            "carrying" => "game_state/inventory/items.json",
                            "faction" => "game_state/factions/faction_core.json",
                            _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null)
                        },
                        ["factionId"] = domain == "faction" ? factionId : null,
                        ["values"] = new JsonObject()
                    }),
                "wrong-values" => new JsonArray(
                    new JsonObject
                    {
                        ["canonicalPath"] = domain switch
                        {
                            "progression" => "game_state/player/experience.json",
                            "carrying" => "game_state/inventory/items.json",
                            "faction" => "game_state/factions/faction_core.json",
                            _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null)
                        },
                        ["factionId"] = domain == "faction" ? factionId : null,
                        ["values"] = domain switch
                        {
                            "progression" => new JsonObject { ["experienceForNextLevel"] = 999 },
                            "carrying" => new JsonObject { ["maxWeight"] = 999 },
                            "faction" => new JsonObject { ["influence"] = 999 },
                            _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null)
                        }
                    }),
                "bound" when domain == "progression" => new JsonArray(
                    new JsonObject
                    {
                        ["canonicalPath"] = "game_state/player/experience.json",
                        ["values"] = new JsonObject
                        {
                            ["playerLevel"] = 1,
                            ["level"] = 1,
                            ["currentExperience"] = 0,
                            ["experience"] = 0,
                            ["totalExperience"] = 0,
                            ["experienceForNextLevel"] = 240,
                            ["experienceGained"] = 0
                        },
                        ["reason"] = "The orbital setting uses a 240-point first progression interval."
                    }),
                "bound" when domain == "carrying" => new JsonArray(
                    new JsonObject
                    {
                        ["canonicalPath"] = "game_state/inventory/items.json",
                        ["values"] = new JsonObject
                        {
                            ["maxWeight"] = 35,
                            ["totalWeight"] = 0
                        },
                        ["reason"] = "The current load system uses kilograms."
                    }),
                "bound" when domain == "faction" => new JsonArray(
                    new JsonObject
                    {
                        ["canonicalPath"] = "game_state/factions/faction_core.json",
                        ["factionId"] = factionId,
                        ["values"] = new JsonObject
                        {
                            ["influence"] = 12,
                            ["powerProfile"] = new JsonObject { ["orbitalReach"] = 4 }
                        }
                    },
                    new JsonObject
                    {
                        ["canonicalPath"] = "game_state/factions/faction_resources.json",
                        ["factionId"] = factionId,
                        ["values"] = new JsonObject { ["signalRelays"] = 2 }
                    },
                    new JsonObject
                    {
                        ["canonicalPath"] = "game_state/world/current_location.json",
                        ["factionId"] = factionId,
                        ["values"] = new JsonObject
                        {
                            ["controlType"] = "Network",
                            ["controlLevel"] = 12
                        }
                    }),
                _ => throw new ArgumentOutOfRangeException(nameof(authorityMode), authorityMode, null)
            };

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Нейтральная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/mortal_bootstrap_scaffold.json",
            new JsonObject
            {
                ["requestId"] = "request_setting_authority",
                ["turnNumber"] = 4,
                ["structuredGmAuthority"] = new JsonObject
                {
                    ["playerSkills"] = new JsonArray(),
                    ["playerProgression"] = AuthorityEntries("progression"),
                    ["carryingRules"] = AuthorityEntries("carrying"),
                    ["factionMechanics"] = AuthorityEntries("faction")
                },
                ["worldEventRequirements"] = new JsonObject
                {
                    ["minimumCount"] = 1,
                    ["requiredEventIds"] = new JsonArray("world_event_life_001_opening")
                }
            }.ToJsonString());
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "sessionId": "session_setting_authority",
          "requestId": "request_setting_authority",
          "turnNumber": 4
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);
        var expectedCodes = new[]
        {
            "mortal_bootstrap_progression_requires_structured_gm_authority",
            "mortal_bootstrap_carrying_requires_structured_gm_authority",
            "mortal_bootstrap_faction_mechanics_require_structured_gm_authority"
        };

        foreach (var code in expectedCodes)
        {
            if (expectAuthorityIssues)
            {
                Assert.Contains(issues, issue =>
                    string.Equals(issue.Code, code, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                Assert.DoesNotContain(issues, issue =>
                    string.Equals(issue.Code, code, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public async Task ValidateGameStateAsync_UnstructuredBootstrapProseDoesNotDeclareActorCapabilities()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Нейтральная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "playerAuthoredStart": {
            "characterDescription": "A merchant apprentice wants to learn from a mentor.",
            "worldDescription": "A science-fiction station.",
            "startingCircumstances": "A teacher offers training while a trader sells goods."
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_requested_teacher_missing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "mortal_bootstrap_requested_trade_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MortalBootstrapStateBuilder_DoesNotInferResourcesFromPaidTrainingOrTradeProse()
    {
        var paidStartFiles = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 4,
            characterDescription: "Асурэн де Вальмонт, молодой аристократ-маг.",
            worldDescription: "Столица Этернии с платными уроками навыков и купеческой торговлей.",
            startingCircumstances: "За дверью ждёт наставница, которая продаёт первые уроки через витрину обучения, а рядом купец предлагает купить кинжал и бинты.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T02:00:00Z"));

        Assert.Empty(paidStartFiles["game_state/player/experience.json"]);
        Assert.DoesNotContain("game_state/npcs/npc_core.json", paidStartFiles.Keys);
        Assert.Empty(paidStartFiles["game_state/inventory/items.json"]["items"]!.AsArray());

        var plainStartFiles = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 4,
            characterDescription: "Мирон, молодой архивариус-изгнанник.",
            worldDescription: "Город-государство у болот и старых руин.",
            startingCircumstances: "Мирон приходит в себя ночью в архивной башне после кражи запретной описи.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T02:00:00Z"));

        Assert.Empty(plainStartFiles["game_state/player/experience.json"]);
    }

    [Fact]
    public async Task MortalBootstrapStateBuilder_DoesNotMaterializeTeacherUntilGmDeclaresCapability()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Асурэн де Вальмонт, молодой аристократ-маг.",
            worldDescription: "Столица Этернии с городскими наставниками и витринами обучения.",
            startingCircumstances: "За дверью ждёт наставница семейного архива, которая может обучить чтению печатей за плату.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T01:00:00Z"));

        Assert.DoesNotContain("game_state/npcs/npc_core.json", files.Keys);

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "baselineMaterializedBeforeDispatch": true,
          "playerAuthoredStart": {
            "characterDescription": "Асурэн де Вальмонт, молодой аристократ-маг.",
            "worldDescription": "Столица Этернии с городскими наставниками и витринами обучения.",
            "startingCircumstances": "За дверью ждёт наставница семейного архива, которая может обучить чтению печатей за плату."
          },
          "structuredGmAuthority": {
            "actorCapabilities": [
              { "capability": "canTeach", "required": true }
            ]
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_requested_teacher_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MortalBootstrapStateBuilder_DoesNotInferTeacherFromLearnIntent()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Кай Рен, молодой подмастерье картографа, хочет научиться защищаться ножом.",
            worldDescription: "Портовый город с картографическими мастерскими, купеческими домами и уличными бандами.",
            startingCircumstances: "Утро в мастерской старого картографа у причала Соляных Верфей. Мастер Орт велит Каю сверить печати.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-09T01:00:00Z"));

        Assert.Empty(files["game_state/player/experience.json"]);
        Assert.DoesNotContain("game_state/npcs/npc_core.json", files.Keys);
    }

    [Fact]
    public async Task ValidateGameStateAsync_LocalUiSessionLock_IsClientOwnedAndDoesNotFailStateContract()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Асурэн де Вальмонт, молодой аристократ-маг.",
            worldDescription: "Столица Этернии с городскими наставниками и витринами обучения.",
            startingCircumstances: "За дверью ждёт наставница семейного архива, которая может обучить чтению печатей за плату.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await new LocalUiSessionLockService(_fs).AcquireOrRefreshAsync(
            new LocalUiSessionLockOwner("console:test", "console", "Консольный тест", TimeSpan.FromMinutes(2)),
            "Команда /обучение");
        await TrainingRequestState.WriteRequestAsync(
            _fs,
            requestKind: "mortal_teacher_showcase",
            sourceActorId: "npc_life_001_start_teacher",
            sourceActorName: "Наставница семейного архива",
            sourceActorKind: "npc_teacher",
            realm: "mortal",
            createdAtTurn: 1,
            sourceActorSnapshotHash: "test-snapshot-hash",
            reason: "missing_showcase");

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.FilePath, LocalUiSessionLockService.LockPath, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(issue.Code, "missing_allowed_top_level_key", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(issue.Code, "strict_state_missing_allowed_top_level_key", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.FilePath, TrainingRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(issue.Code, "missing_allowed_top_level_key", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(issue.Code, "strict_state_missing_allowed_top_level_key", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MortalBootstrapStateBuilder_DoesNotInferTrackerCompetencyFromCharacterProse()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 5,
            characterDescription: "Молодая женщина-следопыт из обедневшего дворянского рода: зовут Асурэн де Вальмонт, носит дорожный плащ, умеет читать следы и скрывать страх за вежливостью.",
            worldDescription: "Тёмное фэнтези позднего средневековья.",
            startingCircumstances: "Асурэн приходит в себя в дорожной харчевне у северных ворот.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-05T01:00:00Z"));

        Assert.Empty(files["game_state/player/skills_passive.json"]["passiveSkillChanges"]!.AsArray());
        Assert.Empty(files["game_state/player/skills_active.json"]["activeSkillChanges"]!.AsArray());
        Assert.Empty(files["game_state/player/skill_mastery.json"]["skillMasteryChanges"]!.AsArray());
    }

    [Fact]
    public void MortalBootstrapStateBuilder_PreservesNarrativeWorldEventWithoutInferringMechanics()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 4,
            characterDescription: "Ренар Тис, 24-летний ученик картографа и курьер, умеющий обращаться с ножом.",
            worldDescription: "Мрачное низкое фэнтези, речной город Кальдер с гильдиями, домами и орденами.",
            startingCircumstances: "Картографическая мастерская «Медная стрелка» владельца Орта Веннера: пришёл курьер Речной гильдии, пропал землемер, на столе лежит запечатанный футляр карты Северной дамбы.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-11T10:59:27Z"));

        Assert.Empty(files["game_state/player/skills_active.json"]["activeSkillChanges"]!.AsArray());
        Assert.Empty(files["game_state/player/skills_passive.json"]["passiveSkillChanges"]!.AsArray());
        Assert.Empty(files["game_state/player/skill_mastery.json"]["skillMasteryChanges"]!.AsArray());
        Assert.Empty(files["game_state/inventory/items.json"]["items"]!.AsArray());
        Assert.DoesNotContain("game_state/npcs/npc_core.json", files.Keys);

        var worldEvents = files["game_state/world/world_events.json"]["worldEventsLog"]!.AsArray();
        var openingEvent = Assert.Single(worldEvents.OfType<JsonObject>());
        Assert.Equal("world_event_life_001_opening", openingEvent["eventId"]!.GetValue<string>());
        Assert.Contains("Картографическая мастерская", openingEvent["title"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Северной дамбы", openingEvent["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("local", openingEvent["visibility"]!.GetValue<string>());
    }

    [Fact]
    public async Task ValidateGameStateAsync_MortalBootstrapRejectsRemovedRequiredCompetenciesNpcAndWorldEvent()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 4,
            characterDescription: "Ренар Тис, 24-летний ученик картографа и курьер, умеющий обращаться с ножом.",
            worldDescription: "Мрачное низкое фэнтези, речной город Кальдер.",
            startingCircumstances: "Мастерская владельца Орта Веннера: пропал землемер у Северной дамбы.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-11T10:59:27Z"));

        foreach (var (path, node) in files)
        {
            if (!string.Equals(path, "game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase))
                await _fs.WriteFileAtomicAsync(path, node.ToJsonString());
        }

        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", """
        { "activeSkillChanges": [], "removeActiveSkills": [] }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/skills_passive.json", """
        { "passiveSkillChanges": [], "removePassiveSkills": [] }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", """
        { "skillMasteryChanges": [] }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        { "worldEventsLog": [] }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Элиан Безмолвный",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "requestId": "request_renar_missing_anchors",
          "turnNumber": 4,
          "playerAuthoredStart": {
            "characterDescription": "Ренар Тис, 24-летний ученик картографа и курьер, умеющий обращаться с ножом.",
            "worldDescription": "Мрачное низкое фэнтези, речной город Кальдер.",
            "startingCircumstances": "Мастерская владельца Орта Веннера: пропал землемер у Северной дамбы."
          },
          "structuredGmAuthority": {
            "playerSkills": [
              { "skillId": "starter_knife_handling", "skillName": "Обращение с ножом", "skillKind": "active" },
              { "skillId": "starter_cartography", "skillName": "Картография", "skillKind": "passive" },
              { "skillId": "starter_courier_training", "skillName": "Курьерская выучка", "skillKind": "passive" }
            ],
            "actorCapabilities": [
              { "capability": "canTeach", "required": true }
            ]
          },
          "worldEventRequirements": {
            "minimumCount": 1,
            "requiredEventIds": ["world_event_life_001_opening"]
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "sessionId": "session_renar_missing_anchors",
          "requestId": "request_renar_missing_anchors",
          "turnNumber": 4,
          "status": "success"
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_explicit_competency_missing", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("skills_active.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_explicit_competency_missing", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("skills_passive.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_explicit_competency_missing", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("skill_mastery.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_world_event_missing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_requested_teacher_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LaterMortalTurnDoesNotReapplyBootstrapCompetencyAndEventRequirements()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        { "soulName": "Элиан Безмолвный", "currentRealm": "Mortal World", "currentIncarnation": 1 }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "requestId": "request_bootstrap_turn_4",
          "turnNumber": 4,
          "structuredGmAuthority": {
            "playerSkills": [
              { "skillId": "starter_knife_handling", "skillName": "Обращение с ножом", "skillKind": "active" }
            ]
          },
          "worldEventRequirements": {
            "minimumCount": 1,
            "requiredEventIds": ["world_event_life_001_opening"]
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "sessionId": "session_later_turn",
          "requestId": "request_later_turn_5",
          "turnNumber": 5,
          "status": "success"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", """
        { "activeSkillChanges": [], "removeActiveSkills": [] }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        { "worldEventsLog": [] }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_explicit_competency_missing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "mortal_bootstrap_world_event_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MortalBootstrapStateBuilder_DoesNotCreatePassiveSkillTextFromProse()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 5,
            characterDescription: "Лира Мерран, молодая городская следопытка-писарь: умеет читать следы, сверять документы и замечать мелкие улики.",
            worldDescription: "Тёмное городское фэнтези позднего средневековья.",
            startingCircumstances: "Лира просыпается в комнате над архивом Медных Линий.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-09T01:00:00Z"));

        Assert.Empty(files["game_state/player/skills_passive.json"]["passiveSkillChanges"]!.AsArray());
    }

    [Fact]
    public async Task ValidateGameStateAsync_FreshMortalBootstrapEmptyClientSkillStateIsCanonical()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 5,
            characterDescription: "Молодая женщина-следопыт из обедневшего дворянского рода: умеет читать следы и скрывать страх за вежливостью.",
            worldDescription: "Тёмное фэнтези позднего средневековья.",
            startingCircumstances: "Асурэн приходит в себя в дорожной харчевне у северных ворот.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-05T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.DoesNotContain(issues, issue =>
            issue.FilePath is not null &&
            issue.FilePath.Contains("skills_", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "passive_skill_missing_structured_bonuses", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "passive_skill_missing_player_stat_bonus_mirror", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawMortalLocation_OutdoorLocationMissingBiome_ReportsCurrentContractIssue()
    {
        var currentLocation = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        currentLocation.Remove("biome");
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = currentLocation }.ToJsonString());

        var issues = await _validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        var biomeIssue = Assert.Single(issues, issue =>
            string.Equals(
                issue.Code,
                "mortal_location_materialization_physical_shape_invalid",
                StringComparison.Ordinal));
        Assert.Equal("mortal_location_materialization", biomeIssue.Section);
        Assert.Equal(
            "game_state/world/current_location.json.currentLocationData.biome",
            biomeIssue.FilePath);
        Assert.NotNull(biomeIssue.MortalLocationRepairContext);
    }

    [Fact]
    public async Task ValidateGameStateAsync_CanonicalFactionWithOnlyTemporaryIdentity_ReportsBlockingIssue()
    {
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", new JsonObject
        {
            ["factions"] = new JsonArray
            {
                new JsonObject
                {
                    ["factionId"] = null,
                    ["initialId"] = "temp-faction-merchant-guild",
                    ["isNewFaction"] = true,
                    ["name"] = "Купеческая гильдия порта",
                    ["description"] = "Гильдия долговых книг и архивных печатей.",
                    ["image_prompt"] = "merchant guild archive seal, gothic port fantasy",
                    ["level"] = 1,
                    ["experience"] = 0,
                    ["experienceForNextLevel"] = 100,
                    ["developmentArchetype"] = "mercantile_archive_guild",
                    ["isPlayerFaction"] = false,
                    ["isPlayerMember"] = false,
                    ["reputation"] = 0,
                    ["powerProfile"] = new JsonObject
                    {
                        ["military"] = 1,
                        ["economic"] = 10,
                        ["social"] = 4,
                        ["covert"] = 2,
                        ["logistics"] = 5,
                        ["stability"] = 7,
                        ["arcane_tech"] = 1,
                        ["exploration"] = 1
                    },
                    ["resources"] = new JsonObject
                    {
                        ["wealth"] = 5,
                        ["metaResources"] = new JsonArray(),
                        ["strategicGoods"] = new JsonArray()
                    },
                    ["ranks"] = new JsonObject
                    {
                        ["branches"] = new JsonArray()
                    }
                }
            }
        }.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.Code, "canonical_faction_core_requires_permanent_faction_id", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertPlayerFacingBootstrapTextIsClean(JsonNode? node, string context)
    {
        var forbiddenTerms = new[] { "bootstrap", "baseline", "client", "клиент", "scaffold" };
        foreach (var text in EnumerateJsonStrings(node))
        {
            foreach (var term in forbiddenTerms)
            {
                Assert.DoesNotContain(term, text, StringComparison.OrdinalIgnoreCase);
            }
        }

        static IEnumerable<string> EnumerateJsonStrings(JsonNode? current)
        {
            switch (current)
            {
                case JsonValue value when value.TryGetValue<string>(out var text):
                    yield return text;
                    break;
                case JsonArray array:
                    foreach (var item in array)
                    foreach (var text in EnumerateJsonStrings(item))
                        yield return text;
                    break;
                case JsonObject obj:
                    foreach (var property in obj)
                    foreach (var text in EnumerateJsonStrings(property.Value))
                        yield return text;
                    break;
            }
        }
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentCodexStoredEntriesCanReferenceOtherCurrentEntries()
    {
        var preTurnCodex = """
        {
          "entries": [
            {
              "entryId": "codex_life_001_world",
              "title": "Текущий смертный мир",
              "category": "geography",
              "content": "Базовая запись текущего мира.",
              "summary": "Базовая запись текущего мира.",
              "sourceFile": "current_world/world_setting.json",
              "relatedEntries": []
            }
          ]
        }
        """;

        var currentCodex = """
        {
          "entries": [
            {
              "entryId": "codex_life_001_world",
              "title": "Текущий смертный мир",
              "category": "geography",
              "content": "Базовая запись текущего мира.",
              "summary": "Базовая запись текущего мира.",
              "sourceFile": "current_world/world_setting.json",
              "relatedEntries": []
            },
            {
              "entryId": "codex_life_001_ethernia",
              "title": "Этерния",
              "category": "geography",
              "content": "Город-государство дворянских домов и магических гильдий.",
              "summary": "Город-государство дворянских домов и магических гильдий.",
              "sourceFile": "current_world/geography.json",
              "relatedEntries": [ "codex_life_001_world" ]
            },
            {
              "entryId": "codex_life_001_house_valmont",
              "title": "Дом Вальмонт",
              "category": "factions",
              "content": "Дворянский дом, связанный с архивами и руническими тайнами.",
              "summary": "Дворянский дом, связанный с архивами и руническими тайнами.",
              "sourceFile": "current_world/cultures.json",
              "relatedEntries": [ "codex_life_001_ethernia" ]
            }
          ]
        }
        """;

        await WriteValidatedSnapshotManifestAsync(("lore/codex_entries.json", preTurnCodex));
        await _fs.WriteFileAtomicAsync("lore/codex_entries.json", currentCodex);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "codex_related_entry_unknown_target", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "codex_life_001_ethernia", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ClientOwnedMortalBootstrapBaselineLoreDoesNotRequireGmRewrite()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Лира Вальмонт, молодая дворянка-маг.",
            worldDescription: "Поместье Вальмонт в столице Этернии.",
            startingCircumstances: "Лира просыпается в своих покоях после тревожных снов.",
            createdAtUtc: DateTimeOffset.Parse("2026-06-29T07:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        var bootstrapLoreFiles = new[]
        {
            "lore/current_world/world_setting.json",
            "lore/current_world/geography.json",
            "lore/current_world/history.json",
            "lore/current_world/cultures.json",
            "lore/current_world/threats.json",
            "lore/codex_entries.json"
        };

        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "GM-инициированного воплощения",
            includeSnapshotFilesAsRollbackBaseline: false,
            bootstrapLoreFiles.Select(path => (Path: path, Json: files[path].ToJsonString())).ToArray());
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "sessionId": "session_mortal_bootstrap_validation_tests",
          "requestId": "request_mortal_bootstrap_validation_tests",
          "turnNumber": 3
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_reused_previous_world_lore", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NeutralBootstrapInventsNoLocationPlaceholdersButStillRejectsAuthoredFactionPlaceholder()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Эйра, молодая городская писарка при архиве купеческой гильдии.",
            worldDescription: "Портовый город-государство с купеческими гильдиями, архивами и тайными культами.",
            startingCircumstances: "Эйра просыпается до рассвета в комнате при архиве; на столе лежит чужая опечатанная расписка.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-09T01:00:00Z"));
        files["game_state/factions/faction_core.json"]["factions"] = new JsonArray(
            new JsonObject
            {
                ["factionId"] = "faction_life_001_initial_context",
                ["name"] = "Силы стартовой сцены",
                ["displayName"] = "Силы стартовой сцены"
            });

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "playerAuthoredStart": {
            "characterDescription": "Эйра, молодая городская писарка при архиве купеческой гильдии.",
            "worldDescription": "Портовый город-государство с купеческими гильдиями, архивами и тайными культами.",
            "startingCircumstances": "Эйра просыпается до рассвета в комнате при архиве; на столе лежит чужая опечатанная расписка."
          }
        }
        """);

        var bootstrapLoreFiles = new[]
        {
            "lore/current_world/world_setting.json",
            "lore/current_world/geography.json",
            "lore/current_world/history.json",
            "lore/current_world/cultures.json",
            "lore/current_world/threats.json",
            "lore/codex_entries.json"
        };

        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "GM-инициированного воплощения",
            includeSnapshotFilesAsRollbackBaseline: false,
            bootstrapLoreFiles.Select(path => (Path: path, Json: files[path].ToJsonString())).ToArray());
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "sessionId": "session_mortal_bootstrap_validation_tests",
          "requestId": "request_mortal_bootstrap_validation_tests",
          "turnNumber": 3
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_placeholder_player_visible_name", StringComparison.OrdinalIgnoreCase) &&
            (issue.FilePath.Contains("current_location.json", StringComparison.OrdinalIgnoreCase) ||
             issue.FilePath.Contains("world_map.json", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.Code, "mortal_bootstrap_placeholder_player_visible_name", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("game_state/factions/faction_core.json", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "Силы стартовой сцены", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MortalBootstrapPlaceholderValidation_InspectsOnlyIdentityAndTitleFields()
    {
        const string narrativePhrase = "Летописцы называют эпизод стартовой сценой.";
        const string rolePhrase = "Наставник стартовой сцены в театральной постановке";
        using var document = JsonDocument.Parse($$"""
        {
          "name": "Стартовая сцена новой жизни",
          "title": "Путь из стартовой сцены",
          "description": "{{narrativePhrase}}",
          "role": "{{rolePhrase}}"
        }
        """);
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(
            "ValidateMortalBootstrapPlayerVisibleNamesInElement",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(_validator, [document.RootElement, "fixture", null, issues]);

        Assert.Contains(issues, issue => issue.FilePath == "fixture.name");
        Assert.Contains(issues, issue => issue.FilePath == "fixture.title");
        Assert.DoesNotContain(issues, issue => issue.FilePath == "fixture.description");
        Assert.DoesNotContain(issues, issue => issue.FilePath == "fixture.role");
    }

    [Fact]
    public async Task ValidateGameStateAsync_ClientOwnedMortalBootstrapBaselineWithPlaceholderNames_DoesNotReportAcceptedBootstrapPlaceholderIssue()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Эйра, молодая городская писарка при архиве купеческой гильдии.",
            worldDescription: "Портовый город-государство с купеческими гильдиями, архивами и тайными культами.",
            startingCircumstances: "Эйра просыпается до рассвета в комнате при архиве; на столе лежит чужая опечатанная расписка.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-09T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "playerAuthoredStart": {
            "characterDescription": "Эйра, молодая городская писарка при архиве купеческой гильдии.",
            "worldDescription": "Портовый город-государство с купеческими гильдиями, архивами и тайными культами.",
            "startingCircumstances": "Эйра просыпается до рассвета в комнате при архиве; на столе лежит чужая опечатанная расписка."
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_placeholder_player_visible_name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MortalBootstrapRequestedTeacherWithoutTeacherProfile_ReportsTrainingSurfaceIssue()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Асурэн де Вальмонт, молодой аристократ-маг.",
            worldDescription: "Этерния: темное фэнтези с учителями навыков и витринами обучения.",
            startingCircumstances: "За дверью ждёт наставница Селина Орвейн, которая может обучать магической диагностике, быстрым выпадам и этикету через витрину обучения.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        var npcCorePath = _fs.ResolvePath("game_state/npcs/npc_core.json");
        if (File.Exists(npcCorePath))
            File.Delete(npcCorePath);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "playerAuthoredStart": {
            "characterDescription": "Асурэн де Вальмонт, молодой аристократ-маг.",
            "worldDescription": "Этерния: темное фэнтези с учителями навыков и витринами обучения.",
            "startingCircumstances": "За дверью ждёт наставница Селина Орвейн, которая может обучать магической диагностике, быстрым выпадам и этикету через витрину обучения."
          },
          "structuredGmAuthority": {
            "actorCapabilities": [
              { "capability": "canTeach", "required": true }
            ]
          }
        }
        """);

        var bootstrapLoreFiles = new[]
        {
            "lore/current_world/world_setting.json",
            "lore/current_world/geography.json",
            "lore/current_world/history.json",
            "lore/current_world/cultures.json",
            "lore/current_world/threats.json",
            "lore/codex_entries.json"
        };

        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "GM-инициированного воплощения",
            includeSnapshotFilesAsRollbackBaseline: false,
            bootstrapLoreFiles.Select(path => (Path: path, Json: files[path].ToJsonString())).ToArray());

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.Code, "mortal_bootstrap_requested_teacher_missing", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MortalBootstrapLearnIntentWithoutStructuredGmAuthority_DoesNotReportTrainingSurfaceIssue()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Кай Рен, молодой подмастерье картографа, хочет научиться защищаться ножом.",
            worldDescription: "Портовый город с картографическими мастерскими, купеческими домами и уличными бандами.",
            startingCircumstances: "Утро в мастерской старого картографа у причала Соляных Верфей. Мастер Орт велит Каю сверить печати.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-09T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        var npcCorePath = _fs.ResolvePath("game_state/npcs/npc_core.json");
        if (File.Exists(npcCorePath))
            File.Delete(npcCorePath);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "playerAuthoredStart": {
            "characterDescription": "Кай Рен, молодой подмастерье картографа, хочет научиться защищаться ножом.",
            "worldDescription": "Портовый город с картографическими мастерскими, купеческими домами и уличными бандами.",
            "startingCircumstances": "Утро в мастерской старого картографа у причала Соляных Верфей. Мастер Орт велит Каю сверить печати."
          }
        }
        """);

        var bootstrapLoreFiles = new[]
        {
            "lore/current_world/world_setting.json",
            "lore/current_world/geography.json",
            "lore/current_world/history.json",
            "lore/current_world/cultures.json",
            "lore/current_world/threats.json",
            "lore/codex_entries.json"
        };

        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "GM-инициированного воплощения",
            includeSnapshotFilesAsRollbackBaseline: false,
            bootstrapLoreFiles.Select(path => (Path: path, Json: files[path].ToJsonString())).ToArray());

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_requested_teacher_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MortalBootstrapRequestedTradeWithoutTradeState_ReportsTradeSurfaceIssue()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Ронан Вельт, молодой городской писарь.",
            worldDescription: "Портовый город Астерн с лавками, архивами и купеческими гильдиями.",
            startingCircumstances: "Рядом купец Дорн предлагает купить воск, чернила и простой талисман перед опасным поручением.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-09T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        var npcCorePath = _fs.ResolvePath("game_state/npcs/npc_core.json");
        if (File.Exists(npcCorePath))
            File.Delete(npcCorePath);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "playerAuthoredStart": {
            "characterDescription": "Ронан Вельт, молодой городской писарь.",
            "worldDescription": "Портовый город Астерн с лавками, архивами и купеческими гильдиями.",
            "startingCircumstances": "Рядом купец Дорн предлагает купить воск, чернила и простой талисман перед опасным поручением."
          },
          "structuredGmAuthority": {
            "actorCapabilities": [
              { "capability": "canTrade", "required": true }
            ]
          }
        }
        """);

        var bootstrapLoreFiles = new[]
        {
            "lore/current_world/world_setting.json",
            "lore/current_world/geography.json",
            "lore/current_world/history.json",
            "lore/current_world/cultures.json",
            "lore/current_world/threats.json",
            "lore/codex_entries.json"
        };

        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "GM-инициированного воплощения",
            includeSnapshotFilesAsRollbackBaseline: false,
            bootstrapLoreFiles.Select(path => (Path: path, Json: files[path].ToJsonString())).ToArray());

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.Code, "mortal_bootstrap_requested_trade_missing", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MortalBootstrapPlayerIsMerchantWithoutTradePromise_DoesNotReportTradeSurfaceIssue()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Ронан Вельт, молодой купец-писарь без собственной лавки.",
            worldDescription: "Портовый город Астерн с архивами и купеческими гильдиями.",
            startingCircumstances: "Ронан просыпается до рассвета в каморке при архиве; на столе лежит чужая опечатанная расписка.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-09T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        var npcCorePath = _fs.ResolvePath("game_state/npcs/npc_core.json");
        if (File.Exists(npcCorePath))
            File.Delete(npcCorePath);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "playerAuthoredStart": {
            "characterDescription": "Ронан Вельт, молодой купец-писарь без собственной лавки.",
            "worldDescription": "Портовый город Астерн с архивами и купеческими гильдиями.",
            "startingCircumstances": "Ронан просыпается до рассвета в каморке при архиве; на столе лежит чужая опечатанная расписка."
          }
        }
        """);

        var bootstrapLoreFiles = new[]
        {
            "lore/current_world/world_setting.json",
            "lore/current_world/geography.json",
            "lore/current_world/history.json",
            "lore/current_world/cultures.json",
            "lore/current_world/threats.json",
            "lore/codex_entries.json"
        };

        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "GM-инициированного воплощения",
            includeSnapshotFilesAsRollbackBaseline: false,
            bootstrapLoreFiles.Select(path => (Path: path, Json: files[path].ToJsonString())).ToArray());

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_requested_trade_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NpcSkillStringEntries_ReportShapeIssuesInsteadOfThrowing()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Северная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "NPCId": "npc_life_001_old_miron",
              "name": "Старый Мирон",
              "image_prompt": "old hunter mentor",
              "rarity": "Common",
              "worldview": "Осторожность важнее бравады.",
              "personalityArchetype": "stern_practical_mentor",
              "culturalStance": "Pragmatist",
              "race": "Человек",
              "class": "Охотник",
              "appearanceDescription": "Седой охотник с дорожным ножом.",
              "history": "Много лет водит артель по границе леса.",
              "progressionType": "static_teacher_npc",
              "currentLocationId": "loc_life_001_start",
              "initialLocationId": null,
              "age": 57,
              "level": 2,
              "experience": 0,
              "experienceForNextLevel": 150,
              "relationshipLevel": 0,
              "attitude": "Нейтралитет",
              "playerCompanionDirective": "not_companion",
              "culturalLayer": "приграничная охотничья артель",
              "personalityTraits": [],
              "maxWeight": 35,
              "totalWeight": 0,
              "isOverloaded": false,
              "progressionTrackers": {},
              "plans": "Обучить Лиру осторожности.",
              "personalQuests": [],
              "relationshipLock": {
                "isLocked": false,
                "breakthroughQuestId": null
              },
              "characteristics": {
                "strength": 12,
                "dexterity": 13
              },
              "activeSkills": [
                "Короткий выпад копьём"
              ],
              "passiveSkills": [
                "Чтение следов"
              ],
              "equippedItems": {},
              "fateCards": [],
              "inventory": [],
              "goals": {
                "shortTerm": "Проверить готовность Лиры.",
                "longTerm": "Сделать из Лиры осторожную следопытку."
              }
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MortalBootstrap);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "expected_object", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("npc_core", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("activeSkills[0]", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "expected_object", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("npc_core", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("passiveSkills[0]", StringComparison.OrdinalIgnoreCase) &&
            issue.RepairHint?.Contains("JSON object", StringComparison.OrdinalIgnoreCase) == true);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles) =>
        WriteValidatedSnapshotManifestAsync(
            sourceLabel: "mortal bootstrap validation tests",
            includeSnapshotFilesAsRollbackBaseline: true,
            snapshotFiles);

    private async Task WriteValidatedSnapshotManifestAsync(
        string sourceLabel,
        bool includeSnapshotFilesAsRollbackBaseline,
        params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_mortal_bootstrap_validation_tests";
        const string requestId = "request_mortal_bootstrap_validation_tests";
        const int turnNumber = 3;
        const string playerAction = "Mortal bootstrap validation test.";

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": {{JsonSerializer.Serialize(playerAction)}}
        }
        """);

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();

        foreach (var (path, json) in snapshotFiles)
        {
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await _fs.WriteFileAtomicAsync(snapshotPath, json);
            files[path] = snapshotPath;
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            if (includeSnapshotFilesAsRollbackBaseline)
                rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-06-29T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = sourceLabel,
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }
}
