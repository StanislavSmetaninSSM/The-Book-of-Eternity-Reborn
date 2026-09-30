namespace BookOfEternityClient.Services;

[Flags]
internal enum GameStateValidationPhase : ulong
{
    None = 0,
    JsonIntegrity = 1UL << 0,
    RequiredFiles = 1UL << 1,
    RequiredFields = 1UL << 2,
    LoreBootstrapRequiredFiles = 1UL << 3,
    MortalBootstrapPlayerVisibleNames = 1UL << 4,
    MortalBootstrapContentAnchors = 1UL << 5,
    CrossReferences = 1UL << 6,
    SoulStateConsistency = 1UL << 7,
    PlayerStateFiles = 1UL << 8,
    NpcStateFiles = 1UL << 9,
    SkillContractConsistency = 1UL << 10,
    TrainingShowcases = 1UL << 11,
    WorldQuestCombatFactionStateFiles = 1UL << 12,
    MetaMiscStateFiles = 1UL << 13,
    AcceptedTurnActorMaterializationCompleteness = 1UL << 14,
    AfterlifeSpiritualConflictState = 1UL << 15,
    SourceOfLightCapstoneGlobalState = 1UL << 16,
    ShiningLeadershipHeadReferences = 1UL << 17,
    LifeEvaluationRewardCycle = 1UL << 18,
    NoLifeEvaluationRewardsOnTriggerTurn = 1UL << 19,
    GuardianResonancePowerEvents = 1UL << 20,
    ShiningTreasuryClientOwnedState = 1UL << 21,
    AfterlifeActiveThreatPreTurnContinuity = 1UL << 22,
    AfterlifeGlobalFlagPreTurnContinuity = 1UL << 23,
    ClientOwnedControlFiles = 1UL << 24,
    RealmSegregation = 1UL << 25,
    RivalAndResidentCrossReferences = 1UL << 26,
    GuardianProjectStateFiles = 1UL << 27,
    AcceptedTurnFactionMaterializationCompleteness = 1UL << 28,
    AcceptedTurnItemMaterializationCompleteness = 1UL << 29,
    AcceptedTurnLocationMaterializationCompleteness = 1UL << 30,
    AcceptedTurnEffectMaterializationCompleteness = 1UL << 31,
    AcceptedTurnWoundMaterializationCompleteness = 1UL << 32,
    All = ((1UL << 26) - 1) |
          AcceptedTurnFactionMaterializationCompleteness |
          AcceptedTurnItemMaterializationCompleteness |
          AcceptedTurnLocationMaterializationCompleteness |
          AcceptedTurnEffectMaterializationCompleteness |
          AcceptedTurnWoundMaterializationCompleteness,
    Selectable = All | RivalAndResidentCrossReferences | GuardianProjectStateFiles
}

internal static class GameStateValidationPhaseRules
{
    public static void ThrowIfInvalid(
        GameStateValidationPhase phases,
        string paramName)
    {
        if (phases == GameStateValidationPhase.None ||
            (phases & ~GameStateValidationPhase.Selectable) != 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                phases,
                "Validation phase selection must contain only one or more defined phases.");
        }
    }

    public static bool Includes(
        this GameStateValidationPhase phases,
        GameStateValidationPhase phase)
    {
        return (phases & phase) != 0;
    }
}

internal sealed class GameStateValidationSelection
{
    private readonly HashSet<string>? _stateFiles;

    public GameStateValidationSelection(
        GameStateValidationPhase phases,
        IEnumerable<string>? stateFiles = null)
    {
        GameStateValidationPhaseRules.ThrowIfInvalid(phases, nameof(phases));
        Phases = phases;

        if (stateFiles == null)
            return;

        _stateFiles = stateFiles
            .Select(NormalizePath)
            .Where(path => path.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_stateFiles.Count == 0)
        {
            throw new ArgumentException(
                "State-file selection must contain at least one non-empty relative path.",
                nameof(stateFiles));
        }
    }

    public static GameStateValidationSelection All { get; } =
        new(GameStateValidationPhase.All);

    public GameStateValidationPhase Phases { get; }

    public bool IncludesStateFile(string relativePath)
    {
        return _stateFiles == null ||
               _stateFiles.Contains(NormalizePath(relativePath));
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Replace('\\', '/');
    }
}
