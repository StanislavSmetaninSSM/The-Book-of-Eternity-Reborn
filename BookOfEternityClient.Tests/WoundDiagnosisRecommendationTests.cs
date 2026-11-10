using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundDiagnosisRecommendationTests
{
    // Losing the diagnostic requirements while retaining its name must fail both consumers.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KnownPhysicalDiagnosisShowsDeclaredConditionsWithoutExecution(bool status)
    {
        var node = WoundContractTestData.CreateActiveWound();
        node["treatment"]!["diagnosisPaths"] = new JsonArray(Path("public", "Осторожный осмотр"));
        var input = WoundPlayerProjectionTestData.Input(node);
        var before = input.Carriers.PlayerWounds!.ToJsonString();
        var projection = WoundPlayerProjection.Build(input);
        Assert.True(projection.IsAvailable);
        var command = ExplorerWoundCommandResultBuilder.Build("/раны", projection, null, false);
        var text = Text(status ? WoundPlayerStatusBlocks.Build(projection) : command.Blocks);
        Assert.Contains("Осторожный осмотр", text);
        Assert.Contains("17 ед.", text);
        Assert.Contains("не ниже 3 ступени", text);
        Assert.Contains("не подтверждают наличие", text);
        Assert.Contains("/лечить", text); Assert.Contains("длительности и расходам", text);
        Assert.Equal(CommandExecutionState.Completed, command.State);
        Assert.Empty(command.Prompts);
        Assert.Equal(before, input.Carriers.PlayerWounds.ToJsonString());
        Assert.DoesNotContain("diagnosis_private", text);
        Assert.DoesNotContain("field_medicine", text);
    }

    // Future closure must not make a private path's name or requirements current knowledge.
    [Theory]
    [InlineData("hidden", false)]
    [InlineData("hidden", true)]
    [InlineData("gm_only", false)]
    [InlineData("gm_only", true)]
    public void DiagnosisConditionsUseCurrentKnowledgeWithoutGrantingGmOnlyAccess(string visibility, bool known)
    {
        var node = WoundContractTestData.CreateActiveWound();
        node["treatment"]!["routes"]![0]!["visibility"] = "hidden";
        node["treatment"]!["knownRouteIds"] = known ? new JsonArray("clean_and_suture") : new JsonArray();
        var first = Path("public", "Первичный осмотр");
        var later = Path(visibility, "СЕКРЕТНЫЙ_ОСМОТР", "later");
        later["requiresKnownFacts"] = new JsonArray("route:clean_and_suture");
        later["requirements"]![0]!["quantity"] = 987;
        node["treatment"]!["diagnosisPaths"] = new JsonArray(first, later);
        var projection = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.Input(node));
        Assert.True(projection.IsAvailable);
        var text = Text(ExplorerWoundCommandResultBuilder.Build("/раны", projection, null, false).Blocks)
            + Text(WoundPlayerStatusBlocks.Build(projection));
        Assert.Contains("Первичный осмотр", text);
        Assert.Equal(known && visibility == "hidden", text.Contains("СЕКРЕТНЫЙ_ОСМОТР", StringComparison.Ordinal));
        Assert.Equal(known && visibility == "hidden", text.Contains("987", StringComparison.Ordinal));
        Assert.DoesNotContain("clean_and_suture", text);
    }

    // Opaque spiritual requirements are not a registered readable codec.
    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void SpiritualDiagnosisDoesNotPrintOpaquePrivateRequirements(string realm)
    {
        var node = WoundContractTestData.CreateSpiritualActiveWound(realm: realm,
            ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        node["treatment"]!["diagnosisPaths"] = new JsonArray(new JsonObject
        {
            ["diagnosisPathId"] = "diagnosis_private", ["displayName"] = "Оценить духовную ткань", ["visibility"] = "public",
            ["requiresKnownFacts"] = new JsonArray(),
            ["requirements"] = new JsonArray(new JsonObject
            {
                ["kind"] = "source_capability", ["capabilityRef"] = "private_probe", ["actorRole"] = "provider",
                ["privateCondition"] = "СЕКРЕТНОЕ_УСЛОВИЕ"
            }),
            ["check"] = new JsonObject(), ["reveals"] = new JsonArray(), ["failurePolicy"] = "no_reveal"
        });
        var projection = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.InputInRealm(realm, node));
        Assert.True(projection.IsAvailable);
        var text = Text(ExplorerWoundCommandResultBuilder.Build("/раны", projection, null, false).Blocks)
            + Text(WoundPlayerStatusBlocks.Build(projection));
        Assert.Contains("Оценить духовную ткань", text);
        Assert.DoesNotContain("СЕКРЕТНОЕ_УСЛОВИЕ", text);
        Assert.DoesNotContain("privateCondition", text);
        Assert.DoesNotContain("diagnosis_private", text);
    }

    private static JsonObject Path(string visibility, string name, string id = "diagnosis_private") => new()
    {
        ["diagnosisPathId"] = id, ["displayName"] = name, ["visibility"] = visibility,
        ["requiresKnownFacts"] = new JsonArray(),
        ["requirements"] = new JsonArray(
            new JsonObject { ["kind"] = "resource_quantity", ["resourceRef"] = "energy", ["quantity"] = 17, ["ownerRole"] = "target" },
            new JsonObject { ["kind"] = "skill_tier", ["capabilityRef"] = "field_medicine", ["minimumTier"] = 3, ["actorRole"] = "provider" }),
        ["check"] = WoundContractTestData.CreateMortalDiagnosisCheck(0), ["reveals"] = new JsonArray("route:clean_and_suture"), ["failurePolicy"] = "no_reveal"
    };

    private static string Text<T>(T value) => JsonSerializer.Serialize(value,
        new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
