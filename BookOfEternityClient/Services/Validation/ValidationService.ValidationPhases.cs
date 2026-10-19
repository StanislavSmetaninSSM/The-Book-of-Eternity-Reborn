using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private readonly AsyncLocal<GameStateValidationSelection?> _activeGameStateValidationSelection = new();

    private async Task<List<ValidationIssue>> ValidateGameStateCoreAsync(
        GameStateValidationSelection selection, bool metaControlShapesOnly, StoryService.AcceptedStoryAppend? acceptedStoryAppend,
        StoryService.MortalJointStoryProof? jointStoryProof)
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
#if DEBUG
            using var diagnosticOverride = _fs.BrowserDiagnosticSnapshotOverride(_prevalidatedPendingTurnSnapshotOverride != null);
#endif
            _knownCanonicalFactionIdsCache = null;
            _knownCanonicalFactionNamesCache = null;

            var issues = new List<ValidationIssue>();
            var phases = selection.Phases;

            if (metaControlShapesOnly)
                await RunMetaControlShapeReadPhaseAsync(issues);
            else
            {
                await RunCoreValidationPhasesAsync(phases, issues);
                await RunActorAndStateValidationPhasesAsync(phases, issues);
                await RunLifecycleValidationPhasesAsync(phases, issues, acceptedStoryAppend, jointStoryProof);
            }

            if (_pendingSnapshotInvocation.Value == null) LogValidationErrors(issues);
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
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("JsonIntegrity", null);
#endif
            await ValidateJsonIntegrity(issues);
        }
        if (phases.Includes(GameStateValidationPhase.RequiredFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("RequiredFiles", null);
#endif
            ValidateRequiredFiles(issues);
        }
        if (phases.Includes(GameStateValidationPhase.RequiredFields))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("RequiredFields", null);
#endif
            await ValidateRequiredFields(issues);
        }
        if (phases.Includes(GameStateValidationPhase.LoreBootstrapRequiredFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("LoreBootstrapRequiredFiles", null);
#endif
            await ValidateLoreBootstrapRequiredFilesAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.MortalBootstrapPlayerVisibleNames))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("MortalBootstrapPlayerVisibleNames", null);
#endif
            await ValidateMortalBootstrapPlayerVisibleNamesAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.MortalBootstrapContentAnchors))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("MortalBootstrapContentAnchors", null);
#endif
            await ValidateMortalBootstrapContentAnchorsAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.CrossReferences))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("CrossReferences", null);
#endif
            await ValidateCrossReferences(issues);
        }
        if (phases.Includes(GameStateValidationPhase.RivalAndResidentCrossReferences) &&
            !phases.Includes(GameStateValidationPhase.CrossReferences))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("RivalAndResidentCrossReferences", null);
#endif
            await ValidateRivalAndResidentCrossReferencesAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.SoulStateConsistency))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("SoulStateConsistency", null);
