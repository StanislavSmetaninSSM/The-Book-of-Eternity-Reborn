using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundDiagnosisMethodContractTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 30)]
    public void ExplicitMortalMethodRetainsProcedureDurationAndCostWithoutDefaulting(bool consumesItem, int durationMinutes)
    {
        var original = WoundContractTestData.CreateActiveWound();
        var originalJson = original.ToJsonString();
        var baseline = WoundMaterializationContract.Parse(originalJson, "original");
        Assert.True(baseline.IsValid, JsonSerializer.Serialize(baseline.Issues));
        Assert.NotNull(baseline.Wound);
        Assert.Equal("mortal_world", baseline.Wound.Owner.Realm);
        Assert.Equal("physical", baseline.Wound.Classification.Domain);
        Assert.Empty(baseline.Wound.Treatment.DiagnosisPaths);
        var baselineCanonical = WoundMaterializationContract.SerializeCanonical(baseline.Wound);
        var reparsed = WoundMaterializationContract.Parse(baselineCanonical, "original.canonical");
        Assert.True(reparsed.IsValid, JsonSerializer.Serialize(reparsed.Issues));
        Assert.Equal(baselineCanonical, WoundMaterializationContract.SerializeCanonical(reparsed.Wound!));

        // This ordinary route is only the existing structural parameter oracle.
        // No route executes and no accepted state, claim or check authority is minted.
        var ordinary = Assert.IsType<JsonObject>(Assert.Single(original["treatment"]!["routes"]!.AsArray())).DeepClone().AsObject();
        if (!consumesItem)
        {
            ordinary["requirements"]!.AsArray().RemoveAt(0);
            ordinary["resolution"]!["modifierSource"]!["requirementIndex"] = 0;
            ordinary["resourcePolicy"]!["mutations"] = new JsonArray();
        }
        ordinary["resourcePolicy"]!["consumeOn"] = new JsonArray("success", "failed_attempt");
        ordinary["outcomes"] = new JsonArray(
            new JsonObject { ["bandId"] = "diagnostic_success", ["minimumMargin"] = 0, ["maximumMargin"] = null,
                ["category"] = "success", ["result"] = new JsonArray(new JsonObject { ["kind"] = "stabilize" }) },
            new JsonObject { ["bandId"] = "diagnostic_failure", ["minimumMargin"] = null, ["maximumMargin"] = -1,
                ["category"] = "failed_attempt", ["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" }) });
        var ordinaryJson = ordinary.ToJsonString();
        var parameterResult = MortalWoundTreatmentContract.ParseRouteShape(JsonSerializer.SerializeToElement(ordinary), "ordinary.parameters");
        Assert.True(parameterResult.IsValid, JsonSerializer.Serialize(parameterResult.Issues));
        var parameters = Assert.IsType<MortalWoundProcedureRouteDefinition>(parameterResult.Route);
        Assert.Equal("mortal_wound_procedure_v1", parameters.Resolution.FormulaKey);
        Assert.Equal(15, parameters.Resolution.Difficulty);
        Assert.Equal("accepted_d20", parameters.Resolution.RollSource);
        Assert.Equal("natural_20_first_natural_1_last", parameters.Resolution.CriticalPolicy);
        Assert.Equal(consumesItem ? 1 : 0,
            Assert.IsType<MortalWoundResolvedSkillTierModifierSource>(parameters.Resolution.ModifierSource).RequirementIndex);
        Assert.Equal(new[] { "success", "failed_attempt" }, parameters.Bands.Select(band => band.Category));
        Assert.Equal(0L, parameters.Bands[0].MinimumMargin); Assert.Null(parameters.Bands[0].MaximumMargin);
        Assert.Null(parameters.Bands[1].MinimumMargin); Assert.Equal(-1L, parameters.Bands[1].MaximumMargin);
        Assert.Equal(consumesItem ? 1 : 0, parameters.ResourcePolicy.Mutations.Length);
        Assert.Equal(new[] { "cancelled", "validation_failed", "rolled_back" }, parameters.ResourcePolicy.RefundOn);
        Assert.True(JsonNode.DeepEquals(ordinary, JsonNode.Parse(Write(writer =>
            MortalWoundTreatmentContract.WriteRouteCanonical(writer, parameters)))));

        var bands = new JsonArray();
        foreach (var row in ordinary["outcomes"]!.AsArray().OfType<JsonObject>())
        {
            var band = row.DeepClone().AsObject();
            Assert.True(band.Remove("result"));
            bands.Add(band);
        }
        var check = new JsonObject
        {
            ["schemaVersion"] = 1, ["kind"] = "mortal_procedure_diagnosis",
            ["resolution"] = ordinary["resolution"]!.DeepClone(), ["outcomes"] = bands,
            ["durationMinutes"] = durationMinutes, ["resourcePolicy"] = ordinary["resourcePolicy"]!.DeepClone()
        };
        var method = new JsonObject
        {
            ["diagnosisPathId"] = "inspect_explicit_method", ["displayName"] = "Осмотреть края раны",
            ["visibility"] = "public", ["requiresKnownFacts"] = new JsonArray("route:clean_and_suture"),
            ["requirements"] = ordinary["requirements"]!.DeepClone(), ["check"] = check,
            ["reveals"] = new JsonArray("route:clean_and_suture"), ["failurePolicy"] = "no_reveal"
        };
        Assert.Contains("clean_and_suture", baseline.Wound.Treatment.KnownRouteIds);
        Assert.Equal(durationMinutes, check["durationMinutes"]!.GetValue<int>());
        Assert.Equal(consumesItem ? 1 : 0, check["resourcePolicy"]!["mutations"]!.AsArray().Count);
        Assert.All(bands.OfType<JsonObject>(), band => Assert.False(band.ContainsKey("result")));
        var methodJson = method.ToJsonString();
        var commonIssues = new List<ValidationIssue>();
        var common = WoundMaterializationContract.ReadDiagnosisPath(JsonSerializer.SerializeToElement(method), "method", commonIssues);
        Assert.Empty(commonIssues); Assert.NotNull(common);
        Assert.Equal("inspect_explicit_method", common.DiagnosisPathId);
        Assert.True(JsonNode.DeepEquals(check, JsonNode.Parse(common.Check.GetRawText())));
        Assert.Equal(new[] { "route:clean_and_suture" }, common.Reveals);
        Assert.Equal(parameters.Requirements.Length, common.Requirements.Count);

        var parsed = MortalWoundTreatmentContract.ParseDiagnosisPathShape(JsonSerializer.SerializeToElement(method), "method");
        Assert.Equal(originalJson, original.ToJsonString());
        Assert.Equal(ordinaryJson, ordinary.ToJsonString());
        Assert.Equal(methodJson, method.ToJsonString());
        Assert.Equal(baselineCanonical, WoundMaterializationContract.SerializeCanonical(baseline.Wound));
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); // Intended RED: current Mortal validator requires empty check.
        var definition = Assert.IsType<MortalWoundDiagnosisPathDefinition>(parsed.DiagnosisPath);
        var canonical = Write(writer => MortalWoundTreatmentContract.WriteDiagnosisPathCanonical(writer, definition));
        Assert.True(JsonNode.DeepEquals(method, JsonNode.Parse(canonical)));
        var roundTrip = MortalWoundTreatmentContract.ParseDiagnosisPathShape(JsonSerializer.SerializeToElement(JsonNode.Parse(canonical)), "roundTrip");
        Assert.True(roundTrip.IsValid, JsonSerializer.Serialize(roundTrip.Issues));
        Assert.Equal(canonical, Write(writer => MortalWoundTreatmentContract.WriteDiagnosisPathCanonical(writer,
            Assert.IsType<MortalWoundDiagnosisPathDefinition>(roundTrip.DiagnosisPath))));
    }

    private static string Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) write(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
