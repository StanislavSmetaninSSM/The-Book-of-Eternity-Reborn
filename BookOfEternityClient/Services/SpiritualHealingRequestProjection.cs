using BookOfEternityClient.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>One shared scalar program and GM graph references; never publication authority.</summary>
internal static class SpiritualHealingRequestProjection
{
    internal static (WoundMaterializationEnvelope[] Wounds, SpiritualWoundStage[] Stages) BuildProgram(
        AfterlifeWoundTreatmentAuthority.SourceData data, JsonObject request, SpiritualHealingResolution resolution)
    {
        var attempt = request["attempt"]!.AsObject();
        var wounds = data.Catalog.Occurrences.Where(o => o.Wound.Owner == data.Wound.Owner &&
                o.Wound.Classification.Domain == "spiritual" && o.Wound.Lifecycle == "active" && o.Wound.Severity.Rank is >= 1 and <= 4)
            .Select(o => o.Wound).OrderBy(w => w.WoundId == data.Wound.WoundId ? 0 : 1)
            .ThenBy(w => w.WoundId, StringComparer.Ordinal).ToArray();
        var stages = wounds.SelectMany(w => SpiritualWoundRecoveryPlanner.Build(w, data.Source.HealingTier,
            attempt["attemptId"]!.GetValue<string>(), attempt["cycleKey"]!.GetValue<string>(),
            w.WoundId == data.Wound.WoundId ? resolution.Calculation : null)).ToArray();
        return (wounds, stages);
    }

    internal static IEnumerable<(string WoundId, int Rank)> RequiredGraphs(
        IReadOnlyList<WoundMaterializationEnvelope> wounds, IReadOnlyList<SpiritualWoundStage> stages) =>
        stages.Where(s => s.Kind != "heal" && s.SeverityRank < wounds.Single(w => w.WoundId == s.WoundId).Severity.Rank)
            .Select(s => (s.WoundId, s.SeverityRank)).Distinct();

    internal static string GraphRef(string requestFingerprint, string woundId, int rank) =>
        SpiritualWoundRecoveryPlanner.Coordinate("spiritual_healing_graph", requestFingerprint, woundId, rank);
    internal static string ComplicationRef(string requestFingerprint, string id) =>
        SpiritualWoundRecoveryPlanner.Coordinate("spiritual_complication_ref", requestFingerprint, id, 0);
    internal static JsonArray Complications(string requestFingerprint, WoundMaterializationEnvelope wound) => new(
        wound.Complications.Select(c => (JsonNode)new JsonObject
        { ["complicationRef"] = ComplicationRef(requestFingerprint, c.ComplicationId), ["displayName"] = c.DisplayName }).ToArray());

    internal static JsonObject Create(AfterlifeWoundTreatmentAuthority.SourceData data, JsonObject request,
        SpiritualHealingResolution? resolved = null)
    {
        var attempt = request["attempt"]!.AsObject();
        var resolution = resolved ?? SpiritualHealingResolver.ResolvePrepared(data.Source,
            attempt["roll"]!["mode"]!.GetValue<string>(), attempt["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray()).Resolution
            ?? throw new InvalidDataException("The retained healing request lost its scalar program.");
        var program = BuildProgram(data, request, resolution);
        var fingerprint = request["requestFingerprint"]!.GetValue<string>();
        return new JsonObject
        {
            ["schemaVersion"] = 1, ["requestFingerprint"] = fingerprint,
            ["attemptId"] = attempt["attemptId"]!.DeepClone(), ["woundName"] = data.Wound.Display.Name,
            ["healingTier"] = data.Source.HealingTier, ["roll"] = attempt["roll"]!.DeepClone(),
            ["activeResult"] = JsonSerializer.SerializeToNode(resolution.Calculation, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            ["destinationGraphs"] = new JsonArray(RequiredGraphs(program.Wounds, program.Stages).Select(g => (JsonNode)new JsonObject
            {
                ["stageRef"] = GraphRef(fingerprint, g.WoundId, g.Rank), ["severityRank"] = g.Rank,
                ["woundName"] = program.Wounds.Single(w => w.WoundId == g.WoundId).Display.Name,
                ["complications"] = Complications(fingerprint, program.Wounds.Single(w => w.WoundId == g.WoundId))
            }).ToArray())
        };
    }
}
