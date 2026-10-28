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
            var frameJson = await _fs.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            var frame = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(frameJson!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "spiritual graph interface manifest")!;
            // The unauthenticated map only chooses the inventory to read. Actual
            // observed original evidence below must independently select the family.
            var observed = frame.Files.ContainsKey(AcceptedMechanicsPlan.WoundCommandPath)
                ? PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease, new[] { AcceptedMechanicsPlan.WoundCommandPath })
                : AfterlifeWoundHealingPlanner.ReadOriginalNaturalSource(_fs, lease);
            if (!observed.Success || observed.Snapshot is not { } snapshot || observed.Evidence is not { } evidence)
                throw new InvalidDataException("No authenticated original spiritual request.");
            if (frame.ProgressionControl is null || frame.SessionId != snapshot.SessionId || frame.RequestId != snapshot.RequestId ||
                frame.TurnNumber != snapshot.TurnNumber || PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(frame,
                    LiveTurnPreparationService.ManifestHashJsonOptions, static m => m.ManifestPayloadHash,
                    static (m, hash) => m.ManifestPayloadHash = hash) != snapshot.SnapshotToken)
                throw new InvalidDataException("The original graph request lost its control binding.");
            JsonObject? request = null;
            JsonObject? naturalProjection = null;
            byte[]? sourceBytes = null;
            if (snapshot.CoveredLogicalPaths.Contains(AcceptedMechanicsPlan.WoundCommandPath, StringComparer.Ordinal))
            {
                sourceBytes = snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath);
                request = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(sourceBytes),
                    new JsonSerializerOptions(), "original spiritual graph request")!;
                var selection = SpiritualSelfTreatmentRequestCodec.ReadActiveSelection(request);
                if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(frame.ProgressionControl.CurrentRealm, out var realm) ||
                    !SpiritualSelfTreatmentRequestCodec.IsBoundActive(request,
                        new(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber, frame.ProgressionControl), selection,
                        realm, frame.ProgressionControl.NextChaosSeaTurnOrdinal))
                    throw new InvalidDataException("Interface graphs require the exact signed active family.");
            }
            else
            {
                if (!PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(frame, m => m.Files,
                        m => m.SnapshotFileHashes, m => m.RollbackBaselineFiles,
                        AfterlifeWoundTreatmentAuthority.SourcePaths.Append(AcceptedMechanicsPlan.WoundCommandPath).ToArray(), out _))
                    throw new InvalidDataException("Natural interface lacks the actual signed ordinary source/absence coverage.");
                var originalData = AfterlifeWoundTreatmentAuthority.PrepareNaturalSourceData(
                    new(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber, frame.ProgressionControl),
                    AfterlifeWoundHealingPlanner.Continuation.ReadSignedSources(snapshot))
                    ?? throw new InvalidDataException("Interface graphs have no authenticated ordinary natural cohort.");
                naturalProjection = SpiritualHealingRequestProjection.CreateNatural(originalData);
            }
            var live = await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
            if (live is not null && (sourceBytes is null || !live.AsSpan().SequenceEqual(sourceBytes)))
                throw new InvalidDataException("The original healing request was replaced before interface admission.");
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
            var publishedImages = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
            if (live is null)
            {
                async Task<byte[]?> ReadPublished(string path)
                {
                    if (publishedImages.TryGetValue(path, out var captured)) return captured.Bytes;
                    var bytes = await _fs.ReadFileBytesAsync(lease, path);
                    publishedImages.Add(path, new(bytes is not null, bytes));
                    return bytes;
                }
                var published = await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(
                    frame.ProgressionControl, snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber,
                    ReadPublished, snapshot.SnapshotToken, request, naturalProjection);
                if (published is null)
                {
                    if (naturalProjection is null)
                        throw new InvalidDataException("The consumed spiritual request has no matching published result.");
                    // Ordinary natural mode has no consumable request even before
                    // publication. Its unpublished client baselines must still agree.
                    foreach (var path in new[] { ProgressionScheduleService.SchedulePath,
                                 WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath })
                    {
                        var actual = await ReadPublished(path);
                        var before = snapshot.ReadRequiredBytes(path);
                        if (actual is null || !actual.AsSpan().SequenceEqual(before))
                            throw new InvalidDataException("Natural interface has neither its unpublished baseline nor its exact published result.");
                    }
                }
                // Exact accepted bytes need no graph reconstruction. Unrelated legal interface normalization
                // may change those bytes, so compare its closed graph DATA to the actual accepted stages.
                else if (new CanonicalBeforeImage(true, responseBytes).Fingerprint !=
                    published.GraphResponseFingerprint)
                    ValidatePublishedSpiritualGraphs(graphs, published);
            }
            var projected = JsonNode.Parse(root.GetRawText())!.AsObject();
            projected.Remove("spiritualHealingGraphs");
            // Graphs-only is a payload only on this authenticated route; ordinary policy stays unchanged.
            if (!projected.ContainsKey("dialogueOptions") && !projected.ContainsKey("image_prompt")) projected["dialogueOptions"] = new JsonArray();
            evidence.RequireCurrent(_fs, lease, snapshot);
            var bound = new BoundSpiritualInterface(_fs, lease, ownsLease, snapshot, evidence,
                new(live is not null, live), new(true, responseBytes), publishedImages,
                JsonDocument.Parse(projected.ToJsonString()));
            transferred = true;
            return bound;
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (JsonException or InvalidDataException or InvalidOperationException or
                ArgumentException or KeyNotFoundException or NullReferenceException or IOException or UnauthorizedAccessException))
        {
            operationFailure = failure;
            issues.Add(new("output/interface_updates.json.spiritualHealingGraphs", IssueSeverity.Error,
                "Spiritual healing graphs require the actual signed self or ordinary natural-cycle source.",
                code: "spiritual_healing_interface_unbound", actual: failure.Message,
                repairHint: "Keep the original turn/cycle and any existing treatment attempt/dice. Supply only requested stageRefs; never replace the source."));
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

    private static void ValidatePublishedSpiritualGraphs(JsonElement rows, SpiritualHealingPersistedResult result)
    {
        var stages = result.Stages;
        var initial = stages.GroupBy(s => s.Before.WoundId).ToDictionary(g => g.Key, g => g.First().Before, StringComparer.Ordinal);
        var fingerprint = result.ProgramReference;
        var required = stages.Where(s => s.Kind != "heal" && s.After.Severity.Rank < initial[s.After.WoundId].Severity.Rank)
            .GroupBy(s => (s.After.WoundId, s.After.Severity.Rank)).Select(g => g.First()).ToDictionary(
                s => SpiritualHealingRequestProjection.GraphRef(fingerprint, s.After.WoundId, s.After.Severity.Rank),
                StringComparer.Ordinal);
        if (rows.GetArrayLength() != required.Count)
            throw new InvalidDataException("Published spiritual graphs differ from the accepted stage set.");
        foreach (var row in rows.EnumerateArray())
        {
            var reference = row.GetProperty("stageRef").GetString()!;
            if (!required.TryGetValue(reference, out var stage))
                throw new InvalidDataException("Published spiritual graphs contain a foreign stageRef.");
            var wound = initial[stage.Before.WoundId];
            var complications = wound.Complications.ToDictionary(
                c => SpiritualHealingRequestProjection.ComplicationRef(fingerprint, c.ComplicationId),
                c => c.ComplicationId, StringComparer.Ordinal);
            var parsed = WoundResponseInputComposer.ParseSpiritualHealingGraph(row.GetProperty("consequenceDefinitions"),
                wound, stage.After.Severity.Rank, reference, complications);
            var accepted = WoundMaterializationContract.ImportOwnedGraph(stage.After.Complications, stage.After.Consequences);
            if (!JsonNode.DeepEquals(SpiritualGraphData(parsed), SpiritualGraphData(accepted)))
                throw new InvalidDataException("Published spiritual graph content differs from its accepted stage.");
        }
    }

    // Resolve ephemeral parser/application handles to definition+ownership coordinates. The executor allocates
    // root IDs and globally renumbers slots; only those spellings are excluded, never their ordered content.
    private static JsonNode SpiritualGraphData(WoundWorkingOwnedGraph graph)
    {
        var coordinates = graph.Roots.ToDictionary(r => r.Reference, r => JsonSerializer.Serialize(new[]
        {
            r.DefinitionKey,
            graph.Complications.SingleOrDefault(c => c.OwnedRoots.Contains(r.Reference))?.Reference.Value
        }));
        return JsonSerializer.SerializeToNode(new
        {
            Definitions = graph.Definitions.OrderBy(d => d.Definition.GetProperty("definitionKey").GetString(), StringComparer.Ordinal)
                .Select(d => d.Definition),
            Roots = graph.Roots.OrderBy(r => coordinates[r.Reference], StringComparer.Ordinal).Select(r => new
            {
                Coordinate = coordinates[r.Reference],
                Entries = graph.Entries.Where(e => e.Root == r.Reference).OrderBy(e => e.Slot)
                    .Select(e => new { e.ProfileKey, e.ReadableSummary })
            }),
            Complications = graph.Complications.OrderBy(c => c.Reference.Value, StringComparer.Ordinal).Select(c => new
            {
                Id = c.Reference.Value, c.Kind, c.State, c.DisplayName, c.TreatmentDifficultyModifier, c.Visibility,
                OwnedRoots = c.OwnedRoots.Select(r => coordinates[r])
            })
        })!;
    }

    private sealed class BoundSpiritualInterface(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        bool ownsLease, PendingTurnSnapshotReadAuthority snapshot, PendingTurnSnapshotReader.OriginalReadEvidence evidence,
        CanonicalBeforeImage request, CanonicalBeforeImage response,
        IReadOnlyDictionary<string, CanonicalBeforeImage> publishedImages, JsonDocument policy) : IAsyncDisposable
    {
        internal JsonElement Root => policy.RootElement;
        public async ValueTask DisposeAsync()
        {
            Exception? completionFailure = null;
            try
            {
                files.VerifyCurrentSessionOperation(lease);
                evidence.RequireCurrent(files, lease, snapshot);
                foreach (var pair in publishedImages)
                {
                    var bytes = await files.ReadFileBytesAsync(lease, pair.Key);
                    if (new CanonicalBeforeImage(bytes is not null, bytes).Fingerprint != pair.Value.Fingerprint)
                        throw new InvalidDataException("Published spiritual interface binding changed: " + pair.Key);
                }
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
