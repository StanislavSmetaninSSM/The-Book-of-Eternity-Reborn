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
        if (root.ValueKind != JsonValueKind.Object) return null;
        var hasGraphs = root.TryGetProperty("spiritualHealingGraphs", out var graphs);
        var hasResidentMemory = root.TryGetProperty(SpiritualTreatmentParticipation.ResidentThoughtResponseProperty, out var residentMemory);
        var hasOrdinaryMemory = root.TryGetProperty(GuardianAbodeResidentState.UpdateThoughtJournalProperty, out var ordinaryMemory);
        var hasRadiantMemory = root.TryGetProperty(SpiritualTreatmentParticipation.ShiningPatientMemoryResponseProperty, out var radiantMemory);
        var hasAnyMemory = hasResidentMemory || hasOrdinaryMemory || hasRadiantMemory;
        if (!hasGraphs && !hasAnyMemory) return null;
        if (!hasGraphs) graphs = JsonSerializer.SerializeToElement(Array.Empty<object>());
        FileSystemManager.CanonicalWriteLease? lease = null;
        var ownsLease = false;
        var transferred = false;
        Exception? operationFailure = null;
        PendingTurnSnapshotObservedReadResult? memoryLocality = null;
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
            SpiritualCombatHealingIntent? combatIntent = null;
            AfterlifeWoundTreatmentAuthority.SelectedWoundSourceData? combatOriginal = null;
            SpiritualDiagnosisProgram? diagnosisProgram = null;
            if (snapshot.CoveredLogicalPaths.Contains(AcceptedMechanicsPlan.WoundCommandPath, StringComparer.Ordinal))
            {
                sourceBytes = snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath);
                request = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(sourceBytes),
                    new JsonSerializerOptions(), "original spiritual graph request")!;
                if (SpiritualCombatHealingIntent.IsKind(request))
                {
                    // The first authenticated read selects inventory only. A new
                    // complete observed original read must prove every combat source.
                    var original = PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease,
                        PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                            new[] { AcceptedMechanicsPlan.WoundCommandPath }, AfterlifeWoundTreatmentAuthority.CombatSourcePaths));
                    if (!original.Success || original.Snapshot is not { } combatSnapshot || original.Evidence is not { } combatEvidence ||
                        combatSnapshot.SessionId != snapshot.SessionId || combatSnapshot.RequestId != snapshot.RequestId ||
                        combatSnapshot.SnapshotToken != snapshot.SnapshotToken || combatSnapshot.TurnNumber != snapshot.TurnNumber)
                        throw new InvalidDataException("Interface combat graphs lost their actual complete original.");
                    snapshot = combatSnapshot; evidence = combatEvidence;
                    combatIntent = SpiritualCombatHealingIntent.Parse(request);
                    combatOriginal = AfterlifeWoundTreatmentAuthority.ReadCombatOriginalSource(snapshot, combatIntent, out _);
                    if (!combatIntent.IsBound(new(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber, frame.ProgressionControl), combatIntent.Selection))
                        throw new InvalidDataException("Interface combat graphs differ from the exact original control/selection.");
                }
                else if (SpiritualDiagnosisRequestCodec.IsKind(request))
                {
                    if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(frame.ProgressionControl.CurrentRealm, out var diagnosisRealm))
                        throw new InvalidDataException("Diagnosis interface lost its current spiritual realm.");
                    var original = AfterlifeWoundHealingPlanner.ReadOriginalDiagnosisSource(_fs, lease, diagnosisRealm);
                    if (!original.Success || original.Snapshot is not { } diagnosisSnapshot || original.Evidence is not { } diagnosisEvidence ||
                        diagnosisSnapshot.SnapshotToken != snapshot.SnapshotToken || diagnosisSnapshot.SessionId != snapshot.SessionId ||
                        diagnosisSnapshot.RequestId != snapshot.RequestId || diagnosisSnapshot.TurnNumber != snapshot.TurnNumber)
                        throw new InvalidDataException("Diagnosis graphs lost their complete same signed original.");
                    snapshot = diagnosisSnapshot; evidence = diagnosisEvidence;
                    var context = new SpiritualSelfTreatmentStageContext(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber, frame.ProgressionControl);
                    if (!SpiritualDiagnosisRequestCodec.IsBound(request, context))
                        throw new InvalidDataException("Diagnosis interface differs from its complete original request/control.");
                    var sources = AfterlifeWoundHealingPlanner.Continuation.ReadSignedSources(snapshot,
                        AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths(diagnosisRealm));
                    var candidate = AfterlifeWoundTreatmentAuthority.PrepareDiagnosisCandidate(SpiritualDiagnosisRequestCodec.ReadSelection(request), sources, context.Turn);
                    diagnosisProgram = AfterlifeWoundTreatmentAuthority.BindPreparedDiagnosisSource(context, candidate, sources, request).Program;
                    if (hasResidentMemory || hasRadiantMemory)
                        throw new InvalidDataException("Diagnosis cannot use patient-participation protected memory responses.");
                }
                else
                {
                    var selection = SpiritualSelfTreatmentRequestCodec.ReadActiveSelection(request);
                    if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(frame.ProgressionControl.CurrentRealm, out var realm) ||
                        !SpiritualSelfTreatmentRequestCodec.IsBoundActive(request,
                            new(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber, frame.ProgressionControl), selection,
                            realm, frame.ProgressionControl.NextChaosSeaTurnOrdinal))
                        throw new InvalidDataException("Interface graphs require the exact signed active family.");
                }
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
            async Task<byte[]?> ReadPublished(string path)
            {
                if (publishedImages.TryGetValue(path, out var captured)) return captured.Bytes;
                var bytes = await _fs.ReadFileBytesAsync(lease, path);
                publishedImages.Add(path, new(bytes is not null, bytes));
                return bytes;
            }
            SpiritualHealingPersistedResult? publishedSafe = null;
            if (live is null)
            {
                if (combatIntent is not null)
                {
                    var publishedCombat = await ProgressionScheduleService.ReadPublishedSpiritualCombatCoreAsync(
                        frame.ProgressionControl, snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber,
                        ReadPublished, snapshot.SnapshotToken, combatIntent.Root);
                    if (publishedCombat is null) throw new InvalidDataException("Consumed combat intent lacks its actual accepted outcome.");
                    if (publishedCombat.Result is { } combatResult)
                    {
                        if (new CanonicalBeforeImage(true, responseBytes).Fingerprint != combatResult.ToCanonicalJson()["graphResponseFingerprint"]!.GetValue<string>())
                            ValidatePublishedSpiritualGraphs(graphs, combatResult.Stages, combatIntent.Fingerprint);
                    }
                    else if (graphs.GetArrayLength() != 0)
                        throw new InvalidDataException("Blocked combat cannot publish destination graphs.");
                }
                else
                {
                var published = await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(
                    frame.ProgressionControl, snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber,
                    ReadPublished, snapshot.SnapshotToken, request, naturalProjection);
                publishedSafe = published;
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
            }
            else if (diagnosisProgram is not null)
            {
                var members = diagnosisProgram.NaturalMembers.ToDictionary(m => m.Wound.WoundId, m => m.Wound, StringComparer.Ordinal);
                var required = SpiritualHealingRequestProjection.RequiredGraphs(members.Values.ToArray(), diagnosisProgram.NaturalStages)
                    .ToDictionary(r => SpiritualHealingRequestProjection.GraphRef(diagnosisProgram.NaturalProgramReference, r.WoundId, r.Rank), StringComparer.Ordinal);
                if (graphs.GetArrayLength() != required.Count) throw new InvalidDataException("Diagnosis graphs omit or add a natural destination.");
                foreach (var row in graphs.EnumerateArray())
                {
                    var reference = row.GetProperty("stageRef").GetString()!;
                    if (!required.TryGetValue(reference, out var destination)) throw new InvalidDataException("Foreign diagnosis natural destination.");
                    var wound = members[destination.WoundId];
                    var complications = wound.Complications.ToDictionary(c => SpiritualHealingRequestProjection.ComplicationRef(
                        diagnosisProgram.NaturalProgramReference, c.ComplicationId), c => c.ComplicationId, StringComparer.Ordinal);
                    _ = WoundResponseInputComposer.ParseSpiritualHealingGraph(row.GetProperty("consequenceDefinitions"), wound,
                        destination.Rank, reference, complications);
                }
            }
            else if (combatIntent is not null && combatOriginal is not null)
            {
                var possible = Enumerable.Range(1, combatOriginal.Wound.Severity.Rank - 1).ToDictionary(
                    rank => SpiritualHealingRequestProjection.GraphRef(combatIntent.Fingerprint, combatIntent.WoundId, rank));
                var complications = combatOriginal.Wound.Complications.ToDictionary(
                    c => SpiritualHealingRequestProjection.ComplicationRef(combatIntent.Fingerprint, c.ComplicationId), c => c.ComplicationId, StringComparer.Ordinal);
                foreach (var row in graphs.EnumerateArray())
                {
                    if (!possible.TryGetValue(row.GetProperty("stageRef").GetString()!, out var rank))
                        throw new InvalidDataException("Unpublished combat graph has a foreign original destination reference.");
                    _ = WoundResponseInputComposer.ParseSpiritualHealingGraph(row.GetProperty("consequenceDefinitions"),
                        combatOriginal.Wound, rank, row.GetProperty("stageRef").GetString()!, complications);
                }
            }
            if (diagnosisProgram is not null && hasOrdinaryMemory)
            {
                if (publishedSafe is null || ordinaryMemory.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("Ordinary diagnosis memory requires its completed cycle and existing array command contract.");
                var rosterPath = GuardianAbodeResidentState.StatePath;
                var beforeRoster = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                    snapshot.ReadRequiredBytes(rosterPath)), new JsonSerializerOptions(), "diagnosis original resident memory")!;
                var expected = beforeRoster.DeepClone().AsObject();
                GuardianAbodeResidentState.ApplyThoughtJournalUpdates(expected, JsonNode.Parse(ordinaryMemory.GetRawText())!.AsArray());
                var beforeJournal = beforeRoster[GuardianAbodeResidentState.ThoughtJournalProperty] as JsonArray ?? new JsonArray();
                var afterJournal = expected[GuardianAbodeResidentState.ThoughtJournalProperty]!.AsArray();
                if (afterJournal.Count < beforeJournal.Count || beforeJournal.Where((row, index) => !JsonNode.DeepEquals(row, afterJournal[index])).Any())
                    throw new InvalidDataException("Ordinary diagnosis memory cannot replace an existing canonical journal prefix.");
                var actualBytes = await ReadPublished(rosterPath) ?? throw new InvalidDataException("Diagnosis ordinary resident journal is absent.");
                var actual = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(actualBytes), new JsonSerializerOptions(), rosterPath)!;
                if (!JsonNode.DeepEquals(afterJournal, actual[GuardianAbodeResidentState.ThoughtJournalProperty]))
                    throw new InvalidDataException("Ordinary diagnosis memory differs from its actual canonical append.");
            }
            var patientRecord = publishedSafe?.ShiningPatientParticipationRecord;
            if (diagnosisProgram is null && (hasOrdinaryMemory || hasRadiantMemory || hasResidentMemory && patientRecord is not null))
            {
                if (publishedSafe is null || patientRecord is null || !SpiritualTreatmentParticipation.IsShiningPatientParticipation(patientRecord) ||
                    new CanonicalBeforeImage(true, responseBytes).Fingerprint != publishedSafe.GraphResponseFingerprint ||
                    hasOrdinaryMemory && ordinaryMemory.ValueKind != JsonValueKind.Array ||
                    hasRadiantMemory && radiantMemory.ValueKind != JsonValueKind.Object ||
                    hasResidentMemory && residentMemory.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("Patient memory requires its exact published schema5 natural8/active8 response.");
                var owner = SpiritualTreatmentParticipation.RecordOwner(patientRecord);
                var needsRoster = owner.OwnerKind == "resident" || hasOrdinaryMemory;
                var paths = new[] { WoundCarrierCatalog.AfterlifeProfilesPath, ShiningAbodeState.StatePath }
                    .Concat(needsRoster ? new[] { GuardianAbodeResidentState.StatePath } : Array.Empty<string>()).ToArray();
                memoryLocality = PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease, paths);
                if (!memoryLocality.Success || memoryLocality.Snapshot is not { } local || memoryLocality.Evidence is not { } localEvidence ||
                    local.SessionId != snapshot.SessionId || local.RequestId != snapshot.RequestId ||
                    local.TurnNumber != snapshot.TurnNumber || local.SnapshotToken != snapshot.SnapshotToken)
                    throw new InvalidDataException("Patient memory lost its same signed profiles/locality/conditional roster.");
                localEvidence.RequireCurrent(_fs, lease, local);
                JsonObject Original(string path) => StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                    local.ReadRequiredBytes(path)), new JsonSerializerOptions(), path)!;
                var beforeRoster = needsRoster ? Original(GuardianAbodeResidentState.StatePath) : null;
                var program = SpiritualTreatmentParticipation.PrepareShiningPatientMemory(owner,
                    SpiritualTreatmentParticipation.RecordLocality(patientRecord), Original(WoundCarrierCatalog.AfterlifeProfilesPath),
                    beforeRoster, Original(ShiningAbodeState.StatePath), snapshot.TurnNumber,
                    hasOrdinaryMemory ? JsonNode.Parse(ordinaryMemory.GetRawText())!.AsArray() : null,
                    hasResidentMemory ? JsonNode.Parse(residentMemory.GetRawText())!.AsArray() : null,
                    hasRadiantMemory ? JsonNode.Parse(radiantMemory.GetRawText())!.AsObject() : null);
                if (needsRoster)
                {
                    var bytes = await ReadPublished(GuardianAbodeResidentState.StatePath)
                        ?? throw new InvalidDataException("Published patient memory roster is absent.");
                    program.RequireResidentAfterImage(beforeRoster!, StrictJsonAuthority.Deserialize<JsonObject>(
                        CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), "published patient memory roster")!);
                }
                if (hasRadiantMemory)
                {
                    var bytes = await ReadPublished(WoundCarrierCatalog.AfterlifeProfilesPath)
                        ?? throw new InvalidDataException("Published radiant profile is absent.");
                    program.RequireProfileAfterImage(StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                        new JsonSerializerOptions(), "published radiant memory")!);
                }
            }
            else if (hasResidentMemory)
            {
                // I runs after common publication in the Engine. A request or
                // proposed grant alone cannot exempt memory from ordinary policy.
                var residentRecord = publishedSafe?.ResidentParticipationRecord;
                var residentQuote = publishedSafe?.ServiceQuote is { Provider: { Realm: "shining_abode", OwnerKind: "resident" } } serviceQuote
                    ? serviceQuote : null;
                var residentOwnerId = residentRecord is not null ? SpiritualTreatmentParticipation.RecordOwnerId(residentRecord)
                    : residentQuote?.Provider.OwnerId;
                if (residentMemory.ValueKind != JsonValueKind.Array || publishedSafe is null || residentOwnerId is null ||
                    new CanonicalBeforeImage(true, responseBytes).Fingerprint != publishedSafe.GraphResponseFingerprint)
                    throw new InvalidDataException("Protected resident memory requires its exact published participation or faction-service response.");
                memoryLocality = residentQuote is not null
                    ? AfterlifeWoundHealingPlanner.ReadOriginalServiceLocalitySource(_fs, lease, residentQuote)
                    : AfterlifeWoundHealingPlanner.ReadOriginalAidLocalitySource(_fs, lease, SpiritualTreatmentParticipation.Selection(residentRecord!));
                if (!memoryLocality.Success || memoryLocality.Snapshot is not { } local || memoryLocality.Evidence is not { } localEvidence ||
                    local.SessionId != snapshot.SessionId || local.RequestId != snapshot.RequestId || local.TurnNumber != snapshot.TurnNumber ||
                    local.SnapshotToken != snapshot.SnapshotToken || !local.CoveredLogicalPaths.Contains(GuardianAbodeResidentState.StatePath, StringComparer.Ordinal))
                    throw new InvalidDataException("Protected resident memory lost its same actual original roster.");
                localEvidence.RequireCurrent(_fs, lease, local);
                var before = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                    local.ReadRequiredBytes(GuardianAbodeResidentState.StatePath)), new JsonSerializerOptions(), "original resident memory roster")!;
                var commands = SpiritualTreatmentParticipation.ValidateResidentThoughtAppend(JsonNode.Parse(residentMemory.GetRawText())!.AsArray(),
                    residentOwnerId, snapshot.TurnNumber, before);
                var expected = before.DeepClone().AsObject();
                GuardianAbodeResidentState.ApplyThoughtJournalUpdates(expected, commands);
                var currentBytes = await ReadPublished(GuardianAbodeResidentState.StatePath)
                    ?? throw new InvalidDataException("Published resident memory roster is absent.");
                var current = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(currentBytes),
                    new JsonSerializerOptions(), "published resident memory roster")!;
                if (!JsonNode.DeepEquals(expected[GuardianAbodeResidentState.ThoughtJournalProperty], current[GuardianAbodeResidentState.ThoughtJournalProperty]))
                    throw new InvalidDataException("Protected resident memory differs from its complete exact accepted append.");
            }
            var projected = JsonNode.Parse(root.GetRawText())!.AsObject();
            projected.Remove("spiritualHealingGraphs");
            if (hasResidentMemory) projected.Remove(SpiritualTreatmentParticipation.ResidentThoughtResponseProperty);
            if (hasOrdinaryMemory) projected.Remove(GuardianAbodeResidentState.UpdateThoughtJournalProperty);
            if (hasRadiantMemory) projected.Remove(SpiritualTreatmentParticipation.ShiningPatientMemoryResponseProperty);
            // Graphs-only is a payload only on this authenticated route; ordinary policy stays unchanged.
            if (!projected.ContainsKey("dialogueOptions") && !projected.ContainsKey("image_prompt")) projected["dialogueOptions"] = new JsonArray();
            evidence.RequireCurrent(_fs, lease, snapshot);
            var bound = new BoundSpiritualInterface(_fs, lease, ownsLease, snapshot, evidence,
                new(live is not null, live), new(true, responseBytes), publishedImages,
                JsonDocument.Parse(projected.ToJsonString()), memoryLocality?.Snapshot, memoryLocality?.Evidence);
            transferred = true;
            return bound;
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (JsonException or InvalidDataException or InvalidOperationException or
                ArgumentException or KeyNotFoundException or NullReferenceException or IOException or UnauthorizedAccessException))
        {
            operationFailure = failure;
            issues.Add(new("output/interface_updates.json." + (hasRadiantMemory ? SpiritualTreatmentParticipation.ShiningPatientMemoryResponseProperty :
                    hasOrdinaryMemory ? GuardianAbodeResidentState.UpdateThoughtJournalProperty : hasResidentMemory ? SpiritualTreatmentParticipation.ResidentThoughtResponseProperty : "spiritualHealingGraphs"), IssueSeverity.Error,
                hasAnyMemory ? "Memory requires its exact published treatment result and complete append proof." : "Spiritual healing graphs require the actual signed safe or combat source.",
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

    private static void ValidatePublishedSpiritualGraphs(JsonElement rows, SpiritualHealingPersistedResult result) =>
        ValidatePublishedSpiritualGraphs(rows, result.Stages, result.ProgramReference);

    private static void ValidatePublishedSpiritualGraphs(JsonElement rows, IReadOnlyList<SpiritualHealingPublishedStage> stages, string fingerprint)
    {
        var initial = stages.GroupBy(s => s.Before.WoundId).ToDictionary(g => g.Key, g => g.First().Before, StringComparer.Ordinal);
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
        IReadOnlyDictionary<string, CanonicalBeforeImage> publishedImages, JsonDocument policy,
        PendingTurnSnapshotReadAuthority? memorySnapshot, PendingTurnSnapshotReader.OriginalReadEvidence? memoryEvidence) : IAsyncDisposable
    {
        internal JsonElement Root => policy.RootElement;
        public async ValueTask DisposeAsync()
        {
            Exception? completionFailure = null;
            try
            {
                files.VerifyCurrentSessionOperation(lease);
                evidence.RequireCurrent(files, lease, snapshot);
                memoryEvidence?.RequireCurrent(files, lease, memorySnapshot!);
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
