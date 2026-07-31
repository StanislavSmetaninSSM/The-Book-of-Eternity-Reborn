using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static class IntegrationValidationProfiles
{
    internal static readonly GameStateValidationSelection ActorMaterialization = Select(
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.NpcStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.AcceptedTurnActorMaterializationCompleteness |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection AfterlifeActiveThreat = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.AfterlifeActiveThreatPreTurnContinuity);

    internal static readonly GameStateValidationSelection AfterlifeArchive = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection AfterlifeChronicle = Select(
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection AfterlifeEntityProfile = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection AfterlifeGlobalFlag = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.AfterlifeGlobalFlagPreTurnContinuity);

    internal static readonly GameStateValidationSelection AfterlifeRealm = Select(
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection AfterlifeConflict = Select(
        GameStateValidationPhase.AfterlifeSpiritualConflictState);

    internal static readonly GameStateValidationSelection AfterlifeStory = Select(
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection CanonicalGuardian = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents);

    internal static readonly GameStateValidationSelection CanonicalInventory = Select(
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.PlayerStateFiles);

    internal static readonly GameStateValidationSelection CanonicalNpc = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.NpcStateFiles |
        GameStateValidationPhase.RivalAndResidentCrossReferences);

    internal static readonly GameStateValidationSelection CommandDisplaySave = Select(
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection FactionState = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles);

    internal static readonly GameStateValidationSelection GuardianArchiveTrade = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection GuardianPolicy = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection MechanicalBonus = Select(
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.PlayerStateFiles |
        GameStateValidationPhase.NpcStateFiles |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection MortalBootstrap = Select(
        GameStateValidationPhase.RequiredFiles |
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.LoreBootstrapRequiredFiles |
        GameStateValidationPhase.MortalBootstrapPlayerVisibleNames |
        GameStateValidationPhase.MortalBootstrapContentAnchors |
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.PlayerStateFiles |
        GameStateValidationPhase.NpcStateFiles |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles);

    internal static readonly GameStateValidationSelection NpcState = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.NpcStateFiles |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.AcceptedTurnActorMaterializationCompleteness |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RivalAndResidentCrossReferences);

    internal static readonly GameStateValidationSelection PlayerGuardian = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.GuardianResonancePowerEvents |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection QuestReward = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection ReadableDocument = Select(
        GameStateValidationPhase.JsonIntegrity |
        GameStateValidationPhase.RequiredFields |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection RealmSemantics = Select(
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection SarefStory = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection ShiningState = Select(
        GameStateValidationPhase.CrossReferences |
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ShiningLeadershipHeadReferences |
        GameStateValidationPhase.ShiningTreasuryClientOwnedState |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection SoulIdentity = Select(
        GameStateValidationPhase.SoulStateConsistency |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection SourceOfLight = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.SourceOfLightCapstoneGlobalState |
        GameStateValidationPhase.ClientOwnedControlFiles |
        GameStateValidationPhase.RealmSegregation);

    internal static readonly GameStateValidationSelection SystemGuardianLibrary = Select(
        GameStateValidationPhase.RequiredFiles |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection Training = Select(
        GameStateValidationPhase.PlayerStateFiles |
        GameStateValidationPhase.SkillContractConsistency |
        GameStateValidationPhase.TrainingShowcases |
        GameStateValidationPhase.MetaMiscStateFiles);

    internal static readonly GameStateValidationSelection Qte = Select(
        GameStateValidationPhase.MetaMiscStateFiles |
        GameStateValidationPhase.ClientOwnedControlFiles);

    internal static readonly GameStateValidationSelection Weather = Select(
        GameStateValidationPhase.WorldQuestCombatFactionStateFiles);

    private static GameStateValidationSelection Select(GameStateValidationPhase phases) =>
        new(phases);
}
