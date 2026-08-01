using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private readonly AsyncLocal<GameStateValidationSelection?> _activeGameStateValidationSelection = new();

    private async Task<List<ValidationIssue>> ValidateGameStateInternalAsync(
        GameStateValidationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var previousSelection = _activeGameStateValidationSelection.Value;
        var previousGuardianPolicyContext =
            _gameStateValidationGuardianPolicyContextCache.Value;
        var previousGuardianProjectTrackerPolicyContext =
            _gameStateValidationGuardianProjectTrackerPolicyContextCache.Value;
        _activeGameStateValidationSelection.Value = selection;
        _gameStateValidationGuardianPolicyContextCache.Value = null;
        _gameStateValidationGuardianProjectTrackerPolicyContextCache.Value = null;

        try
        {
            _knownCanonicalFactionIdsCache = null;
            _knownCanonicalFactionNamesCache = null;

            var issues = new List<ValidationIssue>();
            var phases = selection.Phases;

            await RunCoreValidationPhasesAsync(phases, issues);
            await RunActorAndStateValidationPhasesAsync(phases, issues);
            await RunLifecycleValidationPhasesAsync(phases, issues);

            LogValidationErrors(issues);
            return issues;
        }
        finally
        {
            _gameStateValidationGuardianPolicyContextCache.Value =
                previousGuardianPolicyContext;
            _gameStateValidationGuardianProjectTrackerPolicyContextCache.Value =
                previousGuardianProjectTrackerPolicyContext;
            _activeGameStateValidationSelection.Value = previousSelection;
        }
    }

    private bool IsGameStateValidationInProgress
        => _activeGameStateValidationSelection.Value != null;

    private bool ShouldValidateStateFile(string relativePath)
        => _activeGameStateValidationSelection.Value?.IncludesStateFile(relativePath) ?? true;

    private async Task RunCoreValidationPhasesAsync(
        GameStateValidationPhase phases,
        List<ValidationIssue> issues)
    {
        if (phases.Includes(GameStateValidationPhase.JsonIntegrity))
            await ValidateJsonIntegrity(issues);
        if (phases.Includes(GameStateValidationPhase.RequiredFiles))
            ValidateRequiredFiles(issues);
        if (phases.Includes(GameStateValidationPhase.RequiredFields))
            await ValidateRequiredFields(issues);
        if (phases.Includes(GameStateValidationPhase.LoreBootstrapRequiredFiles))
            await ValidateLoreBootstrapRequiredFilesAsync(issues);
        if (phases.Includes(GameStateValidationPhase.MortalBootstrapPlayerVisibleNames))
            await ValidateMortalBootstrapPlayerVisibleNamesAsync(issues);
        if (phases.Includes(GameStateValidationPhase.MortalBootstrapContentAnchors))
            await ValidateMortalBootstrapContentAnchorsAsync(issues);
        if (phases.Includes(GameStateValidationPhase.CrossReferences))
            await ValidateCrossReferences(issues);
        if (phases.Includes(GameStateValidationPhase.RivalAndResidentCrossReferences) &&
            !phases.Includes(GameStateValidationPhase.CrossReferences))
            await ValidateRivalAndResidentCrossReferencesAsync(issues);
        if (phases.Includes(GameStateValidationPhase.SoulStateConsistency))
            await ValidateSoulStateConsistency(issues);
    }

    private async Task RunActorAndStateValidationPhasesAsync(
        GameStateValidationPhase phases,
        List<ValidationIssue> issues)
    {
        if (phases.Includes(GameStateValidationPhase.PlayerStateFiles))
            await ValidatePlayerStateFiles(issues);
        if (phases.Includes(GameStateValidationPhase.NpcStateFiles))
            await ValidateNpcStateFiles(issues);
        if (phases.Includes(GameStateValidationPhase.SkillContractConsistency))
            await ValidateSkillContractConsistencyAsync(issues);
        if (phases.Includes(GameStateValidationPhase.TrainingShowcases))
            await ValidateTrainingShowcasesAsync(issues);
        if (phases.Includes(GameStateValidationPhase.WorldQuestCombatFactionStateFiles))
            await ValidateWorldQuestCombatFactionStateFiles(issues);
        if (phases.Includes(GameStateValidationPhase.MetaMiscStateFiles))
            await ValidateMetaMiscStateFiles(issues);
        else if (phases.Includes(GameStateValidationPhase.GuardianProjectStateFiles))
            await ValidateGuardianProjectStateFilesAsync(issues);
        if (phases.Includes(GameStateValidationPhase.AcceptedTurnActorMaterializationCompleteness))
            await ValidateAcceptedTurnActorMaterializationCompletenessAsync(issues);
        if (phases.Includes(GameStateValidationPhase.AfterlifeSpiritualConflictState))
            await ValidateAfterlifeSpiritualConflictStateAsync(issues);
        if (phases.Includes(GameStateValidationPhase.SourceOfLightCapstoneGlobalState))
            await ValidateSourceOfLightCapstoneGlobalStateAsync(issues);
        if (phases.Includes(GameStateValidationPhase.ShiningLeadershipHeadReferences))
            await ValidateShiningLeadershipHeadReferencesAsync(issues);
    }

    private async Task RunLifecycleValidationPhasesAsync(
        GameStateValidationPhase phases,
        List<ValidationIssue> issues)
    {
        if (phases.Includes(GameStateValidationPhase.LifeEvaluationRewardCycle))
            await ValidateLifeEvaluationRewardCycleAsync(issues);
        if (phases.Includes(GameStateValidationPhase.NoLifeEvaluationRewardsOnTriggerTurn))
            await ValidateNoLifeEvaluationRewardsOnTriggerTurnAsync(issues);
        if (phases.Includes(GameStateValidationPhase.GuardianResonancePowerEvents))
            await ValidateGuardianResonancePowerEventsAsync(issues);
        if (phases.Includes(GameStateValidationPhase.ShiningTreasuryClientOwnedState))
            await ValidateShiningTreasuryClientOwnedStateAsync(issues);
        if (phases.Includes(GameStateValidationPhase.AfterlifeActiveThreatPreTurnContinuity))
            await ValidateAfterlifeActiveThreatPreTurnContinuityAsync(issues);
        if (phases.Includes(GameStateValidationPhase.AfterlifeGlobalFlagPreTurnContinuity))
            await ValidateAfterlifeGlobalFlagPreTurnContinuityAsync(issues);
        if (phases.Includes(GameStateValidationPhase.ClientOwnedControlFiles))
            await ValidateClientOwnedControlFilesAsync(issues);
        if (phases.Includes(GameStateValidationPhase.RealmSegregation))
            await ValidateRealmSegregationAsync(issues);
    }

    private void LogValidationErrors(List<ValidationIssue> issues)
    {
        foreach (var issue in issues.Where(i => i.Severity == IssueSeverity.Error))
            _logger.LogWarning("Ошибка валидации: [{File}] {Message}", issue.FilePath, issue.Message);
    }
}
