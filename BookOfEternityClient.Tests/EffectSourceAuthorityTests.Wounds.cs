using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectSourceAuthorityWoundTests
{
    [Fact]
    public void Build_AcceptsExactSourceBoundWoundConsequence()
    {
        var definition = CreateSourceBoundWoundDefinition();
        var authority = Build(
            Source("wound", WoundId, definition));

        var result = authority.Resolve(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                WoundId,
                EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject());

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, authority.Issues.Select(static issue =>
                $"{issue.Code}: {issue.FilePath}")));
        Assert.DoesNotContain(authority.Issues, issue =>
            issue.Code?.StartsWith("effect_source_wound_", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("case")]
    [InlineData("confusable")]
    [InlineData("stale")]
    public void Build_RejectsMissingOrInexactWoundConsequenceLinkWithoutRetargeting(
        string mutation)
    {
        var definition = CreateSourceBoundWoundDefinition();
        switch (mutation)
        {
            case "missing":
                definition["links"] = new JsonArray();
                break;
            case "case":
                definition["links"]![0]!["targetId"] = "WOUND_TEST_TORN_SIDE";
                break;
            case "confusable":
                definition["links"]![0]!["targetId"] = "wound_teѕt_torn_side";
                break;
            case "stale":
                definition["components"]![0]!["payload"]!["woundId"] = "wound_retired";
                definition["links"]![0]!["targetId"] = "wound_retired";
                break;
            default:
                throw new InvalidOperationException(mutation);
        }

        var authority = Build(
            new[]
            {
                Source("wound", WoundId, definition),
                Source(
                    "wound",
                    "wound_unrelated_available",
                    definitions: new JsonArray())
            },
            historicalSourceIds: new HashSet<string>(StringComparer.Ordinal)
            {
                "wound_retired"
            });

        Assert.Contains(authority.Issues, issue =>
            issue.Code is "effect_source_wound_link_missing" or
                "effect_source_wound_link_mismatch" or
                "effect_source_wound_payload_mismatch");
        var result = authority.Resolve(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                WoundId,
                EffectMaterializationTestFixture.DefinitionKey),
            "player",
            new JsonObject());
        Assert.False(result.Success);
        Assert.Null(result.Source);
    }

    [Fact]
    public void Build_RejectsWoundConsequencePayloadThatDoesNotMatchItsExactSourceLink()
    {
        var definition = CreateSourceBoundWoundDefinition();
        definition["components"]![0]!["payload"]!["woundId"] =
            "wound_other_exact";
        var authority = Build(
            Source("wound", WoundId, definition),
            Source(
                "wound",
                "wound_other_exact",
                definitions: new JsonArray()));

        Assert.Contains(authority.Issues, issue =>
            issue.Code == "effect_source_wound_payload_mismatch");
    }

    [Fact]
    public void Build_RejectsSourceBoundWoundConsequenceBoundToNonWoundSource()
    {
        var definition = CreateSourceBoundWoundDefinition();
        var authority = Build(
            Source("skill", "skill_wrong_wound_owner", definition),
            Source("wound", WoundId, definitions: new JsonArray()));

        Assert.Contains(authority.Issues, issue =>
            issue.Code == "effect_source_wound_source_bound_mismatch");
    }

    private const string WoundId = "wound_test_torn_side";

    private static JsonObject CreateSourceBoundWoundDefinition()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "wound_consequence");
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "expire"
        };
        return definition;
    }

    private static EffectSourceAuthority Build(
        params EffectSourceExport[] exports) =>
        Build(exports, new HashSet<string>(StringComparer.Ordinal));

    private static EffectSourceAuthority Build(
        IReadOnlyList<EffectSourceExport> exports,
        IReadOnlySet<string> historicalSourceIds) =>
        EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            exports,
            Array.Empty<EffectSourceExport>(),
            historicalSourceIds));

    private static EffectSourceExport Source(
        string kind,
        string sourceId,
        JsonObject? definition = null,
        JsonArray? definitions = null) =>
        new(
            "mortal_world",
            kind,
            sourceId,
            definitions ?? new JsonArray(
                definition ?? EffectMaterializationTestFixture.CreateDefinition()),
            Materializable: true,
            Active: true,
            SameTurn: false,
            SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal)
            {
                "active"
            });
}
