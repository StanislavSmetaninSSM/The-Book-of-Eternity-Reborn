using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class MortalQteDecisionPublication
{
    private const string JointHashDomain = "T070-MORTAL-QTE-JOINT-COMMON-PUBLICATION-V2\n";
    private static readonly string[] JointFields = Fields.Concat(new[] { "jointTreatment", "consumedPrivatePaths" }).ToArray();
    private static readonly string[] JointTreatmentFields = ["operationKey", "attemptId", "requestFingerprint",
        "persistedResultFingerprint", "resourceFinalizationFingerprint", "repairCheckpointFingerprint", "treatmentHistoryOrdinal"];
    private static readonly string[] JointFootprintPaths = FootprintPaths
        .Concat(MortalItemCanonicalProjectionPlanner.ProjectionRootPaths)
        .Concat(CanonicalResourceOwnerAuthorityComposer.SourceAuthorityPaths)
        .Append(WoundAcceptedTurnSnapshotContract.PendingResolutionPath)
        .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
    private static readonly object JointHistorySuffixIssuer = new();

    // A non-authorizing read capability for exactly the validated typed TREAT row.
    // No ordinary adapter caller or parsed marker string can issue it.
    internal sealed class JointHistorySuffixProof
    {
        private readonly WoundHistoryTransition _row;
        internal JointHistorySuffixProof(object issuer, WoundHistoryTransition row)
        {
            if (!ReferenceEquals(issuer, JointHistorySuffixIssuer)) throw Invalid("joint_history_issuer");
            _row = row;
        }
        internal bool Matches(WoundHistoryTransition row) =>
            (row with { TransitionResult = null }) == (_row with { TransitionResult = null }) &&
            row.TreatmentResult is not null && _row.TreatmentResult is not null &&
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(row.TreatmentResult.ToCanonicalJson()) ==
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(_row.TreatmentResult.ToCanonicalJson());
    }

    internal static async Task<byte[]> CreateJointAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        MortalJointRepairPublicationTakeReceipt taken, IReadOnlyList<CanonicalLocalFileChange> prepared)
    {
        var probe = AcceptedTurnAuthorityRegistry.ProbeTakenJointRepairPublication(fs, lease, taken);
        if (!probe.IsValid) throw Invalid("joint_taken_receipt");
        var baseline = await CreateAsync(fs, lease, taken.Plan, taken.Common.Binding, prepared) ?? throw Invalid("joint_qte_receipt");
        var root = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(baseline),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "owner-produced joint completion") ?? throw Invalid("joint_null");
        var history = WoundHistoryState.Parse(taken.Plan.WoundHistoryAfterImage?.ToJsonString(), WoundHistoryState.HistoryPath);
        var candidates = history.State?.Transitions.Where(row => row.OperationKey == taken.Authority.Request.Coordinates.OperationKey &&
            row.AttemptId == taken.Authority.Request.Coordinates.AttemptId && row.TreatmentResult is not null).ToArray();
        if (!history.IsValid || candidates is not { Length: 1 }) throw Invalid("joint_intended_history");
        var transition = candidates[0];
        var expected = MortalWoundTreatmentPersistedResult.Create(taken.Authority.Resolution);
        if (PersistedFingerprint(transition.TreatmentResult!) != PersistedFingerprint(expected)) throw Invalid("joint_intended_result");
        var resource = MortalWoundTreatmentResourceComposer.RecomputePersistedGuaranteedFinalization(transition.TreatmentResult!);
        if (resource is null || resource.Fingerprint != taken.Authority.Finalization.FinalizationFingerprint) throw Invalid("joint_intended_finalization");
        root["schemaVersion"] = 2;
        root["jointTreatment"] = new JsonObject {
            ["operationKey"] = expected.Request.Coordinates.OperationKey, ["attemptId"] = expected.Request.Coordinates.AttemptId,
            ["requestFingerprint"] = expected.Request.RequestFingerprint, ["persistedResultFingerprint"] = PersistedFingerprint(expected),
            ["resourceFinalizationFingerprint"] = resource.Fingerprint,
            ["repairCheckpointFingerprint"] = taken.Authority.RepairCheckpointFingerprint, ["treatmentHistoryOrdinal"] = transition.Ordinal };
        var footprint = new JsonObject();
        var permittedControls = MechanicalCommandPaths.Append(MortalWoundOpportunityAdapter.DecisionPath).ToHashSet(StringComparer.Ordinal);
        foreach (var change in prepared)
            if (!JointFootprintPaths.Contains(change.RelativePath, StringComparer.Ordinal) && !permittedControls.Contains(change.RelativePath))
                throw Invalid("joint_uncovered_member: " + change.RelativePath);
        foreach (var path in JointFootprintPaths)
        {
            var change = prepared.SingleOrDefault(row => row.RelativePath == path);
            footprint[path] = Digest(change is null ? await fs.ReadFileBytesAsync(lease, path) : change.After);
        }
        if (!prepared.Any(row => row.RelativePath == WoundAcceptedTurnSnapshotContract.PendingResolutionPath && row.After is null))
            throw Invalid("joint_wave_consumption");
        root["publishedFootprint"] = footprint;
        root["consumedPrivatePaths"] = new JsonArray(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        root.Remove("fingerprint"); root["fingerprint"] = JointFingerprint(root);
        return SerializePublicationRoot(root);
    }

    private static async Task<CompletedProof> ReadJointCompletedProofAsync(FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease, PendingTurnSnapshotReadAuthority inspection, JsonObject root)
    {
        if (root.Count != JointFields.Length || root.Any(row => !JointFields.Contains(row.Key, StringComparer.Ordinal))) throw Invalid("joint_shape");
        var payload = root.DeepClone().AsObject(); payload.Remove("fingerprint");
        if (Text(root, "fingerprint") != JointFingerprint(payload) || !fs.IsCurrentSessionGeneration(lease, Text(root, "generation")))
            throw Invalid("joint_seal_or_generation");
        var snapshot = ReadSnapshot(fs, lease);
        if (snapshot.Realm != "mortal_world" || inspection.SessionId != snapshot.SessionId || inspection.RequestId != snapshot.RequestId ||
            inspection.SnapshotToken != snapshot.SnapshotToken || inspection.TurnNumber != snapshot.TurnNumber ||
            Text(root, "sessionId") != snapshot.SessionId || Text(root, "requestId") != snapshot.RequestId ||
            Text(root, "snapshotToken") != snapshot.SnapshotToken || JointInteger(root, "turn") != snapshot.TurnNumber ||
            Text(root, "manifestHash") != Digest(await fs.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath)) ||
            Text(root, "authorityHash") != Digest(await fs.ReadFileBytesAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath)) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text(root, "inputFingerprint"))) throw Invalid("joint_original");
        var quartet = ClosedMap(root["originalQuartet"], QuartetPaths);
        foreach (var path in QuartetPaths)
            if (quartet[path]?.GetValue<string>() != Digest(snapshot.ReadRequiredBytes(path))) throw Invalid("joint_original_quartet");
        var footprint = ClosedMap(root["publishedFootprint"], JointFootprintPaths);
        foreach (var path in JointFootprintPaths)
            if (footprint[path]?.GetValue<string>() != Digest(await fs.ReadFileBytesAsync(lease, path))) throw Invalid("joint_published_footprint: " + path);
        if (root["consumedPrivatePaths"] is not JsonArray privatePaths || privatePaths.Count != 1 ||
            privatePaths[0]?.GetValue<string>() != WoundAcceptedTurnSnapshotContract.PendingResolutionPath ||
            await fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath) is not null)
            throw Invalid("joint_wave_present");
        var allowed = MechanicalCommandPaths.Append(MortalWoundOpportunityAdapter.DecisionPath).ToArray();
        if (root["consumedCommands"] is not JsonArray commands || commands.Count is < 2 or > 4 ||
            commands.Select(node => node?.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() != commands.Count ||
            commands.Any(node => !allowed.Contains(node?.GetValue<string>(), StringComparer.Ordinal)) ||
            !commands.Any(node => node?.GetValue<string>() == MortalWoundOpportunityAdapter.DecisionPath) ||
            !commands.Any(node => node?.GetValue<string>() == AcceptedMechanicsPlan.WoundCommandPath)) throw Invalid("joint_consumed_commands");

        var original = MortalQteWoundOpportunityProjection.ReadSnapshot(fs, lease);
        if (original.SessionId != snapshot.SessionId || original.RequestId != snapshot.RequestId ||
            original.SnapshotToken != snapshot.SnapshotToken || original.TurnNumber != snapshot.TurnNumber || original.Realm != snapshot.Realm)
            throw Invalid("joint_partition_binding");
        var treatment = ClosedMap(root["jointTreatment"], JointTreatmentFields);
        var history = WoundHistoryState.Parse(await fs.ReadFileAsync(lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        var originalHistory = WoundHistoryState.Parse(MortalQteWoundOpportunityProjection.ReadObservedRoot(original, WoundHistoryState.HistoryPath),
            WoundHistoryState.HistoryPath);
        var related = history.State?.Transitions.Where(row => row.OperationKey == Text(treatment, "operationKey") ||
            row.AttemptId == Text(treatment, "attemptId") || row.TreatmentResult?.Request.RequestFingerprint == Text(treatment, "requestFingerprint")).ToArray();
        if (!history.IsValid || !originalHistory.IsValid || related is not { Length: 1 } || related[0].TreatmentResult is not { } result)
            throw Invalid("joint_treatment_history");
        var row = related[0]; var request = result.Request; var coordinates = request.Coordinates;
        if (row.Kind != "treat" || row.OperationKey != coordinates.OperationKey || row.AttemptId != coordinates.AttemptId ||
            row.WoundId != coordinates.WoundId || coordinates.SessionId != snapshot.SessionId || coordinates.RequestId != snapshot.RequestId ||
            coordinates.SnapshotToken != snapshot.SnapshotToken || coordinates.Turn != snapshot.TurnNumber || row.Turn != snapshot.TurnNumber ||
            row.Ordinal != originalHistory.State!.NextOrdinal + 1 || row.Ordinal != JointInteger(treatment, "treatmentHistoryOrdinal") ||
            coordinates.OperationKey != Text(treatment, "operationKey") || coordinates.AttemptId != Text(treatment, "attemptId") ||
            request.RequestFingerprint != Text(treatment, "requestFingerprint") || PersistedFingerprint(result) != Text(treatment, "persistedResultFingerprint") ||
            !originalHistory.State.Transitions.Any(previous => previous.WoundId == row.WoundId) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text(treatment, "repairCheckpointFingerprint"))) throw Invalid("joint_treatment_binding");
        var finalization = MortalWoundTreatmentResourceComposer.RecomputePersistedGuaranteedFinalization(result);
        if (finalization is null || finalization.Fingerprint != Text(treatment, "resourceFinalizationFingerprint")) throw Invalid("joint_finalization");
        await ValidateJointPublishedResourcesAsync(fs, lease, snapshot, result, finalization);
        var offered = MortalQteWoundOpportunityProjection.Project(original);
        var issues = new List<ValidationIssue>();
        var suffix = new JointHistorySuffixProof(JointHistorySuffixIssuer, row);
        if (!MortalWoundOpportunityAdapter.TryReadPublishedJointReceiptPartition(fs, lease, original, suffix, issues, out var receipts) || receipts is null)
            throw Invalid("joint_receipt_partition: " + string.Join(";", issues.Select(issue => issue.Code)));
        var matches = receipts.Receipts.Where(receipt => receipt.SessionId == snapshot.SessionId && receipt.RequestId == snapshot.RequestId &&
            receipt.SnapshotToken == snapshot.SnapshotToken && receipt.Turn == snapshot.TurnNumber && receipt.ReceiptId == Text(root, "receiptId")).ToArray();
        var signed = MortalWoundOccurrenceState.Parse(MortalQteWoundOpportunityProjection.ReadObservedRoot(original, MortalWoundOccurrenceState.StatePath),
            MortalWoundOccurrenceState.StatePath);
        var receipt = matches.Length == 1 ? matches[0] : throw Invalid("joint_receipt_binding");
        var source = signed.State?.Occurrences.SingleOrDefault(occurrence => occurrence.OccurrenceId == receipt.OpportunityId);
        if (!signed.IsValid || source is null || offered.Count != 1 || offered[0]!["opportunityRef"]?.GetValue<string>() != source.OpportunityRef ||
            source.AdapterKind != "qte" || source.Domain != "physical" || source.ProfileKey != "mortal_qte_terminal_health_damage_v1" ||
            source.Owner.Realm != "mortal_world" || source.Owner.OwnerKind != "player" || source.Owner.OwnerId != "player_current" ||
            source.Owner.CarrierPath != WoundCarrierCatalog.PlayerPath || receipt.Decision != "materialize" ||
            Text(root, "receiptFingerprint") != receipt.ReceiptFingerprint || Text(root, "decisionFingerprint") != receipt.DecisionFingerprint ||
            Text(root, "operationKey") != receipt.OperationKey) throw Invalid("joint_qte_source");
        return new(root, receipt);
    }

    internal static async Task ValidatePublishedJointAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        MortalJointRepairPublicationTakeReceipt taken)
    {
        var proof = await ReadCompletedProofAsync(fs, lease, ReadInspectionSnapshot(fs, lease));
        var treatment = ClosedMap(proof.Root["jointTreatment"], JointTreatmentFields);
        if (proof.Root["schemaVersion"]?.GetValue<int>() != 2 ||
            Text(proof.Root, "inputFingerprint") != taken.Plan.InputFingerprint ||
            Text(treatment, "operationKey") != taken.Authority.Request.Coordinates.OperationKey ||
            Text(treatment, "attemptId") != taken.Authority.Request.Coordinates.AttemptId ||
            Text(treatment, "requestFingerprint") != taken.Authority.Request.RequestFingerprint ||
            Text(treatment, "persistedResultFingerprint") != PersistedFingerprint(MortalWoundTreatmentPersistedResult.Create(taken.Authority.Resolution)) ||
            Text(treatment, "resourceFinalizationFingerprint") != taken.Authority.Finalization.FinalizationFingerprint ||
            Text(treatment, "repairCheckpointFingerprint") != taken.Authority.RepairCheckpointFingerprint)
            throw Invalid("joint_readback_taken_binding");
    }

    private static async Task ValidateJointPublishedResourcesAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        PendingTurnSnapshotReadAuthority original, MortalWoundTreatmentPersistedResult result,
        MortalWoundTreatmentResourceComposer.PersistedGuaranteedFinalizationProof finalization)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await fs.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath), false);
        if (definitions.Catalog is not { } catalog || !definitions.IsValid) throw Invalid("joint_resource_definitions");
        var state = ResourceStateContract.ParseCanonical(await fs.ReadFileAsync(lease, ResourceMaterializationContract.StatePath), catalog, false);
        var history = ResourceHistoryState.ParseCanonical(await fs.ReadFileAsync(lease, ResourceMaterializationContract.HistoryPath), catalog, false);
        if (state.Ledger is not { } ledger || history.History is not { } transitions || state.Issues.Count != 0 || history.Issues.Count != 0 ||
            transitions.ValidateStateAgreement(ledger).Count != 0) throw Invalid("joint_resource_agreement");
        var originalHistory = ResourceHistoryState.ParseCanonical(CanonicalJsonUtf8.DecodeOneOptionalBom(
            original.ReadRequiredBytes(ResourceMaterializationContract.HistoryPath)), catalog, false);
        if (originalHistory.History is not { } prior || originalHistory.Issues.Count != 0) throw Invalid("joint_original_resource_history");
        var priorRows = JsonNode.Parse(prior.ToCanonicalJson())!.AsObject()["entries"]!.AsArray();
        var currentRows = JsonNode.Parse(transitions.ToCanonicalJson())!.AsObject()["entries"]!.AsArray();
        if (currentRows.Count < priorRows.Count || priorRows.Where((value, index) => !JsonNode.DeepEquals(value, currentRows[index])).Any())
            throw Invalid("joint_resource_history_prefix");
        var resourceConsumptions = finalization.Consumptions.Where(value => value.Kind == "resource_quantity").ToArray();
        if (transitions.Transitions.Skip(prior.Transitions.Count).Count(row => row.Phase == ResourceMutationPhase.RegisteredSystemOutcome &&
                row.Operation == ResourceTransitionOperation.Spend) != resourceConsumptions.Length) throw Invalid("joint_registered_spend_inventory");
        var owners = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(catalog, path => fs.ReadFileAsync(lease, path), ledger, transitions,
            CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        if (owners.Authority is null || owners.Issues.Count != 0) throw Invalid("joint_resource_owner");
        foreach (var consumption in resourceConsumptions)
        {
            if (!ResourceDefinitionCatalog.TryParseOwnerKind(consumption.OwnerKind, out var kind)) throw Invalid("joint_resource_kind");
            var owner = owners.Authority.Resolve(new ResourceOwnerRequest(consumption.Realm, kind, consumption.AuthorityRef, consumption.OwnerId, null));
            if (!owner.Success || owner.Entry is null) throw Invalid("joint_resource_owner_coordinate");
            var coordinate = new ResourceCoordinate(owner.Entry.Key.Realm, owner.Entry.Key.OwnerKind, owner.Entry.Key.ResourceOwnerId, consumption.AuthorityRef);
            var matches = transitions.Transitions.Where(row => row.Turn == original.TurnNumber &&
                row.Phase == ResourceMutationPhase.RegisteredSystemOutcome && row.Operation == ResourceTransitionOperation.Spend &&
                row.Coordinate == coordinate && row.RequestedAmount == consumption.Quantity && row.AppliedAmount == consumption.Quantity &&
                row.Outcome == ResourceTransitionOutcome.Applied && row.ReceiptId is null && row.OriginKind == "registered_system_outcome" &&
                row.OriginId == row.EventRef && row.SourceEvidence.SourceId == row.EventRef &&
                row.BeforeState is not null && row.AfterState is not null && row.SourceEvidence.SourceKind == "registered_system_outcome" &&
                row.AfterState.Current == row.BeforeState.Current - consumption.Quantity &&
                row.SourceEvidence.AuthorityFingerprint == WoundAcceptedTurnFingerprintWriter.Compute(new string?[] {
                    "book_of_eternity.mortal_wound_treatment.resource_publication_source", "1",
                    result.Request.ResourceAuthority.AcceptedStateFingerprint, result.Request.RequestFingerprint, result.Receipt.ResultFingerprint,
                    finalization.Fingerprint, consumption.IntentFingerprint, row.EventRef,
                    $"{coordinate.Realm}/{ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind)}/{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}",
                    row.BeforeState.Current.ToString(CultureInfo.InvariantCulture), row.AfterState.Current.ToString(CultureInfo.InvariantCulture) })).ToArray();
            if (matches.Length != 1) throw Invalid("joint_resource_consumption_receipt");
            var transition = matches[0];
            if (!catalog.TryResolveExact(consumption.AuthorityRef, out var definition) || definition is null) throw Invalid("joint_resource_definition");
            var sources = ResourceMutationSourceCatalog.Create(new[] { new ResourceMutationSourceExport("registered_system_outcome",
                transition.EventRef, transition.SourceEvidence.AuthorityFingerprint, ResourceMutationSourceState.Active, SameTurn: true) });
            var route = sources.Catalog?.Resolve(new ResourceMutationSourceRequest("registered_system_outcome", transition.EventRef, ResourceOperation.Spend),
                definition, coordinate);
            if (route is not { IsValid: true, Route: not null } || transition.Priority != route.Route.Priority ||
                transition.PolicyFingerprint != route.Route.PolicyBinding.AuthorityFingerprint) throw Invalid("joint_resource_policy");
        }
    }

    private static string PersistedFingerprint(MortalWoundTreatmentPersistedResult result) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new[] { "book_of_eternity.mortal_joint_repair.persisted_result.v2",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(result.ToCanonicalJson()) });
    private static int JointInteger(JsonObject root, string field) => root[field] is JsonValue value && value.TryGetValue<int>(out var number)
        ? number : throw Invalid("joint_integer_" + field);
    private static string JointFingerprint(JsonObject root) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JointHashDomain + WoundAcceptedTurnFingerprintWriter.CanonicalJson(root))));
}
