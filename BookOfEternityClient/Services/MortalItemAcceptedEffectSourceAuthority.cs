using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalItemAcceptedTurnOwner(
    string ItemId,
    string? ItemRef,
    string FilePath,
    string JsonPath,
    MortalItemCarrierCoordinate Carrier,
    JsonObject Item,
    bool SameTurn);

internal sealed record MortalItemTreatmentPublicationBaselineSealResult(
    MortalItemAcceptedTurnNormalizationSnapshot? Snapshot,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Snapshot is not null && Issues.Count == 0;
}

internal sealed class MortalItemAcceptedTurnNormalizationSnapshot
{
    private readonly Dictionary<string, string> _itemIdsByCreationRef;
    private readonly Dictionary<string, string> _receiptIdsByCreationRef;
    private readonly Dictionary<string, string> _createTransitionIdsByCreationRef;
    private readonly Dictionary<string, string> _transferTransitionIdsByItemId;
    private readonly Dictionary<string, MortalItemRouteAuthority> _routesByCreationRef;
    private readonly MortalItemAcceptedTransfer[] _transfers;
    private readonly Dictionary<string, JsonNode?> _currentRoots;
    private readonly Dictionary<string, JsonNode?> _backupRoots;
    private readonly Dictionary<string, string> _creationPathsByCreationRef;
    private readonly Dictionary<string, int> _creationOrdinalsByCreationRef;
    private readonly MortalTreatmentItemCommandEnvelope? _itemCommandEnvelope;
    private readonly NpcCoreChangesContract.Authority? _npcCoreAuthority;
    private readonly CanonicalBeforeImage? _npcTradePending;
    private readonly CanonicalBeforeImage? _trainingPending;
    private readonly MortalItemNpcTradeTailDisposition? _npcTradeDisposition;
    private readonly Dictionary<string, JsonNode?> _itemPhaseAfterImages;
    private readonly JsonObject? _itemPhaseIdentityIndexAfterImage;
    private readonly string _itemPhaseFingerprint;
    private readonly Dictionary<string, JsonNode?> _finalCarrierRoots;
    private readonly JsonObject? _finalIdentityIndexAfterImage;
    private readonly string[] _appliedTransformIds;
    private readonly string _finalBaselineFingerprint;

