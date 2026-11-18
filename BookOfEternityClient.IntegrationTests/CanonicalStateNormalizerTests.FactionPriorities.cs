using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Theory]
    [InlineData("absent", false)]
    [InlineData("null", false)]
    [InlineData("object", false)]
    [InlineData("string", true)]
    [InlineData("number", true)]
    [InlineData("boolean", true)]
    [InlineData("array", true)]
    [InlineData("incomplete", true)]
    public void FactionPriorities_FullDeclarationNullableShape(string shape, bool rejects)
    {
        var faction = BuildCompleteMinimalMortalCreation();
        SetFactionPrioritiesShape(faction, shape);
        using var response = JsonDocument.Parse(new JsonObject
        {
            ["factionDataChanges"] = new JsonArray(faction)
        }.ToJsonString());
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = validator.ValidateResponse(response.RootElement);
        AssertFactionPrioritiesShape(issues, rejects);
    }

    [Theory]
    [InlineData("absent", false)]
    [InlineData("null", false)]
    [InlineData("object", false)]
    [InlineData("string", true)]
    [InlineData("number", true)]
    [InlineData("boolean", true)]
    [InlineData("array", true)]
    [InlineData("incomplete", true)]
    public async Task FactionPriorities_SelectedCanonicalNullableShape(string shape, bool rejects)
    {
        var faction = BuildStoredMaterializedMortalFaction("faction_watch");
        SetFactionPrioritiesShape(faction, shape);
        await _fs.WriteFileAtomicAsync(MortalFactionCorePath, new JsonObject
        {
            ["factions"] = new JsonArray(faction)
        }.ToJsonString());
        var before = await _fs.ReadFileBytesAsync(MortalFactionCorePath);
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.WorldQuestCombatFactionStateFiles, [MortalFactionCorePath]));
        Assert.Equal(before, await _fs.ReadFileBytesAsync(MortalFactionCorePath));
        AssertFactionPrioritiesShape(issues, rejects);
    }

    private static void SetFactionPrioritiesShape(JsonObject faction, string shape)
    {
        faction["developmentArchetype"] = "Balanced";
        faction.Remove("customArchetypePriorities");
        if (shape == "absent") return;
        faction["customArchetypePriorities"] = shape switch
        {
            "null" => null,
            "object" => new JsonObject { ["primary"] = "economic", ["secondary"] = "social", ["tertiary"] = "logistics" },
            "string" => JsonValue.Create("none"),
            "number" => JsonValue.Create(0),
            "boolean" => JsonValue.Create(false),
            "array" => new JsonArray(),
            "incomplete" => new JsonObject { ["primary"] = "economic" },
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };
        if (shape == "object" || shape == "incomplete") faction["developmentArchetype"] = "Road Custodians";
    }

    private static void AssertFactionPrioritiesShape(IEnumerable<ValidationIssue> issues, bool rejects)
    {
        // This is the public field-shape contract, not complete faction birth/owner acceptance.
        var errors = issues.Where(issue => issue.Severity == IssueSeverity.Error &&
            issue.FilePath.Contains(".customArchetypePriorities", StringComparison.Ordinal)).ToArray();
        if (rejects) Assert.NotEmpty(errors);
        else Assert.Empty(errors);
    }
}
