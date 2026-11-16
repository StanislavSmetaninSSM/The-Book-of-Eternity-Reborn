using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Refuses additional elapsed-cycle work on the local QTE owner, which has no sealed living-world report contribution.
/// Detached arithmetic never authenticates an ordinary original or grants permission to publish a report.
/// </summary>
internal static class MortalLivingWorldQteBoundary
{
    internal static IReadOnlyList<ValidationIssue> Validate(JsonObject? currentClock,
        JsonObject? currentSchedule, JsonObject responseFragment)
    {
        try
        {
            var deltaNode = responseFragment["timeChange"];
            var absoluteNode = responseFragment["setWorldTime"];
            if (deltaNode is null && absoluteNode is null) return [];
            if (deltaNode is not null && absoluteNode is not null)
                return InvalidClock("QTE cannot combine incremental and absolute clock commands.");
            int delta = 0;
            if (deltaNode is not null && (!Integer(deltaNode, out delta) || delta < 0))
                return InvalidClock("QTE elapsed minutes must be a nonnegative integer.");
            if (absoluteNode is null && delta == 0) return [];
            if (currentClock is null || currentSchedule is null ||
                currentClock["timeChange"] is not null || currentClock["setWorldTime"] is not null ||
                !Integer(currentClock["currentTimeInMinutes"], out var current) || current < 0 ||
                !RealmSemantics.IsMortalRealm(currentSchedule["currentRealm"]?.GetValue<string>()) ||
                !Integer(currentSchedule["currentWorldTimeInMinutes"], out var scheduled) || scheduled != current ||
                !Integer(currentSchedule["lastWorldSimulationTimeInMinutes"], out var lastWorld) || lastWorld < 0 || lastWorld > current ||
                !Integer(currentSchedule["lastFactionSimulationTimeInMinutes"], out var lastFaction) || lastFaction < 0 || lastFaction > current ||
                !Integer(currentSchedule["worldCycleMinutes"], out var worldCycle) || worldCycle <= 0 ||
                !Integer(currentSchedule["factionCycleMinutes"], out var factionCycle) || factionCycle <= 0)
                return InvalidClock("QTE needs one settled current Mortal clock and agreeing current progression schedule.");
            int resulting;
            if (absoluteNode is not null)
            {
                if (absoluteNode is not JsonObject absolute ||
                    !Integer(absolute["currentTimeInMinutes"], out resulting) || resulting < current)
                    return InvalidClock("QTE absolute time must resolve numerically without reversing the current clock.");
            }
            else resulting = checked(current + delta);
            if ((resulting - lastWorld) / worldCycle > (current - lastWorld) / worldCycle ||
                (resulting - lastFaction) / factionCycle > (current - lastFaction) / factionCycle)
                return Issue("time_unsupported", "This QTE outcome introduces another world/faction cycle without a sealed living-world assessment.");
            return [];
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException or OverflowException)
        { return InvalidClock("The QTE clock command or current clock/schedule is malformed or unresolved."); }
    }

    /// <summary>Reads current operation DATA once for the supplied candidates; never reuses a retained ordinary assessment.</summary>
    internal static async Task<IReadOnlyList<ValidationIssue>> ValidateCurrentAsync(
        Func<string, Task<byte[]?>> readCurrent, IReadOnlyList<JsonObject> fragments)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        var report = await readCurrent(MortalLivingWorldSource.ReportPath);
        var hasClockCommand = fragments.Any(fragment => fragment["timeChange"] is not null || fragment["setWorldTime"] is not null);
        if (report is null && !hasClockCommand) return [];
        try
        {
            var soul = Root(await readCurrent("game_state/meta/soul_state.json"), "QTE current soul");
            if (soul is not null && !RealmSemantics.IsMortalRealm(soul["currentRealm"]?.GetValue<string>())) return [];
            if (report is not null)
                return Issue("report_unsupported", "A supplied living-world report cannot be consumed by the local QTE owner, including malformed report bytes.");
            if (soul is null) return InvalidClock("A QTE clock command requires a current Mortal soul.");
            var clock = Root(await readCurrent(MortalLivingWorldSource.WorldTimePath), "QTE current clock");
            var schedule = Root(await readCurrent(ProgressionScheduleService.SchedulePath), "QTE current schedule");
            return fragments.SelectMany(fragment => Validate(clock, schedule, fragment)).ToArray();
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException or InvalidDataException)
        { return InvalidClock("Current physical QTE clock/schedule DATA cannot be read as strict JSON objects."); }
    }

    internal static Task<IReadOnlyList<ValidationIssue>> ValidateCurrentAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, IReadOnlyList<JsonObject> fragments)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        return ValidateCurrentAsync(path => files.ReadFileBytesAsync(lease, path), fragments);
    }

    internal static void ThrowIfRejected(IReadOnlyList<ValidationIssue> issues)
    {
        if (issues.Count != 0)
            throw new InvalidDataException(string.Join("; ", issues.Select(issue => issue.Code + ": " + issue.Message + " " + issue.RepairHint)));
    }

    private static bool Integer(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue scalar && scalar.TryGetValue(out value);
    }
    private static JsonObject? Root(byte[]? bytes, string name) => bytes is null ? null :
        StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), name)
        ?? throw new InvalidDataException(name + " must be an object.");
    private static IReadOnlyList<ValidationIssue> InvalidClock(string detail) => Issue("clock_invalid", detail);
    private static IReadOnlyList<ValidationIssue> Issue(string suffix, string detail) =>
        [new ValidationIssue(QteSceneService.QteOfferPath, IssueSeverity.Error, detail,
            code: "mortal_living_world_qte_" + suffix, section: "MortalLivingWorldQte",
            repairHint: "Resolve meaningful elapsed time through a normal assessed ordinary Mortal turn instead of this QTE. Preserve the original, accepted scene and private history; do not invent time, events or replacement report authority.")];
}
