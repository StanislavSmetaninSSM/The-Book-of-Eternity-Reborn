using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalItemProjectionRootParseResult(
    JsonNode? Root,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Preserves the exact missing/present distinction and supported top-level
/// topology for the closed Mortal-item projection graph.
/// </summary>
internal static class MortalItemProjectionRootParser
{
    internal static MortalItemProjectionRootParseResult Parse(
        string? json,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (json is null)
        {
            return new MortalItemProjectionRootParseResult(
                null,
                Array.Empty<ValidationIssue>());
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var duplicateIssues = new List<ValidationIssue>();
            ResourceMaterializationContract.FindDuplicateProperties(
                document.RootElement,
                path,
                duplicateIssues,
                "mortal_item_projection_root_duplicate_property");
            if (duplicateIssues.Count != 0)
                return Invalid(path, "duplicate object property");

            var isVehicles = string.Equals(
                path,
                StorageTransportMoveService.VehiclesPath,
                StringComparison.Ordinal);
            var supportedTopology = document.RootElement.ValueKind ==
                                    JsonValueKind.Object ||
                                    isVehicles && document.RootElement.ValueKind ==
                                    JsonValueKind.Array;
            if (!supportedTopology)
            {
                return Invalid(
                    path,
                    document.RootElement.ValueKind.ToString());
            }

            var root = JsonNode.Parse(document.RootElement.GetRawText());
            if (root is null)
                return Invalid(path, "JSON null");
            return new MortalItemProjectionRootParseResult(
                root,
                Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or
            ArgumentException)
        {
            return Invalid(path, exception.Message);
        }
    }

    internal static JsonObject? ToCarrierCatalogObject(
        JsonNode? root,
        string path) => root switch
        {
            null => null,
            JsonObject obj => obj.DeepClone().AsObject(),
            JsonArray array when string.Equals(
                path,
                StorageTransportMoveService.VehiclesPath,
                StringComparison.Ordinal) => new JsonObject
                {
                    ["vehicles"] = array.DeepClone()
                },
            _ => throw new InvalidOperationException(
                $"Projection root '{path}' has unsupported topology.")
        };

    private static MortalItemProjectionRootParseResult Invalid(
        string path,
        string actual) => new(
        null,
        new[]
        {
            new ValidationIssue(
                path,
                IssueSeverity.Error,
                "A present Mortal-item projection root must contain strict JSON with the registered top-level topology.",
                code: "mortal_item_projection_root_invalid",
                actor: "mortal_item:projection",
                section: "MortalItemMaterialization",
                expected: string.Equals(
                    path,
                    StorageTransportMoveService.VehiclesPath,
                    StringComparison.Ordinal)
                    ? "strict object or legacy array root, or proven file absence"
                    : "strict object root, or proven file absence",
                actual: actual,
                repairHint:
                    "Restore the named projection root from the validated snapshot; do not replace present invalid bytes with an absent-root assumption.",
                repairTargetFiles: new[] { path })
        });
}
