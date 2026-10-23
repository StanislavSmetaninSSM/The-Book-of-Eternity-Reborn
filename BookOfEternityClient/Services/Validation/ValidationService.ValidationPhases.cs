using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private readonly AsyncLocal<GameStateValidationSelection?> _activeGameStateValidationSelection = new();

    /// <summary>
    /// Executes selected phases under the caller's admission, deferring reporting for a pending-snapshot proof.
    /// </summary>
    /// <param name="selection">
    /// Exact game-state phase and file selection.
    /// </param>
    /// <param name="metaControlShapesOnly">
    /// Whether to run only the existing Meta control-shape component.
    /// </param>
    /// <param name="acceptedStoryAppend">
    /// Optional accepted QTE story proof.
    /// </param>
    /// <param name="jointStoryProof">
    /// Optional Mortal joint story proof.
    /// </param>
    /// <returns>
    /// Ordered issues; a pending-snapshot caller must verify and close before publishing them.
    /// </returns>
    private async Task<List<ValidationIssue>> ValidateGameStateCoreAsync(
        GameStateValidationSelection selection, bool metaControlShapesOnly, StoryService.AcceptedStoryAppend? acceptedStoryAppend,
        StoryService.MortalJointStoryProof? jointStoryProof)
    {
        using var innerTiming1 = _fs.MeasureOperation("GameState.ValidateCoreAsync");
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

    /// <summary>
    /// Reuses selected read-only source DATA while preserving the actual phase and its ordered issues.
    /// </summary>
    /// <param name="validate">
    /// Original CrossReferences or MetaMisc phase; no verdict is retained between invocations.
    /// </param>
    /// <returns>
    /// Completion after the selected phase's fresh source proof, or its original unsupported route.
    /// </returns>
    private async Task RunReusableGameStateSourcePhaseAsync(Func<Task> validate)
    {
        // Completed guardian comparison owns a nested lease and keeps its original route.
        if (_guardianMusingsPublication.Value is { Enabled: true, Completion: not null })
        {
            await validate();
            return;
        }
        await _fs.ReadGameStateDataAsync(async () => { await validate(); return true; });
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
            using var innerTiming2 = _fs.MeasureOperation("GameState.JsonIntegrity");
            await ValidateJsonIntegrity(issues);
        }
        if (phases.Includes(GameStateValidationPhase.RequiredFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("RequiredFiles", null);
#endif
            using var innerTiming3 = _fs.MeasureOperation("GameState.RequiredFiles");
            ValidateRequiredFiles(issues);
        }
        if (phases.Includes(GameStateValidationPhase.RequiredFields))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("RequiredFields", null);
#endif
            using var innerTiming4 = _fs.MeasureOperation("GameState.RequiredFields");
            await ValidateRequiredFields(issues);
        }
        if (phases.Includes(GameStateValidationPhase.LoreBootstrapRequiredFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("LoreBootstrapRequiredFiles", null);
#endif
            using var innerTiming5 = _fs.MeasureOperation("GameState.LoreBootstrapRequiredFiles");
            await ValidateLoreBootstrapRequiredFilesAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.MortalBootstrapPlayerVisibleNames))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("MortalBootstrapPlayerVisibleNames", null);
#endif
            using var innerTiming6 = _fs.MeasureOperation("GameState.MortalBootstrapPlayerVisibleNames");
            await ValidateMortalBootstrapPlayerVisibleNamesAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.MortalBootstrapContentAnchors))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("MortalBootstrapContentAnchors", null);
#endif
            using var innerTiming7 = _fs.MeasureOperation("GameState.MortalBootstrapContentAnchors");
            await ValidateMortalBootstrapContentAnchorsAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.CrossReferences))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("CrossReferences", null);
