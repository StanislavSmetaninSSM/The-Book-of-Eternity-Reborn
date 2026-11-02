using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Public response admission must not silently accept a retired player wound command.</summary>
public sealed class PlayerLegacyWoundResponseValidationTests
{
    private const string LegacyPath = "response.playerWoundChanges";
    private const string LegacyCode = "wound_materialization_legacy_response_unsupported";
    private const string EffectsPath = "game_state/player/effects.json";
    private const string Effects = "{\"schemaVersion\":1,\"entries\":[]}";

    /// <summary>Loose arrays and objects must cause an actual admission Error, independently of its code.</summary>
    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"woundName\":\"Старая рана\"}]")]
    [InlineData("{}")]
    [InlineData("{\"woundName\":\"Старая рана\"}")]
    public async Task ValidateResponse_RejectsLegacyArraysAndObjectsBeforeAdmission(string payload)
    {
        await AssertLegacyRefusalWithoutMutationAsync(payload);
    }

    /// <summary>Existing null/scalar shape refusals need the retired-command classification, not a repair into loose data.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("7")]
    public async Task ValidateResponse_ClassifiesLegacyNullAndScalarWithoutLooseRepair(string payload)
    {
        await AssertLegacyRefusalWithoutMutationAsync(payload);
    }

    /// <summary>Absent legacy data and a genuine current neighboring command remain valid and read-only.</summary>
    [Fact]
    public async Task ValidateResponse_AcceptsAbsentWoundsAndCurrentAppearanceWithoutMutation()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var wounds = await SeedCurrentCarriersAsync(context);
        AssertLawfulControls(context.Validator);
        var raw = CurrentResponse().ToJsonString();
        using var document = JsonDocument.Parse(raw);

        Assert.Empty(context.Validator.ValidateResponse(document.RootElement));

        await AssertUnchangedAsync(context, wounds, raw, document);
    }

    /// <summary>Runs the actual public validator after genuine controls, preserving the submitted JSON and canonical bytes.</summary>
    private static async Task AssertLegacyRefusalWithoutMutationAsync(string payload)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var wounds = await SeedCurrentCarriersAsync(context);
        AssertLawfulControls(context.Validator);
        var response = CurrentResponse();
        response["playerWoundChanges"] = JsonNode.Parse(payload);
        var raw = response.ToJsonString();
        using var document = JsonDocument.Parse(raw);

        var issues = context.Validator.ValidateResponse(document.RootElement);

        await AssertUnchangedAsync(context, wounds, raw, document);
        // The first oracle measures admission; a new diagnostic code cannot manufacture a causal refusal.
        Assert.Contains(issues, issue => issue.FilePath == LegacyPath && issue.Severity == IssueSeverity.Error);
        var issue = Assert.Single(issues);
        Assert.Equal(LegacyPath, issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(LegacyCode, issue.Code);
        Assert.DoesNotContain("effectChanges", issue.RepairHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Seeds an independently validated current player carrier; no authority or validator result is substituted.</summary>
    private static async Task<string> SeedCurrentCarriersAsync(ResourceMaterializationTestContext context)
    {
        var carrier = WoundContractTestData.CreatePlayerCarrier();
        Assert.Empty(WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(carrier, null, null, null, null)).Issues);
        var wounds = carrier.ToJsonString();
        await context.WriteExactJsonAsync(WoundCarrierCatalog.PlayerPath, wounds);
        await context.WriteExactJsonAsync(EffectsPath, Effects);
        return wounds;
    }

    /// <summary>A supported ordinary response field supplies the adjacent lawful control.</summary>
    private static JsonObject CurrentResponse() => new()
    {
        ["response"] = "Путник продолжает путь.",
        ["playerAppearanceChange"] = "На плаще видна дорожная пыль."
    };

    /// <summary>Checks absence and the adjacent current field before testing the retired input.</summary>
    private static void AssertLawfulControls(ValidationService validator)
    {
        using var absent = JsonDocument.Parse("{\"response\":\"Путь продолжается.\"}");
        Assert.Empty(validator.ValidateResponse(absent.RootElement));
        using var current = JsonDocument.Parse(CurrentResponse().ToJsonString());
        Assert.Empty(validator.ValidateResponse(current.RootElement));
    }

    /// <summary>Confirms read-only behavior against exact canonical source bytes, not a reserialized expectation.</summary>
    private static async Task AssertUnchangedAsync(ResourceMaterializationTestContext context, string wounds, string raw, JsonDocument document)
    {
        Assert.Equal(raw, document.RootElement.GetRawText());
        Assert.Equal(Encoding.UTF8.GetBytes(wounds), await context.FileSystem.ReadFileBytesAsync(WoundCarrierCatalog.PlayerPath));
        Assert.Equal(Encoding.UTF8.GetBytes(Effects), await context.FileSystem.ReadFileBytesAsync(EffectsPath));
    }
}
