using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Reports a confirmed private frontier append without advancing the saved wound decision.
    /// </summary>
    /// <param name="Disposition">
    /// Committed after exact read-back and owner reconstruction, not_committed for the unchanged old image, or blocked.
    /// </param>
    /// <param name="NextRequest">
    /// Activated owner-derived successor only after a confirmed commit and cold replay.
    /// </param>
    /// <param name="Issues">
    /// Failure diagnostics, or the successor's outstanding dependent diagnostics after commit.
    /// </param>
    internal sealed record SpiritualWoundDependentProgressCommitResult(string Disposition,
        SpiritualWoundContinuationRequest? NextRequest, IReadOnlyList<ValidationIssue> Issues);

    /// <summary>
    /// Keeps physical response evaluation together with its conditional progress commit, without reusable authority.
    /// </summary>
    /// <param name="Evaluation">
    /// The evaluation performed after witnessing physical inputs, or <see langword="null"/> when that witness failed.
    /// </param>
    /// <param name="Progress">
    /// The progress result; a non-advanced evaluation has the existing blocked/not-advanced result.
    /// Witness and commit failures require retaining the issued transport for recovery.
    /// </param>
    internal sealed record SpiritualWoundDependentResponseResult(SpiritualWoundContinuationEvaluation? Evaluation,
        SpiritualWoundDependentProgressCommitResult Progress);

    /// <summary>
    /// Saves one fully validated issued frontier before activating a separate dependent request.
    /// Preserves standalone progress dispositions while retaining evaluation inside the operation.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease covering response validation, snapshot comparison and atomic checkpoint replacement.
    /// </param>
    /// <param name="expectedRequest">
    /// Exact current request; its field list is compared with owner-derived permissions before persistence.
    /// </param>
    /// <param name="response">
    /// Closed empty-decision response correlated to the completed frontier.
    /// </param>
    /// <param name="writeOverrideAsync">
    /// Optional test-only atomic checkpoint writer; null uses the filesystem writer.
    /// </param>
    /// <param name="expectedRequestBytes">
    /// Optional exact outer request observed by the engine; null is reserved for direct internal tests.
    /// </param>
    /// <param name="expectedReadyBytes">
    /// Optional exact outer Ready observed by the engine; null is reserved for direct internal tests.
    /// </param>
    /// <returns>
    /// A durable successor only after exact read-back and genuine cold replay, or an explicit transport refusal.
    /// </returns>
    internal async Task<SpiritualWoundDependentProgressCommitResult> CommitSpiritualWoundDependentProgressAsync(
        FileSystemManager.CanonicalWriteLease lease, SpiritualWoundContinuationRequest expectedRequest,
        SpiritualWoundContinuationResponse response,
        Func<FileSystemManager.CanonicalWriteLease, string, byte[], Task>? writeOverrideAsync = null,
        byte[]? expectedRequestBytes = null, byte[]? expectedReadyBytes = null)
        => (await EvaluateAndCommitSpiritualWoundDependentResponseAsync(lease, expectedRequest, response,
            writeOverrideAsync, expectedRequestBytes, expectedReadyBytes)).Progress;

    /// <summary>
    /// Evaluates a witnessed response once and durably commits only an advanced frontier.
    /// The engine uses this for dependent responses instead of evaluating again before the progress operation.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease covering the complete witness, evaluation and conditional commit.
    /// </param>
    /// <param name="expectedRequest">
    /// Issued envelope whose permissions are reconstructed by the actual evaluation.
    /// </param>
    /// <param name="response">
    /// Correlated response to evaluate; no earlier evaluation is accepted as authority.
    /// </param>
    /// <param name="writeOverrideAsync">
    /// Optional atomic checkpoint writer for existing failure tests; <see langword="null"/> uses the filesystem.
    /// </param>
    /// <param name="expectedRequestBytes">
    /// Exact engine-observed request bytes, or <see langword="null"/> for direct internal calls.
    /// </param>
    /// <param name="expectedReadyBytes">
    /// Exact engine-observed Ready bytes, or <see langword="null"/> for direct internal calls.
    /// </param>
    /// <returns>
    /// Rejection or resolution without a write, an independently reopened committed successor,
    /// or a witness/commit failure that must retain transport for recovery.
    /// </returns>
    internal async Task<SpiritualWoundDependentResponseResult> EvaluateAndCommitSpiritualWoundDependentResponseAsync(
        FileSystemManager.CanonicalWriteLease lease, SpiritualWoundContinuationRequest expectedRequest,
        SpiritualWoundContinuationResponse response,
        Func<FileSystemManager.CanonicalWriteLease, string, byte[], Task>? writeOverrideAsync = null,
        byte[]? expectedRequestBytes = null, byte[]? expectedReadyBytes = null)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        SpiritualWoundContinuationEvaluation? evaluated = null;
        SpiritualWoundDependentResponseResult Result(SpiritualWoundDependentProgressCommitResult progress) =>
            new(evaluated, progress);
        try
        {
            var first = await ClassifySpiritualPendingAsync(lease);
            IReadOnlyDictionary<string, CanonicalBeforeImage> before;
            using (var capture = first.Capture)
            {
                if (first.Disposition != "match" || capture is null)
                    return Result(ProgressFailure("blocked", "spiritual_dependent_progress_owner_unavailable"));
                var witnessed = new Dictionary<string, CanonicalBeforeImage>(
                    await capture.ReadC2TransportWitnessAsync(lease), StringComparer.Ordinal);
                foreach (var (path, expected) in new[]
                {
                    ("game_state/control/validation_repair_request.json", expectedRequestBytes),
                    ("game_state/control/validation_repair_ready.json", expectedReadyBytes)
                })
                {
                    var bytes = await _fs.ReadFileBytesAsync(lease, path);
                    if (expected is not null && (bytes is null || !expected.AsSpan().SequenceEqual(bytes)))
                        return Result(ProgressFailure("blocked", "spiritual_dependent_progress_transport_changed"));
                    witnessed[path] = new(bytes is not null, bytes);
                }
                before = witnessed;
            }
            evaluated = await EvaluateSpiritualWoundContinuationDraftAsync(lease, expectedRequest, response);
            if (evaluated.Disposition != SpiritualWoundContinuationDisposition.Advanced)
                return Result(ProgressFailure("blocked", "spiritual_dependent_progress_not_advanced"));
            var classified = await ClassifySpiritualPendingAsync(lease);
            SpiritualWoundDependentProgressCommitResult written;
            using (var capture = classified.Capture)
            {
                if (classified.Disposition != "match" || capture is null)
                    return Result(ProgressFailure("blocked", "spiritual_dependent_progress_owner_unavailable"));
                written = await capture.CommitDependentProgressCoreAsync(lease, expectedRequest, before, writeOverrideAsync);
            }
            if (written.Disposition != "committed") return Result(written);
            var next = await ReadSpiritualWoundContinuationAsync(lease);
            if (next.Disposition != "dependent_draft" || next.Request is null || next.Issues.Count == 0 ||
                JsonSerializer.Serialize(next.Request) == JsonSerializer.Serialize(expectedRequest) ||
                next.AcceptedRequest is null || JsonSerializer.Serialize(next.AcceptedRequest) != JsonSerializer.Serialize(expectedRequest))
                return Result(ProgressFailure("blocked", "spiritual_dependent_progress_reopen_failed"));
            return Result(new("committed", next.Request, next.Issues));
        }
        catch (Exception error) when (error is not CoordinatedStatePublicationUncertainException &&
            (error is IOException or UnauthorizedAccessException or InvalidOperationException or
                FormatException or JsonException or OverflowException or DecoderFallbackException))
        {
            return Result(ProgressFailure("blocked", "spiritual_dependent_progress_failed"));
        }
    }

    /// <summary>
    /// Creates an explicit private progress failure without interpreting partial checkpoint state as success.
    /// </summary>
    /// <param name="disposition">
    /// Not_committed for confirmed old bytes, otherwise blocked.
    /// </param>
    /// <param name="code">
    /// Exact failure category for diagnostics.
    /// </param>
    /// <returns>
    /// A result with no activated request and one error.
    /// </returns>
    private static SpiritualWoundDependentProgressCommitResult ProgressFailure(string disposition, string code) =>
        new(disposition, null, [new ValidationIssue(SpiritualWoundCaptureCheckpointState.StatePath,
            IssueSeverity.Error, "The dependent frontier could not be durably accepted.", code: code)]);

    /// <summary>
    /// Writes the closed private field witness using the existing public camel-case property names.
    /// </summary>
    /// <param name="fields">
    /// Exact sorted owner-derived permissions; their order is preserved.
    /// </param>
    /// <returns>
    /// Detached comparison-only JSON without changing the existing correlation serialization formula.
    /// </returns>
    private static JsonArray SerializeDependentProgressFields(IReadOnlyList<SpiritualWoundContinuationField> fields) =>
        new(fields.Select(field => (JsonNode)new JsonObject
            { ["path"] = field.Path, ["jsonPointer"] = field.JsonPointer }).ToArray());

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Appends one comparison row while preserving the selected decision, pending packet and allocation boundaries.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease retained through exact before/after checks.
        /// </param>
        /// <param name="request">
        /// Owner-validated completed request from the immediately preceding evaluation.
        /// </param>
        /// <param name="before">
        /// Complete physical witness captured before that evaluation; every image must still match.
        /// </param>
        /// <param name="writeOverrideAsync">
        /// Optional atomic-write test hook; null selects ordinary atomic persistence.
        /// </param>
        /// <returns>
        /// Confirmed checkpoint commit, confirmed unchanged old image, or ambiguous/invalid rejection.
        /// </returns>
        internal async Task<SpiritualWoundDependentProgressCommitResult> CommitDependentProgressCoreAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualWoundContinuationRequest request,
            IReadOnlyDictionary<string, CanonicalBeforeImage> before,
            Func<FileSystemManager.CanonicalWriteLease, string, byte[], Task>? writeOverrideAsync)
        {
            await _gate.WaitAsync();
            try
            {
                var checkpoint = await RequireC2PendingSubmissionCoreAsync(lease);
                var pair = _matchedC2Pair!;
                var canonical = Encoding.UTF8.GetBytes(SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint));
                if (!ExactBytes(canonical, pair.CheckpointBytes) || !ExactBytes(pair.PendingBytes,
                        Encoding.UTF8.GetBytes(SpiritualWoundDecisionPendingState.SerializeCanonical(pair.Pending))))
                    return ProgressFailure("blocked", "spiritual_dependent_progress_noncanonical_origin");
                foreach (var item in before)
                {
                    var observedBefore = await _validator._fs.ReadFileBytesAsync(lease, item.Key);
                    if (!SameExactImage(item.Value, new CanonicalBeforeImage(observedBefore is not null, observedBefore)))
                        return ProgressFailure("blocked", "spiritual_dependent_progress_input_changed");
                }
                var root = SpiritualWoundStateJson.Parse(SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint));
                var body = root["checkpoint"]!.AsObject();
                var submission = body["pendingSubmission"]!.AsObject();
                var rows = submission["dependentDraftProgress"] as JsonArray ?? new JsonArray();
                var layer = ReadC2CommittedInputLayer(checkpoint);
                foreach (var progress in rows)
                foreach (var image in progress!["inputChanges"]!.AsArray())
                    layer[image!["path"]!.GetValue<string>()] = new(true,
                        Convert.FromBase64String(image["contentBase64"]!.GetValue<string>()));
                var images = new JsonArray();
                foreach (var path in new[] { AfterlifeSpiritualConflictState.StatePath, FixedOriginalOutputPaths[0] }
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    var image = before[path];
                    if (SameExactImage(layer[path], image)) continue;
                    if (!image.Existed || image.Bytes is null)
                        return ProgressFailure("blocked", "spiritual_dependent_progress_missing_input");
                    images.Add(new JsonObject
                    {
                        ["path"] = path, ["existed"] = true, ["contentBase64"] = Convert.ToBase64String(image.Bytes),
                        ["contentFingerprint"] = image.Fingerprint
                    });
                }
                rows.Add(new JsonObject
                {
                    ["ordinal"] = rows.Count + 1, ["acceptedContinuationId"] = request.ContinuationId,
                    ["dependentDraftFields"] = SerializeDependentProgressFields(request.DependentDraftFields),
                    ["inputChanges"] = images
                });
                if (submission["dependentDraftProgress"] is null) submission["dependentDraftProgress"] = rows;
                body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
                    "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
                var parsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
                    SpiritualWoundCaptureCheckpointState.StatePath, _draftInputs.PathInventory);
                if (!parsed.IsValid || parsed.State is null)
                    return ProgressFailure("blocked", "spiritual_dependent_progress_shape_invalid");
                var bytes = Encoding.UTF8.GetBytes(SpiritualWoundCaptureCheckpointState.SerializeCanonical(parsed.State));
                await RequireC2PendingSubmissionCoreAsync(lease);
                var writer = writeOverrideAsync ?? ((FileSystemManager.CanonicalWriteLease held,
                    string path, byte[] content) => _validator._fs.WriteFileAtomicBytesAsync(held, path, content));
                try { await writer(lease, SpiritualWoundCaptureCheckpointState.StatePath, bytes); }
                catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException)
                { /* Exact read-back is permitted only for a known ordinary transport failure. */ }
                var observed = await _validator._fs.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath);
                if (!ExactBytes(observed, bytes))
                    return ExactBytes(observed, pair.CheckpointBytes)
                        ? ProgressFailure("not_committed", "spiritual_dependent_progress_not_committed")
                        : ProgressFailure("blocked", "spiritual_dependent_progress_ambiguous");
                var freshness = await CheckRetainedInputsAfterC2TransportAsync(lease, bytes, pair.PendingBytes);
                if (!IsCurrentOwner || freshness.Count != 0)
                    return ProgressFailure("blocked", "spiritual_dependent_progress_origin_changed");
                foreach (var item in before)
                {
                    var expected = item.Key == SpiritualWoundCaptureCheckpointState.StatePath ? bytes : item.Value.Bytes;
                    if (!ExactBytes(await _validator._fs.ReadFileBytesAsync(lease, item.Key), expected))
                        return ProgressFailure("blocked", "spiritual_dependent_progress_postflight_changed");
                }
                return new("committed", null, []);
            }
            finally { _gate.Release(); }
        }
    }
}
