using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundAlternativeTreatmentRepairTests
{
    [Theory]
    [InlineData("route", "outcomes[0].result[0]", true)]
    [InlineData("route", "interruption.result[0]", true)]
    [InlineData("diagnosisPath", "outcomes[0].result[0]", false)]
    [InlineData("diagnosisPath", "interruption.result[0]", false)]
    public void PersistedTransport_SourceParameterDiagnosticsRequireRouteRoot(string root, string contour, bool expected)
    {
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateInvalidModeCandidate().Request));
        var json = packet.ToJsonObject();
        var path = root + "." + contour + ".legacies[0].effectDraft.applications[0].parameters.amount";
        json["preservedProposal"]!["route"]!["mode"] = "procedure";
        json["issues"]![0]!["path"] = path;
        json["issues"]![0]!["code"] = "effect_source_parameter_required";
        json["issues"]![0]!["expected"] = "supply the required parameter declared by its local source definition";
        var author = json["requiredResponseShape"]!["woundTreatmentAuthorings"]![0]!;
        author["route"]!["correctOnly"] = root == "route" ? new JsonArray(path) : new JsonArray();
        author["diagnosisPath"]!["correctOnly"] = root == "diagnosisPath" ? new JsonArray(path) : new JsonArray();
        Assert.Equal(expected, ValidWave(json, packet));
    }

    [Theory]
    [InlineData("recipe_missing")]
    [InlineData("recipe_extra")]
    [InlineData("recipe_wrong_base")]
    [InlineData("recipe_wrong_path")]
    [InlineData("recipe_null_path")]
    [InlineData("issue_wrong_code")]
    [InlineData("issue_wrong_expected")]
    [InlineData("issue_private_actual")]
    [InlineData("issue_private_path")]
    [InlineData("preserved_offender")]
    [InlineData("unrelated_null_hole")]
    [InlineData("absent_unoffending_root")]
    [InlineData("private_key")]
    [InlineData("private_value_marker")]
    [InlineData("identity")]
    public void PersistedTransport_RejectsExactClosedVariantMutations(string axis)
    {
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateInvalidModeCandidate().Request));
        var json = packet.ToJsonObject();
        var author = json["requiredResponseShape"]!["woundTreatmentAuthorings"]![0]!;
        var issue = json["issues"]![0]!;
        switch (axis)
        {
            case "recipe_missing": author["route"]!.AsObject().Remove("correctOnly"); break;
            case "recipe_extra": author["route"]!["extra"] = true; break;
            case "recipe_wrong_base": author["route"]!["base"] = "preservedProposal"; break;
            case "recipe_wrong_path": author["route"]!["correctOnly"] = new JsonArray("route.displayName"); break;
            case "recipe_null_path": author["diagnosisPath"] = null; break;
            case "issue_wrong_code": issue["code"] = "wound_response_invalid_field"; break;
            case "issue_wrong_expected": issue["expected"] = "rewrite everything"; break;
            case "issue_private_actual": issue["actual"] = "game_state/private.json"; break;
            case "issue_private_path": issue["path"] = "route.ownerId"; break;
            case "preserved_offender": json["preservedProposal"]!["route"]!["mode"] = "unsupported_mode"; break;
            case "unrelated_null_hole": json["preservedProposal"]!["route"]!["requirements"]![0] = null; break;
            case "absent_unoffending_root": json["preservedProposal"]!.AsObject().Remove("diagnosisPath"); break;
            case "private_key": json["preservedProposal"]!["route"]!["operationKey"] = "secret"; break;
            case "private_value_marker": json["safeContext"]!["event"] = "sha256:private"; break;
            case "identity": author["authoringRequestRef"] = "unsafe/ref"; break;
        }
        Assert.False(ValidWave(json, packet));
    }

    [Fact]
    public void PersistedTransport_RejectsRecursiveDuplicatesBeforeNodeNormalization()
    {
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateInvalidModeCandidate().Request));
        var json = packet.ToJsonObject().ToJsonString().Replace("\"routeId\":", "\"routeId\":\"duplicate\",\"routeId\":", StringComparison.Ordinal);
        using var document = JsonDocument.Parse("[" + json + "]");
        Assert.False(WoundRepairPacketBuilder.IsValidPersistedRepairWave(document.RootElement,
            JsonSerializer.SerializeToElement(new[] { packet.CreateReceipt() }, new JsonSerializerOptions
                { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), packet.SessionId, packet.RequestId, packet.SnapshotToken));
    }

    [Fact]
    public void PersistedTransport_AppendRuleKeepsOriginalFactsAndExactRecipe()
    {
        var original = CreateAuthor();
        original["diagnosisPath"]!["reveals"] = new JsonArray("route:old_fact");
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(Request(original)));
        Assert.True(ValidWave(packet.ToJsonObject(), packet));
        var wrong = packet.ToJsonObject();
        wrong["preservedProposal"]!["diagnosisPath"]!.AsObject().Remove("reveals");
        Assert.False(ValidWave(wrong, packet));
    }

    [Theory]
    [InlineData("object", false)]
    [InlineData("scalar", false)]
    [InlineData("array", false)]
    [InlineData("object", true)]
    public void Correction_VisibleRouteRejectsWholeNonNullPathEvenWhenPathIsMalformed(string shape, bool invalidMode)
    {
        var original = CreateAuthor();
        original["route"]!["visibility"] = "public";
        original["diagnosisPath"]!.AsObject().Remove("displayName");
        if (shape == "scalar") original["diagnosisPath"] = "bad path";
        if (shape == "array") original["diagnosisPath"] = new JsonArray("bad path");
        if (invalidMode) original["route"]!["mode"] = "unsupported_mode";
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(Request(original)));
        Assert.Contains(packet.Issues, issue => issue.Path == "diagnosisPath");
        Assert.DoesNotContain(packet.Issues, issue => issue.Path.StartsWith("diagnosisPath.", StringComparison.Ordinal));
        var corrected = original.DeepClone().AsObject();
        corrected["diagnosisPath"] = null;
        corrected["route"]!["mode"] = "procedure";
        Assert.True(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        corrected["route"]!["displayName"] = "unrelated rewrite";
        Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
    }
}
