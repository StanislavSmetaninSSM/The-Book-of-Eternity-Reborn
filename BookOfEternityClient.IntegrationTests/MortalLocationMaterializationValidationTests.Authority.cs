using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLocationMaterializationValidationTests
{
    [Fact]
    public async Task CanonicalAuthorityReaders_IgnoreCurrentProjectionAliasesAndUseOrdinalIds()
    {
        const string nestedProjectionAlias = "loc_current_projection_semantic_alias";
        await using var context = await CreateCanonicalCrossReferenceContextAsync();
        var current = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath))!.AsObject();
        current["customStates"] = new JsonArray(new JsonObject
        {
            ["kind"] = "narrative_reference",
            ["locationId"] = nestedProjectionAlias,
            ["description"] = "Эта ссылка не является canonical authority."
        });
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            current);

        var npcAuthority = await NpcCoreChangesContract.ReadAuthorityAsync(
            context.FileSystem);
        var factionAuthority = await context.Validator
            .ReadMortalLocationAuthorityAsync();

        Assert.Contains(
            MortalLocationTestFixture.LocationId,
            npcAuthority.KnownPermanentLocationIds);
        Assert.Contains(
            MortalLocationTestFixture.LocationId,
            factionAuthority.LocationIds);
        foreach (var rejected in new[]
                 {
                     MortalLocationTestFixture.LocationId.ToUpperInvariant(),
                     nestedProjectionAlias
                 })
        {
            Assert.DoesNotContain(rejected, npcAuthority.KnownPermanentLocationIds);
            Assert.DoesNotContain(rejected, factionAuthority.LocationIds);
        }
    }

    [Fact]
    public async Task CrossReferences_CaseVariantStorageIdDoesNotResolveCanonicalLocationStorage()
    {
        await using var context = await CreateCanonicalCrossReferenceContextAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["worldMapUpdates"] = new JsonObject
                {
                    ["storageUpdates"] = new JsonArray(new JsonObject
                    {
                        ["targetLocationId"] = MortalLocationTestFixture.LocationId,
                        ["storageId"] = "STORAGE_EXACT_FORD"
                    })
                }
            });

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.CrossReferences);

        Assert.Contains(issues, issue =>
            issue.Code == "world_map_storage_update_unknown_storage" &&
            issue.FilePath.EndsWith(
                ".storageUpdates[0].storageId",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task CrossReferences_CaseVariantThreatIdDoesNotResolveCanonicalLocationThreat()
    {
        await using var context = await CreateCanonicalCrossReferenceContextAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["worldMapUpdates"] = new JsonObject
                {
                    ["threatsToRemove"] = new JsonArray(new JsonObject
                    {
                        ["targetLocationId"] = MortalLocationTestFixture.LocationId,
                        ["threatId"] = "THREAT_EXACT_FORD"
                    })
                }
            });

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.CrossReferences);

        Assert.Contains(issues, issue =>
            issue.Code == "world_map_threat_remove_unknown_existing_threat" &&
            issue.FilePath.EndsWith(
                ".threatsToRemove[0].threatId",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task CanonicalProjection_CurrentStorageMetadataMustMatchMapWhileContentsRemainCurrentOwned()
    {
        await using var context = await CreateCanonicalCrossReferenceContextAsync();
        var current = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath))!.AsObject();
        var currentStorage = current["locationStorages"]!
            .AsArray()
            .Single()!
            .AsObject();
        currentStorage["contents"] = new JsonArray();
        currentStorage["name"] = "Подменённое имя хранилища";
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            current);

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_materialization_current_projection_mismatch" &&
            issue.FilePath ==
            $"{MortalLocationMaterializationContract.CurrentLocationPath}.locationStorages");
    }

    [Fact]
    public async Task RawAuthority_CurrentStorageContentsRemainOwnedByItemTransitions()
    {
        await using var context = await CreateCanonicalCrossReferenceContextAsync();
        var current = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath))!.AsObject();
        current["locationStorages"]![0]!["contents"] = new JsonArray(
            new JsonObject
            {
                ["creationRef"] = "raw-item-current-storage-authority"
            });
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            current);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "mortal_location_canonical_state_client_owned_mutation" &&
            issue.FilePath == MortalLocationMaterializationContract.CurrentLocationPath);
    }

    [Theory]
    [InlineData("description")]
    [InlineData("storage")]
    [InlineData("threat")]
    public async Task RawAuthority_DirectCanonicalSemanticMutationFailsClosed(
        string mutation)
    {
        await using var context = await CreateCanonicalCrossReferenceContextAsync();
        var map = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        var current = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath))!.AsObject();
        var mapLocation = map["locations"]![0]!.AsObject();

        switch (mutation)
        {
            case "description":
                mapLocation["description"] = "Прямая подмена описания карты.";
                current["description"] = "Прямая подмена описания карты.";
                break;
            case "storage":
                mapLocation["locationStorages"]![0]!["name"] = "Подменённое хранилище";
                current["locationStorages"]![0]!["name"] = "Подменённое хранилище";
                break;
            case "threat":
                mapLocation["activeThreats"]![0]!["intensity"] = 4;
                current["activeThreats"]![0]!["intensity"] = 4;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        await context.WriteJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath, current);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_canonical_state_client_owned_mutation" &&
            issue.FilePath == MortalLocationMaterializationContract.WorldMapPath);
        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_canonical_state_client_owned_mutation" &&
            issue.FilePath == MortalLocationMaterializationContract.CurrentLocationPath);
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(issues));
    }

    [Fact]
    public async Task RawAuthority_DirectCanonicalMutationBesideCommandFailsClosed()
    {
        await using var context = await CreateCanonicalCrossReferenceContextAsync();
        var map = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        map["locations"]![0]!["description"] = "Подмена рядом с узкой командой.";
        map["worldMapUpdates"] = new JsonObject
        {
            ["locationUpdates"] = new JsonArray(new JsonObject
            {
                ["locationId"] = MortalLocationTestFixture.LocationId,
                ["purpose"] = "Разрешённая узкая правка назначения."
            })
        };
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_canonical_state_client_owned_mutation" &&
            issue.FilePath == MortalLocationMaterializationContract.WorldMapPath);
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(issues));
    }

    [Fact]
    public async Task RawAuthority_DirectValidShapedIdentityTransitionMutationFailsClosed()
    {
        await using var context = await CreateCanonicalCrossReferenceContextAsync();
        var index = (await context.ReadJsonAsync(MortalLocationIdentityState.StatePath))!
            .AsObject();
        index["locationEntries"]![0]!["transitions"]!.AsArray().Add(new JsonObject
        {
            ["transitionId"] = "mltrn_forged_raw_authority",
            ["kind"] = "semantic_update",
            ["turn"] = 42
        });
        await context.WriteJsonAsync(MortalLocationIdentityState.StatePath, index);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_identity_index_client_owned_mutation" &&
            issue.FilePath == MortalLocationIdentityState.StatePath);
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(issues));
    }

    private static async Task<MortalLocationMaterializationTestContext>
        CreateCanonicalCrossReferenceContextAsync()
    {
        var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        location["locationStorages"] = new JsonArray(new JsonObject
        {
            ["storageId"] = "storage_exact_ford",
            ["name"] = "Сундук у брода",
            ["description"] = "Точное canonical хранилище.",
            ["image_prompt"] = "A sturdy chest beside a dark river ford, no text",
            ["capacity"] = 8,
            ["volume"] = 40.0,
            ["owner"] = null,
            ["authorizedUsers"] = new JsonArray(),
            ["hasFullAccess"] = true
        });
        location["activeThreats"] = new JsonArray(new JsonObject
        {
            ["threatId"] = "threat_exact_ford",
            ["name"] = "Угроза у брода",
            ["description"] = "Точная canonical угроза для проверки ссылочной authority.",
            ["intensity"] = 3,
            ["longTermGoal"] = "Удерживать переправу под наблюдением.",
            ["currentActivity"] = null,
            ["threatArchetype"] = new JsonObject
            {
                ["motivation"] = "Preservation",
                ["method"] = "Covert",
                ["customMotivation"] = null,
                ["customMethod"] = null
            },
            ["impactProfile"] = new JsonObject
            {
                ["primaryTargetType"] = "Location",
                ["primaryTargetId"] = null,
                ["primaryTargetName"] = "Чёрный брод",
                ["primaryImpact"] = "Stability",
                ["baseImpactValue"] = 2
            }
        });
        location["materialization"]!["sections"]!["storageMetadata"] =
            PopulatedDisposition();
        location["materialization"]!["sections"]!["activeThreats"] =
            PopulatedDisposition();
        MortalLocationTestFixture.ResealCanonicalLocation(location);
        using (var locationDocument = JsonDocument.Parse(location.ToJsonString()))
        {
            Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLocation(
                locationDocument.RootElement,
                "authority test canonical location"));
        }
        await context.WritePreTurnCanonicalStateAsync(location);
        await context.CaptureValidatedPendingSnapshotAsync();
        return context;
    }
}
