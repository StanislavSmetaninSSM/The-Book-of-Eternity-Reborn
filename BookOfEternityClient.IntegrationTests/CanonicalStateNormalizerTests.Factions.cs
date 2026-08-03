using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed partial class CanonicalStateNormalizerTests
{
    private const string MortalFactionCorePath =
        "game_state/factions/faction_core.json";
    private const string MortalFactionStructurePath =
        "game_state/factions/faction_structure.json";
    private const string MortalFactionResourcesPath =
        "game_state/factions/faction_resources.json";
    private const string MortalFactionProjectsPath =
        "game_state/factions/faction_projects.json";
    private const string MortalFactionCustomPath =
        "game_state/factions/faction_custom.json";
    private const string MortalFactionChroniclesPath =
        "game_state/factions/faction_chronicles.json";

    [Fact]
    public async Task Normalize_MaterializedCreation_ExtractsEverySidecarAndChronicle()
    {
        var backups = await WriteFactionCoreAsync(
            BuildCompleteMinimalMortalCreation());

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var core = await ReadFactionObjectAsync(MortalFactionCorePath);
        var faction = Assert.IsType<JsonObject>(
            Assert.Single(core["factions"]!.AsArray()));
        Assert.Equal(
            "temp-faction-watch",
            faction["factionId"]!.GetValue<string>());
        Assert.NotNull(faction["materialization"]);
        Assert.False(faction.ContainsKey("governance"));
        Assert.False(faction.ContainsKey("leadership"));
        Assert.False(faction.ContainsKey("resources"));
        Assert.False(faction.ContainsKey("ranks"));
        Assert.False(faction.ContainsKey("structuredBonuses"));
        Assert.False(faction.ContainsKey("activeProjects"));
        Assert.False(faction.ContainsKey("completedProjects"));
        Assert.False(faction.ContainsKey("customStates"));
        Assert.False(faction.ContainsKey("scribeChronicle"));

        AssertExactEmptyStructure(await ReadFactionObjectAsync(
            MortalFactionStructurePath));
        AssertExactEmptyResources(await ReadFactionObjectAsync(
            MortalFactionResourcesPath));
        AssertExactEmptyCustom(await ReadFactionObjectAsync(
            MortalFactionCustomPath));

        var chronicles = await ReadFactionObjectAsync(
            MortalFactionChroniclesPath);
        var entry = Assert.IsType<JsonObject>(
            Assert.Single(chronicles["entries"]!.AsArray()));
        Assert.Equal(
            "temp-faction-watch",
            entry["factionId"]!.GetValue<string>());
        Assert.Equal(
            "#12 - The Wayfarer Watch took responsibility for the western road.",
            entry["entry"]!.GetValue<string>());
        Assert.False(entry.ContainsKey("timestamp"));
    }

    [Fact]
    public async Task Normalize_MaterializedCreation_ExtractsPopulatedSidecars()
    {
        var backups = await WriteFactionCoreAsync(
            BuildCompletePopulatedMortalCreation());

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var structure = await ReadFactionObjectAsync(
            MortalFactionStructurePath);
        var structureEntry = Assert.IsType<JsonObject>(
            Assert.Single(structure["entries"]!.AsArray()));
        Assert.Equal(
            "Open moot",
            structureEntry["governance"]!["model"]!.GetValue<string>());
        Assert.Equal(
            "collective",
            structureEntry["leadership"]!["leadershipState"]!.GetValue<string>());
        var branch = Assert.IsType<JsonObject>(Assert.Single(
            structureEntry["ranks"]!["branches"]!.AsArray()));
        Assert.Equal("road_wardens", branch["branchId"]!.GetValue<string>());
        var bonus = Assert.IsType<JsonObject>(Assert.Single(
            structureEntry["structuredBonuses"]!.AsArray()));
        Assert.Equal("safe_passage", bonus["bonusId"]!.GetValue<string>());

        var resources = await ReadFactionObjectAsync(
            MortalFactionResourcesPath);
        var resourceEntry = Assert.IsType<JsonObject>(
            Assert.Single(resources["entries"]!.AsArray()));
        Assert.Single(resourceEntry["metaResources"]!.AsArray());
        Assert.Single(resourceEntry["strategicGoods"]!.AsArray());

        var projects = await ReadFactionObjectAsync(
            MortalFactionProjectsPath);
        var activeProject = Assert.IsType<JsonObject>(
            Assert.Single(projects["activeProjects"]!.AsArray()));
        Assert.Equal(
            "project_watchtower",
            activeProject["projectId"]!.GetValue<string>());
        var completedProject = Assert.IsType<JsonObject>(
            Assert.Single(projects["completedProjects"]!.AsArray()));
        Assert.Equal(
            "project_bridge_survey",
            completedProject["projectId"]!.GetValue<string>());

        var custom = await ReadFactionObjectAsync(MortalFactionCustomPath);
        var customEntry = Assert.IsType<JsonObject>(
            Assert.Single(custom["entries"]!.AsArray()));
        var customState = Assert.IsType<JsonObject>(
            Assert.Single(customEntry["customStates"]!.AsArray()));
        Assert.Equal(
            "bridge_repair_priority",
            customState["stateId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Normalize_MaterializedCreation_BindsEverySidecarToSameTurnIdentity()
    {
        var backups = await WriteFactionCoreAsync(
            BuildCompletePopulatedMortalCreation());

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var core = await ReadFactionObjectAsync(MortalFactionCorePath);
        var faction = Assert.IsType<JsonObject>(
            Assert.Single(core["factions"]!.AsArray()));
        Assert.Equal(
            "temp-faction-watch",
            faction["materialization"]!["factionId"]!.GetValue<string>());

        foreach (var path in new[]
                 {
                     MortalFactionStructurePath,
                     MortalFactionResourcesPath,
                     MortalFactionCustomPath
                 })
        {
            var sidecar = await ReadFactionObjectAsync(path);
            var entry = Assert.IsType<JsonObject>(
                Assert.Single(sidecar["entries"]!.AsArray()));
            Assert.Equal(
                "temp-faction-watch",
                entry["factionId"]!.GetValue<string>());
            Assert.False(entry.ContainsKey("initialFactionId"));
        }

        var projects = await ReadFactionObjectAsync(
            MortalFactionProjectsPath);
        foreach (var project in projects["activeProjects"]!
                     .AsArray()
                     .Concat(projects["completedProjects"]!.AsArray())
                     .OfType<JsonObject>())
        {
            Assert.Equal(
                "temp-faction-watch",
                project["factionId"]!.GetValue<string>());
            Assert.False(project.ContainsKey("initialFactionId"));
        }

        var chronicles = await ReadFactionObjectAsync(
            MortalFactionChroniclesPath);
        var chronicle = Assert.IsType<JsonObject>(
            Assert.Single(chronicles["entries"]!.AsArray()));
        Assert.Equal(
            "temp-faction-watch",
            chronicle["factionId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Normalize_MaterializedPromotion_PreservesExistingChronicle()
    {
        var promotion = BuildCompleteMinimalMortalCreation();
        promotion["factionId"] = "faction_wayfarer_watch";
        promotion.Remove("initialId");
        promotion.Remove("isNewFaction");
        promotion["materialization"]!["factionId"] =
            "faction_wayfarer_watch";
        promotion["materialization"]!["materializationId"] =
            "fmat_wayfarer_watch_promotion";
        promotion["scribeChronicle"] = new JsonArray(
            "#12 - The Wayfarer Watch adopted a permanent charter.");

        var previousCore = new JsonObject
        {
            ["factions"] = new JsonArray(new JsonObject
            {
                ["factionId"] = "faction_wayfarer_watch",
                ["name"] = "Wayfarer Watch",
                ["legacyMarker"] = "preserve-me"
            })
        };
        var backups = await WriteFactionCoreAsync(promotion, previousCore);
        await AddFactionBackupAsync(
            backups,
            MortalFactionChroniclesPath,
            new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = "faction_wayfarer_watch",
                    ["factionName"] = "Wayfarer Watch",
                    ["entry"] = "#4 - Caravan survivors founded the first road watch.",
                    ["timestamp"] = "2026-07-01T00:00:00Z"
                })
            });

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var chronicles = await ReadFactionObjectAsync(
            MortalFactionChroniclesPath);
        var entries = chronicles["entries"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Contains(entries, entry =>
            entry["entry"]!.GetValue<string>() ==
                "#4 - Caravan survivors founded the first road watch." &&
            entry["timestamp"]!.GetValue<string>() ==
                "2026-07-01T00:00:00Z");
        var promotionEntry = Assert.Single(entries, entry =>
            entry["entry"]!.GetValue<string>() ==
                "#12 - The Wayfarer Watch adopted a permanent charter.");
        Assert.Equal(
            "faction_wayfarer_watch",
            promotionEntry["factionId"]!.GetValue<string>());
        Assert.False(promotionEntry.ContainsKey("timestamp"));

        var core = await ReadFactionObjectAsync(MortalFactionCorePath);
        var faction = Assert.IsType<JsonObject>(
            Assert.Single(core["factions"]!.AsArray()));
        Assert.Equal("preserve-me", faction["legacyMarker"]!.GetValue<string>());
    }

    [Fact]
    public async Task Normalize_MaterializedCreation_PersistsExplicitProjectEmptiness()
    {
        var backups = await WriteFactionCoreAsync(
            BuildCompleteMinimalMortalCreation());

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var projects = await ReadFactionObjectAsync(
            MortalFactionProjectsPath);
        Assert.Equal(2, projects.Count);
        Assert.Empty(projects["activeProjects"]!.AsArray());
        Assert.Empty(projects["completedProjects"]!.AsArray());
    }

    [Fact]
    public async Task Normalize_UntouchedLegacyCore_PreservesCarrierShapedFields()
    {
        var legacyFaction = new JsonObject
        {
            ["factionId"] = "faction_legacy_watch",
            ["name"] = "Legacy Watch",
            ["governance"] = new JsonObject
            {
                ["model"] = "Inherited council"
            },
            ["leadership"] = new JsonObject
            {
                ["leadershipState"] = "vacant"
            },
            ["ranks"] = new JsonObject
            {
                ["branches"] = new JsonArray()
            },
            ["structuredBonuses"] = new JsonArray(),
            ["resources"] = new JsonObject
            {
                ["metaResources"] = new JsonArray(),
                ["strategicGoods"] = new JsonArray()
            },
            ["activeProjects"] = new JsonArray(),
            ["completedProjects"] = new JsonArray(),
            ["customStates"] = new JsonArray(),
            ["scribeChronicle"] = new JsonArray(
                "#3 - The old watch survived the winter.")
        };
        var currentRoot = new JsonObject
        {
            ["factions"] = new JsonArray(legacyFaction.DeepClone())
        };
        await _fs.WriteFileAtomicAsync(
            MortalFactionCorePath,
            currentRoot.ToJsonString());
        var backups = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        await AddFactionBackupAsync(backups, MortalFactionCorePath, currentRoot);

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var core = await ReadFactionObjectAsync(MortalFactionCorePath);
        var normalized = Assert.IsType<JsonObject>(
            Assert.Single(core["factions"]!.AsArray()));
        Assert.True(
            JsonNode.DeepEquals(legacyFaction, normalized),
            $"Expected untouched legacy core to remain unchanged.{Environment.NewLine}" +
            $"Expected: {legacyFaction.ToJsonString()}{Environment.NewLine}" +
            $"Actual: {normalized.ToJsonString()}");
        Assert.False(
            _fs.FileExists(MortalFactionChroniclesPath),
            "Untouched receipt-less legacy chronicles must remain embedded.");
    }

    [Fact]
    public async Task Normalize_GovernanceOnlyCarrier_PreservesExistingRanksAndBonuses()
    {
        var currentFaction = BuildStoredMaterializedMortalFaction(
            "faction_wayfarer_watch");
        currentFaction["governance"] = new JsonObject
        {
            ["model"] = "Rotating road council",
            ["decisionProcess"] = "One delegate from each patrol votes."
        };
        currentFaction["leadership"] = new JsonObject
        {
            ["leadershipState"] = "collective",
            ["summary"] = "Patrol delegates share leadership.",
            ["leaderNpcIds"] = new JsonArray()
        };

        var currentCore = new JsonObject
        {
            ["factions"] = new JsonArray(currentFaction)
        };
        await _fs.WriteFileAtomicAsync(
            MortalFactionCorePath,
            currentCore.ToJsonString());
        var backups = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        await AddFactionBackupAsync(
            backups,
            MortalFactionCorePath,
            new JsonObject
            {
                ["factions"] = new JsonArray(
                    BuildStoredMaterializedMortalFaction(
                        "faction_wayfarer_watch"))
            });
        await AddFactionBackupAsync(
            backups,
            MortalFactionStructurePath,
            new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = "faction_wayfarer_watch",
                    ["factionName"] = "Wayfarer Watch",
                    ["governance"] = new JsonObject
                    {
                        ["model"] = "Founder's word"
                    },
                    ["leadership"] = new JsonObject
                    {
                        ["leadershipState"] = "vacant",
                        ["summary"] = "No successor has been chosen.",
                        ["leaderNpcIds"] = new JsonArray()
                    },
                    ["ranks"] = new JsonObject
                    {
                        ["branches"] = new JsonArray(new JsonObject
                        {
                            ["branchId"] = "road_wardens",
                            ["ranks"] = new JsonArray()
                        })
                    },
                    ["structuredBonuses"] = new JsonArray(new JsonObject
                    {
                        ["bonusId"] = "safe_passage"
                    })
                })
            });

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var structure = await ReadFactionObjectAsync(
            MortalFactionStructurePath);
        var entry = Assert.IsType<JsonObject>(
            Assert.Single(structure["entries"]!.AsArray()));
        Assert.Equal(
            "Rotating road council",
            entry["governance"]!["model"]!.GetValue<string>());
        Assert.Equal(
            "collective",
            entry["leadership"]!["leadershipState"]!.GetValue<string>());
        Assert.Single(entry["ranks"]!["branches"]!.AsArray());
        Assert.Single(entry["structuredBonuses"]!.AsArray());

        var core = await ReadFactionObjectAsync(MortalFactionCorePath);
        var storedFaction = Assert.IsType<JsonObject>(
            Assert.Single(core["factions"]!.AsArray()));
        Assert.False(storedFaction.ContainsKey("governance"));
        Assert.False(storedFaction.ContainsKey("leadership"));
    }

    [Fact]
    public async Task Normalize_MaterializedPromotion_StripsMergedLegacyCarrierAndPreservesSidecars()
    {
        const string factionId = "faction_wayfarer_watch";
        var legacyFaction = BuildCarrierShapedLegacyMortalFaction(factionId);
        var promotion = BuildCompletePopulatedMortalCreation();
        promotion["factionId"] = factionId;
        promotion.Remove("initialId");
        promotion.Remove("isNewFaction");
        promotion["materialization"]!["factionId"] = factionId;
        promotion["materialization"]!["materializationId"] =
            "fmat_wayfarer_watch_promotion";

        foreach (var field in new[]
                 {
                     "governance",
                     "leadership",
                     "ranks",
                     "structuredBonuses",
                     "resources",
                     "activeProjects",
                     "completedProjects",
                     "customStates",
                     "scribeChronicle"
                 })
        {
            promotion[field] = legacyFaction[field]!.DeepClone();
        }

        var backups = await WriteFactionCoreAsync(
            promotion,
            new JsonObject
            {
                ["factions"] = new JsonArray(legacyFaction)
            });

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var core = await ReadFactionObjectAsync(MortalFactionCorePath);
        var storedFaction = Assert.IsType<JsonObject>(
            Assert.Single(core["factions"]!.AsArray()));
        Assert.Equal(
            "preserve-me",
            storedFaction["legacyMarker"]!.GetValue<string>());
        Assert.NotNull(storedFaction["materialization"]);
        foreach (var field in new[]
                 {
                     "governance",
                     "leadership",
                     "ranks",
                     "structuredBonuses",
                     "resources",
                     "activeProjects",
                     "completedProjects",
                     "customStates",
                     "scribeChronicle"
                 })
        {
            Assert.False(
                storedFaction.ContainsKey(field),
                $"Expected materialized core to omit carrier field '{field}'.");
        }

        var structure = await ReadFactionObjectAsync(
            MortalFactionStructurePath);
        var structureEntry = Assert.IsType<JsonObject>(
            Assert.Single(structure["entries"]!.AsArray()));
        Assert.Equal(
            "Legacy council",
            structureEntry["governance"]!["model"]!.GetValue<string>());
        Assert.Single(structureEntry["ranks"]!["branches"]!.AsArray());
        Assert.Single(structureEntry["structuredBonuses"]!.AsArray());

        var resources = await ReadFactionObjectAsync(
            MortalFactionResourcesPath);
        var resourceEntry = Assert.IsType<JsonObject>(
            Assert.Single(resources["entries"]!.AsArray()));
        Assert.Single(resourceEntry["metaResources"]!.AsArray());
        Assert.Single(resourceEntry["strategicGoods"]!.AsArray());

        var projects = await ReadFactionObjectAsync(
            MortalFactionProjectsPath);
        Assert.Single(projects["activeProjects"]!.AsArray());
        Assert.Single(projects["completedProjects"]!.AsArray());

        var custom = await ReadFactionObjectAsync(MortalFactionCustomPath);
        var customEntry = Assert.IsType<JsonObject>(
            Assert.Single(custom["entries"]!.AsArray()));
        Assert.Single(customEntry["customStates"]!.AsArray());

        var chronicles = await ReadFactionObjectAsync(
            MortalFactionChroniclesPath);
        Assert.Contains(
            chronicles["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["entry"]!.GetValue<string>() ==
                "#3 - The old watch survived the winter.");
    }

    [Fact]
    public async Task Normalize_MaterializedPromotion_MigratesEmbeddedLegacyChroniclesAndDeduplicatesByIdentityAndText()
    {
        const string factionId = "faction_wayfarer_watch";
        const string duplicateEntry =
            "#4 - Caravan survivors founded the first road watch.";
        const string embeddedOnlyEntry =
            "#8 - The old watch reopened the mountain road.";
        const string promotionEntry =
            "#12 - The Wayfarer Watch adopted a permanent charter.";

        var legacyFaction = BuildCarrierShapedLegacyMortalFaction(factionId);
        legacyFaction["scribeChronicle"] = new JsonArray(
            duplicateEntry,
            embeddedOnlyEntry);

        var promotion = BuildCompleteMinimalMortalCreation();
        promotion["factionId"] = factionId;
        promotion.Remove("initialId");
        promotion.Remove("isNewFaction");
        promotion["materialization"]!["factionId"] = factionId;
        promotion["materialization"]!["materializationId"] =
            "fmat_wayfarer_watch_promotion";
        promotion["scribeChronicle"] = new JsonArray(promotionEntry);

        var backups = await WriteFactionCoreAsync(
            promotion,
            new JsonObject
            {
                ["factions"] = new JsonArray(legacyFaction)
            });
        await AddFactionBackupAsync(
            backups,
            MortalFactionChroniclesPath,
            new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["factionName"] = "Wayfarer Watch",
                    ["entry"] = duplicateEntry,
                    ["timestamp"] = "2026-07-01T00:00:00Z",
                    ["source"] = "legacy-import"
                })
            });

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var chronicles = await ReadFactionObjectAsync(
            MortalFactionChroniclesPath);
        var entries = chronicles["entries"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(3, entries.Length);
        var deduplicated = Assert.Single(entries, entry =>
            entry["factionId"]!.GetValue<string>() == factionId &&
            entry["entry"]!.GetValue<string>() == duplicateEntry);
        Assert.Equal(
            "2026-07-01T00:00:00Z",
            deduplicated["timestamp"]!.GetValue<string>());
        Assert.Equal(
            "legacy-import",
            deduplicated["source"]!.GetValue<string>());
        Assert.Contains(entries, entry =>
            entry["factionId"]!.GetValue<string>() == factionId &&
            entry["entry"]!.GetValue<string>() == embeddedOnlyEntry);
        Assert.Contains(entries, entry =>
            entry["factionId"]!.GetValue<string>() == factionId &&
            entry["entry"]!.GetValue<string>() == promotionEntry);
    }

    [Fact]
    public async Task Normalize_OrdinaryChronicleUpdate_RepeatedTextAppendsDistinctEvent()
    {
        const string factionId = "faction_wayfarer_watch";
        const string repeatedText =
            "#14 - The watch renewed its western-road patrol.";
        await _fs.WriteFileAtomicAsync(
            MortalFactionChroniclesPath,
            new JsonObject
            {
                ["factionChronicleUpdates"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["factionName"] = "Wayfarer Watch",
                    ["entry"] = repeatedText,
                    ["timestamp"] = "2026-08-03T12:00:00Z",
                    ["eventId"] = "patrol-renewal-2"
                })
            }.ToJsonString());

        var backups = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        await AddFactionBackupAsync(
            backups,
            MortalFactionChroniclesPath,
            new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["factionName"] = "Wayfarer Watch",
                    ["entry"] = repeatedText,
                    ["timestamp"] = "2026-08-02T12:00:00Z",
                    ["eventId"] = "patrol-renewal-1"
                })
            });

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var chronicles = await ReadFactionObjectAsync(
            MortalFactionChroniclesPath);
        Assert.False(chronicles.ContainsKey("factionChronicleUpdates"));
        var entries = chronicles["entries"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Contains(entries, entry =>
            entry["eventId"]!.GetValue<string>() == "patrol-renewal-1");
        Assert.Contains(entries, entry =>
            entry["eventId"]!.GetValue<string>() == "patrol-renewal-2");
    }

    [Fact]
    public async Task Normalize_ExistingChronicles_SameTextDifferentMetadataRemainDistinct()
    {
        const string factionId = "faction_wayfarer_watch";
        const string repeatedText =
            "#15 - The watch escorted another caravan through the pass.";
        await _fs.WriteFileAtomicAsync(
            MortalFactionChroniclesPath,
            new JsonObject
            {
                ["entries"] = new JsonArray(
                    new JsonObject
                    {
                        ["factionId"] = factionId,
                        ["entry"] = repeatedText,
                        ["timestamp"] = "2026-08-01T15:00:00Z",
                        ["caravanId"] = "caravan-north"
                    },
                    new JsonObject
                    {
                        ["factionId"] = factionId,
                        ["entry"] = repeatedText,
                        ["timestamp"] = "2026-08-02T15:00:00Z",
                        ["caravanId"] = "caravan-south"
                    })
            }.ToJsonString());

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase));

        var chronicles = await ReadFactionObjectAsync(
            MortalFactionChroniclesPath);
        var entries = chronicles["entries"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Contains(entries, entry =>
            entry["caravanId"]!.GetValue<string>() == "caravan-north");
        Assert.Contains(entries, entry =>
            entry["caravanId"]!.GetValue<string>() == "caravan-south");
    }

    [Theory]
    [InlineData("chronicle")]
    [InlineData("text")]
    public async Task Normalize_MaterializedPromotion_DeduplicatesEmbeddedLegacyChronicleAgainstAlias(
        string aliasName)
    {
        const string factionId = "faction_wayfarer_watch";
        const string repeatedText =
            "#9 - The old watch reopened the eastern crossing.";

        var legacyFaction = BuildCarrierShapedLegacyMortalFaction(factionId);
        legacyFaction["scribeChronicle"] = new JsonArray(repeatedText);

        var promotion = BuildCompleteMinimalMortalCreation();
        promotion["factionId"] = factionId;
        promotion.Remove("initialId");
        promotion.Remove("isNewFaction");
        promotion["materialization"]!["factionId"] = factionId;
        promotion["materialization"]!["materializationId"] =
            "fmat_wayfarer_watch_alias_promotion";
        promotion["scribeChronicle"] = new JsonArray();

        var backups = await WriteFactionCoreAsync(
            promotion,
            new JsonObject
            {
                ["factions"] = new JsonArray(legacyFaction)
            });
        var existingAliasEntry = new JsonObject
        {
            ["factionId"] = factionId,
            ["factionName"] = "Wayfarer Watch",
            ["timestamp"] = "2026-07-09T00:00:00Z",
            ["source"] = "canonical-alias"
        };
        existingAliasEntry[aliasName] = repeatedText;
        await AddFactionBackupAsync(
            backups,
            MortalFactionChroniclesPath,
            new JsonObject
            {
                ["entries"] = new JsonArray(existingAliasEntry)
            });

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var chronicles = await ReadFactionObjectAsync(
            MortalFactionChroniclesPath);
        var retained = Assert.IsType<JsonObject>(
            Assert.Single(chronicles["entries"]!.AsArray()));
        Assert.Equal(
            repeatedText,
            retained[aliasName]!.GetValue<string>());
        Assert.Equal(
            "2026-07-09T00:00:00Z",
            retained["timestamp"]!.GetValue<string>());
        Assert.Equal(
            "canonical-alias",
            retained["source"]!.GetValue<string>());
        Assert.False(retained.ContainsKey("entry"));
    }

    [Fact]
    public async Task Normalize_MaterializedCreations_SameNameDistinctIdsRemainSeparate()
    {
        var east = BuildPopulatedCreationForIdentity(
            "temp-faction-east",
            "fmat_shared_watch_east",
            "Shared Watch");
        var west = BuildPopulatedCreationForIdentity(
            "temp-faction-west",
            "fmat_shared_watch_west",
            "Shared Watch");
        var backups = await WriteFactionCoreChangesAsync(east, west);

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        await AssertEveryFactionSurfaceHasExactIdsAsync(
            "temp-faction-east",
            "temp-faction-west");
    }

    [Fact]
    public async Task Normalize_MaterializedCreations_CaseOnlyDistinctIdsRemainSeparate()
    {
        var lower = BuildPopulatedCreationForIdentity(
            "temp-faction-watch",
            "fmat_case_watch_lower",
            "Lowercase Watch");
        var upper = BuildPopulatedCreationForIdentity(
            "TEMP-FACTION-WATCH",
            "fmat_case_watch_upper",
            "Uppercase Watch");
        var backups = await WriteFactionCoreChangesAsync(lower, upper);

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        await AssertEveryFactionSurfaceHasExactIdsAsync(
            "TEMP-FACTION-WATCH",
            "temp-faction-watch");
    }

    [Fact]
    public async Task Normalize_TransientGovernanceAndLeadership_WinOverLiveStructureWhileCommandsRemainLast()
    {
        const string factionId = "faction_wayfarer_watch";
        var currentFaction = BuildStoredMaterializedMortalFaction(factionId);
        currentFaction["governance"] = new JsonObject
        {
            ["model"] = "Transient road council"
        };
        currentFaction["leadership"] = new JsonObject
        {
            ["leadershipState"] = "collective",
            ["summary"] = "Transient patrol delegates lead together.",
            ["leaderNpcIds"] = new JsonArray()
        };
        currentFaction["ranks"] = new JsonObject
        {
            ["branches"] = new JsonArray(new JsonObject
            {
                ["branchId"] = "command_branch",
                ["displayName"] = "Stale transient branch",
                ["ranks"] = new JsonArray()
            })
        };
        currentFaction["structuredBonuses"] = new JsonArray(new JsonObject
        {
            ["bonusId"] = "command_bonus",
            ["description"] = "Stale transient bonus."
        });
        await _fs.WriteFileAtomicAsync(
            MortalFactionCorePath,
            new JsonObject
            {
                ["factions"] = new JsonArray(currentFaction)
            }.ToJsonString());

        var liveStructure = new JsonObject
        {
            ["entries"] = new JsonArray(new JsonObject
            {
                ["factionId"] = factionId,
                ["factionName"] = "Wayfarer Watch",
                ["governance"] = new JsonObject
                {
                    ["model"] = "Stale live council"
                },
                ["leadership"] = new JsonObject
                {
                    ["leadershipState"] = "vacant",
                    ["summary"] = "The live sidecar has not caught up.",
                    ["leaderNpcIds"] = new JsonArray()
                },
                ["ranks"] = new JsonObject
                {
                    ["branches"] = new JsonArray(new JsonObject
                    {
                        ["branchId"] = "baseline_branch",
                        ["displayName"] = "Baseline Branch",
                        ["ranks"] = new JsonArray()
                    })
                },
                ["structuredBonuses"] = new JsonArray(new JsonObject
                {
                    ["bonusId"] = "baseline_bonus",
                    ["description"] = "Preserved live bonus."
                })
            }),
            ["factionRankChanges"] = new JsonArray(new JsonObject
            {
                ["factionId"] = factionId,
                ["branchesToAdd"] = new JsonArray(new JsonObject
                {
                    ["branchId"] = "command_branch",
                    ["displayName"] = "Command-applied Branch",
                    ["ranks"] = new JsonArray()
                })
            }),
            ["factionBonusChanges"] = new JsonArray(new JsonObject
            {
                ["factionId"] = factionId,
                ["bonusesToAddOrUpdate"] = new JsonArray(new JsonObject
                {
                    ["bonusId"] = "command_bonus",
                    ["description"] = "Command-applied bonus."
                })
            })
        };
        await _fs.WriteFileAtomicAsync(
            MortalFactionStructurePath,
            liveStructure.ToJsonString());

        var backups = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        await AddFactionBackupAsync(
            backups,
            MortalFactionCorePath,
            new JsonObject
            {
                ["factions"] = new JsonArray(
                    BuildStoredMaterializedMortalFaction(factionId))
            });
        await AddFactionBackupAsync(
            backups,
            MortalFactionStructurePath,
            new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["factionName"] = "Wayfarer Watch",
                    ["governance"] = new JsonObject
                    {
                        ["model"] = "Historic council"
                    },
                    ["leadership"] = new JsonObject
                    {
                        ["leadershipState"] = "vacant",
                        ["summary"] = "Historic leadership.",
                        ["leaderNpcIds"] = new JsonArray()
                    },
                    ["ranks"] = new JsonObject
                    {
                        ["branches"] = new JsonArray()
                    },
                    ["structuredBonuses"] = new JsonArray()
                })
            });

        await CreateFactionNormalizer().NormalizeAccumulatedStateAsync(backups);

        var structure = await ReadFactionObjectAsync(
            MortalFactionStructurePath);
        var entry = Assert.IsType<JsonObject>(
            Assert.Single(structure["entries"]!.AsArray()));
        Assert.Equal(
            "Transient road council",
            entry["governance"]!["model"]!.GetValue<string>());
        Assert.Equal(
            "Transient patrol delegates lead together.",
            entry["leadership"]!["summary"]!.GetValue<string>());

        var branches = entry["ranks"]!["branches"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(2, branches.Length);
        Assert.Contains(branches, branch =>
            branch["branchId"]!.GetValue<string>() == "baseline_branch");
        var commandBranch = Assert.Single(branches, branch =>
            branch["branchId"]!.GetValue<string>() == "command_branch");
        Assert.Equal(
            "Command-applied Branch",
            commandBranch["displayName"]!.GetValue<string>());

        var bonuses = entry["structuredBonuses"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(2, bonuses.Length);
        Assert.Contains(bonuses, bonus =>
            bonus["bonusId"]!.GetValue<string>() == "baseline_bonus");
        var commandBonus = Assert.Single(bonuses, bonus =>
            bonus["bonusId"]!.GetValue<string>() == "command_bonus");
        Assert.Equal(
            "Command-applied bonus.",
            commandBonus["description"]!.GetValue<string>());
    }

    private CanonicalStateNormalizer CreateFactionNormalizer() =>
        new(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

    private async Task<Dictionary<string, string>> WriteFactionCoreAsync(
        JsonObject faction,
        JsonObject? previousCore = null)
    {
        await _fs.WriteFileAtomicAsync(
            MortalFactionCorePath,
            new JsonObject
            {
                ["factionDataChanges"] = new JsonArray(faction)
            }.ToJsonString());

        var backups = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        await AddFactionBackupAsync(
            backups,
            MortalFactionCorePath,
            previousCore ?? new JsonObject
            {
                ["factions"] = new JsonArray()
            });
        return backups;
    }

    private async Task<Dictionary<string, string>> WriteFactionCoreChangesAsync(
        params JsonObject[] factions)
    {
        var changes = new JsonArray();
        foreach (var faction in factions)
            changes.Add(faction);

        await _fs.WriteFileAtomicAsync(
            MortalFactionCorePath,
            new JsonObject
            {
                ["factionDataChanges"] = changes
            }.ToJsonString());

        var backups = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        await AddFactionBackupAsync(
            backups,
            MortalFactionCorePath,
            new JsonObject
            {
                ["factions"] = new JsonArray()
            });
        return backups;
    }

    private async Task AddFactionBackupAsync(
        IDictionary<string, string> backups,
        string originalPath,
        JsonObject root)
    {
        var backupPath =
            "test_backups/task4_" +
            originalPath.Replace("/", "_", StringComparison.Ordinal);
        await _fs.WriteFileAtomicAsync(backupPath, root.ToJsonString());
        backups[originalPath] = backupPath;
    }

    private async Task<JsonObject> ReadFactionObjectAsync(string path)
    {
        var json = await _fs.ReadFileAsync(path);
        Assert.False(
            string.IsNullOrWhiteSpace(json),
            $"Expected {path} to exist and contain JSON.");
        return Assert.IsType<JsonObject>(JsonNode.Parse(json!));
    }

    private async Task AssertEveryFactionSurfaceHasExactIdsAsync(
        params string[] expectedIds)
    {
        var core = await ReadFactionObjectAsync(MortalFactionCorePath);
        AssertExactFactionIds(core["factions"]!.AsArray(), expectedIds);

        foreach (var path in new[]
                 {
                     MortalFactionStructurePath,
                     MortalFactionResourcesPath,
                     MortalFactionCustomPath,
                     MortalFactionChroniclesPath
                 })
        {
            var sidecar = await ReadFactionObjectAsync(path);
            AssertExactFactionIds(sidecar["entries"]!.AsArray(), expectedIds);
        }

        var projects = await ReadFactionObjectAsync(
            MortalFactionProjectsPath);
        AssertExactFactionIds(
            projects["activeProjects"]!.AsArray(),
            expectedIds);
        AssertExactFactionIds(
            projects["completedProjects"]!.AsArray(),
            expectedIds);
    }

    private static void AssertExactFactionIds(
        JsonArray entries,
        params string[] expectedIds)
    {
        var expected = expectedIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var actual = entries
            .OfType<JsonObject>()
            .Select(entry => entry["factionId"]!.GetValue<string>())
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, actual);
    }

    private static void AssertExactEmptyStructure(JsonObject actual)
    {
        var expected = new JsonObject
        {
            ["entries"] = new JsonArray(new JsonObject
            {
                ["factionId"] = "temp-faction-watch",
                ["factionName"] = "Wayfarer Watch",
                ["name"] = "Wayfarer Watch",
                ["governance"] = new JsonObject
                {
                    ["model"] = "Open moot",
                    ["decisionProcess"] =
                        "Active wardens decide by simple majority."
                },
                ["leadership"] = new JsonObject
                {
                    ["leadershipState"] = "vacant",
                    ["summary"] =
                        "The founder died and no successor has been chosen.",
                    ["leaderNpcIds"] = new JsonArray()
                },
                ["ranks"] = new JsonObject
                {
                    ["branches"] = new JsonArray()
                },
                ["structuredBonuses"] = new JsonArray()
            })
        };
        AssertFactionJsonEquals(expected, actual);
    }

    private static void AssertExactEmptyResources(JsonObject actual)
    {
        var expected = new JsonObject
        {
            ["entries"] = new JsonArray(new JsonObject
            {
                ["factionId"] = "temp-faction-watch",
                ["factionName"] = "Wayfarer Watch",
                ["name"] = "Wayfarer Watch",
                ["metaResources"] = new JsonArray(),
                ["strategicGoods"] = new JsonArray()
            })
        };
        AssertFactionJsonEquals(expected, actual);
    }

    private static void AssertExactEmptyCustom(JsonObject actual)
    {
        var expected = new JsonObject
        {
            ["entries"] = new JsonArray(new JsonObject
            {
                ["factionId"] = "temp-faction-watch",
                ["factionName"] = "Wayfarer Watch",
                ["name"] = "Wayfarer Watch",
                ["customStates"] = new JsonArray()
            })
        };
        AssertFactionJsonEquals(expected, actual);
    }

    private static void AssertFactionJsonEquals(
        JsonObject expected,
        JsonObject actual)
    {
        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            $"Expected: {expected.ToJsonString()}{Environment.NewLine}" +
            $"Actual: {actual.ToJsonString()}");
    }

    private static JsonObject BuildCompletePopulatedMortalCreation()
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction["leadership"] = new JsonObject
        {
            ["leadershipState"] = "collective",
            ["summary"] = "The road wardens govern as a collective.",
            ["leaderNpcIds"] = new JsonArray()
        };
        faction["ranks"] = new JsonObject
        {
            ["branches"] = new JsonArray(new JsonObject
            {
                ["branchId"] = "road_wardens",
                ["name"] = "Road Wardens",
                ["ranks"] = new JsonArray(new JsonObject
                {
                    ["rankId"] = "warden",
                    ["name"] = "Warden"
                })
            })
        };
        faction["structuredBonuses"] = new JsonArray(new JsonObject
        {
            ["bonusId"] = "safe_passage",
            ["description"] =
                "Wardens provide safer passage on watched roads."
        });
        faction["resources"] = new JsonObject
        {
            ["metaResources"] = new JsonArray(new JsonObject
            {
                ["resourceId"] = "warden_trust",
                ["name"] = "Warden Trust",
                ["amount"] = 8
            }),
            ["strategicGoods"] = new JsonArray(new JsonObject
            {
                ["goodId"] = "bridge_timbers",
                ["name"] = "Bridge Timbers",
                ["amount"] = 12
            })
        };
        faction["activeProjects"] = new JsonArray(new JsonObject
        {
            ["projectId"] = "project_watchtower",
            ["name"] = "Raise the Watchtower"
        });
        faction["completedProjects"] = new JsonArray(new JsonObject
        {
            ["projectId"] = "project_bridge_survey",
            ["name"] = "Survey the Old Bridge"
        });
        faction["customStates"] = new JsonArray(new JsonObject
        {
            ["stateId"] = "bridge_repair_priority",
            ["value"] = "urgent"
        });
        faction["materialization"] = BuildMortalMaterialization(
            "temp-faction-watch",
            populated: true);
        return faction;
    }

    private static JsonObject BuildPopulatedCreationForIdentity(
        string initialId,
        string materializationId,
        string name)
    {
        var faction = BuildCompletePopulatedMortalCreation();
        faction["initialId"] = initialId;
        faction["name"] = name;
        faction["scribeChronicle"] = new JsonArray(
            $"#12 - {name} completed its materialization.");
        faction["materialization"]!["factionId"] = initialId;
        faction["materialization"]!["materializationId"] = materializationId;
        return faction;
    }

    private static JsonObject BuildCarrierShapedLegacyMortalFaction(
        string factionId) =>
        new()
        {
            ["factionId"] = factionId,
            ["name"] = "Wayfarer Watch",
            ["description"] = "The old road watch before materialization.",
            ["legacyMarker"] = "preserve-me",
            ["governance"] = new JsonObject
            {
                ["model"] = "Legacy council",
                ["decisionProcess"] = "The longest-serving wardens vote."
            },
            ["leadership"] = new JsonObject
            {
                ["leadershipState"] = "collective",
                ["summary"] = "Veteran wardens lead together.",
                ["leaderNpcIds"] = new JsonArray()
            },
            ["ranks"] = new JsonObject
            {
                ["branches"] = new JsonArray(new JsonObject
                {
                    ["branchId"] = "legacy_wardens",
                    ["name"] = "Legacy Wardens",
                    ["ranks"] = new JsonArray()
                })
            },
            ["structuredBonuses"] = new JsonArray(new JsonObject
            {
                ["bonusId"] = "legacy_safe_passage",
                ["description"] = "The old watch knows the safest paths."
            }),
            ["resources"] = new JsonObject
            {
                ["metaResources"] = new JsonArray(new JsonObject
                {
                    ["resourceId"] = "legacy_trust",
                    ["name"] = "Legacy Trust",
                    ["amount"] = 4
                }),
                ["strategicGoods"] = new JsonArray(new JsonObject
                {
                    ["goodId"] = "legacy_timbers",
                    ["name"] = "Legacy Timbers",
                    ["amount"] = 6
                })
            },
            ["activeProjects"] = new JsonArray(new JsonObject
            {
                ["projectId"] = "legacy_watchtower",
                ["name"] = "Repair the Old Watchtower"
            }),
            ["completedProjects"] = new JsonArray(new JsonObject
            {
                ["projectId"] = "legacy_bridge_survey",
                ["name"] = "Survey the Old Bridge"
            }),
            ["customStates"] = new JsonArray(new JsonObject
            {
                ["stateId"] = "legacy_bridge_priority",
                ["value"] = "urgent"
            }),
            ["scribeChronicle"] = new JsonArray(
                "#3 - The old watch survived the winter.")
        };

    private static JsonObject BuildCompleteMinimalMortalCreation() =>
        new()
        {
            ["factionId"] = null,
            ["initialId"] = "temp-faction-watch",
            ["isNewFaction"] = true,
            ["name"] = "Wayfarer Watch",
            ["description"] = "A small watch formed to keep one road safe.",
            ["image_prompt"] =
                "weathered road wardens beneath a wooden watchtower",
            ["factionColor"] = "#7B6852",
            ["purpose"] = "Keep the old western road open.",
            ["currentAgenda"] =
                "Repair the bridge before the spring thaw.",
            ["principles"] = new JsonArray(
                "Every traveler receives warning before judgment."),
            ["memory"] = new JsonObject
            {
                ["summary"] =
                    "The watch formed after the bridge massacre.",
                ["lastUpdatedTurn"] = 12,
                ["enduringFacts"] = new JsonArray(
                    "The first wardens were caravan survivors."),
                ["openThreads"] = new JsonArray(
                    "The bridge attackers were never identified.")
            },
            ["governance"] = new JsonObject
            {
                ["model"] = "Open moot",
                ["decisionProcess"] =
                    "Active wardens decide by simple majority."
            },
            ["leadership"] = new JsonObject
            {
                ["leadershipState"] = "vacant",
                ["summary"] =
                    "The founder died and no successor has been chosen.",
                ["leaderNpcIds"] = new JsonArray()
            },
            ["powerProfile"] = new JsonObject
            {
                ["military"] = 0,
                ["economic"] = 0,
                ["social"] = 0,
                ["covert"] = 0,
                ["logistics"] = 0,
                ["stability"] = 0,
                ["arcane_tech"] = 0,
                ["exploration"] = 0
            },
            ["ranks"] = new JsonObject
            {
                ["branches"] = new JsonArray()
            },
            ["structuredBonuses"] = new JsonArray(),
            ["resources"] = new JsonObject
            {
                ["metaResources"] = new JsonArray(),
                ["strategicGoods"] = new JsonArray()
            },
            ["relations"] = new JsonArray(),
            ["activeProjects"] = new JsonArray(),
            ["completedProjects"] = new JsonArray(),
            ["controlledTerritories"] = new JsonArray(),
            ["customStates"] = new JsonArray(),
            ["scribeChronicle"] = new JsonArray(
                "#12 - The Wayfarer Watch took responsibility for the western road."),
            ["isPlayerFaction"] = false,
            ["isPlayerMember"] = false,
            ["playerRank"] = null,
            ["playerBranch"] = null,
            ["playerStrategyDirective"] = null,
            ["reputation"] = 0,
            ["reputationDescription"] = null,
            ["level"] = 1,
            ["experience"] = 0,
            ["experienceForNextLevel"] = 100,
            ["developmentArchetype"] = "Custodian",
            ["materialization"] = BuildMortalMaterialization(
                "temp-faction-watch",
                populated: false)
        };

    private static JsonObject BuildStoredMaterializedMortalFaction(
        string factionId)
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction["factionId"] = factionId;
        faction.Remove("initialId");
        faction.Remove("isNewFaction");
        faction.Remove("governance");
        faction.Remove("leadership");
        faction.Remove("ranks");
        faction.Remove("structuredBonuses");
        faction.Remove("resources");
        faction.Remove("activeProjects");
        faction.Remove("completedProjects");
        faction.Remove("customStates");
        faction.Remove("scribeChronicle");
        faction["materialization"]!["factionId"] = factionId;
        return faction;
    }

    private static JsonObject BuildMortalMaterialization(
        string factionId,
        bool populated) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = "fmat_watch_creation",
            ["factionType"] = "mortal_faction",
            ["factionId"] = factionId,
            ["materializedAtTurn"] = 12,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["hasFormalHierarchy"] = populated,
                ["usesFactionResources"] = populated,
                ["maintainsRelations"] = false,
                ["runsProjects"] = populated,
                ["holdsTerritoryOrInfluence"] = false,
                ["supportsPlayerMembership"] = false,
                ["usesCustomMechanics"] = populated
            },
            ["sections"] = new JsonObject
            {
                ["hierarchy"] = FactionDisposition(
                    populated,
                    "No ranks exist yet."),
                ["resources"] = FactionDisposition(
                    populated,
                    "No formal resources exist yet."),
                ["relations"] = FactionDisposition(
                    false,
                    "No formal relations exist yet."),
                ["projects"] = FactionDisposition(
                    populated,
                    "No projects exist yet."),
                ["territoryAndInfluence"] = FactionDisposition(
                    false,
                    "No territory is claimed."),
                ["playerMembership"] = FactionDisposition(
                    false,
                    "The player is not a member."),
                ["customStates"] = FactionDisposition(
                    populated,
                    "No custom state exists.")
            }
        };

    private static JsonObject FactionDisposition(
        bool populated,
        string emptyReason) =>
        populated
            ? new JsonObject
            {
                ["state"] = "populated"
            }
            : new JsonObject
            {
                ["state"] = "empty_by_design",
                ["reason"] = emptyReason
            };
}