#endif
            using var innerTiming8 = _fs.MeasureOperation("GameState.CrossReferences");
            await RunReusableGameStateSourcePhaseAsync(() => ValidateCrossReferences(issues));
        }
        if (phases.Includes(GameStateValidationPhase.RivalAndResidentCrossReferences) &&
            !phases.Includes(GameStateValidationPhase.CrossReferences))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("RivalAndResidentCrossReferences", null);
#endif
            using var innerTiming9 = _fs.MeasureOperation("GameState.RivalAndResidentCrossReferences");
            await ValidateRivalAndResidentCrossReferencesAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.SoulStateConsistency))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("SoulStateConsistency", null);
#endif
            using var innerTiming10 = _fs.MeasureOperation("GameState.SoulStateConsistency");
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
            using var innerTiming11 = _fs.MeasureOperation("GameState.PlayerStateFiles");
            await ValidatePlayerStateFiles(issues);
        }
        if (phases.Includes(GameStateValidationPhase.NpcStateFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("NpcStateFiles", null);
#endif
            using var innerTiming12 = _fs.MeasureOperation("GameState.NpcStateFiles");
            await ValidateNpcStateFiles(issues);
        }
        if (phases.Includes(GameStateValidationPhase.SkillContractConsistency))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("SkillContractConsistency", null);
#endif
            using var innerTiming13 = _fs.MeasureOperation("GameState.SkillContractConsistency");
            await ValidateSkillContractConsistencyAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.TrainingShowcases))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("TrainingShowcases", null);
#endif
            using var innerTiming14 = _fs.MeasureOperation("GameState.TrainingShowcases");
            await ValidateTrainingShowcasesAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.WorldQuestCombatFactionStateFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("WorldQuestCombatFactionStateFiles", null);
#endif
            using var innerTiming15 = _fs.MeasureOperation("GameState.WorldQuestCombatFactionStateFiles");
            await ValidateWorldQuestCombatFactionStateFiles(issues);
        }
        if (phases.Includes(GameStateValidationPhase.MetaMiscStateFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("MetaMiscStateFiles", null);
#endif
            using var innerTiming16 = _fs.MeasureOperation("GameState.MetaMiscStateFiles");
            await RunReusableGameStateSourcePhaseAsync(() => ValidateMetaMiscStateFiles(issues));
        }
        else if (phases.Includes(GameStateValidationPhase.GuardianProjectStateFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("GuardianProjectStateFiles", null);
#endif
            using var innerTiming17 = _fs.MeasureOperation("GameState.GuardianProjectStateFiles");
            await ValidateGuardianProjectStateFilesAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.AcceptedTurnActorMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnActorMaterializationCompleteness", null);
#endif
            using var innerTiming18 = _fs.MeasureOperation("GameState.AcceptedTurnActorMaterializationCompleteness");
            await ValidateAcceptedTurnActorMaterializationCompletenessAsync(issues);
        }
        if (phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnFactionMaterializationCompleteness", null);
#endif
            using var innerTiming19 = _fs.MeasureOperation("GameState.AcceptedTurnFactionMaterializationCompleteness");
            await ValidateAcceptedTurnFactionMaterializationCompletenessAsync(issues);
        }
        if (phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnLocationMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnLocationMaterializationCompleteness", null);
#endif
            using var innerTiming20 = _fs.MeasureOperation("GameState.AcceptedTurnLocationMaterializationCompleteness");
            await ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(issues);
        }
        if (phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnItemMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnItemMaterializationCompleteness", null);
#endif
            using var innerTiming21 = _fs.MeasureOperation("GameState.AcceptedTurnItemMaterializationCompleteness");
            await ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(issues, null);
        }
        if (phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnEffectMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnEffectMaterializationCompleteness", null);
#endif
            using var innerTiming22 = _fs.MeasureOperation("GameState.AcceptedTurnEffectMaterializationCompleteness");
            await ValidateAcceptedTurnCanonicalEffectMaterializationAsync(issues, null);
        }
        if (phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnWoundMaterializationCompleteness))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AcceptedTurnWoundMaterializationCompleteness", null);
#endif
            using var innerTiming23 = _fs.MeasureOperation("GameState.AcceptedTurnWoundMaterializationCompleteness");
            await ValidateAcceptedTurnCanonicalWoundMaterializationAsync(
                issues,
                writeLease: null);
        }
        if (phases.Includes(GameStateValidationPhase.AfterlifeSpiritualConflictState))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AfterlifeSpiritualConflictState", null);
