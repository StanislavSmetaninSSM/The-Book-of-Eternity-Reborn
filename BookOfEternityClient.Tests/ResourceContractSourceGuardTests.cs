using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceContractSourceGuardTests
{
    [Fact]
    public void ShiningBlessingPersistence_DoesNotReviveNumericRerollMirrors()
    {
        var effectSource = ReadProductionSource(
            "Services",
            "ShiningBlessingEffectState.cs");
        var transientOnly = new[]
        {
            "private static JsonObject BuildPendingEffectsFromPreparedPackage",
            "private static IReadOnlyList<string> BuildActivationSummaryLines",
            "private static List<string> BuildDirectiveLines"
        };
        foreach (var signature in transientOnly)
            effectSource = effectSource.Replace(
                ExtractMethodSource(effectSource, signature),
                string.Empty,
                StringComparison.Ordinal);

        Assert.DoesNotContain("[\"rerolls\"]", effectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"rerollsSpent\"]", effectSource, StringComparison.Ordinal);

        var resourceSource = ReadProductionSource(
            "Services",
            "ShiningBlessingRerollResourceService.cs");
        Assert.Contains("StripNumericMirror(memory);", resourceSource, StringComparison.Ordinal);
        Assert.Contains("StripNumericMirror(relic);", resourceSource, StringComparison.Ordinal);
        Assert.Contains("rerollResourceBinding", resourceSource, StringComparison.Ordinal);

        var validatorSource = ReadProductionSource(
            "Services",
            "Validation",
            "ValidationService.AfterlifeArchiveTradeAndLifecycle.cs");
        Assert.Contains(
            "pending_shining_blessings_numeric_reroll_mirror_forbidden",
            validatorSource,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("money")]
    [InlineData("ink_feathers")]
    [InlineData("light_sparks")]
    [InlineData("treasury_balance")]
    [InlineData("faction_resource_ledger")]
    [InlineData("experience")]
    [InlineData("mastery")]
    [InlineData("level")]
    [InlineData("reputation")]
    [InlineData("relationship")]
    [InlineData("owner_bond_level")]
    [InlineData("spiritual_power")]
    [InlineData("spiritual_strain")]
    [InlineData("spiritual_shield")]
    public void DefinitionAdmission_RejectsReservedOutOfScopeMechanicalFamilies(
        string resourceKey)
    {
        var proposal = CreateSettingResourceProposal(resourceKey);
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = ResourceDefinitionCatalog.MaterializeProposal(
            document.RootElement,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            createdAtTurn: 42,
            createdEventRef: "turn_42",
            allocateIdentity: static () => new ResourceDefinitionIdentity(
                "resource_definition_out_of_scope",
                "resource_definition_seal_out_of_scope"));

        Assert.Null(result.Definition);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_out_of_scope_key" &&
            issue.FilePath.EndsWith(".resourceKey", StringComparison.Ordinal));
    }

    [Fact]
    public void DefinitionAdmission_RetainsExplicitSettingDefinedReserveRoute()
    {
        var proposal = CreateSettingResourceProposal("mana");
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = ResourceDefinitionCatalog.MaterializeProposal(
            document.RootElement,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            createdAtTurn: 42,
            createdEventRef: "turn_42",
            allocateIdentity: static () => new ResourceDefinitionIdentity(
                "resource_definition_mana_guard",
                "resource_definition_seal_mana_guard"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal("mana", result.Definition!.ResourceKey);
    }

    private static JsonObject CreateSettingResourceProposal(string resourceKey) => new()
    {
        ["resourceKey"] = resourceKey,
        ["definitionVersion"] = 1,
        ["displayName"] = resourceKey,
        ["numericKind"] = "integer",
        ["unit"] = "point",
        ["quantum"] = 1,
        ["minimumPolicy"] = new JsonObject
        {
            ["kind"] = "definition_fixed",
            ["value"] = 0
        },
        ["capacityPolicy"] = new JsonObject
        {
            ["kind"] = "instance_fixed"
        },
        ["initializationPolicy"] = new JsonObject
        {
            ["kind"] = "maximum"
        },
        ["allowedOwnerKinds"] = new JsonArray("player"),
        ["allowedOperations"] = new JsonArray("spend", "gain"),
        ["defaultFloorPolicy"] = "reject_below_minimum",
        ["defaultCapPolicy"] = "clamp_to_maximum",
        ["visibility"] = "player_visible"
    };

    private static string ReadProductionSource(params string[] relativeParts)
    {
        var parts = new[] { TestRepoPaths.RepoRoot, "BookOfEternityClient" }
            .Concat(relativeParts)
            .ToArray();
        return File.ReadAllText(Path.Combine(parts));
    }

    private static string ExtractMethodSource(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find method signature '{signature}'.");
        var openBrace = source.IndexOf('{', start);
        Assert.True(openBrace >= 0, $"Could not find method body for '{signature}'.");

        var depth = 0;
        for (var index = openBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}')
                depth--;

            if (depth == 0)
                return source[start..(index + 1)];
        }

        Assert.Fail($"Could not extract method body for '{signature}'.");
        return string.Empty;
    }
}
