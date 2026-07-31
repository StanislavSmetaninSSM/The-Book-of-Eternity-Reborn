using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static class GuardianValidationProfiles
{
    public static readonly GameStateValidationSelection AcceptedAuthority = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.AcceptedTurnActorMaterializationCompleteness |
        GameStateValidationPhase.GuardianResonancePowerEvents |
        GameStateValidationPhase.ClientOwnedControlFiles,
        "game_state/control/incarnation_trigger.json",
        "game_state/meta/guardians.json",
        "game_state/meta/soul_state.json",
        "game_state/npcs/npc_core.json",
        GuardianProjectState.TrackerPath);

    public static readonly GameStateValidationSelection SystemGuardianAttraction = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles,
        SystemGuardianLibraryService.AttractionRequestPath,
        "game_state/meta/guardians.json",
        "game_state/meta/soul_state.json");

    public static readonly GameStateValidationSelection IdleValidation = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.MetaMiscStateFiles,
        "game_state/meta/soul_state.json",
        "game_state/npcs/npc_core.json",
        "game_state/npcs/npc_inventory.json");

    public static readonly GameStateValidationSelection LifecycleSnapshots = Select(
        GameStateValidationPhase.LoreBootstrapRequiredFiles |
        GameStateValidationPhase.MortalBootstrapContentAnchors |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.LifeEvaluationRewardCycle |
        GameStateValidationPhase.NoLifeEvaluationRewardsOnTriggerTurn |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation,
        "game_state/control/ascension.json",
        "game_state/control/life_transitions.json",
        "game_state/meta/soul_state.json",
        "game_state/quests/regular_quests.json");

    public static readonly GameStateValidationSelection PowerJournalOfferings = Select(
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents |
        GameStateValidationPhase.ClientOwnedControlFiles,
        "game_state/meta/abode_power_journal.json",
        "game_state/meta/guardians.json",
        "game_state/meta/soul_state.json",
        GuardianProjectState.TrackerPath);

    public static readonly GameStateValidationSelection ProjectsPower = Select(
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents |
        GameStateValidationPhase.ClientOwnedControlFiles,
        "game_state/meta/guardians.json",
        "game_state/meta/abode_power_journal.json",
        "game_state/quests/soul_quests.json",
        GuardianProjectState.TrackerPath,
        GuardianProjectState.JournalPath);

    public static readonly GameStateValidationSelection ProjectsPowerState = Select(
        GameStateValidationPhase.GuardianProjectStateFiles,
        GuardianProjectState.TrackerPath);

    public static readonly GameStateValidationSelection ProjectsPowerEvents = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents,
        "game_state/meta/guardians.json",
        "game_state/meta/abode_power_journal.json",
        GuardianProjectState.TrackerPath,
        GuardianProjectState.JournalPath);

    public static readonly GameStateValidationSelection ProjectsPowerAuthority = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.MetaMiscStateFiles,
        "game_state/meta/guardians.json",
        "game_state/quests/soul_quests.json",
        GuardianProjectState.TrackerPath,
        GuardianProjectState.JournalPath);

    public static readonly GameStateValidationSelection QuestProgress = Select(
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.RealmSegregation,
        "game_state/meta/guardians.json",
        "game_state/meta/soul_state.json",
        "game_state/quests/soul_quests.json");

    public static readonly GameStateValidationSelection RivalResidents = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.NpcStateFiles |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles,
        "game_state/inventory/items.json",
        "game_state/meta/guardians.json",
        "game_state/meta/soul_state.json",
        "game_state/npcs/npc_core.json",
        "game_state/quests/soul_quests.json",
        "game_state/world/world_events.json",
        "lore/codex_entries.json");

    public static readonly GameStateValidationSelection RivalResidentsCrossReferences =
        Select(GameStateValidationPhase.RivalAndResidentCrossReferences);

    public static readonly GameStateValidationSelection RivalResidentsNpcContract = Select(
        GameStateValidationPhase.RivalAndResidentCrossReferences |
        GameStateValidationPhase.NpcStateFiles,
        "game_state/npcs/npc_core.json");

    public static readonly GameStateValidationSelection TradeOfferingResonance = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents |
        GameStateValidationPhase.ClientOwnedControlFiles,
        "game_state/control/incarnation_trigger.json",
        "game_state/meta/abode_power_journal.json",
        "game_state/meta/guardian_abode_power_journal.json",
        "game_state/meta/guardians.json",
        "game_state/meta/soul_state.json");

    private static GameStateValidationSelection Select(
        GameStateValidationPhase phases,
        params string[] stateFiles)
        => stateFiles.Length == 0
            ? new GameStateValidationSelection(phases)
            : new GameStateValidationSelection(phases, stateFiles);
}

internal static class GuardianReasoningProfiles
{
    public static readonly AcceptedTurnReasoningValidationSelection Core = new(
        AcceptedTurnReasoningValidationScope.Core);

    public static readonly AcceptedTurnReasoningValidationSelection Guardian = new(
        AcceptedTurnReasoningValidationScope.Core |
        AcceptedTurnReasoningValidationScope.GuardianMusing);

    public static readonly AcceptedTurnReasoningValidationSelection MortalNpc = new(
        AcceptedTurnReasoningValidationScope.Core |
        AcceptedTurnReasoningValidationScope.MortalNpcJournal);

    public static readonly AcceptedTurnReasoningValidationSelection AfterlifeResident = new(
        AcceptedTurnReasoningValidationScope.Core |
        AcceptedTurnReasoningValidationScope.AfterlifeResidentJournal);

    public static readonly AcceptedTurnReasoningValidationSelection AfterlifeEntity = new(
        AcceptedTurnReasoningValidationScope.Core |
        AcceptedTurnReasoningValidationScope.AfterlifeEntityLedger |
        AcceptedTurnReasoningValidationScope.AfterlifeMemoryOwner);

    public static readonly AcceptedTurnReasoningValidationSelection ShiningFaction = new(
        AcceptedTurnReasoningValidationScope.Core |
        AcceptedTurnReasoningValidationScope.ShiningFactionMemory);
}
