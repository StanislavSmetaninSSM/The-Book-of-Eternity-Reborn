using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExampleDocumentationValidationTests
{
    private static readonly string[] RegisteredSpiritualWoundProfiles =
        SpiritualWoundEffectProfileCatalog.RegisteredProfiles
            .OrderBy(static profile => profile, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void WoundMaterializationManifest_CoversProfileMatrixAndWorkedSourceGraph()
    {
        var manifest = ExampleValidationManifest.Load();
        var required = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["wound_spiritual_profiles_v1"] = "E_CLI_Effect_Materialization.txt",
            ["wound_spiritual_source_worked_v1"] = "E_CLI_Effect_Materialization.txt"
        };

        foreach (var (contractId, expectedFile) in required)
        {
            var entry = Assert.Single(
                manifest.EffectMaterializationCoverage,
                candidate => string.Equals(
                    candidate.ContractId,
                    contractId,
                    StringComparison.Ordinal));
            Assert.Equal(expectedFile, entry.File);
            Assert.Contains("Chaos Sea", entry.Realms, StringComparer.Ordinal);
            Assert.Contains("Shining Abode", entry.Realms, StringComparer.Ordinal);
            AssertTruthfulValidationMetadata(entry);
            Assert.NotEmpty(entry.RequiredText);

            var example = File.ReadAllText(Path.Combine(
                TestRepoPaths.RepoRoot,
                "Examples",
                expectedFile));
            Assert.All(entry.RequiredText, token =>
                Assert.Contains(token, example, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void SpiritualWoundProfileExample_UsesExactClosedRuntimeCatalog()
    {
        Assert.Equal(8, RegisteredSpiritualWoundProfiles.Length);
        var root = Assert.Single(ParseNamedJsonFences(
            "E_CLI_Effect_Materialization.txt",
            "wound_spiritual_profiles_v1"));
        var fragments = Assert.IsType<JsonArray>(
                root["registeredSpiritualWoundProfileFragments"])
            .OfType<JsonObject>()
            .ToArray();

        Assert.Equal(
            RegisteredSpiritualWoundProfiles,
            fragments
                .Select(fragment => fragment["profile"]!.GetValue<string>())
                .OrderBy(static profile => profile, StringComparer.Ordinal));

        foreach (var fragment in fragments)
        {
            var profile = fragment["profile"]!.GetValue<string>();
            var descriptor = SpiritualWoundEffectProfileCatalog.Profiles[profile];
            var payload = Assert.IsType<JsonObject>(fragment["payload"]);
            Assert.Equal(descriptor.Axis, payload["axis"]!.GetValue<string>());
            Assert.Equal(
                new[] { "axis", "magnitude", "operation" },
                payload.Select(static property => property.Key)
                    .OrderBy(static property => property, StringComparer.Ordinal));

            foreach (var realm in new[] { "chaos_sea", "shining_abode" })
            {
                var definition = EffectMaterializationTestFixture
                    .CreateSpiritualWoundDefinition(
                        profile,
                        realm: realm,
                        definitionKey: "definition_documented_" + profile);
                definition["components"] = new JsonArray(fragment.DeepClone());
                definition["triggers"]![0]!["componentIds"] = new JsonArray(
                    fragment["componentId"]!.GetValue<string>());
                using var document = JsonDocument.Parse(
                    new JsonArray(definition).ToJsonString());
                Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
                    document.RootElement,
                    $"woundSpiritualProfiles.{profile}",
                    realm));
            }
        }
    }

    [Fact]
    public void SpiritualWoundWorkedExample_IsCompleteGmSourceGraphWithoutClientIds()
    {
        var root = Assert.Single(ParseNamedJsonFences(
            "E_CLI_Effect_Materialization.txt",
            "wound_spiritual_source_worked_v1"));
        var decision = Assert.Single(
            Assert.IsType<JsonArray>(root["woundDecisions"])
                .OfType<JsonObject>());
        Assert.Equal("materialize", decision["decision"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(
            decision["woundRef"]!.GetValue<string>()));
        var proposal = Assert.IsType<JsonObject>(decision["proposal"]);
        var definitions = Assert.IsType<JsonArray>(
                proposal["consequenceDefinitions"])
            .OfType<JsonObject>()
            .ToArray();
        Assert.NotEmpty(definitions);

        var documentedProfiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var wrapper in definitions)
        {
            Assert.False(string.IsNullOrWhiteSpace(
                wrapper["definitionRef"]!.GetValue<string>()));
            var definition = Assert.IsType<JsonObject>(wrapper["definition"]);
            Assert.Empty(Assert.IsType<JsonArray>(definition["links"]));
            var rootDescriptor = Assert.IsType<JsonObject>(wrapper["root"]);
            Assert.NotEmpty(Assert.IsType<JsonArray>(rootDescriptor["slots"]));
            foreach (var component in Assert.IsType<JsonArray>(
                         definition["components"]).OfType<JsonObject>())
            {
                documentedProfiles.Add(component["profile"]!.GetValue<string>());
            }

            var clientBound = definition.DeepClone().AsObject();
            clientBound["links"] = new JsonArray(new JsonObject
            {
                ["kind"] = "wound",
                ["targetId"] = "wound_documentation_validation",
                ["role"] = "source"
            });
            foreach (var realm in new[] { "chaos_sea", "shining_abode" })
            {
                clientBound["allowedRealms"] = new JsonArray(realm);
                using var document = JsonDocument.Parse(
                    new JsonArray(clientBound.DeepClone()).ToJsonString());
                Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
                    document.RootElement,
                    "woundSpiritualWorkedSource",
                    realm));
            }
        }

        Assert.All(documentedProfiles, profile =>
            Assert.Contains(profile, RegisteredSpiritualWoundProfiles));
        var serialized = root.ToJsonString();
        foreach (var forbidden in new[]
                 {
                     "woundId",
                     "effectId",
                     "complicationId",
                     "transitionId",
                     "applicationRef",
                     "effectChanges"
                 })
        {
            Assert.DoesNotContain(forbidden, serialized, StringComparison.Ordinal);
        }
    }
}
