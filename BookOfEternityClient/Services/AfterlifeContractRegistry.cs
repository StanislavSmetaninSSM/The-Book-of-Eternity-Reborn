namespace BookOfEternityClient.Services;

public sealed record AfterlifeContractSurface(
    string Path,
    string Owner,
    string Realm,
    string Authority,
    bool IsKnownClientOwnedSurface);

public static class AfterlifeContractRegistry
{
    private static readonly AfterlifeContractSurface[] Surfaces =
    {
        new(AfterlifeSpiritualConflictState.StatePath, "afterlife_spiritual_conflict", "Chaos Sea|Shining Abode", "GM-authored through afterlifeSpiritualConflictUpdate", false),
        new(AfterlifeEntityProfileState.StatePath, "afterlife_entity_profiles", "Chaos Sea|Shining Abode", "GM-authored profile/update surfaces plus client-owned local player upgrade paths", false),
        new(SarefMainStoryState.StatePath, "saref_main_story", "Chaos Sea|Shining Abode", "GM-authored hidden main-story state; absent legacy file is valid", false),
        new(GuardianAbodeOfferingState.PendingRequestPath, "guardian_abode_offering", "Chaos Sea", "client-owned pending request", true),
        new(GuardianTradeRequestState.PendingRequestPath, "guardian_trade", "Chaos Sea", "client-owned pending request", true),
        new(PlayerGuardianFoundationState.PendingRequestPath, "player_guardian_foundation", "Chaos Sea", "client-owned pending request", true),
        new(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, "guardian_abode_residents", "Chaos Sea", "client-owned pending request", true),
        new(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, "guardian_abode_resident_interactions", "Chaos Sea", "client-owned pending request", true),
        new(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, "guardian_abode_resident_transfers", "Chaos Sea", "client-owned pending request", true),
        new(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, "resident_companion_manifestation", "MortalWorldProfile handoff with afterlife validation awareness", "client-owned manifestation handoff request", true),
        new(ActorSocialInteractionRequestState.PendingGuardianRequestPath, "guardian_social_interactions", "Chaos Sea", "client-owned pending request", true),
        new(ActorSocialInteractionRequestState.PendingNpcRequestPath, "mortal_npc_social_interactions", "Mortal World; afterlife blocker/preservation semantics", "client-owned Mortal pending request", true),
        new(NpcTradeRequestState.PendingRequestPath, "mortal_npc_trade_inventory", "Mortal World; afterlife blocker/preservation semantics", "client-owned Mortal pending request", true),
        new(AfterlifeArchiveActionState.ConsultationRequestPath, "afterlife_archive_consultation", "Chaos Sea", "client-owned request", true),
        new(AfterlifeArchiveActionState.ProjectFuelRequestPath, "afterlife_archive_project_fuel", "Chaos Sea", "client-owned request", true),
        new(SystemGuardianLibraryService.AttractionRequestPath, "system_guardian_library", "Chaos Sea", "client-owned system Guardian attraction request", true),
        new(AfterlifeReturnGuardService.GuardPath, "afterlife_return_guard", "Chaos Sea", "client-owned guard after mortal-life return", true),
        new(ProgressionScheduleService.SchedulePath, "afterlife_scheduler", "Chaos Sea|Shining Abode", "client-owned scheduler baseline", true),
        new(ProgressionScheduleService.ReportPath, "afterlife_scheduler", "Chaos Sea|Shining Abode", "GM-authored progressionProcessingReport", false),
        new(ShiningCoreActionRequestState.PendingActionsRequestPath, "shining_core_actions", "Shining Abode", "client-owned request queue", true),
        new(ShiningTradeRequestState.PendingRequestsPath, "shining_trade", "Shining Abode", "client-owned request", true),
        new(ShiningFactionRequestState.PendingFoundingsRequestPath, "shining_faction_foundings", "Shining Abode", "client-owned request", true),
        new(ShiningFactionRequestState.PendingRealignmentsRequestPath, "shining_faction_realignments", "Shining Abode", "client-owned request", true),
        new(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, "shining_faction_leadership", "Shining Abode", "client-owned request", true),
        new(SourceOfLightCapstoneState.PendingRequestPath, "source_of_light_capstone", "Shining Abode", "client-owned capstone request", true),
        new("game_state/control/incarnation_trigger.json", "incarnation_trigger", "Chaos Sea|Shining Abode pending-bootstrap handoff", "GM-authored TriggerIncarnation", false),
        new("game_state/control/ascension.json", "ascension_trigger", "Mortal World to Chaos Sea/afterlife transition", "GM-authored AscensionTrigger/playerChoice", false),
        new("game_state/control/life_transitions.json", "life_transition", "Mortal World / Life Evaluation / afterlife return setup", "GM-authored TriggerLifeEnd", false),
        new(WorldDirectiveService.PendingSetupPath, "incarnation_world_setup", "Afterlife local incarnation prep", "client-owned setup prompt/prep file", true),
        new(ScenarioCoreService.ManifestPath, "next_life_scenario_core", "Afterlife local incarnation prep", "client-owned next-life scenario manifest", true),
        new(AfterlifeArchiveCandidateService.ManifestPath, "life_evaluation_archive_candidate", "Life Evaluation / Chaos Sea transition", "client-owned archive candidate manifest", true),
        new(AfterlifeNotificationState.NotificationsPath, "afterlife_notifications", "Chaos Sea|Shining Abode", "client-derived notifications; GM must not write", false)
    };

    public static IReadOnlyList<AfterlifeContractSurface> All => Surfaces;

    public static bool IsKnownClientOwnedSurface(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return Surfaces.Any(surface =>
            surface.IsKnownClientOwnedSurface &&
            string.Equals(surface.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    public static AfterlifeContractSurface? Find(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return Surfaces.FirstOrDefault(surface =>
            string.Equals(surface.Path, path, StringComparison.OrdinalIgnoreCase));
    }
}
