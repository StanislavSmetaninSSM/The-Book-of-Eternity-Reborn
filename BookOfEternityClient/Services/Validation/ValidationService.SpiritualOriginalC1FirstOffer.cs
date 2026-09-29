using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Carries a detached first decision packet and any unpublished instance admission row.
    /// No value in this result grants persistence or accepted publication authority.
    /// </summary>
    /// <param name="Pending">
    /// Strictly parsed first packet, or <see langword="null"/> when no eligible offer exists or validation fails.
    /// </param>
    /// <param name="ProposedInstanceRow">
    /// New derived instance row awaiting common publication, or <see langword="null"/> for a reused instance.
    /// </param>
    /// <param name="Issues">
    /// Validation failures; empty for a valid offer or a source without positive severity.
    /// </param>
    internal sealed record SpiritualC1FirstOfferResult(
        SpiritualWoundDecisionPendingState? Pending, JsonObject? ProposedInstanceRow,
        IReadOnlyList<ValidationIssue> Issues);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Composes the first unpublished spiritual decision packet from the current closed exchange.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease authenticating the original capture and signed snapshot.
        /// </param>
        /// <param name="interval">
        /// Exact latest closed interval owned by this capture.
        /// </param>
        /// <returns>
        /// A detached strict packet, no offer for a zero ceiling, or issues; no files are written.
        /// </returns>
        internal async Task<SpiritualC1FirstOfferResult> ComposeC1FirstOfferAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval)
        {
            ArgumentNullException.ThrowIfNull(interval);
            await _gate.WaitAsync();
            try { return await ComposeC1FirstOfferCoreAsync(lease, interval); }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Revalidates and composes the first packet while the caller holds the capture gate.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for the signed and physical owner checks.
        /// </param>
        /// <param name="interval">
        /// Exact current closed interval.
        /// </param>
        /// <returns>
        /// Strict detached first packet, no offer, or validation issues.
        /// </returns>
        private async Task<SpiritualC1FirstOfferResult> ComposeC1FirstOfferCoreAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval)
        {
                EnsureCurrent(lease);
                if (_closedExchangeEvidence.Count != _nextResourceOrdinal ||
                    _closedExchangeEvidence.Count == 0 ||
                    !ReferenceEquals(_closedExchangeEvidence[^1].Interval, interval) ||
                    _resources is null || !_resources.Owns(interval) ||
                    _effects is null || _originalOutputProjection is null)
                    return FirstOfferFailure("spiritual_first_offer_frontier_mismatch");
                if (_woundSelections.Count != 0 || _woundDeclines.Count != 0 ||
                    _effects.WoundReadVersion != 0)
                    return FirstOfferFailure("spiritual_first_offer_already_decided");
                var physicalIssues = await CheckRetainedInputsAsync(lease);
                if (physicalIssues.Count != 0)
                    return new(null, null, physicalIssues);
                if (!_usesColdOriginalInputs)
                {
                    foreach (var path in _draftInputs.PathInventory.Where(path =>
                                 !_observed.ContainsKey(path)))
                    {
                        var bytes = await _validator._fs.ReadFileBytesAsync(lease, path);
                        if (_draftInputs.ReadImage(path).Fingerprint ==
                            new CanonicalBeforeImage(bytes is not null, bytes).Fingerprint)
                            continue;
                        RevokeUnderLease(lease);
                        return FirstOfferFailure("spiritual_first_offer_registered_input_changed");
                    }
                }
                var sourceIssues = await _source.CheckContinuationInputsAsync(lease);
                if (sourceIssues.Count != 0)
                    return new(null, null, sourceIssues);
                EnsureCurrent(lease);
                var signed = ReadVerifiedSignedC1OriginCore(lease);
                var creationReceiptIssues = ValidateSignedOriginalCreationReceipts(lease, signed.Receipt);
                if (creationReceiptIssues.Count != 0)
                    return new(null, null, creationReceiptIssues);
                if (!_source.TryReadInitialActiveExchangeInventory(out var conflictId,
                        out var originalExchangeIds) ||
                    conflictId != interval.ConflictId ||
                    _closedExchangeEvidence.Count > originalExchangeIds.Length ||
                    _closedExchangeEvidence.Where((entry, index) =>
                        entry.Interval.Ordinal != index || entry.Interval.ConflictId != conflictId ||
                        entry.Interval.ExchangeId != originalExchangeIds[index] ||
                        entry.Sources.Any(source => !_source.Owns(source))).Any())
                    return FirstOfferFailure("spiritual_first_offer_original_inventory_mismatch");
                if (_closedExchangeEvidence.Take(_closedExchangeEvidence.Count - 1)
                    .SelectMany(entry => entry.Sources)
                    .Any(source => source.Calculation.MaximumSeverityRank > 0 ||
                        source.GuaranteedSeverityRank != null))
                    return FirstOfferFailure("spiritual_first_offer_prior_decision_unresolved");
                if (!_closedExchangeEvidence[^1].Sources.Any(source =>
                        source.Calculation.MaximumSeverityRank > 0 ||
                        source.GuaranteedSeverityRank != null))
                    return new(null, null, []);
                var instance = SpiritualWoundFirstOfferInstanceResolver.Resolve(
                    signed.Receipt, signed.Conflict, _source.Realm, _source.SessionId,
                    _source.RequestId, _source.SnapshotToken, _source.TurnNumber);
                if (instance.InstanceId is null || instance.Issues.Count != 0)
                    return new(null, null, instance.Issues);
                var inventory = ReadC1ImageInventoryCore();
                if (inventory.BeforeImages.Count != inventory.RegisteredPaths.Count ||
                    inventory.RegisteredPaths.Any(path => !inventory.BeforeImages.ContainsKey(path)))
                    return FirstOfferFailure("spiritual_first_offer_rollback_incomplete");
                var composedImages = await ReadC1CandidateOwnerImagesCoreAsync(lease, interval);
                if (composedImages.Images is not { } changed)
                    return new(null, null, composedImages.Issues);
                if (changed.Keys.Any(path => !inventory.BeforeImages.ContainsKey(path)))
                    return FirstOfferFailure("spiritual_first_offer_candidate_unregistered");
                var candidate = inventory.BeforeImages.ToDictionary(pair => pair.Key,
                    pair => new CanonicalBeforeImage(pair.Value.Existed, pair.Value.Bytes),
                    StringComparer.Ordinal);
                foreach (var pair in changed)
                    candidate[pair.Key] = new CanonicalBeforeImage(pair.Value.Existed, pair.Value.Bytes);
                var response = _draftInputs.ReadImage(FixedOriginalOutputPaths[0]);
                if (!response.Existed || response.Bytes is null)
                    return FirstOfferFailure("spiritual_first_offer_original_response_missing");
                var originalDice = _source.ReadOriginalAcceptedD20Values();
                try
                {
                    var root = ComposeFirstOfferRoot(instance.InstanceId,
                        signed.SnapshotFingerprint, originalExchangeIds.Length, originalDice,
                        inventory.RegisteredPaths, inventory.BeforeImages, candidate,
                        response.Bytes);
                    var parsed = SpiritualWoundDecisionPendingState.Parse(root.ToJsonString(),
                        SpiritualWoundDecisionPendingState.StatePath, inventory.RegisteredPaths);
                    if (!parsed.IsValid || parsed.State is null)
                        return new(null, null, parsed.Issues);
                    return new(parsed.State, instance.ProposedRow?.DeepClone().AsObject(), []);
                }
                catch (Exception error) when (error is ArgumentException or FormatException or
                    InvalidOperationException or OverflowException or System.Text.Json.JsonException)
                {
                    return FirstOfferFailure("spiritual_first_offer_composition_invalid");
                }
        }

        private JsonObject ComposeFirstOfferRoot(string instanceId, string originalFingerprint,
            int exchangeCount, int[] originalDice, IReadOnlyList<string> registeredPaths,
            IReadOnlyDictionary<string, CanonicalBeforeImage> before,
            IReadOnlyDictionary<string, CanonicalBeforeImage> candidate, byte[] responseBytes)
        {
            var bounds = new JsonObject
            {
                ["exchangeCount"] = exchangeCount,
                ["sourceSlotCount"] = checked(2 * exchangeCount),
                ["diceCount"] = originalDice.Length,
                ["imagePathCount"] = registeredPaths.Count
            };
            var claims = new JsonArray();
            var sources = new JsonArray();
            var seenDice = new HashSet<int>();
            foreach (var entry in _closedExchangeEvidence)
            {
                var exchangeClaims = new JsonArray();
                foreach (var die in entry.DiceClaims.OrderBy(die => die.SourceIndex))
                {
                    if (die.SourceIndex < 0 || die.SourceIndex >= originalDice.Length ||
                        originalDice[die.SourceIndex] != die.Value || !seenDice.Add(die.SourceIndex))
                        throw new InvalidOperationException("The closed exchange reuses a signed die.");
                    var claim = new JsonObject
                    {
                        ["sourceIndex"] = die.SourceIndex,
                        ["value"] = die.Value,
                        ["exchangeOrdinal"] = entry.Interval.Ordinal,
                        ["claimFingerprint"] = ""
                    };
                    claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim);
                    claims.Add(claim.DeepClone());
                    exchangeClaims.Add(claim);
                }
                foreach (var source in entry.Sources)
                    sources.Add(ComposeFirstOfferSource(source, entry, sources.Count,
                        bounds, originalDice, exchangeClaims, originalFingerprint));
            }
            var nextSource = 0;
            foreach (var source in sources)
            {
                if (source!["maximumSeverityRank"]!.GetValue<int>() > 0 ||
                    source["guaranteedSeverityRank"] is not null) break;
                nextSource++;
            }
            var packet = new JsonObject
            {
                ["sessionId"] = _source.SessionId,
                ["requestId"] = _source.RequestId,
                ["snapshotToken"] = _source.SnapshotToken,
                ["turn"] = _source.TurnNumber,
                ["realm"] = _source.Realm,
                ["continuationGeneration"] = 1,
                ["conflictInstanceRef"] = instanceId,
                ["originalSnapshotFingerprint"] = originalFingerprint,
                ["retainedPrefixFingerprint"] = "",
                ["bounds"] = bounds,
                ["cursor"] = new JsonObject
                {
                    ["exchangeOrdinal"] = _closedExchangeEvidence.Count,
                    ["waveOrdinal"] = 0,
                    ["nextSourceOrdinal"] = nextSource
                },
                ["sources"] = sources,
                ["stagedDecisions"] = new JsonArray(),
                ["diceClaims"] = claims,
                ["beforeImages"] = FirstOfferImages(registeredPaths, before, "original_before"),
                ["candidateAfterImages"] = FirstOfferImages(registeredPaths, candidate, "candidate_after"),
                ["preservedDraft"] = new JsonObject
                {
                    ["contentBase64"] = Convert.ToBase64String(responseBytes),
                    ["contentFingerprint"] = RawFingerprint(responseBytes)
                },
                ["packetFingerprint"] = ""
            };
            packet["retainedPrefixFingerprint"] =
                SpiritualWoundDecisionPendingState.ComputePrefixFingerprint(packet);
            packet["packetFingerprint"] =
                SpiritualWoundStateJson.Hash(packet, "pending_packet", "packetFingerprint");
            return new JsonObject { ["schemaVersion"] = 1, ["pending"] = packet };
        }

        private JsonObject ComposeFirstOfferSource(PreparedSpiritualSource source,
            RetainedClosedExchange entry, int sourceOrdinal, JsonObject bounds,
            IReadOnlyList<int> originalDice, JsonArray exchangeClaims,
            string originalFingerprint)
        {
            var acting = FirstOfferActor(source.ActingActor);
            var affected = FirstOfferActor(source.AffectedActor);
            var art = source.OriginalSpecialArtJson is null ? null :
                SpiritualWoundStateJson.Parse(source.OriginalSpecialArtJson);
            var calculation = source.Calculation;
            var selected = new JsonArray(entry.DiceClaims.OrderBy(claim => claim.SourceIndex)
                .Select(claim => (JsonNode?)JsonValue.Create(claim.SourceIndex)).ToArray());
            var evidence = new JsonObject
            {
                ["sessionId"] = _source.SessionId,
                ["requestId"] = _source.RequestId,
                ["snapshotToken"] = _source.SnapshotToken,
                ["turn"] = _source.TurnNumber,
                ["realm"] = _source.Realm,
                ["originalSnapshotFingerprint"] = originalFingerprint,
                ["bounds"] = bounds.DeepClone(),
                ["acceptedD20Values"] = new JsonArray(originalDice
                    .Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
                ["diceClaims"] = exchangeClaims.DeepClone()
            };
            var witness = new JsonObject
            {
                ["sourceId"] = "",
                ["coordinate"] = source.Coordinate,
                ["conflictId"] = source.ConflictId,
                ["exchangeId"] = source.ExchangeId,
                ["turnEvidence"] = evidence,
                ["sourceOrdinal"] = sourceOrdinal,
                ["exchangeOrdinal"] = entry.Interval.Ordinal,
                ["affectedSide"] = source.AffectedSide,
                ["actingActor"] = acting,
                ["affectedActor"] = affected,
                ["operation"] = source.Operation,
                ["appliedArtId"] = art?["artId"]?.GetValue<string>() ??
                    source.ActingActor + ":" + source.Operation,
                ["appliedArtKind"] = art is null ? "standard" : "special",
                ["appliedArtTier"] = calculation.Input.AppliedArtTier,
                ["targetResilienceTier"] = calculation.Input.TargetResilienceTier,
                ["priorStrainRank"] = calculation.PreviousStrainRank,
                ["destinationStrainRank"] = calculation.NewStrainRank,
                ["extraStrainJumps"] = calculation.ExtraJumpSteps,
                ["harmfulMargin"] = calculation.Input.HarmfulMargin,
                ["maximumSeverityRank"] = calculation.MaximumSeverityRank,
                ["guaranteedSeverityRank"] = source.GuaranteedSeverityRank,
                ["dangerMode"] = calculation.Input.DangerMode,
                ["dangerDeclarationFingerprint"] = entry.DangerDeclarationFingerprint,
                ["escalationFingerprint"] = null,
                ["retraumaWoundId"] = source.RetraumaWoundId,
                ["retraumaWoundJsonBase64"] = CanonicalBase64(source.RetraumaWoundJson),
                ["exchangeJsonBase64"] = CanonicalBase64(source.ExchangeJson),
                ["specialArtJsonBase64"] = CanonicalBase64(source.OriginalSpecialArtJson),
                ["selectedDiceIndices"] = selected,
                ["resourceResultFingerprint"] = entry.ResourceResultFingerprint,
                ["effectPlanFingerprint"] = entry.EffectPlanFingerprint,
                ["conflictBeforeFingerprint"] = entry.ConflictBeforeFingerprint,
                ["conflictAfterFingerprint"] = entry.ConflictAfterFingerprint,
                ["sourceFingerprint"] = ""
            };
            var fingerprint = SpiritualWoundStateJson.Hash(witness, "source", "sourceId", "sourceFingerprint");
            witness["sourceFingerprint"] = fingerprint;
            witness["sourceId"] = "spiritual_source_" + fingerprint[7..];
            return witness;
        }

        private static JsonObject FirstOfferActor(string coordinate)
        {
            var parts = coordinate.Split(':', 2);
            if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
                throw new FormatException("The source actor coordinate is incomplete.");
            return new JsonObject { ["actorKind"] = parts[0], ["actorId"] = parts[1] };
        }

        private static string? CanonicalBase64(string? json) => json is null ? null :
            Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(
                SpiritualWoundStateJson.Parse(json))));

        private static JsonArray FirstOfferImages(IReadOnlyList<string> paths,
            IReadOnlyDictionary<string, CanonicalBeforeImage> images, string role)
        {
            var rows = new JsonArray();
            foreach (var path in paths)
            {
                var image = images[path];
                rows.Add(new JsonObject
                {
                    ["path"] = path,
                    ["role"] = role,
                    ["existed"] = image.Existed,
                    ["contentBase64"] = image.Bytes is null ? null : Convert.ToBase64String(image.Bytes),
                    ["contentFingerprint"] = image.Fingerprint
                });
            }
            return rows;
        }

        private static string RawFingerprint(byte[] bytes) => "sha256:" +
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        private static SpiritualC1FirstOfferResult FirstOfferFailure(string code) =>
            new(null, null,
            [
                new ValidationIssue(AfterlifeSpiritualConflictState.StatePath,
                    IssueSeverity.Error, "The original spiritual exchange cannot start a first wound offer.",
                    code: code, section: "AcceptedTurnWoundMaterialization")
            ]);
    }
}
