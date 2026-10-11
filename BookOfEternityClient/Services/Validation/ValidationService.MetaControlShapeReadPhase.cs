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

    // Preparatory scalar wrapper: no Meta reservation, physical lease or result reuse yet.
    private Task RunMetaControlShapeReadPhaseAsync(List<ValidationIssue> issues) =>
        ValidateMetaControlShapeFilesCoreAsync(issues);

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
