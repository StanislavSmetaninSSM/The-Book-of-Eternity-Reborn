using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const string SeverityReactionChildDefinitionKey =
        "definition_t070b6_reaction_child";
    private const string SeverityReactionChildEffectId =
        "effect_t070b6_reaction_child";

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
        AssertFreshSeverityEffectIds(priorRootIds, currentRootIds);
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
            AssertSingleSeverityGenerationSuccessor(
                identity,
                priorRootsByDefinition[binding.DefinitionKey],
                binding.DefinitionKey,
                binding.EffectId);
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
        AssertSeverityWoundIndexAndHistoryAgreement(
            context,
            beforeWound,
            published);
        var output = JsonNode.Parse(Assert.IsType<string>(
            await context.FileSystem.ReadFileAsync(
                "output/narrative_response.json")))!.AsObject();
        Assert.Equal(
            HeldTreatmentPipelineContext.FinalSceneText,
            output["response"]?.GetValue<string>());
    }

    [Fact]
    public async Task SeverityReduction_RealReactionDescendantIsRetiredWithItsSourceGeneration()
    {
        await using var context = await CreateSeverityReductionPipelineContextAsync(
            fault: null,
            rootCount: 1,
            reductionSteps: 1,
            acceptedDie: 20,
            includeEnergyResource: false,
            initialSeverityRank: 4,
            includeReactionDescendant: true);
        var before = ReadCurrentTreatmentWound(context.FileSystem);
        var priorRootId = Assert.Single(
            before.Consequences.OwnedEffectSources.RootBindings).EffectId;
        var priorClosureIds = ReadSeverityTargetEffects(
                context.FileSystem,
                "player",
                "player_current")
            .Where(static effect => string.Equals(
                effect["source"]?["kind"]?.GetValue<string>(),
                "wound",
                StringComparison.Ordinal))
            .Select(static effect => effect["effectId"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(
            new[] { priorRootId, SeverityReactionChildEffectId }
                .OrderBy(static value => value, StringComparer.Ordinal),
            priorClosureIds.OrderBy(static value => value, StringComparer.Ordinal));

        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);
        var disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "severity reduction reaction-descendant publication oracle",
            snapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            null);

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, disposition);
        var published = ReadCurrentTreatmentWound(context.FileSystem);
        Assert.Equal("III", published.Severity.Value);
        var freshRootId = Assert.Single(
            published.Consequences.OwnedEffectSources.RootBindings).EffectId;
        AssertFreshSeverityEffectIds(priorClosureIds, new[] { freshRootId });
        var identity = ReadSeverityEffectIdentity(context.FileSystem);
        AssertSeverityGenerationExpired(
            RequireSeverityEffectIdentity(identity, priorRootId));
        AssertSeverityGenerationExpired(
            RequireSeverityEffectIdentity(identity, SeverityReactionChildEffectId));
        var fresh = RequireSeverityEffectIdentity(identity, freshRootId);
        Assert.Equal("active", fresh.State);
        AssertSeverityGenerationCreatedFrom(fresh, priorRootId);
        AssertSingleSeverityGenerationSuccessor(
            identity,
            priorRootId,
            Assert.Single(published.Consequences.OwnedEffectSources.RootBindings)
                .DefinitionKey,
            freshRootId);
        var activeWoundEffects = ReadSeverityTargetEffects(
                context.FileSystem,
                "player",
                "player_current")
            .Where(static effect => string.Equals(
                effect["source"]?["kind"]?.GetValue<string>(),
                "wound",
                StringComparison.Ordinal))
            .Select(static effect => effect["effectId"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(new[] { freshRootId }, activeWoundEffects);
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
        var competingPlan = Assert.IsType<AcceptedMechanicsPlan>(fault.ForeignPlan);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out var competingBinding,
            out var competingCached));
        Assert.Same(competingPlan, competingCached.Plan);
        Assert.NotSame(context.Plan, competingCached.Plan);
        Assert.Equal(
            fault.ForeignBindingFingerprint,
            AcceptedMechanicsPlanFingerprints.ComputeInput(
                competingBinding));
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
        var replayCommand = Assert.IsType<byte[]>(await context.ReadFileBytesAsync(
            AcceptedMechanicsPlan.WoundCommandPath));
        var replayPending = await context.ReadFileBytesAsync(
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        var finalizedRequest = context.Request;
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
        Assert.Equal(finalizedRequest.RequestFingerprint, finalized.RequestFingerprint);
        var replay = MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            finalized,
            history,
            ReadCurrentTreatmentWound(coldContext.FileSystem),
            acceptedState);
        Assert.Equal("ExactReplay", replay.Disposition);
        Assert.NotNull(replay.ReplayReceipt);

        await RestoreSeverityReplayCommandSurfacesAsync(
            coldContext,
            replayCommand,
            replayPending);
        Assert.True(await ContainsExactTreatmentRequestAsync(coldContext, finalized));
        await AssertColdReplayAcceptedThroughGameEngineAsync(
            coldContext,
            HeldTreatmentPipelineContext.Turn,
            "severity reduction cold replay coordinator",
            copied);
        await AssertProcedurePublishedStateBytesAsync(coldContext, copied);
        Assert.False(await ContainsExactTreatmentRequestAsync(coldContext, finalized));
        Assert.Equal(
            resourceSpends,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(coldContext.FileSystem)));
        Assert.Equal(transitions, CountProcedureTreatmentTransitions(coldContext));

        await coldContext.AcquireLeaseAsync();
        var mismatchedCommand = ParseJsonObjectBytes(replayCommand);
        var mismatchedResult = Assert.IsType<JsonObject>(
            Assert.IsType<JsonObject>(Assert.Single(
                mismatchedCommand["commands"]!.AsArray()))["result"]);
        mismatchedResult["resultFingerprint"] =
            "sha256:" + new string('0', 64);
        await coldContext.FileSystem.WriteFileAtomicAsync(
            coldContext.Lease,
            AcceptedMechanicsPlan.WoundCommandPath,
            mismatchedCommand.ToJsonString(
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var mismatchedBytes = Assert.IsType<byte[]>(
            await coldContext.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));
        var mismatchedDisposition = await RunColdReplayThroughGameEngineAsync(
            coldContext,
            HeldTreatmentPipelineContext.Turn,
            "severity reduction mismatched cold replay coordinator");
        Assert.Equal(
            AcceptedTurnValidationDisposition.TerminalRejected,
            mismatchedDisposition);
        Assert.Equal(
            mismatchedBytes,
            await coldContext.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));
    }

    [Fact]
    public async Task SeverityReduction_ColdReplayRejectsChangedSceneTextWithValidCommandRef()
    {
        await using var context = await CreateSeverityReductionPipelineContextAsync(
            fault: null,
            rootCount: 2,
            reductionSteps: 1,
            acceptedDie: 20,
            includeEnergyResource: true);
        var replayCommand = Assert.IsType<byte[]>(await context.ReadFileBytesAsync(
            AcceptedMechanicsPlan.WoundCommandPath));
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);
        var initialDisposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "severity reduction changed scene replay initial publication",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);
        Assert.Equal(
            AcceptedTurnValidationDisposition.Accepted,
            initialDisposition);

        var coldRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-held-treatment-pipeline-severity-scene-replay-" +
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
                SessionId = "session_t070b6_scene_replay",
                RequestId = "request_t070b6_scene_replay",
                TurnNumber = HeldTreatmentPipelineContext.Turn,
                PlayerAction = "Replay a changed treatment scene.",
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
        var copiedBytes = await CaptureProcedurePublishedStateBytesAsync(
            coldContext);

        var changed = ParseJsonObjectBytes(replayCommand);
        var row = Assert.IsType<JsonObject>(Assert.Single(
            changed["commands"]!.AsArray()));
        var request = Assert.IsType<JsonObject>(
            Assert.IsType<JsonObject>(row["authority"])["request"]);
        var result = Assert.IsType<JsonObject>(row["result"]);
        const string changedText =
            "The wound closes beneath an account that was never published.";
        row["finalSceneText"] = changedText;
        row["commandRef"] = MortalWoundTreatmentCommandCodec.ComputeCommandRef(
            changed["sessionId"]!.GetValue<string>(),
            changed["requestId"]!.GetValue<string>(),
            changed["snapshotToken"]!.GetValue<string>(),
            row["operationKey"]!.GetValue<string>(),
            request["requestFingerprint"]!.GetValue<string>(),
            result["resultFingerprint"]!.GetValue<string>(),
            changedText);
        var strict = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(changed));
        Assert.True(strict.Success, DescribeValidationIssues(strict.Issues));
        await coldContext.FileSystem.WriteFileAtomicAsync(
            coldContext.Lease,
            AcceptedMechanicsPlan.WoundCommandPath,
            changed.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var changedBytes = Assert.IsType<byte[]>(
            await coldContext.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));

        var disposition = await RunColdReplayThroughGameEngineAsync(
            coldContext,
            HeldTreatmentPipelineContext.Turn,
            "severity reduction changed scene replay coordinator");

        Assert.Equal(
            AcceptedTurnValidationDisposition.TerminalRejected,
            disposition);
        Assert.Equal(
            changedBytes,
            await coldContext.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));
        var afterBytes = await CaptureProcedurePublishedStateBytesAsync(coldContext);
        AssertPublishedImagesEqual(
            copiedBytes
                .Where(static pair => !string.Equals(
                    pair.Key,
                    AcceptedMechanicsPlan.WoundCommandPath,
                    StringComparison.Ordinal))
                .ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value,
                    StringComparer.Ordinal),
            afterBytes
                .Where(static pair => !string.Equals(
                    pair.Key,
                    AcceptedMechanicsPlan.WoundCommandPath,
                    StringComparison.Ordinal))
                .ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value,
                    StringComparer.Ordinal));
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
        var carrierBytesBefore = await CaptureAllSeverityCarrierBytesAsync(
            context.FileSystem);
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
        var targetCarrierPaths = targetKind switch
        {
            "player" => new HashSet<string>(StringComparer.Ordinal)
            {
                WoundCarrierCatalog.PlayerPath,
                EffectCarrierCatalog.PlayerPath
            },
            "npc" => new HashSet<string>(StringComparer.Ordinal)
            {
                WoundCarrierCatalog.NpcPath,
                EffectCarrierCatalog.NpcPath
            },
            "combatant" => new HashSet<string>(StringComparer.Ordinal)
            {
                WoundCarrierCatalog.EnemiesPath
            },
            _ => throw new ArgumentOutOfRangeException(nameof(targetKind))
        };
        foreach (var pair in carrierBytesBefore.Where(pair =>
                     !targetCarrierPaths.Contains(pair.Key)))
        {
            AssertExactOptionalBytes(
                pair.Value,
                await context.FileSystem.ReadFileBytesAsync(pair.Key));
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
        var originalWound = ReadCurrentTreatmentWound(context.FileSystem);
        var originalRootId = Assert.Single(originalWound
            .Consequences.OwnedEffectSources.RootBindings)
            .EffectId;
        var firstRequest = context.Request;
        var firstResolution = context.Resolution;
        var firstPlan = context.Plan;
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
        AssertFreshSeverityEffectIds(
            new[] { originalRootId },
            new[] { firstFreshRootId });
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
        AssertSingleSeverityGenerationSuccessor(
            identityAfterFirst,
            originalRootId,
            Assert.Single(firstPublished.Consequences.OwnedEffectSources.RootBindings)
                .DefinitionKey,
            firstFreshRootId);
        var preservedOriginalHistory = originalTerminalAfterFirst.Raw.DeepClone();
        var preservedFirstFreshCreate = Assert.Single(
            firstFreshAfterFirst.Transitions,
            static transition => string.Equals(
                transition.Kind,
                "create",
                StringComparison.Ordinal)).Raw.DeepClone();
        var preservedFirstTreatmentHistory = ReadSeverityHistoryRowJson(
            context.FileSystem,
            firstRequest.Coordinates.OperationKey).DeepClone();
        Assert.Equal(
            resourceSpendsBefore + 1,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(
            woundTransitionsBefore + 1,
            CountProcedureTreatmentTransitions(context));
        await AssertSeverityTreatmentSpendAgreementAsync(
            context.FileSystem,
            firstRequest,
            firstResolution,
            firstPlan);
        AssertSeverityWoundIndexAndHistoryAgreement(
            context,
            originalWound,
            firstPublished);
        Assert.False(await ContainsCurrentExactTreatmentRequestAsync(context));
        await AssertSeverityProcedureClaimReleasedAsync(context, firstRequest);

        await PrepareNextSeverityReductionAttemptAsync(
            context,
            secondOperationKey,
            acceptedDie: 20,
            expectedCategory: "success");
        var secondRequest = context.Request;
        var secondResolution = context.Resolution;
        var secondPlan = context.Plan;
        var secondAllocatedEffectIds = Assert.IsType<EffectAcceptedTurnPlan>(
                context.Plan.EffectPlan)
            .AllocatedEffectIds
            .ToArray();
        var secondReplayCommand = Assert.IsType<byte[]>(
            await context.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));
        var secondReplayPending = await context.ReadFileBytesAsync(
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
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
        AssertFreshSeverityEffectIds(
            new[] { originalRootId, firstFreshRootId },
            new[] { secondFreshRootId });

        var identityAfterSecond = ReadSeverityEffectIdentity(context.FileSystem);
        var originalTerminalAfterSecond = RequireSeverityEffectIdentity(
            identityAfterSecond,
            originalRootId);
        Assert.True(JsonNode.DeepEquals(
            preservedOriginalHistory,
            originalTerminalAfterSecond.Raw));
        AssertSeverityGenerationExpired(
            RequireSeverityEffectIdentity(identityAfterSecond, firstFreshRootId));
        var firstFreshAfterSecond = RequireSeverityEffectIdentity(
            identityAfterSecond,
            firstFreshRootId);
        var firstFreshCreateAfterSecond = Assert.Single(
            firstFreshAfterSecond.Transitions,
            static transition => string.Equals(
                transition.Kind,
                "create",
                StringComparison.Ordinal));
        Assert.True(JsonNode.DeepEquals(
            preservedFirstFreshCreate,
            firstFreshCreateAfterSecond.Raw));
        var secondFreshAfterSecond = RequireSeverityEffectIdentity(
            identityAfterSecond,
            secondFreshRootId);
        Assert.Equal("active", secondFreshAfterSecond.State);
        AssertSeverityGenerationCreatedFrom(
            secondFreshAfterSecond,
            firstFreshRootId);
        AssertSingleSeverityGenerationSuccessor(
            identityAfterSecond,
            firstFreshRootId,
            Assert.Single(secondPublished.Consequences.OwnedEffectSources.RootBindings)
                .DefinitionKey,
            secondFreshRootId);
        Assert.True(JsonNode.DeepEquals(
            preservedFirstTreatmentHistory,
            ReadSeverityHistoryRowJson(
                context.FileSystem,
                firstRequest.Coordinates.OperationKey)));
        Assert.Equal(
            resourceSpendsBefore + 2,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(
            woundTransitionsBefore + 2,
            CountProcedureTreatmentTransitions(context));
        Assert.Equal(0, await ReadCurrentTreatmentEnergyAsync(context));
        Assert.False(await ContainsCurrentExactTreatmentRequestAsync(context));
        await AssertSeverityTreatmentSpendAgreementAsync(
            context.FileSystem,
            firstRequest,
            firstResolution,
            firstPlan);
        await AssertSeverityTreatmentSpendAgreementAsync(
            context.FileSystem,
            secondRequest,
            secondResolution,
            secondPlan);
        AssertSeverityWoundIndexAndHistoryAgreement(
            context,
            firstPublished,
            secondPublished);
        await AssertSeverityProcedureClaimReleasedAsync(context, secondRequest);

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
        await RestoreSeverityReplayCommandSurfacesAsync(
            coldContext,
            secondReplayCommand,
            secondReplayPending);
        Assert.True(await ContainsExactTreatmentRequestAsync(
            coldContext,
            finalized));
        await AssertColdReplayAcceptedThroughGameEngineAsync(
            coldContext,
            HeldTreatmentPipelineContext.Turn + 1,
            "severity reduction second-generation cold replay coordinator",
            copiedBytes);
        await AssertProcedurePublishedStateBytesAsync(coldContext, copiedBytes);
        Assert.False(await ContainsExactTreatmentRequestAsync(
            coldContext,
            finalized));
        Assert.Equal(
            resourceSpendsBefore + 2,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(coldContext.FileSystem)));
        Assert.Equal(
            woundTransitionsBefore + 2,
            CountProcedureTreatmentTransitions(coldContext));
    }

    private async Task AssertColdReplayAcceptedThroughGameEngineAsync(
        HeldTreatmentPipelineContext coldContext,
        int expectedTurn,
        string source,
        IReadOnlyDictionary<string, ExactFileImage> expectedBytes)
    {
        var disposition = await RunColdReplayThroughGameEngineAsync(
            coldContext,
            expectedTurn,
            source);
        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, disposition);
        await AssertProcedurePublishedStateBytesAsync(coldContext, expectedBytes);
    }

    private async Task<AcceptedTurnValidationDisposition>
        RunColdReplayThroughGameEngineAsync(
        HeldTreatmentPipelineContext coldContext,
        int expectedTurn,
        string source)
    {
        await coldContext.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(coldContext);
        return await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            source,
            snapshotContext,
            null,
            expectedTurn,
            null);
    }

    private static async Task RestoreSeverityReplayCommandSurfacesAsync(
        HeldTreatmentPipelineContext context,
        byte[] command,
        byte[]? pending)
    {
        await context.FileSystem.WriteFileAtomicAsync(
            context.Lease,
            AcceptedMechanicsPlan.WoundCommandPath,
            Encoding.UTF8.GetString(command).TrimStart('\uFEFF'));
        if (pending is not null)
        {
            await context.FileSystem.WriteFileAtomicAsync(
                context.Lease,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                Encoding.UTF8.GetString(pending).TrimStart('\uFEFF'));
        }
    }

    private static async Task<bool> ContainsExactTreatmentRequestAsync(
        HeldTreatmentPipelineContext context,
        MortalWoundTreatmentAttemptRequest request)
    {
        var commandBytes = await context.ReadFileBytesAsync(
            AcceptedMechanicsPlan.WoundCommandPath);
        var pendingBytes = await context.ReadFileBytesAsync(
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        var command = commandBytes is null
            ? new JsonObject()
            : ParseJsonObjectBytes(commandBytes);
        var pending = pendingBytes is null
            ? null
            : ParseJsonObjectBytes(pendingBytes);
        return MortalWoundTreatmentDurableSurfaceQuarantine.ContainsExactRequestRows(
            command,
            pending,
            request.Coordinates.OperationKey,
            request.Coordinates.AttemptId,
            request.RequestFingerprint);
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
            int currentEnergy = 2,
            int initialSeverityRank = 3,
            bool includeReactionDescendant = false)
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
                targetId,
                initialSeverityRank,
                includeReactionDescendant);
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
            await NormalizeSeverityFixtureJsonBytesAsync(
                fileSystem,
                ResourceMaterializationContract.DefinitionsPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath);
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

    private static async Task<IReadOnlyDictionary<string, byte[]?>>
        CaptureAllSeverityCarrierBytesAsync(FileSystemManager fileSystem)
    {
        var paths = new[]
        {
            WoundCarrierCatalog.PlayerPath,
            WoundCarrierCatalog.NpcPath,
            WoundCarrierCatalog.EnemiesPath,
            EffectCarrierCatalog.PlayerPath,
            EffectCarrierCatalog.NpcPath
        }.Distinct(StringComparer.Ordinal);
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths)
            result.Add(path, await fileSystem.ReadFileBytesAsync(path));
        return result;
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

    private static WoundIdentityState ReadSeverityWoundIdentity(
        FileSystemManager fileSystem)
    {
        var parsed = WoundIdentityState.Parse(
            File.ReadAllText(fileSystem.ResolvePath(WoundIdentityState.StatePath)),
            WoundIdentityState.StatePath);
        Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues));
        return Assert.IsType<WoundIdentityState>(parsed.State);
    }

    private static WoundHistoryTransition
        AssertSeverityWoundIndexAndHistoryAgreement(
            HeldTreatmentPipelineContext context,
            WoundMaterializationEnvelope before,
            WoundMaterializationEnvelope published)
    {
        var identity = ReadSeverityWoundIdentity(context.FileSystem);
        var identityEntry = Assert.Single(identity.Entries, entry => string.Equals(
            entry.WoundId,
            published.WoundId,
            StringComparison.Ordinal));
        Assert.Empty(WoundIdentityState.ValidateActiveAgreement(
            identityEntry,
            published,
            WoundIdentityState.StatePath));
        Assert.Equal(
            WoundIdentityState.ComputeSemanticFingerprint(published),
            identityEntry.SemanticFingerprint);

        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var row = Assert.Single(history.State!.Transitions, transition =>
            string.Equals(
                transition.OperationKey,
                context.Request.Coordinates.OperationKey,
                StringComparison.Ordinal));
        Assert.Equal(published.WoundId, row.WoundId);
        Assert.Equal("treat", row.Kind);
        Assert.Equal(published.LastTransition.TransitionId, row.TransitionId);
        Assert.Equal(published.LastTransition.Ordinal, row.WoundTransitionOrdinal);
        Assert.Equal(context.Request.Coordinates.Turn, row.Turn);
        Assert.Equal(context.Request.Coordinates.EventRef, row.EventRef);
        Assert.Equal(
            WoundIdentityState.ComputeSemanticFingerprint(before),
            row.BeforeFingerprint);
        Assert.Equal(
            WoundIdentityState.ComputeSemanticFingerprint(published),
            row.AfterFingerprint);
        Assert.Equal(context.Request.RequestFingerprint, row.SourceFingerprint);
        Assert.Equal(context.Request.Coordinates.AttemptId, row.AttemptId);
        Assert.False(row.Terminal);
        Assert.Equal(WoundAcceptedTurnPlanner.TreatmentPublicationSummary,
            row.ReadableSummary);
        Assert.Equal(
            WoundHistoryState.ComputeOutputFingerprint(
                row.OperationKey,
                row.EventRef,
                row.ReadableSummary),
            row.OutputFingerprint);

        var result = Assert.IsType<MortalWoundTreatmentPersistedResult>(
            row.TreatmentResult);
        Assert.Equal(context.Request.RequestFingerprint,
            result.Request.RequestFingerprint);
        Assert.Equal(context.Request.Coordinates.CoordinatesFingerprint,
            result.Request.Coordinates.CoordinatesFingerprint);
        Assert.Equal(context.Request.Coordinates.OperationKey,
            result.Request.Coordinates.OperationKey);
        Assert.Equal(context.Request.Coordinates.AttemptId,
            result.Request.Coordinates.AttemptId);
        Assert.Equal(context.Request.Coordinates.WoundId,
            result.Request.Coordinates.WoundId);
        Assert.Equal(context.Request.Coordinates.RouteId,
            result.Request.Coordinates.RouteId);
        Assert.Equal(context.Resolution.RequestFingerprint,
            result.Receipt.RequestFingerprint);
        Assert.Equal(context.Resolution.ResultFingerprint,
            result.Receipt.ResultFingerprint);
        Assert.Equal(context.Resolution.ResultCategory,
            result.Receipt.ResultCategory);
        Assert.Equal(context.Resolution.RouteCompletion,
            result.Receipt.RouteCompletion);
        Assert.True(result.Receipt.HasMatchingFingerprint());

        var physicalIdentity = JsonNode.Parse(File.ReadAllText(
            context.FileSystem.ResolvePath(WoundIdentityState.StatePath)));
        var physicalHistory = JsonNode.Parse(File.ReadAllText(
            context.FileSystem.ResolvePath(WoundHistoryState.HistoryPath)));
        Assert.True(JsonNode.DeepEquals(
            context.Plan.WoundIdentityAfterImage,
            physicalIdentity));
        Assert.True(JsonNode.DeepEquals(
            context.Plan.WoundHistoryAfterImage,
            physicalHistory));
        return row;
    }

    private static JsonObject ReadSeverityHistoryRowJson(
        FileSystemManager fileSystem,
        string operationKey)
    {
        var root = JsonNode.Parse(File.ReadAllText(
            fileSystem.ResolvePath(WoundHistoryState.HistoryPath)))!.AsObject();
        return root["transitions"]!.AsArray()
            .OfType<JsonObject>()
            .Single(row => string.Equals(
                row["operationKey"]?.GetValue<string>(),
                operationKey,
                StringComparison.Ordinal));
    }

    private static async Task AssertSeverityTreatmentSpendAgreementAsync(
        FileSystemManager fileSystem,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        AcceptedMechanicsPlan plan)
    {
        var authority = Assert.IsType<
            MortalWoundTreatmentResourcePublicationAuthority>(
            plan.TreatmentResourcePublicationAuthority);
        Assert.Equal(request.RequestFingerprint, authority.RequestFingerprint);
        Assert.Equal(resolution.ResultFingerprint, authority.ResultFingerprint);
        var planned = Assert.Single(plan.ResourceEvents, candidate =>
            string.Equals(
                candidate.EventKind,
                "resource_spent",
                StringComparison.Ordinal) &&
            candidate.Coordinate.OwnerKind == ResourceOwnerKind.Player &&
            string.Equals(
                candidate.Coordinate.ResourceOwnerId,
                "player_current",
                StringComparison.Ordinal) &&
            string.Equals(
                candidate.Coordinate.ResourceKey,
                "energy",
                StringComparison.Ordinal));
        var history = await ReadTreatmentResourceHistoryAsync(fileSystem);
        var actual = Assert.Single(history.Transitions, transition =>
            string.Equals(
                transition.OperationId,
                planned.OperationId,
                StringComparison.Ordinal));
        Assert.Equal(ResourceTransitionOperation.Spend, actual.Operation);
        Assert.Equal(ResourceMutationPhase.RegisteredSystemOutcome, actual.Phase);
        Assert.Equal(planned.EventRef, actual.EventRef);
        Assert.Equal(planned.Coordinate, actual.Coordinate);
        Assert.Equal(planned.Before, actual.BeforeState?.Current);
        Assert.Equal(planned.After, actual.AfterState?.Current);
        Assert.Equal(planned.AppliedAmount, actual.AppliedAmount);
        Assert.Equal(planned.Turn, actual.Turn);
        Assert.Equal(planned.ExecutionSequence, actual.ExecutionSequence);
        Assert.Equal(
            planned.SourceFingerprint,
            actual.SourceEvidence.AuthorityFingerprint);
    }

    private static async Task AssertSeverityProcedureClaimReleasedAsync(
        HeldTreatmentPipelineContext context,
        MortalWoundTreatmentAttemptRequest request)
    {
        await context.AcquireLeaseAsync();
        var releasedState = ExportCurrentTreatmentAcceptedState(context);
        Assert.False(AcceptedTurnAuthorityRegistry
            .HasLiveMortalWoundProcedureReservationAgreement(
                context.FileSystem,
                context.Lease,
                releasedState,
                RequireProcedureAuthority(request)));
        await context.ReleaseLeaseAsync();
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
        Assert.Equal(new[] { entry.EffectId }, transition.SourceEffectIds);
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
        Assert.Equal(new[] { predecessorEffectId }, create.SourceEffectIds);
        Assert.Equal(new[] { entry.EffectId }, create.ResultEffectIds);
    }

    private static void AssertFreshSeverityEffectIds(
        IEnumerable<string> retiredEffectIds,
        IEnumerable<string> freshEffectIds)
    {
        var retired = retiredEffectIds.ToArray();
        var fresh = freshEffectIds.ToArray();
        Assert.Empty(retired.Intersect(fresh, StringComparer.Ordinal));
        var retiredConfusable = retired
            .Select(ResourceMaterializationContract.BuildConfusableKey)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(
            fresh,
            effectId => retiredConfusable.Contains(
                ResourceMaterializationContract.BuildConfusableKey(effectId)));
    }

    private static void AssertSingleSeverityGenerationSuccessor(
        EffectIdentityState identity,
        string predecessorEffectId,
        string definitionKey,
        string expectedEffectId)
    {
        var successors = identity.Entries.Where(entry =>
                string.Equals(
                    entry.Source["kind"]?.GetValue<string>(),
                    "wound",
                    StringComparison.Ordinal) &&
                string.Equals(
                    entry.Source["sourceId"]?.GetValue<string>(),
                    HeldTreatmentPipelineContext.WoundId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    entry.Source["definitionKey"]?.GetValue<string>(),
                    definitionKey,
                    StringComparison.Ordinal) &&
                entry.Transitions.Any(transition =>
                    string.Equals(transition.Kind, "create", StringComparison.Ordinal) &&
                    transition.SourceEffectIds.SequenceEqual(
                        new[] { predecessorEffectId },
                        StringComparer.Ordinal)))
            .ToArray();
        Assert.Equal(expectedEffectId, Assert.Single(successors).EffectId);
    }

    private static JsonObject CreateSeverityReductionTreatmentWound(
            int rootCount,
            int reductionSteps,
            bool includeEnergyResource,
            string mode,
            string targetKind = "player",
            string targetId = "player_current",
            int initialSeverityRank = 3,
            bool includeReactionDescendant = false)
    {
        Assert.InRange(rootCount, 0, 2);
        Assert.InRange(reductionSteps, 1, 2);
        Assert.InRange(initialSeverityRank, 3, 4);
        Assert.True(initialSeverityRank - reductionSteps >= 1);
        var wound = WoundContractTestData.CreateActiveWound(
            HeldTreatmentPipelineContext.WoundId,
            "mortal_world",
            targetKind,
            targetId,
            ResolveSeverityCarrierPath(targetKind));
        var initialSeverity = initialSeverityRank == 4 ? "IV" : "III";
        wound["severity"]!["value"] = initialSeverity;
        wound["severity"]!["rank"] = initialSeverityRank;
        wound["severity"]!["maximumAtCreation"] = initialSeverity;
        wound["consequences"]!["slotBudget"] = initialSeverityRank;
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
        if (includeReactionDescendant)
        {
            Assert.True(rootCount > 0);
            AddSeverityReactionDefinitionGraph(wound, targetKind);
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

    private static void AddSeverityReactionDefinitionGraph(
        JsonObject wound,
        string targetKind)
    {
        var effectTargetKind = targetKind switch
        {
            "player" => "player",
            "npc" => "npc",
            "combatant" => "combatant",
            _ => throw new ArgumentOutOfRangeException(
                nameof(targetKind), targetKind, null)
        };
        var sources = wound["consequences"]!["ownedEffectSources"]!.AsObject();
        var rootBinding = Assert.IsType<JsonObject>(sources["rootBindings"]![0]);
        var rootEffectId = rootBinding["effectId"]!.GetValue<string>();
        var rootDefinitionKey = rootBinding["definitionKey"]!.GetValue<string>();
        var definitions = sources["definitions"]!.AsArray();
        var rootIndex = definitions.Select((definition, index) => (definition, index))
            .Single(pair => string.Equals(
                pair.definition!["definitionKey"]!.GetValue<string>(),
                rootDefinitionKey,
                StringComparison.Ordinal))
            .index;
        var reactionRoot = WoundContractTestData.CreateApplyDefinitionRoot(
            HeldTreatmentPipelineContext.WoundId,
            "mortal_world",
            rootDefinitionKey,
            SeverityReactionChildDefinitionKey);
        reactionRoot["allowedTargetKinds"] = new JsonArray(effectTargetKind);
        definitions[rootIndex] = reactionRoot;
        definitions.Add(WoundContractTestData.CreateOwnedEffectDefinition(
            HeldTreatmentPipelineContext.WoundId,
            "mortal_world",
            SeverityReactionChildDefinitionKey,
            "wound_consequence",
            targetKind: effectTargetKind));
        var entry = wound["consequences"]!["entries"]!.AsArray()
            .OfType<JsonObject>()
            .Single(candidate => string.Equals(
                candidate["effectId"]!.GetValue<string>(),
                rootEffectId,
                StringComparison.Ordinal));
        entry["profileKey"] = "event_reaction";
        entry["readableSummary"] =
            "The wound reaction owns one real materialized descendant.";
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
            .ToList();
        var parsedWound = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            ResolveSeverityCarrierPath(targetKind));
        Assert.True(
            parsedWound.IsValid,
            DescribeValidationIssues(parsedWound.Issues));
        var canonicalWound = Assert.IsType<WoundMaterializationEnvelope>(
            parsedWound.Wound);
        var reactionFact = canonicalWound.Consequences.OwnedEffectSources
            .DefinitionFacts.SingleOrDefault(fact =>
                fact.ApplyDefinitionTargets.Contains(
                    SeverityReactionChildDefinitionKey,
                    StringComparer.Ordinal));
        string? reactionRootEffectId = null;
        if (reactionFact is not null)
        {
            reactionRootEffectId = canonicalWound.Consequences.OwnedEffectSources
                .RootBindings.Single(binding => string.Equals(
                    binding.DefinitionKey,
                    reactionFact.DefinitionKey,
                    StringComparison.Ordinal)).EffectId;
            var childDefinition = canonicalWound.Consequences.OwnedEffectSources
                .Definitions
                .Select(static definition =>
                    JsonNode.Parse(definition.GetRawText())!.AsObject())
                .Single(definition => string.Equals(
                    definition["definitionKey"]!.GetValue<string>(),
                    SeverityReactionChildDefinitionKey,
                    StringComparison.Ordinal));
            effects.Add(CreateCanonicalHeldTreatmentWoundEffect(
                canonicalWound,
                childDefinition,
                SeverityReactionChildEffectId));
        }
        for (var index = 0; index < effects.Count; index++)
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

        var woundFingerprint = WoundIdentityState.ComputeSemanticFingerprint(
            canonicalWound);
        await fileSystem.WriteFileAtomicAsync(
            WoundIdentityState.StatePath,
            WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry(
                    ownerKind: targetKind,
                    ownerId: targetId,
                    carrierPath: ResolveSeverityCarrierPath(targetKind),
                    semanticFingerprint: woundFingerprint)).ToJsonString());

        var identity = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
        var entries = identity["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        for (var index = 0; index < entries.Length; index++)
        {
            var ordinal = index + 1;
            var transition = Assert.IsType<JsonObject>(Assert.Single(
                entries[index]["transitions"]!.AsArray()));
            transition["transitionId"] = $"effect_transition_t070b6_seed_{ordinal}";
            transition["eventRef"] = $"turn_42:t070b6_seed:{ordinal}";
            if (string.Equals(
                    entries[index]["effectId"]!.GetValue<string>(),
                    SeverityReactionChildEffectId,
                    StringComparison.Ordinal))
            {
                transition["sourceEffectIds"] =
                    new JsonArray(Assert.IsType<string>(reactionRootEffectId));
            }
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
        provider["tradeInventoryReceipts"] ??= new JsonArray();
        await fileSystem.WriteFileAtomicAsync(
            NpcCoreChangesContract.NpcCorePath,
            npcCore.ToJsonString());
    }

    private static async Task NormalizeSeverityFixtureJsonBytesAsync(
        FileSystemManager fileSystem,
        params string[] paths)
    {
        foreach (var path in paths)
        {
            var root = JsonNode.Parse(Assert.IsType<string>(
                await fileSystem.ReadFileAsync(path)));
            Assert.NotNull(root);
            await fileSystem.WriteFileAtomicAsync(
                path,
                root.ToJsonString(
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
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
        var reactionRootEffectId = FindSeverityReactionRootEffectId(
            ReadSeverityTreatmentWound(fileSystem, targetKind, targetId));
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
            if (string.Equals(
                    effectId,
                    SeverityReactionChildEffectId,
                    StringComparison.Ordinal))
            {
                transition["sourceEffectIds"] =
                    new JsonArray(Assert.IsType<string>(reactionRootEffectId));
            }
        }
        await fileSystem.WriteFileAtomicAsync(
            EffectIdentityState.StatePath,
            identity.ToJsonString());
    }

    private static string? FindSeverityReactionRootEffectId(
        WoundMaterializationEnvelope wound)
    {
        var reactionFact = wound.Consequences.OwnedEffectSources.DefinitionFacts
            .SingleOrDefault(fact => fact.ApplyDefinitionTargets.Contains(
                SeverityReactionChildDefinitionKey,
                StringComparer.Ordinal));
        return reactionFact is null
            ? null
            : wound.Consequences.OwnedEffectSources.RootBindings
                .Single(binding => string.Equals(
                    binding.DefinitionKey,
                    reactionFact.DefinitionKey,
                    StringComparison.Ordinal))
                .EffectId;
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
