using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectPendingWaveIntegrationTests
{
    private const string RootEventRef = "turn_43:resource:1";
    private const string WaveZeroTriggerId = "trigger_wave0_damage";
    private const string WaveOneTriggerId = "trigger_wave1_restore";
    private const string WaveZeroComponentId = "component_wave0_damage";
    private const string WaveOneComponentId = "component_wave1_restore";

    private static readonly string[] MechanicalPaths =
    {
        ResourceMaterializationContract.DefinitionsPath,
        ResourceMaterializationContract.StatePath,
        ResourceMaterializationContract.HistoryPath,
        CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
        EffectMaterializationTestContext.PlayerEffectsPath,
        EffectMaterializationTestContext.IdentityIndexPath
    };

    [Fact]
    public async Task CausalPendingWaves_RemainAtomicUntilTerminalReplay()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedMortalPlayerResourcesAsync(turn: 42);
        var definitions = ParseDefinitions(await context.ReadJsonAsync(
            ResourceMaterializationContract.DefinitionsPath));
        var initialState = ParseState(
            await context.ReadJsonAsync(ResourceMaterializationContract.StatePath),
            definitions);
        var initialHistory = ParseHistory(
            await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath),
            definitions);
        var initialPoise = Assert.Single(
            initialState.Entries,
            static entry => entry.Coordinate.ResourceKey == "poise");
        var initialEnergy = Assert.Single(
            initialState.Entries,
            static entry => entry.Coordinate.ResourceKey == "energy");
        var (definition, effect) = CreateTwoWaveEffect(initialPoise.Maximum);

        await context.SeedPlayerSkillSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject { ["worldEventsLog"] = new JsonArray() });
        await context.WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
                {
                    ["applicationState"] = "active",
                    ["pendingSurvivalEffects"] = new JsonArray()
                }
            });
        await SeedCurrentOwnerAuthorityAsync(
            context,
            definitions,
            initialState,
            initialHistory);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var initialBackups = await context.ReadPendingSnapshotBackupsAsync();
        var unpublishedBaseline = await context.CaptureBytesAsync(MechanicalPaths);

        var resourceCommand = CreateRootSpendCommand();
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            resourceCommand);

        var waveZeroPlan = await ValidateAndNormalizeAsync(context, initialBackups);

        Assert.True(waveZeroPlan.AwaitsPendingResolution);
        Assert.Equal(
            unpublishedBaseline,
            await context.CaptureBytesAsync(MechanicalPaths));
        var waveZeroState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            definitions);
        var waveZero = Assert.Single(waveZeroState.Requests);
        Assert.Equal(WaveZeroTriggerId, waveZero.TriggerId);
        Assert.Equal(WaveZeroComponentId, waveZero.CausalAuthority.ComponentId);
        Assert.Equal(0, waveZero.CausalAuthority.WaveOrdinal);
        Assert.True(waveZero.CausalAuthority.ConsumesUse);
        Assert.Equal(2, waveZero.CausalAuthority.UsesBefore);
        Assert.Equal(RootEventRef, waveZero.CausalAuthority.TriggerEventRef);
        Assert.False(string.IsNullOrWhiteSpace(
            waveZero.CausalAuthority.ResourceProducerOperationKey));

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var waveZeroBackups = await context.ReadPendingSnapshotBackupsAsync();
        var waveZeroReceipt = CreateReceipt(
            waveZero.RequestId,
            waveZero.MaximumAmount,
            "The first bounded output depletes poise.");
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            CreateReceiptCommand(waveZeroReceipt));

        var waveOnePlan = await ValidateAndNormalizeAsync(context, waveZeroBackups);

        Assert.True(waveOnePlan.AwaitsPendingResolution);
        Assert.Equal(
            unpublishedBaseline,
            await context.CaptureBytesAsync(MechanicalPaths));
        var waveOneState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            definitions);
        var terminalWaveZero = Assert.Single(waveOneState.TerminalReceipts);
        Assert.Equal(waveZero.RequestId, terminalWaveZero.RequestId);
        Assert.Equal(
            waveZero.CausalAuthority,
            terminalWaveZero.RequestAuthority.CausalAuthority);
        var resolvedWaveZero = Assert.Single(waveOneState.ResolvedPendingBindings);
        Assert.Equal(waveZero.RequestId, resolvedWaveZero.RequestId);
        Assert.Equal(
            waveZero.CausalAuthority,
            resolvedWaveZero.RequestAuthority.CausalAuthority);
        var waveOne = Assert.Single(waveOneState.Requests);
        Assert.Equal(WaveOneTriggerId, waveOne.TriggerId);
        Assert.Equal(WaveOneComponentId, waveOne.CausalAuthority.ComponentId);
        Assert.Equal(1, waveOne.CausalAuthority.WaveOrdinal);
        Assert.False(waveOne.CausalAuthority.ConsumesUse);
        Assert.Null(waveOne.CausalAuthority.UsesBefore);
        Assert.False(string.IsNullOrWhiteSpace(
            waveOne.CausalAuthority.ResourceProducerOperationKey));
        Assert.NotEqual(
            waveZero.CausalAuthority.ResourceProducerOperationKey,
            waveOne.CausalAuthority.ResourceProducerOperationKey);
        Assert.NotEqual(
            waveZero.CausalAuthority.TranscriptPrefixFingerprint,
            waveOne.CausalAuthority.TranscriptPrefixFingerprint);
        Assert.Equal(
            2,
            ReadRemainingUses(await context.ReadJsonAsync(
                EffectMaterializationTestContext.PlayerEffectsPath)));

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var waveOneBackups = await context.ReadPendingSnapshotBackupsAsync();
        var waveOneReceipt = CreateReceipt(
            waveOne.RequestId,
            waveOne.MaximumAmount,
            "The second bounded output restores poise.");
        var fullReceiptCommand = CreateReceiptCommand(
            waveZeroReceipt,
            waveOneReceipt);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            fullReceiptCommand);

        var finalPlan = await ValidateAndNormalizeAsync(context, waveOneBackups);

        Assert.False(finalPlan.AwaitsPendingResolution);
        var finalState = ParseState(
            await context.ReadJsonAsync(ResourceMaterializationContract.StatePath),
            definitions);
        var finalEnergy = Assert.Single(
            finalState.Entries,
            static entry => entry.Coordinate.ResourceKey == "energy");
        var finalPoise = Assert.Single(
            finalState.Entries,
            static entry => entry.Coordinate.ResourceKey == "poise");
        Assert.Equal(initialEnergy.Maximum - 1m, finalEnergy.Current);
        Assert.Equal(initialPoise.Maximum, finalPoise.Current);

        var history = ParseHistory(
            await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath),
            definitions);
        var rootTransition = Assert.Single(
            history.Transitions,
            static transition => transition.EventRef == RootEventRef);
        Assert.Equal(ResourceTransitionOperation.Spend, rootTransition.Operation);
        var effectTransitions = history.Transitions
            .Where(static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger &&
                transition.Turn == 43)
            .OrderBy(static transition => transition.ExecutionSequence)
            .ToArray();
        Assert.Collection(
            effectTransitions,
            transition =>
            {
                Assert.Equal(waveZero.RequestId, transition.ReceiptId);
                Assert.Equal(ResourceTransitionOperation.Damage, transition.Operation);
                Assert.Equal("poise", transition.Coordinate.ResourceKey);
                Assert.True(transition.AppliedAmount > 0m);
            },
            transition =>
            {
                Assert.Equal(waveOne.RequestId, transition.ReceiptId);
                Assert.Equal(ResourceTransitionOperation.Restore, transition.Operation);
                Assert.Equal("poise", transition.Coordinate.ResourceKey);
                Assert.True(transition.AppliedAmount > 0m);
            });
        Assert.True(rootTransition.ExecutionSequence < effectTransitions[0].ExecutionSequence);
        Assert.True(
            effectTransitions[0].ExecutionSequence <
            effectTransitions[1].ExecutionSequence);
        Assert.Equal(
            effectTransitions[0].EventRef,
            waveOne.CausalAuthority.TriggerEventRef);
        Assert.Equal(
            1,
            ReadRemainingUses(await context.ReadJsonAsync(
                EffectMaterializationTestContext.PlayerEffectsPath)));

        var identity = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath));
        var identityEntry = Assert.IsType<JsonObject>(
            Assert.Single(identity["entries"]!.AsArray()));
        var identityTransitions = identityEntry["transitions"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Single(identityTransitions, transition => string.Equals(
            transition["eventRef"]?.GetValue<string>(),
            waveZero.CausalAuthority.ActivationEventRef,
            StringComparison.Ordinal));
        Assert.Single(identityTransitions, transition => string.Equals(
            transition["eventRef"]?.GetValue<string>(),
            waveOne.CausalAuthority.ActivationEventRef,
            StringComparison.Ordinal));

        var terminalState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            definitions);
        Assert.Empty(terminalState.Requests);
        Assert.Equal(2, terminalState.TerminalReceipts.Count);
        Assert.Equal(
            new[] { 0, 1 },
            terminalState.TerminalReceipts
                .Select(static terminal =>
                    terminal.RequestAuthority.CausalAuthority.WaveOrdinal));
        Assert.Equal(2, terminalState.ResolvedPendingBindings.Count);
        Assert.False(context.FileSystem.FileExists(
            ResourceMaterializationContract.CommandPath));
        Assert.False(context.FileSystem.FileExists(
            EffectMaterializationTestContext.CommandPath));

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var replayBackups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            resourceCommand.DeepClone());
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            fullReceiptCommand.DeepClone());
        var protectedReplayPaths = MechanicalPaths
            .Append(ResourcePendingResolutionState.PendingPath)
            .ToArray();
        var beforeReplay = await context.CaptureBytesAsync(protectedReplayPaths);

        var replayPlan = await ValidateAndNormalizeAsync(context, replayBackups);

        Assert.False(replayPlan.AwaitsPendingResolution);
        Assert.Equal(
            beforeReplay,
            await context.CaptureBytesAsync(protectedReplayPaths));
        Assert.False(context.FileSystem.FileExists(
            ResourceMaterializationContract.CommandPath));
        Assert.False(context.FileSystem.FileExists(
            EffectMaterializationTestContext.CommandPath));
    }

    [Fact]
    public async Task LastUsePendingFrontier_ReplaysExactTranscriptAndExpiresOnlyOnce()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedMortalPlayerResourcesAsync(turn: 42);
        var definitions = ParseDefinitions(await context.ReadJsonAsync(
            ResourceMaterializationContract.DefinitionsPath));
        var initialState = ParseState(
            await context.ReadJsonAsync(ResourceMaterializationContract.StatePath),
            definitions);
        var initialHistory = ParseHistory(
            await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath),
            definitions);
        var initialPoise = Assert.Single(
            initialState.Entries,
            static entry => entry.Coordinate.ResourceKey == "poise");
        var initialEnergy = Assert.Single(
            initialState.Entries,
            static entry => entry.Coordinate.ResourceKey == "energy");
        var (definition, effect) = CreateTwoWaveEffect(
            initialPoise.Maximum,
            remainingUses: 1);
        var effectId = effect["effectId"]!.GetValue<string>();

        await context.SeedPlayerSkillSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject { ["worldEventsLog"] = new JsonArray() });
        await context.WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
                {
                    ["applicationState"] = "active",
                    ["pendingSurvivalEffects"] = new JsonArray()
                }
            });
        await SeedCurrentOwnerAuthorityAsync(
            context,
            definitions,
            initialState,
            initialHistory);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var initialBackups = await context.ReadPendingSnapshotBackupsAsync();
        var unpublishedBaseline = await context.CaptureBytesAsync(MechanicalPaths);
        var resourceCommand = CreateRootSpendCommand();
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            resourceCommand);

        var pendingPlan = await ValidateAndNormalizeAsync(context, initialBackups);

        Assert.True(pendingPlan.AwaitsPendingResolution);
        Assert.Empty(pendingPlan.EffectCarrierAfterImages);
        Assert.Empty(pendingPlan.ResourceEvents);
        Assert.Equal(
            unpublishedBaseline,
            await context.CaptureBytesAsync(MechanicalPaths));
        var pendingState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            definitions);
        var request = Assert.Single(pendingState.Requests);
        Assert.Equal(WaveZeroTriggerId, request.TriggerId);
        Assert.Equal(WaveZeroComponentId, request.CausalAuthority.ComponentId);
        Assert.Equal(0, request.CausalAuthority.WaveOrdinal);
        Assert.True(request.CausalAuthority.ConsumesUse);
        Assert.Equal(1, request.CausalAuthority.UsesBefore);
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            request.CausalAuthority.CandidateFingerprint));
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            request.CausalAuthority.TranscriptPrefixFingerprint));
        var acceptedTranscriptPrefix =
            request.CausalAuthority.TranscriptPrefixFingerprint;

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var receiptBackups = await context.ReadPendingSnapshotBackupsAsync();
        var receipt = CreateReceipt(
            request.RequestId,
            request.MaximumAmount,
            "The final bounded use depletes poise.");
        var receiptCommand = CreateReceiptCommand(receipt);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            receiptCommand);

        var finalPlan = await ValidateAndNormalizeAsync(context, receiptBackups);

        Assert.False(finalPlan.AwaitsPendingResolution);
        var finalState = ParseState(
            await context.ReadJsonAsync(ResourceMaterializationContract.StatePath),
            definitions);
        var finalEnergy = Assert.Single(
            finalState.Entries,
            static entry => entry.Coordinate.ResourceKey == "energy");
        var finalPoise = Assert.Single(
            finalState.Entries,
            static entry => entry.Coordinate.ResourceKey == "poise");
        Assert.Equal(initialEnergy.Maximum - 1m, finalEnergy.Current);
        Assert.Equal(0m, finalPoise.Current);

        var finalHistory = ParseHistory(
            await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath),
            definitions);
        var effectTransition = Assert.Single(
            finalHistory.Transitions,
            static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger &&
                transition.Turn == 43);
        Assert.Equal(request.RequestId, effectTransition.ReceiptId);
        Assert.Equal(ResourceTransitionOperation.Damage, effectTransition.Operation);
        Assert.Equal("poise", effectTransition.Coordinate.ResourceKey);
        Assert.Equal(request.MaximumAmount, effectTransition.AppliedAmount);

        var playerEffects = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath));
        Assert.Empty(playerEffects["activeEffects"]!.AsArray());
        var identity = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath));
        var identityEntry = Assert.IsType<JsonObject>(
            Assert.Single(identity["entries"]!.AsArray()));
        Assert.Equal(effectId, identityEntry["effectId"]!.GetValue<string>());
        Assert.Equal("expired", identityEntry["state"]!.GetValue<string>());
        var identityTransitions = identityEntry["transitions"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        var expiration = Assert.Single(identityTransitions, transition =>
            string.Equals(
                transition["kind"]?.GetValue<string>(),
                "expire",
                StringComparison.Ordinal));
        Assert.Equal(
            request.CausalAuthority.ActivationEventRef,
            expiration["eventRef"]!.GetValue<string>());

        var terminalState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            definitions);
        Assert.Empty(terminalState.Requests);
        var terminal = Assert.Single(terminalState.TerminalReceipts);
        Assert.Equal(request.RequestId, terminal.RequestId);
        Assert.Equal(
            request.CausalAuthority,
            terminal.RequestAuthority.CausalAuthority);
        Assert.Equal(
            acceptedTranscriptPrefix,
            terminal.RequestAuthority.CausalAuthority.TranscriptPrefixFingerprint);
        var resolved = Assert.Single(terminalState.ResolvedPendingBindings);
        Assert.Equal(request.RequestId, resolved.RequestId);
        Assert.Equal(
            request.CausalAuthority,
            resolved.RequestAuthority.CausalAuthority);

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var replayBackups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            resourceCommand.DeepClone());
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            receiptCommand.DeepClone());
        var protectedReplayPaths = MechanicalPaths
            .Append(ResourcePendingResolutionState.PendingPath)
            .ToArray();
        var beforeReplay = await context.CaptureBytesAsync(protectedReplayPaths);

        var replayPlan = await ValidateAndNormalizeAsync(context, replayBackups);

        Assert.False(replayPlan.AwaitsPendingResolution);
        Assert.Equal(
            beforeReplay,
            await context.CaptureBytesAsync(protectedReplayPaths));
        Assert.False(context.FileSystem.FileExists(
            ResourceMaterializationContract.CommandPath));
        Assert.False(context.FileSystem.FileExists(
            EffectMaterializationTestContext.CommandPath));
    }

    [Fact]
    public async Task TerminalReplay_ChangedOriginalCommandEnvelopeFailsClosedWithoutAfterImagesOrWrites()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var (definition, effect) = CreateRecurringBoundedTurnEndEffect();
        var seeded = await SeedMortalEffectAsync(context, definition, effect);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var pendingBackups = await context.ReadPendingSnapshotBackupsAsync();
        var originalResourceCommand = CreateRootSpendCommand();
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            originalResourceCommand);

        var pendingPlan = await ValidateAndNormalizeAsync(
            context,
            pendingBackups);

        Assert.True(pendingPlan.AwaitsPendingResolution);
        var pendingState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            seeded.Definitions);
        var request = Assert.Single(pendingState.Requests);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var receiptBackups = await context.ReadPendingSnapshotBackupsAsync();
        var receipt = CreateReceipt(
            request.RequestId,
            amount: 1m,
            "The terminal result damages health.");
        var receiptCommand = CreateReceiptCommand(receipt);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            receiptCommand);

        var terminalPlan = await ValidateAndNormalizeAsync(
            context,
            receiptBackups);

        Assert.False(terminalPlan.AwaitsPendingResolution);
        var terminalState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            seeded.Definitions);
        Assert.Empty(terminalState.Requests);
        var terminal = Assert.Single(terminalState.TerminalReceipts);
        Assert.Equal(request.SessionId, terminal.SessionId);
        Assert.Equal(request.AcceptedRequestId, terminal.AcceptedRequestId);
        Assert.Equal(request.RequestTurn, terminal.RequestTurn);

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var replayBackups = await context.ReadPendingSnapshotBackupsAsync();
        var changedResourceCommand = originalResourceCommand
            .DeepClone()
            .AsObject();
        changedResourceCommand["resourceChanges"]![0]!["amount"] = 2;
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            changedResourceCommand);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            receiptCommand.DeepClone());
        var protectedPaths = MechanicalPaths
            .Concat(new[]
            {
                ResourcePendingResolutionState.PendingPath,
                ResourceMaterializationContract.CommandPath,
                EffectMaterializationTestContext.CommandPath
            })
            .ToArray();
        var beforeReplay = await context.CaptureBytesAsync(protectedPaths);

        var replayIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(replayIssues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Code == "resource_pending_terminal_replay_conflict");
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            context.FileSystem));
        await using var replayLease = await context.FileSystem
            .AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            context.Normalizer.BindTo(replayLease)
                .NormalizeAcceptedMechanicsAsync(replayBackups));
        Assert.Equal(
            beforeReplay,
            await context.CaptureBytesAsync(protectedPaths));
    }

    [Fact]
    public async Task IndependentAcceptedTurn_RestartsAtWaveZeroAndReplaysOnlyItsOwnTerminalBindings()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var (definition, effect) = CreateRecurringBoundedTurnEndEffect();
        var seeded = await SeedMortalEffectAsync(context, definition, effect);
        var initialHealth = Assert.Single(
            seeded.State.Entries,
            static entry => entry.Coordinate.ResourceKey == "health");

        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            playerAction: "Resolve the first independent bounded turn.");
        var firstTurnBackups = await context.ReadPendingSnapshotBackupsAsync();
        var firstPendingPlan = await ValidateAndNormalizeAsync(
            context,
            firstTurnBackups);

        Assert.True(firstPendingPlan.AwaitsPendingResolution);
        var firstPendingState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            seeded.Definitions);
        var firstRequest = Assert.Single(firstPendingState.Requests);
        Assert.Equal(43, firstRequest.RequestTurn);
        Assert.Equal(0, firstRequest.CausalAuthority.WaveOrdinal);

        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            playerAction: "Resolve the first independent bounded turn.");
        var firstReceiptBackups = await context.ReadPendingSnapshotBackupsAsync();
        var firstReceipt = CreateReceipt(
            firstRequest.RequestId,
            amount: 1m,
            "The first independent turn damages health.");
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            CreateReceiptCommand(firstReceipt));

        var firstFinalPlan = await ValidateAndNormalizeAsync(
            context,
            firstReceiptBackups);

        Assert.False(firstFinalPlan.AwaitsPendingResolution);
        var firstTerminalState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            seeded.Definitions);
        Assert.Empty(firstTerminalState.Requests);
        var firstTerminal = Assert.Single(firstTerminalState.TerminalReceipts);
        Assert.Equal(firstRequest.RequestId, firstTerminal.RequestId);
        Assert.Equal(43, firstTerminal.RequestTurn);

        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 44,
            playerAction: "Resolve a new independent bounded turn.");
        var secondTurnBackups = await context.ReadPendingSnapshotBackupsAsync();
        var secondTurnBaseline = await context.CaptureBytesAsync(MechanicalPaths);

        var secondPendingPlan = await ValidateAndNormalizeAsync(
            context,
            secondTurnBackups);

        Assert.True(secondPendingPlan.AwaitsPendingResolution);
        Assert.Equal(
            secondTurnBaseline,
            await context.CaptureBytesAsync(MechanicalPaths));
        var secondPendingState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            seeded.Definitions);
        var retainedFirstTerminal = Assert.Single(
            secondPendingState.TerminalReceipts);
        Assert.Equal(firstRequest.RequestId, retainedFirstTerminal.RequestId);
        var secondRequest = Assert.Single(secondPendingState.Requests);
        Assert.Equal(44, secondRequest.RequestTurn);
        Assert.NotEqual(firstRequest.RequestId, secondRequest.RequestId);
        Assert.NotEqual(
            firstRequest.FullTurnFingerprint,
            secondRequest.FullTurnFingerprint);
        Assert.NotEqual(
            firstRequest.CausalAuthority.ActivationEventRef,
            secondRequest.CausalAuthority.ActivationEventRef);
        Assert.Equal(0, secondRequest.CausalAuthority.WaveOrdinal);

        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 44,
            playerAction: "Resolve a new independent bounded turn.");
        var secondReceiptBackups = await context.ReadPendingSnapshotBackupsAsync();
        var secondReceipt = CreateReceipt(
            secondRequest.RequestId,
            amount: 1m,
            "The second independent turn damages health.");
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            CreateReceiptCommand(secondReceipt));

        var secondFinalPlan = await ValidateAndNormalizeAsync(
            context,
            secondReceiptBackups);

        Assert.False(secondFinalPlan.AwaitsPendingResolution);
        var finalState = ParseState(
            await context.ReadJsonAsync(ResourceMaterializationContract.StatePath),
            seeded.Definitions);
        var finalHealth = Assert.Single(
            finalState.Entries,
            static entry => entry.Coordinate.ResourceKey == "health");
        Assert.Equal(initialHealth.Current - 2m, finalHealth.Current);
        var finalHistory = ParseHistory(
            await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath),
            seeded.Definitions);
        var effectTransitions = finalHistory.Transitions
            .Where(static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger)
            .OrderBy(static transition => transition.ExecutionSequence)
            .ToArray();
        Assert.Collection(
            effectTransitions,
            transition =>
            {
                Assert.Equal(43, transition.Turn);
                Assert.Equal(firstRequest.RequestId, transition.ReceiptId);
            },
            transition =>
            {
                Assert.Equal(44, transition.Turn);
                Assert.Equal(secondRequest.RequestId, transition.ReceiptId);
            });
        var finalPendingState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            seeded.Definitions);
        Assert.Empty(finalPendingState.Requests);
        Assert.Equal(2, finalPendingState.TerminalReceipts.Count);
        Assert.Equal(
            new[] { 43, 44 },
            finalPendingState.TerminalReceipts
                .Select(static terminal => terminal.RequestTurn)
                .OrderBy(static turn => turn));
        Assert.All(
            finalPendingState.TerminalReceipts,
            static terminal => Assert.Equal(
                0,
                terminal.RequestAuthority.CausalAuthority.WaveOrdinal));
        Assert.Equal(2, finalPendingState.ResolvedPendingBindings.Count);
        Assert.Equal(
            1,
            ReadRemainingTurns(await context.ReadJsonAsync(
                EffectMaterializationTestContext.PlayerEffectsPath)));
    }

    [Fact]
    public async Task BoundedAfterComponent_SharedActivationEventRefWaitsForPredecessorWave()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var (definition, effect) = CreateBoundedAfterComponentEffect();
        var seeded = await SeedMortalEffectAsync(context, definition, effect);
        var initialHealth = Assert.Single(
            seeded.State.Entries,
            static entry => entry.Coordinate.ResourceKey == "health");

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var initialBackups = await context.ReadPendingSnapshotBackupsAsync();
        var unpublishedBaseline = await context.CaptureBytesAsync(MechanicalPaths);

        var predecessorPlan = await ValidateAndNormalizeAsync(
            context,
            initialBackups);

        Assert.True(predecessorPlan.AwaitsPendingResolution);
        Assert.Equal(
            unpublishedBaseline,
            await context.CaptureBytesAsync(MechanicalPaths));
        var predecessorState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            seeded.Definitions);
        var predecessor = Assert.Single(predecessorState.Requests);
        Assert.Equal("component_bounded_predecessor", predecessor.CausalAuthority.ComponentId);
        Assert.Null(predecessor.CausalAuthority.AfterComponentId);
        Assert.Equal(0, predecessor.CausalAuthority.WaveOrdinal);

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var predecessorBackups = await context.ReadPendingSnapshotBackupsAsync();
        var predecessorReceipt = CreateReceipt(
            predecessor.RequestId,
            amount: 1m,
            "The bounded predecessor damages health.");
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            CreateReceiptCommand(predecessorReceipt));

        var dependentPlan = await ValidateAndNormalizeAsync(
            context,
            predecessorBackups);

        Assert.True(dependentPlan.AwaitsPendingResolution);
        Assert.Equal(
            unpublishedBaseline,
            await context.CaptureBytesAsync(MechanicalPaths));
        var dependentState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            seeded.Definitions);
        var terminalPredecessor = Assert.Single(dependentState.TerminalReceipts);
        Assert.Equal(predecessor.RequestId, terminalPredecessor.RequestId);
        var dependent = Assert.Single(dependentState.Requests);
        Assert.Equal("component_bounded_dependent", dependent.CausalAuthority.ComponentId);
        Assert.Equal(
            "component_bounded_predecessor",
            dependent.CausalAuthority.AfterComponentId);
        Assert.Equal(1, dependent.CausalAuthority.WaveOrdinal);
        Assert.Equal(predecessor.EventRef, dependent.EventRef);
        Assert.Equal(
            predecessor.CausalAuthority.ActivationEventRef,
            dependent.CausalAuthority.ActivationEventRef);
        Assert.Equal(
            predecessor.CausalAuthority.TriggerEventRef,
            dependent.CausalAuthority.TriggerEventRef);
        Assert.Equal(
            predecessor.CausalAuthority.ActivationOrdinal,
            dependent.CausalAuthority.ActivationOrdinal);
        Assert.Equal(
            predecessor.CausalAuthority.CandidateFingerprint,
            dependent.CausalAuthority.CandidateFingerprint);
        Assert.Equal(
            predecessor.CausalAuthority.TranscriptPrefixFingerprint,
            dependent.CausalAuthority.TranscriptPrefixFingerprint);

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var dependentBackups = await context.ReadPendingSnapshotBackupsAsync();
        var dependentReceipt = CreateReceipt(
            dependent.RequestId,
            amount: 1m,
            "The bounded dependent restores health after its predecessor.");
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            CreateReceiptCommand(predecessorReceipt, dependentReceipt));

        var finalPlan = await ValidateAndNormalizeAsync(
            context,
            dependentBackups);

        Assert.False(finalPlan.AwaitsPendingResolution);
        var finalState = ParseState(
            await context.ReadJsonAsync(ResourceMaterializationContract.StatePath),
            seeded.Definitions);
        var finalHealth = Assert.Single(
            finalState.Entries,
            static entry => entry.Coordinate.ResourceKey == "health");
        Assert.Equal(initialHealth.Current, finalHealth.Current);
        var finalHistory = ParseHistory(
            await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath),
            seeded.Definitions);
        var effectTransitions = finalHistory.Transitions
            .Where(static transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger &&
                transition.Turn == 43)
            .OrderBy(static transition => transition.ExecutionSequence)
            .ToArray();
        Assert.Collection(
            effectTransitions,
            transition =>
            {
                Assert.Equal(predecessor.RequestId, transition.ReceiptId);
                Assert.Equal(ResourceTransitionOperation.Damage, transition.Operation);
                Assert.Equal(predecessor.EventRef, transition.EventRef);
            },
            transition =>
            {
                Assert.Equal(dependent.RequestId, transition.ReceiptId);
                Assert.Equal(ResourceTransitionOperation.Restore, transition.Operation);
                Assert.Equal(dependent.EventRef, transition.EventRef);
            });
        Assert.True(
            effectTransitions[0].ExecutionSequence <
            effectTransitions[1].ExecutionSequence);
        var terminalState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            seeded.Definitions);
        Assert.Empty(terminalState.Requests);
        Assert.Equal(
            new[] { 0, 1 },
            terminalState.TerminalReceipts.Select(static terminal =>
                terminal.RequestAuthority.CausalAuthority.WaveOrdinal));
        Assert.Equal(2, terminalState.ResolvedPendingBindings.Count);
        Assert.Equal(
            2,
            ReadRemainingTurns(await context.ReadJsonAsync(
                EffectMaterializationTestContext.PlayerEffectsPath)));
    }

    private static (JsonObject Definition, JsonObject Effect) CreateTwoWaveEffect(
        decimal poiseMaximum,
        int remainingUses = 2)
    {
        if (remainingUses <= 0)
            throw new ArgumentOutOfRangeException(nameof(remainingUses));

        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_damage");
        var waveZeroComponent = definition["components"]![0]!
            .DeepClone()
            .AsObject();
        waveZeroComponent["componentId"] = WaveZeroComponentId;
        waveZeroComponent["priority"] = 10;
        waveZeroComponent["payload"]!["resource"] = "poise";
        waveZeroComponent["payload"]!["amount"] = poiseMaximum;
        var waveOneComponent = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore")["components"]![0]!
            .DeepClone()
            .AsObject();
        waveOneComponent["componentId"] = WaveOneComponentId;
        waveOneComponent["priority"] = 20;
        waveOneComponent["payload"]!["resource"] = "poise";
        waveOneComponent["payload"]!["amount"] = poiseMaximum;
        var components = new JsonArray(
            waveZeroComponent.DeepClone(),
            waveOneComponent.DeepClone());
        var triggers = new JsonArray(
            CreateTrigger(
                WaveZeroTriggerId,
                "resource_spent",
                WaveZeroComponentId,
                priority: 10,
                consumesUse: true),
            CreateTrigger(
                WaveOneTriggerId,
                "resource_depleted",
                WaveOneComponentId,
                priority: 20,
                consumesUse: false));
        definition["components"] = components.DeepClone();
        definition["parameterBounds"] = new JsonObject
        {
            ["amount"] = new JsonObject
            {
                ["kind"] = "number",
                ["minimum"] = 1,
                ["maximum"] = poiseMaximum
            }
        };
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = remainingUses,
            ["consumingEventTypes"] = new JsonArray("resource_spent")
        };
        definition["triggers"] = triggers.DeepClone();

        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "periodic_damage");
        effect["source"] = CreatePendingEffectSource();
        effect["display"]!["sourceLabel"] = "Кровавый след";
        effect["components"] = components.DeepClone();
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = remainingUses,
            ["consumingTriggerIds"] = new JsonArray(WaveZeroTriggerId)
        };
        effect["triggers"] = triggers.DeepClone();
        return (definition, effect);
    }

    private static (JsonObject Definition, JsonObject Effect)
        CreateRecurringBoundedTurnEndEffect()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_damage");
        definition["components"]![0]!["payload"]!["amount"] = 1;
        definition["triggers"]![0]!["resolutionMode"] = "bounded_receipt";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "periodic_damage");
        effect["source"] = CreatePendingEffectSource();
        effect["display"]!["sourceLabel"] = "Кровавый след";
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        effect["lifetime"]!["remainingTurns"] = 3;
        return (definition, effect);
    }

    private static (JsonObject Definition, JsonObject Effect)
        CreateBoundedAfterComponentEffect()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "event_reaction");
        var reaction = definition["components"]![0]!
            .DeepClone()
            .AsObject();
        reaction["componentId"] = "component_bounded_dispatch";
        reaction["priority"] = 20;
        reaction["payload"] = new JsonObject
        {
            ["eventType"] = "owner_turn_end",
            ["resultKind"] = "bounded_receipt",
            ["componentId"] = "component_bounded_dependent",
            ["dependency"] = "after_component",
            ["afterComponentId"] = "component_bounded_predecessor",
            ["maxExpansion"] = 1
        };
        var predecessor = EffectMaterializationTestFixture
            .CreateDefinition("periodic_damage")["components"]![0]!
            .DeepClone()
            .AsObject();
        predecessor["componentId"] = "component_bounded_predecessor";
        predecessor["priority"] = 10;
        predecessor["payload"]!["amount"] = 1;
        var dependent = EffectMaterializationTestFixture
            .CreateDefinition("periodic_restore")["components"]![0]!
            .DeepClone()
            .AsObject();
        dependent["componentId"] = "component_bounded_dependent";
        dependent["priority"] = 30;
        dependent["payload"]!["amount"] = 1;
        var components = new JsonArray(
            reaction.DeepClone(),
            predecessor.DeepClone(),
            dependent.DeepClone());
        var triggers = new JsonArray(new JsonObject
        {
            ["triggerId"] = "on_owner_turn_end",
            ["eventType"] = "owner_turn_end",
            ["priority"] = 100,
            ["componentIds"] = new JsonArray(
                "component_bounded_predecessor",
                "component_bounded_dispatch"),
            ["consumeUses"] = false,
            ["resolutionMode"] = "bounded_receipt"
        });
        definition["components"] = components.DeepClone();
        definition["triggers"] = triggers.DeepClone();

        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        effect["source"] = CreatePendingEffectSource();
        effect["display"]!["sourceLabel"] = "Кровавый след";
        effect["components"] = components.DeepClone();
        effect["triggers"] = triggers.DeepClone();
        effect["lifetime"]!["remainingTurns"] = 3;
        return (definition, effect);
    }

    private static async Task<SeededMortalEffect> SeedMortalEffectAsync(
        EffectMaterializationTestContext context,
        JsonObject definition,
        JsonObject effect)
    {
        await context.SeedMortalPlayerResourcesAsync(turn: 42);
        var definitions = ParseDefinitions(await context.ReadJsonAsync(
            ResourceMaterializationContract.DefinitionsPath));
        var state = ParseState(
            await context.ReadJsonAsync(ResourceMaterializationContract.StatePath),
            definitions);
        var history = ParseHistory(
            await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath),
            definitions);
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject { ["worldEventsLog"] = new JsonArray() });
        await context.WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
                {
                    ["applicationState"] = "active",
                    ["pendingSurvivalEffects"] = new JsonArray()
                }
            });
        await SeedCurrentOwnerAuthorityAsync(context, definitions, state, history);
        return new SeededMortalEffect(definitions, state);
    }

    private static JsonObject CreateTrigger(
        string triggerId,
        string eventType,
        string componentId,
        int priority,
        bool consumesUse) => new()
    {
        ["triggerId"] = triggerId,
        ["eventType"] = eventType,
        ["priority"] = priority,
        ["componentIds"] = new JsonArray(componentId),
        ["consumeUses"] = consumesUse,
        ["resolutionMode"] = "bounded_receipt"
    };

    private static JsonObject CreateRootSpendCommand() => new()
    {
        ["resourceDefinitionCreations"] = new JsonArray(),
        ["resourceCapacityChanges"] = new JsonArray(),
        ["resourceChanges"] = new JsonArray(new JsonObject
        {
            ["operation"] = "spend",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["resourceKey"] = "energy",
            ["amount"] = 1,
            ["source"] = new JsonObject
            {
                ["kind"] = "action_cost"
            },
            ["eventRef"] = RootEventRef,
            ["reason"] = "The accepted action spends one energy."
        })
    };

    private static JsonObject CreatePendingEffectSource() =>
        new()
        {
            ["kind"] = "skill",
            ["sourceId"] = EffectMaterializationTestContext.MaterializableSkillId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };

    private static JsonObject CreateReceipt(
        string requestId,
        decimal amount,
        string reason) => new()
    {
        ["requestId"] = requestId,
        ["resultKind"] = "resource_delta",
        ["amount"] = amount,
        ["reason"] = reason
    };

    private static JsonObject CreateReceiptCommand(params JsonObject[] receipts) =>
        new()
        {
            ["effectChanges"] = new JsonArray(),
            ["effectResolutionReceipts"] = new JsonArray(
                receipts.Select(static receipt => receipt.DeepClone()).ToArray()),
            ["effectEventReports"] = new JsonArray()
        };

    private static async Task<AcceptedMechanicsPlan> ValidateAndNormalizeAsync(
        EffectMaterializationTestContext context,
        IReadOnlyDictionary<string, string> backups)
    {
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoErrors(issues);
        await using var writeLease = await context.FileSystem
            .AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);
        return Assert.IsType<AcceptedMechanicsPlan>(plan);
    }

    private static async Task SeedCurrentOwnerAuthorityAsync(
        EffectMaterializationTestContext context,
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger state,
        ResourceHistoryState history)
    {
        var composed = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            path => context.FileSystem.ReadFileAsync(path),
            state,
            history,
            CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        Assert.False(string.IsNullOrWhiteSpace(composed.CanonicalAuthorityJson));
        await context.WriteJsonAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            JsonNode.Parse(composed.CanonicalAuthorityJson!)!);
    }

    private static ResourceDefinitionCatalog ParseDefinitions(JsonNode? root)
    {
        var result = ResourceDefinitionCatalog.ParseCanonical(
            Assert.IsType<JsonObject>(root).ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(result.Catalog);
        Assert.Empty(result.Issues);
        return result.Catalog!;
    }

    private static ResourceStateLedger ParseState(
        JsonNode? root,
        ResourceDefinitionCatalog definitions)
    {
        var result = ResourceStateContract.ParseCanonical(
            Assert.IsType<JsonObject>(root).ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.NotNull(result.Ledger);
        Assert.Empty(result.Issues);
        return result.Ledger!;
    }

    private static ResourceHistoryState ParseHistory(
        JsonNode? root,
        ResourceDefinitionCatalog definitions)
    {
        var result = ResourceHistoryState.ParseCanonical(
            Assert.IsType<JsonObject>(root).ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.NotNull(result.History);
        Assert.Empty(result.Issues);
        return result.History!;
    }

    private static ResourcePendingResolutionState ParsePending(
        JsonNode? root,
        ResourceDefinitionCatalog definitions)
    {
        var result = ResourcePendingResolutionState.ParseCanonical(
            Assert.IsType<JsonObject>(root).ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        return result.State!;
    }

    private static int ReadRemainingUses(JsonNode? root)
    {
        var effects = Assert.IsType<JsonObject>(root);
        var effect = Assert.IsType<JsonObject>(
            Assert.Single(effects["activeEffects"]!.AsArray()));
        return effect["lifetime"]!["remainingUses"]!.GetValue<int>();
    }

    private static int ReadRemainingTurns(JsonNode? root)
    {
        var effects = Assert.IsType<JsonObject>(root);
        var effect = Assert.IsType<JsonObject>(
            Assert.Single(effects["activeEffects"]!.AsArray()));
        return effect["lifetime"]!["remainingTurns"]!.GetValue<int>();
    }

    private static void AssertNoErrors(IReadOnlyList<ValidationIssue> issues) =>
        Assert.True(
            issues.All(static issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(static issue =>
                $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}"));

    private sealed record SeededMortalEffect(
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State);
}
