using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private SpiritualOriginalTurnCapture? _spiritualOriginalTurnCapture;

    internal sealed record SpiritualOriginalTurnCaptureResult(
        SpiritualOriginalTurnCapture? Capture, IReadOnlyList<ValidationIssue> Issues);

    // Only the validator's actual original-input path can fill this private sink.
    // It interrupts before resource reduction; it does not publish a common plan.
    private sealed class SpiritualOriginalInputSink
    {
        internal AcceptedMechanicsInput? Input;
        internal SpiritualWoundSourceSession? Source;
        internal bool AwaitOriginalPrefix;
        internal required SpiritualOriginalAllocationOwner Allocations;
        /// <summary>
        /// Retained current source images for named original intake, or <see langword="null"/>
        /// for the existing physical source path.
        /// </summary>
        internal SpiritualOriginalDraftInputs? CurrentInputs;
    }

    internal Task<SpiritualOriginalTurnCaptureResult> CaptureSpiritualOriginalTurnAsync(
        FileSystemManager.CanonicalWriteLease lease) =>
        SpiritualOriginalTurnCapture.CaptureAsync(this, lease);

    /// <summary>
    /// Acquires an original spiritual capture with the selected raw upstream owner intake.
    /// </summary>
    /// <param name="lease">
    /// Active lease for this validator's real canonical filesystem.
    /// </param>
    /// <param name="allocationJournalJson">
    /// Retained comparison rows; an empty array starts the first attempt.
    /// </param>
    /// <param name="replayAllocations">
    /// Forbids new allocation values when <see langword="true"/>.
    /// </param>
    /// <returns>
    /// An original input capture or validation issues, without accepted canonical publication.
    /// </returns>
    internal Task<SpiritualOriginalTurnCaptureResult> CaptureSpiritualOriginalTurnWithIntakeAsync(
        FileSystemManager.CanonicalWriteLease lease,
        string allocationJournalJson = "[]", bool replayAllocations = false) =>
        SpiritualOriginalTurnCapture.CaptureWithIntakeAsync(this, lease, allocationJournalJson, replayAllocations);

    /// <summary>
    /// Recreates original owners from a detached checkpoint after verifying its physical origin.
    /// This entry point does not apply saved continuation advances or publish canonical output.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease for the real filesystem and signed snapshot reader.
    /// </param>
    /// <param name="checkpoint">
    /// Structurally validated checkpoint whose original draft is replayed by actual owners.
    /// </param>
    /// <returns>
    /// A private original capture or validation issues, without canonical writes.
    /// </returns>
    internal Task<SpiritualOriginalTurnCaptureResult> CaptureSpiritualOriginalTurnFromCheckpointOriginAsync(
        FileSystemManager.CanonicalWriteLease lease, SpiritualWoundCaptureCheckpointState checkpoint) =>
        SpiritualOriginalTurnCapture.CaptureFromCheckpointOriginAsync(this, lease, checkpoint);

    // This is an original input owner. It is deliberately not an accepted wound
    // frontier, generation receipt or completed resource/effect plan.
    internal sealed partial class SpiritualOriginalTurnCapture : IDisposable
    {
        private static readonly string[] FixedDraftCapturePaths =
        [
            SpiritualWoundSourceSession.SoulPath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            AfterlifeSpiritualConflictState.StatePath,
            AfterlifeEntityProfileState.StatePath,
            ShiningAbodeState.StatePath,
            AfterlifeSpiritualConflictState.DifficultySettingsPath,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            EffectCarrierCatalog.PlayerPath,
            EffectCarrierCatalog.NpcPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            WoundCarrierCatalog.PlayerPath,
            WoundCarrierCatalog.NpcPath,
            WoundCarrierCatalog.EnemiesPath,
            WoundCarrierCatalog.AlliesPath,
            EffectAcceptedTurnPlan.CommandPath,
            ResourcePendingResolutionState.PendingPath,
            ResourceMaterializationContract.CommandPath,
            AcceptedMechanicsPlan.WoundCommandPath,
            EffectAcceptedTurnInputComposer.WorldTimePath,
            FullPartyInteractionsPath,
            StorageTransportMoveService.VehiclesPath,
            "game_state/meta/guardians.json",
            "game_state/control/life_transitions.json",
            "game_state/misc/characteristics.json",
            "game_state/player/player_status.json",
            "game_state/core/player_status.json",
            LegacyItemResourcePath,
            MortalFactionChroniclesPath,
            GuardianAbodeResidentState.StatePath,
            SarefMainStoryState.StatePath
        ];

        /// <summary>
        /// Gets the exact current location and item carriers required in a named original draft.
        /// </summary>
        internal static readonly string[] FixedOriginalItemLocationCurrentPaths =
        [
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationIdentityState.StatePath,
            MortalBootstrapLocationScaffold.StatePath,
            NpcCoreChangesContract.NpcCorePath,
            FactionCoreChangesContract.FactionCorePath,
            InventoryEquipmentService.ItemsPath,
            "game_state/npcs/npc_inventory.json",
            MortalLocationStorageContentsState.StatePath,
            StorageTransportMoveService.VehiclesPath,
            MortalItemIdentityState.StatePath,
            MortalItemAcceptedTransferCatalog.PlayerRemovalPath,
            "game_state/inventory/item_bonds.json",
            "game_state/inventory/item_text_updates.json",
            "game_state/inventory/recipes.json",
            "game_state/npcs/item_journals.json",
            "game_state/quests/quest_history.json"
        ];

        private static readonly string[] FixedPhysicalWitnessPaths =
        [
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath,
            LiveTurnPreparationService.TurnRequestPath,
            "game_state/control/validation_repair_request.json",
            "ready/turn_complete.json",
            SpiritualWoundDecisionPendingState.StatePath,
            SpiritualWoundCaptureCheckpointState.StatePath
        ];

        private static IEnumerable<string> EnumeratePhysicalWitnessPaths(IEnumerable<string> currentPaths) =>
            FixedPhysicalWitnessPaths
                .Concat(WoundAcceptedTurnSnapshotContract.RequiredPaths
                    .Where(path => !SpiritualOriginalDraftInputs.IsDraftPath(path)))
                .Concat(currentPaths.Where(path =>
                    !SpiritualOriginalDraftInputs.IsDraftPath(path) &&
                    (path.StartsWith("game_state/control/", StringComparison.Ordinal) ||
                     path.StartsWith("input/", StringComparison.Ordinal) ||
                     path.StartsWith("ready/", StringComparison.Ordinal) ||
                     path.StartsWith("stories/", StringComparison.Ordinal)) &&
                    !path.StartsWith(ExplorerLocalTurnRollbackArtifacts.Root + "/",
                        StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith(LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/",
                        StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.Ordinal);

        private static ValidationIssue? ReadRawOriginalInputPathIssue(
            IReadOnlyList<string> currentPaths, IEnumerable<string> declaredPaths,
            bool includeUpstreamIntake, SpiritualOriginalDraftInputs? coldOriginalInputs)
        {
            var declared = declaredPaths.ToArray();
            var aliasedRoot = (coldOriginalInputs?.PathInventory ?? currentPaths)
                .Concat(declared).FirstOrDefault(SpiritualOriginalDraftInputs.HasCaseAliasedDraftRoot);
            if (aliasedRoot != null)
                return SourceIssue(aliasedRoot, "spiritual_original_input_path_alias",
                    "canonical exact draft-root spelling before original input binding");
            var fixedDraftPaths = FixedDraftCapturePaths
                .Concat(includeUpstreamIntake ? FixedOriginalItemLocationCurrentPaths : [])
                .Concat(includeUpstreamIntake ? FixedOriginalOutputPaths : [])
                .Concat(WoundAcceptedTurnSnapshotContract.RequiredPaths)
                .Concat(EffectAcceptedTurnInputComposer.SourceAuthorityPaths).ToArray();
            var physicalFixedPaths = FixedPhysicalWitnessPaths
                .Concat(WoundAcceptedTurnSnapshotContract.RequiredPaths
                    .Where(path => !SpiritualOriginalDraftInputs.IsDraftPath(path))).ToArray();
            var fixedPaths = fixedDraftPaths.Concat(physicalFixedPaths).ToArray();
            var rawDraftPaths = (coldOriginalInputs?.PathInventory ?? declared)
                .Concat(coldOriginalInputs == null ? currentPaths : [])
                .Concat(fixedDraftPaths).Where(SpiritualOriginalDraftInputs.IsDraftPathCandidate);
            // Cold draft images replace current draft names; physical witnesses remain live.
            // The full raw scan contributes only full fixed-path matches, not extra payload.
            var physicalAliasTargets = coldOriginalInputs == null ? fixedPaths : physicalFixedPaths;
            var rawPaths = rawDraftPaths.Concat(EnumeratePhysicalWitnessPaths(currentPaths))
                .Concat(currentPaths.Where(path => physicalAliasTargets.Contains(path, StringComparer.OrdinalIgnoreCase)))
                .Concat(fixedPaths);
            try
            {
                PendingTurnSnapshotAuthority.RequireExactSignedPaths(rawPaths);
                return null;
            }
            catch (InvalidDataException failure)
            {
                return SourceIssue(LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                    "spiritual_original_input_path_alias", failure.Message);
            }
        }

        private static void RevokePreviousOriginalInputs(
            ValidationService validator, FileSystemManager.CanonicalWriteLease lease, bool includeUpstreamIntake)
        {
            validator._mortalOriginalTurnCapture?.Dispose();
            validator._spiritualOriginalTurnCapture?.RevokeUnderLease(lease);
            validator.InvalidateSpiritualWoundSourceSession();
            AcceptedMechanicsPlanAuthority.InvalidateValidated(validator._fs, lease);
            EffectAcceptedTurnPlanAuthority.InvalidateValidated(validator._fs, lease);
            if (includeUpstreamIntake)
                MortalItemAcceptedTurnAuthority.InvalidateValidatedItems(validator._fs, lease);
        }

        /// <summary>
        /// Replaces any retained capture with signed original inputs and their resource/effect owners.
        /// </summary>
        /// <param name="validator">
        /// Validator whose current capture and source session are replaced.
        /// </param>
        /// <param name="lease">
        /// Active canonical write lease for acquisition and invalidation.
        /// </param>
        /// <param name="awaitOriginalPrefix">
        /// Defers source preparation until the owned ordinary prefix closes when <see langword="true"/>; defaults to immediate preparation.
        /// </param>
        /// <returns>
        /// A retained original capture or validation issues preventing acquisition.
        /// </returns>
        /// <param name="allocationJournalJson">
        /// Retained comparison rows, or an empty array for a fresh capture; these confer no input authority.
        /// </param>
        /// <param name="replayAllocations">
        /// Requires strict allocation replay when <see langword="true"/>; otherwise appends after the retained prefix.
        /// </param>
        internal static Task<SpiritualOriginalTurnCaptureResult> CaptureAsync(
            ValidationService validator,
            FileSystemManager.CanonicalWriteLease lease, bool awaitOriginalPrefix = false,
            string allocationJournalJson = "[]", bool replayAllocations = false) =>
            CaptureCoreAsync(validator, lease, awaitOriginalPrefix, allocationJournalJson, replayAllocations,
                includeUpstreamIntake: false);

        /// <summary>
        /// Enters original-prefix capture with its own item and location intake adapters.
        /// </summary>
        /// <param name="validator">
        /// Validator owning the actual source-session and capture lifetimes.
        /// </param>
        /// <param name="lease">
        /// Active canonical write lease for the validator's filesystem.
        /// </param>
        /// <param name="allocationJournalJson">
        /// Retained comparison rows, or an empty array for initial recording.
        /// </param>
        /// <param name="replayAllocations">
        /// Requires strict retained allocation replay when <see langword="true"/>.
        /// </param>
        /// <returns>
        /// An original input capture or validation issues without canonical publication.
        /// </returns>
        internal static Task<SpiritualOriginalTurnCaptureResult> CaptureWithIntakeAsync(
            ValidationService validator, FileSystemManager.CanonicalWriteLease lease,
            string allocationJournalJson, bool replayAllocations) =>
            CaptureCoreAsync(validator, lease, true, allocationJournalJson, replayAllocations,
                includeUpstreamIntake: true);

        /// <summary>
        /// Reopens the signed original and exact physical witnesses before replaying saved draft A.
        /// </summary>
        /// <param name="validator">
        /// Fresh validator that will own any successful capture.
        /// </param>
        /// <param name="lease">
        /// Active lease for the real canonical filesystem.
        /// </param>
        /// <param name="checkpoint">
        /// Structurally parsed private comparison evidence.
        /// </param>
        /// <returns>
        /// A reconstructed original owner tuple or origin validation issues.
        /// </returns>
        internal static async Task<SpiritualOriginalTurnCaptureResult> CaptureFromCheckpointOriginAsync(
            ValidationService validator, FileSystemManager.CanonicalWriteLease lease,
            SpiritualWoundCaptureCheckpointState checkpoint)
        {
            ArgumentNullException.ThrowIfNull(checkpoint);
            validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            if (!AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
                    validator._fs, lease, expectedFactory: null, checkPlansAndItems: false) ||
                AcceptedMechanicsPlanAuthority.HasValidated(validator._fs, lease) ||
                MortalItemAcceptedTurnAuthority.HasValidatedItems(validator._fs, lease))
                return new(null, [SourceIssue(SpiritualWoundCaptureCheckpointState.StatePath,
                    "spiritual_original_intake_claim_conflict", "a vacant physical generation")]);
            var originalRead = PendingTurnSnapshotReader.ReadCurrent(validator._fs, lease,
                PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                    [SpiritualWoundSourceSession.SoulPath],
                    [SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                     SpiritualWoundOpportunityReceiptState.StatePath, AfterlifeSpiritualConflictState.StatePath]));
            if (!originalRead.Success || originalRead.Snapshot is not { } signed)
                return new(null, originalRead.Issues);
            var originalInputs = checkpoint.ReadOriginalDraftInputs();
            if (checkpoint.OriginalRealm != signed.Realm ||
                !originalInputs.MatchesIdentity(signed.SessionId, signed.RequestId,
                    signed.SnapshotToken, signed.TurnNumber) ||
                signed.DeclaredOriginalLogicalPaths.Any(path =>
                    SpiritualOriginalDraftInputs.IsDraftPath(path) &&
                    !originalInputs.PathInventory.Contains(path, StringComparer.Ordinal)) ||
                FixedDraftCapturePaths.Concat(FixedOriginalItemLocationCurrentPaths)
                    .Concat(FixedOriginalOutputPaths)
                    .Concat(WoundAcceptedTurnSnapshotContract.RequiredPaths)
                    .Concat(EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
                    .Where(SpiritualOriginalDraftInputs.IsDraftPath)
                    .Any(path => !originalInputs.PathInventory.Contains(path, StringComparer.Ordinal)))
                return new(null, [SourceIssue(SpiritualWoundCaptureCheckpointState.StatePath,
                    "spiritual_checkpoint_origin_mismatch", "the signed original identity and complete draft inventory")]);
            foreach (var witness in checkpoint.ReadOriginalPhysicalWitnessFingerprints())
            {
                var bytes = await validator._fs.ReadFileBytesAsync(lease, witness.Key);
                if (new CanonicalBeforeImage(bytes != null, bytes).Fingerprint == witness.Value)
                    continue;
                return new(null, [SourceIssue(witness.Key,
                    "spiritual_checkpoint_origin_mismatch", "the exact immutable physical origin witness")]);
            }
            return await CaptureCoreAsync(validator, lease, true,
                checkpoint.ReadAllocationJournalJson(), replayAllocations: true,
                includeUpstreamIntake: true, coldOriginalInputs: originalInputs);
        }

        /// <summary>
        /// Freezes original inputs once and admits selected owners inside one private allocation scope.
        /// </summary>
        /// <param name="validator">
        /// Validator retaining successful source and capture ownership.
        /// </param>
        /// <param name="lease">
        /// Active canonical write lease for every original read and owner handoff.
        /// </param>
        /// <param name="awaitOriginalPrefix">
        /// Retains the chronological original prefix when <see langword="true"/>; otherwise preserves direct capture behavior.
        /// </param>
        /// <param name="allocationJournalJson">
        /// Retained comparison stream, which may include later continuation allocations.
        /// </param>
        /// <param name="replayAllocations">
        /// Rejects new values beyond the retained stream when <see langword="true"/>.
        /// </param>
        /// <param name="includeUpstreamIntake">
        /// Runs raw location and item admission with this capture's adapters when <see langword="true"/>.
        /// </param>
        /// <param name="coldOriginalInputs">
        /// Authenticated original draft to replay on a fresh validator; <see langword="null"/> freezes current physical inputs.
        /// </param>
        /// <returns>
        /// The attached original capture, or issues with all temporary handoffs revoked.
        /// </returns>
        private static async Task<SpiritualOriginalTurnCaptureResult> CaptureCoreAsync(
            ValidationService validator, FileSystemManager.CanonicalWriteLease lease,
            bool awaitOriginalPrefix, string allocationJournalJson, bool replayAllocations,
            bool includeUpstreamIntake, SpiritualOriginalDraftInputs? coldOriginalInputs = null)
        {
            validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            if (includeUpstreamIntake &&
                !AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
                    validator._fs, lease, expectedFactory: null, checkPlansAndItems: false))
                return new(null, new[] { SourceIssue(SpiritualWoundSourceSession.SoulPath,
                    "spiritual_original_intake_claim_conflict",
                    "a current physical generation without outstanding treatment publication claims") });
            var currentPaths = validator._fs.EnumerateFiles(lease, "*");
            var rawPathIssue = ReadRawOriginalInputPathIssue(currentPaths, [], includeUpstreamIntake, coldOriginalInputs);
            if (rawPathIssue != null)
                return new(null, [rawPathIssue]);
            IReadOnlyCollection<string> originalSelection = includeUpstreamIntake
                ? PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                    [SpiritualWoundSourceSession.SoulPath],
                    [SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath,
                     SpiritualWoundOpportunityReceiptState.StatePath,
                     AfterlifeSpiritualConflictState.StatePath])
                : [SpiritualWoundSourceSession.SoulPath];
            PendingTurnSnapshotReadResult originalRead;
            try
            {
                originalRead = PendingTurnSnapshotReader.ReadCurrent(validator._fs, lease, originalSelection);
            }
            catch
            {
                // Moving this pure read before handoff must retain ordinary reader
                // failure revocation, including exceptional rather than issue exits.
                RevokePreviousOriginalInputs(validator, lease, includeUpstreamIntake);
                throw;
            }
            if (!originalRead.Success || originalRead.Snapshot is not { } originalSnapshot)
            {
                RevokePreviousOriginalInputs(validator, lease, includeUpstreamIntake);
                return new(null, originalRead.Issues);
            }
            rawPathIssue = ReadRawOriginalInputPathIssue(currentPaths,
                originalSnapshot.DeclaredOriginalLogicalPaths, includeUpstreamIntake, coldOriginalInputs);
            if (rawPathIssue != null)
                return new(null, [rawPathIssue]);
            RevokePreviousOriginalInputs(validator, lease, includeUpstreamIntake);
            var draftPaths = (coldOriginalInputs?.PathInventory ?? originalSnapshot.DeclaredOriginalLogicalPaths)
                .Concat(coldOriginalInputs == null
                    ? currentPaths.Where(SpiritualOriginalDraftInputs.IsDraftPath)
                    : [])
                .Concat(FixedDraftCapturePaths)
                .Concat(includeUpstreamIntake ? FixedOriginalItemLocationCurrentPaths : [])
                .Concat(includeUpstreamIntake ? FixedOriginalOutputPaths : [])
                .Concat(WoundAcceptedTurnSnapshotContract.RequiredPaths)
                .Concat(EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
                .Where(SpiritualOriginalDraftInputs.IsDraftPath)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var initialDraftImages = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
            foreach (var path in draftPaths)
            {
                if (coldOriginalInputs != null)
                    initialDraftImages.Add(path, coldOriginalInputs.ReadImage(path));
                else
                {
                    var bytes = await validator._fs.ReadFileBytesAsync(lease, path);
                    initialDraftImages.Add(path, new CanonicalBeforeImage(bytes != null, bytes));
                }
            }
            var draftInputs = SpiritualOriginalDraftInputs.Create(originalSnapshot.SessionId,
                originalSnapshot.RequestId, originalSnapshot.SnapshotToken,
                originalSnapshot.TurnNumber, draftPaths, initialDraftImages);
            var initialPhysicalImages = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
            var physicalPaths = EnumeratePhysicalWitnessPaths(currentPaths);
            foreach (var path in physicalPaths)
            {
                var bytes = await validator._fs.ReadFileBytesAsync(lease, path);
                initialPhysicalImages.Add(path, new CanonicalBeforeImage(bytes != null, bytes));
            }
            var allocations = new SpiritualOriginalAllocationOwner(allocationJournalJson, replayAllocations, includeUpstreamIntake);
            var sink = new SpiritualOriginalInputSink
            {
                AwaitOriginalPrefix = awaitOriginalPrefix, Allocations = allocations,
                CurrentInputs = includeUpstreamIntake ? draftInputs : null
            };
            var retained = false;
            using var allocationScope = allocations.Clock.BeginSpeculation();
            try
            {
                var issues = new List<ValidationIssue>();
                if (includeUpstreamIntake)
                {
                    issues.AddRange(await validator.ValidateSpiritualOriginalLocationItemIntakeAsync(
                        lease, draftInputs, allocations, allocationScope));
                    if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                        return new(null, issues);
                }
                issues.AddRange(await validator.ValidateAcceptedTurnRawResourceMaterializationCoreAsync(lease, sink));
                if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                    return new(null, issues);
                if (sink.Input is not { PlanningContext.EffectPlan: { } plan } input ||
                    sink.Source is not { IsCurrentOwner: true } source ||
                    !ReferenceEquals(source, validator._spiritualWoundSourceSession) ||
                    input.SessionId != source.SessionId || input.RequestId != source.RequestId ||
                    input.SnapshotToken != source.SnapshotToken || input.Turn != source.TurnNumber ||
                    !EffectAcceptedTurnPlanAuthority.TryPeekValidated(validator._fs, lease,
                        out var binding, out var result) || !result.Success ||
                    !ReferenceEquals(result.Plan, plan) || binding.SessionId != input.SessionId ||
                    binding.SnapshotToken != input.SnapshotToken)
                    return new(null, new[] { SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                        "spiritual_original_capture_missing", "exact signed source and validated original effect handoff") });
                if (!draftInputs.MatchesIdentity(input.SessionId, input.RequestId,
                        input.SnapshotToken, input.Turn) ||
                    !draftInputs.MatchesIdentity(source.SessionId, source.RequestId,
                        source.SnapshotToken, source.TurnNumber))
                    return new(null, new[] { SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                        "spiritual_original_input_identity_mismatch",
                        "the exact original snapshot, resource input and source session identity") });

                var outputProjection = includeUpstreamIntake
                    ? BuildOriginalOutputProjection(draftInputs, allocations.Clock)
                    : null;

                // Include effect source inputs even if the original base has no effects.
                // Publication before-images alone do not cover every source authority read.
                var paths = input.BeforeImages.Keys
                    .Concat(EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
                    .Concat(source.SelectedPaths)
                    .Append(EffectAcceptedTurnPlan.CommandPath)
                    .Append(EffectAcceptedTurnInputComposer.WorldTimePath)
                    // Observed read inputs remain binding even when absent or
                    // when they produced no publication after-image this turn.
                    .Append(AcceptedMechanicsPlan.WoundCommandPath)
                    .Append(FullPartyInteractionsPath)
                    .Append(StorageTransportMoveService.VehiclesPath)
                    .Append("game_state/meta/guardians.json")
                    .Append("game_state/control/life_transitions.json")
                    .Append("game_state/misc/characteristics.json")
                    .Append("game_state/player/player_status.json")
                    .Append("game_state/core/player_status.json")
                    .Append(LegacyItemResourcePath)
                    .Append(MortalFactionChroniclesPath)
                    .Append(GuardianAbodeResidentState.StatePath)
                    .Append(SarefMainStoryState.StatePath)
                    .Concat(includeUpstreamIntake ? FixedOriginalItemLocationCurrentPaths : [])
                    .Concat(includeUpstreamIntake ? FixedOriginalOutputPaths : [])
                    .Distinct(StringComparer.Ordinal);
                var observed = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
                foreach (var path in paths)
                {
                    var bytes = SpiritualOriginalDraftInputs.IsDraftPath(path) && coldOriginalInputs != null
                        ? coldOriginalInputs.ReadImage(path).Bytes
                        : await validator._fs.ReadFileBytesAsync(lease, path);
                    var image = new CanonicalBeforeImage(bytes != null, bytes);
                    observed.Add(path, image);
                    CanonicalBeforeImage initial;
                    if (SpiritualOriginalDraftInputs.IsDraftPath(path))
                    {
                        if (!draftInputs.PathInventory.Contains(path, StringComparer.Ordinal))
                            return new(null, new[] { SourceIssue(path,
                                "spiritual_original_input_unregistered", "a registered original draft path") });
                        initial = draftInputs.ReadImage(path);
                    }
                    else if (!initialPhysicalImages.TryGetValue(path, out initial!))
                    {
                        return new(null, new[] { SourceIssue(path,
                            "spiritual_original_input_unregistered", "a retained physical freshness witness") });
                    }
                    if (initial.Fingerprint != image.Fingerprint)
                        return new(null, new[] { SourceIssue(path,
                            "spiritual_original_input_changed", "the unchanged original input image") });
                }
                var physicalWitnesses = initialPhysicalImages
                    .Where(pair => pair.Key is LiveTurnPreparationService.PendingTurnSnapshotManifestPath or
                        PendingTurnSnapshotAuthority.AuthorityPath or LiveTurnPreparationService.TurnRequestPath ||
                        WoundAcceptedTurnSnapshotContract.RequiredPaths.Contains(pair.Key,
                            StringComparer.Ordinal) ||
                        observed.ContainsKey(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                var signedServiceRollbackImages = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
                foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                    SpiritualWoundDecisionPendingState.StatePath })
                {
                    if (originalSnapshot.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal))
                    {
                        var bytes = originalSnapshot.ReadRequiredBytes(path);
                        signedServiceRollbackImages.Add(path, new CanonicalBeforeImage(true, bytes));
                    }
                    else if (originalSnapshot.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal))
                        signedServiceRollbackImages.Add(path, new CanonicalBeforeImage(false, null));
                }
                var capture = new SpiritualOriginalTurnCapture(validator, input, source, observed,
                    physicalWitnesses, signedServiceRollbackImages, draftInputs, allocations,
                    outputProjection, includeUpstreamIntake ? originalSnapshot : null)
                {
                    _usesOriginalPrefix = awaitOriginalPrefix,
                    _usesColdOriginalInputs = coldOriginalInputs != null
                };
                if (coldOriginalInputs != null)
                    source.BindColdOriginalInputs(lease, capture);
                validator._spiritualOriginalTurnCapture = capture;
                allocationScope.Commit();
                retained = true;
                return new(capture, issues);
            }
            finally
            {
                if (!retained)
                {
                    allocations.Journal.Invalidate();
                    if (allocations.Items != null)
                        MortalItemAcceptedTurnAuthority.InvalidateValidatedItems(validator._fs, lease, allocations.Items);
                    validator.InvalidateSpiritualWoundSourceSession();
                    EffectAcceptedTurnPlanAuthority.InvalidateValidated(validator._fs, lease);
                }
            }
        }

        private readonly ValidationService _validator;
        private readonly AcceptedMechanicsInput _input;
        private readonly SpiritualWoundSourceSession _source;
        private readonly IReadOnlyDictionary<string, CanonicalBeforeImage> _observed;
        private readonly IReadOnlyDictionary<string, CanonicalBeforeImage> _physicalWitnesses;
        private readonly IReadOnlyDictionary<string, CanonicalBeforeImage> _signedServiceRollbackImages;
        private readonly SpiritualOriginalDraftInputs _draftInputs;
        private bool _usesColdOriginalInputs;
        private bool _initialColdSourceInputsTaken;
        private SpiritualOriginalDraftInputs? _coldCurrentInputs;
        private SpiritualOriginalDraftInputs? _coldProposedInputs;
        private long _coldViewRevision;
        private readonly SpiritualOriginalAllocationOwner _allocations;
        private readonly SpiritualOriginalOutputProjection? _originalOutputProjection;
        private AcceptedMechanicsPlanner.ResourceExecutionSession? _resources;
        private EffectAcceptedTurnPlanner.EffectAcceptedDraft? _effects;
        private AcceptedMechanicsPlanner.ResourceExecutionStep? _lastResourceStep;
        private int _nextResourceOrdinal;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private bool _disposed;

        /// <summary>
        /// Retains the original execution owners, observed inputs and the same initial allocation owner.
        /// </summary>
        /// <param name="validator">
        /// Validator registering the current capture.
        /// </param>
        /// <param name="input">
        /// Authenticated original mechanics input with attempt-owned factories.
        /// </param>
        /// <param name="source">
        /// Exact retained source session acquired during original composition.
        /// </param>
        /// <param name="observed">
        /// Observed input before-images cloned for later freshness checks.
        /// </param>
        /// <param name="allocations">
        /// Allocation owner that prepared the input and remains exclusive to this attempt.
        /// </param>
        /// <param name="physicalWitnesses">
        /// Excluded physical observations retained separately from ordinary draft data.
        /// </param>
        /// <param name="signedServiceRollbackImages">
        /// Signed original private service-root images retained apart from physical freshness witnesses.
        /// </param>
        /// <param name="draftInputs">
        /// Exact distributed original draft images bound to the same original identity.
        /// </param>
        /// <param name="outputProjection">
        /// Named intake's immutable projected output, or <see langword="null"/> for legacy capture.
        /// </param>
        /// <param name="signedOriginSnapshot">
        /// Selected signed C1 origin including receipt and conflict, or <see langword="null"/> for legacy capture.
        /// </param>
        private SpiritualOriginalTurnCapture(ValidationService validator,
            AcceptedMechanicsInput input, SpiritualWoundSourceSession source,
            IReadOnlyDictionary<string, CanonicalBeforeImage> observed,
            IReadOnlyDictionary<string, CanonicalBeforeImage> physicalWitnesses,
            IReadOnlyDictionary<string, CanonicalBeforeImage> signedServiceRollbackImages,
            SpiritualOriginalDraftInputs draftInputs, SpiritualOriginalAllocationOwner allocations,
            SpiritualOriginalOutputProjection? outputProjection,
            PendingTurnSnapshotReadAuthority? signedOriginSnapshot)
        {
            _validator = validator;
            _input = input;
            _source = source;
            _observed = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(observed);
            _physicalWitnesses = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(physicalWitnesses);
            _signedServiceRollbackImages = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(
                signedServiceRollbackImages);
            _draftInputs = draftInputs;
            _coldCurrentInputs = draftInputs;
            _allocations = allocations;
            _originalOutputProjection = outputProjection;
            _signedC1OriginSnapshot = signedOriginSnapshot;
        }

        internal string SessionId => _input.SessionId;
        internal string RequestId => _input.RequestId;
        /// <summary>
        /// Gets whether this capture, source and allocation journal remain current and healthy.
        /// </summary>
        internal bool IsCurrentOwner => !_disposed && _allocations.Journal.IsHealthy && _source.IsCurrentOwner &&
            ReferenceEquals(_validator._spiritualOriginalTurnCapture, this);

        /// <summary>
        /// Grants the exact immutable initial replay view to this capture's source once.
        /// Later continuation layers require a separate capture-owned transaction.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for this capture's real filesystem.
        /// </param>
        /// <param name="source">
        /// Exact retained source session receiving the initial cold view.
        /// </param>
        /// <returns>
        /// The immutable original draft already admitted by this capture.
        /// </returns>
        internal SpiritualOriginalDraftInputs TakeInitialColdSourceInputs(
            FileSystemManager.CanonicalWriteLease lease, SpiritualWoundSourceSession source)
        {
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            if (!_usesColdOriginalInputs || _initialColdSourceInputsTaken || _disposed ||
                !_allocations.Journal.IsHealthy || !ReferenceEquals(_source, source) ||
                !source.IsCurrentOwner || _resources != null)
                throw new InvalidOperationException("Only the current cold capture can bind its initial source view once.");
            _initialColdSourceInputsTaken = true;
            return _draftInputs;
        }

        /// <summary>
        /// Selects this capture's current or speculative cold layer for its exact source owner.
        /// </summary>
        /// <param name="source">
        /// Source session bound to this capture.
        /// </param>
        /// <param name="advancingCapture">
        /// Whether this exact capture requested its next resource exchange.
        /// </param>
        /// <param name="inputs">
        /// Selected detached layer, or <see langword="null"/> on rejection.
        /// </param>
        /// <param name="revision">
        /// Current committed layer revision, or zero on rejection.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for the current cold owner and its selected layer.
        /// </returns>
        internal bool TryReadColdSourceLayer(SpiritualWoundSourceSession source, bool advancingCapture,
            out SpiritualOriginalDraftInputs? inputs, out long revision)
        {
            inputs = null;
            revision = 0;
            if (!_usesColdOriginalInputs || !IsCurrentOwner || !ReferenceEquals(source, _source) ||
                _coldCurrentInputs is null ||
                !ReferenceEquals(source.ColdOriginalInputs, _coldCurrentInputs))
                return false;
            if (_coldProposedInputs != null && !advancingCapture)
                return false;
            inputs = _coldProposedInputs ?? _coldCurrentInputs;
            revision = _coldViewRevision;
            return true;
        }

        /// <summary>
        /// Checks that an owner-issued prepared ticket still targets the selected cold layer.
        /// </summary>
        /// <param name="source">
        /// Exact bound source session.
        /// </param>
        /// <param name="advancingCapture">
        /// Whether the ticket belongs to this capture's resource advancement.
        /// </param>
        /// <param name="inputs">
        /// Layer selected when the source ticket was prepared.
        /// </param>
        /// <param name="revision">
        /// Committed layer revision observed by that ticket.
        /// </param>
        /// <returns>
        /// <see langword="true"/> while the same layer and revision remain selected.
        /// </returns>
        internal bool OwnsColdSourceLayer(SpiritualWoundSourceSession source, bool advancingCapture,
            SpiritualOriginalDraftInputs inputs, long revision) =>
            TryReadColdSourceLayer(source, advancingCapture, out var selected, out var currentRevision) &&
            ReferenceEquals(selected, inputs) && currentRevision == revision;

        /// <summary>
        /// Rejects a stale lease, disposed capture or faulted allocation owner before any cached return.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease for the current operation.
        /// </param>
        private void EnsureCurrent(FileSystemManager.CanonicalWriteLease lease)
        {
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            ObjectDisposedException.ThrowIf(_disposed, this);
            _allocations.Journal.EnsureUsable();
            if (!IsCurrentOwner)
                throw new InvalidOperationException("The original turn capture was revoked.");
        }

        /// <summary>
        /// Verifies that every retained original input still matches its captured physical image.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for reading the physical witnesses.
        /// </param>
        /// <returns>
        /// Validation issues for changed inputs, or an empty list when the original origin remains current.
        /// </returns>
        internal async Task<IReadOnlyList<ValidationIssue>> CheckRetainedInputsAsync(
            FileSystemManager.CanonicalWriteLease lease) =>
            await CheckRetainedInputsCoreAsync(lease, null, null);

        /// <summary>
        /// Rechecks the original witnesses after C2 has replaced only its two private roots.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease retained through the new-generation read-back.
        /// </param>
        /// <param name="checkpointBytes">
        /// Exact newly confirmed checkpoint bytes replacing the retained old-root expectation.
        /// </param>
        /// <param name="pendingBytes">
        /// Exact newly confirmed pending bytes replacing the retained old-root expectation.
        /// </param>
        /// <returns>
        /// Origin freshness issues without treating the authorized private-root advance as drift.
        /// </returns>
        private async Task<IReadOnlyList<ValidationIssue>> CheckRetainedInputsAfterC2TransportAsync(
            FileSystemManager.CanonicalWriteLease lease, byte[] checkpointBytes, byte[] pendingBytes) =>
            await CheckRetainedInputsCoreAsync(lease, checkpointBytes, pendingBytes);

        /// <summary>
        /// Checks retained original witnesses with an optional exact C2 private-root successor pair.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for all physical reads.
        /// </param>
        /// <param name="checkpointBytes">
        /// Newly confirmed checkpoint bytes, or <see langword="null"/> for the original expectation.
        /// </param>
        /// <param name="pendingBytes">
        /// Newly confirmed pending bytes, or <see langword="null"/> for the original expectation.
        /// </param>
        /// <returns>
        /// Validation issues if an origin witness no longer has its expected exact image.
        /// </returns>
        private async Task<IReadOnlyList<ValidationIssue>> CheckRetainedInputsCoreAsync(
            FileSystemManager.CanonicalWriteLease lease, byte[]? checkpointBytes, byte[]? pendingBytes)
        {
            EnsureCurrent(lease);
            if ((checkpointBytes is null) != (pendingBytes is null))
                throw new InvalidOperationException("C2 transport needs both private-root successor bytes.");
            CanonicalBeforeImage ExpectedImage(string path, CanonicalBeforeImage original) =>
                checkpointBytes is not null && path == SpiritualWoundCaptureCheckpointState.StatePath
                    ? new CanonicalBeforeImage(true, checkpointBytes)
                    : pendingBytes is not null && path == SpiritualWoundDecisionPendingState.StatePath
                        ? new CanonicalBeforeImage(true, pendingBytes)
                        : original;
            foreach (var pair in _physicalWitnesses)
            {
                var bytes = await _validator._fs.ReadFileBytesAsync(lease, pair.Key);
                if (ExpectedImage(pair.Key, pair.Value).Fingerprint ==
                    new CanonicalBeforeImage(bytes != null, bytes).Fingerprint)
                    continue;
                RevokeUnderLease(lease);
                return new[] { SourceIssue(pair.Key, "spiritual_original_input_changed",
                    "the original observed physical control input") };
            }
            foreach (var pair in _observed)
            {
                // Source owner checks the conflict's retained prefix and permitted
                // missing fields. The common owner does not infer those permissions.
                if (pair.Key == AfterlifeSpiritualConflictState.StatePath &&
                    (!_usesOriginalPrefix || _originalPrefix != null))
                    continue;
                var bytes = _usesColdOriginalInputs && SpiritualOriginalDraftInputs.IsDraftPath(pair.Key)
                    ? _draftInputs.ReadImage(pair.Key).Bytes
                    : await _validator._fs.ReadFileBytesAsync(lease, pair.Key);
                if (ExpectedImage(pair.Key, pair.Value).Fingerprint ==
                    new CanonicalBeforeImage(bytes != null, bytes).Fingerprint)
                    continue;
                RevokeUnderLease(lease);
                return new[] { SourceIssue(pair.Key, "spiritual_original_input_changed",
                    "the original observed accepted-turn input; live edits need their actual owned producer") };
            }
            return Array.Empty<ValidationIssue>();
        }

        /// <summary>
        /// Creates the original turn's resource and effect owners and binds them to this
        /// capture's exact spiritual source owner before any resource execution can begin.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease used to revalidate retained input and commit the
        /// prepared source continuation when the original prefix is not deferred.
        /// </param>
        /// <returns>
        /// An empty collection when the exact owner tuple is ready, or validation diagnostics
        /// when retained input or source preparation prevents execution.
        /// </returns>
        internal async Task<IReadOnlyList<ValidationIssue>> BeginResourceExecutionAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                EnsureCurrent(lease);
                if (_resources != null)
                    throw new InvalidOperationException("An original turn has one resource executor.");
                var issues = await CheckRetainedInputsAsync(lease);
                if (issues.Count != 0)
                    return issues;
                using var allocationScope = _allocations.Clock.BeginSpeculation();
                var source = _usesOriginalPrefix ? null : await _source.PrepareContinuationAsync(lease);
                if (source != null && source.Ticket == null)
                    return source.Issues;
                EnsureCurrent(lease);
                var terminalIssues = await PrepareTerminalExecutionAsync();
                if (terminalIssues.Count != 0)
                    return terminalIssues;
                var created = AcceptedMechanicsPlanner.BeginOriginalSpiritualResourceExecution(_input, _terminal);
                if (created.Session == null)
                    return created.Issues;
                var effectOwner = EffectAcceptedTurnPlanner.EffectAcceptedDraft.Begin(
                    _input.PlanningContext!.EffectPlan!,
                    _input.PlanningContext.EffectIdentityFactory ?? new EffectIdentityFactory());
                try
                {
                    created.Session.BindOriginalCompletionOwners(effectOwner, _source);
                }
                catch
                {
                    effectOwner.Dispose();
                    created.Session.Dispose();
                    throw;
                }
                _resources = created.Session;
                _effects = effectOwner;
                if (_usesOriginalPrefix)
                {
                    allocationScope.Commit();
                    return Array.Empty<ValidationIssue>();
                }
                EnsureCurrent(lease);
                var committed = _source.CommitPreparedContinuation(lease, source!.Ticket!);
                if (committed.Session == null)
                {
                    RevokeUnderLease(lease);
                    return committed.Issues;
                }
                allocationScope.Commit();
                return Array.Empty<ValidationIssue>();
            }
            catch
            {
                // A partially constructed executor cannot be retried as if it
                // were the untouched original resource/effect owner.
                Dispose();
                throw;
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Advances the next source-owned resource exchange after fresh retained-input validation.
        /// Pending wound integration blocks advancement; the returned boundary grants no common publication authority.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease used to revalidate and advance the retained turn.
        /// </param>
        /// <returns>
        /// The next resource boundary or issues preventing advancement.
        /// </returns>
        internal async Task<AcceptedMechanicsPlanner.ResourceContinuationResult>
            AdvanceNextResourceExchangeAsync(FileSystemManager.CanonicalWriteLease lease)
            => await AdvanceNextResourceExchangeCoreAsync(lease, null);

        /// <summary>
        /// Advances one cold source/resource exchange using only a capture-owned dependent conflict correction.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease owning this capture and its source session.
        /// </param>
        /// <param name="changes">
        /// Exact registered conflict image replacing the current detached layer for this step.
        /// </param>
        /// <returns>
        /// The actual accepted resource boundary or diagnostics; a failed preparation retains the prior layer.
        /// </returns>
        internal Task<AcceptedMechanicsPlanner.ResourceContinuationResult>
            AdvanceNextColdResourceExchangeAsync(FileSystemManager.CanonicalWriteLease lease,
                IReadOnlyDictionary<string, CanonicalBeforeImage> changes)
        {
            ArgumentNullException.ThrowIfNull(changes);
            return AdvanceNextResourceExchangeCoreAsync(lease, changes);
        }

        /// <summary>
        /// Advances the retained resource owner with an optional capture-owned cold correction.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease for the resource boundary.
        /// </param>
        /// <param name="coldChanges">
        /// One detached dependent conflict image, or <see langword="null"/> for ordinary advancement.
        /// </param>
        /// <returns>
        /// The accepted step or rejection diagnostics without accepting an invalid proposed layer.
        /// </returns>
        private async Task<AcceptedMechanicsPlanner.ResourceContinuationResult>
            AdvanceNextResourceExchangeCoreAsync(FileSystemManager.CanonicalWriteLease lease,
                IReadOnlyDictionary<string, CanonicalBeforeImage>? coldChanges)
        {
            await _gate.WaitAsync();
            try { return await AdvanceNextResourceExchangeUnderGateAsync(lease, coldChanges); }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Runs the existing resource continuation while the capture gate is already held.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for the retained owner transaction.
        /// </param>
        /// <param name="coldChanges">
        /// One dependent conflict correction, or <see langword="null"/> for an unchanged source.
        /// </param>
        /// <returns>
        /// The owned resource boundary and diagnostics without publishing canonical state.
        /// </returns>
        private async Task<AcceptedMechanicsPlanner.ResourceContinuationResult>
            AdvanceNextResourceExchangeUnderGateAsync(FileSystemManager.CanonicalWriteLease lease,
                IReadOnlyDictionary<string, CanonicalBeforeImage>? coldChanges)
        {
            var advanced = false;
            try
            {
                EnsureCurrent(lease);
                if (_effects is { HasPendingWoundIntegration: true })
                    return ResourceFailure("spiritual_wound_routing_required", "register the actual inserted wound before advancing resources");
                if (_resources == null)
                    throw new InvalidOperationException("Begin the original resource executor first.");
                if (_lastResourceStep is { PendingResource: not null } or { PendingExchange: not null })
                    return ResourceFailure("spiritual_resource_wait_unresolved", "resume the exact owned wait first");
                var issues = await CheckRetainedInputsAsync(lease);
                if (issues.Count != 0)
                    return new(null, issues);
                if (coldChanges != null && (!_usesColdOriginalInputs || _nextResourceOrdinal == 0 ||
                    _coldCurrentInputs is null || _coldProposedInputs != null ||
                    _coldViewRevision == long.MaxValue || coldChanges.Count != 1 ||
                    !coldChanges.TryGetValue(AfterlifeSpiritualConflictState.StatePath, out var conflictImage) ||
                    conflictImage is null || !conflictImage.Existed))
                    return ResourceFailure("spiritual_cold_continuation_view_invalid",
                        "one present dependent conflict image after an admitted exchange");
                if (_usesOriginalPrefix && _originalPrefix == null)
                {
                    using var prefixAllocations = _allocations.Clock.BeginSpeculation();
                    advanced = true;
                    var prefixResult = await AcceptOriginalPrefixStepAsync(lease,
                        _resources.AdvanceOriginalPrefix(), null);
                    if (prefixResult.Step != null) prefixAllocations.Commit();
                    if (prefixResult.Step?.OriginalPrefix == null)
                        return prefixResult;
                }
                using var allocationScope = _allocations.Clock.BeginSpeculation();
                if (coldChanges != null)
                {
                    try
                    {
                        var proposed = _coldCurrentInputs!.WithImageChanges(coldChanges);
                        if (!_source.MatchesColdOriginalExchangeInventory(_draftInputs, proposed))
                            return ResourceFailure("spiritual_cold_continuation_inventory_changed",
                                "the same original exchange inventory");
                        _coldProposedInputs = proposed;
                    }
                    catch (Exception error) when (error is ArgumentException or InvalidOperationException or
                        System.Text.Json.JsonException or FormatException)
                    {
                        return ResourceFailure("spiritual_cold_continuation_view_invalid", error.Message);
                    }
                }
                var prepared = _usesOriginalPrefix
                    ? await SpiritualWoundSourceSession.PreparedContinuation.PrepareAsync(_source, lease, this)
                    : await _source.PrepareContinuationAsync(lease);
                if (prepared.Ticket == null)
                {
                    if (_usesOriginalPrefix && _nextResourceOrdinal == 0)
                        RevokeUnderLease(lease);
                    return new(null, prepared.Issues);
                }
                var context = _input.PlanningContext!;
                var batches = _source.BuildPreparedResourceBatches(lease, prepared.Ticket,
                    _terminal?.ExecutionOwners ?? context.Owners, SourceResourceBaseline);
                if (!batches.IsValid)
                    return new(null, batches.Issues);
                var batch = batches.Exchanges.SingleOrDefault(value => value.Ordinal == _nextResourceOrdinal);
                if (batch == null)
                    return ResourceFailure("spiritual_resource_exchange_missing", "the next captured source exchange");
                EnsureCurrent(lease);
                advanced = true;
                _resources.StageNextExchange(batch);
                var step = _resources.AdvanceThroughExchange();
                if (step.Result is { IsValid: false } failed)
                {
                    RevokeUnderLease(lease);
                    return new(null, failed.Issues);
                }
                var committed = _source.CommitPreparedContinuation(lease, prepared.Ticket);
                if (committed.Session == null)
                {
                    RevokeUnderLease(lease);
                    return new(null, committed.Issues);
                }
                if (_coldProposedInputs is { } acceptedInputs)
                {
                    _coldCurrentInputs = acceptedInputs;
                    _coldViewRevision++;
                }
                _lastResourceStep = step;
                if (step.Interval != null)
                {
                    if (!_resources.Owns(step.Interval))
                        throw new InvalidOperationException("Closed interval must belong to the retained executor.");
                    RetainClosedExchangeEvidence(step.Interval);
                    _nextResourceOrdinal++;
                }
                allocationScope.Commit();
                return new(step, Array.Empty<ValidationIssue>());
            }
            catch
            {
                if (advanced)
                    Dispose();
                throw;
            }
            finally
            {
                _coldProposedInputs = null;
            }
        }

        private static AcceptedMechanicsPlanner.ResourceContinuationResult ResourceFailure(
            string code, string detail) => new(null, new[] {
                SourceIssue(AfterlifeSpiritualConflictState.StatePath, code, detail) });

        private void RevokeUnderLease(FileSystemManager.CanonicalWriteLease lease)
        {
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            if (EffectAcceptedTurnPlanAuthority.TryPeekValidated(_validator._fs, lease,
                    out var current) && ReferenceEquals(current.Plan, _input.PlanningContext!.EffectPlan))
                EffectAcceptedTurnPlanAuthority.InvalidateValidated(_validator._fs, lease);
            Dispose();
        }

        /// <summary>
        /// Revokes the allocation journal and unpublished execution owners; repeated disposal is harmless.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _allocations.Journal.Invalidate();
            _resources?.Dispose();
            _effects?.Dispose();
            _source.Revoke();
            if (ReferenceEquals(_validator._spiritualOriginalTurnCapture, this))
                _validator._spiritualOriginalTurnCapture = null;
            if (ReferenceEquals(_validator._spiritualWoundSourceSession, _source))
                _validator._spiritualWoundSourceSession = null;
        }
    }
}
