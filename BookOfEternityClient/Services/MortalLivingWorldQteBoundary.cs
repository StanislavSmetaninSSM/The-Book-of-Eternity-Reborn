using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Detached QTE clock admission DATA only; current physical ownership remains with each caller.</summary>
internal static class MortalLivingWorldQteBoundary
{
    internal static IReadOnlyList<ValidationIssue> Validate(JsonObject? currentClock,
        JsonObject? currentSchedule, JsonObject responseFragment) =>
        throw new NotImplementedException("QTE living-world clock admission is not implemented.");
}
