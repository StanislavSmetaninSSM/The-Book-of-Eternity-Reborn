using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class ShiningAbodeTradeAndForgeStateTests
{
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

}