    internal MortalItemAcceptedTurnNormalizationSnapshot(
        string sessionId,
        string snapshotToken,
        int turn,
        IReadOnlyDictionary<string, string> itemIdsByCreationRef,
        IReadOnlyDictionary<string, MortalItemRouteAuthority> routesByCreationRef,
        IReadOnlyList<MortalItemAcceptedTransfer> transfers,
        IReadOnlyDictionary<string, JsonNode?> currentRoots,
        IReadOnlyDictionary<string, JsonNode?> backupRoots,
        IReadOnlyDictionary<string, string> creationPathsByCreationRef,
        IReadOnlyDictionary<string, int> creationOrdinalsByCreationRef,
        MortalTreatmentItemCommandEnvelope? itemCommandEnvelope = null,
        NpcCoreChangesContract.Authority? npcCoreAuthority = null,
        CanonicalBeforeImage? npcTradePending = null,
        CanonicalBeforeImage? trainingPending = null,
        MortalItemNpcTradeTailDisposition? npcTradeDisposition = null,
        MortalItemCanonicalProjectionResult? itemPhase = null,
        MortalItemPublicationBaselineResult? finalBaseline = null)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(sessionId))
            throw new ArgumentException("Expected an exact accepted-turn session ID.", nameof(sessionId));
        if (!ResourceMaterializationContract.IsExactIdentifier(snapshotToken))
            throw new ArgumentException("Expected an exact accepted-turn snapshot token.", nameof(snapshotToken));
        if (turn <= 0)
            throw new ArgumentOutOfRangeException(nameof(turn));
        ArgumentNullException.ThrowIfNull(itemIdsByCreationRef);
        ArgumentNullException.ThrowIfNull(routesByCreationRef);
        ArgumentNullException.ThrowIfNull(transfers);
        ArgumentNullException.ThrowIfNull(currentRoots);
        ArgumentNullException.ThrowIfNull(backupRoots);
        ArgumentNullException.ThrowIfNull(creationPathsByCreationRef);
        ArgumentNullException.ThrowIfNull(creationOrdinalsByCreationRef);

        SessionId = sessionId;
        SnapshotToken = snapshotToken;
        Turn = turn;
        _itemIdsByCreationRef = itemIdsByCreationRef.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);
        _routesByCreationRef = routesByCreationRef.ToDictionary(
            static pair => pair.Key,
            static pair => CloneRoute(pair.Value),
            StringComparer.Ordinal);
        _transfers = transfers.Select(CloneTransfer).ToArray();
        _currentRoots = CloneRoots(currentRoots);
        _backupRoots = CloneRoots(backupRoots);
        _creationPathsByCreationRef = creationPathsByCreationRef.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);
        _creationOrdinalsByCreationRef = creationOrdinalsByCreationRef.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);

        var hasAnyFinalBaselineBinding = itemCommandEnvelope is not null ||
                                         npcCoreAuthority is not null ||
                                         npcTradePending is not null ||
                                         trainingPending is not null ||
                                         npcTradeDisposition is not null ||
                                         itemPhase is not null ||
                                         finalBaseline is not null;
        var hasCompleteFinalBaselineBinding = itemCommandEnvelope is not null &&
                                              npcCoreAuthority is not null &&
                                              npcTradePending is not null &&
                                              trainingPending is not null &&
                                              npcTradeDisposition is not null &&
                                              itemPhase is not null &&
                                              finalBaseline is not null;
        if (hasAnyFinalBaselineBinding != hasCompleteFinalBaselineBinding)
        {
            throw new ArgumentException(
                "A treatment item snapshot requires either every final-baseline binding or none.");
        }

        _itemCommandEnvelope = itemCommandEnvelope?.Clone();
        _npcCoreAuthority = npcCoreAuthority is null
            ? null
            : CloneNpcCoreAuthority(npcCoreAuthority);
        _npcTradePending = CloneBeforeImage(npcTradePending);
        _trainingPending = CloneBeforeImage(trainingPending);
        _npcTradeDisposition = npcTradeDisposition;
        _itemPhaseAfterImages = itemPhase is null
            ? new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            : CloneRoots(itemPhase.ItemPhaseAfterImages);
        _itemPhaseIdentityIndexAfterImage = itemPhase?.IdentityIndexAfterImage
            .DeepClone().AsObject();
        _itemPhaseFingerprint = itemPhase?.Fingerprint ?? string.Empty;
        _finalCarrierRoots = finalBaseline is null
            ? new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            : CloneRoots(finalBaseline.FinalCarrierRoots);
        _finalIdentityIndexAfterImage = finalBaseline?.IdentityIndexAfterImage
            .DeepClone().AsObject();
        _appliedTransformIds = finalBaseline?.AppliedTransformIds.ToArray() ??
                               Array.Empty<string>();
        _finalBaselineFingerprint = finalBaseline?.Fingerprint ?? string.Empty;
        if (hasCompleteFinalBaselineBinding &&
            (itemPhase!.Issues.Count != 0 || finalBaseline!.Issues.Count != 0))
        {
            throw new ArgumentException(
                "A treatment item snapshot cannot seal an invalid projection result.");
        }

        _receiptIdsByCreationRef = new Dictionary<string, string>(StringComparer.Ordinal);
        _createTransitionIdsByCreationRef = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var creationRef in _itemIdsByCreationRef.Keys.OrderBy(
                     value => _creationOrdinalsByCreationRef.GetValueOrDefault(
                         value,
                         int.MaxValue)))
        {
            if (!_creationOrdinalsByCreationRef.TryGetValue(
                    creationRef,
                    out var creationOrdinal) ||
                creationOrdinal < 1)
            {
                throw new ArgumentException(
                    "Every accepted creation requires one positive production ordinal.",
                    nameof(creationOrdinalsByCreationRef));
            }
            var routeFingerprint = _routesByCreationRef.TryGetValue(
                creationRef,
                out var route)
                ? RouteFingerprint(route)
                : "missing";
            _receiptIdsByCreationRef.Add(
                creationRef,
                DeterministicId(
                    "mirec_",
                    "accepted_root_receipt",
                    creationRef,
                    routeFingerprint,
                    creationOrdinal));
            _createTransitionIdsByCreationRef.Add(
                creationRef,
                DeterministicId(
                    "mitrn_",
                    "accepted_create_transition",
                    creationRef,
                    routeFingerprint,
                    creationOrdinal));
        }

        _transferTransitionIdsByItemId = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < _transfers.Length; index++)
        {
            var transfer = _transfers[index];
            if (!_transferTransitionIdsByItemId.TryAdd(
                    transfer.ItemId,
                    DeterministicId(
                        "mitrn_",
                        "accepted_transfer_transition",
                        transfer.ItemId,
                        TransferFingerprint(transfer),
                        index + 1)))
            {
                throw new ArgumentException(
                    "Accepted transfer item IDs must be ordinal-unique.",
                    nameof(transfers));
            }
        }
        ProjectionProofFingerprint = ComputeProjectionProofFingerprint();
        ProofFingerprint = ComputeProofFingerprint();
    }

    internal string SessionId { get; }

    internal string SnapshotToken { get; }

    internal int Turn { get; }

    internal string ProofFingerprint { get; }

    internal string ProjectionProofFingerprint { get; }

    internal bool HasFinalPublicationBaseline =>
        _itemCommandEnvelope is not null &&
        _npcCoreAuthority is not null &&
        _npcTradePending is not null &&
        _trainingPending is not null &&
        _npcTradeDisposition is not null &&
        _itemPhaseIdentityIndexAfterImage is not null &&
        _finalIdentityIndexAfterImage is not null &&
        !string.IsNullOrEmpty(_itemPhaseFingerprint) &&
        !string.IsNullOrEmpty(_finalBaselineFingerprint);

    internal IReadOnlyList<MortalItemAcceptedTransfer> Transfers =>
        _transfers.Select(CloneTransfer).ToArray();

    internal MortalItemRouteAuthorityCatalog CloneRouteCatalog() =>
        MortalItemRouteAuthorityCatalog.CreateFrozen(_routesByCreationRef);

    internal IReadOnlyDictionary<string, JsonNode?> CloneCurrentProjectionRoots() =>
        CloneRoots(_currentRoots);

    internal IReadOnlyDictionary<string, JsonNode?> CloneBackupProjectionRoots() =>
        CloneRoots(_backupRoots);

    internal MortalTreatmentItemCommandEnvelope? CloneItemCommandEnvelope() =>
        _itemCommandEnvelope?.Clone();

    internal NpcCoreChangesContract.Authority? CloneNpcCoreAuthority() =>
        _npcCoreAuthority is null
            ? null
            : CloneNpcCoreAuthority(_npcCoreAuthority);

    internal CanonicalBeforeImage? CloneNpcTradePending() =>
        CloneBeforeImage(_npcTradePending);

    internal CanonicalBeforeImage? CloneTrainingPending() =>
        CloneBeforeImage(_trainingPending);

    internal MortalItemNpcTradeTailDisposition? NpcTradeDisposition =>
        _npcTradeDisposition;

    internal MortalItemCanonicalProjectionResult? CloneItemPhase() =>
        !HasFinalPublicationBaseline
            ? null
            : new MortalItemCanonicalProjectionResult(
                CloneRoots(_itemPhaseAfterImages),
                _itemPhaseIdentityIndexAfterImage!.DeepClone().AsObject(),
                Array.Empty<ValidationIssue>(),
                _itemPhaseFingerprint);

    internal MortalItemPublicationBaselineResult? CloneFinalBaseline() =>
        !HasFinalPublicationBaseline
            ? null
            : new MortalItemPublicationBaselineResult(
                CloneRoots(_finalCarrierRoots),
                _finalIdentityIndexAfterImage!.DeepClone().AsObject(),
                _appliedTransformIds.ToArray(),
                Array.Empty<ValidationIssue>(),
                _finalBaselineFingerprint);

    internal MortalItemAcceptedTurnNormalizationSnapshot Clone() =>
        CreateClone(includeFinalBaseline: HasFinalPublicationBaseline);

    internal bool MatchesTreatmentPublicationBaseline(
        MortalTreatmentItemCommandEnvelope itemCommandEnvelope,
        NpcCoreChangesContract.Authority npcCoreAuthority,
        CanonicalBeforeImage npcTradePending,
        CanonicalBeforeImage trainingPending,
        MortalItemNpcTradeTailDisposition npcTradeDisposition)
    {
        ArgumentNullException.ThrowIfNull(itemCommandEnvelope);
        ArgumentNullException.ThrowIfNull(npcCoreAuthority);
        ArgumentNullException.ThrowIfNull(npcTradePending);
        ArgumentNullException.ThrowIfNull(trainingPending);
        if (!HasFinalPublicationBaseline)
            return false;

        var recomposed = CreateClone(includeFinalBaseline: false)
            .CreateTreatmentPublicationBaseline(
                itemCommandEnvelope,
                npcCoreAuthority,
                npcTradePending,
                trainingPending,
                npcTradeDisposition);
        return recomposed.IsValid &&
               recomposed.Snapshot is not null &&
               MatchesFinalPublicationBaseline(recomposed.Snapshot);
    }

    internal MortalItemTreatmentPublicationBaselineSealResult
        CreateTreatmentPublicationBaseline(
            MortalTreatmentItemCommandEnvelope itemCommandEnvelope,
            NpcCoreChangesContract.Authority npcCoreAuthority,
            CanonicalBeforeImage npcTradePending,
            CanonicalBeforeImage trainingPending,
            MortalItemNpcTradeTailDisposition npcTradeDisposition)
    {
        ArgumentNullException.ThrowIfNull(itemCommandEnvelope);
        ArgumentNullException.ThrowIfNull(npcCoreAuthority);
        ArgumentNullException.ThrowIfNull(npcTradePending);
        ArgumentNullException.ThrowIfNull(trainingPending);
        if (HasFinalPublicationBaseline)
        {
            return InvalidBaselineSeal(
                "mortal_item_publication_baseline_already_sealed",
                "an unsealed accepted item projection snapshot",
                "a final baseline is already attached");
        }

        var identityState = MortalItemIdentityState.Parse(
            _currentRoots.GetValueOrDefault(MortalItemIdentityState.StatePath));
        if (identityState.Issues.Count != 0)
        {
            return new MortalItemTreatmentPublicationBaselineSealResult(
                null,
                identityState.Issues.ToArray());
        }
        var itemPhase = MortalItemCanonicalProjectionPlanner.Project(
            new MortalItemCanonicalProjectionInput(
                Turn,
                this,
                CloneRouteCatalog(),
                CloneCurrentProjectionRoots(),
                CloneBackupProjectionRoots(),
                identityState));
        if (!itemPhase.IsValid)
        {
            return new MortalItemTreatmentPublicationBaselineSealResult(
                null,
                itemPhase.Issues.ToArray());
        }
        var finalBaseline = MortalItemPublicationBaselinePlanner.Project(
            new MortalItemPublicationBaselineInput(
                itemPhase,
                itemCommandEnvelope.Clone(),
                CloneNpcCoreAuthority(npcCoreAuthority),
                CloneBeforeImage(npcTradePending)!,
                CloneBeforeImage(trainingPending)!,
                npcTradeDisposition,
                CloneBackupProjectionRoots()));
        if (finalBaseline.Issues.Count != 0)
        {
            return new MortalItemTreatmentPublicationBaselineSealResult(
                null,
                finalBaseline.Issues.ToArray());
        }

        var sealedSnapshot = new MortalItemAcceptedTurnNormalizationSnapshot(
            SessionId,
            SnapshotToken,
            Turn,
            _itemIdsByCreationRef,
            _routesByCreationRef,
            _transfers,
            _currentRoots,
            _backupRoots,
            _creationPathsByCreationRef,
            _creationOrdinalsByCreationRef,
            itemCommandEnvelope,
            npcCoreAuthority,
            npcTradePending,
            trainingPending,
            npcTradeDisposition,
            itemPhase,
            finalBaseline);
        if (!sealedSnapshot.RecomputesFinalPublicationBaseline())
        {
            return InvalidBaselineSeal(
                "mortal_item_publication_baseline_seal_mismatch",
                "one independently reproducible complete final baseline",
                "stored projection does not recompute");
        }
        return new MortalItemTreatmentPublicationBaselineSealResult(
            sealedSnapshot,
            Array.Empty<ValidationIssue>());
    }

    internal bool RecomputesFinalPublicationBaseline()
    {
        if (!HasFinalPublicationBaseline)
            return false;
        var baseSnapshot = CreateClone(includeFinalBaseline: false);
        var identityState = MortalItemIdentityState.Parse(
            baseSnapshot._currentRoots.GetValueOrDefault(
                MortalItemIdentityState.StatePath));
        if (identityState.Issues.Count != 0)
            return false;
        var itemPhase = MortalItemCanonicalProjectionPlanner.Project(
            new MortalItemCanonicalProjectionInput(
                Turn,
                baseSnapshot,
                baseSnapshot.CloneRouteCatalog(),
                baseSnapshot.CloneCurrentProjectionRoots(),
                baseSnapshot.CloneBackupProjectionRoots(),
                identityState));
        if (!ItemPhaseAgrees(itemPhase))
            return false;
        var finalBaseline = MortalItemPublicationBaselinePlanner.Project(
            new MortalItemPublicationBaselineInput(
                itemPhase,
                _itemCommandEnvelope!.Clone(),
                CloneNpcCoreAuthority(_npcCoreAuthority!),
                CloneBeforeImage(_npcTradePending)!,
                CloneBeforeImage(_trainingPending)!,
                _npcTradeDisposition!.Value,
                baseSnapshot.CloneBackupProjectionRoots()));
        return FinalBaselineAgrees(finalBaseline) &&
               string.Equals(
                   ProofFingerprint,
                   ComputeProofFingerprint(),
                   StringComparison.Ordinal);
    }

    internal bool MatchesFinalPublicationBaseline(
        MortalItemAcceptedTurnNormalizationSnapshot expected) =>
        expected is not null &&
        HasFinalPublicationBaseline &&
        expected.HasFinalPublicationBaseline &&
        string.Equals(
            ProjectionProofFingerprint,
            expected.ProjectionProofFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            ProofFingerprint,
            expected.ProofFingerprint,
            StringComparison.Ordinal) &&
        RecomputesFinalPublicationBaseline() &&
        expected.RecomputesFinalPublicationBaseline();

    internal bool TryGetAllocatedItemId(string creationRef, out string itemId) =>
        _itemIdsByCreationRef.TryGetValue(creationRef, out itemId!);

    internal bool TryGetRootReceiptId(string creationRef, out string receiptId) =>
        _receiptIdsByCreationRef.TryGetValue(creationRef, out receiptId!);

    internal bool TryGetCreateTransitionId(string creationRef, out string transitionId) =>
        _createTransitionIdsByCreationRef.TryGetValue(creationRef, out transitionId!);

    internal bool TryGetTransferTransitionId(string itemId, out string transitionId) =>
        _transferTransitionIdsByItemId.TryGetValue(itemId, out transitionId!);

    internal bool TryGetCreationPath(string creationRef, out string path) =>
        _creationPathsByCreationRef.TryGetValue(creationRef, out path!);

    internal bool MatchesRouteCatalog(MortalItemRouteAuthorityCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (_routesByCreationRef.Count != catalog.ByCreationRef.Count)
            return false;
        foreach (var pair in _routesByCreationRef)
        {
            if (!catalog.ByCreationRef.TryGetValue(pair.Key, out var actual) ||
                !string.Equals(
                    RouteFingerprint(pair.Value),
                    RouteFingerprint(actual),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    internal bool MatchesTransfers(
        IReadOnlyList<MortalItemAcceptedTransfer> transfers)
    {
        ArgumentNullException.ThrowIfNull(transfers);
        return _transfers.Length == transfers.Count &&
               _transfers.Select(TransferFingerprint).SequenceEqual(
                   transfers.Select(TransferFingerprint),
                   StringComparer.Ordinal);
    }

    internal bool MatchesProjectionRoots(
        IReadOnlyDictionary<string, JsonNode?> currentRoots,
        IReadOnlyDictionary<string, JsonNode?> backupRoots)
    {
        ArgumentNullException.ThrowIfNull(currentRoots);
        ArgumentNullException.ThrowIfNull(backupRoots);
        return RootsMatchFrozenSubset(currentRoots, _currentRoots) &&
               RootsMatchFrozenSubset(backupRoots, _backupRoots);
    }

    internal bool MatchesAcceptedOwnerAuthority(ResourceOwnerAuthority ownerAuthority)
    {
        ArgumentNullException.ThrowIfNull(ownerAuthority);
        var planned = ownerAuthority.Entries.Values
            .Where(static entry =>
                entry.Key.OwnerKind == ResourceOwnerKind.Item &&
                string.Equals(entry.Key.Realm, "mortal_world", StringComparison.Ordinal) &&
                entry.SameTurn &&
                entry.SameTurnRef != null)
            .ToArray();
        if (planned.Length != _itemIdsByCreationRef.Count)
            return false;

        foreach (var entry in planned)
        {
            if (!_itemIdsByCreationRef.TryGetValue(entry.SameTurnRef!, out var itemId) ||
                !string.Equals(
                    itemId,
                    entry.Key.ResourceOwnerId,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    internal bool MatchesExactCacheState(
        string? sessionId,
        string? snapshotToken,
        IReadOnlyDictionary<string, string> itemIdsByCreationRef,
        string proofFingerprint)
    {
        ArgumentNullException.ThrowIfNull(itemIdsByCreationRef);
        if (!string.Equals(SessionId, sessionId, StringComparison.Ordinal) ||
            !string.Equals(SnapshotToken, snapshotToken, StringComparison.Ordinal) ||
            !string.Equals(ProofFingerprint, proofFingerprint, StringComparison.Ordinal) ||
            _itemIdsByCreationRef.Count != itemIdsByCreationRef.Count)
        {
            return false;
        }

        foreach (var allocation in _itemIdsByCreationRef)
        {
            if (!itemIdsByCreationRef.TryGetValue(
                    allocation.Key,
                    out var itemId) ||
                !string.Equals(allocation.Value, itemId, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    internal string ComputePublicationFingerprint(string cacheFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheFingerprint);
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_item.accepted_turn_publication_take",
            "1",
            SessionId,
            SnapshotToken,
            Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cacheFingerprint,
            ProofFingerprint,
            _itemIdsByCreationRef.Count.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        };
        foreach (var allocation in _itemIdsByCreationRef
                     .OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            fields.Add(allocation.Key);
            fields.Add(allocation.Value);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private string DeterministicId(
        string prefix,
        string domain,
        string subject,
        string authorityFingerprint,
        int ordinal)
    {
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_item.accepted_turn_identity",
            "1",
            domain,
            SessionId,
            SnapshotToken,
            Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            subject,
            authorityFingerprint,
            ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        return prefix + fingerprint["sha256:".Length..];
    }

    private string ComputeProofFingerprint()
    {
        if (!HasFinalPublicationBaseline)
            return ProjectionProofFingerprint;

        var itemCommandEnvelope = _itemCommandEnvelope!;
        var npcCoreAuthority = _npcCoreAuthority!;
        var npcTradePending = _npcTradePending!;
        var trainingPending = _trainingPending!;
        var npcTradeDisposition = _npcTradeDisposition!.Value;
        var itemPhaseAfterImages = _itemPhaseAfterImages;
        var finalCarrierRoots = _finalCarrierRoots;
        var appliedTransformIds = _appliedTransformIds;
        var finalBaselineFingerprint = _finalBaselineFingerprint;
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_item.accepted_turn_complete_publication_proof",
            "1",
            ProjectionProofFingerprint,
            itemCommandEnvelope.Fingerprint,
            npcTradePending.Fingerprint,
            trainingPending.Fingerprint,
            npcTradeDisposition.ToString(),
            _itemPhaseFingerprint,
            finalBaselineFingerprint
        };
        AppendNpcCoreAuthority(fields, npcCoreAuthority);
        AppendRootFingerprints(fields, "item_phase", itemPhaseAfterImages);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            _itemPhaseIdentityIndexAfterImage));
        AppendRootFingerprints(fields, "final", finalCarrierRoots);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
            _finalIdentityIndexAfterImage));
        foreach (var appliedTransformId in appliedTransformIds)
            fields.Add(appliedTransformId);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private string ComputeProjectionProofFingerprint()
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_item.accepted_turn_projection_proof",
            "1",
            SessionId,
            SnapshotToken,
            Turn.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        foreach (var pair in _itemIdsByCreationRef.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add(pair.Key);
            fields.Add(pair.Value);
            fields.Add(_receiptIdsByCreationRef[pair.Key]);
            fields.Add(_createTransitionIdsByCreationRef[pair.Key]);
            fields.Add(_creationPathsByCreationRef.GetValueOrDefault(pair.Key));
            fields.Add(_creationOrdinalsByCreationRef.GetValueOrDefault(pair.Key)
                .ToString(System.Globalization.CultureInfo.InvariantCulture));
            fields.Add(_routesByCreationRef.TryGetValue(pair.Key, out var route)
                ? RouteFingerprint(route)
                : "missing");
        }
        foreach (var transfer in _transfers)
        {
            fields.Add(TransferFingerprint(transfer));
            fields.Add(_transferTransitionIdsByItemId[transfer.ItemId]);
        }
        AppendRootFingerprints(fields, "current", _currentRoots);
        AppendRootFingerprints(fields, "backup", _backupRoots);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal static bool RootsMatchFrozenSubset(
        IReadOnlyDictionary<string, JsonNode?> actual,
        IReadOnlyDictionary<string, JsonNode?> frozen)
    {
        if (actual.Count != frozen.Count)
            return false;
        foreach (var pair in actual)
        {
            if (!frozen.TryGetValue(pair.Key, out var expected) ||
                !JsonNode.DeepEquals(pair.Value, expected))
            {
                return false;
            }
        }
        return true;
    }

    private static void AppendRootFingerprints(
        ICollection<string?> fields,
        string stage,
        IReadOnlyDictionary<string, JsonNode?> roots)
    {
        foreach (var pair in roots.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            fields.Add(stage);
            fields.Add(pair.Key);
            fields.Add(pair.Value is null
                ? "missing"
                : WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
    }

    private MortalItemAcceptedTurnNormalizationSnapshot CreateClone(
        bool includeFinalBaseline)
    {
        var itemPhase = includeFinalBaseline ? CloneItemPhase() : null;
        var finalBaseline = includeFinalBaseline ? CloneFinalBaseline() : null;
        return new MortalItemAcceptedTurnNormalizationSnapshot(
            SessionId,
            SnapshotToken,
            Turn,
            _itemIdsByCreationRef,
            _routesByCreationRef,
            _transfers,
            _currentRoots,
            _backupRoots,
            _creationPathsByCreationRef,
            _creationOrdinalsByCreationRef,
            includeFinalBaseline ? _itemCommandEnvelope : null,
            includeFinalBaseline ? _npcCoreAuthority : null,
            includeFinalBaseline ? _npcTradePending : null,
            includeFinalBaseline ? _trainingPending : null,
            includeFinalBaseline ? _npcTradeDisposition : null,
            itemPhase,
            finalBaseline);
    }

    private bool ItemPhaseAgrees(MortalItemCanonicalProjectionResult itemPhase) =>
        itemPhase.IsValid &&
        string.Equals(
            itemPhase.Fingerprint,
            _itemPhaseFingerprint,
            StringComparison.Ordinal) &&
        RootsMatchFrozenSubset(
            itemPhase.ItemPhaseAfterImages,
            _itemPhaseAfterImages) &&
        JsonNode.DeepEquals(
            itemPhase.IdentityIndexAfterImage,
            _itemPhaseIdentityIndexAfterImage);

    private bool FinalBaselineAgrees(
        MortalItemPublicationBaselineResult finalBaseline) =>
        finalBaseline.Issues.Count == 0 &&
        string.Equals(
            finalBaseline.Fingerprint,
            _finalBaselineFingerprint,
            StringComparison.Ordinal) &&
        RootsMatchFrozenSubset(
            finalBaseline.FinalCarrierRoots,
            _finalCarrierRoots) &&
        JsonNode.DeepEquals(
            finalBaseline.IdentityIndexAfterImage,
            _finalIdentityIndexAfterImage) &&
        finalBaseline.AppliedTransformIds.SequenceEqual(
            _appliedTransformIds,
            StringComparer.Ordinal);

    private static MortalItemTreatmentPublicationBaselineSealResult
        InvalidBaselineSeal(string code, string expected, string actual) => new(
        null,
        new[]
        {
            new ValidationIssue(
                MortalItemIdentityState.StatePath,
                IssueSeverity.Error,
                "The accepted Mortal-item final publication baseline could not be sealed.",
                code: code,
                actor: "mortal_item:publication_baseline",
                section: "MortalItemMaterialization",
                expected: expected,
                actual: actual,
                repairHint:
                    "Reject publication and rebuild the accepted item projection from the validated turn snapshot.",
                repairTargetFiles: MortalItemCanonicalProjectionPlanner
                    .ProjectionRootPaths.ToArray())
        });

    private static CanonicalBeforeImage? CloneBeforeImage(
        CanonicalBeforeImage? image) => image is null
        ? null
        : new CanonicalBeforeImage(image.Existed, image.Bytes);

    private static NpcCoreChangesContract.Authority CloneNpcCoreAuthority(
        NpcCoreChangesContract.Authority authority) => new(
        new HashSet<string>(
            authority.KnownPermanentLocationIds,
            StringComparer.Ordinal),
        new HashSet<string>(
            authority.SameTurnLocationInitialIds,
            StringComparer.Ordinal),
        new Dictionary<string, string>(
            authority.FactionNamesById,
            StringComparer.Ordinal),
        new HashSet<string>(
            authority.WorldCharacteristicKeys,
            StringComparer.Ordinal));

    private static void AppendNpcCoreAuthority(
        ICollection<string?> fields,
        NpcCoreChangesContract.Authority authority)
    {
        fields.Add("npcCoreAuthority");
        foreach (var value in authority.KnownPermanentLocationIds.OrderBy(
                     static value => value,
                     StringComparer.Ordinal))
        {
            fields.Add("permanent_location");
            fields.Add(value);
        }
        foreach (var value in authority.SameTurnLocationInitialIds.OrderBy(
                     static value => value,
                     StringComparer.Ordinal))
        {
            fields.Add("same_turn_location");
            fields.Add(value);
        }
        foreach (var pair in authority.FactionNamesById.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add("faction");
            fields.Add(pair.Key);
            fields.Add(pair.Value);
        }
        foreach (var value in authority.WorldCharacteristicKeys.OrderBy(
                     static value => value,
                     StringComparer.Ordinal))
        {
            fields.Add("characteristic");
            fields.Add(value);
        }
    }

    internal static Dictionary<string, JsonNode?> CloneRoots(
        IReadOnlyDictionary<string, JsonNode?> roots) => roots.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value?.DeepClone(),
            StringComparer.Ordinal);

    internal static MortalItemRouteAuthority CloneRoute(MortalItemRouteAuthority value) =>
        value with
        {
            Destination = value.Destination with
            {
                ContainerPath = value.Destination.ContainerPath.ToArray()
            },
            SourceItemIds = value.SourceItemIds.ToArray()
        };

    internal static MortalItemAcceptedTransfer CloneTransfer(
        MortalItemAcceptedTransfer value) => value with
        {
            SourceCarrier = value.SourceCarrier with
            {
                ContainerPath = value.SourceCarrier.ContainerPath.ToArray()
            },
            DestinationCarrier = value.DestinationCarrier with
            {
                ContainerPath = value.DestinationCarrier.ContainerPath.ToArray()
            }
        };

    private static string RouteFingerprint(MortalItemRouteAuthority value) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_item.route_authority",
            "1",
            value.Route,
            value.AuthorityKind,
            value.AuthorityId,
            CarrierFingerprint(value.Destination),
            string.Join("\0", value.SourceItemIds)
        });

    private static string TransferFingerprint(MortalItemAcceptedTransfer value) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_item.accepted_transfer",
            "1",
            value.ItemId,
            CarrierFingerprint(value.SourceCarrier),
            CarrierFingerprint(value.DestinationCarrier),
            value.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value.AuthorityKind,
            value.AuthorityId,
            value.DestinationSurface.ToString(),
            value.DestinationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value.RemovalSurface.ToString(),
            value.RemovalIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

    private static string CarrierFingerprint(MortalItemCarrierCoordinate value) =>
        WoundAcceptedTurnFingerprintWriter.CanonicalJson(new JsonObject
        {
            ["kind"] = value.Kind,
            ["ownerId"] = value.OwnerId,
            ["containerId"] = value.ContainerId,
            ["containerPath"] = new JsonArray(
                value.ContainerPath.Select(static item => (JsonNode?)item).ToArray())
        })!;
}

internal static class MortalItemAcceptedTurnAuthority
{
    internal static bool HasValidatedItems(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        return AcceptedTurnAuthorityRegistry.HasMortalItemsValidated(
            fs,
            writeLease);
    }

    internal static void RegisterValidatedItems(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken,
        MortalItemCarrierCatalog catalog,
        IEnumerable<string> knownItemIds,
        MortalItemRouteAuthorityCatalog routeAuthorities,
        MortalItemAcceptedTransferCatalog? transferCatalog,
        IReadOnlyDictionary<string, JsonNode?> currentProjectionRoots,
        IReadOnlyDictionary<string, JsonNode?> backupProjectionRoots)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(knownItemIds);
        ArgumentNullException.ThrowIfNull(routeAuthorities);
        ArgumentNullException.ThrowIfNull(currentProjectionRoots);
        ArgumentNullException.ThrowIfNull(backupProjectionRoots);

        var newCandidates = catalog.Occurrences
            .Where(static occurrence =>
                occurrence.ItemId == null &&
                occurrence.CreationRef != null)
            .Select(occurrence => new NewCandidate(
                occurrence.CreationRef!,
                occurrence.Item.DeepClone().AsObject(),
                occurrence.FilePath,
                occurrence.JsonPath,
                CloneCarrier(occurrence.Carrier),
                IsEligiblePlayerEffectSource(occurrence),
                IsEquippedPlayerReference(catalog, occurrence.CreationRef!)))
            .ToArray();
        var stableCandidates = catalog.Occurrences
            .Where(static occurrence => occurrence.ItemId != null)
            .OrderBy(static occurrence => occurrence.ItemId, StringComparer.Ordinal)
            .Select(occurrence => new StableCandidate(
                occurrence.ItemId!,
                occurrence.Item.DeepClone().AsObject(),
                occurrence.FilePath,
                occurrence.JsonPath,
                CloneCarrier(occurrence.Carrier),
                IsEligiblePlayerEffectSource(occurrence),
                IsEquippedPlayerReference(catalog, occurrence.ItemId!)))
            .ToArray();
        var governedItemIds = knownItemIds
            .OrderBy(static itemId => itemId, StringComparer.Ordinal)
            .ToArray();
        var fingerprint = CreateFingerprint(
            newCandidates,
            stableCandidates,
            governedItemIds);
        AcceptedTurnAuthorityRegistry.RegisterMortalItemsValidated(
            fs,
            writeLease,
            sessionId,
            snapshotToken,
            fingerprint,
            newCandidates,
            stableCandidates,
            governedItemIds,
            routeAuthorities.ByCreationRef,
            transferCatalog?.Transfers,
            currentProjectionRoots,
            backupProjectionRoots);
    }

    internal static IReadOnlyList<ValidationIssue>
        RegisterValidatedTreatmentItems(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease writeLease,
            string sessionId,
            string snapshotToken,
            MortalItemCarrierCatalog catalog,
            IEnumerable<string> knownItemIds,
            IReadOnlyDictionary<string, JsonNode?> currentProjectionRoots,
            object treatmentContinuationAuthority,
            object reservationAuthority)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(knownItemIds);
        ArgumentNullException.ThrowIfNull(currentProjectionRoots);
        ArgumentNullException.ThrowIfNull(treatmentContinuationAuthority);
        ArgumentNullException.ThrowIfNull(reservationAuthority);

        var newCandidates = catalog.Occurrences
            .Where(static occurrence =>
                occurrence.ItemId == null &&
                occurrence.CreationRef != null)
            .Select(occurrence => new NewCandidate(
                occurrence.CreationRef!,
                occurrence.Item.DeepClone().AsObject(),
                occurrence.FilePath,
                occurrence.JsonPath,
                CloneCarrier(occurrence.Carrier),
                IsEligiblePlayerEffectSource(occurrence),
                IsEquippedPlayerReference(catalog, occurrence.CreationRef!)))
            .ToArray();
        var stableCandidates = catalog.Occurrences
            .Where(static occurrence => occurrence.ItemId != null)
            .OrderBy(static occurrence => occurrence.ItemId, StringComparer.Ordinal)
            .Select(occurrence => new StableCandidate(
                occurrence.ItemId!,
                occurrence.Item.DeepClone().AsObject(),
                occurrence.FilePath,
                occurrence.JsonPath,
                CloneCarrier(occurrence.Carrier),
                IsEligiblePlayerEffectSource(occurrence),
                IsEquippedPlayerReference(catalog, occurrence.ItemId!)))
            .ToArray();
        var governedItemIds = knownItemIds
            .OrderBy(static itemId => itemId, StringComparer.Ordinal)
            .ToArray();
        var fingerprint = CreateFingerprint(
            newCandidates,
            stableCandidates,
            governedItemIds);
        return AcceptedTurnAuthorityRegistry
            .RegisterMortalTreatmentItemsValidated(
                fs,
                writeLease,
                sessionId,
                snapshotToken,
                fingerprint,
                newCandidates,
                stableCandidates,
                governedItemIds,
                currentProjectionRoots,
                treatmentContinuationAuthority,
                reservationAuthority);
    }

    internal static IReadOnlyList<ValidationIssue>
        ConfirmValidatedTreatmentItems(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease writeLease,
            string sessionId,
            string snapshotToken,
            IReadOnlyDictionary<string, JsonNode?> currentProjectionRoots,
            object treatmentContinuationAuthority,
            object reservationAuthority)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        ArgumentNullException.ThrowIfNull(currentProjectionRoots);
        ArgumentNullException.ThrowIfNull(treatmentContinuationAuthority);
        ArgumentNullException.ThrowIfNull(reservationAuthority);
        return AcceptedTurnAuthorityRegistry.ConfirmMortalTreatmentItemsValidated(
            fs,
            writeLease,
            sessionId,
            snapshotToken,
            currentProjectionRoots,
            treatmentContinuationAuthority,
            reservationAuthority);
    }

    internal static IReadOnlyList<ValidationIssue>
        SealValidatedTreatmentPublicationBaseline(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease writeLease,
            string sessionId,
            string snapshotToken,
            int turn,
            NpcCoreChangesContract.Authority npcCoreAuthority,
            CanonicalBeforeImage npcTradePending,
            CanonicalBeforeImage trainingPending,
            object treatmentContinuationAuthority,
            object reservationAuthority)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        ArgumentNullException.ThrowIfNull(npcCoreAuthority);
        ArgumentNullException.ThrowIfNull(npcTradePending);
        ArgumentNullException.ThrowIfNull(trainingPending);
        ArgumentNullException.ThrowIfNull(treatmentContinuationAuthority);
        ArgumentNullException.ThrowIfNull(reservationAuthority);
        return AcceptedTurnAuthorityRegistry
            .SealMortalTreatmentItemPublicationBaseline(
                fs,
                writeLease,
                sessionId,
                snapshotToken,
                turn,
                npcCoreAuthority,
                npcTradePending,
                trainingPending,
                treatmentContinuationAuthority,
                reservationAuthority);
    }

    internal static IReadOnlyList<EffectSourceExport> GetValidatedEffectSources(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        AcceptedTurnAuthorityRegistry.GetMortalItemEffectSources(
            fs,
            writeLease,
            sessionId,
            snapshotToken);

    internal static void InvalidateValidatedItems(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        AcceptedTurnAuthorityRegistry.InvalidateMortalItemsValidated(
            fs,
            writeLease);
    }

    internal static IReadOnlySet<EffectSourceOwnerKey> GetReplacedEffectSourceOwners(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        AcceptedTurnAuthorityRegistry.GetMortalItemReplacedSourceOwners(
            fs,
            writeLease,
            sessionId,
            snapshotToken);

    internal static bool TryGetAllocatedItemId(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken,
        string creationRef,
        out string itemId)
    {
        itemId = string.Empty;
        return AcceptedTurnAuthorityRegistry.TryGetMortalItemId(
            fs,
            writeLease,
            sessionId,
            snapshotToken,
            creationRef,
            out itemId);
    }

    internal static bool TryCaptureNormalizationSnapshot(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken,
        int turn,
        out MortalItemAcceptedTurnNormalizationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        snapshot = null!;
        return AcceptedTurnAuthorityRegistry.TryCaptureMortalItemNormalizationSnapshot(
            fs,
            writeLease,
            sessionId,
            snapshotToken,
            turn,
            out snapshot);
    }

    internal static IReadOnlyList<MortalItemAcceptedTurnOwner> GetValidatedOwners(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        AcceptedTurnAuthorityRegistry.GetMortalItemOwners(
            fs,
            writeLease,
            sessionId,
            snapshotToken);

    internal static IReadOnlySet<string> GetMissingGovernedItemIds(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        AcceptedTurnAuthorityRegistry.GetMissingGovernedMortalItemIds(
            fs,
            writeLease,
            sessionId,
            snapshotToken);

    private static string CreateFingerprint(
        IEnumerable<NewCandidate> newCandidates,
        IEnumerable<StableCandidate> stableCandidates,
        IEnumerable<string> governedItemIds)
    {
        var root = new JsonObject
        {
            ["new"] = new JsonArray(newCandidates.Select(candidate => (JsonNode)new JsonObject
            {
                ["creationRef"] = candidate.CreationRef,
                ["item"] = candidate.Item.DeepClone(),
                ["filePath"] = candidate.FilePath,
                ["jsonPath"] = candidate.JsonPath,
                ["carrier"] = CarrierNode(candidate.Carrier),
                ["effectEligible"] = candidate.EffectEligible,
                ["equipped"] = candidate.Equipped
            }).ToArray()),
            ["stable"] = new JsonArray(stableCandidates.Select(candidate => (JsonNode)new JsonObject
            {
                ["itemId"] = candidate.ItemId,
                ["item"] = candidate.Item.DeepClone(),
                ["filePath"] = candidate.FilePath,
                ["jsonPath"] = candidate.JsonPath,
                ["carrier"] = CarrierNode(candidate.Carrier),
                ["effectEligible"] = candidate.EffectEligible,
                ["equipped"] = candidate.Equipped
            }).ToArray()),
            ["governedItemIds"] = new JsonArray(
                governedItemIds.Select(static itemId => (JsonNode)itemId).ToArray())
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    private static bool IsEquippedPlayerReference(
        MortalItemCarrierCatalog catalog,
        string reference) =>
        catalog.ByCompanionReference.TryGetValue(reference, out var references) &&
        references.Any(candidate =>
            string.Equals(
                candidate.FilePath,
                InventoryEquipmentService.ItemsPath,
                StringComparison.Ordinal) &&
            candidate.JsonPath.StartsWith(
                InventoryEquipmentService.ItemsPath + ".equippedItems.",
                StringComparison.Ordinal) &&
            candidate.ExpectedCarrier is
            {
                Kind: "player_inventory",
                OwnerId: "player"
            });

    private static bool IsEligiblePlayerEffectSource(
        MortalItemCarrierOccurrence occurrence) =>
        string.Equals(
            occurrence.Carrier.Kind,
            "player_inventory",
            StringComparison.Ordinal) &&
        MortalItemLocalActionPolicy.IsCarriedByPlayer(occurrence.Item);

    private static MortalItemCarrierCoordinate CloneCarrier(
        MortalItemCarrierCoordinate carrier) =>
        carrier with { ContainerPath = carrier.ContainerPath.ToArray() };

    private static JsonObject CarrierNode(MortalItemCarrierCoordinate carrier) =>
        new()
        {
            ["kind"] = carrier.Kind,
            ["ownerId"] = carrier.OwnerId,
            ["containerId"] = carrier.ContainerId,
            ["containerPath"] = new JsonArray(
                carrier.ContainerPath.Select(static value => (JsonNode)value).ToArray())
        };

    private static IReadOnlySet<string> ItemPredicates(bool carried, bool equipped)
    {
        var predicates = new HashSet<string>(StringComparer.Ordinal);
        if (carried)
            predicates.Add("carried");
        if (equipped)
            predicates.Add("equipped");
        return predicates;
    }

    internal sealed class Cache
    {
        internal sealed class ValidatedPublicationTakeSnapshot
        {
            internal ValidatedPublicationTakeSnapshot(
                object cacheAuthority,
                object fence,
                string cacheFingerprint,
                MortalItemAcceptedTurnNormalizationSnapshot normalizationSnapshot,
                bool terminalReleaseOnly)
            {
                CacheAuthority = cacheAuthority;
                Fence = fence;
                CacheFingerprint = cacheFingerprint;
                NormalizationSnapshot = normalizationSnapshot;
                TerminalReleaseOnly = terminalReleaseOnly;
                PublicationFingerprint = ComputePublicationTakeFingerprint(
                    normalizationSnapshot,
                    cacheFingerprint,
                    terminalReleaseOnly);
            }

            internal object CacheAuthority { get; }
            internal object Fence { get; }
            internal string CacheFingerprint { get; }
            internal MortalItemAcceptedTurnNormalizationSnapshot
                NormalizationSnapshot { get; }
            internal bool TerminalReleaseOnly { get; }
            internal string PublicationFingerprint { get; }

            internal static string ComputePublicationTakeFingerprint(
                MortalItemAcceptedTurnNormalizationSnapshot normalizationSnapshot,
                string cacheFingerprint,
                bool terminalReleaseOnly) =>
                WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                {
                    "book_of_eternity.mortal_item.accepted_turn_publication_take_receipt",
                    "1",
                    normalizationSnapshot.ComputePublicationFingerprint(cacheFingerprint),
                    terminalReleaseOnly
                        ? "terminal_release_only"
                        : "normal_publication"
                });
        }

        private readonly object _gate = new();
        private readonly object _cacheAuthority = new();
        private object _validatedFence = new();
        private string? _sessionId;
        private string? _snapshotToken;
        private string? _fingerprint;
        private bool _validated;
        private Dictionary<string, string> _itemIdsByCreationRef = new(StringComparer.Ordinal);
        private EffectSourceExport[] _sources = Array.Empty<EffectSourceExport>();
        private HashSet<EffectSourceOwnerKey> _replacedSourceOwners = new();
        private MortalItemAcceptedTurnOwner[] _owners = Array.Empty<MortalItemAcceptedTurnOwner>();
        private HashSet<string> _missingGovernedItemIds = new(StringComparer.Ordinal);
        private IReadOnlyDictionary<string, MortalItemRouteAuthority> _routesByCreationRef =
            new Dictionary<string, MortalItemRouteAuthority>(StringComparer.Ordinal);
        private IReadOnlyList<MortalItemAcceptedTransfer> _transfers =
            Array.Empty<MortalItemAcceptedTransfer>();
        private IReadOnlyDictionary<string, JsonNode?> _currentProjectionRoots =
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        private IReadOnlyDictionary<string, JsonNode?> _backupProjectionRoots =
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        private Dictionary<string, string> _creationPathsByCreationRef =
            new(StringComparer.Ordinal);
        private Dictionary<string, int> _creationOrdinalsByCreationRef =
            new(StringComparer.Ordinal);
        private string _normalizationProofFingerprint = string.Empty;
        private MortalItemAcceptedTurnNormalizationSnapshot?
            _treatmentPublicationBaselineSnapshot;

        internal bool HasValidated
        {
            get
            {
                lock (_gate)
                    return _validated;
            }
        }

        internal void Register(
            string sessionId,
            string snapshotToken,
            string fingerprint,
            IReadOnlyList<NewCandidate> newCandidates,
            IReadOnlyList<StableCandidate> stableCandidates,
            IReadOnlyList<string> governedItemIds,
            IReadOnlyDictionary<string, MortalItemRouteAuthority> routesByCreationRef,
            IReadOnlyList<MortalItemAcceptedTransfer>? transfers,
            IReadOnlyDictionary<string, JsonNode?> currentProjectionRoots,
            IReadOnlyDictionary<string, JsonNode?> backupProjectionRoots)
        {
            ArgumentNullException.ThrowIfNull(routesByCreationRef);
            ArgumentNullException.ThrowIfNull(currentProjectionRoots);
            ArgumentNullException.ThrowIfNull(backupProjectionRoots);
            routesByCreationRef = routesByCreationRef.ToDictionary(
                static pair => pair.Key,
                static pair => MortalItemAcceptedTurnNormalizationSnapshot.CloneRoute(
                    pair.Value),
                StringComparer.Ordinal);
            transfers = transfers?.Select(
                MortalItemAcceptedTurnNormalizationSnapshot.CloneTransfer).ToArray() ??
                Array.Empty<MortalItemAcceptedTransfer>();
            currentProjectionRoots =
                MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
                    currentProjectionRoots);
            backupProjectionRoots =
                MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
                    backupProjectionRoots);
            lock (_gate)
            {
                _validatedFence = new object();
                if (string.Equals(_sessionId, sessionId, StringComparison.Ordinal) &&
                    string.Equals(_snapshotToken, snapshotToken, StringComparison.Ordinal) &&
                    string.Equals(_fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    _routesByCreationRef = routesByCreationRef;
                    _transfers = transfers;
                    _currentProjectionRoots = currentProjectionRoots;
                    _backupProjectionRoots = backupProjectionRoots;
                    _creationPathsByCreationRef = newCandidates.ToDictionary(
                        static candidate => candidate.CreationRef,
                        static candidate => candidate.FilePath,
                        StringComparer.Ordinal);
                    _creationOrdinalsByCreationRef = newCandidates
                        .Select(static (candidate, index) =>
                            new KeyValuePair<string, int>(
                                candidate.CreationRef,
                                index + 1))
                        .ToDictionary(
                            static pair => pair.Key,
                            static pair => pair.Value,
                            StringComparer.Ordinal);
                    _normalizationProofFingerprint = string.Empty;
                    _treatmentPublicationBaselineSnapshot = null;
                    _validated = true;
                    return;
                }

                var known = governedItemIds.ToHashSet(StringComparer.Ordinal);
                var allocations = new Dictionary<string, string>(StringComparer.Ordinal);
                var exports = new List<EffectSourceExport>();
                var owners = new List<MortalItemAcceptedTurnOwner>();
                foreach (var candidate in stableCandidates)
                {
                    owners.Add(new MortalItemAcceptedTurnOwner(
                        candidate.ItemId,
                        ItemRef: null,
                        candidate.FilePath,
                        candidate.JsonPath,
                        CloneCarrier(candidate.Carrier),
                        candidate.Item.DeepClone().AsObject(),
                        SameTurn: false));
                    if (!candidate.EffectEligible)
                        continue;
                    exports.Add(new EffectSourceExport(
                        "mortal_world",
                        "item",
                        candidate.ItemId,
                        candidate.Item["activeEffectDefinitions"]?.DeepClone().AsArray() ??
                            new JsonArray(),
                        Materializable: true,
                        Active: true,
                        SameTurn: true,
                        SatisfiedPredicates: ItemPredicates(
                            carried: true,
                            candidate.Equipped)));
                }
                foreach (var candidate in newCandidates)
                {
                    string itemId;
                    do
                    {
                        itemId = "itm_" + Guid.NewGuid().ToString("N");
                    }
                    while (!known.Add(itemId));

                    allocations.Add(candidate.CreationRef, itemId);
                    owners.Add(new MortalItemAcceptedTurnOwner(
                        itemId,
                        candidate.CreationRef,
                        candidate.FilePath,
                        candidate.JsonPath,
                        CloneCarrier(candidate.Carrier),
                        candidate.Item.DeepClone().AsObject(),
                        SameTurn: true));
                    if (!candidate.EffectEligible)
                        continue;
                    exports.Add(new EffectSourceExport(
                        "mortal_world",
                        "item",
                        itemId,
                        candidate.Item["activeEffectDefinitions"]?.DeepClone().AsArray() ??
                            new JsonArray(),
                        Materializable: true,
                        Active: true,
                        SameTurn: true,
                        SourceRef: candidate.CreationRef,
                        SatisfiedPredicates: ItemPredicates(
                            carried: true,
                            equipped: candidate.Equipped)));
                }

                _sessionId = sessionId;
                _snapshotToken = snapshotToken;
                _fingerprint = fingerprint;
                _validated = true;
                _routesByCreationRef = routesByCreationRef;
                _transfers = transfers;
                _currentProjectionRoots = currentProjectionRoots;
                _backupProjectionRoots = backupProjectionRoots;
                _creationPathsByCreationRef = newCandidates.ToDictionary(
                    static candidate => candidate.CreationRef,
                    static candidate => candidate.FilePath,
                    StringComparer.Ordinal);
                _creationOrdinalsByCreationRef = newCandidates
                    .Select(static (candidate, index) =>
                        new KeyValuePair<string, int>(
                            candidate.CreationRef,
                            index + 1))
                    .ToDictionary(
                        static pair => pair.Key,
                        static pair => pair.Value,
                        StringComparer.Ordinal);
                _normalizationProofFingerprint = string.Empty;
                _treatmentPublicationBaselineSnapshot = null;
                _itemIdsByCreationRef = allocations;
                _sources = exports.ToArray();
                _owners = owners.ToArray();
                _missingGovernedItemIds = governedItemIds
                    .Except(stableCandidates.Select(static candidate => candidate.ItemId), StringComparer.Ordinal)
                    .ToHashSet(StringComparer.Ordinal);
                _replacedSourceOwners = governedItemIds
                    .Select(static itemId => new EffectSourceOwnerKey(
                        "mortal_world",
                        "item",
                        itemId))
                    .ToHashSet();
            }
        }

        internal void InvalidateValidated()
        {
            lock (_gate)
            {
                _validatedFence = new object();
                _validated = false;
            }
        }

        internal bool ConfirmsTreatmentContinuation(
            string sessionId,
            string snapshotToken,
            IReadOnlyDictionary<string, JsonNode?> currentProjectionRoots)
        {
            ArgumentNullException.ThrowIfNull(currentProjectionRoots);
            lock (_gate)
            {
                return _validated &&
                       string.Equals(_sessionId, sessionId, StringComparison.Ordinal) &&
                       string.Equals(_snapshotToken, snapshotToken, StringComparison.Ordinal) &&
                       _currentProjectionRoots.Count ==
                       MortalItemCanonicalProjectionPlanner.ProjectionRootPaths.Count &&
                       _currentProjectionRoots.Keys.ToHashSet(StringComparer.Ordinal)
                           .SetEquals(
                               MortalItemCanonicalProjectionPlanner
                                   .ProjectionRootPaths) &&
                       _backupProjectionRoots.Count ==
                       MortalItemCanonicalProjectionPlanner.ProjectionRootPaths.Count &&
                       _backupProjectionRoots.Keys.ToHashSet(StringComparer.Ordinal)
                           .SetEquals(
                               MortalItemCanonicalProjectionPlanner
                                   .ProjectionRootPaths) &&
                       MortalItemAcceptedTurnNormalizationSnapshot
                           .RootsMatchFrozenSubset(
                               currentProjectionRoots,
                               _currentProjectionRoots);
            }
        }

        internal bool TrySealTreatmentPublicationBaseline(
            MortalItemAcceptedTurnNormalizationSnapshot baseSnapshot,
            MortalItemAcceptedTurnNormalizationSnapshot sealedSnapshot)
        {
            ArgumentNullException.ThrowIfNull(baseSnapshot);
            ArgumentNullException.ThrowIfNull(sealedSnapshot);
            lock (_gate)
            {
                if (!_validated ||
                    _treatmentPublicationBaselineSnapshot is not null ||
                    baseSnapshot.HasFinalPublicationBaseline ||
                    !sealedSnapshot.HasFinalPublicationBaseline ||
                    !baseSnapshot.MatchesExactCacheState(
                        _sessionId,
                        _snapshotToken,
                        _itemIdsByCreationRef,
                        _normalizationProofFingerprint) ||
                    !string.Equals(
                        baseSnapshot.ProjectionProofFingerprint,
                        sealedSnapshot.ProjectionProofFingerprint,
                        StringComparison.Ordinal) ||
                    !sealedSnapshot.RecomputesFinalPublicationBaseline())
                {
                    return false;
                }

                _treatmentPublicationBaselineSnapshot = sealedSnapshot.Clone();
                _normalizationProofFingerprint = string.Empty;
                _validatedFence = new object();
                return true;
            }
        }

        internal bool TryTakeValidatedTreatmentPublication(
            MortalItemAcceptedTurnNormalizationSnapshot expected,
            out ValidatedPublicationTakeSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(expected);
            lock (_gate)
            {
                if (_validated &&
                    _fingerprint is not null &&
                    _treatmentPublicationBaselineSnapshot is not null &&
                    expected.MatchesFinalPublicationBaseline(
                        _treatmentPublicationBaselineSnapshot) &&
                    expected.MatchesExactCacheState(
                        _sessionId,
                        _snapshotToken,
                        _itemIdsByCreationRef,
                        _normalizationProofFingerprint))
                {
                    snapshot = new ValidatedPublicationTakeSnapshot(
                        _cacheAuthority,
                        _validatedFence,
                        _fingerprint,
                        expected,
                        terminalReleaseOnly: false);
                    _validated = false;
                    return true;
                }

                snapshot = null!;
                return false;
            }
        }

        internal bool TryTakeInvalidatedTreatmentPublicationForTerminalRelease(
            out ValidatedPublicationTakeSnapshot snapshot)
        {
            lock (_gate)
            {
                var retainedBaseline = _treatmentPublicationBaselineSnapshot?.Clone();
                if (_validated ||
                    _fingerprint is null ||
                    retainedBaseline is null ||
                    !retainedBaseline.RecomputesFinalPublicationBaseline() ||
                    !retainedBaseline.MatchesExactCacheState(
                        _sessionId,
                        _snapshotToken,
                        _itemIdsByCreationRef,
                        _normalizationProofFingerprint))
                {
                    snapshot = null!;
                    return false;
                }

                snapshot = new ValidatedPublicationTakeSnapshot(
                    _cacheAuthority,
                    _validatedFence,
                    _fingerprint,
                    retainedBaseline,
                    terminalReleaseOnly: true);
                return true;
            }
        }

        internal bool IsTreatmentPublicationTakeCurrent(
            ValidatedPublicationTakeSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            lock (_gate)
            {
                return PublicationTakeSnapshotAgrees(snapshot) &&
                       ReferenceEquals(_validatedFence, snapshot.Fence) &&
                       !_validated &&
                       CurrentCacheStateAgrees(snapshot);
            }
        }

        internal bool TryRearmValidatedTreatmentPublication(
            ValidatedPublicationTakeSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            lock (_gate)
            {
                if (!PublicationTakeSnapshotAgrees(snapshot) ||
                    !ReferenceEquals(_validatedFence, snapshot.Fence) ||
                    snapshot.TerminalReleaseOnly ||
                    _validated ||
                    !CurrentCacheStateAgrees(snapshot))
                {
                    return false;
                }

                _validated = true;
                return true;
            }
        }

        internal bool IsTreatmentPublicationRearmed(
            ValidatedPublicationTakeSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            lock (_gate)
            {
                return PublicationTakeSnapshotAgrees(snapshot) &&
                       ReferenceEquals(_validatedFence, snapshot.Fence) &&
                       !snapshot.TerminalReleaseOnly &&
                       _validated &&
                       CurrentCacheStateAgrees(snapshot);
            }
        }

        internal IReadOnlyList<EffectSourceExport> GetSources(
            string sessionId,
            string snapshotToken)
        {
            lock (_gate)
            {
                if (!Matches(sessionId, snapshotToken))
                    return Array.Empty<EffectSourceExport>();
                return _sources.Select(static source => source with
                {
                    Definitions = source.Definitions.DeepClone().AsArray(),
                    SatisfiedPredicates = source.SatisfiedPredicates == null
                        ? null
                        : new HashSet<string>(
                            source.SatisfiedPredicates,
                            StringComparer.Ordinal)
                }).ToArray();
            }
        }

        internal bool TryGetItemId(
            string sessionId,
            string snapshotToken,
            string creationRef,
            out string itemId)
        {
            lock (_gate)
            {
                if (Matches(sessionId, snapshotToken) &&
                    _itemIdsByCreationRef.TryGetValue(creationRef, out var value))
                {
                    itemId = value;
                    return true;
                }
                itemId = string.Empty;
                return false;
            }
        }

        internal bool TryCaptureNormalizationSnapshot(
            string sessionId,
            string snapshotToken,
            int turn,
            out MortalItemAcceptedTurnNormalizationSnapshot snapshot)
        {
            lock (_gate)
            {
                if (Matches(sessionId, snapshotToken))
                {
                    if (_treatmentPublicationBaselineSnapshot is not null)
                    {
                        if (_treatmentPublicationBaselineSnapshot.Turn != turn)
                        {
                            snapshot = null!;
                            return false;
                        }
                        snapshot = _treatmentPublicationBaselineSnapshot.Clone();
                    }
                    else
                    {
                        snapshot = new MortalItemAcceptedTurnNormalizationSnapshot(
                            sessionId,
                            snapshotToken,
                            turn,
                            _itemIdsByCreationRef,
                            _routesByCreationRef,
                            _transfers,
                            _currentProjectionRoots,
                            _backupProjectionRoots,
                            _creationPathsByCreationRef,
                            _creationOrdinalsByCreationRef);
                    }
                    _normalizationProofFingerprint = snapshot.ProofFingerprint;
                    return true;
                }

                snapshot = null!;
                return false;
            }
        }

        internal IReadOnlySet<EffectSourceOwnerKey> GetReplacedSourceOwners(
            string sessionId,
            string snapshotToken)
        {
            lock (_gate)
            {
                return Matches(sessionId, snapshotToken)
                    ? new HashSet<EffectSourceOwnerKey>(_replacedSourceOwners)
                    : new HashSet<EffectSourceOwnerKey>();
            }
        }

        internal IReadOnlyList<MortalItemAcceptedTurnOwner> GetOwners(
            string sessionId,
            string snapshotToken)
        {
            lock (_gate)
            {
                if (!Matches(sessionId, snapshotToken))
                    return Array.Empty<MortalItemAcceptedTurnOwner>();
                return _owners.Select(static owner => owner with
                {
                    Carrier = CloneCarrier(owner.Carrier),
                    Item = owner.Item.DeepClone().AsObject()
                }).ToArray();
            }
        }

        internal IReadOnlySet<string> GetMissingGovernedItemIds(
            string sessionId,
            string snapshotToken)
        {
            lock (_gate)
            {
                return Matches(sessionId, snapshotToken)
                    ? new HashSet<string>(_missingGovernedItemIds, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
            }
        }

        private bool Matches(string sessionId, string snapshotToken) =>
            _validated &&
            string.Equals(_sessionId, sessionId, StringComparison.Ordinal) &&
            string.Equals(_snapshotToken, snapshotToken, StringComparison.Ordinal);

        private bool PublicationTakeSnapshotAgrees(
            ValidatedPublicationTakeSnapshot snapshot) =>
            ReferenceEquals(snapshot.CacheAuthority, _cacheAuthority) &&
            string.Equals(
                snapshot.PublicationFingerprint,
                ValidatedPublicationTakeSnapshot.ComputePublicationTakeFingerprint(
                    snapshot.NormalizationSnapshot,
                    snapshot.CacheFingerprint,
                    snapshot.TerminalReleaseOnly),
                StringComparison.Ordinal);

        private bool CurrentCacheStateAgrees(
            ValidatedPublicationTakeSnapshot snapshot)
        {
            return string.Equals(
                _fingerprint,
                snapshot.CacheFingerprint,
                StringComparison.Ordinal) &&
                   _treatmentPublicationBaselineSnapshot is not null &&
                   snapshot.NormalizationSnapshot.MatchesFinalPublicationBaseline(
                       _treatmentPublicationBaselineSnapshot) &&
                   snapshot.NormalizationSnapshot.MatchesExactCacheState(
                       _sessionId,
                       _snapshotToken,
                       _itemIdsByCreationRef,
                       _normalizationProofFingerprint);
        }
    }

    internal sealed record NewCandidate(
        string CreationRef,
        JsonObject Item,
        string FilePath,
        string JsonPath,
        MortalItemCarrierCoordinate Carrier,
        bool EffectEligible,
        bool Equipped);

    internal sealed record StableCandidate(
        string ItemId,
        JsonObject Item,
        string FilePath,
        string JsonPath,
        MortalItemCarrierCoordinate Carrier,
        bool EffectEligible,
        bool Equipped);
}
