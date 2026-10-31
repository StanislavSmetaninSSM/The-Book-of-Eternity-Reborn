using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
    /// <summary>
    /// Compares the complete ordinary Mortal schedule against its authenticated
    /// original and the correlated legal outcome set. It never infers that an
    /// optional report existed or creates a new report, owner or time charge.
    /// Only the actual preparation's UTC timestamp is nondeterministic; its exact
    /// round-trip UTC shape is checked under the existing capture policy.
    /// </summary>
    internal static void RequirePublishedMortalTreatmentOutcome(ProgressionControl control,
        string sessionId, string requestId, int turn, byte[] originalScheduleBytes,
        byte[]? currentClockBytes, byte[]? currentScheduleBytes, byte[]? currentReportBytes)
    {
        if (control.CurrentRealm != "Mortal World" || currentClockBytes is null || currentScheduleBytes is null ||
            currentReportBytes is not null || control.WorldCycleMinutes <= 0 || control.FactionCycleMinutes <= 0)
            throw new InvalidDataException("Physical completion requires its settled Mortal clock/schedule and consumed report.");
        var original = JsonSerializer.Deserialize<ProgressionScheduleState>(CanonicalJsonUtf8.DecodeOneOptionalBom(originalScheduleBytes), JsonOpts)
            ?? throw new InvalidDataException("Missing original physical progression schedule.");
        var originalRoot = JsonSerializer.SerializeToNode(original, JsonOpts);
        if (!JsonNode.DeepEquals(originalRoot, JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(originalScheduleBytes))) ||
            original.CurrentRealm != control.CurrentRealm || original.CurrentWorldTimeInMinutes != control.CurrentWorldTimeInMinutes)
            throw new InvalidDataException("Original physical schedule is not its closed current control source.");
        var originalClock = EncodeUtf8WithPreamble(new JsonObject
        { ["currentTimeInMinutes"] = control.CurrentWorldTimeInMinutes }.ToJsonString());
        SanitizeFrozenSchedule(original, control.CurrentRealm, originalClock, null);
        if (!JsonNode.DeepEquals(originalRoot, JsonSerializer.SerializeToNode(original, JsonOpts)) ||
            !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(control, JsonOpts), JsonSerializer.SerializeToNode(
                BuildProgressionControl(original,
                    ComputeElapsedCycles(control.CurrentWorldTimeInMinutes, original.LastWorldSimulationTimeInMinutes, original.WorldCycleMinutes),
                    ComputeElapsedCycles(control.CurrentWorldTimeInMinutes, original.LastFactionSimulationTimeInMinutes, original.FactionCycleMinutes),
                    new AfterlifeCatchupContext(false, 0, "none", 0, Array.Empty<string>())), JsonOpts)))
            throw new InvalidDataException("Full physical original control does not reconstruct from its signed prepared schedule.");
        var world = ResolveFrozenWorldTime(currentClockBytes, control.CurrentWorldTimeInMinutes, allowIncrementalTimeChange: false);
        var currentRoot = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(currentScheduleBytes))?.AsObject()
            ?? throw new InvalidDataException("Missing current physical progression schedule.");
        var current = currentRoot.Deserialize<ProgressionScheduleState>(JsonOpts)
            ?? throw new InvalidDataException("Malformed current physical progression schedule.");
        if (world.HasUnresolvedAbsoluteOverride || world.Minutes < 0 || current.LastUpdatedUtc is not { } preparedAt)
            throw new InvalidDataException("Physical completion has an unresolved clock or missing preparation timestamp.");
        RequireCapturedUtc(preparedAt);
        RequireCapturedUtc(original.LastUpdatedUtc ?? throw new InvalidDataException("Missing original preparation timestamp."));

        // Whole candidate states are derived together through the actual ordinary
        // reducer. This is a legal comparison set, not independently allowed counters.
        bool Matches(bool reportConsumed)
        {
            var expected = originalRoot!.Deserialize<ProgressionScheduleState>(JsonOpts)!;
            SanitizeFrozenSchedule(expected, control.CurrentRealm, currentClockBytes, null);
            var report = reportConsumed ? new ProgressionProcessingReport
            {
                SessionId = sessionId, RequestId = requestId, TurnNumber = turn,
                WorldCyclesProcessed = Math.Max(0, control.WorldCyclesAlreadyPendingBeforeTurn),
                FactionCyclesProcessed = Math.Max(0, control.FactionCyclesAlreadyPendingBeforeTurn)
            } : null;
            // The shared reducer derives exact required last markers when the
            // optional comparison report omits their redundant values.
            ApplyMortalOutcomeFromInputs(expected, control, report, reportConsumed, world);
            expected.CurrentRealm = control.CurrentRealm;
            expected.LastUpdatedUtc = preparedAt;
            return JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected, JsonOpts), currentRoot);
        }
        var reportRequired = control.WorldCyclesAlreadyPendingBeforeTurn > 0 || control.FactionCyclesAlreadyPendingBeforeTurn > 0;
        if (!Matches(reportConsumed: true) && (reportRequired || !Matches(reportConsumed: false)))
            throw new InvalidDataException("Complete current physical schedule differs from every legal ordinary outcome of its original control.");
    }
}
