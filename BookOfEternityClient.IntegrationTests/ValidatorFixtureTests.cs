using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class ValidatorFixtureTests
{
    private static readonly HashSet<string> MortalLocationFixtureIds = new(StringComparer.Ordinal)
    {
        "current_location_known_partial",
        "world_map_existing_link",
        "world_map_existing_storage",
        "world_map_threat_completion_active"
    };

    public static IEnumerable<object[]> FixtureDefinitions()
    {
        return ReadFixtureDefinitions()
            .Where(definition => !MortalLocationFixtureIds.Contains(definition.Id))
            .Select(static definition => new object[] { definition });
    }

    public static IEnumerable<object[]> MortalLocationFixtureDefinitions()
    {
        return ReadFixtureDefinitions()
            .Where(definition => MortalLocationFixtureIds.Contains(definition.Id))
            .Select(static definition => new object[] { definition });
    }

    private static IEnumerable<ValidatorFixtureDefinition> ReadFixtureDefinitions()
    {
        foreach (var fixtureJson in Directory.EnumerateFiles(TestRepoPaths.ValidatorFixturesRoot, "fixture.json", SearchOption.AllDirectories))
        {
            var fixtureDir = Path.GetDirectoryName(fixtureJson)!;
            using var doc = JsonDocument.Parse(File.ReadAllText(fixtureJson));
            var definition = JsonSerializer.Deserialize<ValidatorFixtureDefinition>(doc.RootElement.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException($"Failed to deserialize fixture definition: {fixtureJson}");

            if (string.IsNullOrWhiteSpace(definition.Id))
                definition.Id = Path.GetFileName(fixtureDir);

            yield return definition;
        }
    }

    [Theory]
    [MemberData(nameof(FixtureDefinitions))]
    public async Task BrokenVariant_ProducesExpectedCodes(ValidatorFixtureDefinition definition)
    {
        using var harness = new ValidatorFixtureHarness(definition);
        var result = await harness.RunBrokenAsync();

        foreach (var expectedCode in definition.ExpectedBrokenCodes)
            Assert.Contains(expectedCode, result.ErrorCodes, StringComparer.OrdinalIgnoreCase);

        foreach (var forbiddenCode in definition.ForbiddenBrokenCodes)
            Assert.DoesNotContain(forbiddenCode, result.ErrorCodes, StringComparer.OrdinalIgnoreCase);

        if (!definition.AllowExtraBrokenCodes)
        {
            var actual = result.ErrorCodes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var expected = definition.ExpectedBrokenCodes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [MemberData(nameof(FixtureDefinitions))]
    public async Task FixedVariant_ClearsTargetCodes(ValidatorFixtureDefinition definition)
    {
        using var harness = new ValidatorFixtureHarness(definition);
        var result = await harness.RunFixedAsync();

        foreach (var expectedBrokenCode in definition.ExpectedBrokenCodes)
            Assert.DoesNotContain(expectedBrokenCode, result.ErrorCodes, StringComparer.OrdinalIgnoreCase);

        foreach (var forbiddenCode in definition.ForbiddenFixedCodes)
            Assert.DoesNotContain(forbiddenCode, result.ErrorCodes, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(MortalLocationFixtureDefinitions))]
    public async Task MortalLocationMigratedFixture_BrokenAndFixedVariantsRespectCurrentContract(
        ValidatorFixtureDefinition definition)
    {
        using var brokenHarness = new ValidatorFixtureHarness(definition);
        var broken = await brokenHarness.RunBrokenAsync();
        foreach (var expectedCode in definition.ExpectedBrokenCodes)
            Assert.Contains(expectedCode, broken.ErrorCodes, StringComparer.OrdinalIgnoreCase);
        foreach (var forbiddenCode in definition.ForbiddenBrokenCodes)
            Assert.DoesNotContain(forbiddenCode, broken.ErrorCodes, StringComparer.OrdinalIgnoreCase);

        using var fixedHarness = new ValidatorFixtureHarness(definition);
        var fixedResult = await fixedHarness.RunFixedAsync();
        foreach (var expectedBrokenCode in definition.ExpectedBrokenCodes)
            Assert.DoesNotContain(expectedBrokenCode, fixedResult.ErrorCodes, StringComparer.OrdinalIgnoreCase);
        foreach (var forbiddenCode in definition.ForbiddenFixedCodes)
            Assert.DoesNotContain(forbiddenCode, fixedResult.ErrorCodes, StringComparer.OrdinalIgnoreCase);
    }
}
