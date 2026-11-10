using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Actual selected PlayerStateFiles must refuse retired root shapes at the canonical player path.</summary>
public sealed class WoundCanonicalPlayerCarrierValidationTests
{
    /// <summary>Both empty and populated bare arrays require a root refusal before loose wound validation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlayerStateFiles_RejectsBareArrayAtCanonicalPlayerPath(bool populated)
    {
        var json = populated
            ? "[{\"woundName\":\"Старая рана\",\"severity\":\"moderate\",\"generatedEffects\":[],\"healingState\":{}}]"
            : "[]";
        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(populated ? 1 : 0, document.RootElement.GetArrayLength());
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var issues = await ValidateUnchangedPlayerFileAsync(context, json);
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error && !IsRootError(issue));
        // Intended causal RED is only this real missing refusal, after setup and byte-invariance premises.
        // No new diagnostic code/message is required to manufacture a failure.
        Assert.Contains(issues, IsRootError);
    }

    /// <summary>Existing generic root refusals remain valid for every scalar/null JSON kind.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("17")]
    [InlineData("\"Скаляр\"")]
    [InlineData("true")]
    [InlineData("false")]
    public async Task PlayerStateFiles_PreservesScalarAndNullRootRefusal(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array);
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var issues = await ValidateUnchangedPlayerFileAsync(context, json);
        // These preserving controls may already PASS. A generic root Error is sufficient.
        Assert.Contains(issues, IsRootError);
    }

    /// <summary>Genuine strict empty and populated player carriers remain valid through the same actual phase.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlayerStateFiles_AcceptsCanonicalEmptyAndPopulatedCarrier(bool populated)
    {
        const string woundId = "wound_canonical_player_carrier";
        var carrier = populated
            ? WoundContractTestData.CreatePlayerCarrier(WoundContractTestData.CreateActiveWound(woundId))
            : WoundContractTestData.CreatePlayerCarrier();
        var catalog = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(carrier, null, null, null, null));
        Assert.Empty(catalog.Issues);
        Assert.Equal(populated ? 1 : 0, catalog.Occurrences.Count);
        if (populated)
        {
            var occurrence = Assert.Single(catalog.Occurrences);
            Assert.Equal(woundId, occurrence.WoundId);
            Assert.Equal(WoundCarrierCatalog.PlayerPath, occurrence.FilePath);
            Assert.Equal("player_current", occurrence.Wound.Owner.OwnerId);
            Assert.Equal("active", occurrence.Wound.Lifecycle);
        }
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var issues = await ValidateUnchangedPlayerFileAsync(context, carrier.ToJsonString());
        Assert.Empty(issues);
    }

    /// <summary>Runs the real phase on one selected canonical source and compares its exact bytes before/after.</summary>
    /// <param name="context">Independent existing filesystem/validator fixture with genuine session generation.</param>
    /// <param name="json">Exact current file contents, including deliberately unsupported root shapes.</param>
    /// <returns>The actual ordered phase findings; no parser result or verdict is substituted.</returns>
    private static async Task<IReadOnlyList<ValidationIssue>> ValidateUnchangedPlayerFileAsync(
        ResourceMaterializationTestContext context, string json)
    {
        var bytes = new UTF8Encoding(false).GetBytes(json);
        await context.WriteExactBytesAsync(WoundCarrierCatalog.PlayerPath, bytes);
        var path = context.FileSystem.ResolvePath(WoundCarrierCatalog.PlayerPath);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        try
        {
            return await context.Validator.ValidateGameStateAsync(new GameStateValidationSelection(
                GameStateValidationPhase.PlayerStateFiles, [WoundCarrierCatalog.PlayerPath]));
        }
        finally { Assert.Equal(bytes, File.ReadAllBytes(path)); }
    }

    /// <summary>Tests actual canonical-root refusal independently of a particular diagnostic code or message.</summary>
    /// <param name="issue">One finding returned by the real selected validator.</param>
    /// <returns>True only for an Error on the exact canonical player root.</returns>
    private static bool IsRootError(ValidationIssue issue) =>
        issue.Severity == IssueSeverity.Error && issue.FilePath == WoundCarrierCatalog.PlayerPath;
}
