using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Checks canonical display DATA against the existing topology producer, never accepting raw commands.</summary>
    private async Task ValidateCanonicalCurrentDerivedFieldsAsync(JsonElement root, string path, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("activeEffectDefinitions", out var definitions))
            issues.AddRange(EffectSourceDefinitionContract.ValidateArray(definitions, path + ".activeEffectDefinitions", "mortal_world"));
        var needsMap = false;
        var hasDerived = false;
        foreach (var name in new[] { "knownExits", "adjacencyMap" })
        {
            if (!root.TryGetProperty(name, out var value)) continue;
            hasDerived = true;
            if (value.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue(path + "." + name, IssueSeverity.Error,
                    "Canonical derived topology must be an array.", code: "mortal_location_derived_topology_invalid"));
                return;
            }
            needsMap |= value.GetArrayLength() != 0;
        }
        if (!hasDerived) return;
        var mapText = await ReadSameTurnOwnerCurrentTextAsync(MortalLocationMaterializationContract.WorldMapPath);
        var map = string.IsNullOrWhiteSpace(mapText) ? null : JsonNode.Parse(mapText) as JsonObject;
        if (map is null && !needsMap) return; // Shape-only inspection; required-file validation owns absence.
        if (map?["locations"] is not JsonArray locations || map["links"] is not JsonArray links)
        {
            issues.Add(new ValidationIssue(path, IssueSeverity.Error, "Derived topology requires a canonical map.",
                code: "mortal_location_derived_topology_invalid"));
            return;
        }
        var actual = JsonNode.Parse(root.GetRawText())!.AsObject();
        var expected = actual.DeepClone().AsObject();
        MortalLocationAcceptedTurnPlanner.RebuildDerivedCurrentTopology(expected, locations, links);
        foreach (var name in new[] { "knownExits", "adjacencyMap" })
            if (!JsonNode.DeepEquals(actual[name] ?? new JsonArray(), expected[name] ?? new JsonArray()))
                issues.Add(new ValidationIssue(path + "." + name, IssueSeverity.Error,
                    "Canonical derived topology differs from its current map.", code: "mortal_location_derived_topology_invalid"));
    }
}
