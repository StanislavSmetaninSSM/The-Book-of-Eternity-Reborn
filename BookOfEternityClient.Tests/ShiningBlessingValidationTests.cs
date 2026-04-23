using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningBlessingValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ShiningBlessingValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-shining-blessing-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public void ValidatePendingShiningBlessingEffects_ConsumedRouteWithoutConsumedRouteSeedIds_Fails()
    {
        var root = CreateBaseSoulRoot();
        root[ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
        {
            ["applicationState"] = "active",
            ["materializedAtUtc"] = "2026-04-17T00:00:00Z",
            ["sourcePackagePreparedAtTurn"] = 42,
            ["currentIncarnation"] = 5,
            ["sourceCardIds"] = new JsonArray("card_route"),
            ["pendingRouteEffects"] = new JsonArray
            {
                new JsonObject
                {
                    ["effectId"] = "card_route",
                    ["sourceCardId"] = "card_route",
                    ["routeOptions"] = 1,
                    ["latestTurn"] = 6,
                    ["status"] = ShiningBlessingEffectState.GenericStatusConsumed,
                    ["consumedAtTurn"] = 5,
                    ["consumedAtUtc"] = "2026-04-17T00:05:00Z",
                    ["consumedEventIds"] = new JsonArray("evt_route_alpha")
                }
            }
        };

        var issues = InvokeValidation(root);

        Assert.Contains(issues, issue => issue.FilePath.Contains("consumedRouteSeedIds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePendingShiningBlessingEffects_ConsumedSocialWithoutTarget_Fails()
    {
        var root = CreateBaseSoulRoot();
        root[ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
        {
            ["applicationState"] = "active",
            ["materializedAtUtc"] = "2026-04-17T00:00:00Z",
            ["sourcePackagePreparedAtTurn"] = 42,
            ["currentIncarnation"] = 5,
            ["sourceCardIds"] = new JsonArray("card_social"),
            ["pendingSocialEffects"] = new JsonArray
            {
                new JsonObject
                {
                    ["effectId"] = "card_social",
                    ["sourceCardId"] = "card_social",
                    ["delta"] = 15,
                    ["status"] = ShiningBlessingEffectState.GenericStatusConsumed,
                    ["consumedAtTurn"] = 5,
                    ["consumedAtUtc"] = "2026-04-17T00:05:00Z"
                }
            }
        };

        var issues = InvokeValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "pending_shining_blessings_social_missing_consumed_target", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePendingShiningBlessingEffects_PendingRelicEntitlementWithoutAllowance_Fails()
    {
        var root = CreateBaseSoulRoot();
        root[ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
        {
            ["applicationState"] = "active",
            ["materializedAtUtc"] = "2026-04-17T00:00:00Z",
            ["sourcePackagePreparedAtTurn"] = 42,
            ["currentIncarnation"] = 5,
            ["sourceCardIds"] = new JsonArray("card_relic"),
            ["relicRefinementEntitlements"] = new JsonObject
            {
                ["rerolls"] = 0,
                ["freeShape"] = false,
                ["freeRetune"] = false,
                ["status"] = ShiningBlessingEffectState.RelicStatusPendingEntitlement,
                ["sourceCardIds"] = new JsonArray("card_relic")
            }
        };

        var issues = InvokeValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "pending_shining_blessings_empty_relic_entitlement", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePendingShiningBlessingEffects_ExpiredSocialStatus_Fails()
    {
        var root = CreateBaseSoulRoot();
        root[ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
        {
            ["applicationState"] = "active",
            ["materializedAtUtc"] = "2026-04-17T00:00:00Z",
            ["sourcePackagePreparedAtTurn"] = 42,
            ["currentIncarnation"] = 5,
            ["sourceCardIds"] = new JsonArray("card_social"),
            ["pendingSocialEffects"] = new JsonArray
            {
                new JsonObject
                {
                    ["effectId"] = "card_social",
                    ["sourceCardId"] = "card_social",
                    ["delta"] = 15,
                    ["status"] = ShiningBlessingEffectState.GenericStatusExpired,
                    ["expiredAtTurn"] = 7,
                    ["expiredAtUtc"] = "2026-04-17T00:07:00Z"
                }
            }
        };

        var issues = InvokeValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "pending_shining_blessings_invalid_effect_status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePendingShiningBlessingEffects_SourceCardCountMismatch_Fails()
    {
        var root = CreateBaseSoulRoot();
        root[ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
        {
            ["applicationState"] = "active",
            ["materializedAtUtc"] = "2026-04-17T00:00:00Z",
            ["sourcePackagePreparedAtTurn"] = 42,
            ["currentIncarnation"] = 5,
            ["sourceCardIds"] = new JsonArray("card_a", "card_b"),
            ["sourceCardCount"] = 1
        };

        var issues = InvokeValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "pending_shining_blessings_source_card_count_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePendingShiningBlessingEffects_ConsumedMemoryRerollsSpentBeyondGrant_Fails()
    {
        var root = CreateBaseSoulRoot();
        root[ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
        {
            ["applicationState"] = "active",
            ["materializedAtUtc"] = "2026-04-17T00:00:00Z",
            ["sourcePackagePreparedAtTurn"] = 42,
            ["currentIncarnation"] = 5,
            ["sourceCardIds"] = new JsonArray("card_memory"),
            ["sourceCardCount"] = 1,
            ["memorySelection"] = new JsonObject
            {
                ["options"] = 1,
                ["rerolls"] = 1,
                ["rerollsSpent"] = 2,
                ["status"] = ShiningBlessingEffectState.GenericStatusConsumed,
                ["consumedAtTurn"] = 5,
                ["consumedAtUtc"] = "2026-04-17T00:05:00Z",
                ["sourceCardIds"] = new JsonArray("card_memory")
            }
        };

        var issues = InvokeValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "pending_shining_blessings_memory_rerolls_spent_exceeds_grant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePendingShiningBlessingEffects_DuplicateConsumedRouteSeedIds_Fails()
    {
        var root = CreateBaseSoulRoot();
        root[ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
        {
            ["applicationState"] = "active",
            ["materializedAtUtc"] = "2026-04-17T00:00:00Z",
            ["sourcePackagePreparedAtTurn"] = 42,
            ["currentIncarnation"] = 5,
            ["sourceCardIds"] = new JsonArray("card_route"),
            ["sourceCardCount"] = 1,
            ["pendingRouteEffects"] = new JsonArray
            {
                new JsonObject
                {
                    ["effectId"] = "card_route",
                    ["sourceCardId"] = "card_route",
                    ["routeOptions"] = 1,
                    ["latestTurn"] = 6,
                    ["status"] = ShiningBlessingEffectState.GenericStatusConsumed,
                    ["consumedAtTurn"] = 5,
                    ["consumedAtUtc"] = "2026-04-17T00:05:00Z",
                    ["consumedEventIds"] = new JsonArray("evt_route_alpha"),
                    ["consumedRouteSeedIds"] = new JsonArray("route_seed_alpha", "route_seed_alpha")
                }
            }
        };

        var issues = InvokeValidation(root);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "pending_shining_blessings_duplicate_consumed_array_item", StringComparison.OrdinalIgnoreCase));
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
        }
    }

    private List<ValidationIssue> InvokeValidation(JsonObject root)
    {
        var issues = new List<ValidationIssue>();
        using var doc = JsonDocument.Parse(root.ToJsonString());
        var method = typeof(ValidationService).GetMethod(
            "ValidatePendingShiningBlessingEffects",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(_validator, new object[] { doc.RootElement, "game_state/meta/soul_state.json", issues });
        return issues;
    }

    private static JsonObject CreateBaseSoulRoot() =>
        new()
        {
            ["soulName"] = "Soul",
            ["currentRealm"] = "Mortal World",
            ["currentIncarnation"] = 5,
            ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
            ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() }
        };
}
