using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourcePendingResolutionIntegrationTests
{
    private static readonly string[] MechanicalPaths =
    {
        ResourceMaterializationContract.StatePath,
        ResourceMaterializationContract.HistoryPath,
        EffectMaterializationTestContext.PlayerEffectsPath,
        EffectMaterializationTestContext.IdentityIndexPath
    };

    [Fact]
    public async Task BoundedTurnEnd_BeforeReceiptPublishesOnlyPendingTechnicalState()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedBoundedTurnEndAsync(context);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var before = await context.CaptureBytesAsync(MechanicalPaths);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        AssertNoErrors(issues);
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);

        Assert.NotNull(plan);
        Assert.Equal(before, await context.CaptureBytesAsync(MechanicalPaths));
        var pendingRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ResourcePendingResolutionState.PendingPath));
        var definitions = ParseDefinitions(await context.ReadJsonAsync(
            ResourceMaterializationContract.DefinitionsPath));
        var parsed = ResourcePendingResolutionState.ParseCanonical(
            pendingRoot.ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var request = Assert.Single(parsed.State!.Requests);
        Assert.Equal("pending", request.State);
        Assert.Equal("health", request.Coordinate.ResourceKey);
        Assert.Equal(ResourceOperation.Damage, request.Operation);
        Assert.Equal(0m, request.MinimumAmount);
        Assert.Equal(3m, request.MaximumAmount);

        var safePacket = parsed.State.BuildSafeGmPacket().ToJsonString();
        Assert.DoesNotContain("player_current", safePacket, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, safePacket, StringComparison.Ordinal);
        Assert.DoesNotContain("sha256:", safePacket, StringComparison.Ordinal);
        Assert.DoesNotContain("resourceOwnerId", safePacket, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullTurnReceipt_ResolvesThroughCommonMutationAndAdvancesEffectExactlyOnce()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedBoundedTurnEndAsync(context);
        await PublishPendingAsync(context);
        var pending = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            ParseDefinitions(await context.ReadJsonAsync(
                ResourceMaterializationContract.DefinitionsPath)));
        var request = Assert.Single(pending.Requests);

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            ReceiptCommand(request.RequestId, amount: 2m));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        AssertNoErrors(issues);
        await using (var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var plan = await context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups);
            Assert.NotNull(plan);
        }

        var definitions = ParseDefinitions(await context.ReadJsonAsync(
            ResourceMaterializationContract.DefinitionsPath));
        var state = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!
                .ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.NotNull(state.Ledger);
        Assert.Empty(state.Issues);
        var health = Assert.Single(
            state.Ledger!.Entries,
            entry => entry.Coordinate.ResourceKey == "health");
        Assert.Equal(health.Maximum - 2m, health.Current);

        var history = ResourceHistoryState.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath))!
                .ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.NotNull(history.History);
        Assert.Empty(history.Issues);
        var transition = Assert.Single(
            history.History!.Transitions,
            candidate => candidate.Phase == ResourceMutationPhase.EffectTrigger);
        Assert.Equal("bounded_receipt", transition.OriginKind);
        Assert.Equal(request.RequestId, transition.ReceiptId);
        Assert.Equal(2m, transition.AppliedAmount);

        var effects = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        var effect = Assert.IsType<JsonObject>(
            Assert.Single(effects["activeEffects"]!.AsArray()));
        Assert.Equal(1, effect["lifetime"]!["remainingTurns"]!.GetValue<int>());

        var terminalState = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            definitions);
        Assert.Empty(terminalState.Requests);
        var terminal = Assert.Single(terminalState.TerminalReceipts);
        Assert.Equal(request.RequestId, terminal.RequestId);
        Assert.Equal("resource_delta", terminal.ResultKind);
        Assert.False(context.FileSystem.FileExists(EffectMaterializationTestContext.CommandPath));

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var replayBackups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            ReceiptCommand(request.RequestId, amount: 2m));
        var beforeReplay = await context.CaptureBytesAsync(
            MechanicalPaths.Concat(new[] { ResourcePendingResolutionState.PendingPath }).ToArray());

        var replayIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        AssertNoErrors(replayIssues);
        await using (var replayLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var replayPlan = await context.Normalizer.BindTo(replayLease)
                .NormalizeAcceptedMechanicsAsync(replayBackups);
            Assert.NotNull(replayPlan);
        }
        Assert.Equal(
            beforeReplay,
            await context.CaptureBytesAsync(
                MechanicalPaths.Concat(new[] { ResourcePendingResolutionState.PendingPath }).ToArray()));
        Assert.False(context.FileSystem.FileExists(EffectMaterializationTestContext.CommandPath));
    }

    [Fact]
    public async Task BoundedAfterComponent_DoesNotApplyDependentReceiptWhenPredecessorMadeNoChange()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedBoundedAfterComponentAsync(context);
        await PublishPendingAsync(context);
        var definitions = ParseDefinitions(await context.ReadJsonAsync(
            ResourceMaterializationContract.DefinitionsPath));
        var pending = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            definitions);
        Assert.Equal(2, pending.Requests.Count);
        var predecessor = Assert.Single(
            pending.Requests,
            request => request.MaximumAmount == 1m);
        var dependent = Assert.Single(
            pending.Requests,
            request => request.MaximumAmount == 2m);

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            new JsonObject
            {
                ["effectChanges"] = new JsonArray(),
                ["effectResolutionReceipts"] = new JsonArray(
                    new JsonObject
                    {
                        ["requestId"] = predecessor.RequestId,
                        ["resultKind"] = "narrated_no_state_change",
                        ["reason"] = "Предшествующий компонент не изменил ресурс."
                    },
                    new JsonObject
                    {
                        ["requestId"] = dependent.RequestId,
                        ["resultKind"] = "resource_delta",
                        ["amount"] = 2,
                        ["reason"] = "Зависимый компонент получил числовой результат."
                    }),
                ["effectEventReports"] = new JsonArray()
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoErrors(issues);
        await using (var writeLease = await context.FileSystem
                         .AcquireCanonicalWriteLeaseAsync())
        {
            var plan = await context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups);
            Assert.NotNull(plan);
            Assert.False(plan!.AwaitsPendingResolution);
        }

        var history = ResourceHistoryState.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath))!
                .ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.NotNull(history.History);
        Assert.Empty(history.Issues);
        Assert.DoesNotContain(history.History!.Transitions, transition =>
            transition.Phase == ResourceMutationPhase.EffectTrigger);
    }

    [Fact]
    public async Task BoundedAfterComponent_AppliesDependentReceiptAfterExactPredecessor()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedBoundedAfterComponentAsync(context);
        await PublishPendingAsync(context);
        var definitions = ParseDefinitions(await context.ReadJsonAsync(
            ResourceMaterializationContract.DefinitionsPath));
        var pending = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            definitions);
        var predecessor = Assert.Single(
            pending.Requests,
            request => request.MaximumAmount == 1m);
        var dependent = Assert.Single(
            pending.Requests,
            request => request.MaximumAmount == 2m);

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            new JsonObject
            {
                ["effectChanges"] = new JsonArray(),
                ["effectResolutionReceipts"] = new JsonArray(
                    new JsonObject
                    {
                        ["requestId"] = predecessor.RequestId,
                        ["resultKind"] = "resource_delta",
                        ["amount"] = 1,
                        ["reason"] = "Предшествующий компонент изменил ресурс."
                    },
                    new JsonObject
                    {
                        ["requestId"] = dependent.RequestId,
                        ["resultKind"] = "resource_delta",
                        ["amount"] = 2,
                        ["reason"] = "Зависимый компонент сработал после предшественника."
                    }),
                ["effectEventReports"] = new JsonArray()
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoErrors(issues);
        await using (var writeLease = await context.FileSystem
                         .AcquireCanonicalWriteLeaseAsync())
        {
            var plan = await context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups);
            Assert.NotNull(plan);
            Assert.False(plan!.AwaitsPendingResolution);
        }

        var history = ResourceHistoryState.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath))!
                .ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.NotNull(history.History);
        Assert.Empty(history.Issues);
        var transitions = history.History!.Transitions
            .Where(transition => transition.Phase == ResourceMutationPhase.EffectTrigger)
            .OrderBy(transition => transition.ExecutionSequence)
            .ToArray();
        Assert.Collection(
            transitions,
            transition => Assert.Equal(1m, transition.AppliedAmount),
            transition => Assert.Equal(2m, transition.AppliedAmount));
        Assert.True(
            transitions[0].ExecutionSequence < transitions[1].ExecutionSequence);
    }

    [Fact]
    public async Task InvalidReceipt_FailsClosedWithoutMechanicalOrPendingWrites()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await SeedBoundedTurnEndAsync(context);
        await PublishPendingAsync(context);
        var definitions = ParseDefinitions(await context.ReadJsonAsync(
            ResourceMaterializationContract.DefinitionsPath));
        var request = Assert.Single(ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            definitions).Requests);

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            ReceiptCommand(request.RequestId, amount: request.MaximumAmount + 1m));
        var protectedPaths = MechanicalPaths
            .Concat(new[]
            {
                ResourcePendingResolutionState.PendingPath,
                EffectMaterializationTestContext.CommandPath
            })
            .ToArray();
        var before = await context.CaptureBytesAsync(protectedPaths);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Code == "resource_pending_receipt_out_of_bounds");
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            context.Normalizer.BindTo(writeLease).NormalizeAcceptedMechanicsAsync(backups));
        Assert.Equal(before, await context.CaptureBytesAsync(protectedPaths));
    }

    [Fact]
    public async Task SameTurnAppliedBoundedEffect_RebindsAcceptedApplicationAcrossReceiptResubmission()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedMortalPlayerResourcesAsync(turn: 42);
        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_restore");
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = 1,
            ["consumingEventTypes"] = new JsonArray("resource_damaged")
        };
        definition["triggers"] = new JsonArray(BoundedResourceDamagedTrigger());
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            });
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
                    ["pendingSurvivalEffects"] = new JsonArray(new JsonObject
                    {
                        ["sourceCardId"] = "card_same_turn_bounded_authority",
                        ["status"] = ShiningBlessingEffectState
                            .SurvivalStatusPendingFirstRuinousFailure,
                        ["recovery"] = 20,
                        ["downgrade"] = 1
                    })
                }
            });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = "event_same_turn_bounded_hit",
                    ["summary"] = "A bounded-resource trigger follows an accepted hit.",
                    ["isActive"] = true,
                    ["visibility"] = "player_known",
                    ["severity"] = "ruinous"
                })
            });
        var resourceCommandRoot = new JsonObject
        {
            ["resourceDefinitionCreations"] = new JsonArray(),
            ["resourceCapacityChanges"] = new JsonArray(),
            ["resourceChanges"] = new JsonArray(new JsonObject
            {
                ["operation"] = "damage",
                ["target"] = new JsonObject
                {
                    ["kind"] = "player",
                    ["targetId"] = "player_current"
                },
                ["resourceKey"] = "health",
                ["amount"] = 1,
                ["source"] = new JsonObject
                {
                    ["kind"] = "narrative_outcome",
                    ["sourceId"] = "event_same_turn_bounded_hit"
                },
                ["eventRef"] = "turn_43:resource:1",
                ["reason"] = "An accepted hit produces the exact resource event."
            })
        };
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            resourceCommandRoot);
        var apply = EffectMaterializationTestFixture.CreateApplyCommand();
        apply["eventRef"]!["authorityId"] = "turn_43";
        var commandRoot = EffectMaterializationTestFixture.CreateCommandRoot(apply);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            commandRoot);

        var initialIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoErrors(initialIssues);
        await using (var initialLease = await context.FileSystem
                         .AcquireCanonicalWriteLeaseAsync())
        {
            var pendingPlan = await context.Normalizer.BindTo(initialLease)
                .NormalizeAcceptedMechanicsAsync(backups);
            Assert.NotNull(pendingPlan);
            Assert.True(pendingPlan!.AwaitsPendingResolution);
        }

        var definitions = ParseDefinitions(await context.ReadJsonAsync(
            ResourceMaterializationContract.DefinitionsPath));
        var pending = ParsePending(
            await context.ReadJsonAsync(ResourcePendingResolutionState.PendingPath),
            definitions);
        var request = Assert.Single(pending.Requests);
        Assert.Equal("accepted_application", request.EffectAuthority.BindingKind);
        Assert.Equal(
            "turn_43:accepted_effect",
            request.EffectAuthority.AuthorityId);
        commandRoot["effectResolutionReceipts"] = new JsonArray(new JsonObject
        {
            ["requestId"] = request.RequestId,
            ["resultKind"] = "resource_delta",
            ["amount"] = 2,
            ["reason"] = "Рассказчик подтвердил связанный итог."
        });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            commandRoot);

        var receiptIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoErrors(receiptIssues);
        await using (var receiptLease = await context.FileSystem
                         .AcquireCanonicalWriteLeaseAsync())
        {
            var resolvedPlan = await context.Normalizer.BindTo(receiptLease)
                .NormalizeAcceptedMechanicsAsync(backups);
            Assert.NotNull(resolvedPlan);
            Assert.False(resolvedPlan!.AwaitsPendingResolution);
        }

        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Empty(player["activeEffects"]!.AsArray());
        var identity = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(identity["entries"]!.AsArray()));
        Assert.False(string.IsNullOrWhiteSpace(entry["effectId"]!.GetValue<string>()));
        Assert.Equal("expired", entry["state"]!.GetValue<string>());
        var transitions = entry["transitions"]!.AsArray();
        var created = Assert.IsType<JsonObject>(transitions[0]);
        Assert.Equal(
            request.EffectAuthority.AuthorityId,
            created["eventRef"]!.GetValue<string>());

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var replayBackups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            commandRoot);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            resourceCommandRoot);
        var protectedPaths = MechanicalPaths
            .Concat(new[] { ResourcePendingResolutionState.PendingPath })
            .ToArray();
        var beforeReplay = await context.CaptureBytesAsync(protectedPaths);

        var replayIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        AssertNoErrors(replayIssues);
        await using (var replayLease = await context.FileSystem
                         .AcquireCanonicalWriteLeaseAsync())
        {
            var replayPlan = await context.Normalizer.BindTo(replayLease)
                .NormalizeAcceptedMechanicsAsync(replayBackups);
            Assert.NotNull(replayPlan);
        }
        Assert.Equal(
            beforeReplay,
            await context.CaptureBytesAsync(protectedPaths));
        Assert.False(context.FileSystem.FileExists(
            EffectMaterializationTestContext.CommandPath));
        Assert.False(context.FileSystem.FileExists(
            ResourceMaterializationContract.CommandPath));
    }

    private static async Task SeedBoundedTurnEndAsync(
        EffectMaterializationTestContext context)
    {
        await context.SeedMortalPlayerResourcesAsync(turn: 42);
        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
        definition["triggers"]![0]!["resolutionMode"] = "bounded_receipt";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            ownerKind: "player",
            profile: "periodic_damage");
        effect["triggers"]![0]!["resolutionMode"] = "bounded_receipt";
        effect["lifetime"]!["remainingTurns"] = 2;
        await context.SeedPlayerWoundSourceAsync(definition);
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
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
    }

    private static async Task SeedBoundedAfterComponentAsync(
        EffectMaterializationTestContext context)
    {
        await context.SeedMortalPlayerResourcesAsync(turn: 42);
        var definition = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        var reaction = definition["components"]![0]!.AsObject();
        reaction["componentId"] = "reaction_dispatch";
        reaction["payload"] = new JsonObject
        {
            ["eventType"] = "owner_turn_end",
            ["resultKind"] = "bounded_receipt",
            ["componentId"] = "reaction_dependent",
            ["dependency"] = "after_component",
            ["afterComponentId"] = "reaction_predecessor",
            ["maxExpansion"] = 1
        };
        var predecessor = EffectMaterializationTestFixture
            .CreateDefinition("periodic_damage")["components"]![0]!
            .DeepClone().AsObject();
        predecessor["componentId"] = "reaction_predecessor";
        predecessor["priority"] = 100;
        predecessor["payload"]!["amount"] = 1;
        var dependent = predecessor.DeepClone().AsObject();
        dependent["componentId"] = "reaction_dependent";
        dependent["priority"] = -100;
        dependent["payload"]!["amount"] = 2;
        definition["components"] = new JsonArray(
            reaction.DeepClone(),
            predecessor.DeepClone(),
            dependent.DeepClone());
        definition["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "on_owner_turn_end",
            ["eventType"] = "owner_turn_end",
            ["priority"] = 100,
            ["componentIds"] = new JsonArray(
                "reaction_predecessor",
                "reaction_dispatch"),
            ["consumeUses"] = false,
            ["resolutionMode"] = "bounded_receipt"
        });
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        effect["lifetime"]!["remainingTurns"] = 2;
        await context.SeedPlayerWoundSourceAsync(definition);
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
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
    }

    private static async Task PublishPendingAsync(
        EffectMaterializationTestContext context)
    {
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoErrors(issues);
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);
        Assert.NotNull(plan);
    }

    private static JsonObject ReceiptCommand(string requestId, decimal amount) =>
        new()
        {
            ["effectChanges"] = new JsonArray(),
            ["effectResolutionReceipts"] = new JsonArray(new JsonObject
            {
                ["requestId"] = requestId,
                ["resultKind"] = "resource_delta",
                ["amount"] = amount,
                ["reason"] = "Рассказчик подтвердил итог в разрешённом диапазоне."
            }),
            ["effectEventReports"] = new JsonArray()
        };

    private static JsonObject BoundedResourceDamagedTrigger() => new()
    {
        ["triggerId"] = "on_resource_damaged",
        ["eventType"] = "resource_damaged",
        ["priority"] = 100,
        ["componentIds"] = new JsonArray("component_001"),
        ["consumeUses"] = true,
        ["resolutionMode"] = "bounded_receipt"
    };

    private static ResourceDefinitionCatalog ParseDefinitions(JsonNode? root)
    {
        var result = ResourceDefinitionCatalog.ParseCanonical(
            Assert.IsType<JsonObject>(root).ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(result.Catalog);
        Assert.Empty(result.Issues);
        return result.Catalog!;
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

    private static void AssertNoErrors(IReadOnlyList<ValidationIssue> issues) =>
        Assert.True(
            issues.All(static issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(static issue =>
                $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}"));
}
