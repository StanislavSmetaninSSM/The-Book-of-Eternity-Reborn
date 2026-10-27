using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Ordinary extraction boundaries; these tests confer no HOT/history authority.</summary>
public sealed class AcceptedTurnNarrativeInterfacePolicyBoundaryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Narrative")]
    [InlineData("Interface")]
    public async Task PublicOrdinaryOwner_ConsumesSharedPolicyStages(string owner)
    {
        using var fixture = new OrdinaryOutputFixture(output);
        await fixture.SeedValidAsync();
        var issues = owner == "Narrative"
            ? await fixture.Validator.ValidateAcceptedTurnNarrativePayloadAsync()
            : await fixture.Validator.ValidateAcceptedTurnInterfacePayloadAsync();
        Assert.Empty(issues);
        fixture.AssertSeedBytesUnchanged();
        output.WriteLine("REAL_ORDINARY_PREREQUISITES_COMPLETE owner=" + owner);

        var prefix = "EvaluateAccepted" + owner + "Prefix";
        var tail = "EvaluateAccepted" + owner + "Tail";
        var source = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,
            "BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs"));
        var start = source.IndexOf("private async Task<List<ValidationIssue>> ValidateAcceptedTurn" + owner + "PayloadInternalAsync", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = source.IndexOf("\n    private ", start + 1, StringComparison.Ordinal);
        var body = source[start..end];
        var first = body.IndexOf(prefix + "(", StringComparison.Ordinal);
        var timestamp = body.IndexOf("ValidateRequiredIsoTimestampField(", StringComparison.Ordinal);
        var last = body.IndexOf(tail + "(", StringComparison.Ordinal);
        Assert.True(first >= 0 && first < timestamp && timestamp < last,
            "The actual ordinary owner must consume prefix, the existing ambient timestamp leaf, then tail.");
    }

    [Fact]
    public void SharedExtraction_ReversesToExactReviewedOrdinarySource()
    {
        var root = TestRepoPaths.RepoRoot;
        var inventory = JsonNode.Parse(File.ReadAllText(Path.Combine(root,
            "BookOfEternityClient.Tests/TestData/t179pd3d-ordinary-source-segments.json")))!.AsObject();
        var segments = inventory["segments"]!.AsObject();
        string Segment(string key) => segments[key]!.GetValue<string>();
        var source = File.ReadAllText(Path.Combine(root, inventory["sourcePath"]!.GetValue<string>()));
        // Reverse only a strict future 3e extraction to the accepted 3d A first;
        // the four original 3d adapters and original6a9 hash below stay unchanged.
        source = DebugPrefixSourceInventory.ReverseIfPresent(source);
        source = NarrativePolicySourceInventory.ReverseIfPresent(source);
        source = ReasoningPolicySourceInventory.ReverseIfPresent(source);
        // Reverse only the new signed spiritual-field adapter. The ordinary owner and
        // entire pure policy still reconstruct to the same original reviewed hash.
        const string spiritualAdapter = "            await using var boundSpiritualInterface = await BindSpiritualHealingInterfaceAsync(doc.RootElement, json, issues);\n            var policyRoot = boundSpiritualInterface?.Root ?? doc.RootElement;\n";
        Assert.Equal(1, source.Split(spiritualAdapter, StringSplitOptions.None).Length - 1);
        source = source.Replace(spiritualAdapter, string.Empty, StringComparison.Ordinal)
            .Replace("EvaluateAcceptedInterfacePrefix(policyRoot, interfacePath, issues)", "EvaluateAcceptedInterfacePrefix(doc.RootElement, interfacePath, issues)", StringComparison.Ordinal)
            .Replace("ValidateRequiredIsoTimestampField(\n                policyRoot,\n                interfacePath,", "ValidateRequiredIsoTimestampField(\n                doc.RootElement,\n                interfacePath,", StringComparison.Ordinal)
            .Replace("EvaluateAcceptedInterfaceTail(policyRoot, interfacePath, issues)", "EvaluateAcceptedInterfaceTail(doc.RootElement, interfacePath, issues)", StringComparison.Ordinal);
        var partialPath = Path.Combine(root,
            "BookOfEternityClient/Services/Validation/ValidationService.NarrativeInterfacePolicy.cs");
        Assert.True(File.Exists(partialPath), "The shared value-only policy partial has not been implemented.");

        var narrativePrefix = Segment("narrativePrefix")
            .Replace("            string responseText = string.Empty;\n", string.Empty, StringComparison.Ordinal)
            .Replace("return issues;", "return false;", StringComparison.Ordinal);
        narrativePrefix = "            responseText = string.Empty;\n" + narrativePrefix;
        var narrativeTail = Segment("narrativeTail")
            .Replace("                await ValidateAchievementUnlockNarrativeMarkersAsync(responseText, issues);\n", string.Empty, StringComparison.Ordinal)
            .Replace("                await ValidateAcceptedTurnProseStateDeltasAsync(responseText, issues);\n", string.Empty, StringComparison.Ordinal);
        var interfacePrefix = Segment("interfacePrefix").Replace("return issues;", "return false;", StringComparison.Ordinal);
        string Method(string signature, string body, bool returns) =>
            "    private " + signature + "\n    {\n" +
            body.Replace("doc.RootElement", "root", StringComparison.Ordinal)
                .Replace("\n            ", "\n        ", StringComparison.Ordinal).TrimStart(' ').Insert(0, "        ") +
            (returns ? "        return true;\n" : string.Empty) + "    }\n";
        var expected = "using System.Text.Json;\n\nnamespace BookOfEternityClient.Services;\n\npublic partial class ValidationService\n{\n" +
            Method("bool EvaluateAcceptedNarrativePrefix(JsonElement root, List<ValidationIssue> issues, out string responseText)", narrativePrefix, true) + "\n" +
            Method("void EvaluateAcceptedNarrativeTail(JsonElement root, string responseText, List<ValidationIssue> issues)", narrativeTail, false) + "\n" +
            Method("bool EvaluateAcceptedInterfacePrefix(JsonElement root, string interfacePath, List<ValidationIssue> issues)", interfacePrefix, true) + "\n" +
            Method("void EvaluateAcceptedInterfaceTail(JsonElement root, string interfacePath, List<ValidationIssue> issues)", Segment("interfaceTail"), false) + "}\n";
        Assert.Equal(expected, File.ReadAllText(partialPath));

        var replacements = new Dictionary<string, string>
        {
            ["            if (!EvaluateAcceptedNarrativePrefix(doc.RootElement, issues, out var responseText))\n                return issues;\n\n"] = Segment("narrativePrefix"),
            ["            EvaluateAcceptedNarrativeTail(doc.RootElement, responseText, issues);\n\n            if (!string.IsNullOrWhiteSpace(responseText))\n            {\n                await ValidateAchievementUnlockNarrativeMarkersAsync(responseText, issues);\n                await ValidateAcceptedTurnProseStateDeltasAsync(responseText, issues);\n            }\n"] = Segment("narrativeTail"),
            ["            if (!EvaluateAcceptedInterfacePrefix(doc.RootElement, interfacePath, issues))\n                return issues;\n\n"] = Segment("interfacePrefix"),
            ["            EvaluateAcceptedInterfaceTail(doc.RootElement, interfacePath, issues);\n"] = Segment("interfaceTail")
        };
        foreach (var (adapter, original) in replacements)
        {
            Assert.Equal(1, source.Split(adapter, StringSplitOptions.None).Length - 1);
            source = source.Replace(adapter, original, StringComparison.Ordinal);
        }
        Assert.Equal(inventory["sourceSha256"]!.GetValue<string>(),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant());
    }

}
