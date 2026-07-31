namespace BookOfEternityClient.Services;

[Flags]
internal enum GameStateValidationPhase : uint
{
    None = 0,
    JsonIntegrity = 1u << 0,
    RequiredFiles = 1u << 1,
    RequiredFields = 1u << 2,
    LoreBootstrapRequiredFiles = 1u << 3,
    MortalBootstrapPlayerVisibleNames = 1u << 4,
    MortalBootstrapContentAnchors = 1u << 5,
    CrossReferences = 1u << 6,
    SoulStateConsistency = 1u << 7,
    PlayerStateFiles = 1u << 8,
    NpcStateFiles = 1u << 9,
    SkillContractConsistency = 1u << 10,
    TrainingShowcases = 1u << 11,
    WorldQuestCombatFactionStateFiles = 1u << 12,
    MetaMiscStateFiles = 1u << 13,
    AcceptedTurnActorMaterializationCompleteness = 1u << 14,
    AfterlifeSpiritualConflictState = 1u << 15,
    SourceOfLightCapstoneGlobalState = 1u << 16,
    ShiningLeadershipHeadReferences = 1u << 17,
    LifeEvaluationRewardCycle = 1u << 18,
    NoLifeEvaluationRewardsOnTriggerTurn = 1u << 19,
    GuardianResonancePowerEvents = 1u << 20,
    ShiningTreasuryClientOwnedState = 1u << 21,
    AfterlifeActiveThreatPreTurnContinuity = 1u << 22,
    AfterlifeGlobalFlagPreTurnContinuity = 1u << 23,
    ClientOwnedControlFiles = 1u << 24,
    RealmSegregation = 1u << 25,
    RivalAndResidentCrossReferences = 1u << 26,
    GuardianProjectStateFiles = 1u << 27,
    All = (1u << 26) - 1,
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
