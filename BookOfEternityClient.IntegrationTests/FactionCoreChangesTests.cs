using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FactionCoreChangesTests : IDisposable
{
    private const string CorePath =
        "game_state/factions/faction_core.json";
    private const string StructurePath =
        "game_state/factions/faction_structure.json";
    private const string ResourcesPath =
        "game_state/factions/faction_resources.json";
    private const string ProjectsPath =
        "game_state/factions/faction_projects.json";
    private const string CustomPath =
        "game_state/factions/faction_custom.json";
    private const string ChroniclesPath =
        "game_state/factions/faction_chronicles.json";
    private const string NpcCorePath =
        "game_state/npcs/npc_core.json";

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public FactionCoreChangesTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-faction-core-changes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(
            _fs,
            NullLogger<ValidationService>.Instance);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("purposeAndPrinciples")]
    [InlineData("progressionAndPower")]
    [InlineData("governanceAndLeadership")]
    [InlineData("playerMembership")]
    [InlineData("relations")]
    public async Task Validate_EachCompleteGroup_IsAccepted(string groupName)
    {
        await WriteExistingFixtureAsync(current =>
            current[FactionCoreChangesContract.PropertyName] =
                new JsonArray(BuildCommand(
                    "faction_watch",
                    groupName,
                    BuildCompleteGroup(groupName))));

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith(
                "faction_core_changes_",
                StringComparison.Ordinal) == true ||
            issue.Code == "faction_existing_full_resend_forbidden");
    }

    [Fact]
    public async Task Normalize_CompleteCoreGroupsApplyConsumeAndPreserveReceipt()
    {
        var fixture = await WriteExistingFixtureAsync(current =>
        {
            var command = new JsonObject
            {
                ["factionId"] = "faction_watch",
                ["reason"] = "The bridge charter replaced the complete core.",
                ["profile"] = BuildCompleteGroup("profile"),
                ["purposeAndPrinciples"] =
                    BuildCompleteGroup("purposeAndPrinciples"),
                ["progressionAndPower"] =
                    BuildCompleteGroup("progressionAndPower"),
                ["playerMembership"] =
                    BuildCompleteGroup("playerMembership"),
                ["relations"] = BuildCompleteGroup("relations")
            };
            current[FactionCoreChangesContract.PropertyName] =
                new JsonArray(command);
        });
        var receiptBefore =
            FindFaction(fixture.PreTurnCore, "faction_watch")
                ["materialization"]!
                .ToJsonString();

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();
        Assert.Empty(issues);
        await CreateNormalizer().NormalizeAccumulatedStateAsync(
            fixture.Backups);

        var core = await ReadObjectAsync(CorePath);
        Assert.False(core.ContainsKey(
            FactionCoreChangesContract.PropertyName));
        var watch = FindFaction(core, "faction_watch");
        Assert.Equal("Bridge Watch", watch["name"]!.GetValue<string>());
        Assert.Equal(
            "Guard both banks of the river crossing.",
            watch["purpose"]!.GetValue<string>());
        Assert.Equal(4, watch["level"]!.GetValue<int>());
        Assert.True(watch["isPlayerMember"]!.GetValue<bool>());
        Assert.Single(watch["relations"]!.AsArray());
        Assert.Equal(
            "The first watch captain kept the bridge ledger.",
            watch["memory"]!["summary"]!.GetValue<string>());
        Assert.Equal(
            receiptBefore,
            watch["materialization"]!.ToJsonString());
    }

    [Fact]
    public async Task Normalize_GovernanceAndLeadership_ExtractsToStructure()
    {
        var fixture = await WriteExistingFixtureAsync(current =>
            current[FactionCoreChangesContract.PropertyName] =
                new JsonArray(BuildCommand(
                    "faction_watch",
                    "governanceAndLeadership",
                    BuildCompleteGroup("governanceAndLeadership"))));
        var receiptBefore =
            FindFaction(fixture.PreTurnCore, "faction_watch")
                ["materialization"]!
                .ToJsonString();

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();
        Assert.Empty(issues);
        await CreateNormalizer().NormalizeAccumulatedStateAsync(
            fixture.Backups);

        var structure = await ReadObjectAsync(StructurePath);
        var entry = FindEntry(structure, "entries", "faction_watch");
        Assert.Equal(
            "Elected bridge council",
            entry["governance"]!["model"]!.GetValue<string>());
        Assert.Equal(
            "The watch captain chairs the council.",
            entry["leadership"]!["summary"]!.GetValue<string>());
        Assert.Equal(
            "npc_watch_captain",
            entry["leadership"]!["leaderNpcIds"]![0]!.GetValue<string>());

        var core = await ReadObjectAsync(CorePath);
        var watch = FindFaction(core, "faction_watch");
        Assert.False(watch.ContainsKey("governance"));
        Assert.False(watch.ContainsKey("leadership"));
        Assert.Equal(
            receiptBefore,
            watch["materialization"]!.ToJsonString());
        Assert.False(core.ContainsKey(
            FactionCoreChangesContract.PropertyName));
    }

    [Fact]
    public async Task Normalize_InitiallyEmptyRelationsBecomePopulatedWithoutReceiptRewrite()
    {
        var fixture = await WriteExistingFixtureAsync(current =>
            current[FactionCoreChangesContract.PropertyName] =
                new JsonArray(BuildCommand(
                    "faction_watch",
                    "relations",
                    BuildCompleteGroup("relations"))));
        var receiptBefore =
            FindFaction(fixture.PreTurnCore, "faction_watch")
                ["materialization"]!
                .ToJsonString();

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();
        Assert.Empty(issues);
        await CreateNormalizer().NormalizeAccumulatedStateAsync(
            fixture.Backups);

        var core = await ReadObjectAsync(CorePath);
        var watch = FindFaction(core, "faction_watch");
        var relation = Assert.Single(
            watch["relations"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(
            "faction_bridge_compact",
            relation["targetFactionId"]!.GetValue<string>());
        Assert.Equal(
            receiptBefore,
            watch["materialization"]!.ToJsonString());
        Assert.Equal(
            "empty_by_design",
            watch["materialization"]!["sections"]!["relations"]!["state"]!
                .GetValue<string>());
    }

    [Theory]
    [InlineData("partial", "faction_core_changes_profile_invalid")]
    [InlineData("unknown", "faction_core_changes_unknown_member")]
    [InlineData("protected", "faction_core_changes_protected_member")]
    [InlineData("duplicate", "faction_core_changes_duplicate_target")]
    public async Task Validate_InvalidCommandForms_ReportStableIssues(
        string variation,
        string expectedCode)
    {
        await WriteExistingFixtureAsync(current =>
        {
            var command = BuildCommand(
                "faction_watch",
                "profile",
                BuildCompleteGroup("profile"));
            switch (variation)
            {
                case "partial":
                    command["profile"]!.AsObject().Remove("description");
                    break;
                case "unknown":
                    command["profile"]!["futureProfileMember"] = true;
                    break;
                case "protected":
                    command["resources"] = new JsonObject();
                    break;
            }

            current[FactionCoreChangesContract.PropertyName] =
                variation == "duplicate"
                    ? new JsonArray(command, command.DeepClone())
                    : new JsonArray(command);
        });

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode &&
            issue.Actor == "mortal_faction:faction_watch");
    }

    [Fact]
    public async Task Normalize_InvalidCommand_RemainsVisibleAndUnapplied()
    {
        var fixture = await WriteExistingFixtureAsync(current =>
        {
            var command = BuildCommand(
                "faction_watch",
                "profile",
                BuildCompleteGroup("profile"));
            command["materialization"] = new JsonObject
            {
                ["materializationId"] = "forbidden_rewrite"
            };
            current[FactionCoreChangesContract.PropertyName] =
                new JsonArray(command);
        });
        var nameBefore =
            FindFaction(fixture.PreTurnCore, "faction_watch")
                ["name"]!
                .GetValue<string>();

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();
        Assert.Contains(issues, issue =>
            issue.Code == "faction_core_changes_protected_member");
        await CreateNormalizer().NormalizeAccumulatedStateAsync(
            fixture.Backups);

        var core = await ReadObjectAsync(CorePath);
        Assert.True(core.ContainsKey(
            FactionCoreChangesContract.PropertyName));
        Assert.Equal(
            nameBefore,
            FindFaction(core, "faction_watch")["name"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("unknown", "faction_core_changes_target_not_existing")]
    [InlineData("legacy", "faction_core_changes_target_not_materialized")]
    public async Task Validate_UnknownOrUnpromotedLegacyTarget_IsRejected(
        string variation,
        string expectedCode)
    {
        await WriteExistingFixtureAsync(
            current =>
            {
                var factionId = variation == "unknown"
                    ? "faction_unknown"
                    : "faction_watch";
                current[FactionCoreChangesContract.PropertyName] =
                    new JsonArray(BuildCommand(
                        factionId,
                        "profile",
                        BuildCompleteGroup("profile")));
                if (variation == "legacy")
                {
                    FindFaction(current, "faction_watch")
                        .Remove("materialization");
                }
            },
            preTurn =>
            {
                if (variation == "legacy")
                {
                    FindFaction(preTurn, "faction_watch")
                        .Remove("materialization");
                }
            });

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode);
    }

    [Fact]
    public async Task Validate_DuplicateNestedJsonMember_FailsClosed()
    {
        await WriteExistingFixtureAsync(current =>
            current[FactionCoreChangesContract.PropertyName] =
                new JsonArray(BuildCommand(
                    "faction_watch",
                    "profile",
                    BuildCompleteGroup("profile"))));
        var currentJson = (await _fs.ReadFileAsync(CorePath))!;
        currentJson = currentJson.Replace(
            "\"factionColor\":\"#315A88\"",
            "\"factionColor\":\"#315A88\",\"factionColor\":\"#FFFFFF\"",
            StringComparison.Ordinal);
        await _fs.WriteFileAtomicAsync(CorePath, currentJson);

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_core_changes_duplicate_property" &&
            issue.FilePath.EndsWith(".factionColor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Validate_ExistingFullResend_IsForbiddenEvenWhenIdentical()
    {
        await WriteExistingFixtureAsync(current =>
        {
            var watch = FindFaction(current, "faction_watch").DeepClone();
            current.Remove("factions");
            current["factionDataChanges"] = new JsonArray(watch);
        });

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_existing_full_resend_forbidden" &&
            issue.Actor == "mortal_faction:faction_watch");
    }

    [Fact]
    public async Task Normalize_SameTurnPromotionOverlayReviewRegression_AppliesToPromotedEffectiveRowAndPreservesHistory()
    {
        var fixture = await WritePromotionFixtureAsync();

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();
        Assert.DoesNotContain(issues, issue =>
            issue.Code is
                "faction_core_changes_duplicate_effective_identity" or
                "faction_core_changes_ambiguous_target");
        Assert.Empty(issues);
        await CreateNormalizer().NormalizeAccumulatedStateAsync(
            fixture.Backups);

        var core = await ReadObjectAsync(CorePath);
        Assert.False(core.ContainsKey(
            FactionCoreChangesContract.PropertyName));
        var watch = FindFaction(core, "faction_watch");
        Assert.Equal("Promoted Bridge Watch", watch["name"]!.GetValue<string>());
        Assert.Equal(
            "fmat_promoted_watch",
            watch["materialization"]!["materializationId"]!.GetValue<string>());

        var chronicles = await ReadObjectAsync(ChroniclesPath);
        var entries = chronicles["entries"]!
            .AsArray()
            .OfType<JsonObject>()
            .Where(entry =>
                entry["factionId"]!.GetValue<string>() == "faction_watch")
            .Select(entry => entry["entry"]!.GetValue<string>())
            .ToArray();
        Assert.Contains(
            "#7 - The old watch kept the western bridge.",
            entries);
        Assert.Contains(
            "#12 - The promoted watch ratified its complete charter.",
            entries);
    }

    [Fact]
    public async Task Validate_SameTurnPromotionWithValidatedCoreChange_AllowsCommandedProfileAndPreservesUnrelatedPurpose()
    {
        var fixture = await WritePromotionFixtureAsync();

        var commandIssues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();
        var materializationIssues = await _validator
            .ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Empty(commandIssues);
        Assert.DoesNotContain(materializationIssues, issue =>
            issue.Code ==
                "faction_materialization_promotion_history_changed");

        await CreateNormalizer().NormalizeAccumulatedStateAsync(
            fixture.Backups);
        var core = await ReadObjectAsync(CorePath);
        var watch = FindFaction(core, "faction_watch");
        Assert.Equal(
            "Promoted Bridge Watch",
            watch["name"]!.GetValue<string>());
        Assert.Equal(
            "Keep the old road open.",
            watch["purpose"]!.GetValue<string>());
    }

    [Fact]
    public async Task Validate_SameTurnPromotionWithInvalidCoreChange_DoesNotAuthorizeHistoricalDelta()
    {
        await WritePromotionFixtureAsync(current =>
        {
            current["factionDataChanges"]![0]!["name"] =
                "Promoted Bridge Watch";
            current[FactionCoreChangesContract.PropertyName]![0]![
                "reason"] = " ";
        });

        var commandIssues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();
        var materializationIssues = await _validator
            .ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(commandIssues, issue =>
            issue.Code == "faction_core_changes_reason_required");
        Assert.Contains(materializationIssues, issue =>
            issue.Code ==
                "faction_materialization_promotion_history_changed" &&
            issue.Actor == "mortal_faction:faction_watch");
    }

    [Theory]
    [InlineData("duplicate_factions")]
    [InlineData("duplicate_carrier")]
    [InlineData("mutated_baseline")]
    public async Task Validate_InvalidPromotionOverlayReviewRegression_FailsClosed(
        string variation)
    {
        await WritePromotionFixtureAsync(current =>
        {
            switch (variation)
            {
                case "duplicate_factions":
                    current["factions"]!.AsArray().Add(
                        current["factions"]![0]!.DeepClone());
                    break;
                case "duplicate_carrier":
                    current["factionDataChanges"]!.AsArray().Add(
                        current["factionDataChanges"]![0]!.DeepClone());
                    break;
                case "mutated_baseline":
                    current["factions"]![0]!["name"] =
                        "Mutated legacy baseline";
                    break;
            }
        });

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();

        Assert.Contains(issues, issue =>
            issue.Code ==
            "faction_core_changes_duplicate_effective_identity");
    }

    [Fact]
    public async Task Validate_NestedGenericNpcIdAuthorityReviewRegression_IsRejectedByValidatorAndNormalizer()
    {
        const string nestedItemId = "npc_nested_inventory_item";
        var governance =
            BuildCompleteGroup("governanceAndLeadership");
        governance["leadership"]!["leaderNpcIds"] =
            new JsonArray(nestedItemId);
        var fixture = await WriteExistingFixtureAsync(
            current =>
                current[FactionCoreChangesContract.PropertyName] =
                    new JsonArray(BuildCommand(
                        "faction_watch",
                        "governanceAndLeadership",
                        governance)),
            mutateNpcCore: npcCore =>
            {
                var npc = npcCore["NPCsInScene"]![0]!.AsObject();
                npc["inventory"] = new JsonObject
                {
                    ["items"] = new JsonArray(new JsonObject
                    {
                        ["id"] = nestedItemId,
                        ["name"] = "Captain's bridge token"
                    })
                };
            });

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();

        Assert.Contains(issues, issue =>
            issue.Code ==
                "faction_core_changes_governance_and_leadership_invalid" &&
            issue.FilePath.Contains(
                "leaderNpcIds",
                StringComparison.Ordinal));

        await CreateNormalizer().NormalizeAccumulatedStateAsync(
            fixture.Backups);
        var core = await ReadObjectAsync(CorePath);
        Assert.True(core.ContainsKey(
            FactionCoreChangesContract.PropertyName));
        var structure = await ReadObjectAsync(StructurePath);
        var watch = FindEntry(
            structure,
            "entries",
            "faction_watch");
        Assert.Equal(
            "vacant",
            watch["leadership"]!["leadershipState"]!
                .GetValue<string>());
    }

    [Theory]
    [InlineData("NPCId")]
    [InlineData("npcId")]
    [InlineData("id")]
    public async Task Validate_RecognizedNpcCarrierIdentityAliasReviewRegression_IsAccepted(
        string identityAlias)
    {
        await WriteExistingFixtureAsync(
            current =>
                current[FactionCoreChangesContract.PropertyName] =
                    new JsonArray(BuildCommand(
                        "faction_watch",
                        "governanceAndLeadership",
                        BuildCompleteGroup(
                            "governanceAndLeadership"))),
            mutateNpcCore: npcCore =>
            {
                var npc = npcCore["NPCsInScene"]![0]!.AsObject();
                npc.Remove("NPCId");
                npc[identityAlias] = "npc_watch_captain";
            });

        var issues =
            await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code ==
            "faction_core_changes_governance_and_leadership_invalid");
    }

    [Fact]
    public async Task GameResponseDeserializationAndStateDistributor_PersistCommand()
    {
        await WriteExistingFixtureAsync();
        var response = JsonSerializer.Deserialize<GameResponse>(
            """
            {
              "factionCoreChanges": [
                {
                  "factionId": "faction_watch",
                  "reason": "The bridge charter changed the watch profile.",
                  "profile": {
                    "name": "Bridge Watch",
                    "description": "Wardens of both bridge approaches.",
                    "image_prompt": "bridge wardens beneath blue banners",
                    "factionColor": "#315A88"
                  }
                }
              ]
            }
            """,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        Assert.NotNull(response);
        var property = typeof(GameResponse).GetProperty(
            "FactionCoreChanges",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        Assert.NotNull(property.GetValue(response));
        var distributor = new StateDistributor(
            _fs,
            NullLogger<StateDistributor>.Instance);

        var modified = await distributor.DistributeAsync(response);

        Assert.Contains(CorePath, modified);
        var core = await ReadObjectAsync(CorePath);
        var command = Assert.Single(
            core[FactionCoreChangesContract.PropertyName]!
                .AsArray()
                .OfType<JsonObject>());
        Assert.Equal(
            "faction_watch",
            command["factionId"]!.GetValue<string>());
    }

    [Fact]
    public void GameResponseAndFileMapping_ExposeFactionCoreChanges()
    {
        var property = typeof(GameResponse).GetProperty(
            "FactionCoreChanges",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        Assert.Equal(typeof(JsonElement[]), property.PropertyType);
        Assert.True(FileMapping.FieldToFile.TryGetValue(
            "factionCoreChanges",
            out var mappedPath));
        Assert.Equal(CorePath, mappedPath);
    }

    private async Task<FactionFixture> WriteExistingFixtureAsync(
        Action<JsonObject>? mutateCurrent = null,
        Action<JsonObject>? mutatePreTurn = null,
        Action<JsonObject>? mutateNpcCore = null)
    {
        var preTurnCore = BuildMaterializedCore();
        mutatePreTurn?.Invoke(preTurnCore);
        var currentCore = preTurnCore.DeepClone().AsObject();
        mutateCurrent?.Invoke(currentCore);

        var structure = BuildStructureRoot();
        var resources = BuildResourcesRoot();
        var projects = new JsonObject
        {
            ["activeProjects"] = new JsonArray(),
            ["completedProjects"] = new JsonArray()
        };
        var custom = BuildCustomRoot();
        var chronicles = BuildChroniclesRoot();
        var npcCore = BuildNpcCoreRoot();
        mutateNpcCore?.Invoke(npcCore);
        var state = new Dictionary<string, JsonObject>(
            StringComparer.OrdinalIgnoreCase)
        {
            [CorePath] = currentCore,
            [StructurePath] = structure,
            [ResourcesPath] = resources,
            [ProjectsPath] = projects,
            [CustomPath] = custom,
            [ChroniclesPath] = chronicles,
            [NpcCorePath] = npcCore
        };
        foreach (var (path, root) in state)
            await _fs.WriteFileAtomicAsync(path, root.ToJsonString());

        var snapshots = new Dictionary<string, JsonObject>(
            StringComparer.OrdinalIgnoreCase)
        {
            [CorePath] = preTurnCore,
            [StructurePath] = structure.DeepClone().AsObject(),
            [ResourcesPath] = resources.DeepClone().AsObject(),
            [ProjectsPath] = projects.DeepClone().AsObject(),
            [CustomPath] = custom.DeepClone().AsObject(),
            [ChroniclesPath] = chronicles.DeepClone().AsObject(),
            [NpcCorePath] = npcCore.DeepClone().AsObject()
        };
        await WriteValidatedSnapshotManifestAsync(snapshots);
        return new FactionFixture(
            preTurnCore,
            BuildBackupMap(snapshots.Keys));
    }

    private async Task<FactionFixture> WritePromotionFixtureAsync(
        Action<JsonObject>? mutateCurrent = null)
    {
        var preTurnCore = BuildMaterializedCore();
        var legacy = FindFaction(preTurnCore, "faction_watch");
        legacy.Remove("materialization");
        legacy["scribeChronicle"] =
            new JsonArray("#7 - The old watch kept the western bridge.");

        var promoted = BuildMaterializedFaction(
            "faction_watch",
            "Wayfarer Watch",
            "fmat_promoted_watch");
        promoted["materialization"]!["materializedAtTurn"] = 12;
        var historicalStructure = BuildStructureEntry(
            "faction_watch",
            "Wayfarer Watch");
        promoted["governance"] =
            historicalStructure["governance"]!.DeepClone();
        promoted["leadership"] =
            historicalStructure["leadership"]!.DeepClone();
        promoted["ranks"] = new JsonObject
        {
            ["branches"] = new JsonArray()
        };
        promoted["structuredBonuses"] = new JsonArray();
        promoted["resources"] = new JsonObject
        {
            ["metaResources"] = new JsonArray(),
            ["strategicGoods"] = new JsonArray()
        };
        promoted["activeProjects"] = new JsonArray();
        promoted["completedProjects"] = new JsonArray();
        promoted["customStates"] = new JsonArray();
        promoted["scribeChronicle"] = new JsonArray(
            "#12 - The promoted watch ratified its complete charter.");
        var currentCore = new JsonObject
        {
            ["factions"] = preTurnCore["factions"]!.DeepClone(),
            ["factionDataChanges"] = new JsonArray(promoted),
            [FactionCoreChangesContract.PropertyName] =
                new JsonArray(BuildCommand(
                    "faction_watch",
                    "profile",
                    new JsonObject
                    {
                        ["name"] = "Promoted Bridge Watch",
                        ["description"] =
                            "A legacy watch promoted under the complete charter.",
                        ["image_prompt"] =
                            "veteran bridge wardens raising a restored banner",
                        ["factionColor"] = "#315A88"
                    }))
        };
        mutateCurrent?.Invoke(currentCore);

        var structure = BuildStructureRoot();
        var resources = BuildResourcesRoot();
        var projects = new JsonObject
        {
            ["activeProjects"] = new JsonArray(),
            ["completedProjects"] = new JsonArray()
        };
        var custom = BuildCustomRoot();
        var chronicles = new JsonObject
        {
            ["entries"] = new JsonArray()
        };
        var npcCore = BuildNpcCoreRoot();
        var currentState = new Dictionary<string, JsonObject>(
            StringComparer.OrdinalIgnoreCase)
        {
            [CorePath] = currentCore,
            [StructurePath] = structure,
            [ResourcesPath] = resources,
            [ProjectsPath] = projects,
            [CustomPath] = custom,
            [ChroniclesPath] = chronicles,
            [NpcCorePath] = npcCore
        };
        foreach (var (path, root) in currentState)
            await _fs.WriteFileAtomicAsync(path, root.ToJsonString());

        var snapshots = new Dictionary<string, JsonObject>(
            StringComparer.OrdinalIgnoreCase)
        {
            [CorePath] = preTurnCore,
            [StructurePath] = structure.DeepClone().AsObject(),
            [ResourcesPath] = resources.DeepClone().AsObject(),
            [ProjectsPath] = projects.DeepClone().AsObject(),
            [CustomPath] = custom.DeepClone().AsObject(),
            [ChroniclesPath] = chronicles.DeepClone().AsObject(),
            [NpcCorePath] = npcCore.DeepClone().AsObject()
        };
        await WriteValidatedSnapshotManifestAsync(snapshots);
        return new FactionFixture(
            preTurnCore,
            BuildBackupMap(snapshots.Keys));
    }

    private async Task WriteValidatedSnapshotManifestAsync(
        IReadOnlyDictionary<string, JsonObject> snapshotFiles)
    {
        const string sessionId = "session_faction_core_changes";
        const string requestId = "request_faction_core_changes";
        const int turnNumber = 12;
        const string playerAction = "Update one faction core group.";
        await _fs.WriteFileAtomicAsync(
            "input/turn_request.json",
            $$"""
              {
                "sessionId": "{{sessionId}}",
                "requestId": "{{requestId}}",
                "turnNumber": {{turnNumber}},
                "playerAction": "{{playerAction}}"
              }
              """);

        var files = new JsonObject();
        var hashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();
        foreach (var (path, root) in snapshotFiles)
        {
            var json = root.ToJsonString();
            var snapshotPath =
                $"game_state/control/pending_turn_snapshot/{path}";
            await _fs.WriteFileAtomicAsync(snapshotPath, json);
            files[path] = snapshotPath;
            hashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-08-03T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = hashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "accepted faction core changes turn",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(
                manifest);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority
            .SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static Dictionary<string, string> BuildBackupMap(
        IEnumerable<string> paths) =>
        paths.ToDictionary(
            path => path,
            path => $"game_state/control/pending_turn_snapshot/{path}",
            StringComparer.OrdinalIgnoreCase);

    private CanonicalStateNormalizer CreateNormalizer() =>
        new(
            _fs,
            NullLogger<CanonicalStateNormalizer>.Instance);

    private async Task<JsonObject> ReadObjectAsync(string path)
    {
        var json = await _fs.ReadFileAsync(path);
        Assert.False(
            string.IsNullOrWhiteSpace(json),
            $"Expected {path} to contain JSON.");
        return Assert.IsType<JsonObject>(JsonNode.Parse(json!));
    }

    private static JsonObject FindFaction(
        JsonObject root,
        string factionId) =>
        root["factions"]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(faction =>
                faction["factionId"]!.GetValue<string>() == factionId);

    private static JsonObject FindEntry(
        JsonObject root,
        string propertyName,
        string factionId) =>
        root[propertyName]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(entry =>
                entry["factionId"]!.GetValue<string>() == factionId);

    private static JsonObject BuildCommand(
        string factionId,
        string groupName,
        JsonObject group) =>
        new()
        {
            ["factionId"] = factionId,
            ["reason"] = "A complete absolute faction update is required.",
            [groupName] = group
        };

    private static JsonObject BuildCompleteGroup(string groupName) =>
        groupName switch
        {
            "profile" => new JsonObject
            {
                ["name"] = "Bridge Watch",
                ["description"] =
                    "Wardens who protect both bridge approaches.",
                ["image_prompt"] =
                    "weathered bridge wardens beneath blue and brass banners",
                ["factionColor"] = "#315A88"
            },
            "purposeAndPrinciples" => new JsonObject
            {
                ["purpose"] =
                    "Guard both banks of the river crossing.",
                ["currentAgenda"] =
                    "Ratify the bridge compact before winter.",
                ["principles"] = new JsonArray(
                    "No traveler is denied a warning.",
                    "Bridge tolls fund bridge repairs.")
            },
            "progressionAndPower" => new JsonObject
            {
                ["level"] = 4,
                ["experience"] = 120,
                ["experienceForNextLevel"] = 200,
                ["developmentArchetype"] = "Balanced",
                ["customArchetypePriorities"] = null,
                ["powerProfile"] = new JsonObject
                {
                    ["military"] = 2,
                    ["economic"] = 4,
                    ["social"] = 3,
                    ["covert"] = 1,
                    ["logistics"] = 4,
                    ["stability"] = 3,
                    ["arcane_tech"] = 0,
                    ["exploration"] = 2
                }
            },
            "governanceAndLeadership" => new JsonObject
            {
                ["governance"] = new JsonObject
                {
                    ["model"] = "Elected bridge council",
                    ["decisionProcess"] =
                        "Five seats decide by a simple majority."
                },
                ["leadership"] = new JsonObject
                {
                    ["leadershipState"] = "headed",
                    ["summary"] =
                        "The watch captain chairs the council.",
                    ["leaderNpcIds"] =
                        new JsonArray("npc_watch_captain")
                }
            },
            "playerMembership" => new JsonObject
            {
                ["isPlayerFaction"] = false,
                ["isPlayerMember"] = true,
                ["playerRank"] = "Road Warden",
                ["playerBranch"] = "western_road",
                ["playerStrategyDirective"] = null,
                ["reputation"] = 85,
                ["reputationDescription"] = "Trusted road ally"
            },
            "relations" => new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["targetFactionId"] =
                        "faction_bridge_compact",
                    ["status"] = "allied",
                    ["description"] =
                        "Both factions defend the bridge."
                })
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(groupName),
                groupName,
                "Unknown faction core group.")
        };

    private static JsonObject BuildMaterializedCore() =>
        new()
        {
            ["factions"] = new JsonArray(
                BuildMaterializedFaction(
                    "faction_watch",
                    "Wayfarer Watch",
                    "fmat_watch"),
                BuildMaterializedFaction(
                    "faction_bridge_compact",
                    "Bridge Compact",
                    "fmat_bridge_compact"))
        };

    private static JsonObject BuildMaterializedFaction(
        string factionId,
        string name,
        string materializationId) =>
        new()
        {
            ["factionId"] = factionId,
            ["name"] = name,
            ["description"] = "A complete historical faction.",
            ["image_prompt"] =
                "weathered wardens beside an old stone bridge",
            ["factionColor"] = "#6A7382",
            ["purpose"] = "Keep the old road open.",
            ["currentAgenda"] = "Repair the western bridge.",
            ["principles"] = new JsonArray("Warn before judgment."),
            ["level"] = 2,
            ["experience"] = 30,
            ["experienceForNextLevel"] = 100,
            ["developmentArchetype"] = "Balanced",
            ["customArchetypePriorities"] = null,
            ["powerProfile"] = new JsonObject
            {
                ["military"] = 1,
                ["economic"] = 1,
                ["social"] = 1,
                ["covert"] = 1,
                ["logistics"] = 1,
                ["stability"] = 1,
                ["arcane_tech"] = 0,
                ["exploration"] = 1
            },
            ["isPlayerFaction"] = false,
            ["isPlayerMember"] = false,
            ["playerRank"] = null,
            ["playerBranch"] = null,
            ["playerStrategyDirective"] = null,
            ["reputation"] = 0,
            ["reputationDescription"] = null,
            ["relations"] = new JsonArray(),
            ["controlledTerritories"] = new JsonArray(),
            ["memory"] = new JsonObject
            {
                ["summary"] =
                    "The first watch captain kept the bridge ledger.",
                ["lastUpdatedTurn"] = 7,
                ["enduringFacts"] = new JsonArray(
                    "The bridge ledger survived the flood."),
                ["openThreads"] = new JsonArray(
                    "The missing toll chest remains unfound.")
            },
            ["materialization"] = BuildEnvelope(
                factionId,
                materializationId)
        };

    private static JsonObject BuildEnvelope(
        string factionId,
        string materializationId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = materializationId,
            ["factionType"] = "mortal_faction",
            ["factionId"] = factionId,
            ["materializedAtTurn"] = 7,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["hasFormalHierarchy"] = false,
                ["usesFactionResources"] = false,
                ["maintainsRelations"] = false,
                ["runsProjects"] = false,
                ["holdsTerritoryOrInfluence"] = false,
                ["supportsPlayerMembership"] = false,
                ["usesCustomMechanics"] = false
            },
            ["sections"] = new JsonObject
            {
                ["hierarchy"] =
                    EmptyDisposition("No formal ranks existed."),
                ["resources"] =
                    EmptyDisposition("No treasury existed."),
                ["relations"] =
                    EmptyDisposition("No compact existed at materialization."),
                ["projects"] =
                    EmptyDisposition("No chartered projects existed."),
                ["territoryAndInfluence"] =
                    EmptyDisposition("No territory was claimed."),
                ["playerMembership"] =
                    EmptyDisposition("The player was not a member."),
                ["customStates"] =
                    EmptyDisposition("No custom mechanics existed.")
            }
        };

    private static JsonObject BuildStructureRoot() =>
        new()
        {
            ["entries"] = new JsonArray(
                BuildStructureEntry(
                    "faction_watch",
                    "Wayfarer Watch"),
                BuildStructureEntry(
                    "faction_bridge_compact",
                    "Bridge Compact"))
        };

    private static JsonObject BuildStructureEntry(
        string factionId,
        string factionName) =>
        new()
        {
            ["factionId"] = factionId,
            ["factionName"] = factionName,
            ["name"] = factionName,
            ["governance"] = new JsonObject
            {
                ["model"] = "Open moot",
                ["decisionProcess"] =
                    "All wardens decide by consensus."
            },
            ["leadership"] = new JsonObject
            {
                ["leadershipState"] = "vacant",
                ["summary"] =
                    "No permanent chair has been selected.",
                ["leaderNpcIds"] = new JsonArray()
            },
            ["ranks"] = new JsonObject
            {
                ["branches"] = new JsonArray()
            },
            ["structuredBonuses"] = new JsonArray()
        };

    private static JsonObject BuildResourcesRoot() =>
        new()
        {
            ["entries"] = new JsonArray(
                BuildEmptyResourceEntry(
                    "faction_watch",
                    "Wayfarer Watch"),
                BuildEmptyResourceEntry(
                    "faction_bridge_compact",
                    "Bridge Compact"))
        };

    private static JsonObject BuildEmptyResourceEntry(
        string factionId,
        string factionName) =>
        new()
        {
            ["factionId"] = factionId,
            ["factionName"] = factionName,
            ["name"] = factionName,
            ["metaResources"] = new JsonArray(),
            ["strategicGoods"] = new JsonArray()
        };

    private static JsonObject BuildCustomRoot() =>
        new()
        {
            ["entries"] = new JsonArray(
                BuildEmptyCustomEntry(
                    "faction_watch",
                    "Wayfarer Watch"),
                BuildEmptyCustomEntry(
                    "faction_bridge_compact",
                    "Bridge Compact"))
        };

    private static JsonObject BuildEmptyCustomEntry(
        string factionId,
        string factionName) =>
        new()
        {
            ["factionId"] = factionId,
            ["factionName"] = factionName,
            ["name"] = factionName,
            ["customStates"] = new JsonArray()
        };

    private static JsonObject BuildChroniclesRoot() =>
        new()
        {
            ["entries"] = new JsonArray(
                new JsonObject
                {
                    ["factionId"] = "faction_watch",
                    ["factionName"] = "Wayfarer Watch",
                    ["entry"] =
                        "#7 - The watch survived the western flood."
                },
                new JsonObject
                {
                    ["factionId"] = "faction_bridge_compact",
                    ["factionName"] = "Bridge Compact",
                    ["entry"] =
                        "#7 - The compact reopened the eastern tollhouse."
                })
        };

    private static JsonObject BuildNpcCoreRoot() =>
        new()
        {
            ["NPCsInScene"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_watch_captain",
                ["name"] = "Captain Vesna"
            })
        };

    private static JsonObject EmptyDisposition(string reason) =>
        new()
        {
            ["state"] = "empty_by_design",
            ["reason"] = reason
        };

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Ignore test-only temporary cleanup failures.
        }
    }

    private sealed record FactionFixture(
        JsonObject PreTurnCore,
        IReadOnlyDictionary<string, string> Backups);
}
