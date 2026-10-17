using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExampleDocumentationValidationTests
{
    [Fact]
    public void MovedNamedConsumersResolveEveryFrozenSectionThroughTheActualParsers()
    {
        var bytes = File.ReadAllBytes(Path.Combine(TestRepoPaths.RepoRoot,
            "specs/1557-gm-doc-modularization/named-consumer-inputs.json"));
        Assert.Equal("92dcf515fe28f2c5293cac321878d33357011dcb6d16c956dd711d167f3602d5", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        var rows = JsonNode.Parse(bytes)!["rows"]!.AsArray();
        Assert.Equal(23, rows.Count);
        Assert.Equal(135, rows.Sum(row => row!["rawBodies"]!.AsArray().Count));
        foreach (var row in rows)
        {
            var file = row!["file"]!.GetValue<string>();
            var contract = row["contractId"]!.GetValue<string>();
            var expected = row["rawBodies"]!.AsArray().Select(body => body!.GetValue<string>()).ToArray();
            Assert.Equal(expected[0], ExtractNamedJsonFence(file, contract));
            var actual = ParseNamedJsonFences(file, contract);
            Assert.Equal(expected.Length, actual.Count);
            for (var i = 0; i < expected.Length; i++)
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected[i]), actual[i]), contract + ":" + i);
        }
    }
    [Theory]
    [InlineData("afterlife_effect_conditions_five_kinds_v1", "missing")]
    [InlineData("afterlife_effect_conditions_five_kinds_v1", "reordered")]
    [InlineData("afterlife_effect_conditions_projection_v1", "missing")]
    [InlineData("afterlife_effect_conditions_projection_v1", "reordered")]
    public void CrossOwnerNamedViewsRefuseMissingOrReorderedActualSegments(string contract, string mutation)
    {
        var section = GmDocumentationNamedSectionSource.Retrieve(TestRepoPaths.RepoRoot,
            "E_CLI_Afterlife_Effects_Resources.txt", contract);
        Assert.Equal(5, section.Segments.Count);
        var complete = GmDocumentationNamedSectionSource.Assemble(section);
        Assert.StartsWith("##" + (contract.Contains("projection", StringComparison.Ordinal) ? "# " : " ") + contract,
            complete, StringComparison.Ordinal);
        var pieces = section.Segments.ToList();
        if (mutation == "missing") pieces.RemoveAt(2);
        else pieces.Reverse();
        var error = Assert.Throws<InvalidDataException>(() =>
            GmDocumentationNamedSectionSource.Assemble(section with { Segments = pieces }));
        Assert.StartsWith("named.section_order_or_bytes:" + contract, error.Message, StringComparison.Ordinal);
    }

}
