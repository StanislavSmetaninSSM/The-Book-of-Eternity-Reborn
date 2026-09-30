using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExampleDocumentationValidationTests
{
    [Fact]
    public void SpiritualWoundSourceEnvelope_DocumentedArtUsesProductionParser()
    {
        const string contractId = "spiritual_wound_source_envelope_v1";
        var json = ExtractNamedJsonFence("E_CLI_Afterlife_Turns.txt", contractId);
        using var source = JsonDocument.Parse(json);
        Assert.True(SpiritualWoundSourceEnvelope.TryRead(source.RootElement, out var envelope, out var error), error);
        Assert.NotNull(envelope);
        Assert.Equal(2, envelope.MaximumSeverityRank);
        Assert.Equal(1, envelope.GuaranteedSeverityRank);
        Assert.Equal("guardian", source.RootElement.GetProperty("ownerActorType").GetString());
        Assert.Equal("guardian_echo_seam", source.RootElement.GetProperty("ownerActorId").GetString());
        Assert.Equal("pressure", source.RootElement.GetProperty("baseOperation").GetString());
        Assert.Equal("sideStrain", source.RootElement.GetProperty("combatEffect").GetProperty("mechanicalAxis").GetString());

        // The two alternatives are derived from the actual authored source,
        // not a separate fixture that could drift away from the example.
        var ordinary = JsonNode.Parse(json)!.AsObject();
        Assert.True(ordinary.Remove(SpiritualWoundSourceEnvelope.Property));
        Assert.True(SpiritualWoundSourceEnvelope.TryRead(ordinary, out var ordinaryEnvelope, out error), error);
        Assert.NotNull(ordinaryEnvelope);
        Assert.Equal(4, ordinaryEnvelope.MaximumSeverityRank);
        Assert.Null(ordinaryEnvelope.GuaranteedSeverityRank);
        var contradictory = JsonNode.Parse(json)!.AsObject();
        contradictory[SpiritualWoundSourceEnvelope.Property]!["guaranteedSeverityRank"] = 3;
        Assert.False(SpiritualWoundSourceEnvelope.TryRead(contradictory, out _, out error));
        Assert.False(string.IsNullOrWhiteSpace(error));

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot, "Examples", "example_validation_manifest.json")));
        var contract = Assert.Single(
            manifest.RootElement.GetProperty("afterlifeEntityProfileCoverage").EnumerateArray(),
            row => row.GetProperty("contractId").GetString() == contractId);
        Assert.Equal("E_CLI_Afterlife_Turns.txt", contract.GetProperty("file").GetString());
        Assert.Equal("production-validator", contract.GetProperty("validationKind").GetString());
        Assert.Contains("SpiritualWoundSourceEnvelope.TryRead",
            contract.GetProperty("validationRoute").GetString()!, StringComparison.Ordinal);
        Assert.Contains("not a complete actor profile",
            contract.GetProperty("focusedFragmentReason").GetString()!, StringComparison.Ordinal);
        Assert.Contains("final publication",
            contract.GetProperty("coverageLimit").GetString()!, StringComparison.Ordinal);
        Assert.Equal(new[] { "Chaos Sea", "Shining Abode" },
            contract.GetProperty("realms").EnumerateArray().Select(row => row.GetString()).ToArray());
    }
}
