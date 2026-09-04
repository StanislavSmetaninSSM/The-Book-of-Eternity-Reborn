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
    [Theory]
    [InlineData("procedure", "success", 2, 1, 20)]
    [InlineData("procedure", "partial_success", 2, 1, 10)]
    [InlineData("procedure", "failed_attempt", 2, 1, 6)]
    [InlineData("procedure", "success", 1, 2, 20)]
    [InlineData("guaranteed", "success", 2, 1, 20)]
    [InlineData("procedure", "success", 0, 1, 20)]
    public async Task SeverityReduction_CompleteOutcomeMatrixPublishesFreshCanonicalGeneration(
        string mode,
        string expectedCategory,
        int rootCount,
        int reductionSteps,
        int acceptedDie)
    {
        await using var context = await CreateSeverityReductionPipelineContextAsync(
            fault: null,
            rootCount,
            reductionSteps,
            acceptedDie,
            includeEnergyResource: true,
            mode,
            expectedCategory);
        var beforeWound = ReadCurrentTreatmentWound(context.FileSystem);
        var priorRootsByDefinition = beforeWound.Consequences.OwnedEffectSources
            .RootBindings.ToDictionary(
                static binding => binding.DefinitionKey,
                static binding => binding.EffectId,
                StringComparer.Ordinal);
        var priorRootIds = priorRootsByDefinition.Values.ToArray();
        var unrelatedEffectBefore = ReadPlayerEffectById(
            context.FileSystem,
            ProcedureFateShieldEffectId).DeepClone();
        var resourceHistoryBefore = await ReadTreatmentResourceHistoryAsync(
            context.FileSystem);
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            resourceHistoryBefore);
        var unselectedSpendsBefore = CountUnselectedSeverityResourceSpends(
            resourceHistoryBefore);
        var transitionsBefore = CountProcedureTreatmentTransitions(context);
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);

        var disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "severity-reduction compound publication oracle",
            snapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            null);

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, disposition);
        var published = ReadCurrentTreatmentWound(context.FileSystem);
        var currentRootIds = published.Consequences.OwnedEffectSources.RootBindings
            .Select(static binding => binding.EffectId)
            .ToArray();
        Assert.Equal(reductionSteps == 1 ? "II" : "I", published.Severity.Value);
        Assert.Equal(3 - reductionSteps, published.Severity.Rank);
        Assert.Equal(rootCount, currentRootIds.Length);
        Assert.Empty(priorRootIds.Intersect(currentRootIds, StringComparer.Ordinal));
        var identity = ReadSeverityEffectIdentity(context.FileSystem);
        foreach (var priorRootId in priorRootIds)
        {
            AssertSeverityGenerationExpired(
                RequireSeverityEffectIdentity(identity, priorRootId));
        }
        var effects = ReadSeverityTargetEffects(
                context.FileSystem,
                "player",
                "player_current")
            .Where(static effect => string.Equals(
                effect["source"]?["kind"]?.GetValue<string>(),
                "wound",
                StringComparison.Ordinal))
            .ToDictionary(
                static effect => effect["effectId"]!.GetValue<string>(),
                StringComparer.Ordinal);
        Assert.Equal(rootCount, effects.Count);
        foreach (var binding in published.Consequences.OwnedEffectSources
                     .RootBindings)
        {
            var effect = effects[binding.EffectId];
            Assert.Equal(
                HeldTreatmentPipelineContext.WoundId,
                effect["source"]?["sourceId"]?.GetValue<string>());
            Assert.Equal(
                binding.DefinitionKey,
                effect["source"]?["definitionKey"]?.GetValue<string>());
            var active = RequireSeverityEffectIdentity(identity, binding.EffectId);
            Assert.Equal("active", active.State);
            Assert.Equal(
                HeldTreatmentPipelineContext.WoundId,
                active.Source["sourceId"]?.GetValue<string>());
            Assert.Equal(
                binding.DefinitionKey,
                active.Source["definitionKey"]?.GetValue<string>());
            AssertSeverityGenerationCreatedFrom(
                active,
                priorRootsByDefinition[binding.DefinitionKey]);
        }
        Assert.True(JsonNode.DeepEquals(
            unrelatedEffectBefore,
            ReadPlayerEffectById(
                context.FileSystem,
                ProcedureFateShieldEffectId)));
        Assert.Equal(
            unselectedSpendsBefore,
            CountUnselectedSeverityResourceSpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(
            string.Equals(expectedCategory, "success", StringComparison.Ordinal)
                ? new[] { HeldTreatmentPipelineContext.RouteId }
                : Array.Empty<string>(),
            published.Treatment.CompletedRouteIds);
        Assert.Equal(
            resourceSpendsBefore + 1,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(transitionsBefore + 1, CountProcedureTreatmentTransitions(context));
        Assert.False(await ContainsCurrentExactTreatmentRequestAsync(context));
        var output = JsonNode.Parse(Assert.IsType<string>(
            await context.FileSystem.ReadFileAsync(
                "output/narrative_response.json")))!.AsObject();
        Assert.Equal(
            HeldTreatmentPipelineContext.FinalSceneText,
            output["response"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("wound_post_seal")]
    [InlineData("wound_output")]
    public async Task SeverityReduction_PostPublicationWriteFailureRestoresEveryByteAndExactRetryReusesAuthority(
        string boundary)
    {
        var fault = new AcceptedTreatmentPipelineFault(boundary, 1);
        await using var context = await CreateSeverityReductionPipelineContextAsync(
            fault,
            rootCount: 2,
            reductionSteps: 1,
            acceptedDie: 20,
            includeEnergyResource: true);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        var pendingBefore = await context.ReadFileBytesAsync(
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        var originalPlan = context.Plan;
        var originalEffectIds = Assert.IsType<EffectAcceptedTurnPlan>(
                originalPlan.EffectPlan)
            .AllocatedEffectIds
            .ToArray();
        var originalBindingFingerprint =
            AcceptedMechanicsPlanFingerprints.ComputeInput(context.OriginalBinding);
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var transitionsBefore = CountProcedureTreatmentTransitions(context);
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);

        fault.Arm(context);
        var firstDisposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                $"severity reduction {boundary} rollback oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);

        Assert.True(
            fault.Fired,
            $"Observed={string.Join(", ", fault.ObservedPhases)}");
        Assert.Equal(
            AcceptedTurnValidationDisposition.RetryablePublicationRearmed,
            firstDisposition);
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        AssertExactOptionalBytes(
            pendingBefore,
            await context.ReadFileBytesAsync(
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
        Assert.True(await ContainsCurrentExactTreatmentRequestAsync(context));
        Assert.Equal(
            resourceSpendsBefore,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(transitionsBefore, CountProcedureTreatmentTransitions(context));

        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out var rearmedBinding,
            out var rearmed));
        Assert.True(rearmed.Success, DescribeValidationIssues(rearmed.Issues));
        Assert.Same(originalPlan, rearmed.Plan);
        Assert.Equal(
            originalEffectIds,
            Assert.IsType<EffectAcceptedTurnPlan>(rearmed.Plan!.EffectPlan)
                .AllocatedEffectIds);
        Assert.Equal(
            originalBindingFingerprint,
            AcceptedMechanicsPlanFingerprints.ComputeInput(rearmedBinding));
        AssertProcedureReservationIsLive(context);
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        await context.ReleaseLeaseAsync();

        var retryDisposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                $"severity reduction {boundary} exact retry oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, retryDisposition);
        var published = ReadCurrentTreatmentWound(context.FileSystem);
        Assert.Equal("II", published.Severity.Value);
        Assert.Equal(
            originalEffectIds.OrderBy(static value => value, StringComparer.Ordinal),
            published.Consequences.OwnedEffectSources.RootBindings
                .Select(static binding => binding.EffectId)
                .OrderBy(static value => value, StringComparer.Ordinal));
        Assert.Equal(
            resourceSpendsBefore + 1,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(transitionsBefore + 1, CountProcedureTreatmentTransitions(context));
        Assert.False(await ContainsCurrentExactTreatmentRequestAsync(context));
        await context.AcquireLeaseAsync();
        var releasedState = ExportCurrentTreatmentAcceptedState(context);
        Assert.False(AcceptedTurnAuthorityRegistry
            .HasLiveMortalWoundProcedureReservationAgreement(
                context.FileSystem,
                context.Lease,
                releasedState,
                RequireProcedureAuthority(context.Request)));
    }

    [Fact]
    public async Task SeverityReduction_ChangedPlanDuringCompletionRemainsProtectedAndRequiresRestart()
    {
        var fault = new AcceptedTreatmentPipelineFault(
            "transaction_commit_conflict",
            1);
        await using var context = await CreateSeverityReductionPipelineContextAsync(
            fault,
            rootCount: 2,
            reductionSteps: 1,
            acceptedDie: 20,
            includeEnergyResource: true);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        var pendingBefore = await context.ReadFileBytesAsync(
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);

        fault.Arm(context);
        var disposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "severity reduction changed-plan completion oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);

        Assert.True(
            fault.Fired,
            $"Observed={string.Join(", ", fault.ObservedPhases)}");
        Assert.Equal(
            AcceptedTurnValidationDisposition.RetryablePublicationHeldBlocked,
            disposition);
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        AssertExactOptionalBytes(
            pendingBefore,
            await context.ReadFileBytesAsync(
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
        Assert.True(await ContainsCurrentExactTreatmentRequestAsync(context));

        await context.AcquireLeaseAsync();
        AssertProcedureReservationIsLive(context);
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
        AssertTreatmentPublicationRestartBlocked(context);

        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var competing = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            acceptedState,
            history,
            ReadCurrentTreatmentWound(context.FileSystem),
            HeldTreatmentPipelineContext.OperationKey + "_competing",
            HeldTreatmentPipelineContext.RouteId,
            Assert.Single(acceptedState.Binding.AcceptedEvents).EventRef);
        Assert.False(competing.IsValid);
        Assert.Null(competing.Request);
        Assert.Equal(
            "mortal_wound_treatment_procedure_reservation_authority_invalid",
            Assert.Single(competing.Issues).Code);
    }

    [Fact]
    public async Task SeverityReduction_ColdReplayFromCopiedCanonicalRootsIsExactAndWriteFree()
    {
        await using var context = await CreateSeverityReductionPipelineContextAsync(
            fault: null,
            rootCount: 2,
            reductionSteps: 1,
            acceptedDie: 20,
            includeEnergyResource: true);
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);
        var disposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "severity reduction cold replay initial publication",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);
        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, disposition);
        var published = await CaptureProcedurePublishedStateBytesAsync(context);
        var resourceSpends = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var transitions = CountProcedureTreatmentTransitions(context);

        var coldRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-held-treatment-pipeline-severity-cold-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coldRoot);
        var coldFileSystem = new FileSystemManager(
            coldRoot,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance);
        coldFileSystem.EnsureDirectoryStructure();
        CopyDirectory(
            context.FileSystem.GameSessionPath,
            coldFileSystem.GameSessionPath);
        await new LiveTurnPreparationService(coldFileSystem).PrepareAsync(
            new LiveTurnPreparationOptions
            {
                SessionId = "session_t070b6_severity_cold_replay",
                RequestId = "request_t070b6_severity_cold_replay",
                TurnNumber = HeldTreatmentPipelineContext.Turn,
                PlayerAction = "Replay the accepted severity reduction.",
                CurrentRealm = "Mortal World",
                PreGeneratedDices1d20 = new[] { 20 }
            });
        await using var coldContext = new HeldTreatmentPipelineContext(
            coldRoot,
            coldFileSystem,
            ParseSeverityTreatmentContext(),
            hooks: null,
            itemScenario: null);
        await coldContext.AcquireLeaseAsync();
        AcceptedTurnAuthorityRegistry.InvalidateAcceptedTurnValidated(
            coldContext.FileSystem,
            coldContext.Lease);
        var copied = await CaptureProcedurePublishedStateBytesAsync(coldContext);
        AssertPublishedImagesEqual(published, copied);

        var acceptedState = ExportCurrentTreatmentAcceptedState(coldContext);
        var history = ReadCurrentTreatmentHistory(coldContext.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        var finalized = Assert.Single(recovered.FinalizedRequests);
        var replay = MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            finalized,
            history,
            ReadCurrentTreatmentWound(coldContext.FileSystem),
            acceptedState);

        Assert.Equal("ExactReplay", replay.Disposition);
        Assert.NotNull(replay.ReplayReceipt);
        await AssertProcedurePublishedStateBytesAsync(coldContext, copied);
        Assert.Equal(
            resourceSpends,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(coldContext.FileSystem)));
        Assert.Equal(transitions, CountProcedureTreatmentTransitions(coldContext));
    }

    [Theory]
    [InlineData("player", "player_current")]
    [InlineData("npc", "wounded_npc_t070b6")]
    [InlineData("combatant", "wounded_combatant_t070b6")]
    public async Task SeverityReduction_OwnerCarrierParityUsesAcceptedNearbyCanonicalTarget(
        string targetKind,
        string targetId)
    {
        await using var context = await CreateSeverityReductionPipelineContextAsync(
            fault: null,
            rootCount: 1,
            reductionSteps: 1,
            acceptedDie: 20,
            includeEnergyResource: false,
            targetKind: targetKind,
            targetId: targetId);
        Assert.Contains(
            context.AcceptedState.RequirementSnapshot.Actors,
            actor => string.Equals(actor.ActorKind, targetKind, StringComparison.Ordinal) &&
                     string.Equals(actor.ActorId, targetId, StringComparison.Ordinal) &&
                     actor.Reachable);
        Assert.Equal(targetKind, context.AcceptedState.RequirementContext.TargetKind);
        Assert.Equal(targetId, context.AcceptedState.RequirementContext.TargetId);
        var beforeWound = ReadSeverityTreatmentWound(
            context.FileSystem,
            targetKind,
            targetId);
        var oldRootIds = beforeWound.Consequences.OwnedEffectSources.RootBindings
            .Select(static binding => binding.EffectId)
            .ToArray();
        var unrelatedBefore = ReadPlayerEffectById(
            context.FileSystem,
            ProcedureFateShieldEffectId);
        var playerWoundsBefore = await context.FileSystem.ReadFileBytesAsync(
            WoundCarrierCatalog.PlayerPath);
        var playerEffectsBefore = await context.FileSystem.ReadFileBytesAsync(
            EffectCarrierCatalog.PlayerPath);
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);

        var disposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                $"severity reduction {targetKind} carrier parity oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);

        var postDispositionIssues = disposition ==
                                    AcceptedTurnValidationDisposition.Accepted
            ? Array.Empty<ValidationIssue>()
            : (await new ValidationService(
                    context.FileSystem,
                    NullLogger<ValidationService>.Instance)
                .ValidateGameStateAsync())
                .Where(static issue => issue.Severity == IssueSeverity.Error)
                .ToArray();
        Assert.True(
            disposition == AcceptedTurnValidationDisposition.Accepted,
            $"Disposition={disposition}; " +
            DescribeValidationIssues(postDispositionIssues));
        var published = ReadSeverityTreatmentWound(
            context.FileSystem,
            targetKind,
            targetId);
        Assert.Equal(targetKind, published.Owner.OwnerKind);
        Assert.Equal(targetId, published.Owner.OwnerId);
        Assert.Equal("II", published.Severity.Value);
        var newRootIds = published.Consequences.OwnedEffectSources.RootBindings
            .Select(static binding => binding.EffectId)
            .ToArray();
        Assert.Empty(oldRootIds.Intersect(newRootIds, StringComparer.Ordinal));
        var woundEffects = ReadSeverityTargetEffects(
                context.FileSystem,
                targetKind,
                targetId)
            .Where(static effect => string.Equals(
                effect["source"]?["kind"]?.GetValue<string>(),
                "wound",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            newRootIds.OrderBy(static value => value, StringComparer.Ordinal),
            woundEffects
                .Select(static effect => effect["effectId"]!.GetValue<string>())
                .OrderBy(static value => value, StringComparer.Ordinal));
        Assert.All(woundEffects, effect =>
        {
            Assert.Equal(
                targetKind == "player" ? "player" :
                targetKind == "npc" ? "npc" : "combatant",
                effect["target"]?["kind"]?.GetValue<string>());
            Assert.Equal(
                targetId,
                effect["target"]?["targetId"]?.GetValue<string>());
        });
        Assert.True(JsonNode.DeepEquals(
            unrelatedBefore,
            ReadPlayerEffectById(
                context.FileSystem,
                ProcedureFateShieldEffectId)));
        if (!string.Equals(targetKind, "player", StringComparison.Ordinal))
        {
            Assert.Equal(
                playerWoundsBefore,
                await context.FileSystem.ReadFileBytesAsync(
                    WoundCarrierCatalog.PlayerPath));
            Assert.Equal(
                playerEffectsBefore,
                await context.FileSystem.ReadFileBytesAsync(
                    EffectCarrierCatalog.PlayerPath));
        }
    }

    [Fact]
    public async Task SeverityReduction_TwoConsecutiveAttemptsRetireOnlyCurrentGenerationAndColdReplayIsExact()
    {
        const string secondOperationKey =
            HeldTreatmentPipelineContext.OperationKey + "_second_generation";
        await using var context = await CreateSeverityReductionPipelineContextAsync(
            fault: null,
            rootCount: 1,
            reductionSteps: 1,
            acceptedDie: 10,
            includeEnergyResource: true,
            expectedCategory: "partial_success",
            currentEnergy: 4);
        var originalRootId = Assert.Single(ReadCurrentTreatmentWound(
                context.FileSystem)
            .Consequences.OwnedEffectSources.RootBindings)
            .EffectId;
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var woundTransitionsBefore = CountProcedureTreatmentTransitions(context);
        await context.ReleaseLeaseAsync();
        var (firstEngine, firstSnapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);

        var firstDisposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                firstEngine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "severity reduction first generation publication",
                firstSnapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, firstDisposition);
        var firstPublished = ReadCurrentTreatmentWound(context.FileSystem);
        Assert.Equal("II", firstPublished.Severity.Value);
        var firstFreshRootId = Assert.Single(
            firstPublished.Consequences.OwnedEffectSources.RootBindings).EffectId;
        Assert.NotEqual(originalRootId, firstFreshRootId);
        var identityAfterFirst = ReadSeverityEffectIdentity(context.FileSystem);
        var originalTerminalAfterFirst = RequireSeverityEffectIdentity(
            identityAfterFirst,
            originalRootId);
        AssertSeverityGenerationExpired(originalTerminalAfterFirst);
        var firstFreshAfterFirst = RequireSeverityEffectIdentity(
            identityAfterFirst,
            firstFreshRootId);
        Assert.Equal("active", firstFreshAfterFirst.State);
        AssertSeverityGenerationCreatedFrom(
            firstFreshAfterFirst,
            originalRootId);
        var preservedOriginalHistory = originalTerminalAfterFirst.Raw.DeepClone();

        await PrepareNextSeverityReductionAttemptAsync(
            context,
            secondOperationKey,
            acceptedDie: 20,
            expectedCategory: "success");
        var secondAllocatedEffectIds = Assert.IsType<EffectAcceptedTurnPlan>(
                context.Plan.EffectPlan)
            .AllocatedEffectIds
            .ToArray();
        await context.ReleaseLeaseAsync();
        var (secondEngine, secondSnapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);

        var secondDisposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                secondEngine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "severity reduction second generation publication",
                secondSnapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn + 1,
                null);

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, secondDisposition);
        var secondPublished = ReadCurrentTreatmentWound(context.FileSystem);
        Assert.Equal("I", secondPublished.Severity.Value);
        var secondFreshRootId = Assert.Single(
            secondPublished.Consequences.OwnedEffectSources.RootBindings).EffectId;
        Assert.Equal(Assert.Single(secondAllocatedEffectIds), secondFreshRootId);
        Assert.Equal(
            3,
            new[] { originalRootId, firstFreshRootId, secondFreshRootId }
                .Distinct(StringComparer.Ordinal)
                .Count());

        var identityAfterSecond = ReadSeverityEffectIdentity(context.FileSystem);
        var originalTerminalAfterSecond = RequireSeverityEffectIdentity(
            identityAfterSecond,
            originalRootId);
        Assert.True(JsonNode.DeepEquals(
            preservedOriginalHistory,
            originalTerminalAfterSecond.Raw));
        AssertSeverityGenerationExpired(
            RequireSeverityEffectIdentity(identityAfterSecond, firstFreshRootId));
        var secondFreshAfterSecond = RequireSeverityEffectIdentity(
            identityAfterSecond,
            secondFreshRootId);
        Assert.Equal("active", secondFreshAfterSecond.State);
        AssertSeverityGenerationCreatedFrom(
            secondFreshAfterSecond,
            firstFreshRootId);
        Assert.Equal(
            resourceSpendsBefore + 2,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(
            woundTransitionsBefore + 2,
            CountProcedureTreatmentTransitions(context));
        Assert.Equal(0, await ReadCurrentTreatmentEnergyAsync(context));
        Assert.False(await ContainsCurrentExactTreatmentRequestAsync(context));

        await context.AcquireLeaseAsync();
        var releasedState = ExportCurrentTreatmentAcceptedState(context);
        Assert.False(AcceptedTurnAuthorityRegistry
            .HasLiveMortalWoundProcedureReservationAgreement(
                context.FileSystem,
                context.Lease,
                releasedState,
                RequireProcedureAuthority(context.Request)));
        await context.ReleaseLeaseAsync();

        var publishedBytes = await CaptureProcedurePublishedStateBytesAsync(context);
        var coldRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-held-treatment-pipeline-severity-repeat-cold-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(coldRoot);
        var coldFileSystem = new FileSystemManager(
            coldRoot,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance);
        coldFileSystem.EnsureDirectoryStructure();
        CopyDirectory(context.FileSystem.GameSessionPath, coldFileSystem.GameSessionPath);
        await new LiveTurnPreparationService(coldFileSystem).PrepareAsync(
            new LiveTurnPreparationOptions
            {
                SessionId = "session_t070b6_repeat_cold_replay",
                RequestId = "request_t070b6_repeat_cold_replay",
                TurnNumber = HeldTreatmentPipelineContext.Turn + 1,
                PlayerAction = "Replay the second accepted severity reduction.",
                CurrentRealm = "Mortal World",
                PreGeneratedDices1d20 = new[] { 20 }
            });
        await using var coldContext = new HeldTreatmentPipelineContext(
            coldRoot,
            coldFileSystem,
            ParseSeverityTreatmentContext(),
            hooks: null,
            itemScenario: null);
        await coldContext.AcquireLeaseAsync();
        AcceptedTurnAuthorityRegistry.InvalidateAcceptedTurnValidated(
            coldContext.FileSystem,
            coldContext.Lease);
        var copiedBytes = await CaptureProcedurePublishedStateBytesAsync(coldContext);
        AssertPublishedImagesEqual(publishedBytes, copiedBytes);
        var coldAcceptedState = ExportCurrentTreatmentAcceptedState(coldContext);
        var coldHistory = ReadCurrentTreatmentHistory(coldContext.FileSystem);
        var recovered = coldAcceptedState.RestorePersistedTreatmentRequests(
            coldHistory);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        var finalized = Assert.Single(
            recovered.FinalizedRequests,
            request => string.Equals(
                request.Coordinates.OperationKey,
                secondOperationKey,
                StringComparison.Ordinal));
        var replay = MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            finalized,
            coldHistory,
            ReadCurrentTreatmentWound(coldContext.FileSystem),
            coldAcceptedState);
        Assert.Equal("ExactReplay", replay.Disposition);
        Assert.NotNull(replay.ReplayReceipt);
        await AssertProcedurePublishedStateBytesAsync(coldContext, copiedBytes);
        Assert.Equal(
            resourceSpendsBefore + 2,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(coldContext.FileSystem)));
        Assert.Equal(
            woundTransitionsBefore + 2,
            CountProcedureTreatmentTransitions(coldContext));
    }

    private async Task<HeldTreatmentPipelineContext>
        CreateSeverityReductionPipelineContextAsync(
            AcceptedTreatmentPipelineFault? fault,
            int rootCount,
            int reductionSteps,
            int acceptedDie,
            bool includeEnergyResource,
            string mode = "procedure",
            string expectedCategory = "success",
            string targetKind = "player",
            string targetId = "player_current",
            int currentEnergy = 2)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "boe-held-treatment-pipeline-severity-" + Guid.NewGuid().ToString("N"));
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
            var wound = CreateSeverityReductionTreatmentWound(
                rootCount,
                reductionSteps,
                includeEnergyResource,
                mode,
                targetKind,
                targetId);
            await SeedHeldTreatmentAuthorityAsync(fileSystem, wound);
            if (currentEnergy != 2)
            {
                await WriteCanonicalPlayerTreatmentResourceAuthorityAsync(
                    fileSystem,
                    currentEnergy);
            }
            await ConfigureSeverityOwnerCarriersAsync(
                fileSystem,
                wound,
                targetKind,
                targetId);
            await SeedProcedurePlayerSkillAsync(fileSystem);
            await SeedProcedureProviderSkillAsync(fileSystem);
            await SeedProcedureFateShieldAsync(fileSystem);
            await ResealAllSeverityEffectIdentityAsync(
                fileSystem,
                targetKind,
                targetId);
            var prepared = await new LiveTurnPreparationService(fileSystem)
                .PrepareAsync(new LiveTurnPreparationOptions
                {
                    SessionId = "session_t070b6_severity_pipeline",
                    RequestId = "request_t070b6_severity_pipeline",
                    TurnNumber = HeldTreatmentPipelineContext.Turn,
                    PlayerAction = "Reduce the accepted wound severity.",
                    CurrentRealm = "Mortal World",
                    PreGeneratedDices1d20 = new[] { acceptedDie }
                });
            Assert.Equal("input/turn_request.json", prepared.TurnRequestPath);
            context = new HeldTreatmentPipelineContext(
                root,
                fileSystem,
                ParseSeverityTreatmentContext(targetKind, targetId),
                fault?.Hooks,
                itemScenario: null);
            await context.AcquireLeaseAsync();

            var acceptedState = ExportCurrentTreatmentAcceptedState(context);
            var history = ReadCurrentTreatmentHistory(fileSystem);
            var currentWound = ReadSeverityTreatmentWound(
                fileSystem,
                targetKind,
                targetId);
            var acceptedEventRef = Assert.Single(
                acceptedState.Binding.AcceptedEvents).EventRef;
            var preparedRequest = string.Equals(mode, "guaranteed", StringComparison.Ordinal)
                ? MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
                    acceptedState,
                    history,
                    currentWound,
                    HeldTreatmentPipelineContext.OperationKey,
                    HeldTreatmentPipelineContext.RouteId,
                    acceptedEventRef)
                : MortalWoundTreatmentPlanner.PrepareProcedureRequest(
                    acceptedState,
                    history,
                    currentWound,
                    HeldTreatmentPipelineContext.OperationKey,
                    HeldTreatmentPipelineContext.RouteId,
                    acceptedEventRef);
            Assert.True(
                preparedRequest.IsValid,
                DescribeValidationIssues(preparedRequest.Issues));
            var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
                preparedRequest.Request);
            AssertSeverityClaims(
                request,
                includeEnergyResource,
                mode,
                acceptedDie);
            var resolved = string.Equals(mode, "guaranteed", StringComparison.Ordinal)
                ? MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(
                    request,
                    history,
                    currentWound,
                    acceptedState)
                : MortalWoundTreatmentPlanner.CreateProcedureAttempt(
                    request,
                    history,
                    currentWound,
                    acceptedState);
            Assert.Equal("Resolved", resolved.Disposition);
            Assert.Empty(resolved.Issues);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
                resolved.Resolution);
            Assert.Equal(expectedCategory, resolution.ResultCategory);
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

            await context.ReleaseLeaseAsync();
            await new StateDistributor(
                    fileSystem,
                    NullLogger<StateDistributor>.Instance)
                .DistributeAsync(
                    new GameResponse
                    {
                        Response = HeldTreatmentPipelineContext.FinalSceneText
                    },
                    recomposed);
            await AddGuardianCleanupCommandSurfaceAsync(fileSystem);
            await new ProgressionScheduleService(
                    fileSystem,
                    NullLogger<ProgressionScheduleService>.Instance)
                .EnsureInitializedAsync();
            await context.AcquireLeaseAsync();
            await RestoreSeverityReductionAndComposeSameSemanticPlanAsync(
                context,
                includeEnergyResource,
                mode,
                acceptedDie,
                expectedCategory);
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

    private static MortalWoundTreatmentAuthority.Context
        ParseSeverityTreatmentContext(
            string targetKind = "player",
            string targetId = "player_current")
    {
        var parsed = MortalWoundTreatmentAuthority.ParseContext(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["targetKind"] = targetKind,
                ["targetId"] = targetId,
                ["providerKind"] = "npc",
                ["providerId"] = HeldTreatmentPipelineContext.ProviderId,
                ["currentLocationId"] = "loc_field_clinic_001"
            }.ToJsonString(),
            "treatmentContext");
        Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues));
        return Assert.IsType<MortalWoundTreatmentAuthority.Context>(
            parsed.Context);
    }

    private static void AssertPublishedImagesEqual(
        IReadOnlyDictionary<string, ExactFileImage> expected,
        IReadOnlyDictionary<string, ExactFileImage> actual)
    {
        Assert.Equal(
            expected.Keys.OrderBy(static path => path, StringComparer.Ordinal),
            actual.Keys.OrderBy(static path => path, StringComparer.Ordinal));
        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value.Exists, actual[pair.Key].Exists);
            Assert.Equal(pair.Value.Bytes, actual[pair.Key].Bytes);
        }
    }

    private static void AssertExactOptionalBytes(
        byte[]? expected,
        byte[]? actual)
    {
        Assert.Equal(expected is not null, actual is not null);
        if (expected is not null)
            Assert.Equal(expected, actual);
    }

    private static async Task RestoreSeverityReductionAndComposeSameSemanticPlanAsync(
        HeldTreatmentPipelineContext context,
        bool includeEnergyResource,
        string mode,
        int acceptedDie,
        string expectedCategory)
    {
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        var request = Assert.Single(recovered.HeldRequests);
        AssertSeverityClaims(
            request,
            includeEnergyResource,
            mode,
            acceptedDie);
        var currentWound = ReadSeverityTreatmentWound(
            context.FileSystem,
            context.TreatmentContext.TargetKind,
            context.TreatmentContext.TargetId);
        var resolutionResult = string.Equals(mode, "guaranteed", StringComparison.Ordinal)
            ? MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(
                request,
                history,
                currentWound,
                acceptedState)
            : MortalWoundTreatmentPlanner.CreateProcedureAttempt(
                request,
                history,
                currentWound,
                acceptedState);
        Assert.Equal("Resolved", resolutionResult.Disposition);
        Assert.Empty(resolutionResult.Issues);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
            resolutionResult.Resolution);
        Assert.Equal(expectedCategory, resolution.ResultCategory);
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

    private static async Task PrepareNextSeverityReductionAttemptAsync(
        HeldTreatmentPipelineContext context,
        string operationKey,
        int acceptedDie,
        string expectedCategory)
    {
        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(
            new LiveTurnPreparationOptions
            {
                SessionId = "session_t070b6_second_generation",
                RequestId = "request_t070b6_second_generation",
                TurnNumber = HeldTreatmentPipelineContext.Turn + 1,
                PlayerAction = "Reduce the same wound from severity II to I.",
                CurrentRealm = "Mortal World",
                PreGeneratedDices1d20 = new[] { acceptedDie }
            });
        await context.RestartAsync(hooks: null);
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var wound = ReadCurrentTreatmentWound(context.FileSystem);
        var prepared = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            acceptedState,
            history,
            wound,
            operationKey,
            HeldTreatmentPipelineContext.RouteId,
            Assert.Single(acceptedState.Binding.AcceptedEvents).EventRef);
        Assert.True(prepared.IsValid, DescribeValidationIssues(prepared.Issues));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            prepared.Request);
        AssertSeverityClaims(
            request,
            includeEnergyResource: true,
            mode: "procedure",
            expectedNaturalRoll: acceptedDie);
        var resolved = MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            request,
            history,
            wound,
            acceptedState);
        Assert.Equal("Resolved", resolved.Disposition);
        Assert.Empty(resolved.Issues);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
            resolved.Resolution);
        Assert.Equal(expectedCategory, resolution.ResultCategory);
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

        await context.ReleaseLeaseAsync();
        await new StateDistributor(
                context.FileSystem,
                NullLogger<StateDistributor>.Instance)
            .DistributeAsync(
                new GameResponse
                {
                    Response = HeldTreatmentPipelineContext.FinalSceneText
                },
                recomposed);
        await AddGuardianCleanupCommandSurfaceAsync(context.FileSystem);
        await context.AcquireLeaseAsync();
        await RestoreSeverityReductionAndComposeSameSemanticPlanAsync(
            context,
            includeEnergyResource: true,
            mode: "procedure",
            acceptedDie,
            expectedCategory);
    }

    private static EffectIdentityState ReadSeverityEffectIdentity(
        FileSystemManager fileSystem)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            fileSystem.ResolvePath(EffectIdentityState.StatePath)));
        var parsed = EffectIdentityState.Parse(
            document.RootElement,
            EffectIdentityState.StatePath);
        Assert.Empty(parsed.Issues);
        return Assert.IsType<EffectIdentityState>(parsed.State);
    }

    private static EffectIdentityEntry RequireSeverityEffectIdentity(
        EffectIdentityState identity,
        string effectId)
    {
        Assert.True(identity.TryGetEntry(effectId, out var entry));
        return entry;
    }

    private static int CountUnselectedSeverityResourceSpends(
        ResourceHistoryState history) => history.Transitions.Count(static transition =>
        transition.Operation == ResourceTransitionOperation.Spend &&
        transition.Phase == ResourceMutationPhase.RegisteredSystemOutcome &&
        (transition.Coordinate.OwnerKind != ResourceOwnerKind.Player ||
         !string.Equals(
             transition.Coordinate.ResourceOwnerId,
             "player_current",
             StringComparison.Ordinal) ||
         !string.Equals(
             transition.Coordinate.ResourceKey,
             "energy",
             StringComparison.Ordinal)));

    private static void AssertSeverityGenerationExpired(
        EffectIdentityEntry entry)
    {
        Assert.Equal("expired", entry.State);
        var transition = Assert.IsType<EffectIdentityTransition>(
            Assert.Single(entry.Transitions, static candidate => string.Equals(
                candidate.Kind,
                "expire",
                StringComparison.Ordinal)));
        Assert.Contains(entry.EffectId, transition.SourceEffectIds);
        Assert.Empty(transition.ResultEffectIds);
    }

    private static void AssertSeverityGenerationCreatedFrom(
        EffectIdentityEntry entry,
        string predecessorEffectId)
    {
        var create = Assert.IsType<EffectIdentityTransition>(
            Assert.Single(entry.Transitions, static candidate => string.Equals(
                candidate.Kind,
                "create",
                StringComparison.Ordinal)));
        Assert.Contains(predecessorEffectId, create.SourceEffectIds);
        Assert.Contains(entry.EffectId, create.ResultEffectIds);
    }

    private static JsonObject CreateSeverityReductionTreatmentWound(
            int rootCount,
            int reductionSteps,
            bool includeEnergyResource,
            string mode,
            string targetKind = "player",
            string targetId = "player_current")
    {
        Assert.InRange(rootCount, 0, 2);
        Assert.InRange(reductionSteps, 1, 2);
        var wound = WoundContractTestData.CreateActiveWound(
            HeldTreatmentPipelineContext.WoundId,
            "mortal_world",
            targetKind,
            targetId,
            ResolveSeverityCarrierPath(targetKind));
        wound["severity"]!["value"] = "III";
        wound["severity"]!["rank"] = 3;
        wound["severity"]!["maximumAtCreation"] = "III";
        wound["consequences"]!["slotBudget"] = 3;
        if (rootCount == 0)
        {
            wound["consequences"]!["slotsUsed"] = 0;
            wound["consequences"]!["entries"] = new JsonArray();
            wound["consequences"]!["ownedEffectSources"] =
                WoundContractTestData.CreateOwnedEffectSources(
                    HeldTreatmentPipelineContext.WoundId,
                    "mortal_world");
        }
        else if (rootCount == 1)
        {
            wound["consequences"]!["ownedEffectSources"]!["definitions"]!
                .AsArray().RemoveAt(0);
            wound["consequences"]!["ownedEffectSources"]!["rootBindings"]!
                .AsArray().RemoveAt(0);
            wound["consequences"]!["entries"]!.AsArray().RemoveAt(0);
            wound["consequences"]!["entries"]![0]!["slot"] = 1;
            wound["consequences"]!["slotsUsed"] = 1;
        }

        var guaranteed = string.Equals(mode, "guaranteed", StringComparison.Ordinal);
        var requirements = new JsonArray(guaranteed
            ? new JsonObject
            {
                ["kind"] = "source_capability",
                ["capabilityRef"] = "exact_materialized_healing_source",
                ["actorRole"] = "provider"
            }
            : new JsonObject
            {
                ["kind"] = "skill_tier",
                ["capabilityRef"] = "field_medicine",
                ["minimumTier"] = 2,
                ["actorRole"] = "provider"
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
        var result = new JsonArray(new JsonObject
        {
            ["kind"] = "reduce_severity",
            ["steps"] = reductionSteps
        });
        var route = new JsonObject
        {
            ["routeId"] = HeldTreatmentPipelineContext.RouteId,
            ["displayName"] = "T070-B.6 severity reduction procedure",
            ["visibility"] = "known_to_player",
            ["mode"] = mode,
            ["requirements"] = requirements,
            ["resourcePolicy"] = new JsonObject
            {
                ["reserveBeforeResolution"] = true,
                ["consumeOn"] = includeEnergyResource
                    ? guaranteed
                        ? new JsonArray("success")
                        : new JsonArray("success", "partial_success", "failed_attempt")
                    : new JsonArray(),
                ["refundOn"] = new JsonArray(
                    "cancelled",
                    "validation_failed",
                    "rolled_back"),
                ["mutations"] = mutations
            },
            ["resolution"] = guaranteed
                ? new JsonObject
                {
                    ["capabilityRef"] = "exact_materialized_healing_source",
                    ["actorRole"] = "provider"
                }
                : new JsonObject
                {
                    ["formulaKey"] = "mortal_wound_procedure_v1",
                    ["difficulty"] = 10,
                    ["rollSource"] = "accepted_d20",
                    ["criticalPolicy"] = "natural_20_first_natural_1_last",
                    ["modifierSource"] = new JsonObject
                    {
                        ["kind"] = "resolved_skill_tier",
                        ["requirementIndex"] = 0
                    }
                },
            ["outcomes"] = guaranteed
                ? new JsonArray(new JsonObject
                {
                    ["category"] = "success",
                    ["result"] = result.DeepClone()
                })
                : new JsonArray(
                    ProcedureBand("success", 5, null, "success", "no_improvement"),
                    ProcedureBand("partial", 0, 4, "partial_success", "no_improvement"),
                    ProcedureBand("failed", -4, -1, "failed_attempt", "no_improvement"),
                    ProcedureBand("critical", null, -5, "failed_attempt", "no_improvement")),
            ["interruption"] = null
        };
        if (!guaranteed)
        {
            foreach (var outcome in route["outcomes"]!.AsArray().OfType<JsonObject>())
                outcome["result"] = result.DeepClone();
        }
        wound["treatment"]!["routes"] = new JsonArray(route);
        wound["treatment"]!["knownRouteIds"] = new JsonArray(
            HeldTreatmentPipelineContext.RouteId);
        return wound;
    }

    private static void AssertSeverityClaims(
        MortalWoundTreatmentAttemptRequest request,
        bool includeEnergyResource,
        string mode,
        int expectedNaturalRoll)
    {
        Assert.Equal(
            includeEnergyResource ? "held" : "not_required",
            request.ResourceAuthority.ReservationDisposition);
        Assert.Equal(
            includeEnergyResource ? 1 : 0,
            request.ResourceAuthority.Claims.Count);
        if (includeEnergyResource)
        {
            Assert.NotNull(request.ResourceAuthority.ReservationId);
            Assert.Contains(request.ResourceAuthority.Claims, static claim =>
                string.Equals(claim.Kind, "resource_quantity", StringComparison.Ordinal) &&
                string.Equals(claim.AuthorityRef, "energy", StringComparison.Ordinal) &&
                string.Equals(claim.OwnerKind, "player", StringComparison.Ordinal) &&
                string.Equals(claim.OwnerId, "player_current", StringComparison.Ordinal) &&
                claim.Quantity == 2);
        }
        else
        {
            Assert.Null(request.ResourceAuthority.ReservationId);
        }
        if (string.Equals(mode, "guaranteed", StringComparison.Ordinal))
        {
            Assert.IsType<MortalWoundTreatmentCapabilityProof>(request.ModeAuthority);
            return;
        }
        var procedure = RequireProcedureAuthority(request);
        Assert.Equal("normal", procedure.RollMode);
        Assert.Equal(new[] { 0 }, procedure.SourceIndices);
        Assert.Equal(expectedNaturalRoll, procedure.NaturalRoll);
        Assert.Null(procedure.PreparedCriticalReaction);
    }

    private static async Task ConfigureSeverityOwnerCarriersAsync(
        FileSystemManager fileSystem,
        JsonObject wound,
        string targetKind,
        string targetId)
    {
        var originalEffectCarrier = JsonNode.Parse(Assert.IsType<string>(
                await fileSystem.ReadFileAsync(EffectCarrierCatalog.PlayerPath)))!
            .AsObject();
        var effects = originalEffectCarrier["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .Select(static effect => effect.DeepClone().AsObject())
            .ToArray();
        for (var index = 0; index < effects.Length; index++)
        {
            var ordinal = index + 1;
            var eventRef = $"turn_42:t070b6_seed:{ordinal}";
            var transitionId = $"effect_transition_t070b6_seed_{ordinal}";
            effects[index]["chronology"]!["createdEventRef"] = eventRef;
            effects[index]["chronology"]!["lastTransitionId"] = transitionId;
            effects[index]["chronology"]!["lastTransitionTurn"] = 42;
            effects[index]["target"]!["kind"] = targetKind switch
            {
                "player" => "player",
                "npc" => "npc",
                "combatant" => "combatant",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(targetKind), targetKind, null)
            };
            effects[index]["target"]!["targetId"] = targetId;
        }

        await fileSystem.WriteFileAtomicAsync(
            WoundCarrierCatalog.PlayerPath,
            (string.Equals(targetKind, "player", StringComparison.Ordinal)
                ? WoundContractTestData.CreatePlayerCarrier(wound)
                : WoundContractTestData.CreatePlayerCarrier()).ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            EffectCarrierCatalog.PlayerPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = string.Equals(
                    targetKind,
                    "player",
                    StringComparison.Ordinal)
                    ? CloneSeverityNodes(effects)
                    : new JsonArray()
            }.ToJsonString());

        var npcWounds = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray()
        };
        var npcEffects = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray()
        };
        if (string.Equals(targetKind, "npc", StringComparison.Ordinal))
        {
            npcWounds = WoundContractTestData.CreateNamedNpcCarrier(
                targetId,
                wound);
            npcEffects["entries"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = targetId,
                ["activeEffects"] = CloneSeverityNodes(effects)
            });
            var npcCore = JsonNode.Parse(Assert.IsType<string>(
                    await fileSystem.ReadFileAsync(
                        NpcCoreChangesContract.NpcCorePath)))!
                .AsObject();
            var target = MortalActorTestFixtures.CreateActor(
                targetId,
                "loc_field_clinic_001",
                "Wounded patient");
            target["displayName"] = "Wounded patient";
            target["activeSkills"] = new JsonArray();
            target["passiveSkills"] = new JsonArray();
            target["inventory"] = new JsonArray();
            npcCore[GuardianPolicyContracts.NpcCoreSceneSectionName]!
                .AsArray()
                .Add(target);
            await fileSystem.WriteFileAtomicAsync(
                NpcCoreChangesContract.NpcCorePath,
                npcCore.ToJsonString());
        }
        await fileSystem.WriteFileAtomicAsync(
            WoundCarrierCatalog.NpcPath,
            npcWounds.ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            EffectCarrierCatalog.NpcPath,
            npcEffects.ToJsonString());

        var enemies = new JsonObject { ["enemiesData"] = new JsonArray() };
        if (string.Equals(targetKind, "combatant", StringComparison.Ordinal))
        {
            var combatant = CreateSeverityCombatant(targetId, wound, effects);
            enemies["enemiesData"] = new JsonArray(combatant);
        }
        await fileSystem.WriteFileAtomicAsync(
            WoundCarrierCatalog.EnemiesPath,
            enemies.ToJsonString());

        var parsedWound = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            ResolveSeverityCarrierPath(targetKind));
        Assert.True(
            parsedWound.IsValid,
            DescribeValidationIssues(parsedWound.Issues));
        var woundFingerprint = WoundIdentityState.ComputeSemanticFingerprint(
            Assert.IsType<WoundMaterializationEnvelope>(parsedWound.Wound));
        await fileSystem.WriteFileAtomicAsync(
            WoundIdentityState.StatePath,
            WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry(
                    ownerKind: targetKind,
                    ownerId: targetId,
                    carrierPath: ResolveSeverityCarrierPath(targetKind),
                    semanticFingerprint: woundFingerprint)).ToJsonString());

        var identity = EffectMaterializationTestFixture.CreateIdentityIndex(effects);
        var entries = identity["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        for (var index = 0; index < entries.Length; index++)
        {
            var ordinal = index + 1;
            var transition = Assert.IsType<JsonObject>(Assert.Single(
                entries[index]["transitions"]!.AsArray()));
            transition["transitionId"] = $"effect_transition_t070b6_seed_{ordinal}";
            transition["eventRef"] = $"turn_42:t070b6_seed:{ordinal}";
        }
        await fileSystem.WriteFileAtomicAsync(
            EffectIdentityState.StatePath,
            identity.ToJsonString());
    }

    private static async Task SeedProcedureProviderSkillAsync(
        FileSystemManager fileSystem)
    {
        var npcCore = JsonNode.Parse(Assert.IsType<string>(
                await fileSystem.ReadFileAsync(
                    NpcCoreChangesContract.NpcCorePath)))!
            .AsObject();
        var provider = npcCore[GuardianPolicyContracts.NpcCoreSceneSectionName]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(actor => string.Equals(
                actor["NPCId"]?.GetValue<string>(),
                HeldTreatmentPipelineContext.ProviderId,
                StringComparison.Ordinal));
        var skill = CreateGuaranteedTreatmentSkill().DeepClone().AsObject();
        skill["skillId"] = "skill_t070b6_provider_field_medicine";
        skill["displayName"] = "Provider Field Medicine";
        skill["skillName"] = "Provider Field Medicine";
        skill["skillDescription"] =
            "Provides nearby canonical procedure authority for every target carrier.";
        skill["mortalWoundTreatmentCapabilities"]![0]!["capabilityRef"] =
            "field_medicine";
        provider["activeSkills"]!.AsArray().Add(skill);
        await fileSystem.WriteFileAtomicAsync(
            NpcCoreChangesContract.NpcCorePath,
            npcCore.ToJsonString());
    }

    private static async Task ResealAllSeverityEffectIdentityAsync(
        FileSystemManager fileSystem,
        string targetKind,
        string targetId)
    {
        var playerRoot = JsonNode.Parse(Assert.IsType<string>(
                await fileSystem.ReadFileAsync(EffectCarrierCatalog.PlayerPath)))!
            .AsObject();
        var fateEffect = playerRoot["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .Single(effect => string.Equals(
                effect["effectId"]?.GetValue<string>(),
                ProcedureFateShieldEffectId,
                StringComparison.Ordinal));
        fateEffect["display"] = new JsonObject
        {
            ["name"] = "Щит Судьбы",
            ["description"] =
                "Чернила Судьбы смягчают следующий критический провал до обычного провала.",
            ["category"] = "buff",
            ["visibility"] = "visible"
        };
        await fileSystem.WriteFileAtomicAsync(
            EffectCarrierCatalog.PlayerPath,
            playerRoot.ToJsonString());
        var allEffects = playerRoot["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .Select(static effect => effect.DeepClone().AsObject())
            .ToList();
        if (!string.Equals(targetKind, "player", StringComparison.Ordinal))
        {
            allEffects.AddRange(ReadSeverityTargetEffects(
                    fileSystem,
                    targetKind,
                    targetId)
                .Select(static effect => effect.DeepClone().AsObject()));
        }
        var identity = EffectMaterializationTestFixture.CreateIdentityIndex(
            allEffects.ToArray());
        var entries = identity["entries"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        for (var index = 0; index < entries.Length; index++)
        {
            var effectId = entries[index]["effectId"]!.GetValue<string>();
            var fate = string.Equals(
                effectId,
                ProcedureFateShieldEffectId,
                StringComparison.Ordinal);
            var effect = allEffects.Single(candidate => string.Equals(
                candidate["effectId"]?.GetValue<string>(),
                effectId,
                StringComparison.Ordinal));
            var transition = Assert.IsType<JsonObject>(Assert.Single(
                entries[index]["transitions"]!.AsArray()));
            transition["transitionId"] = fate
                ? "effect_transition_t070b5_fate_seed"
                : effect["chronology"]?["lastTransitionId"]?.GetValue<string>();
            transition["eventRef"] = fate
                ? "turn_30:t070b5_fate_seed:1"
                : effect["chronology"]?["createdEventRef"]?.GetValue<string>();
            transition["turn"] = fate
                ? 30
                : effect["chronology"]?["lastTransitionTurn"]?.GetValue<int>();
            if (fate)
                entries[index]["createdAtTurn"] = 30;
        }
        await fileSystem.WriteFileAtomicAsync(
            EffectIdentityState.StatePath,
            identity.ToJsonString());
    }

    private static JsonObject CreateSeverityCombatant(
        string targetId,
        JsonObject wound,
        IReadOnlyList<JsonObject> effects) => new()
    {
        ["NPCId"] = null,
        ["name"] = "Wounded canonical combatant",
        ["displayName"] = "Wounded canonical combatant",
        ["image_prompt"] = "setting-neutral wounded combatant portrait",
        ["description"] = "A complete canonical combatant used by T070-B.6.",
        ["type"] = "Test combatant",
        ["isGroup"] = false,
        ["actions"] = new JsonArray(),
        ["resistances"] = new JsonArray(),
        ["activeBuffs"] = CloneSeverityNodes(effects.Where(static effect =>
            string.Equals(
                effect["display"]?["category"]?.GetValue<string>(),
                "buff",
                StringComparison.Ordinal))),
        ["activeDebuffs"] = CloneSeverityNodes(effects.Where(static effect =>
            !string.Equals(
                effect["display"]?["category"]?.GetValue<string>(),
                "buff",
                StringComparison.Ordinal))),
        ["combatantId"] = targetId,
        ["activeWounds"] = new JsonArray(wound.DeepClone())
    };

    private static JsonArray CloneSeverityNodes(IEnumerable<JsonObject> nodes) =>
        new(nodes.Select(static node => (JsonNode)node.DeepClone()).ToArray());

    private static string ResolveSeverityCarrierPath(string targetKind) =>
        targetKind switch
        {
            "player" => WoundCarrierCatalog.PlayerPath,
            "npc" => WoundCarrierCatalog.NpcPath,
            "combatant" => WoundCarrierCatalog.EnemiesPath,
            _ => throw new ArgumentOutOfRangeException(
                nameof(targetKind), targetKind, null)
        };

    private static WoundMaterializationEnvelope ReadSeverityTreatmentWound(
        FileSystemManager fileSystem,
        string targetKind,
        string targetId)
    {
        var root = JsonNode.Parse(File.ReadAllText(fileSystem.ResolvePath(
            ResolveSeverityCarrierPath(targetKind))))!.AsObject();
        JsonObject wound = targetKind switch
        {
            "player" => Assert.IsType<JsonObject>(Assert.Single(
                root["activeWounds"]!.AsArray())),
            "npc" => Assert.IsType<JsonObject>(Assert.Single(
                root["entries"]!.AsArray(),
                entry => string.Equals(
                    entry?["npcId"]?.GetValue<string>(),
                    targetId,
                    StringComparison.Ordinal)))["activeWounds"]![0]!.AsObject(),
            "combatant" => Assert.IsType<JsonObject>(Assert.Single(
                root["enemiesData"]!.AsArray(),
                entry => string.Equals(
                    entry?["combatantId"]?.GetValue<string>(),
                    targetId,
                    StringComparison.Ordinal)))["activeWounds"]![0]!.AsObject(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(targetKind), targetKind, null)
        };
        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            ResolveSeverityCarrierPath(targetKind));
        Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues));
        return Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
    }

    private static IReadOnlyList<JsonObject> ReadSeverityTargetEffects(
        FileSystemManager fileSystem,
        string targetKind,
        string targetId)
    {
        var path = targetKind switch
        {
            "player" => EffectCarrierCatalog.PlayerPath,
            "npc" => EffectCarrierCatalog.NpcPath,
            "combatant" => EffectCarrierCatalog.EnemiesPath,
            _ => throw new ArgumentOutOfRangeException(
                nameof(targetKind), targetKind, null)
        };
        var root = JsonNode.Parse(File.ReadAllText(fileSystem.ResolvePath(path)))!
            .AsObject();
        return targetKind switch
        {
            "player" => root["activeEffects"]!.AsArray()
                .OfType<JsonObject>()
                .ToArray(),
            "npc" => Assert.IsType<JsonObject>(Assert.Single(
                    root["entries"]!.AsArray(),
                    entry => string.Equals(
                        entry?["NPCId"]?.GetValue<string>(),
                        targetId,
                        StringComparison.Ordinal)))["activeEffects"]!.AsArray()
                .OfType<JsonObject>()
                .ToArray(),
            "combatant" => ReadSeverityCombatantEffects(root, targetId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(targetKind), targetKind, null)
        };
    }

    private static IReadOnlyList<JsonObject> ReadSeverityCombatantEffects(
        JsonObject root,
        string targetId)
    {
        var combatant = Assert.IsType<JsonObject>(Assert.Single(
            root["enemiesData"]!.AsArray(),
            entry => string.Equals(
                entry?["combatantId"]?.GetValue<string>(),
                targetId,
                StringComparison.Ordinal)));
        return combatant["activeBuffs"]!.AsArray().OfType<JsonObject>()
            .Concat(combatant["activeDebuffs"]!.AsArray().OfType<JsonObject>())
            .ToArray();
    }

    private static JsonObject ReadPlayerEffectById(
        FileSystemManager fileSystem,
        string effectId)
    {
        var root = JsonNode.Parse(File.ReadAllText(fileSystem.ResolvePath(
            EffectCarrierCatalog.PlayerPath)))!.AsObject();
        return Assert.IsType<JsonObject>(Assert.Single(
            root["activeEffects"]!.AsArray(),
            effect => string.Equals(
                effect?["effectId"]?.GetValue<string>(),
                effectId,
                StringComparison.Ordinal)));
    }

}
