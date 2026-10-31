using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task OrdinaryProcedureCompletedItemComparisonReachesActualPostValidatorWithoutLiveReceipt()
    {
        // Existing typed ordinary producer, not the historical reflective full-state probe.
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack());
        var fs = context.FileSystem;
        await context.ReleaseLeaseAsync();
        var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(fs,
            new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance), validator,
            new Dictionary<string, string>(StringComparer.Ordinal));
        Assert.DoesNotContain(published.Issues, issue => issue.Severity == IssueSeverity.Error);
        var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(published.TreatmentResourcePublicationTransaction);
        await using var cleanup = (IAsyncDisposable)transaction;
        Assert.Null(transaction.ReadCompletedItemValidation());
        var turn = JsonSerializer.Deserialize<TurnRequest>(Assert.IsType<string>(await fs.ReadFileAsync(LiveTurnPreparationService.TurnRequestPath)),
            BookOfEternityClient.Configuration.SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        var advanced = await transaction.AdvancePublishedAgreementWithProgressionAsync(fs,
            new ProgressionScheduleService(fs, NullLogger<ProgressionScheduleService>.Instance), turn.ProgressionControl!);
        Assert.True(advanced.IsValid, JsonSerializer.Serialize(advanced.Issues));
        var finalized = await transaction.CompleteAsync(fs);
        Assert.True(finalized.IsValid, JsonSerializer.Serialize(finalized.Issues));
        Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Finalized, finalized.Outcome);
        var completed = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction.CompletedItemValidation>(transaction.ReadCompletedItemValidation());
        async Task<string> ClaimsAsync()
        {
            await using var lease = await fs.AcquireCanonicalWriteLeaseAsync();
            var publication = context.Plan.TreatmentResourcePublicationAuthority!;
            return JsonSerializer.Serialize(AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(fs,
                lease, publication.RequestAuthority, publication.Finalization));
        }
        var claims = await ClaimsAsync();
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        var raw = await validator.ValidatePostMaterializedGameStateAsync();
        var issue = Assert.Single(raw, value => value.Code == "npc_existing_inventory_resend_forbidden");
        using (validator.UseCompletedTreatmentItemValidationScope(completed))
        {
            var actual = await validator.ValidatePostMaterializedGameStateAsync();
            Assert.Equal(JsonSerializer.Serialize(raw.Where(value => !ReferenceEquals(value, issue))), JsonSerializer.Serialize(actual));
        }
        Assert.Equal(-1, completed.FindUniqueIssue(new[] { issue, issue }));
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        Assert.Equal(1, await ReadSelectedNpcItemCountAsync(context));
        Assert.Equal(claims, await ClaimsAsync());
    }
}
