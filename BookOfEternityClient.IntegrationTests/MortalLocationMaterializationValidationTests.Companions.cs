using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLocationMaterializationValidationTests
{
    [Fact]
    public void Companions_DuplicateExactStorageIdentityFailsClosed()
    {
        var location = CreateRawLocation(
            "locref_duplicate_storage",
            "mlocmat_duplicate_storage",
            x: 80,
            route: "world_map_creation");
        var storage = CreateStorage("storage_duplicate");
        location["locationStorages"] = new JsonArray(
            storage.DeepClone(),
            storage.DeepClone());
        location["materialization"]!["sections"]!["storageMetadata"] =
            PopulatedDisposition();

        var result = Build(rawWorldMapUpdates: Updates(location));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_storage_identity_duplicate" &&
            issue.FilePath.EndsWith(
                ".locationStorages[1].storageId",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Companions_RemoteLocationStorageContentsFailClosed()
    {
        var location = CreateRawLocation(
            "locref_remote_storage_contents",
            "mlocmat_remote_storage_contents",
            x: 81,
            route: "world_map_creation");
        var storage = CreateStorage("storage_remote");
        storage["contents"] = new JsonArray(
            new JsonObject
            {
                ["creationRef"] = "itemref_remote_forbidden"
            });
        location["locationStorages"] = new JsonArray(storage);
        location["materialization"]!["sections"]!["storageMetadata"] =
            PopulatedDisposition();

        var result = Build(rawWorldMapUpdates: Updates(location));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_remote_storage_contents_forbidden" &&
            issue.FilePath.EndsWith(
                ".locationStorages[0].contents",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Companions_CaseVariantStorageIdentityFailsClosed()
    {
        var location = CreateRawLocation(
            "locref_confusable_storage",
            "mlocmat_confusable_storage",
            x: 82,
            route: "world_map_creation");
        location["locationStorages"] = new JsonArray(
            CreateStorage("storage_case_sensitive"),
            CreateStorage("STORAGE_CASE_SENSITIVE"));
        location["materialization"]!["sections"]!["storageMetadata"] =
            PopulatedDisposition();

        var result = Build(rawWorldMapUpdates: Updates(location));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_storage_identity_confusable" &&
            issue.FilePath.EndsWith(
                ".locationStorages[1].storageId",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Companions_LoreNameAndPathAreNotCodexAuthority()
    {
        var location = CreateRawLocation(
            "locref_lore_name_path",
            "mlocmat_lore_name_path",
            x: 83,
            route: "world_map_creation");
        location["loreBindings"] = new JsonArray(new JsonObject
        {
            ["kind"] = "codex",
            ["name"] = "Легенда о Чёрном броде",
            ["filePath"] = "lore/codex_entries.json"
        });
        location["materialization"]!["sections"]!["loreBindings"] =
            PopulatedDisposition();

        var result = Build(rawWorldMapUpdates: Updates(location));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_lore_binding_selector_invalid" &&
            issue.FilePath.EndsWith(".loreBindings[0]", StringComparison.Ordinal));
    }

    [Fact]
    public void Companions_StorageWithoutExactIdentityFailsClosed()
    {
        var location = CreateRawLocation(
            "locref_storage_missing_identity",
            "mlocmat_storage_missing_identity",
            x: 84,
            route: "world_map_creation");
        var storage = CreateStorage("storage_missing_identity");
        storage.Remove("storageId");
        storage["name"] = "Сундук без authority";
        location["locationStorages"] = new JsonArray(storage);
        location["materialization"]!["sections"]!["storageMetadata"] =
            PopulatedDisposition();

        var result = Build(rawWorldMapUpdates: Updates(location));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_storage_identity_invalid" &&
            issue.FilePath.EndsWith(
                ".locationStorages[0].storageId",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Companions_ThreatNameIsNotAuthority()
    {
        var location = CreateRawLocation(
            "locref_threat_name_only",
            "mlocmat_threat_name_only",
            x: 85,
            route: "world_map_creation");
        location["activeThreats"] = new JsonArray(new JsonObject
        {
            ["threatName"] = "Разбойники у брода",
            ["dangerLevel"] = "moderate",
            ["description"] = "Имя угрозы не заменяет её точную identity."
        });
        location["materialization"]!["sections"]!["activeThreats"] =
            PopulatedDisposition();

        var result = Build(rawWorldMapUpdates: Updates(location));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_threat_identity_invalid" &&
            issue.FilePath.EndsWith(".activeThreats[0]", StringComparison.Ordinal));
    }

    [Fact]
    public void Companions_DuplicateExactThreatIdentityFailsClosed()
    {
        var location = CreateRawLocation(
            "locref_duplicate_threat",
            "mlocmat_duplicate_threat",
            x: 851,
            route: "world_map_creation");
        location["activeThreats"] = new JsonArray(
            new JsonObject { ["threatId"] = "threat_ford_bandits" },
            new JsonObject { ["threatId"] = "threat_ford_bandits" });
        location["materialization"]!["sections"]!["activeThreats"] =
            PopulatedDisposition();

        var result = Build(rawWorldMapUpdates: Updates(location));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_threat_identity_duplicate" &&
            issue.FilePath.EndsWith(
                ".activeThreats[1].threatId",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Companions_CaseVariantThreatIdentityFailsClosed()
    {
        var location = CreateRawLocation(
            "locref_confusable_threat",
            "mlocmat_confusable_threat",
            x: 852,
            route: "world_map_creation");
        location["activeThreats"] = new JsonArray(
            new JsonObject { ["threatId"] = "threat_ford_bandits" },
            new JsonObject { ["threatId"] = "THREAT_FORD_BANDITS" });
        location["materialization"]!["sections"]!["activeThreats"] =
            PopulatedDisposition();

        var result = Build(rawWorldMapUpdates: Updates(location));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_threat_identity_confusable" &&
            issue.FilePath.EndsWith(
                ".activeThreats[1].threatId",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Companions_CaseVariantCodexIdentityFailsClosed()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            EmptyWorldMap());
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.WriteJsonAsync(
            "lore/codex_entries.json",
            new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["entryId"] = "codex_black_ford"
                })
            });
        await context.CaptureValidatedPendingSnapshotAsync();

        var location = CreateRawLocation(
            "locref_codex_case_variant",
            "mlocmat_codex_case_variant",
            x: 86,
            route: "world_map_creation");
        location["loreBindings"] = new JsonArray(new JsonObject
        {
            ["kind"] = "codex",
            ["codexEntryId"] = "CODEX_BLACK_FORD"
        });
        location["materialization"]!["sections"]!["loreBindings"] =
            PopulatedDisposition();
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: Updates(location));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_lore_binding_target_confusable" &&
            issue.FilePath.EndsWith(
                ".loreBindings[0].codexEntryId",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Companions_CanonicalDanglingQuestBindingFailsPostValidation()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        location["loreBindings"] = new JsonArray(new JsonObject
        {
            ["kind"] = "quest",
            ["questId"] = "quest_missing_from_canonical_authority"
        });
        location["materialization"]!["sections"]!["loreBindings"] =
            PopulatedDisposition();
        MortalLocationTestFixture.ResealCanonicalLocation(location);
        await context.WritePreTurnCanonicalStateAsync(location);
        await context.WriteJsonAsync(
            MortalLocationCompanionAuthority.RegularQuestsPath,
            new JsonObject { ["quests"] = new JsonArray() });

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_lore_binding_target_unknown" &&
            issue.FilePath.EndsWith(
                ".loreBindings[0].questId",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Companions_ExactCanonicalLoreAuthoritiesAreAccepted()
    {
        await using var context = await CreateEmptyAcceptedTurnContextAsync();
        await context.WriteJsonAsync(
            MortalLocationCompanionAuthority.CodexPath,
            AuthorityRoot("entries", "entryId", "codex_black_ford"));
        await context.WriteJsonAsync(
            MortalLocationCompanionAuthority.RegularQuestsPath,
            AuthorityRoot("quests", "questId", "quest_black_ford"));
        await context.WriteJsonAsync(
            MortalLocationCompanionAuthority.WorldEventsPath,
            AuthorityRoot("worldEventsLog", "eventId", "event_black_ford"));
        await context.CaptureValidatedPendingSnapshotAsync();
        var location = CreateLoreBoundLocation(
            "locref_exact_lore",
            "mlocmat_exact_lore",
            x: 87,
            new JsonObject
            {
                ["kind"] = "codex",
                ["codexEntryId"] = "codex_black_ford"
            },
            new JsonObject
            {
                ["kind"] = "quest",
                ["questId"] = "quest_black_ford"
            },
            new JsonObject
            {
                ["kind"] = "world_event",
                ["worldEventId"] = "event_black_ford"
            });
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: Updates(location));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith(
                "mortal_location_lore_binding_target_",
                StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Companions_DuplicateWorldEventAuthorityFailsAmbiguous()
    {
        await using var context = await CreateEmptyAcceptedTurnContextAsync();
        await context.WriteJsonAsync(
            MortalLocationCompanionAuthority.WorldEventsPath,
            new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(
                    new JsonObject { ["eventId"] = "event_ambiguous" },
                    new JsonObject { ["eventId"] = "event_ambiguous" })
            });
        await context.CaptureValidatedPendingSnapshotAsync();
        var location = CreateLoreBoundLocation(
            "locref_ambiguous_event",
            "mlocmat_ambiguous_event",
            x: 88,
            new JsonObject
            {
                ["kind"] = "world_event",
                ["worldEventId"] = "event_ambiguous"
            });
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: Updates(location));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_lore_binding_target_ambiguous" &&
            issue.FilePath.EndsWith(
                ".loreBindings[0].worldEventId",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Companions_SoulQuestIsNotMortalQuestAuthority()
    {
        await using var context = await CreateEmptyAcceptedTurnContextAsync();
        await context.WriteJsonAsync(
            "game_state/quests/soul_quests.json",
            AuthorityRoot("quests", "questId", "quest_cross_realm"));
        await context.CaptureValidatedPendingSnapshotAsync();
        var location = CreateLoreBoundLocation(
            "locref_cross_realm_quest",
            "mlocmat_cross_realm_quest",
            x: 89,
            new JsonObject
            {
                ["kind"] = "quest",
                ["questId"] = "quest_cross_realm"
            });
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: Updates(location));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_lore_binding_target_unknown" &&
            issue.FilePath.EndsWith(
                ".loreBindings[0].questId",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Companions_NarrowUpdateUsesExactCanonicalLoreAuthority()
    {
        var existing = MortalLocationTestFixture.CreateCanonicalLocation();
        existing["loreBindings"] = new JsonArray(new JsonObject
        {
            ["kind"] = "codex",
            ["codexEntryId"] = "codex_previous_ford"
        });
        existing["materialization"]!["sections"]!["loreBindings"] =
            PopulatedDisposition();
        MortalLocationTestFixture.ResealCanonicalLocation(existing);
        var codex = new JsonObject
        {
            ["entries"] = new JsonArray(
                new JsonObject { ["entryId"] = "codex_previous_ford" },
                new JsonObject { ["entryId"] = "codex_updated_ford" })
        };

        var result = MortalLocationAcceptedTurnPlanner.Build(
            new MortalLocationAcceptedTurnInput(
                MortalLocationTestFixture.CreateWorldMap(existing),
                MortalLocationTestFixture.CreateCurrentProjection(existing),
                MortalLocationTestFixture.CreateIdentityIndex(existing),
                RawCurrentLocationData: null,
                new JsonObject
                {
                    ["worldMapUpdates"] = new JsonObject
                    {
                        ["locationUpdates"] = new JsonArray(new JsonObject
                        {
                            ["locationId"] = MortalLocationTestFixture.LocationId,
                            ["loreBindings"] = new JsonArray(new JsonObject
                            {
                                ["kind"] = "codex",
                                ["codexEntryId"] = "codex_updated_ford"
                            })
                        })
                    }
                },
                Turn: 42,
                CompanionAuthority: MortalLocationCompanionAuthority.FromCanonicalRoots(
                    codex,
                    questRoot: null,
                    worldEventRoot: null)));

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues));
        var canonical = Assert.Single(
            result.Plan!.FinalWorldMap["locations"]!.AsArray().OfType<JsonObject>());
        var binding = Assert.Single(
            canonical["loreBindings"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(
            "codex_updated_ford",
            binding["codexEntryId"]!.GetValue<string>());
    }

    [Fact]
    public void Companions_NarrowUpdateRejectsCaseVariantLoreAuthority()
    {
        var existing = MortalLocationTestFixture.CreateCanonicalLocation();
        var codex = AuthorityRoot("entries", "entryId", "codex_updated_ford");

        var result = MortalLocationAcceptedTurnPlanner.Build(
            new MortalLocationAcceptedTurnInput(
                MortalLocationTestFixture.CreateWorldMap(existing),
                MortalLocationTestFixture.CreateCurrentProjection(existing),
                MortalLocationTestFixture.CreateIdentityIndex(existing),
                RawCurrentLocationData: null,
                new JsonObject
                {
                    ["worldMapUpdates"] = new JsonObject
                    {
                        ["locationUpdates"] = new JsonArray(new JsonObject
                        {
                            ["locationId"] = MortalLocationTestFixture.LocationId,
                            ["loreBindings"] = new JsonArray(new JsonObject
                            {
                                ["kind"] = "codex",
                                ["codexEntryId"] = "CODEX_UPDATED_FORD"
                            })
                        })
                    }
                },
                Turn: 42,
                CompanionAuthority: MortalLocationCompanionAuthority.FromCanonicalRoots(
                    codex,
                    questRoot: null,
                    worldEventRoot: null)));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_lore_binding_target_confusable" &&
            issue.FilePath.EndsWith(
                ".loreBindings[0].codexEntryId",
                StringComparison.Ordinal));
    }

    private static async Task<MortalLocationMaterializationTestContext>
        CreateEmptyAcceptedTurnContextAsync()
    {
        var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            EmptyWorldMap());
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        return context;
    }

    private static JsonObject CreateLoreBoundLocation(
        string initialId,
        string materializationId,
        int x,
        params JsonObject[] bindings)
    {
        var location = CreateRawLocation(
            initialId,
            materializationId,
            x,
            route: "world_map_creation");
        location["loreBindings"] = new JsonArray(
            bindings.Select(static binding => (JsonNode?)binding).ToArray());
        location["materialization"]!["sections"]!["loreBindings"] =
            PopulatedDisposition();
        return location;
    }

    private static JsonObject AuthorityRoot(
        string collectionName,
        string identityField,
        string identity) =>
        new()
        {
            [collectionName] = new JsonArray(new JsonObject
            {
                [identityField] = identity
            })
        };

    private static JsonObject CreateStorage(string storageId) =>
        MortalLocationTestFixture.CreateStorageMetadata(
            storageId,
            "Дорожный сундук",
            hasFullAccess: true);

    private static JsonObject PopulatedDisposition() =>
        new()
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
}
