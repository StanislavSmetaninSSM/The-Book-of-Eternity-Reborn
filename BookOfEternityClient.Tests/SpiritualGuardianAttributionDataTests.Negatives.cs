using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualGuardianAttributionDataTests
{
    /// <summary>Otherwise valid v1–v4 comparisons explicitly refuse guardian attribution instead of widening their player lanes.</summary>
    /// <param name="version">One existing closed comparison version, validated before the declaration is injected.</param>
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void GuardianAttribution_ExistingVersionsRejectDeclaration(int version)
    {
        var source = version switch
        {
            1 => SpiritualStrainPreimageDataTests.CreateData().Source,
            2 => SpiritualExistingStrainPreimageTests.CreateData(3).Source,
            3 => SpiritualExistingPressureTestData.CreateData(),
            4 => SpiritualPressurePairTestData.CreateSource(),
            _ => throw new ArgumentOutOfRangeException(nameof(version))
        };
        var baseline = source.ToJsonString();
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var supported);
        Assert.Equal(version, Assert.IsType<SpiritualStrainPreimageData>(supported).Version);
        var actual = SpiritualExistingPressureTestData.ReadActual(source);
        var ordinary = SpiritualExistingPressureTestData.ReadOrdinary(source);
        Assert.False(actual.ContainsKey("spiritualWoundFailedAction"));
        Assert.False(ordinary.ContainsKey("spiritualWoundFailedAction"));
        Assert.Equal(version, SpiritualStrainPreimageData.ReadIntrinsic(source, actual).Version);
        Assert.Equal(baseline, source.ToJsonString());

        // Author a matching raw ordinary image using the existing pointer/root shape, not a stale fingerprint.
        // These detached bytes are only a comparator, never signed-input membership or current authority.
        JsonObject? original = version switch
        {
            3 => new JsonObject { ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
            {
                ["exchangeLog"] = new JsonArray(new JsonObject(), new JsonObject(), ordinary.DeepClone()),
                ["playerSideStrain"] = "fractured"
            } },
            4 => new JsonObject { ["schemaVersion"] = 1, ["activeConflict"] = new JsonObject
            {
                ["conflictId"] = "conflict-a", ["playerSideStrain"] = "fractured",
                ["exchangeLog"] = new JsonArray(ordinary.DeepClone())
            } },
            _ => null
        };
        static byte[] RawBytes(JsonObject root) => Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
        var proof = source["strainPreimage"]!.AsObject();
        var declaration = SpiritualGuardianAttributionTestData.FailedAction(actual["incomingAction"]!["actorId"]!.GetValue<string>());
        actual["spiritualWoundFailedAction"] = declaration.DeepClone();
        ordinary["spiritualWoundFailedAction"] = declaration.DeepClone();
        source["exchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(actual);
        proof["ordinaryExchangeJsonBase64"] = SpiritualPressurePairTestData.Encode(ordinary);
        if (original is not null)
        {
            original["activeConflict"]!["exchangeLog"]![version == 3 ? 2 : 0] = ordinary.DeepClone();
            proof["originalInput"]!["contentFingerprint"] = new CanonicalBeforeImage(true, RawBytes(original)).Fingerprint;
        }
        SpiritualExistingPressureTestData.Seal(source);
        var before = source.ToJsonString();
        SpiritualStrainPreimageData? leaked = null;
        const string cause = "Existing player comparison versions cannot carry guardian attribution.";
        var witnessError = Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out leaked));
        Assert.Equal(cause, witnessError.Message); Assert.Null(leaked);
        Assert.Equal(before, source.ToJsonString());
        var intrinsicError = Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, actual));
        Assert.Equal(cause, intrinsicError.Message);
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Requires the exact six-field local declaration, unsupported literal/scalar refusals and no extension field.</summary>
    /// <param name="change">One finite malformed declaration, applied consistently to complete ordinary and actual payloads.</param>
    [Theory]
    [InlineData("missing-version")] [InlineData("missing-side")] [InlineData("missing-actorType")]
    [InlineData("missing-actorId")] [InlineData("missing-actionPath")] [InlineData("missing-operationType")]
    [InlineData("extra-field")] [InlineData("absent")]
    [InlineData("version-string")] [InlineData("version-two")] [InlineData("side-player")]
    [InlineData("actor-player")] [InlineData("actor-foreign")] [InlineData("actor-blank")]
    [InlineData("slot-player")] [InlineData("slot-pointer")] [InlineData("operation-guard")]
    [InlineData("null-declaration")] [InlineData("array-declaration")]
    public void GuardianAttribution_RejectsMalformedOrUnsupportedDeclaration(string change)
    {
        var fixture = SupportedFixture();
        foreach (var exchange in new[] { fixture.Ordinary, fixture.Actual })
        {
            var declaration = exchange["spiritualWoundFailedAction"]!.AsObject();
            if (change.StartsWith("missing-", StringComparison.Ordinal)) Assert.True(declaration.Remove(change[8..]));
            else switch (change)
            {
                case "extra-field": declaration["unowned"] = true; break;
                case "absent": exchange.Remove("spiritualWoundFailedAction"); break;
                case "version-string": declaration["version"] = "1"; break;
                case "version-two": declaration["version"] = 2; break;
                case "side-player": declaration["side"] = "player"; break;
                case "actor-player": declaration["actorType"] = "player_soul"; declaration["actorId"] = "player_soul"; break;
                case "actor-foreign": declaration["actorId"] = "guardian_foreign"; break;
                case "actor-blank": declaration["actorId"] = " guardian_frame "; break;
                case "slot-player": declaration["actionPath"] = "/operationType"; break;
                case "slot-pointer": declaration["actionPath"] = "/activeConflict/exchangeLog/0/incomingAction"; break;
                case "operation-guard": declaration["operationType"] = "guard"; break;
                case "null-declaration": exchange["spiritualWoundFailedAction"] = null; break;
                case "array-declaration": exchange["spiritualWoundFailedAction"] = new JsonArray(); break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
        }
        Reject(fixture);
    }

    /// <summary>Every explicit incoming actor alias binds to the declared affected guardian, including retained intrinsic replay.</summary>
    /// <param name="alias">One existing type or ID alias; no arbitrary pointer lookup is used.</param>
    [Theory]
    [InlineData("actorType")] [InlineData("ownerActorType")]
    [InlineData("actorId")] [InlineData("actorRef")] [InlineData("ownerActorId")] [InlineData("guardianId")] [InlineData("id")]
    public void GuardianAttribution_RejectsForeignIncomingAlias(string alias)
    {
        var fixture = SupportedFixture();
        foreach (var exchange in new[] { fixture.Ordinary, fixture.Actual })
            exchange["incomingAction"]![alias] = alias.EndsWith("Type", StringComparison.Ordinal) ? "resident" : "guardian_foreign";
        Reject(fixture);
    }

    /// <summary>Duplicate declaration properties fail the existing owning strict JSON parser, rather than being silently overwritten.</summary>
    /// <param name="field">One exact declaration field.</param>
    [Theory]
    [InlineData("version")] [InlineData("side")] [InlineData("actorType")]
    [InlineData("actorId")] [InlineData("actionPath")] [InlineData("operationType")]
    public void GuardianAttribution_RejectsDuplicateDeclarationFields(string field)
    {
        var fixture = SupportedFixture();
        var declaration = fixture.Ordinary["spiritualWoundFailedAction"]!.ToJsonString();
        var property = JsonSerializer.Serialize(field) + ":";
        var duplicated = declaration.Replace(property, property + "null," + property, StringComparison.Ordinal);
        var rawOrdinary = fixture.Ordinary.ToJsonString().Replace(declaration, duplicated, StringComparison.Ordinal);
        Assert.NotEqual(fixture.Ordinary.ToJsonString(), rawOrdinary);
        fixture.Source["strainPreimage"]!["ordinaryExchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawOrdinary));
        SpiritualExistingPressureTestData.Seal(fixture.Source);
        var before = fixture.Source.ToJsonString();
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(fixture.Source));
        Assert.Equal(before, fixture.Source.ToJsonString());
    }

    /// <summary>The v5 comparison is a closed eight-field object with no scalar duplicate of the exchange declaration.</summary>
    /// <param name="field">One required field or the forbidden duplicated scalar proof.</param>
    [Theory]
    [InlineData("version")] [InlineData("inputRoleRevision")] [InlineData("ordinaryExchangeJsonBase64")]
    [InlineData("originalInput")] [InlineData("parentWoundJsonBase64")] [InlineData("effectId")]
    [InlineData("componentId")] [InlineData("ordinaryCalculation")] [InlineData("+failedAction")]
    public void GuardianAttribution_RejectsMissingOrExtraComparisonFields(string field)
    {
        var fixture = SupportedFixture();
        var proof = fixture.Source["strainPreimage"]!.AsObject();
        if (field == "+failedAction") proof["failedAction"] = SpiritualGuardianAttributionTestData.FailedAction();
        else Assert.True(proof.Remove(field));
        Reject(fixture);
    }

    /// <summary>Whole-record identity, independent ordinary arithmetic, declared ceilings and singleton guardian selection remain mandatory.</summary>
    /// <param name="change">One finite retained contradiction.</param>
    [Theory]
    [InlineData("actual-no-step")] [InlineData("actual-broken")] [InlineData("ordinary-before")]
    [InlineData("ordinary-actual")] [InlineData("ordinary-player-strain")] [InlineData("actual-player-strain")]
    [InlineData("ordinary-summary")] [InlineData("ordinary-cost")] [InlineData("ordinary-dice")]
    [InlineData("parent-owner")] [InlineData("foreign-effect")] [InlineData("foreign-component")]
    [InlineData("wrong-role")] [InlineData("actual-maximum")] [InlineData("ordinary-calculation")]
    [InlineData("later-local-ordinal")]
    public void GuardianAttribution_RejectsRetainedContradictions(string change)
    {
        var fixture = SupportedFixture(); var proof = fixture.Source["strainPreimage"]!;
        switch (change)
        {
            case "actual-no-step": fixture.Actual["after"]!["oppositionSideStrain"] = "fractured"; break;
            case "actual-broken": fixture.Actual["after"]!["oppositionSideStrain"] = "broken"; break;
            case "ordinary-before": fixture.Ordinary["after"]!["oppositionSideStrain"] = "strained"; break;
            case "ordinary-actual": fixture.Ordinary["after"]!["oppositionSideStrain"] = "overwhelmed"; break;
            case "ordinary-player-strain": fixture.Ordinary["after"]!["playerSideStrain"] = "strained"; break;
            case "actual-player-strain": fixture.Actual["after"]!["playerSideStrain"] = "strained"; break;
            case "ordinary-summary": fixture.Ordinary["summary"] = "Иная запись обмена."; break;
            case "ordinary-cost": fixture.Ordinary["actionCostAudit"]!["player"]!["max"] = 11; break;
            case "ordinary-dice": fixture.Ordinary["diceAudit"]!["margin"] = 7; break;
            case "parent-owner":
                var parent = SpiritualExistingPressureTestData.ReadParent(fixture.Source); parent["owner"]!["ownerId"] = "guardian_foreign";
                proof["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent); break;
            case "foreign-effect": proof["effectId"] = "effect_foreign"; break;
            case "foreign-component": proof["componentId"] = "component_foreign"; break;
            case "wrong-role": proof["inputRoleRevision"] = "existing_pressure_ordinary_v1"; break;
            case "actual-maximum": fixture.Source["maximumSeverityRank"] = 0; break;
            case "ordinary-calculation": proof["ordinaryCalculation"]!["traumaPressure"] = 15L; break;
            case "later-local-ordinal":
                fixture.Source["exchangeOrdinal"] = 1;
                fixture.Source["turnEvidence"]!["bounds"]!["exchangeCount"] = 2;
                fixture.Source["turnEvidence"]!["bounds"]!["sourceSlotCount"] = 4;
                foreach (var claim in fixture.Source["turnEvidence"]!["diceClaims"]!.AsArray().OfType<JsonObject>())
                {
                    claim["exchangeOrdinal"] = 1; claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim);
                }
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        Reject(fixture);
    }

    /// <summary>Checks matching aliases against a different exact guardian ID, preventing a fixture-literal production restriction.</summary>
    [Fact]
    public void GuardianAttribution_AcceptsExactAliasesForAnotherGuardian()
    {
        var fixture = SpiritualGuardianAttributionTestData.Create("guardian_attribution_other"); AssertPrerequisites(fixture);
        foreach (var exchange in new[] { fixture.Ordinary, fixture.Actual })
        {
            var incoming = exchange["incomingAction"]!;
            incoming["ownerActorType"] = "guardian";
            foreach (var alias in new[] { "actorRef", "ownerActorId", "guardianId", "id" }) incoming[alias] = "guardian_attribution_other";
        }
        fixture.Conflict["activeConflict"]!["exchangeLog"]![0] = fixture.Ordinary.DeepClone();
        var rawInput = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + fixture.Conflict.ToJsonString() + "\n")).ToArray();
        fixture.Source["strainPreimage"]!["originalInput"]!["contentFingerprint"] = new CanonicalBeforeImage(true, rawInput).Fingerprint;
        fixture.Source["strainPreimage"]!["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Ordinary);
        fixture.Source["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Actual);
        SpiritualExistingPressureTestData.Seal(fixture.Source); var before = fixture.Source.ToJsonString();
        Assert.Equal(ActualCalculation(), SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var retained));
        Assert.Equal("guardian_attribution_other", Assert.IsType<SpiritualStrainPreimageData>(retained).ParentWound.Owner.OwnerId);
        Assert.Equal(before, fixture.Source.ToJsonString());
    }

    private static SpiritualGuardianAttributionFixture SupportedFixture()
    {
        var fixture = SpiritualGuardianAttributionTestData.Create(); AssertPrerequisites(fixture);
        // A rejection is meaningful only after this exact valid v5 baseline succeeds. Current missing support is never a negative PASS.
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var data);
        Assert.Equal(ActualCalculation(), calculation); Assert.Equal(5, Assert.IsType<SpiritualStrainPreimageData>(data).Version);
        return fixture;
    }

    private static void Reject(SpiritualGuardianAttributionFixture fixture)
    {
        var proof = fixture.Source["strainPreimage"]!.AsObject();
        if (proof.ContainsKey("ordinaryExchangeJsonBase64")) proof["ordinaryExchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Ordinary);
        fixture.Source["exchangeJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(fixture.Actual);
        SpiritualExistingPressureTestData.Seal(fixture.Source); var before = fixture.Source.ToJsonString();
        SpiritualStrainPreimageData? leaked = null;
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out leaked));
        Assert.Null(leaked); Assert.Equal(before, fixture.Source.ToJsonString());
        Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(fixture.Source, fixture.Actual));
        Assert.Equal(before, fixture.Source.ToJsonString());
    }
}
