using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const string ProcedureFateShieldEffectId =
        "effect_t070b5_fate_shield";

    [Fact]
    public async Task ProcedureNoResourceAttempt_StillRequiresCoordinatedTransaction()
    {
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault: null,
            includeEnergyResource: false);
        var originalPlan = context.Plan;
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        await context.ReleaseLeaseAsync();

        var result = await AcceptedTurnCanonicalStateRefresh
            .NormalizeAndValidateWithPlanAsync(
                context.FileSystem,
                new CanonicalStateNormalizer(
                    context.FileSystem,
                    NullLogger<CanonicalStateNormalizer>.Instance),
                new ValidationService(
                    context.FileSystem,
                    NullLogger<ValidationService>.Instance),
                new Dictionary<string, string>(StringComparer.Ordinal));
        var transaction = result.TreatmentResourcePublicationTransaction;
        try
        {
            Assert.NotNull(transaction);
            Assert.Same(originalPlan, result.MechanicsPlan);
            Assert.DoesNotContain(result.Issues, static issue =>
                issue.Severity == IssueSeverity.Error);
        }
        finally
        {
            if (transaction is not null)
                await ((IAsyncDisposable)transaction).DisposeAsync();
        }

        await AssertExactTreatmentTransactionBytesAsync(context, before);
        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out _,
            out var rearmed));
        Assert.True(rearmed.Success, DescribeValidationIssues(rearmed.Issues));
        Assert.Same(originalPlan, rearmed.Plan);
        AssertProcedureReservationIsLive(context);
    }

    [Theory]
    [InlineData("with_plan")]
    [InlineData("accepted_mechanics")]
    public async Task ProcedureNoResourceDirectNormalizerEntry_RejectsWithoutReceipt(
        string entryPoint)
    {
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault: null,
            includeEnergyResource: false);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        var originalPlan = context.Plan;
        await context.ReleaseLeaseAsync();
        var normalizer = new CanonicalStateNormalizer(
            context.FileSystem,
            NullLogger<CanonicalStateNormalizer>.Instance);

        var exception = await Record.ExceptionAsync(async () =>
        {
            switch (entryPoint)
            {
                case "with_plan":
                    await normalizer.NormalizeAccumulatedStateWithPlanAsync(
                        new Dictionary<string, string>(StringComparer.Ordinal));
                    break;
                case "accepted_mechanics":
                    await normalizer.NormalizeAcceptedMechanicsAsync(
                        new Dictionary<string, string>(StringComparer.Ordinal));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(entryPoint),
                        entryPoint,
                        "Unknown normalizer entry point.");
            }
        });

        Assert.IsType<InvalidOperationException>(exception);
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out _,
            out var retained));
        Assert.True(retained.Success, DescribeValidationIssues(retained.Issues));
        Assert.Same(originalPlan, retained.Plan);
        AssertProcedureReservationIsLive(context);
    }

    [Fact]
    public async Task ProcedurePostWriteFailure_RestoresEveryRootAndRearmsSameClaimsAndPlan()
    {
        var fault = new AcceptedTreatmentPipelineFault("wound_output", 1);
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault,
            includeEnergyResource: true,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            includeFateShield: true);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        var originalPlan = context.Plan;
        var originalBindingFingerprint =
            AcceptedMechanicsPlanFingerprints.ComputeInput(context.OriginalBinding);
        var itemTransitionsBefore =
            await CountSelectedItemConsumeTransitionsAsync(context);
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var woundTransitionsBefore = CountProcedureTreatmentTransitions(context);
        var fateTransitionsBefore =
            await CountProcedureFateTransitionsAsync(context);
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);

        fault.Arm(context);
        var disposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "procedure post-write compensation oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);

        Assert.True(
            fault.Fired,
            $"Observed={string.Join(", ", fault.ObservedPhases)}");
        Assert.Equal(
            AcceptedTurnValidationDisposition.RetryablePublicationRearmed,
            disposition);
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        Assert.Equal(
            itemTransitionsBefore,
            await CountSelectedItemConsumeTransitionsAsync(context));
        Assert.Equal(
            resourceSpendsBefore,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(woundTransitionsBefore, CountProcedureTreatmentTransitions(context));
        Assert.Equal(
            fateTransitionsBefore,
            await CountProcedureFateTransitionsAsync(context));
        Assert.Equal(
            new[] { ProcedureFateShieldEffectId },
            await ReadActiveProcedureFateShieldIdsAsync(context));

        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out var rearmedBinding,
            out var rearmed));
        Assert.True(rearmed.Success, DescribeValidationIssues(rearmed.Issues));
        Assert.Same(originalPlan, rearmed.Plan);
        Assert.Equal(
            originalBindingFingerprint,
            AcceptedMechanicsPlanFingerprints.ComputeInput(rearmedBinding));
        AssertProcedureReservationIsLive(context);
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
    }

    [Fact]
    public async Task ProcedureTerminalQuarantine_RemovesDurableAuthorityBeforeReleasingEveryClaim()
    {
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault: null,
            withRollbackAuthority: true,
            includeEnergyResource: true,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            includeFateShield: true);
        var resourceStateBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        var resourceHistoryBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));
        var itemCountBefore = await ReadSelectedNpcItemCountAsync(context);
        var fateTransitionsBefore =
            await CountProcedureFateTransitionsAsync(context);
        await context.ReleaseLeaseAsync();
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "GetValidatedRollbackSnapshotAsync",
            manifest);

        const string npcCorePath = "game_state/npcs/npc_core.json";
        var npcBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(npcCorePath));
        var npcRoot = ParseJsonObjectBytes(npcBefore);
        var provider = Assert.IsType<JsonObject>(Assert.Single(
            Assert.IsType<JsonArray>(npcRoot["NPCsInScene"])));
        var originalWorldview = provider["worldview"]?.GetValue<string>();
        provider["worldview"] =
            "This rejected direct mutation forces terminal procedure quarantine.";
        await context.FileSystem.WriteFileAtomicAsync(
            npcCorePath,
            npcRoot.ToJsonString());
        var rejectedNpcBytes = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(npcCorePath));

        var rawIssuesObject = await InvokePrivateTaskResultAsync(
            engine,
            "CollectAcceptedTurnRawStateIssuesAsync");
        var rawIssues = Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            rawIssuesObject);
        Assert.Contains(rawIssues, static issue => string.Equals(
            issue.Code,
            "npc_existing_core_direct_mutation_forbidden",
            StringComparison.Ordinal));

        var disposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "procedure terminal quarantine ordering oracle",
                snapshotContext,
                rollbackSnapshot,
                HeldTreatmentPipelineContext.Turn,
                new ProgressionControl { CurrentRealm = "Mortal World" });

        Assert.Equal(AcceptedTurnValidationDisposition.TerminalRejected, disposition);
        Assert.Equal(
            rejectedNpcBytes,
            await context.FileSystem.ReadFileBytesAsync(npcCorePath));
        Assert.False(await ContainsCurrentExactTreatmentRequestAsync(context));
        Assert.Equal(
            resourceStateBefore,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        Assert.Equal(
            resourceHistoryBefore,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));
        Assert.Equal(itemCountBefore, await ReadSelectedNpcItemCountAsync(context));
        Assert.Equal(
            fateTransitionsBefore,
            await CountProcedureFateTransitionsAsync(context));
        Assert.Equal(
            new[] { ProcedureFateShieldEffectId },
            await ReadActiveProcedureFateShieldIdsAsync(context));

        await context.AcquireLeaseAsync();
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
        Assert.False(AcceptedTurnAuthorityRegistry
            .HasLiveMortalWoundProcedureReservationAgreement(
                context.FileSystem,
                context.Lease,
                context.AcceptedState,
                RequireProcedureAuthority(context.Request)));

        // Durable authority is already absent at this point.  Rebinding the same
        // accepted turn can therefore reserve the exact die, Fate shield, item,
        // and resource again; this is the observable ordering guarantee.
        var reboundState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = reboundState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        Assert.Empty(recovered.FinalizedRequests);
        var competing = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            reboundState,
            history,
            ReadCurrentTreatmentWound(context.FileSystem),
            HeldTreatmentPipelineContext.OperationKey + "_after_terminal",
            HeldTreatmentPipelineContext.RouteId,
            Assert.Single(reboundState.Binding.AcceptedEvents).EventRef);
        Assert.True(competing.IsValid, DescribeValidationIssues(competing.Issues));
        var competingRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            competing.Request);
        AssertProcedureClaims(
            competingRequest,
            includeEnergyResource: true,
            itemScenario: context.ItemScenario,
            includeFateShield: true);
        Assert.True(competingRequest.RollbackNewProvisionalClaims(reboundState));
        await context.ReleaseLeaseAsync();

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            "[yellow]procedure rollback oracle[/]");

        var rolledBackNpc = ParseJsonObjectBytes(Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(npcCorePath)));
        var rolledBackProvider = Assert.IsType<JsonObject>(Assert.Single(
            Assert.IsType<JsonArray>(rolledBackNpc["NPCsInScene"])));
        Assert.Equal(
            originalWorldview,
            rolledBackProvider["worldview"]?.GetValue<string>());
        Assert.False(await ContainsCurrentExactTreatmentRequestAsync(context));
    }

    [Fact]
    public async Task ProcedurePersistedFateDuplicate_QuarantinesDurableRowsThenReleasesEveryClaim()
    {
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault: null,
            includeEnergyResource: true,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            includeFateShield: true,
            composePublicationPlan: false);
        var resourceStateBefore = Assert.IsType<byte[]>(
            await context.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        var resourceHistoryBefore = Assert.IsType<byte[]>(
            await context.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));
        var itemCountBefore = await ReadSelectedNpcItemCountAsync(context);
        var fateTransitionsBefore =
            await CountProcedureFateTransitionsAsync(context);
        var treatmentTransitionsBefore =
            CountProcedureTreatmentTransitions(context);
        var legacyReport = new JsonObject
        {
            ["eventType"] = "owner_critical_failure",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["evidence"] = new JsonObject
            {
                ["kind"] = "mortal_action_roll",
                ["rollMode"] = "normal",
                ["diceIndexes"] = new JsonArray(0),
                ["selectedIndex"] = 0,
                ["selectedValue"] = 1,
                ["originalOutcome"] = "critical_failure",
                ["resolvedOutcome"] = "failure"
            },
            ["reason"] =
                "Legacy report is rejected while durable authority is quarantined."
        };
        using var reportDocument = JsonDocument.Parse(legacyReport.ToJsonString());
        var proposal = context.CreateMechanicsOnlyTreatmentProposal();
        proposal.EffectEventReports =
            new[] { reportDocument.RootElement.Clone() };

        var duplicate = WoundAcceptedTurnPlanner
            .ComposeMortalWoundTreatmentPublication(
                context.FileSystem,
                context.Lease,
                proposal,
                context.AcceptedState,
                context.Request,
                context.Resolution);

        Assert.False(duplicate.IsValid);
        Assert.Null(duplicate.Plan);
        Assert.Equal(
            "wound_treatment_fate_reaction_cross_surface_duplicate",
            Assert.Single(duplicate.Issues).Code);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out _,
            out var retained));
        Assert.True(retained.Success, DescribeValidationIssues(retained.Issues));
        Assert.NotNull(retained.Plan);
        Assert.True(await ContainsCurrentExactTreatmentRequestAsync(context));
        Assert.Equal(
            resourceStateBefore,
            await context.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        Assert.Equal(
            resourceHistoryBefore,
            await context.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));
        Assert.Equal(itemCountBefore, await ReadSelectedNpcItemCountAsync(context));
        Assert.Equal(
            fateTransitionsBefore,
            await CountProcedureFateTransitionsAsync(context));
        Assert.Equal(
            treatmentTransitionsBefore,
            CountProcedureTreatmentTransitions(context));
        Assert.Equal(
            new[] { ProcedureFateShieldEffectId },
            await ReadActiveProcedureFateShieldIdsAsync(context));

        await context.ReleaseLeaseAsync();
        var released = await AcceptedTurnCanonicalStateRefresh
            .ReleaseValidatedTreatmentPublicationBeforeCanonicalRefreshAsync(
                context.FileSystem,
                "validation_failed");

        Assert.NotNull(released);
        Assert.True(released!.IsValid, DescribeValidationIssues(released.Issues));
        Assert.Empty(released.Issues);
        Assert.Equal(1, released.ChangedCount);
        Assert.Equal(
            MortalWoundTreatmentPublicationTransactionOutcome.Released,
            released.Outcome);
        Assert.False(await ContainsCurrentExactTreatmentRequestAsync(context));
        Assert.Equal(
            resourceStateBefore,
            await context.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        Assert.Equal(
            resourceHistoryBefore,
            await context.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));
        Assert.Equal(itemCountBefore, await ReadSelectedNpcItemCountAsync(context));
        Assert.Equal(
            fateTransitionsBefore,
            await CountProcedureFateTransitionsAsync(context));
        Assert.Equal(
            treatmentTransitionsBefore,
            CountProcedureTreatmentTransitions(context));
        Assert.Equal(
            new[] { ProcedureFateShieldEffectId },
            await ReadActiveProcedureFateShieldIdsAsync(context));

        await context.AcquireLeaseAsync();
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
        Assert.False(AcceptedTurnAuthorityRegistry
            .HasLiveMortalWoundProcedureReservationAgreement(
                context.FileSystem,
                context.Lease,
                context.AcceptedState,
                RequireProcedureAuthority(context.Request)));
        var reboundState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = reboundState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        Assert.Empty(recovered.FinalizedRequests);
        var replacement = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            reboundState,
            history,
            ReadCurrentTreatmentWound(context.FileSystem),
            HeldTreatmentPipelineContext.OperationKey +
            "_after_persisted_fate_duplicate",
            HeldTreatmentPipelineContext.RouteId,
            Assert.Single(reboundState.Binding.AcceptedEvents).EventRef);
        Assert.True(
            replacement.IsValid,
            DescribeValidationIssues(replacement.Issues));
        var replacementRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            replacement.Request);
        AssertProcedureClaims(
            replacementRequest,
            includeEnergyResource: true,
            itemScenario: context.ItemScenario,
            includeFateShield: true);
        Assert.True(replacementRequest.RollbackNewProvisionalClaims(reboundState));
    }

    [Theory]
    [InlineData("dice")]
    [InlineData("fate")]
    public async Task ProcedureChangedDiceOrFateReservation_RejectsBeforeCanonicalWrites(
        string claimAxis)
    {
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault: null,
            includeEnergyResource: false,
            includeFateShield: true);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        ReleaseOnlyProcedureClaim(context, claimAxis);
        await context.ReleaseLeaseAsync();

        var exception = await Record.ExceptionAsync(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem,
                new CanonicalStateNormalizer(
                    context.FileSystem,
                    NullLogger<CanonicalStateNormalizer>.Instance),
                new ValidationService(
                    context.FileSystem,
                    NullLogger<ValidationService>.Instance),
                new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.IsType<InvalidDataException>(exception);
        await AssertExactTreatmentTransactionBytesAsync(context, before);
    }

    [Fact]
    public async Task ProcedureHeldBlockedInvalidation_PreservesEveryClaimAndRejectsNewReservations()
    {
        var fault = new AcceptedTreatmentPipelineFault(
            "pre_canonical_terminal_quarantine_safe_failure",
            1);
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault,
            withRollbackAuthority: true,
            includeEnergyResource: true,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            includeFateShield: true);
        await context.ReleaseLeaseAsync();
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "GetValidatedRollbackSnapshotAsync",
            manifest);

        fault.Arm(context);
        var disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "SettlePreCanonicalTreatmentPublicationBeforeTerminalRollbackAsync",
            rollbackSnapshot,
            "validation_failed");

        Assert.True(fault.Fired);
        Assert.Equal(
            AcceptedTurnValidationDisposition.RetryablePublicationHeldBlocked,
            disposition);
        await context.AcquireLeaseAsync();
        AssertProcedureReservationIsLive(context);
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);

        AcceptedTurnAuthorityRegistry.InvalidateAcceptedTurnValidated(
            context.FileSystem,
            context.Lease);

        AssertProcedureReservationIsLive(context);
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        Assert.False(RequireProcedureAuthority(context.Request)
            .ReleaseProvisionalReservations(
                ExportCurrentTreatmentAcceptedState(context)));
        AssertProcedureReservationIsLive(context);
        var resourceRelease =
            MortalWoundTreatmentResourceComposer.ReleaseTreatmentResources(
                context.FileSystem,
                context.Lease,
                new[] { context.Request },
                "cancelled");
        Assert.False(resourceRelease.IsValid);
        Assert.Equal(0, resourceRelease.ChangedCount);
        AssertProcedureReservationIsLive(context);
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        var blockedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var competing = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            blockedState,
            history,
            ReadCurrentTreatmentWound(context.FileSystem),
            HeldTreatmentPipelineContext.OperationKey + "_blocked_competing",
            HeldTreatmentPipelineContext.RouteId,
            Assert.Single(blockedState.Binding.AcceptedEvents).EventRef);
        Assert.False(competing.IsValid);
        Assert.Null(competing.Request);
        Assert.Contains(competing.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_procedure_reservation_authority_invalid",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("dice")]
    [InlineData("fate")]
    public async Task ProcedurePreTakeSingleClaimDrift_RestoresEveryRootAndBlocksRestart(
        string claimAxis)
    {
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault: null,
            withRollbackAuthority: true,
            includeEnergyResource: true,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            includeFateShield: true);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        ReleaseOnlyProcedureClaim(context, claimAxis);
        await context.ReleaseLeaseAsync();
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "GetValidatedRollbackSnapshotAsync",
            manifest);

        var disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "SettlePreCanonicalTreatmentPublicationBeforeTerminalRollbackAsync",
            rollbackSnapshot,
            "validation_failed");

        Assert.Equal(
            AcceptedTurnValidationDisposition.RetryablePublicationHeldBlocked,
            disposition);
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        await context.AcquireLeaseAsync();
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        AssertTreatmentPublicationRestartBlocked(context);
    }

    [Theory]
    [InlineData("dice")]
    [InlineData("fate")]
    public async Task ProcedurePostTakeSingleClaimDrift_RestoresEveryRootAndBlocksRestart(
        string claimAxis)
    {
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault: null,
            includeEnergyResource: true,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            includeFateShield: true);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        await context.ReleaseLeaseAsync();
        var normalized = await AcceptedTurnCanonicalStateRefresh
            .NormalizeAndValidateWithPlanAsync(
                context.FileSystem,
                new CanonicalStateNormalizer(
                    context.FileSystem,
                    NullLogger<CanonicalStateNormalizer>.Instance),
                new ValidationService(
                    context.FileSystem,
                    NullLogger<ValidationService>.Instance),
                new Dictionary<string, string>(StringComparer.Ordinal));
        var transaction = Assert.IsType<
            MortalWoundTreatmentResourcePublicationTransaction>(
                normalized.TreatmentResourcePublicationTransaction);
        Assert.DoesNotContain(normalized.Issues, static issue =>
            issue.Severity == IssueSeverity.Error);

        var transactionClosed = false;
        try
        {
            await context.AcquireLeaseAsync();
            ReleaseOnlyProcedureClaim(context, claimAxis);
            var released = await transaction.ReleaseTerminalUnderLeaseAsync(
                context.FileSystem,
                context.Lease,
                "validation_failed");

            Assert.False(released.IsValid);
            Assert.Equal(0, released.ChangedCount);
            Assert.Equal(
                MortalWoundTreatmentPublicationTransactionOutcome.ReleaseFailed,
                released.Outcome);
            Assert.Contains(released.Issues, static issue => string.Equals(
                issue.Code,
                "mortal_wound_treatment_procedure_release_conflict",
                StringComparison.Ordinal));
            Assert.Contains(released.Issues, static issue => string.Equals(
                issue.Code,
                "mortal_wound_treatment_publication_terminal_release_failed",
                StringComparison.Ordinal));
            transactionClosed = true;
            await AssertExactTreatmentTransactionBytesAsync(context, before);
            await AssertConfirmedHeldLiveRegistryProbeAsync(context);
            AssertTreatmentPublicationRestartBlocked(context);
        }
        finally
        {
            await context.ReleaseLeaseAsync();
            if (transactionClosed)
                await ((IAsyncDisposable)transaction).DisposeAsync();
        }
    }

    [Fact]
    public async Task ProcedureFatePublicationFailure_DoesNotExpireShieldOrConsumeItem()
    {
        var fault = new AcceptedTreatmentPipelineFault("wound_output", 1);
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault,
            includeEnergyResource: true,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            includeFateShield: true);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        var itemCountBefore = await ReadSelectedNpcItemCountAsync(context);
        var itemTransitionsBefore =
            await CountSelectedItemConsumeTransitionsAsync(context);
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var fateTransitionsBefore =
            await CountProcedureFateTransitionsAsync(context);
        var originalPlan = context.Plan;
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);

        fault.Arm(context);
        var disposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "procedure Fate compensation oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);

        Assert.True(fault.Fired);
        Assert.Equal(
            AcceptedTurnValidationDisposition.RetryablePublicationRearmed,
            disposition);
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        Assert.Equal(itemCountBefore, await ReadSelectedNpcItemCountAsync(context));
        Assert.Equal(
            itemTransitionsBefore,
            await CountSelectedItemConsumeTransitionsAsync(context));
        Assert.Equal(
            resourceSpendsBefore,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(
            fateTransitionsBefore,
            await CountProcedureFateTransitionsAsync(context));
        Assert.Equal(
            new[] { ProcedureFateShieldEffectId },
            await ReadActiveProcedureFateShieldIdsAsync(context));

        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out _,
            out var rearmed));
        Assert.True(rearmed.Success, DescribeValidationIssues(rearmed.Issues));
        Assert.Same(originalPlan, rearmed.Plan);
        AssertProcedureReservationIsLive(context);
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
    }

    [Fact]
    public async Task ProcedureExactRetry_DoesNotDuplicateDiceFateResourceOrHistory()
    {
        var fault = new AcceptedTreatmentPipelineFault("wound_output", 1);
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault,
            includeEnergyResource: true,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            includeFateShield: true);
        var itemTransitionsBefore =
            await CountSelectedItemConsumeTransitionsAsync(context);
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var woundTransitionsBefore = CountProcedureTreatmentTransitions(context);
        var fateTransitionsBefore =
            await CountProcedureFateTransitionsAsync(context);
        var originalPlan = context.Plan;
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);

        fault.Arm(context);
        var first = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "procedure exact retry first attempt",
            snapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            null);
        Assert.Equal(
            AcceptedTurnValidationDisposition.RetryablePublicationRearmed,
            first);
        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out _,
            out var rearmed));
        Assert.True(rearmed.Success, DescribeValidationIssues(rearmed.Issues));
        Assert.Same(originalPlan, rearmed.Plan);
        AssertProcedureReservationIsLive(context);
        await context.ReleaseLeaseAsync();

        var retry = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "procedure exact retry second attempt",
            snapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            null);

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, retry);
        Assert.Equal(1, await ReadSelectedNpcItemCountAsync(context));
        Assert.Equal(
            itemTransitionsBefore + 1,
            await CountSelectedItemConsumeTransitionsAsync(context));
        Assert.Equal(0, await ReadCurrentTreatmentEnergyAsync(context));
        Assert.Equal(
            resourceSpendsBefore + 1,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(
            woundTransitionsBefore + 1,
            CountProcedureTreatmentTransitions(context));
        Assert.Equal(
            // One accepted Fate reaction emits the ordinary effect lifecycle's
            // auditable trigger and terminal consume/expire transitions.
            fateTransitionsBefore + 2,
            await CountProcedureFateTransitionsAsync(context));
        Assert.Empty(await ReadActiveProcedureFateShieldIdsAsync(context));
    }

    [Fact]
    public async Task ProcedureSuccessfulPublication_KeepsUsedDieUnavailableUntilAcceptedStateRebind()
    {
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault: null,
            includeEnergyResource: false);
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);

        var disposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "procedure used-die retention oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);
        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, disposition);

        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(
            new LiveTurnPreparationOptions
            {
                SessionId = "session_t070b5_same_turn_die_rebind",
                RequestId = "request_t070b5_same_turn_die_rebind",
                TurnNumber = HeldTreatmentPipelineContext.Turn,
                PlayerAction = "Attempt to reuse the already spent procedure die.",
                CurrentRealm = "Mortal World",
                PreGeneratedDices1d20 = new[] { 4 }
            });
        await context.RestartAsync(hooks: null);
        var sameTurnState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = sameTurnState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        Assert.Single(recovered.FinalizedRequests);

        var competing = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            sameTurnState,
            history,
            ReadCurrentTreatmentWound(context.FileSystem),
            HeldTreatmentPipelineContext.OperationKey + "_same_turn_reuse",
            HeldTreatmentPipelineContext.RouteId,
            Assert.Single(sameTurnState.Binding.AcceptedEvents).EventRef);
        Assert.False(competing.IsValid);
        Assert.Null(competing.Request);
        Assert.Contains(competing.Issues, static issue =>
            issue.Code?.Contains("_dice_", StringComparison.Ordinal) == true);

        await context.ReleaseLeaseAsync();
        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(
            new LiveTurnPreparationOptions
            {
                SessionId = "session_t070b5_next_turn_die_rebind",
                RequestId = "request_t070b5_next_turn_die_rebind",
                TurnNumber = HeldTreatmentPipelineContext.Turn + 1,
                PlayerAction = "Use the next accepted turn's fresh procedure die.",
                CurrentRealm = "Mortal World",
                PreGeneratedDices1d20 = new[] { 4 }
            });
        await context.RestartAsync(hooks: null);
        var nextTurnState = ExportCurrentTreatmentAcceptedState(context);
        var nextHistory = ReadCurrentTreatmentHistory(context.FileSystem);
        var nextRecovered = nextTurnState.RestorePersistedTreatmentRequests(nextHistory);
        Assert.True(
            nextRecovered.IsValid,
            DescribeValidationIssues(nextRecovered.Issues));
        var next = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            nextTurnState,
            nextHistory,
            ReadCurrentTreatmentWound(context.FileSystem),
            HeldTreatmentPipelineContext.OperationKey + "_next_turn",
            HeldTreatmentPipelineContext.RouteId,
            Assert.Single(nextTurnState.Binding.AcceptedEvents).EventRef);
        Assert.True(next.IsValid, DescribeValidationIssues(next.Issues));
        var nextRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            next.Request);
        AssertProcedureClaims(
            nextRequest,
            includeEnergyResource: false,
            itemScenario: null,
            includeFateShield: false);
        Assert.True(nextRequest.RollbackNewProvisionalClaims(nextTurnState));
    }

    [Fact]
    public async Task ProcedureColdReplay_DoesNotRepublishOrSpendAnySurface()
    {
        await using var context = await CreateProcedureTreatmentPipelineContextAsync(
            fault: null,
            includeEnergyResource: true,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            includeFateShield: true);
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);
        var disposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "procedure cold replay initial publication",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);
        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, disposition);

        var published = await CaptureProcedurePublishedStateBytesAsync(context);
        var itemTransitions = await CountSelectedItemConsumeTransitionsAsync(context);
        var resourceSpends = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var woundTransitions = CountProcedureTreatmentTransitions(context);
        var fateTransitions = await CountProcedureFateTransitionsAsync(context);
        Assert.Empty(await ReadActiveProcedureFateShieldIdsAsync(context));

        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(
            new LiveTurnPreparationOptions
            {
                SessionId = "session_t070b5_cold_replay",
                RequestId = "request_t070b5_cold_replay",
                TurnNumber = HeldTreatmentPipelineContext.Turn,
                PlayerAction = "Replay the already accepted natural-one procedure.",
                CurrentRealm = "Mortal World",
                PreGeneratedDices1d20 = new[] { 1 }
            });
        await context.RestartAsync(hooks: null);
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        var finalized = Assert.Single(recovered.FinalizedRequests);
        var replay = MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            finalized,
            history,
            ReadCurrentTreatmentWound(context.FileSystem),
            acceptedState);

        Assert.Equal("ExactReplay", replay.Disposition);
        Assert.NotNull(replay.ReplayReceipt);
        await AssertProcedurePublishedStateBytesAsync(context, published);
        Assert.Equal(
            itemTransitions,
            await CountSelectedItemConsumeTransitionsAsync(context));
        Assert.Equal(
            resourceSpends,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(woundTransitions, CountProcedureTreatmentTransitions(context));
        Assert.Equal(
            fateTransitions,
            await CountProcedureFateTransitionsAsync(context));
        Assert.Empty(await ReadActiveProcedureFateShieldIdsAsync(context));
    }

    private async Task<HeldTreatmentPipelineContext>
        CreateProcedureTreatmentPipelineContextAsync(
            AcceptedTreatmentPipelineFault? fault,
            bool withRollbackAuthority = false,
            bool includeEnergyResource = true,
            HeldTreatmentItemScenario? itemScenario = null,
            bool includeFateShield = false,
            bool composePublicationPlan = true)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "boe-held-treatment-pipeline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var fileSystem = new FileSystemManager(
            root,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            fault?.Hooks);
        fileSystem.EnsureDirectoryStructure();
        HeldTreatmentPipelineContext? context = null;
        try
        {
            CopyDirectory(TestRepoPaths.BaseSessionRoot, fileSystem.GameSessionPath);
            var wound = CreateProcedureTreatmentWound(
                includeEnergyResource,
                itemScenario is not null);
            await SeedHeldTreatmentAuthorityAsync(fileSystem, wound, itemScenario);
            await SeedProcedurePlayerSkillAsync(fileSystem);
            if (includeFateShield)
                await SeedProcedureFateShieldAsync(fileSystem);
            var prepared = await new LiveTurnPreparationService(fileSystem)
                .PrepareAsync(new LiveTurnPreparationOptions
                {
                    SessionId = "session_t070b5_procedure_pipeline",
                    RequestId = "request_t070b5_procedure_pipeline",
                    TurnNumber = HeldTreatmentPipelineContext.Turn,
                    PlayerAction = includeFateShield
                        ? "Attempt the accepted procedure and invoke the held Fate shield."
                        : "Attempt the accepted no-improvement procedure.",
                    CurrentRealm = "Mortal World",
                    PreGeneratedDices1d20 = includeFateShield
                        ? new[] { 1 }
                        : new[] { 4 }
                });
            Assert.Equal("input/turn_request.json", prepared.TurnRequestPath);
            if (withRollbackAuthority)
            {
                var rollbackEngine = CreateGameEngine(
                    new QueuedConsoleInputSource([]),
                    fileSystem: fileSystem);
                var rollbackSnapshot = await InvokePrivateTaskResultAsync(
                    rollbackEngine,
                    "CreatePreTurnBackup",
                    "t070b5_procedure_terminal_oracle");
                var preparedTurnRequest = JsonSerializer.Deserialize<TurnRequest>(
                    Assert.IsType<string>(await fileSystem.ReadFileAsync(
                        LiveTurnPreparationService.TurnRequestPath)),
                    LiveTurnPreparationService.ManifestJsonOptions);
                Assert.NotNull(preparedTurnRequest);
                _ = await InvokePrivateTaskResultAsync(
                    rollbackEngine,
                    "CreateCanonicalBaselineSnapshotAsync",
                    preparedTurnRequest,
                    rollbackSnapshot,
                    "T070-B.5 procedure terminal rollback oracle");
            }

            var parsedContext = MortalWoundTreatmentAuthority.ParseContext(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["realm"] = "mortal_world",
                    ["targetKind"] = "player",
                    ["targetId"] = "player_current",
                    ["providerKind"] = "npc",
                    ["providerId"] = HeldTreatmentPipelineContext.ProviderId,
                    ["currentLocationId"] = "loc_field_clinic_001"
                }.ToJsonString(),
                "treatmentContext");
            Assert.True(
                parsedContext.IsValid,
                DescribeValidationIssues(parsedContext.Issues));
            context = new HeldTreatmentPipelineContext(
                root,
                fileSystem,
                Assert.IsType<MortalWoundTreatmentAuthority.Context>(
                    parsedContext.Context),
                fault?.Hooks,
                itemScenario);
            await context.AcquireLeaseAsync();

            var acceptedState = ExportCurrentTreatmentAcceptedState(context);
            var history = ReadCurrentTreatmentHistory(fileSystem);
            var currentWound = ReadCurrentTreatmentWound(fileSystem);
            var preparedRequest = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
                acceptedState,
                history,
                currentWound,
                HeldTreatmentPipelineContext.OperationKey,
                HeldTreatmentPipelineContext.RouteId,
                Assert.Single(acceptedState.Binding.AcceptedEvents).EventRef);
            Assert.True(
                preparedRequest.IsValid,
                DescribeValidationIssues(preparedRequest.Issues));
            var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
                preparedRequest.Request);
            AssertProcedureClaims(
                request,
                includeEnergyResource,
                itemScenario,
                includeFateShield);
            var resolved = MortalWoundTreatmentPlanner.CreateProcedureAttempt(
                request,
                history,
                currentWound,
                acceptedState);
            Assert.Equal("Resolved", resolved.Disposition);
            Assert.Empty(resolved.Issues);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
                resolved.Resolution);
            Assert.Equal("failed_attempt", resolution.ResultCategory);
            context.SetResolvedAuthorities(acceptedState, request, resolution);
            var commandRoot = WoundResponseInputComposer
                .ComposeMortalWoundTreatmentCommandRoot(
                    acceptedState.Binding,
                    resolution,
                    HeldTreatmentPipelineContext.FinalSceneText);
            var parsedCommand = WoundResponseInputComposer.ParseCommandRoot(
                JsonSerializer.SerializeToElement(commandRoot));
            Assert.True(
                parsedCommand.Success,
                DescribeValidationIssues(parsedCommand.Issues));
            var recomposed = WoundResponseInputComposer.RecomposeCommandRoot(
                acceptedState.Binding,
                parsedCommand,
                Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.True(
                recomposed.Success,
                DescribeValidationIssues(recomposed.Issues));

            var resourceStateBeforeDistribution =
                await fileSystem.ReadFileBytesAsync(
                    ResourceMaterializationContract.StatePath);
            var proposal = itemScenario?.CreateTreatmentProposal() ??
                           new GameResponse();
            Assert.Null(proposal.Response);
            proposal.Response = HeldTreatmentPipelineContext.FinalSceneText;
            await context.ReleaseLeaseAsync();
            await new StateDistributor(
                    fileSystem,
                    NullLogger<StateDistributor>.Instance)
                .DistributeAsync(proposal, recomposed);
            Assert.Equal(
                resourceStateBeforeDistribution,
                await fileSystem.ReadFileBytesAsync(
                    ResourceMaterializationContract.StatePath));
            await AddGuardianCleanupCommandSurfaceAsync(fileSystem);
            await new ProgressionScheduleService(
                    fileSystem,
                    NullLogger<ProgressionScheduleService>.Instance)
                .EnsureInitializedAsync();
            await context.AcquireLeaseAsync();
            await RestoreProcedureTreatmentAndComposeSameSemanticPlanAsync(
                context,
                includeEnergyResource,
                includeFateShield,
                composePublicationPlan);
            return context;
        }
        catch
        {
            if (context is not null)
                await context.DisposeAsync();
            else
                DeleteHeldTreatmentRootBestEffort(root);
            throw;
        }
    }

    private static async Task RestoreProcedureTreatmentAndComposeSameSemanticPlanAsync(
        HeldTreatmentPipelineContext context,
        bool includeEnergyResource,
        bool includeFateShield,
        bool composePublicationPlan)
    {
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        var request = Assert.Single(recovered.HeldRequests);
        AssertProcedureClaims(
            request,
            includeEnergyResource,
            context.ItemScenario,
            includeFateShield);
        var procedure = RequireProcedureAuthority(request);
        Assert.True(AcceptedTurnAuthorityRegistry
            .HasLiveMortalWoundProcedureReservationAgreement(
                context.FileSystem,
                context.Lease,
                acceptedState,
                procedure));
        var resolved = MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            request,
            history,
            ReadCurrentTreatmentWound(context.FileSystem),
            acceptedState);
        Assert.Equal("Resolved", resolved.Disposition);
        Assert.Empty(resolved.Issues);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
            resolved.Resolution);
        Assert.Equal("failed_attempt", resolution.ResultCategory);
        context.SetResolvedAuthorities(acceptedState, request, resolution);
        if (!composePublicationPlan)
            return;
        var publication = WoundAcceptedTurnPlanner
            .ComposeMortalWoundTreatmentPublication(
                context.FileSystem,
                context.Lease,
                context.CreateMechanicsOnlyTreatmentProposal(),
                acceptedState,
                request,
                resolution);
        Assert.True(
            publication.IsValid,
            DescribeValidationIssues(publication.Issues));
        Assert.Empty(publication.Issues);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(publication.Plan);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out var binding,
            out var cached));
        Assert.True(cached.Success, DescribeValidationIssues(cached.Issues));
        Assert.Same(plan, cached.Plan);
        context.SetCurrentAuthorities(
            acceptedState,
            request,
            resolution,
            plan,
            binding);
    }

    private static void AssertProcedureClaims(
        MortalWoundTreatmentAttemptRequest request,
        bool includeEnergyResource,
        HeldTreatmentItemScenario? itemScenario,
        bool includeFateShield)
    {
        var expectedClaimCount = (includeEnergyResource ? 1 : 0) +
                                 (itemScenario is null ? 0 : 1);
        Assert.Equal(expectedClaimCount, request.ResourceAuthority.Claims.Count);
        if (expectedClaimCount == 0)
        {
            Assert.Equal(
                "not_required",
                request.ResourceAuthority.ReservationDisposition);
            Assert.Null(request.ResourceAuthority.ReservationId);
        }
        else
        {
            Assert.Equal("held", request.ResourceAuthority.ReservationDisposition);
            Assert.NotNull(request.ResourceAuthority.ReservationId);
        }
        if (includeEnergyResource)
        {
            Assert.Contains(request.ResourceAuthority.Claims, static claim =>
                string.Equals(
                    claim.Kind,
                    "resource_quantity",
                    StringComparison.Ordinal) &&
                string.Equals(claim.AuthorityRef, "energy", StringComparison.Ordinal) &&
                string.Equals(claim.OwnerKind, "player", StringComparison.Ordinal) &&
                string.Equals(
                    claim.OwnerId,
                    "player_current",
                    StringComparison.Ordinal) &&
                claim.Quantity == 2);
        }
        if (itemScenario is not null)
        {
            Assert.Contains(request.ResourceAuthority.Claims, static claim =>
                string.Equals(claim.Kind, "item_quantity", StringComparison.Ordinal) &&
                string.Equals(
                    claim.AuthorityRef,
                    HeldTreatmentPipelineContext.SelectedItemId,
                    StringComparison.Ordinal) &&
                string.Equals(claim.OwnerKind, "npc", StringComparison.Ordinal) &&
                string.Equals(
                    claim.OwnerId,
                    HeldTreatmentPipelineContext.ProviderId,
                    StringComparison.Ordinal) &&
                claim.Quantity == 1);
        }

        var procedure = RequireProcedureAuthority(request);
        Assert.Equal("normal", procedure.RollMode);
        Assert.Equal(new[] { 0 }, procedure.SourceIndices);
        Assert.Equal(includeFateShield ? 1 : 4, procedure.NaturalRoll);
        if (includeFateShield)
        {
            var fate = Assert.IsType<MortalWoundPreparedCriticalReaction>(
                procedure.PreparedCriticalReaction);
            Assert.Equal(ProcedureFateShieldEffectId, fate.EffectId);
            Assert.Equal("fate_shield_on_critical_failure", fate.TriggerId);
        }
        else
        {
            Assert.Null(procedure.PreparedCriticalReaction);
        }
    }

    private static MortalWoundProcedureCheckAuthority RequireProcedureAuthority(
        MortalWoundTreatmentAttemptRequest request) =>
        Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);

    private static MortalWoundTreatmentAcceptedStateAuthority
        AssertProcedureReservationIsLive(
        HeldTreatmentPipelineContext context)
    {
        // Accepted-state authorities are deliberately bound to one canonical
        // write lease. Re-export the unchanged state under the caller's current
        // lease before probing the long-lived registry claims.
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        Assert.True(
            AcceptedTurnAuthorityRegistry.HasLiveMortalWoundProcedureReservationAgreement(
                context.FileSystem,
                context.Lease,
                acceptedState,
                RequireProcedureAuthority(context.Request)));
        return acceptedState;
    }

    private static void ReleaseOnlyProcedureClaim(
        HeldTreatmentPipelineContext context,
        string claimAxis)
    {
        var procedure = RequireProcedureAuthority(context.Request);
        var dice = Assert.IsType<MortalWoundProcedureDiceReservation>(
            typeof(MortalWoundProcedureCheckAuthority)
                .GetField(
                    "_diceReservation",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(procedure));
        var state = typeof(AcceptedTurnAuthorityRegistry)
            .GetMethod(
                "GetState",
                BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { context.FileSystem, context.Lease });
        Assert.NotNull(state);

        switch (claimAxis)
        {
            case "dice":
                var diceRegistry = Assert.IsType<
                    MortalWoundProcedureDiceReservationRegistry>(
                        state.GetType()
                            .GetField(
                                "_procedureDice",
                                BindingFlags.Instance | BindingFlags.NonPublic)!
                            .GetValue(state));
                diceRegistry.ReleaseUnchecked(dice);
                break;
            case "fate":
                var agreement = Assert.IsType<
                    MortalWoundCriticalReactionReservationAgreement>(
                        typeof(MortalWoundProcedureCheckAuthority)
                            .GetField(
                                "_criticalReactionAgreement",
                                BindingFlags.Instance | BindingFlags.NonPublic)!
                            .GetValue(procedure));
                var reactionRegistry = Assert.IsType<
                    MortalWoundCriticalReactionReservationRegistry>(
                        state.GetType()
                            .GetField(
                                "_criticalReactions",
                                BindingFlags.Instance | BindingFlags.NonPublic)!
                            .GetValue(state));
                reactionRegistry.ReleaseUnchecked(dice, agreement);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(claimAxis),
                    claimAxis,
                    "Expected dice or fate claim axis.");
        }

        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        Assert.False(AcceptedTurnAuthorityRegistry
            .HasLiveMortalWoundProcedureReservationAgreement(
                context.FileSystem,
                context.Lease,
                acceptedState,
                procedure));
    }

    private static JsonObject CreateProcedureTreatmentWound(
        bool includeEnergyResource,
        bool includeItemRequirement)
    {
        var wound = CreateGuaranteedResourceTreatmentWound();
        var requirements = new JsonArray(new JsonObject
        {
            ["kind"] = "skill_tier",
            ["capabilityRef"] = "field_medicine",
            ["minimumTier"] = 2,
            ["actorRole"] = "target"
        });
        var mutations = new JsonArray();
        if (includeEnergyResource)
        {
            requirements.Add(new JsonObject
            {
                ["kind"] = "resource_quantity",
                ["resourceRef"] = "energy",
                ["quantity"] = 2,
                ["ownerRole"] = "target"
            });
            mutations.Add(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = 1
            });
        }
        if (includeItemRequirement)
        {
            var requirementIndex = requirements.Count;
            requirements.Add(new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = HeldTreatmentPipelineContext.SelectedItemId,
                ["quantity"] = 1,
                ["ownerRole"] = "provider"
            });
            mutations.Add(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = requirementIndex
            });
        }

        var consumeOn = mutations.Count == 0
            ? new JsonArray()
            : new JsonArray("success", "partial_success", "failed_attempt");
        wound["treatment"]!["routes"] = new JsonArray(new JsonObject
        {
            ["routeId"] = HeldTreatmentPipelineContext.RouteId,
            ["displayName"] = "T070-B.5 no-improvement procedure",
            ["visibility"] = "known_to_player",
            ["mode"] = "procedure",
            ["requirements"] = requirements,
            ["resourcePolicy"] = new JsonObject
            {
                ["reserveBeforeResolution"] = true,
                ["consumeOn"] = consumeOn,
                ["refundOn"] = new JsonArray(
                    "cancelled",
                    "validation_failed",
                    "rolled_back"),
                ["mutations"] = mutations
            },
            ["resolution"] = new JsonObject
            {
                ["formulaKey"] = "mortal_wound_procedure_v1",
                ["difficulty"] = 30,
                ["rollSource"] = "accepted_d20",
                ["criticalPolicy"] = "natural_20_first_natural_1_last",
                ["modifierSource"] = new JsonObject
                {
                    ["kind"] = "resolved_skill_tier",
                    ["requirementIndex"] = 0
                }
            },
            ["outcomes"] = new JsonArray(
                ProcedureBand("success", 5, null, "success", "stabilize"),
                ProcedureBand(
                    "partial",
                    0,
                    4,
                    "partial_success",
                    "stabilize"),
                ProcedureBand(
                    "failed",
                    -4,
                    -1,
                    "failed_attempt",
                    "no_improvement"),
                ProcedureBand(
                    "critical",
                    null,
                    -5,
                    "failed_attempt",
                    "no_improvement")),
            ["interruption"] = null
        });
        wound["treatment"]!["knownRouteIds"] = new JsonArray(
            HeldTreatmentPipelineContext.RouteId);
        return wound;
    }

    private static JsonObject ProcedureBand(
        string bandId,
        int? minimum,
        int? maximum,
        string category,
        string operation) => new()
    {
        ["bandId"] = bandId,
        ["minimumMargin"] = minimum,
        ["maximumMargin"] = maximum,
        ["category"] = category,
        ["result"] = new JsonArray(new JsonObject { ["kind"] = operation })
    };

    private static async Task SeedProcedurePlayerSkillAsync(
        FileSystemManager fileSystem)
    {
        var skill = CreateGuaranteedTreatmentSkill().DeepClone().AsObject();
        skill["skillId"] = "skill_t070b5_field_medicine";
        skill["displayName"] = "Field Medicine";
        skill["skillName"] = "Field Medicine";
        skill["skillDescription"] =
            "Provides exact accepted procedure-check authority.";
        skill["mortalWoundTreatmentCapabilities"]![0]!["capabilityRef"] =
            "field_medicine";
        await fileSystem.WriteFileAtomicAsync(
            "game_state/player/skills_active.json",
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(skill)
            }.ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            "game_state/player/skill_mastery.json",
            new JsonObject
            {
                ["skillMasteryChanges"] = new JsonArray(new JsonObject
                {
                    ["skillName"] = "Field Medicine",
                    ["newMasteryLevel"] = 3,
                    ["newCurrentMasteryProgress"] = 0,
                    ["newMasteryProgressNeeded"] = 100,
                    ["masteryLeveledUp"] = false
                })
            }.ToJsonString());
    }

    private static async Task SeedProcedureFateShieldAsync(
        FileSystemManager fileSystem)
    {
        var playerEffects = JsonNode.Parse(Assert.IsType<string>(
                await fileSystem.ReadFileAsync(EffectCarrierCatalog.PlayerPath)))!
            .AsObject();
        var effects = playerEffects["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .Select(static effect => effect.DeepClone().AsObject())
            .ToList();
        var fate = CreateProcedureFateShield();
        effects.Add(fate);
        playerEffects["activeEffects"] = new JsonArray(
            effects.Select(static effect => (JsonNode)effect).ToArray());
        var identity = EffectMaterializationTestFixture.CreateIdentityIndex(
            effects.ToArray());
        var fateEntry = Assert.Single(
            identity["entries"]!.AsArray().OfType<JsonObject>(),
            static entry => string.Equals(
                entry["effectId"]?.GetValue<string>(),
                ProcedureFateShieldEffectId,
                StringComparison.Ordinal));
        fateEntry["createdAtTurn"] = 30;
        var apply = Assert.IsType<JsonObject>(Assert.Single(
            fateEntry["transitions"]!.AsArray()));
        apply["transitionId"] = "effect_transition_t070b5_fate_seed";
        apply["turn"] = 30;
        apply["eventRef"] = "turn_30:t070b5_fate_seed:1";
        await fileSystem.WriteFileAtomicAsync(
            EffectCarrierCatalog.PlayerPath,
            playerEffects.ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            EffectIdentityState.StatePath,
            identity.ToJsonString());
    }

    private static JsonObject CreateProcedureFateShield()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "player",
            "event_reaction");
        effect["effectId"] = ProcedureFateShieldEffectId;
        effect["display"] = new JsonObject
        {
            ["name"] = "Щит Судьбы",
            ["description"] =
                "Чернила Судьбы смягчают следующий критический провал.",
            ["category"] = "buff",
            ["visibility"] = "visible",
            ["sourceLabel"] = "Чернильное Перо"
        };
        effect["source"] = new JsonObject
        {
            ["kind"] = EffectBuiltInSourceCatalog.FateShieldSourceKind,
            ["sourceId"] = EffectBuiltInSourceCatalog.FateShieldSourceId,
            ["definitionKey"] = EffectBuiltInSourceCatalog.FateShieldDefinitionKey
        };
        effect["components"] = new JsonArray(new JsonObject
        {
            ["componentId"] = "fate_shield_reaction",
            ["profile"] = "event_reaction",
            ["priority"] = -100,
            ["payload"] = new JsonObject
            {
                ["eventType"] = "owner_critical_failure",
                ["resultKind"] = "event_outcome",
                ["originalOutcome"] = "critical_failure",
                ["resolvedOutcome"] = "failure",
                ["dependency"] = "before_current_event",
                ["maxExpansion"] = 1
            }
        });
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = 1,
            ["consumingTriggerIds"] = new JsonArray(
                "fate_shield_on_critical_failure"),
            ["displayText"] = "До следующего критического провала"
        };
        effect["stacking"] = new JsonObject
        {
            ["stackKey"] = "ink-feather-fate-shield",
            ["policy"] = "independent",
            ["maxStacks"] = 1,
            ["currentStacks"] = 1,
            ["refreshMode"] = null,
            ["mergeRule"] = null
        };
        effect["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "fate_shield_on_critical_failure",
            ["eventType"] = "owner_critical_failure",
            ["priority"] = -100,
            ["componentIds"] = new JsonArray("fate_shield_reaction"),
            ["consumeUses"] = true,
            ["resolutionMode"] = "deterministic"
        });
        effect["removal"] = new JsonObject
        {
            ["dispelCategories"] = new JsonArray("fate"),
            ["cureKinds"] = new JsonArray(),
            ["onSourceLoss"] = "no_change",
            ["onConditionLoss"] = null,
            ["manualAuthorities"] = new JsonArray()
        };
        effect["links"] = new JsonArray();
        effect["chronology"] = new JsonObject
        {
            ["createdAtTurn"] = 30,
            ["createdEventRef"] = "turn_30:t070b5_fate_seed:1",
            ["lastTransitionId"] = "effect_transition_t070b5_fate_seed",
            ["lastTransitionTurn"] = 30
        };
        return effect;
    }

    private static int CountProcedureTreatmentTransitions(
        HeldTreatmentPipelineContext context) =>
        ReadCurrentTreatmentHistory(context.FileSystem).State!.Transitions.Count(
            static transition => string.Equals(
                transition.Kind,
                "treat",
                StringComparison.Ordinal));

    private static async Task<int> CountProcedureFateTransitionsAsync(
        HeldTreatmentPipelineContext context)
    {
        var root = ParseJsonObjectBytes(Assert.IsType<byte[]>(
            await context.ReadFileBytesAsync(EffectIdentityState.StatePath)));
        var entry = root["entries"]!.AsArray().OfType<JsonObject>()
            .SingleOrDefault(static candidate => string.Equals(
                candidate["effectId"]?.GetValue<string>(),
                ProcedureFateShieldEffectId,
                StringComparison.Ordinal));
        return entry?["transitions"]?.AsArray().Count ?? 0;
    }

    private static async Task<IReadOnlyList<string>>
        ReadActiveProcedureFateShieldIdsAsync(
            HeldTreatmentPipelineContext context)
    {
        var root = ParseJsonObjectBytes(Assert.IsType<byte[]>(
            await context.ReadFileBytesAsync(EffectCarrierCatalog.PlayerPath)));
        return root["activeEffects"]!.AsArray().OfType<JsonObject>()
            .Select(static effect => effect["effectId"]?.GetValue<string>())
            .Where(static effectId => string.Equals(
                effectId,
                ProcedureFateShieldEffectId,
                StringComparison.Ordinal))
            .Cast<string>()
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<string, ExactFileImage>>
        CaptureProcedurePublishedStateBytesAsync(
            HeldTreatmentPipelineContext context)
    {
        var paths = new[]
        {
            NpcCoreChangesContract.NpcCorePath,
            MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            WoundCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            EffectCarrierCatalog.PlayerPath,
            EffectIdentityState.StatePath,
            AcceptedMechanicsPlan.WoundCommandPath,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
            "output/narrative_response.json"
        };
        var images = new Dictionary<string, ExactFileImage>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var bytes = await context.ReadFileBytesAsync(path);
            images.Add(path, new ExactFileImage(bytes is not null, bytes?.ToArray()));
        }
        return images;
    }

    private static async Task AssertProcedurePublishedStateBytesAsync(
        HeldTreatmentPipelineContext context,
        IReadOnlyDictionary<string, ExactFileImage> expected)
    {
        foreach (var pair in expected)
        {
            var actual = await context.ReadFileBytesAsync(pair.Key);
            Assert.Equal(pair.Value.Exists, actual is not null);
            if (pair.Value.Exists)
                Assert.Equal(pair.Value.Bytes, actual);
        }
    }
}
