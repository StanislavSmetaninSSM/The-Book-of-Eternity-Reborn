using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Retains detached comparison data from one disposable continuation walk.
    /// </summary>
    /// <param name="Policy">
    /// Stable permissions proved from the committed raw baseline.
    /// </param>
    /// <param name="Issues">
    /// Baseline dependency diagnostics or the first remaining current-draft failure.
    /// </param>
    internal sealed record SpiritualC2DependentWalk(SpiritualWoundDependentDraftPolicy Policy,
        IReadOnlyList<ValidationIssue> Issues);

    internal sealed partial class SpiritualC2PrivateSession
    {
        /// <summary>
        /// Walks baseline and actual corrections on disposable owners, then restores an unadvanced execution owner.
        /// </summary>
        /// <param name="lease">
        /// Active lease retained across sequential replay and exact physical postflight checks.
        /// </param>
        /// <param name="previewNext">
        /// Whether to prove one actual uncommitted frontier without activating its successor.
        /// </param>
        /// <returns>
        /// Stable comparison context, or null when complete bounded dependencies cannot be proved.
        /// </returns>
        private async Task<SpiritualC2DependentContext?> ReadSequentialDependentContextAsync(
            FileSystemManager.CanonicalWriteLease lease, bool previewNext = false)
        {
            var succeeded = false;
            try
            {
                var witness = await _capture.ReadC2DependentWitnessAsync(lease);
                var baseline = await _capture.WalkC2DependentContextAsync(lease, null, previewNext);
                _capture.Dispose();
                if (baseline is null) return null;

                var currentOpen = await _validator.ClassifySpiritualPendingAsync(lease);
                if (currentOpen.Disposition != "match" || currentOpen.Capture is not { } current)
                {
                    currentOpen.Capture?.Dispose();
                    return null;
                }
                SpiritualC2DependentWalk? actual;
                using (current)
                {
                    if (!SameWitness(witness, await current.ReadC2DependentWitnessAsync(lease))) return null;
                    actual = await current.WalkC2DependentContextAsync(lease, baseline.Policy);
                }
                if (actual is null) return null;

                var finalOpen = await _validator.ClassifySpiritualPendingAsync(lease);
                if (finalOpen.Disposition != "match" || finalOpen.Capture is not { } execution)
                {
                    finalOpen.Capture?.Dispose();
                    return null;
                }
                _capture = execution;
                if (!SameWitness(witness, await execution.ReadC2DependentWitnessAsync(lease))) return null;
                var id = SpiritualOriginalTurnCapture.DependentContextFingerprint(
                    witness[SpiritualWoundCaptureCheckpointState.StatePath].Bytes!,
                    witness[SpiritualWoundDecisionPendingState.StatePath].Bytes!, baseline.Policy.Fields);
                succeeded = true;
                return new(id, baseline.Issues, actual.Issues)
                { DependentDraftFields = baseline.Policy.Fields, DraftPolicy = baseline.Policy };
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or JsonException or OverflowException or DecoderFallbackException)
            {
                return null;
            }
            finally
            {
                if (!succeeded) _capture.Dispose();
            }
        }

        /// <summary>
        /// Compares every observed draft and both private roots without interpreting their contents.
        /// </summary>
        /// <param name="first">
        /// Exact physical inputs captured before speculative execution.
        /// </param>
        /// <param name="second">
        /// Exact physical inputs after reopening a genuine saved-choice owner.
        /// </param>
        /// <returns>
        /// True only when path membership, presence and bytes remain identical.
        /// </returns>
        private static bool SameWitness(IReadOnlyDictionary<string, CanonicalBeforeImage> first,
            IReadOnlyDictionary<string, CanonicalBeforeImage> second) =>
            first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out var image) &&
                pair.Value.Existed == image.Existed && (pair.Value.Bytes is null ? image.Bytes is null :
                    image.Bytes is not null && pair.Value.Bytes.AsSpan().SequenceEqual(image.Bytes)));
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Gets whether multiple exchanges or an independently eligible last-binding result require sequential continuation.
        /// </summary>
        internal bool NeedsSequentialDependentContext => HasC2PendingSubmission &&
            _source.TryReadInitialActiveExchangeInventory(out _, out var ids) &&
            (ids.Length - _closedExchangeEvidence.Count > 1 || _c2SelectedDecision?.BindingOriginal is not null);

        /// <summary>
        /// Binds detached fields to the exact private pair without granting execution authority.
        /// </summary>
        /// <param name="checkpoint">
        /// Matched physical checkpoint bytes containing the saved choice.
        /// </param>
        /// <param name="pending">
        /// Matched physical committed packet bytes.
        /// </param>
        /// <param name="fields">
        /// Deterministically sorted owner-derived raw permissions.
        /// </param>
        /// <returns>
        /// A comparison-only correlation independent of corrected values and current diagnostics.
        /// </returns>
        internal static string DependentContextFingerprint(byte[] checkpoint, byte[] pending,
            IReadOnlyList<SpiritualWoundContinuationField> fields) =>
            RawFingerprint(Encoding.UTF8.GetBytes("spiritual_dependent_context_v1:" +
                RawFingerprint(checkpoint) + ":" + RawFingerprint(pending) + ":" + JsonSerializer.Serialize(fields)));

        /// <summary>
        /// Reads all physical drafts and controls under an authenticated unadvanced saved-selection owner.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease covering the complete witness.
        /// </param>
        /// <returns>
        /// Detached exact images; invalid ownership or changed saved command throws.
        /// </returns>
        internal async Task<IReadOnlyDictionary<string, CanonicalBeforeImage>> ReadC2DependentWitnessAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                var checkpoint = await RequireC2PendingSubmissionCoreAsync(lease);
                await ReadC2SubmissionChangesCoreAsync(lease, checkpoint);
                var result = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
                foreach (var path in _draftInputs.PathInventory.Concat(new[]
                { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath }).Distinct())
                {
                    var bytes = await _validator._fs.ReadFileBytesAsync(lease, path);
                    result[path] = new(bytes is not null, bytes);
                }
                await RequireC2PendingSubmissionCoreAsync(lease);
                return result;
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Executes diagnostic zero-offer intervals without saving a decision, packet or accepted output.
        /// The caller must dispose this owner after either success or failure.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease covering owner replay and input reads.
        /// </param>
        /// <param name="baselinePolicy">
        /// Null derives baseline permissions using uniquely determined ephemeral corrections;
        /// a supplied policy validates and executes only actual physical corrections.
        /// </param>
        /// <param name="previewNext">
        /// Whether the actual current correction may be probed after all persisted progress rows.
        /// </param>
        /// <param name="replayCheckpoint">
        /// Authenticated original replay checkpoint, or null for a matched physical session.
        /// </param>
        /// <param name="replayPending">
        /// Original-owner derived pending packet paired with the replay checkpoint; never public transport.
        /// </param>
        /// <returns>
        /// Detached policy and diagnostics, or null when bounded owner proof is unavailable.
        /// </returns>
        internal async Task<SpiritualC2DependentWalk?> WalkC2DependentContextAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualWoundDependentDraftPolicy? baselinePolicy,
            bool previewNext = false, SpiritualWoundCaptureCheckpointState? replayCheckpoint = null,
            SpiritualWoundDecisionPendingState? replayPending = null)
        {
            await _gate.WaitAsync();
            try
            {
                var checkpoint = replayCheckpoint ?? await RequireC2PendingSubmissionCoreAsync(lease);
                var committed = ReadC2CommittedInputLayer(checkpoint);
                var changes = await ReadC2SubmissionChangesCoreAsync(lease, checkpoint);
                var path = AfterlifeSpiritualConflictState.StatePath;
                if (_coldCurrentInputs is null || _coldProposedInputs is not null ||
                    !SameExactImage(committed[path], _coldCurrentInputs.ReadImage(path))) return null;
                var original = _source.ReadMechanicsConflict(this);
                var currentImage = changes.FirstOrDefault(change => change.Path == path).Image ?? committed[path];
                if (committed[path].Bytes is null || currentImage.Bytes is null) return null;
                var baseline = SpiritualWoundDependentDraftPolicy.ReadStrictRoot(SpiritualWoundStateJson.DecodeUtf8JsonText(committed[path].Bytes!));
                var actualRaw = SpiritualWoundDependentDraftPolicy.ReadStrictRoot(SpiritualWoundStateJson.DecodeUtf8JsonText(currentImage.Bytes!));
                var raw = baselinePolicy is null ? baseline.DeepClone().AsObject() : actualRaw.DeepClone().AsObject();
                var policy = baselinePolicy ?? SpiritualWoundDependentDraftPolicy.Create(original, baseline, [], null)!;
                if (!policy.Allows(raw) || !_source.TryReadInitialActiveExchangeInventory(out _, out var ids)) return null;
                var progress = baselinePolicy is null
                    ? checkpoint.ReadPendingSubmission()?["dependentDraftProgress"] as JsonArray ?? new JsonArray()
                    : new JsonArray();
                var remaining = ids.Length - _closedExchangeEvidence.Count;
                if (progress.Count > remaining || progress.Count == remaining &&
                    (remaining != 1 || _c2SelectedDecision?.BindingOriginal is null)) return null;
                var pending = replayPending ?? _matchedC2Pair!.Pending;
                var pendingBytes = Encoding.UTF8.GetBytes(SpiritualWoundDecisionPendingState.SerializeCanonical(pending));
                if (progress.Count > 0)
                {
                    var physicalCheckpoint = replayCheckpoint is null ? _matchedC2Pair!.CheckpointBytes :
                        await _validator._fs.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath);
                    if (!ExactBytes(Encoding.UTF8.GetBytes(SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint)),
                            physicalCheckpoint)) return null;
                }
                var acceptedLayer = new Dictionary<string, CanonicalBeforeImage>(committed, StringComparer.Ordinal);
                var progressIndex = 0;
                var awaitingActualNarration = false;
                var previewed = false;
                if (!_allocations.Journal.IsAppendMode) _allocations.Journal.EnableAppendAfterReplay();
                var packet = _c2SelectedDecision!.PacketRoot["pending"]!.DeepClone().AsObject();
                var issues = new List<ValidationIssue>();
                var diagnosedCoordinates = new HashSet<(int Ordinal, string Path)>();
                SpiritualC2DependentWalk? Result(IReadOnlyList<ValidationIssue> diagnostics) =>
                    progressIndex != progress.Count || awaitingActualNarration ||
                    (progress.Count > 0 || previewed) && policy.Fields.Count == 0
                        ? null : new(policy, diagnostics);
                while (packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>() == packet["sources"]!.AsArray().Count &&
                       _closedExchangeEvidence.Count < ids.Length)
                {
                    var continued = await AdvanceNextResourceExchangeUnderGateAsync(lease,
                        new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
                        { [path] = new(true, Encoding.UTF8.GetBytes(raw.ToJsonString())) });
                    SpiritualWoundDependentDraftPolicy? terminalPolicy = null;
                    IReadOnlyList<ValidationIssue> terminalIssues = [];
                    if (_closedExchangeEvidence.Count + 1 == ids.Length && continued.Issues.Count != 0 &&
                        continued.Issues.All(issue => issue.Code == "afterlife_conflict_control_snapshot_missing" &&
                            issue.FilePath == "activeConflict.controlState") &&
                        policy.TryProjectBindingTerminalControl(raw, out var projected, out var proposedTerminal))
                    {
                        terminalIssues = continued.Issues.ToArray();
                        var checkedTerminal = await AdvanceNextResourceExchangeUnderGateAsync(lease,
                            new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
                            { [path] = new(true, Encoding.UTF8.GetBytes(projected!.ToJsonString())) });
                        // Every ordinary rule and actual resource execution must pass; only the independently proved echo was replaced.
                        if (checkedTerminal.Issues.Count != 0 || checkedTerminal.Step?.Interval is null) return null;
                        continued = checkedTerminal;
                        terminalPolicy = proposedTerminal;
                    }
                    if (continued.Issues.Count != 0 || continued.Step?.Interval is null)
                    {
                        if (baselinePolicy?.IsTerminalControl == true && continued.Issues.Count != 0)
                            return Result(continued.Issues.ToArray());
                        if (continued.Issues.Count == 0 || !IsCorrectableDependentConflictFailure(continued.Issues)) return null;
                        if (baselinePolicy is not null) return Result(continued.Issues.ToArray());
                        if (awaitingActualNarration) return null;
                        var mechanicsIssues = new List<ValidationIssue>();
                        var mechanics = PrepareSourceMechanics(_source, mechanicsIssues);
                        var conflict = original["activeConflict"] as JsonObject;
                        if (mechanics is null || mechanicsIssues.Count != 0 || conflict is null) return null;
                        var frontier = mechanics.ReadActionPointBefore(conflict["conflictId"]!.GetValue<string>(), mechanics.Ordinal);
                        var nextPolicy = CreateC2DependentPolicy(original, baseline, continued.Issues, frontier, mechanics);
                        if (nextPolicy is null) return null;
                        policy = policy.Merge(nextPolicy);
                        issues.AddRange(continued.Issues);
                        if (_closedExchangeEvidence.Count + 1 == ids.Length && !nextPolicy.RequiresActualBindingResult)
                            return Result(issues.ToArray());
                        var discoveredCoordinate = false;
                        foreach (var issue in continued.Issues)
                            discoveredCoordinate |= diagnosedCoordinates.Add((_closedExchangeEvidence.Count, issue.FilePath));
                        var previousRaw = raw.DeepClone();
                        if (!discoveredCoordinate) return null;
                        if (!nextPolicy.TryApplyPositionCorrections(raw))
                        {
                            if (progressIndex < progress.Count)
                            {
                                var row = progress[progressIndex]!.AsObject();
                                var prefix = checkpoint.ReadDependentProgressPrefix(progressIndex);
                                var id = DependentContextFingerprint(Encoding.UTF8.GetBytes(
                                    SpiritualWoundCaptureCheckpointState.SerializeCanonical(prefix)), pendingBytes, policy.Fields);
                                if (row["acceptedContinuationId"]!.GetValue<string>() != id ||
                                    !JsonNode.DeepEquals(row["dependentDraftFields"], SerializeDependentProgressFields(policy.Fields))) return null;
                                foreach (var imageRow in row["inputChanges"]!.AsArray())
                                {
                                    var imagePath = imageRow!["path"]!.GetValue<string>();
                                    var image = new CanonicalBeforeImage(true,
                                        Convert.FromBase64String(imageRow["contentBase64"]!.GetValue<string>()));
                                    if (!acceptedLayer.TryGetValue(imagePath, out var before) || SameExactImage(before, image)) return null;
                                    if (imagePath == path)
                                    {
                                        var candidate = SpiritualWoundDependentDraftPolicy.ReadStrictRoot(SpiritualWoundStateJson.DecodeUtf8JsonText(image.Bytes!));
                                        if (!policy.Allows(candidate)) return null;
                                        raw = candidate;
                                    }
                                    else if (imagePath != FixedOriginalOutputPaths[0] ||
                                        !ValidC2DependentNarrative(pending, AcceptedTurnOutputProjector.ProjectNarrative(
                                            SpiritualWoundStateJson.DecodeUtf8JsonText(image.Bytes!)).Json)) return null;
                                    acceptedLayer[imagePath] = image;
                                }
                            }
                            else
                            {
                                if (!previewNext || previewed) return Result(issues.ToArray());
                                // The complete actual candidate must pass A before any successor policy exists.
                                if (!policy.Allows(actualRaw) || JsonNode.DeepEquals(actualRaw, previousRaw)) return null;
                                raw = actualRaw.DeepClone().AsObject();
                                previewed = true;
                            }
                            awaitingActualNarration = true;
                            continue;
                        }
                        if (!TryCorrectC2CostDraft(original, raw, mechanics, frontier,
                                continued.Issues.Where(issue => PositionDependencyIndex(issue) is null).ToArray()) ||
                            JsonNode.DeepEquals(previousRaw, raw) || !policy.Allows(raw)) return null;
                        continue;
                    }
                    if (!AppendC2NextExchangeEvidence(packet)) return null;
                    if (baselinePolicy is not null && terminalPolicy is not null)
                        return Result(terminalIssues);
                    if (awaitingActualNarration)
                    {
                        if (policy.RequiresActualBindingResult && terminalPolicy is null) return null;
                        if (progressIndex < progress.Count) progressIndex++;
                        baseline = raw.DeepClone().AsObject();
                        policy = terminalPolicy ?? SpiritualWoundDependentDraftPolicy.Create(original, baseline, [], null)!;
                        issues.Clear();
                        issues.AddRange(terminalIssues);
                        awaitingActualNarration = false;
                    }
                }
                return Result(issues.ToArray());
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Applies only uniquely determined action payments and recoveries to a detached diagnostic image.
        /// </summary>
        /// <param name="original">
        /// Signed conflict membership and original raw-carrier precedence.
        /// </param>
        /// <param name="raw">
        /// Private diagnostic image; this method never writes it to storage.
        /// </param>
        /// <param name="mechanics">
        /// Genuine current resource/effect frontier after all earlier diagnostic exchanges executed.
        /// </param>
        /// <param name="frontier">
        /// Actual balances captured from the same current mechanics owner.
        /// </param>
        /// <param name="issues">
        /// Ordinary validation diagnostics identifying exact failed cost coordinates.
        /// </param>
        /// <returns>
        /// True when every coordinate admits an affordable unique numeric correction or conditional force-audit absence;
        /// false for ambiguous, unrelated or unsupported corrections.
        /// </returns>
        private bool TryCorrectC2CostDraft(JsonObject original, JsonObject raw,
            AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext mechanics,
            AfterlifeConflictActionPointProjection frontier, IReadOnlyList<ValidationIssue> issues)
        {
            foreach (var issue in issues)
            {
                var match = Regex.Match(issue.FilePath,
                    @"^activeConflict\.exchangeLog\[(\d+)\]\.actionCostAudit\.(player|opposition)(?:\.(effectiveCost|before|after))?$",
                    RegexOptions.CultureInvariant);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out var index) ||
                    AfterlifeSpiritualConflictState.ResolveRawExchange(original, raw, index) is not { } source)
                    return false;
                var side = match.Groups[2].Value;
                var exchange = source.Exchange;
                if (!ConflictTokenEquals(ResolveSpiritualCostOperation(exchange, side), "force_incarnation"))
                {
                    if (_source.ProjectDependentOrdinaryCost(this, exchange, side) is not { } expected) return false;
                    var ordinaryAudit = exchange["actionCostAudit"]![side]!;
                    ordinaryAudit["effectiveCost"] = expected.Cost;
                    ordinaryAudit["before"] = expected.Before;
                    ordinaryAudit["after"] = expected.After;
                    continue;
                }
                var burden = SpiritualWoundSourceSession.ReadCurrentActionCostBurden(
                    mechanics, original["activeConflict"]!.AsObject(), exchange, side);
                var before = side == "player" ? frontier.Player.Current : frontier.Opposition.Current;
                if (burden < 0 || burden > int.MaxValue || before < burden || before > int.MaxValue ||
                    decimal.Truncate(before) != before) return false;
                if (burden == 0)
                {
                    if (exchange["actionCostAudit"] is JsonObject audit)
                    {
                        audit.Remove(side);
                        if (audit.Count == 0) exchange.Remove("actionCostAudit");
                    }
                }
                else
                {
                    if (exchange["actionCostAudit"] is not JsonObject) exchange["actionCostAudit"] = new JsonObject();
                    if (exchange["actionCostAudit"]![side] is not JsonObject)
                        exchange["actionCostAudit"]![side] = new JsonObject
                        {
                            ["operationType"] = "force_incarnation", ["baseCost"] = 0, ["minCost"] = 0,
                            ["artTier"] = 0
                        };
                    var audit = exchange["actionCostAudit"]![side]!.AsObject();
                    audit["effectiveCost"] = (int)burden;
                    audit["before"] = (int)before;
                    audit["after"] = (int)(before - burden);
                }
            }
            return true;
        }
    }
}
