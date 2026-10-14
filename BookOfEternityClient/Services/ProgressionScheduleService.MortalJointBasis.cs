using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
    // Raw frozen data is never an accepted phase capability. A fresh engine
    // issuer must first authenticate original/control, physical G and sources.
    internal sealed record FrozenMortalCalculation(CanonicalBeforeImage ScheduleBefore,
        CanonicalBeforeImage ReportBefore, CanonicalBeforeImage ScheduleAfter,
        CanonicalBeforeImage ReportAfter, string MutationFingerprint);

    private static string ComputeAcceptedOutcomeMutationFingerprint(string controlJson,
        CanonicalBeforeImage scheduleBefore, CanonicalBeforeImage reportBefore,
        CanonicalBeforeImage scheduleAfter, CanonicalBeforeImage reportAfter) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.progression.accepted_turn_mutation", "1", controlJson,
            scheduleBefore.Fingerprint, reportBefore.Fingerprint,
            scheduleAfter.Fingerprint, reportAfter.Fingerprint
        });

    internal FrozenMortalCalculation RecomposeFrozenMortalCalculation(JsonObject frozen)
    {
        RequireFrozenKeys(frozen, ["version", "control", "sanitizedSchedule", "realmAfterTurn", "reportState",
            "reportPresent", "parsedReport", "requestContext", "worldMinutes", "worldUnresolvedAbsolute",
            "sanitizationUtc", "preparedAtUtc", "scheduleAfterBase64", "reportAfterBase64", "mutationFingerprint", "sourceBytes"]);
        if (frozen["version"] is not JsonValue version || !version.TryGetValue<int>(out var number) || number != 1)
            throw new InvalidDataException("Unknown frozen progression basis version.");
        var raw = frozen["sourceBytes"] as JsonObject ?? throw new InvalidDataException("Frozen progression sources are missing.");
        RequireFrozenKeys(raw, MortalJointCalculationSourcePaths);
        var sources = raw.ToDictionary(row => row.Key,
            row => row.Value is null ? null : Convert.FromBase64String(row.Value.GetValue<string>()), StringComparer.Ordinal);
        var controlNode = frozen["control"] as JsonObject ?? throw new InvalidDataException("Frozen progression control is missing.");
        var control = controlNode.Deserialize<ProgressionControl>(JsonOpts) ?? throw new InvalidDataException("Frozen progression control is null.");
        var controlJson = JsonSerializer.Serialize(control, JsonOpts);
        if (!JsonNode.DeepEquals(controlNode, JsonNode.Parse(controlJson)) ||
            !HasResolvedRealm(control.CurrentRealm) || IsAfterlifeRealm(control.CurrentRealm))
            throw new InvalidDataException("Frozen control is outside the exact Mortal shape.");
        var scheduleBytes = sources[SchedulePath] ?? throw new InvalidDataException("Frozen schedule absence cannot initialize progression.");
        var schedule = JsonSerializer.Deserialize<ProgressionScheduleState>(CanonicalJsonUtf8.DecodeOneOptionalBom(scheduleBytes), JsonOpts)
            ?? throw new InvalidDataException("Frozen schedule is null.");
        var realm = ResolveFrozenRealm(sources["game_state/meta/soul_state.json"]);
        var sanitizationUtc = frozen["sanitizationUtc"]?.GetValue<string>();
        SanitizeFrozenSchedule(schedule, realm, sources["game_state/world/world_time.json"], sanitizationUtc);
        var sanitizedJson = JsonSerializer.Serialize(schedule, JsonOpts);
        var report = ReadFrozenReport(sources[ReportPath]);
        var context = ReadFrozenRequestContext(sources);
        if (context is not { } selected) throw new InvalidDataException("Frozen request-context selection has no genuine tuple.");
        var world = ResolveFrozenWorldTime(sources["game_state/world/world_time.json"], control.CurrentWorldTimeInMinutes);
        var issues = new List<ValidationIssue>();
        if (!world.HasUnresolvedAbsoluteOverride)
            ValidateMortalOutcome(control, report, context, world.Minutes, issues);
        if (issues.Any(row => row.Severity == IssueSeverity.Error))
            throw new InvalidDataException("Frozen report semantics refused: " + string.Join(";", issues.Select(row => row.Code)));
        var consumed = ApplyMortalOutcomeFromInputs(schedule, control, report.Report,
            report.State == ProgressionFileReadState.Valid && ProgressionReportMatchesCurrentTurn(report.Report, context), world);
        if (HasResolvedRealm(realm)) schedule.CurrentRealm = realm;
        if (frozen["preparedAtUtc"] is not JsonValue preparedAtValue || !preparedAtValue.TryGetValue<string>(out var preparedAtUtc) || preparedAtUtc is null)
            throw new InvalidDataException("Frozen preparation UTC is missing or malformed.");
        RequireCapturedUtc(preparedAtUtc);
        schedule.LastUpdatedUtc = preparedAtUtc;
        var scheduleAfter = EncodeUtf8WithPreamble(JsonSerializer.Serialize(schedule, JsonOpts));
        var reportAfter = consumed && report.FilePresent ? null : sources[ReportPath];
        var result = new FrozenMortalCalculation(new(true, scheduleBytes.ToArray()),
            new(sources[ReportPath] is not null, sources[ReportPath]?.ToArray()),
            new(true, scheduleAfter), new(reportAfter is not null, reportAfter?.ToArray()),
            ComputeAcceptedOutcomeMutationFingerprint(controlJson, new(true, scheduleBytes),
                new(sources[ReportPath] is not null, sources[ReportPath]), new(true, scheduleAfter), new(reportAfter is not null, reportAfter)));
        var computed = new JsonObject
        {
            ["version"] = 1, ["control"] = JsonNode.Parse(controlJson), ["sanitizedSchedule"] = JsonNode.Parse(sanitizedJson),
            ["realmAfterTurn"] = realm, ["reportState"] = report.State.ToString(), ["reportPresent"] = report.FilePresent,
            ["parsedReport"] = report.Report is null ? null : JsonSerializer.SerializeToNode(report.Report, JsonOpts),
            ["requestContext"] = new JsonObject { ["sessionId"] = selected.SessionId, ["requestId"] = selected.RequestId, ["turn"] = selected.TurnNumber },
            ["worldMinutes"] = world.Minutes, ["worldUnresolvedAbsolute"] = world.HasUnresolvedAbsoluteOverride,
            ["sanitizationUtc"] = sanitizationUtc, ["preparedAtUtc"] = preparedAtUtc,
            ["scheduleAfterBase64"] = Convert.ToBase64String(scheduleAfter),
            ["reportAfterBase64"] = reportAfter is null ? null : Convert.ToBase64String(reportAfter),
            ["mutationFingerprint"] = result.MutationFingerprint
        };
        var computedRaw = new JsonObject();
        foreach (var path in MortalJointCalculationSourcePaths)
            computedRaw[path] = sources[path] is { } bytes ? Convert.ToBase64String(bytes) : null;
        computed["sourceBytes"] = computedRaw;
        if (!JsonNode.DeepEquals(frozen, computed))
            throw new InvalidDataException("Frozen progression calculation disagrees with independent raw-input recomposition.");
        return result;
    }

    private static void RequireFrozenKeys(JsonObject value, IEnumerable<string> keys)
    {
        if (!value.Select(row => row.Key).ToHashSet(StringComparer.Ordinal).SetEquals(keys))
            throw new InvalidDataException("Frozen progression basis has missing or foreign fields.");
    }
    private static void RequireCapturedUtc(string value)
    {
        if (!DateTime.TryParseExact(value, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc) ||
            utc.Kind != DateTimeKind.Utc || utc.ToString("o", CultureInfo.InvariantCulture) != value)
            throw new InvalidDataException("Frozen progression requires the actual round-trip UTC capture.");
    }
    private static void SanitizeFrozenSchedule(ProgressionScheduleState schedule, string currentRealm, byte[]? worldBytes, string? sanitizationUtc)
    {
        schedule.WorldCycleMinutes = schedule.WorldCycleMinutes > 0 ? schedule.WorldCycleMinutes : DefaultWorldCycleMinutes;
        schedule.FactionCycleMinutes = schedule.FactionCycleMinutes > 0 ? schedule.FactionCycleMinutes : DefaultFactionCycleMinutes;
        schedule.ChaosSeaCycleEquivalentHours = schedule.ChaosSeaCycleEquivalentHours > 0
            ? schedule.ChaosSeaCycleEquivalentHours : DefaultChaosSeaCycleEquivalentHours;
        schedule.AfterlifeCatchupCycleEquivalentMinutes = schedule.AfterlifeCatchupCycleEquivalentMinutes > 0
            ? schedule.AfterlifeCatchupCycleEquivalentMinutes : DefaultAfterlifeCatchupCycleEquivalentMinutes;
        var resolvedRealm = HasResolvedRealm(schedule.CurrentRealm) ? schedule.CurrentRealm : currentRealm;
        if (!HasResolvedRealm(resolvedRealm)) throw new InvalidDataException("Frozen sanitization realm is unresolved.");
        var previousCurrentWorldTimeInMinutes = schedule.CurrentWorldTimeInMinutes;
        schedule.CurrentRealm = resolvedRealm;
        if (!IsAfterlifeRealm(schedule.CurrentRealm))
            schedule.CurrentWorldTimeInMinutes = ResolveFrozenWorldTime(worldBytes, schedule.CurrentWorldTimeInMinutes).Minutes;
        schedule.PendingWorldCycles = Math.Max(0, schedule.PendingWorldCycles);
        schedule.PendingFactionCycles = Math.Max(0, schedule.PendingFactionCycles);
        schedule.PendingChaosSeaCycles = Math.Max(0, schedule.PendingChaosSeaCycles);
        schedule.PendingGuardianProjectCycles = Math.Max(0, schedule.PendingGuardianProjectCycles);
        schedule.PendingResidentAgencyCycles = Math.Max(0, schedule.PendingResidentAgencyCycles);
        schedule.PendingShiningAbodeCycles = Math.Max(0, schedule.PendingShiningAbodeCycles);
        schedule.PendingShiningFactionCycles = Math.Max(0, schedule.PendingShiningFactionCycles);
        schedule.PendingShiningTradeCycles = Math.Max(0, schedule.PendingShiningTradeCycles);
        schedule.CurrentChaosSeaTurnOrdinal = Math.Max(0, schedule.CurrentChaosSeaTurnOrdinal);
        schedule.LastChaosSeaSimulationOrdinal = Math.Max(0, schedule.LastChaosSeaSimulationOrdinal);
        schedule.LastGuardianProjectCycleOrdinal = Math.Max(0, schedule.LastGuardianProjectCycleOrdinal);
        schedule.LastResidentAgencyCycleOrdinal = Math.Max(0, schedule.LastResidentAgencyCycleOrdinal);
        schedule.LastShiningAbodeCycleOrdinal = Math.Max(0, schedule.LastShiningAbodeCycleOrdinal);
        schedule.LastShiningFactionCycleOrdinal = Math.Max(0, schedule.LastShiningFactionCycleOrdinal);
        schedule.LastShiningTradeCycleOrdinal = Math.Max(0, schedule.LastShiningTradeCycleOrdinal);
        if (!schedule.HasAfterlifeCatchupWorldTimeBaseline)
        {
            schedule.LastAfterlifeCatchupWorldTimeInMinutes = Math.Max(0, previousCurrentWorldTimeInMinutes);
            schedule.HasAfterlifeCatchupWorldTimeBaseline = true;
        }
        else
        {
            schedule.LastAfterlifeCatchupWorldTimeInMinutes = Math.Max(0, schedule.LastAfterlifeCatchupWorldTimeInMinutes);
        }
        if (schedule.LastUpdatedUtc is null)
        {
            if (sanitizationUtc is null) throw new InvalidDataException("Conditional sanitization UTC is missing.");
            RequireCapturedUtc(sanitizationUtc);
            schedule.LastUpdatedUtc = sanitizationUtc;
        }
        else if (sanitizationUtc is not null)
            throw new InvalidDataException("A non-null original timestamp must not invent a sanitization clock read.");
    }

    private static string ResolveFrozenRealm(byte[]? bytes)
    {
        if (bytes is null) return string.Empty;
        var json = CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("currentRealm", out var realm) && realm.ValueKind == JsonValueKind.String
                ? realm.GetString() ?? string.Empty : string.Empty;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException) { return string.Empty; }
    }
    private static WorldTimeResolutionResult ResolveFrozenWorldTime(byte[]? bytes, int fallback)
    {
        if (bytes is null) return new(fallback, true);
        var json = CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
        if (string.IsNullOrWhiteSpace(json)) return new(fallback, true);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (TryReadIntLike(root, "currentTimeInMinutes", out var absolute)) return new(absolute, false);
            if (root.TryGetProperty("setWorldTime", out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                if (TryReadIntLike(nested, "currentTimeInMinutes", out absolute)) return new(absolute, false);
                if (LooksLikeAbsoluteWorldTimeObject(nested)) return new(fallback, true);
            }
            if (TryReadIntLike(root, "timeChange", out var delta)) return new(Math.Max(0, fallback + delta), false);
            if (LooksLikeAbsoluteWorldTimeObject(root)) return new(fallback, true);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException) { return new(fallback, true); }
        return new(fallback, true);
    }
    private static ProgressionReportSnapshot ReadFrozenReport(byte[]? bytes)
    {
        if (bytes is null) return new(null, ProgressionFileReadState.Missing, false);
        var json = CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
        if (string.IsNullOrWhiteSpace(json)) return new(null, ProgressionFileReadState.Malformed, true);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new(null, ProgressionFileReadState.Malformed, true);
            var payload = root.TryGetProperty("progressionProcessingReport", out var nested) && nested.ValueKind == JsonValueKind.Object ? nested : root;
            var report = JsonSerializer.Deserialize<ProgressionProcessingReport>(payload.GetRawText(), JsonOpts);
            return report is null ? new(null, ProgressionFileReadState.Malformed, true) : new(report, ProgressionFileReadState.Valid, true);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException) { return new(null, ProgressionFileReadState.Malformed, true); }
    }
    private static PendingTurnRequestContext? ReadFrozenRequestContext(IReadOnlyDictionary<string, byte[]?> sources)
    {
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", "game_state/control/validation_repair_request.json" })
        {
            if (sources[path] is not { } bytes) continue;
            var json = CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
            if (string.IsNullOrWhiteSpace(json)) continue;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("sessionId", out var session) || session.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(session.GetString()) ||
                    !root.TryGetProperty("requestId", out var request) || request.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(request.GetString()) ||
                    !root.TryGetProperty("turnNumber", out var turn) || turn.ValueKind != JsonValueKind.Number || !turn.TryGetInt32(out var number) || number <= 0) continue;
                return new(session.GetString()!, request.GetString()!, number);
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException) { }
        }
        return null;
    }
}
