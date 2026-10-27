using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Only the actual async interface owner may project this field into ordinary value policy.
    // No supplied manifest, live kind, or graph field by itself grants this route.
    private async Task<BoundSpiritualInterface?> BindSpiritualHealingInterfaceAsync(JsonElement root,
        string currentJson, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("spiritualHealingGraphs", out var graphs)) return null;
        FileSystemManager.CanonicalWriteLease? lease = null;
        var ownsLease = false;
        var transferred = false;
        Exception? operationFailure = null;
        try
        {
            var invocation = _pendingSnapshotInvocation.Value;
            invocation?.RequireCurrent();
            lease = invocation?.Physical?.Lease;
            if (lease is null)
            {
                lease = await _fs.AcquireCanonicalWriteLeaseAsync();
                ownsLease = true;
            }
            _fs.VerifyCurrentSessionOperation(lease);
            var observed = PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease, new[] { AcceptedMechanicsPlan.WoundCommandPath });
            if (!observed.Success || observed.Snapshot is not { } snapshot || observed.Evidence is not { } evidence)
                throw new InvalidDataException("No authenticated original spiritual request.");
            var frameJson = await _fs.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            var frame = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(frameJson!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "spiritual graph interface manifest")!;
            if (frame.ProgressionControl is null || frame.SessionId != snapshot.SessionId || frame.RequestId != snapshot.RequestId ||
                frame.TurnNumber != snapshot.TurnNumber || PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(frame,
                    LiveTurnPreparationService.ManifestHashJsonOptions, static m => m.ManifestPayloadHash,
                    static (m, hash) => m.ManifestPayloadHash = hash) != snapshot.SnapshotToken)
                throw new InvalidDataException("The original graph request lost its control binding.");
            var sourceBytes = snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath);
            var request = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(sourceBytes),
                new JsonSerializerOptions(), "original spiritual graph request")!;
            var attempt = request["attempt"]!.AsObject();
            var selection = new SpiritualSelfTreatmentSelection(attempt["woundId"]!.GetValue<string>(), attempt["woundFingerprint"]!.GetValue<string>());
            if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(frame.ProgressionControl.CurrentRealm, out var realm) ||
                !SpiritualSelfTreatmentRequestCodec.IsBound(request,
                    new(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber, frame.ProgressionControl), selection,
                    realm, frame.ProgressionControl.NextChaosSeaTurnOrdinal))
                throw new InvalidDataException("Interface graphs require the exact signed v2 family.");
            var live = await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
            if (live is null || !live.AsSpan().SequenceEqual(sourceBytes))
                throw new InvalidDataException("The original healing request is no longer live for interface admission.");
            if (graphs.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("spiritualHealingGraphs must be an array.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in graphs.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || row.EnumerateObject().Count() != 2 ||
                    !row.TryGetProperty("stageRef", out var reference) || reference.ValueKind != JsonValueKind.String ||
                    !ResourceMaterializationContract.IsExactIdentifier(reference.GetString()) || !seen.Add(reference.GetString()!) ||
                    !row.TryGetProperty("consequenceDefinitions", out var definitions) || definitions.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("Each graph has one exact stageRef and consequenceDefinitions array.");
            }
            var responseBytes = await _fs.ReadFileBytesAsync(lease, "output/interface_updates.json");
            if (responseBytes is null || CanonicalJsonUtf8.DecodeOneOptionalBom(responseBytes) != currentJson)
                throw new InvalidDataException("The interface response changed before bound validation.");
            var projected = JsonNode.Parse(root.GetRawText())!.AsObject();
            projected.Remove("spiritualHealingGraphs");
            // Graphs-only is a payload only on this authenticated route; ordinary policy stays unchanged.
            if (!projected.ContainsKey("dialogueOptions") && !projected.ContainsKey("image_prompt")) projected["dialogueOptions"] = new JsonArray();
            evidence.RequireCurrent(_fs, lease, snapshot);
            var bound = new BoundSpiritualInterface(_fs, lease, ownsLease, snapshot, evidence,
                new(true, live), new(true, responseBytes), JsonDocument.Parse(projected.ToJsonString()));
            transferred = true;
            return bound;
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (JsonException or InvalidDataException or InvalidOperationException or
                ArgumentException or KeyNotFoundException or NullReferenceException or IOException or UnauthorizedAccessException))
        {
            operationFailure = failure;
            issues.Add(new("output/interface_updates.json.spiritualHealingGraphs", IssueSeverity.Error,
                "Spiritual healing graphs require the actual signed self-treatment request.",
                code: "spiritual_healing_interface_unbound", actual: failure.Message,
                repairHint: "Keep the original attempt/dice. Supply only the requested stageRefs; never replace the retained source."));
            return null;
        }
        catch (Exception failure)
        {
            operationFailure = failure;
            throw;
        }
        finally
        {
            if (ownsLease && !transferred && lease is not null)
                await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, false, operationFailure);
        }
    }

    private sealed class BoundSpiritualInterface(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        bool ownsLease, PendingTurnSnapshotReadAuthority snapshot, PendingTurnSnapshotReader.OriginalReadEvidence evidence,
        CanonicalBeforeImage request, CanonicalBeforeImage response, JsonDocument policy) : IAsyncDisposable
    {
        internal JsonElement Root => policy.RootElement;
        public async ValueTask DisposeAsync()
        {
            Exception? completionFailure = null;
            try
            {
                files.VerifyCurrentSessionOperation(lease);
                evidence.RequireCurrent(files, lease, snapshot);
                var currentRequest = await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
                var currentResponse = await files.ReadFileBytesAsync(lease, "output/interface_updates.json");
                if (new CanonicalBeforeImage(currentRequest is not null, currentRequest).Fingerprint != request.Fingerprint ||
                    new CanonicalBeforeImage(currentResponse is not null, currentResponse).Fingerprint != response.Fingerprint)
                    throw new InvalidDataException("Bound spiritual interface source changed before completion.");
            }
            catch (Exception failure)
            {
                completionFailure = failure;
                throw;
            }
            finally
            {
                policy.Dispose();
                if (ownsLease) await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(files, lease, false, completionFailure);
            }
        }
    }
}
