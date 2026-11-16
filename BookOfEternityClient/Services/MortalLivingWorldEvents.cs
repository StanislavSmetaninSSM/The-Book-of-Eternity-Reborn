using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Pure event/effect checks on caller-authenticated detached DATA; never publication authority.</summary>
internal static class MortalLivingWorldEvents
{
    /// <summary>Validates declared events against original and settled canonical documents, without reads or mutation.</summary>
    internal static IReadOnlyList<ValidationIssue> Validate(JsonObject report,
        IReadOnlyDictionary<string, JsonObject> original, IReadOnlyDictionary<string, JsonObject> current) =>
        throw new NotImplementedException("#1516 T006 event/effect RED scaffold");
}
