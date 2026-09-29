using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Identifies an existing client timestamp produced by a pure accepted-turn projection.
/// </summary>
internal enum AcceptedTurnProjectionTimeKind
{
    /// <summary>
    /// Time assigned to an admitted memory grant.
    /// </summary>
    MemoryLegacyGrant,
    /// <summary>
    /// Fallback time assigned to an admitted archive resolution.
    /// </summary>
    ArchiveResolution,
    /// <summary>
    /// Fallback time assigned to an effective spiritual conflict closure.
    /// </summary>
    ConflictResolution,
    /// <summary>
    /// Time retained for a survival outcome's consumption projection.
    /// </summary>
    SurvivalConsumption,
    /// <summary>
    /// Fallback time assigned to the original accepted-turn interface output.
    /// </summary>
    InterfaceOutputTimestamp
}

/// <summary>
/// Supplies ordinary current time while allowing a capture to retain pure projection timestamps.
/// </summary>
internal class AcceptedTurnProjectionClock
{
    /// <summary>
    /// Reads current UTC time for one owner-supplied causal projection.
    /// </summary>
    /// <param name="role">
    /// Defined timestamp responsibility; it does not authorize the projection.
    /// </param>
    /// <param name="evidence">
    /// Non-null detached causal input whose admission remains the owner's responsibility.
    /// </param>
    /// <returns>
    /// Current UTC time under the ordinary policy.
    /// </returns>
    internal virtual DateTimeOffset GetUtcNow(AcceptedTurnProjectionTimeKind role, JsonObject evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        return DateTimeOffset.UtcNow;
    }
}
