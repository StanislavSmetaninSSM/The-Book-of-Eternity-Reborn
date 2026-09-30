using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundRecoveryAnchorContractTests
{
    [Fact]
    public void CanonicalRecoveryAnchors_RoundTripAsIndependentClosedTypedValues()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var recovery = wound["recovery"]!.AsObject();
        recovery["recoveryAnchor"] = new JsonObject
        {
            ["anchorKind"] = "creation",
            ["anchorMinute"] = 1260L,
            ["anchorTransitionId"] = "wound_transition_create_001"
        };
        recovery["deteriorationAnchor"] = new JsonObject
        {
            ["conditionKey"] = "not_stabilized",
            ["anchorMinute"] = 1260L,
            ["anchorTransitionId"] = "wound_transition_create_001"
        };

        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            "canonicalWound");

        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var typed = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        Assert.Equal(
            new WoundRecoveryAnchor(
                "creation",
                1260L,
                "wound_transition_create_001"),
            typed.Recovery.RecoveryAnchor);
        Assert.Equal(
            new WoundDeteriorationAnchor(
                "not_stabilized",
                1260L,
                "wound_transition_create_001"),
            typed.Recovery.DeteriorationAnchor);

        var reparsed = WoundMaterializationContract.Parse(
            WoundMaterializationContract.SerializeCanonical(typed),
            "canonicalRoundTrip");

        Assert.True(reparsed.IsValid, Describe(reparsed.Issues));
        Assert.Equal(typed.Recovery.RecoveryAnchor, reparsed.Wound!.Recovery.RecoveryAnchor);
        Assert.Equal(
            typed.Recovery.DeteriorationAnchor,
            reparsed.Wound.Recovery.DeteriorationAnchor);
    }

    [Theory]
    [InlineData("recovery_extra")]
    [InlineData("recovery_missing_transition")]
    [InlineData("recovery_negative_minute")]
    [InlineData("deterioration_extra")]
    [InlineData("deterioration_missing_condition")]
    [InlineData("deterioration_negative_minute")]
    public void CanonicalRecoveryAnchors_RejectMalformedOrOpenValues(string mutation)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var recovery = wound["recovery"]!.AsObject();
        var recoveryAnchor = new JsonObject
        {
            ["anchorKind"] = "creation",
            ["anchorMinute"] = 10L,
            ["anchorTransitionId"] = "wound_transition_create_001"
        };
        var deteriorationAnchor = new JsonObject
        {
            ["conditionKey"] = "not_stabilized",
            ["anchorMinute"] = 10L,
            ["anchorTransitionId"] = "wound_transition_create_001"
        };
        recovery["recoveryAnchor"] = recoveryAnchor;
        recovery["deteriorationAnchor"] = deteriorationAnchor;

        switch (mutation)
        {
            case "recovery_extra":
                recoveryAnchor["callerFingerprint"] = "forged";
                break;
            case "recovery_missing_transition":
                recoveryAnchor.Remove("anchorTransitionId");
                break;
            case "recovery_negative_minute":
                recoveryAnchor["anchorMinute"] = -1L;
                break;
            case "deterioration_extra":
                deteriorationAnchor["callerTickKey"] = "forged";
                break;
            case "deterioration_missing_condition":
                deteriorationAnchor.Remove("conditionKey");
                break;
            case "deterioration_negative_minute":
                deteriorationAnchor["anchorMinute"] = -1L;
                break;
            default:
                throw new InvalidOperationException(mutation);
        }

        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            "canonicalWound");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Wound);
        Assert.Contains(
            parsed.Issues,
            issue => issue.FilePath.StartsWith(
                mutation.StartsWith("recovery_", StringComparison.Ordinal)
                    ? "canonicalWound.recovery.recoveryAnchor"
                    : "canonicalWound.recovery.deteriorationAnchor",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanonicalRecoveryAnchors_AllowAbsentOrNullValues(bool explicitNull)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        if (explicitNull)
        {
            wound["recovery"]!["recoveryAnchor"] = null;
            wound["recovery"]!["deteriorationAnchor"] = null;
        }

        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            "canonicalWound");

        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        Assert.Null(parsed.Wound!.Recovery.RecoveryAnchor);
        Assert.Null(parsed.Wound.Recovery.DeteriorationAnchor);
    }

    [Fact]
    public void CanonicalJsonUtf8_AcceptsExactlyOneOptionalLeadingBom()
    {
        const string json = "{\"schemaVersion\":1}";
        var payload = Encoding.UTF8.GetBytes(json);
        var withBom = Encoding.UTF8.GetPreamble().Concat(payload).ToArray();

        Assert.Equal(json, CanonicalJsonUtf8.DecodeOneOptionalBom(payload));
        Assert.Equal(json, CanonicalJsonUtf8.DecodeOneOptionalBom(withBom));
    }

    [Fact]
    public void CanonicalJsonUtf8_RejectsDoubleMidstreamAndInvalidUtf8BomEdges()
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var payload = Encoding.UTF8.GetBytes("{\"schemaVersion\":1}");
        var doubleBom = preamble.Concat(preamble).Concat(payload).ToArray();
        var midstreamBom = payload[..1]
            .Concat(preamble)
            .Concat(payload[1..])
            .ToArray();
        var invalidUtf8 = new byte[] { 0x7b, 0x22, 0xc3, 0x28, 0x22, 0x7d };

        Assert.Throws<InvalidDataException>(() =>
            CanonicalJsonUtf8.DecodeOneOptionalBom(doubleBom));
        Assert.Throws<InvalidDataException>(() =>
            CanonicalJsonUtf8.DecodeOneOptionalBom(midstreamBom));
        Assert.Throws<InvalidDataException>(() =>
            CanonicalJsonUtf8.DecodeOneOptionalBom(invalidUtf8));
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(static issue =>
                $"{issue.Code}@{issue.FilePath}: {issue.Message}"));
}