#endif
            using var innerTiming24 = _fs.MeasureOperation("GameState.AfterlifeSpiritualConflictState");
            await ValidateAfterlifeSpiritualConflictStateAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.SourceOfLightCapstoneGlobalState))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("SourceOfLightCapstoneGlobalState", null);
#endif
            using var innerTiming25 = _fs.MeasureOperation("GameState.SourceOfLightCapstoneGlobalState");
            await ValidateSourceOfLightCapstoneGlobalStateAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.ShiningLeadershipHeadReferences))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("ShiningLeadershipHeadReferences", null);
#endif
            using var innerTiming26 = _fs.MeasureOperation("GameState.ShiningLeadershipHeadReferences");
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
            using var innerTiming27 = _fs.MeasureOperation("GameState.LifeEvaluationRewardCycle");
            await ValidateLifeEvaluationRewardCycleAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.NoLifeEvaluationRewardsOnTriggerTurn))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("NoLifeEvaluationRewardsOnTriggerTurn", null);
#endif
            using var innerTiming28 = _fs.MeasureOperation("GameState.NoLifeEvaluationRewardsOnTriggerTurn");
            await ValidateNoLifeEvaluationRewardsOnTriggerTurnAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.GuardianResonancePowerEvents))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("GuardianResonancePowerEvents", null);
#endif
            using var innerTiming29 = _fs.MeasureOperation("GameState.GuardianResonancePowerEvents");
            await ValidateGuardianResonancePowerEventsAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.ShiningTreasuryClientOwnedState))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("ShiningTreasuryClientOwnedState", null);
#endif
            using var innerTiming30 = _fs.MeasureOperation("GameState.ShiningTreasuryClientOwnedState");
            await ValidateShiningTreasuryClientOwnedStateAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.AfterlifeActiveThreatPreTurnContinuity))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AfterlifeActiveThreatPreTurnContinuity", null);
#endif
            using var innerTiming31 = _fs.MeasureOperation("GameState.AfterlifeActiveThreatPreTurnContinuity");
            await ValidateAfterlifeActiveThreatPreTurnContinuityAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.AfterlifeGlobalFlagPreTurnContinuity))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("AfterlifeGlobalFlagPreTurnContinuity", null);
#endif
            using var innerTiming32 = _fs.MeasureOperation("GameState.AfterlifeGlobalFlagPreTurnContinuity");
            await ValidateAfterlifeGlobalFlagPreTurnContinuityAsync(issues);
        }
        if (phases.Includes(GameStateValidationPhase.ClientOwnedControlFiles))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("ClientOwnedControlFiles", null);
#endif
            using var innerTiming33 = _fs.MeasureOperation("GameState.ClientOwnedControlFiles");
            await ValidateClientOwnedControlFilesAsync(issues, acceptedStoryAppend, jointStoryProof);
        }
        if (phases.Includes(GameStateValidationPhase.RealmSegregation))
        {
#if DEBUG
            using var diagnosticPhase = _fs.BrowserDiagnosticLogical("RealmSegregation", null);
#endif
            using var innerTiming34 = _fs.MeasureOperation("GameState.RealmSegregation");
            await ValidateRealmSegregationAsync(issues);
        }
    }

    private void LogValidationErrors(List<ValidationIssue> issues)
    {
        foreach (var issue in issues.Where(i => i.Severity == IssueSeverity.Error))
            _logger.LogWarning("Ошибка валидации: [{File}] {Message}", issue.FilePath, issue.Message);
    }
}
