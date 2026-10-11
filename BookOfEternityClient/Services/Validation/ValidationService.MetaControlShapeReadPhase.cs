using System.Runtime.ExceptionServices;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Specific current-only shape closure. No manifest/signed/rollback path widening.
    private static readonly HashSet<string> MetaControlShapeCurrentPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "game_state/control/life_transitions.json",
        "game_state/control/incarnation_trigger.json",
        "game_state/control/ascension.json",
        "game_state/control/pending_shining_faction_foundings.json",
        "game_state/control/pending_shining_faction_realignments.json",
        "game_state/control/pending_shining_faction_leadership_transitions.json",
        "game_state/control/pending_npc_trade_inventory_requests.json",
        "game_state/control/pending_guardian_abode_residents_request.json",
        "game_state/control/pending_guardian_abode_resident_transfers.json",
        "game_state/control/pending_guardian_abode_resident_interactions.json",
        "game_state/control/pending_resident_companion_manifestation_request.json",
    };

#if DEBUG
    // Fixed component topology, same real synchronous issuer and production prefix/finally.
    // No ambient-token manufacture, mutable service flag or runtime variant.
    private Task<List<ValidationIssue>> ValidateMetaControlShapesComponentAsync(GameStateValidationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Phases != GameStateValidationPhase.MetaMiscStateFiles ||
            !selection.SelectsOnlyStateFiles(MetaControlShapeCurrentPaths))
            throw new ArgumentException("Meta shape component requires only its exact current-path selection.", nameof(selection));
        return StartAdmittedGameStateAsync(selection, metaControlShapesOnly: true);
    }