#endif
            await ValidateSoulStateConsistency(issues);
        }
    }

    private async Task RunActorAndStateValidationPhasesAsync(
        GameStateValidationPhase phases,
        List<ValidationIssue> issues)
    {
        if (phases.Includes(GameStateValidationPhase.PlayerStateFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("PlayerStateFiles", null);
#endif
            await ValidatePlayerStateFiles(issues);
        }
        if (phases.Includes(GameStateValidationPhase.NpcStateFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("NpcStateFiles", null);
#endif
            await ValidateNpcStateFiles(issues);
        }
        if (phases.Includes(GameStateValidationPhase.SkillContractConsistency))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("SkillContractConsistency", null);
#endif
            await ValidateSkillContractConsistencyAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.TrainingShowcases))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("TrainingShowcases", null);
#endif
            await ValidateTrainingShowcasesAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.WorldQuestCombatFactionStateFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("WorldQuestCombatFactionStateFiles", null);
#endif
            await ValidateWorldQuestCombatFactionStateFiles(issues);
        }
        if (phases.Includes(GameStateValidationPhase.MetaMiscStateFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("MetaMiscStateFiles", null);
#endif
            await ValidateMetaMiscStateFiles(issues);
        }
        else if (phases.Includes(GameStateValidationPhase.GuardianProjectStateFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("GuardianProjectStateFiles", null);
#endif
            await ValidateGuardianProjectStateFilesAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.AcceptedTurnActorMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnActorMaterializationCompleteness", null);
#endif
            await ValidateAcceptedTurnActorMaterializationCompletenessAsync(issues);
        }
        if (phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnFactionMaterializationCompleteness", null);
#endif
            await ValidateAcceptedTurnFactionMaterializationCompletenessAsync(issues);
        }
        if (phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnLocationMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnLocationMaterializationCompleteness", null);
#endif
            await ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(issues);
        }
        if (phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnItemMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnItemMaterializationCompleteness", null);
#endif
            await ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(issues, null);
        }
        if (phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnEffectMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnEffectMaterializationCompleteness", null);
#endif
            await ValidateAcceptedTurnCanonicalEffectMaterializationAsync(issues, null);
        }
        if (phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnWoundMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnWoundMaterializationCompleteness", null);
#endif
            await ValidateAcceptedTurnCanonicalWoundMaterializationAsync(
                issues,
                writeLease: null);
        }
        if (phases.Includes(GameStateValidationPhase.AfterlifeSpiritualConflictState))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AfterlifeSpiritualConflictState", null);
#endif
            await ValidateAfterlifeSpiritualConflictStateAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.SourceOfLightCapstoneGlobalState))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("SourceOfLightCapstoneGlobalState", null);
#endif
            await ValidateSourceOfLightCapstoneGlobalStateAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.ShiningLeadershipHeadReferences))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("ShiningLeadershipHeadReferences", null);
#endif
            await ValidateShiningLeadershipHeadReferencesAsync(issues);
        }
    }

    private async Task RunLifecycleValidationPhasesAsync(
        GameStateValidationPhase phases,
        List<ValidationIssue> issues,
        StoryService.AcceptedStoryAppend? acceptedStoryAppend, StoryService.MortalJointStoryProof? jointStoryProof)
    {
        if (phases.Includes(GameStateValidationPhase.LifeEvaluationRewardCycle))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("LifeEvaluationRewardCycle", null);
#endif
            await ValidateLifeEvaluationRewardCycleAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.NoLifeEvaluationRewardsOnTriggerTurn))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("NoLifeEvaluationRewardsOnTriggerTurn", null);
#endif
            await ValidateNoLifeEvaluationRewardsOnTriggerTurnAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.GuardianResonancePowerEvents))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("GuardianResonancePowerEvents", null);
#endif
            await ValidateGuardianResonancePowerEventsAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.ShiningTreasuryClientOwnedState))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("ShiningTreasuryClientOwnedState", null);
#endif
            await ValidateShiningTreasuryClientOwnedStateAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.AfterlifeActiveThreatPreTurnContinuity))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AfterlifeActiveThreatPreTurnContinuity", null);
#endif
            await ValidateAfterlifeActiveThreatPreTurnContinuityAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.AfterlifeGlobalFlagPreTurnContinuity))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AfterlifeGlobalFlagPreTurnContinuity", null);
#endif
            await ValidateAfterlifeGlobalFlagPreTurnContinuityAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.ClientOwnedControlFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("ClientOwnedControlFiles", null);
#endif
            await ValidateClientOwnedControlFilesAsync(issues, acceptedStoryAppend, jointStoryProof);
        }
        if (phases.Includes(GameStateValidationPhase.RealmSegregation))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("RealmSegregation", null);
#endif
            await ValidateRealmSegregationAsync(issues);
        }
    }

    private void LogValidationErrors(List<ValidationIssue> issues)
    {
        foreach (var issue in issues.Where(i => i.Severity == IssueSeverity.Error))
            _logger.LogWarning("Ошибка валидации: [{File}] {Message}", issue.FilePath, issue.Message);
    }
}
