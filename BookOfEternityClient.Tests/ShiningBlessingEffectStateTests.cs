using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningBlessingEffectStateTests
{
    [Fact]
    public async Task MaterializeForBootstrapAsync_WritesCanonicalBlessingStateAndAppliesResources()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 3,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject
            {
                ["money"] = 0
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            var result = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 3);

            Assert.True(result.Success);
            Assert.True(result.StateChanged);
            Assert.Contains(result.SummaryLines, line => line.Contains("Стартовые ресурсы", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.SummaryLines, line => line.Contains("Blessing audit: effectId=resourceGrant", StringComparison.Ordinal));
            Assert.Contains(result.SummaryLines, line => line.Contains("sourceCardIds=[card_resource]", StringComparison.Ordinal));
            Assert.Contains(result.SummaryLines, line => line.Contains("Blessing audit: effectId=card_social", StringComparison.Ordinal));
            Assert.DoesNotContain(result.SummaryLines, line => line.Contains("effectPayload", StringComparison.OrdinalIgnoreCase));

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var blessingState = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!.AsObject();
            var memorySelection = blessingState["memorySelection"]!.AsObject();
            var resourceGrant = blessingState["resourceGrant"]!.AsObject();
            var relicEntitlements = blessingState["relicRefinementEntitlements"]!.AsObject();

            Assert.Equal("active", blessingState["applicationState"]!.GetValue<string>());
            Assert.Equal(1, memorySelection["options"]!.GetValue<int>());
            Assert.Equal(1, memorySelection["rerolls"]!.GetValue<int>());
            Assert.Equal("applied_at_bootstrap", resourceGrant["status"]!.GetValue<string>());
            Assert.Equal(150, resourceGrant["money"]!.GetValue<int>());
            Assert.Equal(2, resourceGrant["common"]!.GetValue<int>());
            Assert.Equal(1, resourceGrant["uncommon"]!.GetValue<int>());
            Assert.Equal(2, relicEntitlements["rerolls"]!.GetValue<int>());
            Assert.True(relicEntitlements["freeShape"]!.GetValue<bool>());
            Assert.False(relicEntitlements["freeRetune"]!.GetValue<bool>());

            var statusRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/core/player_status.json"))!)!.AsObject();
            Assert.Equal(150, statusRoot["money"]!.GetValue<int>());

            var inventoryRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
            var resources = inventoryRoot["resources"]!.AsObject();
            Assert.Equal(2, resources["common"]!.GetValue<int>());
            Assert.Equal(1, resources["uncommon"]!.GetValue<int>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task MaterializeForBootstrapAsync_RetryAfterPartialResourceGrantDoesNotDuplicateResources()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 3,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject
            {
                ["money"] = 150,
                ["_shiningBootstrapResourceGrantIds"] = new JsonArray("shining_bootstrap_resource:42:3:card_resource")
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            var result = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 3);

            Assert.True(result.Success);
            Assert.True(result.StateChanged);

            var statusRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/core/player_status.json"))!)!.AsObject();
            Assert.Equal(150, statusRoot["money"]!.GetValue<int>());

            var inventoryRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/inventory/items.json"))!)!.AsObject();
            var resources = inventoryRoot["resources"]!.AsObject();
            Assert.Equal(2, resources["common"]!.GetValue<int>());
            Assert.Equal(1, resources["uncommon"]!.GetValue<int>());

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var resourceGrant = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["resourceGrant"]!.AsObject();
            Assert.Equal("shining_bootstrap_resource:42:3:card_resource", resourceGrant["grantId"]!.GetValue<string>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task MaterializeForBootstrapAsync_InvalidPreparedPackage_FailsBeforeWritingEffects()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 3,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());

            var invalidPackage = CreatePreparedPackage();
            invalidPackage["selectedCardIds"] = new JsonArray("card_memory", "missing_card");

            var result = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, invalidPackage, 3);

            Assert.False(result.Success);
            Assert.False(result.StateChanged);
            Assert.Contains("preparedIncarnationPackage", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            Assert.False(soulRoot.ContainsKey(ShiningBlessingEffectState.SoulStateProperty));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_ListsDeferredBlessingEffectsInMortalRealm()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);

            var reminder = await ShiningBlessingEffectState.BuildSystemReminderFragmentAsync(fs, "Mortal World", 5);

            Assert.NotNull(reminder);
            Assert.Contains("SHINING BLESSINGS", reminder);
            Assert.Contains("social pending", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("route pending", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("lore pending", reminder, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task MaterializeForBootstrapAsync_PrimesDescentQualityOnMatchingCompanionRelic()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 3,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject
                {
                    ["equipped"] = new JsonArray(),
                    ["stored"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["relicId"] = "relic_echo",
                            ["name"] = "Эхо",
                            ["rarity"] = "rare",
                            ["companionSeed"] = new JsonObject
                            {
                                ["sourceResidentId"] = "resident_echo"
                            }
                        }
                    }
                }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            var result = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 3);

            Assert.True(result.Success);
            Assert.Contains(result.SummaryLines, line => line.Contains("descent blessing primed", StringComparison.OrdinalIgnoreCase));

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var storedRelic = soulRoot["soulRelics"]!["stored"]!.AsArray()[0]!.AsObject();
            Assert.Equal(15, storedRelic["companionManifestationQualityBonus"]!.GetValue<int>());

            var effectState = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!.AsObject();
            var descent = effectState["pendingDescentEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.DescentStatusPendingResidentDescent, descent["status"]!.GetValue<string>());
            Assert.Equal("relic_echo", descent["primedRelicId"]!.GetValue<string>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_ConsumesSocialAndLoreAndExpiresRoute()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);

            var preTurnNpcCore = new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray()
            };
            var currentNpcCore = new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_ally",
                        ["NPCName"] = "Союзник",
                        ["relationshipLevel"] = 10,
                        ["attitude"] = "Neutral"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", currentNpcCore.ToJsonString());

            var preTurnWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray()
            };
            var currentWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["eventId"] = "evt_lore_1",
                        ["anchorId"] = "anchor_lore_1",
                        ["visibility"] = "player_known"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", currentWorldEvents.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 10,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: preTurnNpcCore.ToJsonString(),
                preTurnWorldEventsJson: preTurnWorldEvents.ToJsonString());

            Assert.True(result.Success);
            Assert.True(result.StateChanged);
            Assert.Contains(result.SummaryLines, line => line.Contains("social blessing applied", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.SummaryLines, line => line.Contains("lore blessing satisfied", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.SummaryLines, line => line.Contains("route blessing expired", StringComparison.OrdinalIgnoreCase));

            var updatedNpcRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
            var npc = updatedNpcRoot[GuardianPolicyContracts.NpcCoreSceneSectionName]!.AsArray()[0]!.AsObject();
            Assert.Equal(25, npc["relationshipLevel"]!.GetValue<int>());

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var blessingState = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!.AsObject();
            var social = blessingState["pendingSocialEffects"]!.AsArray()[0]!.AsObject();
            var lore = blessingState["pendingLoreEffects"]!.AsArray()[0]!.AsObject();
            var route = blessingState["pendingRouteEffects"]!.AsArray()[0]!.AsObject();

            Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, social["status"]!.GetValue<string>());
            Assert.Equal("npc_ally", social["consumedTargetNpcId"]!.GetValue<string>());
            Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, lore["status"]!.GetValue<string>());
            Assert.Equal("evt_lore_1", lore["consumedEventIds"]!.AsArray()[0]!.GetValue<string>());
            Assert.Equal("anchor_lore_1", lore["consumedAnchorIds"]!.AsArray()[0]!.GetValue<string>());
            Assert.Equal(ShiningBlessingEffectState.GenericStatusExpired, route["status"]!.GetValue<string>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReadAndConsumePendingMemorySelectionAsync_TracksChosenLifeEcho()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["livesHistory"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["incarnation"] = 1,
                        ["summary"] = "Первая жизнь у дороги."
                    },
                    new JsonObject
                    {
                        ["incarnation"] = 3,
                        ["summary"] = "Третья жизнь в стеклянном городе."
                    }
                },
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);

            var pendingSelection = await ShiningBlessingEffectState.ReadPendingMemorySelectionAsync(fs);

            Assert.NotNull(pendingSelection);
            Assert.Equal(1, pendingSelection!.Options);
            Assert.Equal(1, pendingSelection.Rerolls);
            Assert.Equal(2, pendingSelection.Candidates.Count);
            Assert.Equal(3, pendingSelection.Candidates[0].Incarnation);

            var changed = await ShiningBlessingEffectState.ConsumePendingMemorySelectionAsync(
                fs,
                currentTurnNumber: 0,
                pendingSelection.Candidates[0],
                rerollsSpent: 1);

            Assert.True(changed);

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var memorySelection = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["memorySelection"]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, memorySelection["status"]!.GetValue<string>());
            Assert.Equal(3, memorySelection["selectedLifeIncarnation"]!.GetValue<int>());
            Assert.Equal("Третья жизнь в стеклянном городе.", memorySelection["selectedLifeSummary"]!.GetValue<string>());
            Assert.Equal(1, memorySelection["rerollsSpent"]!.GetValue<int>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_ConsumesDescentWhenMatchingCompanionMaterializes()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject
                {
                    ["equipped"] = new JsonArray(),
                    ["stored"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["relicId"] = "relic_echo",
                            ["name"] = "Эхо",
                            ["rarity"] = "rare",
                            ["companionSeed"] = new JsonObject
                            {
                                ["sourceResidentId"] = "resident_echo"
                            }
                        }
                    }
                }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);

            var preTurnNpcCore = new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreUpdateSectionName] = new JsonArray(),
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray()
            };
            var currentNpcCore = new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreUpdateSectionName] = new JsonArray(),
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_echo",
                        ["NPCName"] = "Эхо",
                        ["relationshipLevel"] = 15,
                        ["attitude"] = "Neutral",
                        ["sourceCompanionRelicId"] = "relic_echo",
                        ["sourceAfterlifeResidentId"] = "resident_echo"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", currentNpcCore.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", new JsonObject
            {
                ["events"] = new JsonArray()
            }.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 5,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: preTurnNpcCore.ToJsonString(),
                preTurnWorldEventsJson: null);

            Assert.True(result.Success);
            Assert.True(result.StateChanged);
            Assert.Contains(result.SummaryLines, line => line.Contains("descent blessing resolved", StringComparison.OrdinalIgnoreCase));

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var descent = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingDescentEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, descent["status"]!.GetValue<string>());
            Assert.Equal("npc_echo", descent["consumedNpcId"]!.GetValue<string>());
            Assert.Equal("relic_echo", descent["consumedRelicId"]!.GetValue<string>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_ConsumesSocialFromFactionRelationCommit()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray()
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_relationships.json", new JsonObject
            {
                ["NPCRelationshipChanges"] = new JsonArray()
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", new JsonObject
            {
                ["events"] = new JsonArray()
            }.ToJsonString());

            var preTurnFactionCore = new JsonObject
            {
                ["factions"] = new JsonArray()
            };
            var currentFactionCore = new JsonObject
            {
                ["factions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["factionId"] = "faction_glass",
                        ["name"] = "Стеклянный Дом",
                        ["reputation"] = 10,
                        ["reputationDescription"] = "Осторожный интерес"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", currentFactionCore.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 5,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: null,
                preTurnWorldEventsJson: null,
                preTurnNpcRelationshipsJson: null,
                preTurnPlayerStatusJson: null,
                preTurnFactionCoreJson: preTurnFactionCore.ToJsonString());

            Assert.True(result.Success);
            Assert.True(result.StateChanged);
            Assert.Contains(result.SummaryLines, line => line.Contains("faction relation commit", StringComparison.OrdinalIgnoreCase));

            var updatedFactionRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/factions/faction_core.json"))!)!.AsObject();
            var faction = updatedFactionRoot["factions"]!.AsArray()[0]!.AsObject();
            Assert.Equal(25, faction["reputation"]!.GetValue<int>());

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var social = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingSocialEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, social["status"]!.GetValue<string>());
            Assert.Equal("faction_glass", social["consumedTargetFactionId"]!.GetValue<string>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_ConsumesSocialFromNpcRelationshipCommit()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);

            var preTurnNpcCore = new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_ally",
                        ["NPCName"] = "Союзник",
                        ["relationshipLevel"] = 20,
                        ["attitude"] = "Neutral"
                    }
                }
            };
            var currentNpcCore = preTurnNpcCore.DeepClone().AsObject();
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", currentNpcCore.ToJsonString());

            var preTurnNpcRelationships = new JsonObject
            {
                ["NPCRelationshipChanges"] = new JsonArray()
            };
            var currentNpcRelationships = new JsonObject
            {
                ["NPCRelationshipChanges"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_ally",
                        ["NPCName"] = "Союзник",
                        ["newRelationshipLevel"] = 20,
                        ["changeReason"] = "Shared victory"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_relationships.json", currentNpcRelationships.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", new JsonObject
            {
                ["events"] = new JsonArray()
            }.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 5,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: preTurnNpcCore.ToJsonString(),
                preTurnWorldEventsJson: null,
                preTurnNpcRelationshipsJson: preTurnNpcRelationships.ToJsonString());

            Assert.True(result.Success);
            Assert.True(result.StateChanged);
            Assert.Contains(result.SummaryLines, line => line.Contains("relation commit", StringComparison.OrdinalIgnoreCase));

            var updatedNpcRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
            var npc = updatedNpcRoot[GuardianPolicyContracts.NpcCoreSceneSectionName]!.AsArray()[0]!.AsObject();
            Assert.Equal(35, npc["relationshipLevel"]!.GetValue<int>());

            var relationshipRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/npcs/npc_relationships.json"))!)!.AsObject();
            var relationshipEntry = relationshipRoot["NPCRelationshipChanges"]!.AsArray()[0]!.AsObject();
            Assert.Equal(35, relationshipEntry["newRelationshipLevel"]!.GetValue<int>());

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var social = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingSocialEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, social["status"]!.GetValue<string>());
            Assert.Equal("npc_ally", social["consumedTargetNpcId"]!.GetValue<string>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_UsesLexicalTieBreakAcrossNpcAndFactionSocialCommits()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);

            await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_zzz",
                        ["NPCName"] = "Поздний союзник",
                        ["relationshipLevel"] = 10,
                        ["attitude"] = "Neutral"
                    }
                }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_relationships.json", new JsonObject
            {
                ["NPCRelationshipChanges"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_zzz",
                        ["NPCName"] = "Поздний союзник",
                        ["newRelationshipLevel"] = 10,
                        ["changeReason"] = "Shared victory"
                    }
                }
            }.ToJsonString());

            var preTurnFactionCore = new JsonObject
            {
                ["factions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["factionId"] = "faction_aaa",
                        ["name"] = "Ранний союз",
                        ["reputation"] = 0
                    }
                }
            };
            var currentFactionCore = new JsonObject
            {
                ["factions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["factionId"] = "faction_aaa",
                        ["name"] = "Ранний союз",
                        ["reputation"] = 5
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", currentFactionCore.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", new JsonObject
            {
                ["events"] = new JsonArray()
            }.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 5,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: null,
                preTurnWorldEventsJson: null,
                preTurnNpcRelationshipsJson: new JsonObject { ["NPCRelationshipChanges"] = new JsonArray() }.ToJsonString(),
                preTurnPlayerStatusJson: null,
                preTurnFactionCoreJson: preTurnFactionCore.ToJsonString());

            Assert.True(result.Success);
            Assert.True(result.StateChanged);
            Assert.Contains(result.SummaryLines, line => line.Contains("faction relation commit", StringComparison.OrdinalIgnoreCase));

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var social = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingSocialEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, social["status"]!.GetValue<string>());
            Assert.Equal("faction_aaa", social["consumedTargetFactionId"]!.GetValue<string>());
            Assert.False(social.TryGetPropertyValue("consumedTargetNpcId", out _));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_DoesNotFallbackToNewNpcContactWhenRelationCommitSurfaced()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);

            var preTurnNpcCore = new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray()
            };
            var currentNpcCore = new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_new_friend",
                        ["NPCName"] = "Новый друг",
                        ["relationshipLevel"] = 10,
                        ["attitude"] = "Neutral"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", currentNpcCore.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_relationships.json", new JsonObject
            {
                ["NPCRelationshipChanges"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_hostile_commit",
                        ["NPCName"] = "Враждебный контакт",
                        ["newRelationshipLevel"] = -20,
                        ["changeReason"] = "Threat exchange"
                    }
                }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", new JsonObject
            {
                ["events"] = new JsonArray()
            }.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 5,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: preTurnNpcCore.ToJsonString(),
                preTurnWorldEventsJson: null,
                preTurnNpcRelationshipsJson: new JsonObject { ["NPCRelationshipChanges"] = new JsonArray() }.ToJsonString());

            Assert.True(result.Success);
            Assert.False(result.StateChanged);

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var social = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingSocialEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.SocialStatusPendingFirstRelationCommit, social["status"]!.GetValue<string>());
            Assert.False(social.TryGetPropertyValue("consumedTargetNpcId", out _));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_ConsumesSurvivalFromRuinousWorldEventAndRestoresGauges()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject
            {
                ["money"] = 0,
                ["healthPercentage"] = "60%",
                ["energyPercentage"] = "50%",
                ["poisePercentage"] = "40%",
                ["currentCondition"] = "Потрясён",
                ["activeConditions"] = new JsonArray("ruinous")
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray()
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_relationships.json", new JsonObject
            {
                ["NPCRelationshipChanges"] = new JsonArray()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);

            var preTurnStatus = new JsonObject
            {
                ["money"] = 0,
                ["healthPercentage"] = "100%",
                ["energyPercentage"] = "90%",
                ["poisePercentage"] = "80%",
                ["currentCondition"] = "Здоров",
                ["activeConditions"] = new JsonArray()
            };
            var preTurnWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray()
            };
            var currentWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["eventId"] = "evt_ruinous",
                        ["visibility"] = "player_known",
                        ["severity"] = "ruinous"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", currentWorldEvents.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 5,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: null,
                preTurnWorldEventsJson: preTurnWorldEvents.ToJsonString(),
                preTurnNpcRelationshipsJson: null,
                preTurnPlayerStatusJson: preTurnStatus.ToJsonString(),
                preTurnFactionCoreJson: null);

            Assert.True(result.Success);
            Assert.True(result.StateChanged);
            Assert.Contains(result.SummaryLines, line => line.Contains("survival blessing applied", StringComparison.OrdinalIgnoreCase));

            var updatedWorldEvents = JsonNode.Parse((await fs.ReadFileAsync("game_state/world/world_events.json"))!)!.AsObject();
            Assert.Equal("severe", updatedWorldEvents["events"]!.AsArray()[0]!["severity"]!.GetValue<string>());

            var updatedStatus = JsonNode.Parse((await fs.ReadFileAsync("game_state/core/player_status.json"))!)!.AsObject();
            Assert.Equal("68%", updatedStatus["healthPercentage"]!.GetValue<string>());
            Assert.Equal("58%", updatedStatus["energyPercentage"]!.GetValue<string>());
            Assert.Equal("48%", updatedStatus["poisePercentage"]!.GetValue<string>());

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var survival = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingSurvivalEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, survival["status"]!.GetValue<string>());
            Assert.Equal("evt_ruinous", survival["consumedEventId"]!.GetValue<string>());
            Assert.Equal(8, survival["restoredHealthPercentagePoints"]!.GetValue<int>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuildStatusLinesAsync_ReportsResolvedBlessingHooks()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject
            {
                ["money"] = 0,
                ["healthPercentage"] = "60%",
                ["energyPercentage"] = "50%",
                ["poisePercentage"] = "40%"
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_ally",
                        ["NPCName"] = "Союзник",
                        ["relationshipLevel"] = 20,
                        ["attitude"] = "Neutral"
                    }
                }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_relationships.json", new JsonObject
            {
                ["NPCRelationshipChanges"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_ally",
                        ["NPCName"] = "Союзник",
                        ["newRelationshipLevel"] = 20,
                        ["changeReason"] = "Shared victory"
                    }
                }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", new JsonObject
            {
                ["events"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["eventId"] = "evt_route_alpha",
                        ["routeSeedId"] = "route_seed_alpha",
                        ["visibility"] = "player_known"
                    },
                    new JsonObject
                    {
                        ["eventId"] = "evt_lore_alpha",
                        ["anchorId"] = "anchor_alpha",
                        ["visibility"] = "player_known"
                    }
                }
            }.ToJsonString());

            await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 5,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: new JsonObject { [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray() }.ToJsonString(),
                preTurnWorldEventsJson: new JsonObject { ["events"] = new JsonArray() }.ToJsonString(),
                preTurnNpcRelationshipsJson: new JsonObject { ["NPCRelationshipChanges"] = new JsonArray() }.ToJsonString(),
                preTurnPlayerStatusJson: new JsonObject
                {
                    ["money"] = 0,
                    ["healthPercentage"] = "60%",
                    ["energyPercentage"] = "50%",
                    ["poisePercentage"] = "40%"
                }.ToJsonString(),
                preTurnFactionCoreJson: null);

            var lines = await ShiningBlessingEffectState.BuildStatusLinesAsync(fs, currentTurnNumber: 5);

            Assert.Contains(lines, line => line.Contains("социальное благословение закрылось через связь с Союзник", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(lines, line => line.Contains("маршрут раскрылся через seed route_seed_alpha", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(lines, line => line.Contains("след знания закрепился через anchor anchor_alpha", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuildStatusLinesAsync_ReportsPrimedSpentAndExpiredBlessingLifecycleDetails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 5,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() },
                [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
                {
                    ["applicationState"] = "active",
                    ["materializedAtUtc"] = "2026-04-17T00:00:00Z",
                    ["sourcePackagePreparedAtTurn"] = 12,
                    ["currentIncarnation"] = 5,
                    ["sourceCardIds"] = new JsonArray("card_memory", "card_survival", "card_descent", "card_route", "card_relic"),
                    ["sourceCardCount"] = 5,
                    ["memorySelection"] = new JsonObject
                    {
                        ["options"] = 1,
                        ["rerolls"] = 1,
                        ["rerollsSpent"] = 1,
                        ["status"] = ShiningBlessingEffectState.GenericStatusConsumed,
                        ["consumedAtTurn"] = 1,
                        ["consumedAtUtc"] = "2026-04-17T00:10:00Z",
                        ["selectedLifeIncarnation"] = 3,
                        ["selectedLifeSummary"] = "Третья жизнь в стеклянном городе.",
                        ["sourceCardIds"] = new JsonArray("card_memory")
                    },
                    ["relicRefinementEntitlements"] = new JsonObject
                    {
                        ["rerolls"] = 0,
                        ["rerollsSpent"] = 2,
                        ["freeShape"] = false,
                        ["freeRetune"] = false,
                        ["status"] = ShiningBlessingEffectState.GenericStatusConsumed,
                        ["consumedAtTurn"] = 6,
                        ["consumedAtUtc"] = "2026-04-17T00:20:00Z",
                        ["sourceCardIds"] = new JsonArray("card_relic")
                    },
                    ["pendingSurvivalEffects"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["effectId"] = "card_survival",
                            ["sourceCardId"] = "card_survival",
                            ["downgrade"] = 1,
                            ["recovery"] = 20,
                            ["status"] = ShiningBlessingEffectState.GenericStatusConsumed,
                            ["consumedAtTurn"] = 6,
                            ["consumedAtUtc"] = "2026-04-17T00:21:00Z",
                            ["consumedEventId"] = "evt_ruinous_alpha",
                            ["restoredHealthPercentagePoints"] = 8
                        }
                    },
                    ["pendingDescentEffects"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["effectId"] = "card_descent",
                            ["sourceCardId"] = "card_descent",
                            ["sourceActorId"] = "resident_echo",
                            ["latestTurn"] = 12,
                            ["quality"] = 15,
                            ["status"] = ShiningBlessingEffectState.DescentStatusPendingResidentDescent,
                            ["primedRelicId"] = "relic_echo",
                            ["primedAtTurn"] = 2,
                            ["primedAtUtc"] = "2026-04-17T00:12:00Z"
                        }
                    },
                    ["pendingRouteEffects"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["effectId"] = "card_route",
                            ["sourceCardId"] = "card_route",
                            ["routeOptions"] = 1,
                            ["latestTurn"] = 7,
                            ["status"] = ShiningBlessingEffectState.GenericStatusExpired,
                            ["expiredAtTurn"] = 8,
                            ["expiredAtUtc"] = "2026-04-17T00:30:00Z"
                        }
                    }
                }
            }.ToJsonString());

            var lines = await ShiningBlessingEffectState.BuildStatusLinesAsync(fs, currentTurnNumber: 8);

            Assert.Contains(lines, line => line.Contains("выбор эха памяти завершён", StringComparison.OrdinalIgnoreCase) && line.Contains("Третья жизнь в стеклянном городе.", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(lines, line => line.Contains("спасающее благословение сработало через evt_ruinous_alpha", StringComparison.OrdinalIgnoreCase) && line.Contains("health+8", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(lines, line => line.Contains("нисхождение уже закреплено на реликвии relic_echo", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(lines, line => line.Contains("Истекло: route card_route", StringComparison.OrdinalIgnoreCase) && line.Contains("ходу 7", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(lines, line => line.Contains("кузнечные привилегии этой жизни исчерпаны", StringComparison.OrdinalIgnoreCase) && line.Contains("Перебросов потрачено: 2", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_DoesNotCountDuplicateLoreAnchorsTwice()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);
            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var lore = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingLoreEffects"]!.AsArray()[0]!.AsObject();
            lore["clueCount"] = 2;
            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulRoot.ToJsonString());

            var preTurnWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray()
            };
            var currentWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["eventId"] = "evt_lore_a_1",
                        ["anchorId"] = "anchor_shared",
                        ["visibility"] = "player_known"
                    },
                    new JsonObject
                    {
                        ["eventId"] = "evt_lore_a_2",
                        ["anchorId"] = "anchor_shared",
                        ["visibility"] = "player_known"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", currentWorldEvents.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 6,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: null,
                preTurnWorldEventsJson: preTurnWorldEvents.ToJsonString());

            Assert.True(result.Success);
            Assert.False(result.StateChanged);

            var updatedSoulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var updatedLore = updatedSoulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingLoreEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.LoreStatusPendingLoreInsertion, updatedLore["status"]!.GetValue<string>());
            Assert.False(updatedLore.TryGetPropertyValue("consumedAnchorIds", out _));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_UsesAnchorIdTieBreakForLore()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray()
            }.ToJsonString());

            var preTurnWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray()
            };
            var currentWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["eventId"] = "evt_lore_z",
                        ["anchorId"] = "z_anchor",
                        ["visibility"] = "player_known"
                    },
                    new JsonObject
                    {
                        ["eventId"] = "evt_lore_a",
                        ["anchorId"] = "a_anchor",
                        ["visibility"] = "player_known"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", currentWorldEvents.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 6,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: null,
                preTurnWorldEventsJson: preTurnWorldEvents.ToJsonString());

            Assert.True(result.Success);
            Assert.True(result.StateChanged);

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var lore = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingLoreEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, lore["status"]!.GetValue<string>());
            Assert.Equal("evt_lore_a", lore["consumedEventIds"]!.AsArray()[0]!.GetValue<string>());
            Assert.Equal("a_anchor", lore["consumedAnchorIds"]!.AsArray()[0]!.GetValue<string>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_DoesNotCountDuplicateRouteSeedIdsTwice()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);
            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var route = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingRouteEffects"]!.AsArray()[0]!.AsObject();
            route["routeOptions"] = 2;
            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulRoot.ToJsonString());

            var preTurnWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray()
            };
            var currentWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["eventId"] = "evt_route_alpha_1",
                        ["routeSeedId"] = "route_seed_alpha",
                        ["visibility"] = "player_known"
                    },
                    new JsonObject
                    {
                        ["eventId"] = "evt_route_alpha_2",
                        ["routeSeedId"] = "route_seed_alpha",
                        ["visibility"] = "player_known"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", currentWorldEvents.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 6,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: null,
                preTurnWorldEventsJson: preTurnWorldEvents.ToJsonString());

            Assert.True(result.Success);
            Assert.False(result.StateChanged);

            var updatedSoulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var updatedRoute = updatedSoulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingRouteEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.RouteStatusPendingEarlyRouteSeed, updatedRoute["status"]!.GetValue<string>());
            Assert.False(updatedRoute.TryGetPropertyValue("consumedRouteSeedIds", out _));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_ConsumesRouteFromRouteSeedWorldEvent()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray()
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_relationships.json", new JsonObject
            {
                ["NPCRelationshipChanges"] = new JsonArray()
            }.ToJsonString());

            var preTurnWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray()
            };
            var currentWorldEvents = new JsonObject
            {
                ["events"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["eventId"] = "evt_route_alpha",
                        ["routeSeedId"] = "route_seed_alpha",
                        ["visibility"] = "player_known"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", currentWorldEvents.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 6,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: null,
                preTurnWorldEventsJson: preTurnWorldEvents.ToJsonString());

            Assert.True(result.Success);
            Assert.True(result.StateChanged);
            Assert.Contains(result.SummaryLines, line => line.Contains("route blessing satisfied", StringComparison.OrdinalIgnoreCase));

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var route = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingRouteEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, route["status"]!.GetValue<string>());
            Assert.Equal("evt_route_alpha", route["consumedEventIds"]!.AsArray()[0]!.GetValue<string>());
            Assert.Equal("route_seed_alpha", route["consumedRouteSeedIds"]!.AsArray()[0]!.GetValue<string>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAcceptedTurnRuntimeEffectsAsync_DoesNotConsumeDescentForWrongResidentManifestation()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
                ["soulRelics"] = new JsonObject
                {
                    ["equipped"] = new JsonArray(),
                    ["stored"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["relicId"] = "relic_echo",
                            ["name"] = "Эхо",
                            ["rarity"] = "rare",
                            ["companionSeed"] = new JsonObject
                            {
                                ["sourceResidentId"] = "resident_echo"
                            }
                        }
                    }
                }
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/core/player_status.json", new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

            await ShiningBlessingEffectState.MaterializeForBootstrapAsync(fs, CreatePreparedPackage(), 4);

            var preTurnNpcCore = new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreUpdateSectionName] = new JsonArray(),
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray()
            };
            var currentNpcCore = new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreUpdateSectionName] = new JsonArray(),
                [GuardianPolicyContracts.NpcCoreSceneSectionName] = new JsonArray
                {
                    new JsonObject
                    {
                        ["NPCId"] = "npc_wrong_echo",
                        ["NPCName"] = "Чужое эхо",
                        ["relationshipLevel"] = -10,
                        ["attitude"] = "Hostile",
                        ["sourceCompanionRelicId"] = "relic_echo",
                        ["sourceAfterlifeResidentId"] = "resident_other"
                    }
                }
            };
            await fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", currentNpcCore.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/world/world_events.json", new JsonObject
            {
                ["events"] = new JsonArray()
            }.ToJsonString());

            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                fs,
                currentTurnNumber: 5,
                preTurnShiningJson: null,
                preTurnNpcCoreJson: preTurnNpcCore.ToJsonString(),
                preTurnWorldEventsJson: null);

            Assert.True(result.Success);
            Assert.False(result.StateChanged);

            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var descent = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["pendingDescentEffects"]!.AsArray()[0]!.AsObject();
            Assert.Equal(ShiningBlessingEffectState.DescentStatusPendingResidentDescent, descent["status"]!.GetValue<string>());
            Assert.False(descent.TryGetPropertyValue("consumedNpcId", out _));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void BuildPendingWorldDirectiveLines_DescribesExactPayloadSemantics()
    {
        var lines = ShiningBlessingEffectState.BuildPendingWorldDirectiveLines(CreatePreparedPackage());

        Assert.Contains(lines, line => line.Contains("first qualifying non-hostile relation commit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, line => line.Contains("seed 1 early route option", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, line => line.Contains("insert 1 lore clue", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, line => line.Contains("grant relic refinement entitlements", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject CreatePreparedPackage()
    {
        return new JsonObject
        {
            ["preparedAtTurn"] = 42,
            ["selectedCardIds"] = new JsonArray("card_memory", "card_resource", "card_social", "card_route", "card_lore", "card_survival", "card_descent", "card_relic"),
            ["selectedCards"] = new JsonArray
            {
                CreateCard("card_memory", "memory", new JsonObject
                {
                    ["type"] = "expand_memory_selection",
                    ["options"] = 1,
                    ["rerolls"] = 1
                }),
                CreateCard("card_resource", "resource", new JsonObject
                {
                    ["type"] = "grant_starting_resources",
                    ["money"] = 150,
                    ["common"] = 2,
                    ["uncommon"] = 1
                }),
                CreateCard("card_social", "social", new JsonObject
                {
                    ["type"] = "modify_first_ally_relation",
                    ["delta"] = 15
                }),
                CreateCard("card_route", "route", new JsonObject
                {
                    ["type"] = "seed_early_routes",
                    ["routeOptions"] = 1,
                    ["latestTurn"] = 8
                }),
                CreateCard("card_lore", "lore", new JsonObject
                {
                    ["type"] = "insert_lore_clues",
                    ["clueCount"] = 1,
                    ["latestTurn"] = 10
                }),
                CreateCard("card_survival", "survival", new JsonObject
                {
                    ["type"] = "downgrade_ruinous_failure",
                    ["downgrade"] = 1,
                    ["recovery"] = 20
                }),
                CreateCard("card_descent", "descent", new JsonObject
                {
                    ["type"] = "guide_resident_descent",
                    ["latestTurn"] = 8,
                    ["quality"] = 15
                }, sourceActorId: "resident_echo"),
                CreateCard("card_relic", "relic", new JsonObject
                {
                    ["type"] = "grant_relic_refinement",
                    ["rerolls"] = 2,
                    ["freeShape"] = true,
                    ["freeRetune"] = false
                })
            }
        };
    }

    private static JsonObject CreateCard(string cardId, string family, JsonObject payload, string sourceActorId = "guardian_dawn")
    {
        return new JsonObject
        {
            ["cardId"] = cardId,
            ["dedupeKey"] = $"{family}:{cardId}",
            ["sourceType"] = ShiningAbodeState.CardSourceTypeProject,
            ["sourceFactionId"] = "faction_dawn",
            ["displayName"] = cardId,
            ["displaySummary"] = family,
            ["sourceActorId"] = sourceActorId,
            ["effectFamily"] = family,
            ["rarity"] = ShiningAbodeState.RarityCommon,
            ["effectPayload"] = payload
        };
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "boe-shining-blessing-effects-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
