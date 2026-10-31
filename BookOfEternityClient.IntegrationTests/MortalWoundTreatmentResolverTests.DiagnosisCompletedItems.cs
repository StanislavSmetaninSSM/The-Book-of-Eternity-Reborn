using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task DiagnosisCompletedItemComparisonUsesActualPostValidatorAndExactWholeFrame()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 0);
        var (transaction, validator, binding, frame, publication) = await PublishDiagnosisForCompletedItemValidationAsync(fixture);
        await using var cleanup = (IAsyncDisposable)transaction;
        Assert.Null(transaction.ReadCompletedItemValidation()); // Published is not finalized.
        var advanced = await transaction.AdvancePublishedAgreementWithProgressionAsync(fixture.Files,
            new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance), frame.Progression);
        Assert.True(advanced.IsValid, JsonSerializer.Serialize(advanced.Issues));
        var result = await transaction.CompleteAsync(fixture.Files);
        Assert.True(result.IsValid, JsonSerializer.Serialize(result.Issues));
        Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Finalized, result.Outcome);
        Assert.Empty(result.Issues); Assert.Equal(1, result.ChangedCount);
        var completed = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction.CompletedItemValidation>(transaction.ReadCompletedItemValidation());
        async Task<string> ClaimsAsync()
        {
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            return JsonSerializer.Serialize(AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
                fixture.Files, lease, publication.RequestAuthority, publication.Finalization));
        }
        var claims = await ClaimsAsync();
        var bytes = fixture.CaptureJson();
        var raw = await validator.ValidatePostMaterializedGameStateAsync();
        var exactIssue = Assert.Single(raw, IsCompletedItemInventoryIssue);
        Assert.Equal(2, Assert.Single(JsonNode.Parse(exactIssue.Expected!)!.AsArray())!["count"]!.GetValue<int>());
        Assert.Equal(1, Assert.Single(JsonNode.Parse(exactIssue.Actual!)!.AsArray())!["count"]!.GetValue<int>());
        using (validator.UseCompletedTreatmentItemValidationScope(completed))
        {
            var actual = await validator.ValidatePostMaterializedGameStateAsync();
            Assert.Equal(JsonSerializer.Serialize(raw.Where(issue => !ReferenceEquals(issue, exactIssue))), JsonSerializer.Serialize(actual));
        }
        fixture.AssertJson(bytes);

        var npcBytes = Assert.IsType<byte[]>(await fixture.Files.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath));
        var npc = JsonNode.Parse(FileSystemManager.DecodeFileBytes(npcBytes))!.AsObject();
        string generation;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            generation = fixture.Files.ReadExistingSessionGeneration(lease)!;
        bool Matches(JsonObject root, byte[] current, string? token = null, int? turn = null, string? actualGeneration = null) => completed.Matches(fixture.Files,
            actualGeneration ?? generation, binding.SessionId, binding.RequestId, turn ?? binding.Turn, token ?? binding.SnapshotToken, root, current);
        Assert.True(Matches(npc, npcBytes));
        Assert.False(Matches(npc, npcBytes, actualGeneration: generation + "foreign"));
        Assert.False(Matches(npc, npcBytes.Append((byte)' ').ToArray()));
        Assert.False(Matches(npc, npcBytes, token: binding.SnapshotToken + "x"));
        Assert.False(Matches(npc, npcBytes, turn: binding.Turn + 1));
        foreach (var change in new Action<JsonObject>[]
        {
            root => root["NPCsInScene"]![0]!["inventory"]![0]!["count"] = 2,
            root => root["NPCsInScene"]![0]!["inventory"]![0]!["itemId"] = "foreign_item",
            root => root["NPCsInScene"]![0]!["NPCId"] = "foreign_actor",
            root => root["unrelatedRootDrift"] = true
        })
        {
            var changed = npc.DeepClone().AsObject(); change(changed);
            Assert.False(Matches(changed, npcBytes));
            Assert.False(Matches(changed, System.Text.Encoding.UTF8.GetBytes(changed.ToJsonString())));
        }
        Assert.Equal(-1, completed.FindUniqueIssue(new[] { exactIssue, exactIssue }));
        Assert.Equal(0, completed.FindUniqueIssue(new[] { exactIssue }));
        using (var invalidated = validator.UseCompletedTreatmentItemValidationScope(completed))
        {
            invalidated.Invalidate();
            Assert.Equal(JsonSerializer.Serialize(raw), JsonSerializer.Serialize(await validator.ValidatePostMaterializedGameStateAsync()));
        }
        var foreign = new FileSystemManager(fixture.Files.BasePath, NullLogger<FileSystemManager>.Instance);
        var foreignValidator = new ValidationService(foreign, NullLogger<ValidationService>.Instance);
        using (foreignValidator.UseCompletedTreatmentItemValidationScope(completed))
            Assert.Contains(await foreignValidator.ValidatePostMaterializedGameStateAsync(), IsCompletedItemInventoryIssue);
        fixture.AssertJson(bytes);
        Assert.Equal(claims, await ClaimsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiagnosisCompletedItemExportRequiresFinalizationAndObservedSuccessfulClose(bool closeFailure)
    {
        var armed = false; var cuts = 0;
        var hooks = new FileSystemManagerHooks
        {
            OrdinaryWriteLeaseCloseContextFactory = () => armed ? new CompletedItemThrowingClose(() => { cuts++; armed = false; }) : null
        };
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 0, hooks: hooks);
        var (transaction, _, _, frame, _) = await PublishDiagnosisForCompletedItemValidationAsync(fixture);
        await using var cleanup = (IAsyncDisposable)transaction;
        Assert.Null(transaction.ReadCompletedItemValidation());
        if (!closeFailure)
        {
            var compensated = await transaction.CompensateAsync(fixture.Files);
            Assert.True(compensated.IsValid, JsonSerializer.Serialize(compensated.Issues));
            Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Rearmed, compensated.Outcome);
            Assert.Null(transaction.ReadCompletedItemValidation());
            return;
        }
        var advanced = await transaction.AdvancePublishedAgreementWithProgressionAsync(fixture.Files,
            new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance), frame.Progression);
        Assert.True(advanced.IsValid, JsonSerializer.Serialize(advanced.Issues));
        var before = fixture.CaptureJson();
        armed = true;
        var completed = await transaction.CompleteAsync(fixture.Files);
        Assert.Equal(1, cuts);
        Assert.True(completed.IsValid, JsonSerializer.Serialize(completed.Issues));
        Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Finalized, completed.Outcome);
        Assert.Equal(1, completed.ChangedCount);
        Assert.Null(transaction.ReadCompletedItemValidation());
        fixture.AssertJson(before);
        var replay = await transaction.CompleteAsync(fixture.Files);
        Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.TokenReplayed, replay.Outcome);
        Assert.Null(transaction.ReadCompletedItemValidation());
        fixture.AssertJson(before);
    }

    private sealed class CompletedItemThrowingClose(Action onClose) : IDisposable
    {
        public void Dispose() { onClose(); throw new IOException("Controlled finalized lease close refusal."); }
    }

    /// <summary>Names the exact ordinary inventory guard emitted for the genuine medic debit.</summary>
    /// <param name="issue">Actual validator issue.</param>
    /// <returns>Whether this is the one expected unchanged pre-turn inventory guard.</returns>
    internal static bool IsCompletedItemInventoryIssue(ValidationIssue issue) =>
        issue.Code == "npc_existing_inventory_resend_forbidden" && issue.Section == "NPCInventory" &&
        issue.Actor == "mortal_npc:field_medic_01" &&
        issue.FilePath == NpcCoreChangesContract.NpcCorePath + ".NPCsInScene[0].inventory";

    private static async Task<(MortalWoundTreatmentResourcePublicationTransaction Transaction, ValidationService Validator,
        AcceptedMechanicsPlanBinding Binding, SpiritualSelfTreatmentStageContext Frame,
        MortalWoundTreatmentResourcePublicationAuthority Publication)> PublishDiagnosisForCompletedItemValidationAsync(DiagnosisOwnershipFixture fixture)
    {
        var files = fixture.Files;
        var input = fixture.Authority.ReadDiagnosisOriginalInput();
        var frame = MortalWoundDiagnosisInputCodec.Read(input).Frame;
        var admitted = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(files, fixture.Lease, frame);
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease));
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
        var common = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(files, fixture.Lease,
            new GameResponse(), admitted.Owner, admitted.Request);
        Assert.True(common.IsValid, JsonSerializer.Serialize(common.Issues));
        Assert.IsType<AcceptedMechanicsPlan>(common.Plan);
        var binding = Assert.IsType<AcceptedMechanicsInput>(common.OriginalComposedInput).CreateBinding();
        await fixture.ReleaseLeaseAsync();
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
            new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance), validator,
            new Dictionary<string, string>(StringComparer.Ordinal));
        Assert.DoesNotContain(published.Issues, issue => issue.Severity == IssueSeverity.Error);
        return (Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(published.TreatmentResourcePublicationTransaction), validator, binding, frame, common.Plan!.TreatmentResourcePublicationAuthority!);
    }
}
