using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private async Task<List<ValidationIssue>> ValidateGameStateInternalAsync()
    {
        _knownCanonicalFactionIdsCache = null;
        _knownCanonicalFactionNamesCache = null;

        var issues = new List<ValidationIssue>();

        await RunCoreValidationPhasesAsync(issues);
        await RunActorAndStateValidationPhasesAsync(issues);
        await RunLifecycleValidationPhasesAsync(issues);

        LogValidationErrors(issues);
        return issues;
    }

    private async Task RunCoreValidationPhasesAsync(List<ValidationIssue> issues)
    {
        await ValidateJsonIntegrity(issues);
        ValidateRequiredFiles(issues);
        await ValidateRequiredFields(issues);
        await ValidateLoreBootstrapRequiredFilesAsync(issues);
        await ValidateCrossReferences(issues);
        await ValidateSoulStateConsistency(issues);
    }

    private async Task RunActorAndStateValidationPhasesAsync(List<ValidationIssue> issues)
    {
        await ValidatePlayerStateFiles(issues);
        await ValidateNpcStateFiles(issues);
        await ValidateSkillContractConsistencyAsync(issues);
        await ValidateWorldQuestCombatFactionStateFiles(issues);
        await ValidateMetaMiscStateFiles(issues);
        await ValidateAfterlifeSpiritualConflictStateAsync(issues);
        await ValidateShiningLeadershipHeadReferencesAsync(issues);
    }

    private async Task RunLifecycleValidationPhasesAsync(List<ValidationIssue> issues)
    {
        await ValidateLifeEvaluationRewardCycleAsync(issues);
        await ValidateNoLifeEvaluationRewardsOnTriggerTurnAsync(issues);
        await ValidateGuardianResonancePowerEventsAsync(issues);
        await ValidateClientOwnedControlFilesAsync(issues);
        await ValidateRealmSegregationAsync(issues);
    }

    private void LogValidationErrors(List<ValidationIssue> issues)
    {
        foreach (var issue in issues.Where(i => i.Severity == IssueSeverity.Error))
            _logger.LogWarning("Ошибка валидации: [{File}] {Message}", issue.FilePath, issue.Message);
    }
}
