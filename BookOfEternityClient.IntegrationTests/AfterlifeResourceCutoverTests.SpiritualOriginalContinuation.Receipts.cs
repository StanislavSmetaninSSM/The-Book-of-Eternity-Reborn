using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task OriginalSpiritualContinuation_RealReceiptRetryCommitsSourceOnlyAfterAcceptance(int amount)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await SeedOriginalContinuationBoundedEffectAsync(context);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await InvokeOriginalCaptureAsync(context.Validator, "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(captured));
        var capture = OriginalCaptureProperty(captured, "Capture")!;
        var begun = await InvokeOriginalCaptureAsync(capture, "BeginResourceExecutionAsync", lease);
        AssertNoConflictFrameErrors(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(begun));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var sourceRef = Assert.Single(source.Sources);
        var first = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "AdvanceNextResourceExchangeAsync", lease));
        AssertNoConflictFrameErrors(first.Issues);
        var wait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualResourceExchange>(first.Step!.PendingResource);
        Assert.Single(wait.RequiredOutputs);
        var revision = source.ContinuationRevision;
        var binding = source.BuildInputBinding().ToJsonString();
        var known = OriginalContinuationApplied(resources);
        Assert.Single(known);
        Assert.Equal("soul_integrity", known[0].Coordinate.ResourceKey);
        Assert.Equal(9m, known[0].AfterState!.Current);
        var packet = Assert.IsType<JsonObject>(InvokeOriginalCapture(capture, "ReadPendingResourceRequest", lease, wait));
        var packetJson = packet.ToJsonString();
        var request = Assert.Single(packet["requests"]!.AsArray())!;
        var receipts = new JsonArray(new JsonObject
        {
            ["requestId"] = request["requestId"]!.GetValue<string>(),
            ["resultKind"] = "resource_delta", ["amount"] = 99,
            ["reason"] = "Rejected out-of-bound candidate"
        });
        var rejected = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "ResumePendingResourceAsync", lease, wait, receipts));
        Assert.Null(rejected.Step);
        Assert.NotEmpty(rejected.Issues);
        Assert.Equal(revision, source.ContinuationRevision);
        Assert.Equal(binding, source.BuildInputBinding().ToJsonString());
        Assert.Same(sourceRef, Assert.Single(source.Sources));
        Assert.Equal(known, OriginalContinuationApplied(resources));
        Assert.Equal(packetJson, Assert.IsType<JsonObject>(InvokeOriginalCapture(capture,
            "ReadPendingResourceRequest", lease, wait)).ToJsonString());
        receipts[0]!["amount"] = amount;
        receipts[0]!["reason"] = "Accepted exact owned request";
        var accepted = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "ResumePendingResourceAsync", lease, wait, receipts));
        AssertNoConflictFrameErrors(accepted.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(accepted.Step!.Interval);
        Assert.True(resources.Owns(interval));
        var closedEvidence = Assert.Single(Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(capture)
            .ReadClosedExchangeEvidence(lease));
        Assert.Equal(interval.ExchangeId, closedEvidence.ExchangeId);
        Assert.Equal(new[] { 0, 1 }, closedEvidence.DiceClaims.Select(claim => claim.SourceIndex));
        Assert.Equal(revision + 1, source.ContinuationRevision);
        Assert.Same(sourceRef, Assert.Single(source.Sources));
        var all = OriginalContinuationApplied(resources);
        Assert.Contains(known[0], all);
        Assert.Equal(amount == 0 ? 3 : 4, all.Length);
        Assert.Single(interval.EffectAfter.AcceptedActivations);
        Assert.Equal(amount == 0 ? 0 : 1, all.Count(value => value.OriginKind == "bounded_receipt"));
        var after = source.ContinuationRevision;
        var stale = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "ResumePendingResourceAsync", lease, wait, receipts));
        Assert.Null(stale.Step);
        Assert.NotEmpty(stale.Issues);
        Assert.Equal(after, source.ContinuationRevision);
        Assert.Equal(all, OriginalContinuationApplied(resources));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    // A canonical fixture, not a fabricated effect-plan/source capability. The
    // sealed custom resource supports the real periodic Restore operation, while
    // AP retains its narrower Spend/Gain contract. Capture reads all of this from
    // the signed original after the actual owner composer exports its authority.
    private static async Task SeedOriginalContinuationBoundedEffectAsync(ResourceMaterializationTestContext context)
    {
        var parsed = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(),
            allowMissingPristine: false);
        Assert.True(parsed.IsValid);
        var proposal = new JsonObject
        {
            ["resourceKey"] = "soul_integrity", ["definitionVersion"] = 1,
            ["displayName"] = "Целостность души", ["numericKind"] = "integer",
            ["unit"] = "point", ["quantum"] = 1,
            ["minimumPolicy"] = new JsonObject { ["kind"] = "definition_fixed", ["value"] = 0 },
            ["capacityPolicy"] = new JsonObject { ["kind"] = "instance_fixed" },
            ["initializationPolicy"] = new JsonObject { ["kind"] = "maximum" },
            ["allowedOwnerKinds"] = new JsonArray("afterlife_actor"),
            ["allowedOperations"] = new JsonArray("damage", "restore"),
            ["defaultFloorPolicy"] = "clamp_to_minimum", ["defaultCapPolicy"] = "clamp_to_maximum",
            ["visibility"] = "owner_visible"
        };
        using var document = JsonDocument.Parse(proposal.ToJsonString());
        var materialized = ResourceDefinitionCatalog.MaterializeProposal(document.RootElement,
            parsed.Catalog!, 1, "turn_1:resource_definition:1",
            static () => new ResourceDefinitionIdentity("resource_definition_continuation_integrity",
                "resource_definition_seal_continuation_integrity"));
        Assert.True(materialized.IsValid, string.Join(Environment.NewLine, materialized.Issues));
        var definitions = parsed.Catalog!.With(materialized.Definition!);
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var guardian = profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>()
            .Single(profile => profile["actorId"]!.GetValue<string>() == "guardian_frame");
        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_restore");
        definition["allowedRealms"] = new JsonArray("chaos_sea");
        definition["allowedTargetKinds"] = new JsonArray("guardian");
        definition["components"]![0]!["payload"]!["resource"] = "soul_integrity";
        definition["components"]![0]!["payload"]!["amount"] = 1;
        definition["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "on_resource_damaged", ["eventType"] = "resource_damaged",
            ["priority"] = 100, ["componentIds"] = new JsonArray("component_001"),
            ["consumeUses"] = false, ["resolutionMode"] = "bounded_receipt"
        });
        definition["lifetime"]!["advancePhase"] = "afterlife_exchange_end";
        guardian["specialArts"] = new JsonArray(new JsonObject
        {
            ["artId"] = "art_continuation_integrity", ["displayName"] = "Точный отклик",
            ["effectDescription"] = "Восстанавливает подтверждённый след повреждения.",
            ["owner"] = "guardian_frame", ["baseOperation"] = "guard",
            ["activeEffectDefinitions"] = new JsonArray(definition.DeepClone())
        });
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("guardian", "periodic_restore");
        effect["realm"] = "chaos_sea";
        effect["target"]!["targetId"] = "guardian_frame";
        effect["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art", ["sourceId"] = "art_continuation_integrity",
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        effect["lifetime"]!["advancePhase"] = "afterlife_exchange_end";
        guardian["activeEffects"] = new JsonArray(effect);
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/meta/soul_state.json"));
        var conflictRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var conflict = conflictRoot["activeConflict"]!.AsObject();
        var (apState, apHistory) = BuildActionPointState(definitions, profiles, conflict, soul, 6m, 6m);
        var owners = AfterlifeResourceOwnerComposer.Compose(new AfterlifeResourceOwnerCompositionInput(definitions,
            new AfterlifeResourceOwnerRoots(profiles, conflictRoot, soul),
            new AfterlifeResourceOwnerRoots(profiles, conflictRoot, soul)));
        Assert.True(owners.IsValid, string.Join(Environment.NewLine, owners.Issues));
        var owner = owners.Authority!.Entries.Values.Single(value => value.Key.ResourceOwnerId == "guardian_frame");
        var coordinate = new ResourceCoordinate(owner.Key.Realm, owner.Key.OwnerKind, owner.Key.ResourceOwnerId, "soul_integrity");
        var resolved = ResolvedResourceCapacity.Resolve(materialized.Definition!, coordinate,
            new InstanceFixedCapacityInput(FormulaOwner(owner.Key), 10m, owner.AuthorityFingerprint),
            "continuation_integrity_capacity", includeInitialization: true);
        Assert.True(resolved.IsValid, string.Join(Environment.NewLine, resolved.Issues));
        var initialization = InitializeTransition(owner.Key, resolved.Capacity!, 10m, sequence: 2)
            with { Coordinate = coordinate };
        var history = ResourceHistoryState.CreateValidated(apHistory.Transitions.Append(initialization), definitions);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var state = new ResourceStateLedger(apState.Entries.Append(new ResourceStateEntry(coordinate, 10m, 10m,
            initialization.AfterState!.CapacityBinding, ResourceLifecycleState.Active,
            new ResourceChronology(1, initialization.EventRef, initialization.TransitionId, initialization.EventRef, 1))));
        Assert.Empty(history.History!.ValidateStateAgreement(state));
        await context.WriteExactJsonAsync(ResourceMaterializationContract.DefinitionsPath, definitions.ToCanonicalJson());
        await context.WriteExactJsonAsync(ResourceMaterializationContract.StatePath, state.ToCanonicalJson());
        await context.WriteExactJsonAsync(ResourceMaterializationContract.HistoryPath, history.History.ToCanonicalJson());
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.WriteExactJsonAsync(EffectIdentityState.StatePath, EffectMaterializationTestFixture.CreateIdentityIndex(effect).ToJsonString());
        await WriteComposedAuthorityAsync(context, definitions, state, history.History);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8]);
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath, new JsonObject
        {
            ["resourceChanges"] = new JsonArray(new JsonObject
            {
                ["operation"] = "damage", ["target"] = new JsonObject { ["kind"] = "afterlife_actor", ["targetId"] = "guardian_frame" },
                ["resourceKey"] = "soul_integrity", ["amount"] = 1,
                ["source"] = new JsonObject { ["kind"] = "narrative_outcome" },
                ["eventRef"] = "turn_42:resource:1", ["reason"] = "Original accepted resource damage"
            })
        }.ToJsonString());
    }
}
