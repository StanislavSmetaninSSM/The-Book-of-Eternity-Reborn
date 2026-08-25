using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningAbodeTradeAndForgeStateTests
{
    [Fact]
    public void GetTradeStockItemCount_DormantTradeIgnoresProvisionAndResourceSupport()
    {
        var faction = new JsonObject
        {
            ["factionId"] = "faction_dormant",
            ["factionStrength"] = 20,
            ["projects"] = new JsonArray
            {
                new JsonObject
                {
                    ["projectId"] = "project_provision",
                    ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeProvision,
                    ["status"] = ShiningAbodeState.ProjectStatusCompleted,
                    ["isSupported"] = true
                }
            }
        };
        var residentRoot = new JsonObject
        {
            ["entries"] = new JsonArray
            {
                new JsonObject
                {
                    ["residentId"] = "resident_mira",
                    ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
                    ["shiningFactionId"] = "faction_dormant",
                    ["residentRole"] = ShiningAbodeState.ResidentRoleResourceSupport
                }
            }
        };

        Assert.Equal(0, ShiningAbodeState.GetTradeStockItemCount(faction, residentRoot));
        Assert.Equal("none", ShiningAbodeState.GetTradeRarityCeiling(20));
    }

    [Fact]
    public void TryQuoteForgeAction_WithForgeSupportAppliesDiscount()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["availability"] = ShiningAbodeState.AvailabilityActive;
        shiningRoot["radiance"] = new JsonObject
        {
            ["experience"] = 260,
            ["tier"] = 2
        };
        shiningRoot["lightSparks"] = 80;
        shiningRoot["factions"] = new JsonArray
        {
            new JsonObject
            {
                ["factionId"] = "faction_old",
                ["originType"] = ShiningAbodeState.OriginTypeAscendedGuardian,
                ["hallId"] = "hall_old",
                ["charter"] = new JsonObject
                {
                    ["factionName"] = "Старый Дом",
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
                    ["summary"] = "Кузня памяти."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
                    ["headActorId"] = "guardian_old",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["baseStrength"] = 35,
                ["factionStrength"] = 62,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["projectId"] = "project_refinement",
                        ["displayName"] = "Кузня Отголосков",
                        ["summary"] = "Держит refinement контур.",
                        ["toneTags"] = new JsonArray("relic"),
                        ["targetFactionIds"] = new JsonArray(),
                        ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
                        ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
                        ["tier"] = 2,
                        ["status"] = ShiningAbodeState.ProjectStatusCompleted,
                        ["isSupported"] = true,
                        ["strengthReward"] = 12
                    }
                },
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            }
        };
        CompleteOperationalFaction(
            shiningRoot["factions"]!.AsArray()[0]!.AsObject(),
            hasResidentAffiliations: true);

        var residentRoot = new JsonObject
        {
            ["entries"] = new JsonArray
            {
                new JsonObject
                {
                    ["residentId"] = "resident_smith",
                    ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
                    ["shiningFactionId"] = "faction_old",
                    ["residentRole"] = ShiningAbodeState.ResidentRoleForgeSupport
                }
            }
        };

        var soulRoot = new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = 80
            },
            ["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["relicId"] = "relic_old",
                        ["name"] = "Старый Клинок",
                        ["rarity"] = "rare",
                        ["formTag"] = "blade",
                        ["properties"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["propertyId"] = "edge",
                                ["band"] = "rare"
                            }
                        }
                    }
                }
            }
        };

        var success = ShiningAbodeState.TryQuoteForgeAction(
            shiningRoot,
            soulRoot,
            residentRoot,
            ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand,
            "faction_old",
            "relic_old",
            null,
            0,
            null,
            null,
            out var cost,
            out var error);

        Assert.True(success, error);
        Assert.Equal(25, cost.Feathers);
        Assert.Equal(15, cost.LightSparks);
    }

    [Fact]
    public void ForgeBlessingEntitlements_MakeReshapeFreeAndConsumeTheFlag()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["availability"] = ShiningAbodeState.AvailabilityActive;
        shiningRoot["radiance"] = new JsonObject
        {
            ["experience"] = 120,
            ["tier"] = 1
        };
        shiningRoot["lightSparks"] = 80;
        shiningRoot["factions"] = new JsonArray
        {
            new JsonObject
            {
                ["factionId"] = "faction_old",
                ["originType"] = ShiningAbodeState.OriginTypeAscendedGuardian,
                ["hallId"] = "hall_old",
                ["charter"] = new JsonObject
                {
                    ["factionName"] = "Старый Дом",
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
                    ["summary"] = "Кузня памяти."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
                    ["headActorId"] = "guardian_old",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["baseStrength"] = 35,
                ["factionStrength"] = 62,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["projectId"] = "project_refinement",
                        ["displayName"] = "Кузня Отголосков",
                        ["summary"] = "Держит refinement контур.",
                        ["toneTags"] = new JsonArray("relic"),
                        ["targetFactionIds"] = new JsonArray(),
                        ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
                        ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
                        ["tier"] = 2,
                        ["status"] = ShiningAbodeState.ProjectStatusCompleted,
                        ["isSupported"] = true,
                        ["strengthReward"] = 12
                    }
                },
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            }
        };
        CompleteOperationalFaction(
            shiningRoot["factions"]!.AsArray()[0]!.AsObject(),
            hasResidentAffiliations: false);

        var soulRoot = new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = 80
            },
            [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
            {
                ["applicationState"] = "active",
                ["materializedAtUtc"] = "2026-04-17T00:00:00Z",
                ["currentIncarnation"] = 1,
                ["sourcePackagePreparedAtTurn"] = 10,
                ["sourceCardIds"] = new JsonArray("card_relic"),
                ["sourceCardCount"] = 1,
                ["relicRefinementEntitlements"] = new JsonObject
                {
                    ["freeShape"] = true,
                    ["freeRetune"] = false,
                    ["status"] = ShiningBlessingEffectState.RelicStatusPendingEntitlement,
                    ["sourceCardIds"] = new JsonArray("card_relic")
                }
            },
            ["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["relicId"] = "relic_old",
                        ["name"] = "Старый Клинок",
                        ["rarity"] = "rare",
                        ["formTag"] = "blade",
                        ["properties"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["propertyId"] = "edge",
                                ["band"] = "rare"
                            }
                        }
                    }
                }
            }
        };

        Assert.True(ShiningAbodeState.TryQuoteForgeAction(
            shiningRoot,
            soulRoot,
            residentRoot: null,
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            "faction_old",
            "relic_old",
            "lance",
            -1,
            null,
            null,
            out var cost,
            out var error), error);
        Assert.Equal(0, cost.Feathers);
        Assert.Equal(0, cost.LightSparks);

        Assert.True(ShiningAbodeState.TryApplyForgeAction(
            shiningRoot,
            soulRoot,
            residentRoot: null,
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            "faction_old",
            "relic_old",
            "lance",
            -1,
            null,
            null,
            currentTurnNumber: 17,
            resolvedAtUtc: "2026-04-17T00:17:00Z",
            out _,
            out error), error);

        var entitlements = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["relicRefinementEntitlements"]!.AsObject();
        Assert.False(entitlements["freeShape"]!.GetValue<bool>());
        Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, entitlements["status"]!.GetValue<string>());
        Assert.Equal(17, entitlements["consumedAtTurn"]!.GetValue<int>());
        Assert.Equal("2026-04-17T00:17:00Z", entitlements["consumedAtUtc"]!.GetValue<string>());
        Assert.Equal(80, soulRoot["inkFeathers"]?["current"]?.GetValue<int>());
    }

    [Fact]
    public async Task ConsumeRelicRerollAsync_DecrementsPendingBlessingPool()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-shining-relic-reroll-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
            Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
            var profilesRoot = new JsonObject
            {
                [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray
                {
                    new JsonObject
                    {
                        ["actorType"] = "player_soul",
                        ["actorId"] = "player_soul",
                        ["displayName"] = "Душа игрока",
                        ["realm"] = "Shining Abode"
                    }
                }
            };
            var bootstrapSoulRoot = new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                ["currentIncarnation"] = 2,
                ["afterlifeCombatProfile"] = new JsonObject
                {
                    ["spiritFocusTier"] = 0
                },
                ["soulRelics"] = new JsonObject
                {
                    ["equipped"] = new JsonArray(),
                    ["stored"] = new JsonArray()
                }
            };
            await fs.WriteFileAtomicAsync(
                "game_state/core/player_status.json",
                new JsonObject { ["money"] = 0 }.ToJsonString());
            await fs.WriteFileAtomicAsync(
                "game_state/inventory/items.json",
                new JsonObject
                {
                    ["items"] = new JsonArray(),
                    ["equipment"] = new JsonObject(),
                    ["resources"] = new JsonObject()
                }.ToJsonString());
            var initialPlan = await CanonicalResourceQuartetTransaction
                .ComposeExplicitBootstrapAsync(
                    bootstrap.Definitions!,
                    bootstrap.State!,
                    bootstrap.History!,
                    new AfterlifeOwnerResourceAcceptedState(
                        Profiles: profilesRoot,
                        SoulState: bootstrapSoulRoot),
                    fs.ReadFileAsync);
            await CommitFreshResourceBootstrapAsync(fs, initialPlan);
            var materialized = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(
                fs,
                new JsonObject
                {
                    ["preparedAtTurn"] = 10,
                    ["selectedCardIds"] = new JsonArray("card_relic"),
                    ["selectedCards"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["cardId"] = "card_relic",
                            ["dedupeKey"] = "relic:card_relic",
                            ["sourceType"] = ShiningAbodeState.CardSourceTypeProject,
                            ["sourceFactionId"] = "faction_dawn",
                            ["displayName"] = "card_relic",
                            ["displaySummary"] = "relic",
                            ["sourceActorId"] = "guardian_dawn",
                            ["effectFamily"] = "relic",
                            ["rarity"] = ShiningAbodeState.RarityCommon,
                            ["effectPayload"] = new JsonObject
                            {
                                ["type"] = "grant_relic_refinement",
                                [ShiningBlessingRerollAllocationContract.PropertyName] =
                                    ShiningBlessingRerollAllocationContract.Create(1),
                                ["freeShape"] = false,
                                ["freeRetune"] = false
                            }
                        }
                    }
                },
                currentIncarnation: 2);
            Assert.True(materialized.Success, materialized.ErrorMessage);

            var changed = await ShiningBlessingEffectState.ConsumeRelicRerollAsync(fs, currentTurnNumber: 7);

            Assert.True(changed);
            var soulRoot = JsonNode.Parse((await fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            var entitlements = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!["relicRefinementEntitlements"]!.AsObject();
            Assert.False(entitlements.ContainsKey("rerolls"));
            Assert.False(entitlements.ContainsKey("rerollsSpent"));
            Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, entitlements["status"]!.GetValue<string>());
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Theory]
    [InlineData(1, ShiningBlessingEffectState.RelicStatusPendingEntitlement)]
    [InlineData(0, ShiningBlessingEffectState.GenericStatusConsumed)]
    public void ConsumeForgeEntitlements_ClosesOnlyAfterBoundResourceAllocationIsExhausted(
        int remainingRerolls,
        string expectedStatus)
    {
        var soulRoot = new JsonObject
        {
            [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
            {
                ["relicRefinementEntitlements"] = new JsonObject
                {
                    ["rerollResourceBinding"] = new JsonObject
                    {
                        ["realm"] = "shining_abode",
                        ["ownerKind"] = "afterlife_actor",
                        ["resourceOwnerId"] = "player_soul",
                        ["resourceKey"] = "blessing_rerolls",
                        ["allocationId"] = "shining_blessing_relic_10_2"
                    },
                    ["freeShape"] = true,
                    ["freeRetune"] = false,
                    ["status"] = ShiningBlessingEffectState.RelicStatusPendingEntitlement
                }
            }
        };

        Assert.True(ShiningBlessingEffectState.ConsumeForgeEntitlements(
            soulRoot,
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            currentTurnNumber: 17,
            consumedAtUtc: "2026-04-17T00:17:00Z",
            remainingRerolls));

        var entitlements = soulRoot[ShiningBlessingEffectState.SoulStateProperty]!
            ["relicRefinementEntitlements"]!.AsObject();
        Assert.Equal(expectedStatus, entitlements["status"]!.GetValue<string>());
        Assert.False(entitlements["freeShape"]!.GetValue<bool>());
        Assert.Equal(
            remainingRerolls == 0,
            entitlements.ContainsKey("consumedAtTurn"));
    }

    [Fact]
    public void TryApplyForgeAction_DebitsInkFeathersAndLightSparks()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["availability"] = ShiningAbodeState.AvailabilityActive;
        shiningRoot["radiance"] = new JsonObject
        {
            ["experience"] = 260,
            ["tier"] = 2
        };
        shiningRoot["lightSparks"] = 80;
        shiningRoot["factions"] = new JsonArray
        {
            new JsonObject
            {
                ["factionId"] = "faction_old",
                ["originType"] = ShiningAbodeState.OriginTypeAscendedGuardian,
                ["hallId"] = "hall_old",
                ["charter"] = new JsonObject
                {
                    ["factionName"] = "Старый Дом",
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
                    ["summary"] = "Кузня памяти."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
                    ["headActorId"] = "guardian_old",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["baseStrength"] = 35,
                ["factionStrength"] = 62,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["projectId"] = "project_refinement",
                        ["displayName"] = "Кузня Отголосков",
                        ["summary"] = "Держит refinement контур.",
                        ["toneTags"] = new JsonArray("relic"),
                        ["targetFactionIds"] = new JsonArray(),
                        ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
                        ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
                        ["tier"] = 2,
                        ["status"] = ShiningAbodeState.ProjectStatusCompleted,
                        ["isSupported"] = true,
                        ["strengthReward"] = 12
                    }
                },
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            }
        };
        CompleteOperationalFaction(
            shiningRoot["factions"]!.AsArray()[0]!.AsObject(),
            hasResidentAffiliations: true);
        var residentRoot = new JsonObject
        {
            ["entries"] = new JsonArray
            {
                new JsonObject
                {
                    ["residentId"] = "resident_smith",
                    ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
                    ["shiningFactionId"] = "faction_old",
                    ["residentRole"] = ShiningAbodeState.ResidentRoleForgeSupport
                }
            }
        };
        var soulRoot = new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = 80
            },
            ["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["relicId"] = "relic_old",
                        ["name"] = "Старый Клинок",
                        ["rarity"] = "rare",
                        ["formTag"] = "blade",
                        ["properties"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["propertyId"] = "edge",
                                ["band"] = "rare"
                            }
                        }
                    }
                }
            }
        };

        Assert.True(ShiningAbodeState.TryApplyForgeAction(
            shiningRoot,
            soulRoot,
            residentRoot,
            ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand,
            "faction_old",
            "relic_old",
            null,
            0,
            null,
            null,
            currentTurnNumber: 21,
            resolvedAtUtc: "2026-04-21T10:00:00Z",
            out var cost,
            out var error), error);

        Assert.Equal(25, cost.Feathers);
        Assert.Equal(15, cost.LightSparks);
        Assert.Equal(55, soulRoot["inkFeathers"]?["current"]?.GetValue<int>());
        Assert.Equal(65, shiningRoot["lightSparks"]?.GetValue<int>());
    }

    [Fact]
    public void TryQuoteForgeAction_UpliftCountsOnlyObjectAddedProperties()
    {
        var shiningRoot = CreateForgeReadyShiningRoot();
        var soulRoot = CreateForgeSoulRoot(new JsonObject
        {
            ["relicId"] = "relic_old",
            ["name"] = "Старый Клинок",
            ["quality"] = "common",
            ["formTag"] = "blade",
            ["properties"] = new JsonArray()
        });
        var addedProperties = new JsonArray
        {
            null,
            new JsonObject
            {
                ["propertyId"] = "echo_edge",
                ["band"] = "uncommon"
            }
        };

        var success = ShiningAbodeState.TryQuoteForgeAction(
            shiningRoot,
            soulRoot,
            residentRoot: null,
            ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity,
            "faction_old",
            "relic_old",
            null,
            -1,
            null,
            addedProperties,
            out _,
            out var error);

        Assert.False(success);
        Assert.Contains("не хватает 2", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryApplyForgeAction_UpliftSynchronizesQualityAndRarityAliases()
    {
        var shiningRoot = CreateForgeReadyShiningRoot();
        var soulRoot = CreateForgeSoulRoot(new JsonObject
        {
            ["relicId"] = "relic_old",
            ["name"] = "Старый Клинок",
            ["rarity"] = "common",
            ["quality"] = "rare",
            ["formTag"] = "blade",
            ["properties"] = new JsonArray
            {
                new JsonObject { ["propertyId"] = "edge", ["band"] = "rare" },
                new JsonObject { ["propertyId"] = "guard", ["band"] = "rare" },
                new JsonObject { ["propertyId"] = "memory", ["band"] = "rare" }
            }
        });
        var addedProperties = new JsonArray
        {
            new JsonObject
            {
                ["propertyId"] = "radiance",
                ["band"] = "epic"
            }
        };

        var success = ShiningAbodeState.TryApplyForgeAction(
            shiningRoot,
            soulRoot,
            residentRoot: null,
            ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity,
            "faction_old",
            "relic_old",
            null,
            -1,
            null,
            addedProperties,
            currentTurnNumber: 22,
            resolvedAtUtc: "2026-04-22T10:00:00Z",
            out _,
            out var error);

        var relic = soulRoot["soulRelics"]!["stored"]!.AsArray()[0]!.AsObject();
        Assert.True(success, error);
        Assert.Equal("epic", relic["quality"]!.GetValue<string>());
        Assert.Equal("epic", relic["rarity"]!.GetValue<string>());
        Assert.Equal(4, relic["properties"]!.AsArray().Count);
    }

    private static JsonObject CreateForgeReadyShiningRoot()
    {
        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["availability"] = ShiningAbodeState.AvailabilityActive;
        shiningRoot["radiance"] = new JsonObject
        {
            ["experience"] = 500,
            ["tier"] = 4
        };
        shiningRoot["lightSparks"] = 100;
        shiningRoot["factions"] = new JsonArray
        {
            new JsonObject
            {
                ["factionId"] = "faction_old",
                ["originType"] = ShiningAbodeState.OriginTypeAscendedGuardian,
                ["hallId"] = "hall_old",
                ["charter"] = new JsonObject
                {
                    ["factionName"] = "Старый Дом",
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
                    ["summary"] = "Кузня памяти."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
                    ["headActorId"] = "guardian_old",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["baseStrength"] = 35,
                ["factionStrength"] = 62,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["projectId"] = "project_refinement",
                        ["displayName"] = "Кузня Отголосков",
                        ["summary"] = "Держит refinement контур.",
                        ["toneTags"] = new JsonArray("relic"),
                        ["targetFactionIds"] = new JsonArray(),
                        ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
                        ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
                        ["tier"] = 2,
                        ["status"] = ShiningAbodeState.ProjectStatusCompleted,
                        ["isSupported"] = true,
                        ["strengthReward"] = 12
                    }
                },
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            }
        };
        CompleteOperationalFaction(
            shiningRoot["factions"]!.AsArray()[0]!.AsObject(),
            hasResidentAffiliations: false);

        return shiningRoot;
    }

    private static void CompleteOperationalFaction(
        JsonObject faction,
        bool hasResidentAffiliations)
    {
        faction["visibility"] = "revealed";
        faction["factionLifecycle"] = new JsonObject
        {
            ["state"] = ShiningAbodeState.FactionLifecycleStateActive
        };
        ShiningFactionTestMaterialization.Apply(
            faction,
            materializedAtTurn: 1,
            hasResidentAffiliations,
            canTrade: false);
    }

    private static async Task CommitFreshResourceBootstrapAsync(
        FileSystemManager fs,
        CanonicalResourceFreshBootstrapPlan plan)
    {
        Assert.True(plan.IsValid, string.Join(Environment.NewLine, plan.Issues));
        var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>
        {
            new(
                ResourceMaterializationContract.DefinitionsPath,
                plan.BeforeImages[ResourceMaterializationContract.DefinitionsPath],
                plan.Definitions.ToCanonicalJson(),
                RequireCurrentBaseline: true),
            new(
                ResourceMaterializationContract.StatePath,
                plan.BeforeImages[ResourceMaterializationContract.StatePath],
                plan.StateAfterImage!.ToCanonicalJson(),
                RequireCurrentBaseline: true),
            new(
                ResourceMaterializationContract.HistoryPath,
                plan.BeforeImages[ResourceMaterializationContract.HistoryPath],
                plan.HistoryAfterImage!.ToCanonicalJson(),
                RequireCurrentBaseline: true)
        };
        foreach (var (path, afterImage) in plan.OwnerAfterImages)
        {
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                path,
                plan.BeforeImages[path],
                afterImage.ToJsonString(),
                RequireCurrentBaseline: true));
        }

        CanonicalResourceQuartetTransaction.AddAuthorityWriteAndGlobalGuards(
            writes,
            plan.QuartetProjection!);
        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(
            fs,
            writes.ToArray()));
        var exactAuthority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            plan.Definitions,
            fs.ReadFileAsync,
            plan.StateAfterImage!,
            plan.HistoryAfterImage!,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        Assert.True(
            exactAuthority.IsValid,
            string.Join(Environment.NewLine, exactAuthority.Issues));
    }

    private static JsonObject CreateForgeSoulRoot(JsonObject relic) => new()
    {
        ["currentRealm"] = "Shining Abode",
        ["inkFeathers"] = new JsonObject
        {
            ["current"] = 100
        },
        ["soulRelics"] = new JsonObject
        {
            ["equipped"] = new JsonArray(),
            ["stored"] = new JsonArray(relic)
        }
    };
}
