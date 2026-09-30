using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks comparison-only cost permissions; signed ownership remains covered by integration tests.
/// </summary>
public sealed class SpiritualWoundDependentDraftPolicyTests
{
    /// <summary>
    /// Includes a stale chronological before value even when ordinary cost validation masks its diagnostic.
    /// </summary>
    [Fact]
    public void CostMismatch_IncludesOnlyProvenArithmeticClosure()
    {
        var baseline = JsonNode.Parse("""
            {"activeConflict":{"exchangeLog":[{"exchangeId":"next","actionCostAudit":{
             "opposition":{"operationType":"guard","baseCost":2,"minCost":1,"artTier":0,
              "effectiveCost":2,"before":6,"after":4}}}]}}
            """)!.AsObject();
        var policy = Policy(baseline, "afterlife_conflict_opposition_action_cost_mismatch", ".effectiveCost");
        Assert.Equal(new[] { "after", "before", "effectiveCost" },
            policy.Fields.Select(field => field.JsonPointer.Split('/')[^1]));
        var corrected = baseline.DeepClone().AsObject();
        var audit = corrected["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["opposition"]!;
        audit["effectiveCost"] = 3;
        audit["before"] = 3;
        audit["after"] = 0;
        Assert.True(policy.Allows(corrected));
        audit["baseCost"] = 3;
        Assert.False(policy.Allows(corrected));
    }

    /// <summary>
    /// Allows only the prescribed force audit and rejects unrelated or null structural substitutes.
    /// </summary>
    /// <param name="mutation">
    /// Valid addition or an invalid audit, sibling or container replacement.
    /// </param>
    [Theory]
    [InlineData("valid")]
    [InlineData("null_side")]
    [InlineData("null_root")]
    [InlineData("base")]
    [InlineData("extra")]
    [InlineData("other_side")]
    public void MissingForceAudit_PermitsOnlyPrescribedLeaves(string mutation)
    {
        var baseline = JsonNode.Parse("""
            {"activeConflict":{"exchangeLog":[{"exchangeId":"next","operationType":"force_incarnation"}]}}
            """)!.AsObject();
        var policy = Policy(baseline, "afterlife_conflict_wound_force_cost_audit_missing", "");
        Assert.True(policy.Allows(baseline));
        Assert.Equal(7, policy.Fields.Count);
        var candidate = baseline.DeepClone().AsObject();
        var exchange = candidate["activeConflict"]!["exchangeLog"]![0]!;
        exchange["actionCostAudit"] = JsonNode.Parse("""
            {"opposition":{"operationType":"force_incarnation","baseCost":0,"minCost":0,"artTier":0,
             "effectiveCost":1,"before":3,"after":2}}
            """);
        var root = exchange["actionCostAudit"]!;
        switch (mutation)
        {
            case "null_side": root["opposition"] = null; break;
            case "null_root": exchange["actionCostAudit"] = null; break;
            case "base": root["opposition"]!["baseCost"] = 1; break;
            case "extra": root["opposition"]!["unrelated"] = true; break;
            case "other_side": root["player"] = new JsonObject(); break;
        }
        Assert.Equal(mutation == "valid", policy.Allows(candidate));
    }

    /// <summary>
    /// Rejects duplicate keys before projection can ignore or collapse a raw carrier.
    /// </summary>
    [Fact]
    public void RawRoot_RejectsNestedDuplicateKeys()
    {
        Assert.Throws<JsonException>(() => SpiritualWoundDependentDraftPolicy.ReadStrictRoot(
            "{\"activeConflict\":{\"exchangeLog\":[{\"exchangeId\":\"a\",\"exchangeId\":\"b\"}]}}"));
    }

    /// <summary>
    /// Accepts one decoded leading UTF-8 marker while preserving the marker inside an ordinary JSON string.
    /// </summary>
    [Fact]
    public void Utf8Root_AcceptsOneLeadingPreambleWithoutChangingStringContent()
    {
        var root = SpiritualWoundDependentDraftPolicy.ReadStrictRoot(SpiritualWoundStateJson.DecodeUtf8JsonText(
            Encoding.UTF8.GetBytes("\ufeff{\"text\":\"\ufeffunchanged\"}")));
        Assert.Equal("\ufeffunchanged", root["text"]!.GetValue<string>());
    }

    /// <summary>
    /// Keeps malformed roots and duplicate-key rejection strict when a decoded leading marker is present.
    /// </summary>
    /// <param name="json">
    /// Raw text containing two leading markers or a duplicate property after a single marker.
    /// </param>
    [Theory]
    [InlineData("\ufeff\ufeff{}")]
    [InlineData("\ufeff{\"a\":1,\"a\":2}")]
    public void Utf8Root_PreambleDoesNotRelaxStrictJson(string json)
    {
        Assert.ThrowsAny<JsonException>(() => SpiritualWoundDependentDraftPolicy.ReadStrictRoot(
            SpiritualWoundStateJson.DecodeUtf8JsonText(Encoding.UTF8.GetBytes(json))));
    }

    /// <summary>
    /// Creates a comparison policy with an explicit three-point fixture frontier, without minting owner authority.
    /// </summary>
    /// <param name="baseline">
    /// Direct conflict root with the one affected opposition audit.
    /// </param>
    /// <param name="code">
    /// Existing correctable diagnostic code under test.
    /// </param>
    /// <param name="suffix">
    /// Diagnostic leaf suffix, or empty for an absent prescribed force audit.
    /// </param>
    /// <returns>
    /// Non-authoritative comparison policy for this isolated arithmetic fixture.
    /// </returns>
    private static SpiritualWoundDependentDraftPolicy Policy(JsonObject baseline, string code, string suffix)
    {
        var resource = new ResourceStateEntry(
            new("Chaos Sea", ResourceOwnerKind.AfterlifeConflictSide, "side", "spiritual_action_points"),
            3, 6, new(default, "fixture", "fixture"), ResourceLifecycleState.Active,
            new(1, "fixture", "fixture", "fixture", 1));
        return Assert.IsType<SpiritualWoundDependentDraftPolicy>(SpiritualWoundDependentDraftPolicy.Create(
            new JsonObject(), baseline,
            [new ValidationIssue("activeConflict.exchangeLog[0].actionCostAudit.opposition" + suffix,
                IssueSeverity.Error, "fixture", code: code)], new("fixture", resource, resource)));
    }
}