#endif

    private MetaControlShapeReadPhase? _metaControlShapeReadPhase;

    private async Task RunMetaControlShapeReadPhaseAsync(List<ValidationIssue> issues)
    {
        var invocation = _worldValidationInvocation.Value;
        if (invocation is not { MetaReserved: true })
        {
            RequireWorldCacheAccess();
            await ValidateMetaControlShapeFilesCoreAsync(issues);
            return;
        }
        MetaControlShapeReadPhase phase;
        lock (_worldReadGate)
        {
            if (!ReferenceEquals(_worldReservation, invocation) || invocation.Closed ||
                _worldReadPhase != null || _metaControlShapeReadPhase != null || _worldOverrideMutations != 0)
                throw new InvalidOperationException("Meta shape validation cannot enter its read phase.");
            phase = new MetaControlShapeReadPhase(this, _prevalidatedPendingTurnSnapshotOverride!);
            _metaControlShapeReadPhase = phase;
        }
        FileSystemManager.WorldValidationReadScope? physical = null;
        Exception? primary = null;
        var localIssues = new List<ValidationIssue>();
        try
        {
            // Reuse the existing exact original-owner/access/physical lease issuer.
            // No authority PASS cache or signed/draft read widening.
            physical = _fs.BeginWorldValidationRead();
            if (physical == null)
            {
                lock (_worldReadGate) _metaControlShapeReadPhase = null;
                // Genuine held/closing eligibility keeps the whole invocation reservation.
                await ValidateMetaControlShapeFilesCoreAsync(issues);
                return;
            }
            phase.Physical = physical;
            await physical.OpenAsync();
            phase.Active = true;
            phase.RequireCurrent();
            // Pure callback closure: no cache/context isolation or reset here.
            await ValidateMetaControlShapeFilesCoreAsync(localIssues);
            phase.RequireCurrent();
            physical.RequireFullEndProof();
            await phase.RequireUnchangedReadsAsync();
            phase.RequireCurrent();
        }
        catch (Exception error) { primary = error; }
        finally
        {
            phase.Active = false;
            try { if (physical != null) await physical.DisposeAsync(); }
            catch (Exception close)
            {
                if (primary == null) primary = close;
                else try { primary.Data["MetaControlShapes.CloseFailure"] = close; } catch { }
            }
            finally { lock (_worldReadGate) _metaControlShapeReadPhase = null; }
        }
        if (primary != null) ExceptionDispatchInfo.Capture(primary).Throw();
        physical!.RequireOwnerAfterClose();
        RequireWorldCacheAccess();
        if (phase.Invalidated || !ReferenceEquals(phase.Manifest, _prevalidatedPendingTurnSnapshotOverride))
            throw new IOException("Original Meta shape snapshot changed before issue publication.");
        issues.AddRange(localIssues);
    }

    private MetaControlShapeReadPhase? CurrentMetaControlShapeReadPhase()
    {
        RequireWorldCacheAccess();
        var phase = _metaControlShapeReadPhase;
        if (phase is not { Active: true }) return null;
        phase.RequireCurrent();
        return phase;
    }

    private sealed class MetaControlShapeReadPhase(ValidationService service,
        ValidationPendingTurnSnapshotManifest manifest)
    {
        internal readonly ValidationPendingTurnSnapshotManifest Manifest = manifest;
        internal FileSystemManager.WorldValidationReadScope Physical = null!;
        internal FileSystemManager.CanonicalWriteLease Lease => Physical.Lease!;
        internal bool Active, Invalidated;
        private readonly Dictionary<string, byte[]?> _reads = new(StringComparer.OrdinalIgnoreCase);
        internal void RequireCurrent()
        {
            service.RequireWorldCacheAccess();
            if (!Active || Invalidated || !ReferenceEquals(Manifest, service._prevalidatedPendingTurnSnapshotOverride))
                throw new IOException("Original Meta shape context changed.");
            Physical.RequireCurrent();
        }
        internal async Task<string?> ReadTextAsync(string path)
        {
            RequireCurrent();
            if (!MetaControlShapeCurrentPaths.Contains(path))
                throw new InvalidOperationException("Meta shape current read is outside its exact source closure.");
            // Original async snapshot/decode/AFTER-read hook, immediate first failure.
            var read = await service._fs.ReadWorldValidationTextAsync(Lease, path);
            RequireCurrent();
            if (_reads.TryGetValue(path, out var previous))
            {
                if (!Same(previous, read.Bytes)) throw new IOException("Meta shape consumed changed current bytes.");
            }
            else _reads.Add(path, read.Bytes?.ToArray());
            return read.Text;
        }
        private static bool Same(byte[]? first, byte[]? second) => first == null ? second == null :
            second != null && first.AsSpan().SequenceEqual(second);
        internal async Task RequireUnchangedReadsAsync()
        {
            foreach (var (path, bytes) in _reads)
            {
                RequireCurrent();
                var current = await service._fs.ReadFileBytesAsync(Lease, path);
                RequireCurrent();
                if (!Same(bytes, current)) throw new IOException("Meta shape input changed before issue publication.");
            }
        }
    }

    private async Task ValidateMetaControlShapeFilesCoreAsync(List<ValidationIssue> issues)
    {
        await ValidateFlexibleStateFile("game_state/control/life_transitions.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "reason", "summary", "_lastUpdated"
            }, issues, ValidateLifeTransitionControlFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/control/life_transitions.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "reason", "summary", "_lastUpdated"
            }, issues);
        await ValidateFlexibleStateFile("game_state/control/incarnation_trigger.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "worldDescription", "characterDescription", "circumstances", "source", "guardianId", "severityBand", "reason", "provocationSummary", "_lastUpdated"
            }, issues, ValidateIncarnationTriggerControlFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/control/incarnation_trigger.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "worldDescription", "characterDescription", "circumstances", "source", "guardianId", "severityBand", "reason", "provocationSummary", "_lastUpdated"
            }, issues);
        await ValidateFlexibleStateFile("game_state/control/ascension.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AscensionTrigger", "playerChoice", "_lastUpdated"
            }, issues, ValidateAscensionControlFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/control/ascension.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AscensionTrigger", "playerChoice", "_lastUpdated"
            }, issues);
        await ValidateFlexibleStateFile(ShiningFactionRequestState.PendingFoundingsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
            }, issues, ValidatePendingShiningFactionFoundingsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(ShiningFactionRequestState.PendingFoundingsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(ShiningFactionRequestState.PendingRealignmentsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
            }, issues, ValidatePendingShiningFactionRealignmentsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(ShiningFactionRequestState.PendingRealignmentsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
            }, issues, ValidatePendingShiningFactionLeadershipTransitionsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(NpcTradeRequestState.PendingRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NpcTradeRequestState.RequestsProperty,
                "requestId", "npcId", "npcName", "merchantProfile", "tradeCycleId", "derivedTradeSlotCount", "createdAtTurn", "createdAtUtc", "createdAtWorldDate", "refreshAfterWorldDate"
            }, issues, ValidatePendingNpcTradeInventoryRequestsFile);
        await ValidateStrictTopLevelObjectFileAsync(NpcTradeRequestState.PendingRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NpcTradeRequestState.RequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.ResidentsRequestsProperty,
                "requestId", "guardianId", "guardianName", "abodeId", "abodeName", "currentReputation", "createdAtTurn", "createdAtUtc"
            }, issues, ValidatePendingGuardianAbodeResidentsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.ResidentsRequestsProperty,
                "requestId", "guardianId", "guardianName", "abodeId", "abodeName", "currentReputation", "createdAtTurn", "createdAtUtc"
            }, issues);
        await ValidateFlexibleStateFile(GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.TransferRequestsProperty,
                "requestId", "residentId", "residentName",
                "sourceGuardianId", "sourceGuardianName", "sourceAbodeId", "sourceAbodeName",
                "targetGuardianId", "targetGuardianName", "targetAbodeId", "targetAbodeName",
                "abodeDevotionLevel", "abodeDevotionTier", "restlessness", "migrationState",
                "transferMode", "selectionMode", "competitionScore", "competitionLabel", "competitionReason",
                "createdAtTurn", "createdAtUtc"
            }, issues, ValidatePendingGuardianAbodeResidentTransfersRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.TransferRequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.InteractionRequestsProperty
            }, issues, ValidatePendingGuardianAbodeResidentInteractionsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.InteractionRequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.ManifestationRequestsProperty
            }, issues, ValidatePendingResidentCompanionManifestationRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.ManifestationRequestsProperty
            }, issues);
    }
}
