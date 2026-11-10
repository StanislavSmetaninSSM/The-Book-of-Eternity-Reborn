using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundDiagnosisMethodGuardTests
{
    [Theory]
    [InlineData("empty")][InlineData("null")][InlineData("missing")][InlineData("extra")]
    [InlineData("version")][InlineData("kind")][InlineData("duration_missing")][InlineData("duration_negative")]
    [InlineData("duration_fraction")][InlineData("duration_overflow")][InlineData("policy_null")]
    [InlineData("policy_reason")][InlineData("policy_skill_cost")][InlineData("check_roll")]
    [InlineData("modifier_item")][InlineData("bands_partial")][InlineData("bands_order")]
    [InlineData("bands_gap")][InlineData("bands_duplicate")][InlineData("bands_operations")]
    public void CurrentMortalCheckRefusesIncompleteUnknownAndSemanticallyInvalidParameters(string fault)
    {
        var method = WoundContractTestData.CreateMortalDiagnosisPath(true, 30);
        var valid = MortalWoundTreatmentContract.ParseDiagnosisPathShape(JsonSerializer.SerializeToElement(method), "valid");
        Assert.True(valid.IsValid, JsonSerializer.Serialize(valid.Issues));
        var check = method["check"]!.AsObject();
        switch (fault)
        {
            case "empty": method["check"] = new JsonObject(); break;
            case "null": method["check"] = null; break;
            case "missing": method.Remove("check"); break;
            case "extra": check["unowned"] = true; break;
            case "version": check["schemaVersion"] = 99; break;
            case "kind": check["kind"] = "spiritual_guarantee"; break;
            case "duration_missing": check.Remove("durationMinutes"); break;
            case "duration_negative": check["durationMinutes"] = -1; break;
            case "duration_fraction": check["durationMinutes"] = 1.5m; break;
            case "duration_overflow": check["durationMinutes"] = 2147483648L; break;
            case "policy_null": check["resourcePolicy"] = null; break;
            case "policy_reason": check["resourcePolicy"]!["refundOn"] = new JsonArray("rolled_back"); break;
            case "policy_skill_cost": check["resourcePolicy"]!["mutations"]![0]!["requirementIndex"] = 1; break;
            case "check_roll": check["resolution"]!["rollSource"] = "gm_roll"; break;
            case "modifier_item": check["resolution"]!["modifierSource"]!["requirementIndex"] = 0; break;
            case "bands_partial": check["outcomes"]![0]!["category"] = "partial_success"; break;
            case "bands_order": check["outcomes"]![0]!["category"] = "failed_attempt"; break;
            case "bands_gap": check["outcomes"]![1]!["maximumMargin"] = -2; break;
            case "bands_duplicate": check["outcomes"]![1]!["bandId"] = "diagnostic_success"; break;
            case "bands_operations": check["outcomes"]![0]!["result"] = new JsonArray(new JsonObject { ["kind"] = "heal" }); break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        var before = method.ToJsonString();
        var parsed = MortalWoundTreatmentContract.ParseDiagnosisPathShape(JsonSerializer.SerializeToElement(method), "method");
        Assert.False(parsed.IsValid); Assert.Null(parsed.DiagnosisPath); Assert.NotEmpty(parsed.Issues);
        Assert.Equal(before, method.ToJsonString());
    }

    [Theory]
    [InlineData("chaos_sea")][InlineData("shining_abode")]
    public void CompleteMortalCheckNeverBecomesSpiritualGuarantee(string realm)
    {
        var positive = SpiritualDiagnosisAdmissionTests.Context(realm);
        Assert.NotNull(new SpiritualDiagnosisSourceData(positive).Admit(Assert.Single(positive.Catalog.Occurrences).Wound, "diagnose_tissue").Data);
        var check = WoundContractTestData.CreateMortalDiagnosisPath(false, 0)["check"]!.DeepClone().AsObject();
        var context = SpiritualDiagnosisAdmissionTests.Context(realm, check: check);
        var before = check.ToJsonString();
        var result = new SpiritualDiagnosisSourceData(context).Admit(Assert.Single(context.Catalog.Occurrences).Wound, "diagnose_tissue");
        Assert.Null(result.Data); Assert.Equal("check_unsupported", result.IssueCode);
        Assert.Equal(before, check.ToJsonString());
    }
}
