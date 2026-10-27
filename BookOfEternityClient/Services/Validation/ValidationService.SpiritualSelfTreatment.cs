using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private async Task<IReadOnlyList<ValidationIssue>?> TryPrepareSpiritualSelfTreatmentAsync(
        FileSystemManager.CanonicalWriteLease lease)
    {
        var json = await _fs.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        if (json is null)
        {
            var manifestText = await _fs.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            if (manifestText is null) return null;
            var frame = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(manifestText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "spiritual replay manifest");
            if (frame?.Files.ContainsKey(AcceptedMechanicsPlan.WoundCommandPath) != true) return null;
            var observed = PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease, new[] { AcceptedMechanicsPlan.WoundCommandPath });
            if (!observed.Success || observed.Snapshot is not { } signed || observed.Evidence is not { } evidence)
                return observed.Issues;
            var original = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(signed.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)))!.AsObject();
            if (original["kind"]?.GetValue<string>() != "spiritual_self_treatment_request") return null;
            if (frame.ProgressionControl is null || frame.ManifestPayloadHash != signed.SnapshotToken ||
                PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(frame) != signed.SnapshotToken)
                return new[] { new ValidationIssue(AcceptedMechanicsPlan.WoundCommandPath, IssueSeverity.Error,
                    "Spiritual replay lost its original signed control.", code: "spiritual_self_treatment_replay_conflict") };
            evidence.RequireCurrent(_fs, lease, signed);
            var scheduler = new ProgressionScheduleService(_fs, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgressionScheduleService>.Instance);
            if (await scheduler.IsPublishedSpiritualCycleAsync(frame.ProgressionControl, lease, signed.SnapshotToken, original)) return Array.Empty<ValidationIssue>();
            return new[] { new ValidationIssue(AcceptedMechanicsPlan.WoundCommandPath, IssueSeverity.Error,
                "The signed spiritual request is absent without its exact published result.", code: "spiritual_self_treatment_replay_conflict") };
        }
        try
        {
            using var classifier = JsonDocument.Parse(json);
            var root = classifier.RootElement;
            var selected = root.ValueKind == JsonValueKind.Object &&
                (root.TryGetProperty("schemaVersion", out var version) && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var v) && v == 2 ||
                 root.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() == "spiritual_self_treatment_request");
            if (!selected) return null;
        }
        catch (JsonException) { return null; } // The unchanged v1 parser owns malformed unclassified input.

        AfterlifeWoundHealingPlanner.Continuation? owner = null;
        var retainOwner = false;
        try
        {
            foreach (var path in new[] { ResourceMaterializationContract.CommandPath, EffectAcceptedTurnPlan.CommandPath,
                         ResourcePendingResolutionState.PendingPath })
                if (await _fs.ReadFileBytesAsync(lease, path) is not null)
                    throw new InvalidDataException("Concurrent resource/effect/pending work requires its ordinary owner: " + path);
            var manifestJson = await _fs.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath)
                ?? throw new InvalidDataException("Missing signed turn manifest.");
            var manifest = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(manifestJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "spiritual treatment manifest")
                ?? throw new InvalidDataException("Invalid signed turn manifest.");
            owner = await AfterlifeWoundHealingPlanner.Continuation.AdmitAsync(_fs, lease, manifest);
            var prepared = owner.Prepare();
            var effectInput = owner.ComposeEffectInput(prepared);
            var effect = WoundEffectBatchPlanner.Build(prepared, effectInput, new EffectIdentityFactory());
            if (!effect.Success) return effect.Issues;
            var final = WoundAcceptedTurnPlanner.Finalize(prepared, effect);
            if (!final.Success) return final.Issues;
            var bundle = new AcceptedMechanicsWoundStageBundle(owner.Input, prepared, effect.Plan!, final.Plan!);
            owner.RequireCurrent(_fs, lease);
            var common = AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(_fs, lease, bundle);
            if (!common.Success && common.Issues.Count == 0) throw new InvalidDataException("Spiritual treatment produced no complete common plan.");
            retainOwner = common.Success;
            return common.Issues;
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or InvalidOperationException or
                                           ArgumentException or OverflowException or KeyNotFoundException or NullReferenceException)
        {
            return new[] { new ValidationIssue(AcceptedMechanicsPlan.WoundCommandPath, IssueSeverity.Error,
                "Spiritual self-treatment could not produce one complete accepted contribution.",
                code: "spiritual_self_treatment_admission_invalid", actual: failure.Message,
                repairHint: "Keep the original attempt and dice. Correct the exact signed source, progression report or requested destination graphs.") };
        }
        finally
        {
            if (!retainOwner) owner?.Close();
        }
    }
}
