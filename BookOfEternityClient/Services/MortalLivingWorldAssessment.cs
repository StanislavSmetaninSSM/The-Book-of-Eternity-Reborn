using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Detached clock/source DATA supplied by the owning caller; this record is not authentication or publication authority.</summary>
internal sealed record MortalLivingWorldAssessmentContext(string SessionId, string RequestId, int Turn,
    long OriginalMinute, long ResultMinute, long LastWorldMinute, long LastFactionMinute,
    int WorldCycleMinutes, int FactionCycleMinutes);

/// <summary>Admission-only assessment shape and timing checks; canonical effects and source ownership require separate runtime validation.</summary>
internal static class MortalLivingWorldAssessment
{
    /// <summary>Checks a detached report against already acquired clock/source DATA without reading, writing or granting authority.</summary>
    /// <param name="context">The caller's original/result clock and source identity.</param>
    /// <param name="report">The candidate private report, or null when absent.</param>
    /// <returns>Bounded admission issues, not canonical effect or publication acceptance.</returns>
    internal static IReadOnlyList<ValidationIssue> Validate(MortalLivingWorldAssessmentContext context, JsonObject? report) =>
        throw new NotImplementedException("#1516 admission contract RED scaffold");
}
