using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceMaterializationContractTests
{
    [Fact]
    public void ResponseModelAndMappingExposeCommonTransientResourceRoutes()
    {
        var responseFields = typeof(GameResponse)
            .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Select(property => property.GetCustomAttributes(typeof(JsonPropertyNameAttribute), inherit: false)
                .OfType<JsonPropertyNameAttribute>()
                .SingleOrDefault()
                ?.Name)
            .Where(static name => name != null)
            .ToHashSet(StringComparer.Ordinal);

        var expectedFields = new[]
        {
            "resourceDefinitionCreations",
            "resourceCapacityChanges",
            "resourceChanges"
        };

        Assert.All(expectedFields, field => Assert.Contains(field, responseFields));
        Assert.All(expectedFields, field => Assert.Equal(
            ResourceMaterializationContract.CommandPath,
            FileMapping.FieldToFile[field]));
    }

    [Fact]
    public void ResponseSerializationPreservesEverySuppliedResourceCommandArray()
    {
        var response = new GameResponse
        {
            ResourceDefinitionCreations = JsonSerializer.Deserialize<JsonElement[]>(
                "[{\"resourceKey\":\"focus\"}]")!,
            ResourceCapacityChanges = JsonSerializer.Deserialize<JsonElement[]>(
                "[{\"operation\":\"initialize\"}]")!,
            ResourceChanges = JsonSerializer.Deserialize<JsonElement[]>(
                "[{\"operation\":\"spend\"}]")!
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response));

        Assert.Equal(
            "focus",
            document.RootElement
                .GetProperty("resourceDefinitionCreations")[0]
                .GetProperty("resourceKey")
                .GetString());
        Assert.Equal(
            "initialize",
            document.RootElement
                .GetProperty("resourceCapacityChanges")[0]
                .GetProperty("operation")
                .GetString());
        Assert.Equal(
            "spend",
            document.RootElement
                .GetProperty("resourceChanges")[0]
                .GetProperty("operation")
                .GetString());
    }

    [Fact]
    public void ParseDefinitions_MissingIsAllowedOnlyForExplicitPristineBootstrap()
    {
        var allowed = ResourceMaterializationContract.ParseDefinitions(
            json: null,
            allowMissingPristine: true);
        var rejected = ResourceMaterializationContract.ParseDefinitions(
            json: null,
            allowMissingPristine: false);

        Assert.True(allowed.IsMissing);
        Assert.True(allowed.IsValid);
        Assert.Null(allowed.Root);
        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "resource_materialization_root_missing");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{")]
    public void ParseDefinitions_PresentInvalidRootNeverBecomesMissing(string json)
    {
        var result = ResourceMaterializationContract.ParseDefinitions(
            json,
            allowMissingPristine: true);

        Assert.False(result.IsMissing);
        Assert.False(result.IsValid);
        Assert.Null(result.Root);
        Assert.Contains(result.Issues, issue =>
            issue.Code is "resource_materialization_invalid_json" or
                "resource_materialization_invalid_root");
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"definitions\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"definitions\":[{\"resourceKey\":\"health\",\"resourceKey\":\"energy\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"definitions\":[{\"minimumPolicy\":{\"kind\":\"definition_fixed\",\"kind\":\"instance_fixed\"}}]}")]
    public void ParseDefinitions_DuplicatePropertyAtAnyDepthIsRejected(string json)
    {
        var result = ResourceMaterializationContract.ParseDefinitions(
            json,
            allowMissingPristine: false);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_materialization_duplicate_property");
        Assert.Null(result.Root);
    }

    [Fact]
    public void ParseDefinitions_RootIsClosedAndVersioned()
    {
        var unknown = ResourceMaterializationContract.ParseDefinitions(
            "{\"schemaVersion\":1,\"definitions\":[],\"future\":true}",
            allowMissingPristine: false);
        var wrongVersion = ResourceMaterializationContract.ParseDefinitions(
            "{\"schemaVersion\":2,\"definitions\":[]}",
            allowMissingPristine: false);
        var wrongDefinitions = ResourceMaterializationContract.ParseDefinitions(
            "{\"schemaVersion\":1,\"definitions\":{}}",
            allowMissingPristine: false);

        Assert.Contains(unknown.Issues, issue =>
            issue.Code == "resource_materialization_unknown_field");
        Assert.Contains(wrongVersion.Issues, issue =>
            issue.Code == "resource_materialization_invalid_field");
        Assert.Contains(wrongDefinitions.Issues, issue =>
            issue.Code == "resource_materialization_invalid_field");
    }

    [Fact]
    public void ParseDefinitions_EnforcesExactCatalogLimitBoundary()
    {
        var atLimit = ResourceMaterializationContract.ParseDefinitions(
            CreateDefinitionsRoot(ResourceMaterializationContract.MaxDefinitions),
            allowMissingPristine: false);
        var aboveLimit = ResourceMaterializationContract.ParseDefinitions(
            CreateDefinitionsRoot(ResourceMaterializationContract.MaxDefinitions + 1),
            allowMissingPristine: false);

        Assert.True(atLimit.IsValid);
        Assert.NotNull(atLimit.Root);
        Assert.Contains(aboveLimit.Issues, issue =>
            issue.Code == "resource_materialization_limit_exceeded");
        Assert.Null(aboveLimit.Root);
    }

    [Fact]
    public void TechnicalLimits_AreClosedContractConstants()
    {
        Assert.Equal(256, ResourceMaterializationContract.MaxDefinitions);
        Assert.Equal(20_000, ResourceMaterializationContract.MaxLiveEntries);
        Assert.Equal(256, ResourceMaterializationContract.MaxCapacityTransitionsPerTurn);
        Assert.Equal(512, ResourceMaterializationContract.MaxMutationsBeforeTriggers);
        Assert.Equal(1_024, ResourceMaterializationContract.MaxTriggerNodes);
        Assert.Equal(32, ResourceMaterializationContract.MaxTriggerDepth);
        Assert.Equal(64, ResourceMaterializationContract.MaxPendingRequestsPerTurn);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("-12.50", "-12.50")]
    [InlineData("1e2", "100")]
    [InlineData("1.00e-2", "0.0100")]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950335")]
    public void TryReadExactDecimal_AcceptsOnlyExactlyRepresentableJsonNumbers(
        string json,
        string expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.True(ResourceMaterializationContract.TryReadExactDecimal(
            document.RootElement,
            out var actual));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), actual);
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("null")]
    [InlineData("1e1000")]
    [InlineData("0.123456789012345678901234567891")]
    [InlineData("79228162514264337593543950336")]
    public void TryReadExactDecimal_RejectsWrongTypeRangeOrLossyPrecision(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.False(ResourceMaterializationContract.TryReadExactDecimal(
            document.RootElement,
            out _));
    }

    [Theory]
    [InlineData("health", true)]
    [InlineData("mana_v1", true)]
    [InlineData(" health", false)]
    [InlineData("health ", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("e\u0301nergy", false)]
    [InlineData("mana\nkey", false)]
    [InlineData("mana\u202Ekey", false)]
    public void IsExactIdentifier_RequiresTrimmedNormalizationStableText(
        string value,
        bool expected)
    {
        Assert.Equal(expected, ResourceMaterializationContract.IsExactIdentifier(value));
    }

    [Theory]
    [InlineData("10", "0", "0.25", true)]
    [InlineData("10.25", "0", "0.25", true)]
    [InlineData("10.20", "0", "0.25", false)]
    [InlineData("10", "0", "0", false)]
    [InlineData("10", "0", "-1", false)]
    public void QuantumAlignment_IsExactAndNeverUsesTolerance(
        string value,
        string minimum,
        string quantum,
        bool expected)
    {
        Assert.Equal(
            expected,
            ResourceMaterializationContract.IsQuantumAligned(
                decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(minimum, System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(quantum, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void QuantumAlignment_DoesNotRoundAwayMaxScaleMinimum()
    {
        var minimum = decimal.Parse(
            "0.0000000000000000000000000001",
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.False(ResourceMaterializationContract.IsQuantumAligned(
            value: 10m,
            minimum,
            quantum: 1m));
    }

    [Fact]
    public void ExactDecimalArithmetic_RejectsScaleLossAndComparesProductsWithoutRounding()
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var quantum = decimal.Parse("0.0000000000000000000000000001", culture);
        var unequalRatioCurrent = decimal.Parse(
            "0.0500000000000000000000000001",
            culture);

        Assert.False(ResourceMaterializationContract.TryAddExact(
            10m,
            quantum,
            out _));
        Assert.False(ResourceMaterializationContract.TrySubtractExact(
            -10m,
            quantum,
            out _));
        Assert.True(ResourceMaterializationContract.TryAddExact(
            0.1m,
            0.2m,
            out var exactSum));
        Assert.Equal(0.3m, exactSum);
        Assert.False(ResourceMaterializationContract.ProductsEqualExact(
            unequalRatioCurrent,
            0.1m,
            0.05m,
            0.1m));
        Assert.True(ResourceMaterializationContract.ProductsEqualExact(
            0.1m,
            0.1m,
            0.05m,
            0.2m));
    }

    [Fact]
    public void ValidateRawDefinitionFields_RejectsClientOwnedFieldsAtAnyDepth()
    {
        using var document = JsonDocument.Parse("""
        {
          "resourceKey": "mana",
          "capacityPolicy": {
            "kind": "instance_fixed",
            "seal": "forged"
          },
          "materialization": {
            "definitionId": "forged"
          }
        }
        """);

        var issues = ResourceMaterializationContract.ValidateRawDefinitionFields(
            document.RootElement,
            "definition");

        Assert.Contains(issues, issue =>
            issue.Code == "resource_definition_client_field_forbidden" &&
            issue.FilePath == "definition.capacityPolicy.seal");
        Assert.Contains(issues, issue =>
            issue.Code == "resource_definition_client_field_forbidden" &&
            issue.FilePath == "definition.materialization");
    }

    private static string CreateDefinitionsRoot(int count)
    {
        var definitions = string.Join(',', Enumerable.Repeat("{}", count));
        return $"{{\"schemaVersion\":1,\"definitions\":[{definitions}]}}";
    }
}
