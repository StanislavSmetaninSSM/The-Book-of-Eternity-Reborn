using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Keeps publication bound to one filesystem, one take and one exact receipt until explicit invalidation.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC4_PublicationAuthorityRejectsForeignAndReplayedTakes()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await PrepareSpiritualC4PublicationAsync(context);
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority authority;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out authority));
        var completedWounds = Assert.IsType<SpiritualLiveWoundCompletion>(authority.LiveWoundCompletion);
        Assert.Contains(WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(completedWounds.Insertions[0].Prepared),
            issue => issue.Code == "wound_plan_prepared_seal_mismatch");

        var foreign = new FileSystemManager(context.RootPath,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemManager>.Instance);
        await using (var lease = await foreign.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(foreign, lease, out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(foreign, lease, authority, out _));
        }

        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.True(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(
                context.FileSystem, lease, authority, out var receipt));
            Assert.True(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, authority, out _));
            var copiedReceipt = new ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationReceipt(
                authority, receipt.CacheOwner, receipt.Fence);
            Assert.False(AcceptedMechanicsPlanAuthority.IsTakenSpiritualPublicationCurrent(context.FileSystem, lease, copiedReceipt));
            Assert.False(AcceptedMechanicsPlanAuthority.CompleteSpiritualPublication(context.FileSystem, lease, copiedReceipt));
            Assert.False(AcceptedMechanicsPlanAuthority.FailSpiritualPublication(context.FileSystem, lease, copiedReceipt));
            Assert.True(AcceptedMechanicsPlanAuthority.IsTakenSpiritualPublicationCurrent(context.FileSystem, lease, receipt));
            AcceptedMechanicsPlanAuthority.InvalidateValidated(context.FileSystem, lease);
            Assert.False(AcceptedMechanicsPlanAuthority.IsTakenSpiritualPublicationCurrent(context.FileSystem, lease, receipt));
            Assert.False(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(context.FileSystem, lease, authority));
        }

        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        await using var finalLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, finalLease, out var replacement));
        Assert.NotSame(authority, replacement);
        Assert.False(AcceptedMechanicsPlanAuthority.TryTakeValidated(context.FileSystem, finalLease, replacement.Binding, out _));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, finalLease));
        Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, finalLease, replacement, out _));
    }

    /// <summary>
    /// Restores signed pre-turn images and absence when the common writer or its read-back fails.
    /// </summary>
    /// <param name="failReadBack">
    /// Whether failure happens after resource publication while reading its spiritual receipt.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualC4_PublicationFailureRestoresSignedOriginal(bool failReadBack)
    {
        var armed = false;
        var cuts = 0;
        CurrentCommittedPublicationWitness? witness = null;
        var injected = new IOException(failReadBack
            ? "C4 publication read-back failure." : "C4 publication write failure.");
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                if (armed) witness!.Observe(phase, index);
            },
            BeforeCanonicalMutationAsync = path =>
            {
                if (armed && !failReadBack && path == ResourceMaterializationContract.StatePath)
                {
                    cuts++;
                    armed = false;
                    throw injected;
                }
                return Task.CompletedTask;
            },
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && failReadBack && path == SpiritualWoundOpportunityReceiptState.StatePath &&
                    witness!.Has(ResourceMaterializationContract.StatePath) &&
                    witness.Has(SpiritualWoundOpportunityReceiptState.StatePath))
                {
                    witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
                    witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
                    cuts++;
                    armed = false;
                    throw injected;
                }
                return Task.CompletedTask;
            }
        };
        var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), _storageOutput.WriteLine);
        var originals = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[]
        {
            SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
            SpiritualWoundOpportunityReceiptState.StatePath, "output/narrative_response.json"
        }).Distinct(StringComparer.Ordinal))
            originals.Add(path, await context.FileSystem.ReadFileBytesAsync(path));
        await PrepareSpiritualC4PublicationAsync(context);
        var backups = await ReadSpiritualC4BackupsAsync(context);
        witness = new(context.FileSystem, ResourceMaterializationContract.StatePath,
            SpiritualWoundOpportunityReceiptState.StatePath);

        armed = true;
        var failure = await Record.ExceptionAsync(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, backups));
        armed = false;
        witness.AssertRestored("spiritual-original-committed-rollback",
            originals.ToDictionary(pair => context.FileSystem.ResolvePath(pair.Key), pair => pair.Value,
                StringComparer.Ordinal), failure, _storageOutput.WriteLine);
        Assert.Equal(1, cuts);
        if (failReadBack)
        {
            Assert.True(witness.Has(ResourceMaterializationContract.StatePath));
            Assert.True(witness.Has(SpiritualWoundOpportunityReceiptState.StatePath));
            Assert.Same(injected, failure);
        }
        else
        {
            Assert.False(witness.Has(ResourceMaterializationContract.StatePath));
            Assert.False(witness.Has(SpiritualWoundOpportunityReceiptState.StatePath));
            var writeFailure = Assert.IsType<CanonicalStateWriteException>(failure);
            Assert.Equal(ResourceMaterializationContract.StatePath, writeFailure.RelativePath);
            Assert.Same(injected, writeFailure.InnerException);
        }
        foreach (var pair in originals)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Rejects drift after registration without running a normalizer or restoring the user's edited draft.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC4_PreTakeDriftDoesNotWrite()
    {
        var armed = false;
        var writes = new List<string>();
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (armed)
                    writes.Add(path);
                return Task.CompletedTask;
            }
        };
        await using var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await PrepareSpiritualC4PublicationAsync(context);
        var backups = await ReadSpiritualC4BackupsAsync(context);
        var before = (await context.FileSystem.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath))!;
        byte[] changed = [.. before, (byte)' '];
        await context.FileSystem.WriteFileAtomicBytesAsync(AcceptedMechanicsPlan.WoundCommandPath, changed);

        armed = true;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, backups));

        Assert.Contains("spiritual_c4_publication_input_changed", error.Message);
        Assert.Empty(writes);
        Assert.Equal(changed, await context.FileSystem.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
    }

    /// <summary>
    /// Creates a genuine signed completed materialization and registers it through ordinary raw validation.
    /// </summary>
    /// <param name="context">
    /// Fresh signed original fixture with no private continuation yet.
    /// </param>
    /// <param name="materialize">
    /// <see langword="true"/> authors a wound and its physical narration; <see langword="false"/> commits an explicit decline.
    /// </param>
    /// <returns>
    /// A task completing after genuine C2 completion is registered for common publication.
    /// </returns>
    private static async Task PrepareSpiritualC4PublicationAsync(ResourceMaterializationTestContext context, bool materialize = true)
    {
        await CommitInitialC2PairAsync(context);
        if (materialize)
            await WriteSpiritualC4SelectedSceneAsync(context);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            using var offered = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            var submitted = await offered.SubmitDecisionAsync(lease,
                materialize ? OriginalSpiritualWoundDecision(offered.Offer!.OpportunityRef) :
                    JsonSerializer.SerializeToElement(new { opportunityRef = offered.Offer!.OpportunityRef, decision = "none" }),
                materialize ? "Чужое давление надломило волю хранителя." : null);
            AssertNoConflictFrameErrors(submitted.Issues);
            Assert.Equal("completed_unpublished", submitted.Disposition);
            using var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(submitted.Session);
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
    }

    /// <summary>
    /// Reads the ordinary signed snapshot mapping used by the full accepted-turn transaction.
    /// </summary>
    /// <param name="context">
    /// Fixture retaining the current pending-turn snapshot manifest.
    /// </param>
    /// <returns>
    /// The manifest's canonical-path to backup-path mapping.
    /// </returns>
    private static async Task<IReadOnlyDictionary<string, string>> ReadSpiritualC4BackupsAsync(
        ResourceMaterializationTestContext context)
    {
        var manifest = JsonNode.Parse((await context.FileSystem.ReadFileAsync(
            "game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
        return manifest["files"]!.AsObject().ToDictionary(static pair => pair.Key,
            static pair => pair.Value!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Rejects physical edits that were never committed to the completed spiritual continuation.
    /// </summary>
    /// <param name="path">
    /// Previously committed input whose JSON semantics remain unchanged while its exact bytes drift.
    /// </param>
    [Theory]
    [InlineData(AfterlifeSpiritualConflictState.StatePath)]
    [InlineData(AcceptedMechanicsPlan.WoundCommandPath)]
    public async Task OriginalSpiritualC4_RejectsUncommittedPublicationInput(string path)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        byte[] checkpoint;
        byte[] pending;
        var resources = await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            using var offered = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            var submitted = await offered.SubmitDecisionAsync(lease,
                JsonSerializer.SerializeToElement(new
                {
                    opportunityRef = offered.Offer!.OpportunityRef, decision = "none"
                }), null);
            AssertNoConflictFrameErrors(submitted.Issues);
            Assert.Equal("completed_unpublished", submitted.Disposition);
            using var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(submitted.Session);
            checkpoint = (await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath))!;
            pending = (await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath))!;
            var original = (await context.FileSystem.ReadFileBytesAsync(lease, path))!;
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, path, [.. original, (byte)' ']);
        }

        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "spiritual_c4_publication_input_changed" && issue.FilePath == path);
        Assert.Equal(resources, await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath));
        Assert.Equal(checkpoint, await context.FileSystem.ReadFileBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(pending, await context.FileSystem.ReadFileBytesAsync(SpiritualWoundDecisionPendingState.StatePath));
        await using var finalLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, finalLease));
    }

    /// <summary>
    /// Publishes the completed spiritual decision through the ordinary accepted-turn transaction without replanning.
    /// </summary>
    /// <param name="materialize">
    /// Whether the source creates a live guardian wound instead of an explicit decline.
    /// </param>
    /// <param name="terminal">
    /// Whether the original exchange also closes the conflict and retires its temporary resource owner.
    /// </param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task OriginalSpiritualC4_CompletedDecisionUsesCommonPublication(
        bool materialize, bool terminal) => RunSpiritualC4CommonPublicationAsync(materialize, terminal, chronicleDraft: false);

    /// <summary>
    /// Preserves a legitimate original GM chronicle update outside the selected mechanics input roots.
    /// </summary>
    [Fact]
    public Task OriginalSpiritualC4_RetainsOriginalChronicleDraft() =>
        RunSpiritualC4CommonPublicationAsync(materialize: false, terminal: false, chronicleDraft: true);

    /// <summary>
    /// Exercises genuine completed publication with exact retained resources, locations, wounds and effects.
    /// </summary>
    /// <param name="materialize">
    /// Creates a wound when true; otherwise explicitly declines the source.
    /// </param>
    /// <param name="terminal">
    /// Closes the original conflict when true; otherwise retains its active combat condition.
    /// </param>
    /// <param name="chronicleDraft">
    /// Includes a signed prior chronicle and an original GM update that unrelated normalization must preserve.
    /// </param>
    private async Task RunSpiritualC4CommonPublicationAsync(bool materialize, bool terminal, bool chronicleDraft)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await SeedOriginalPrefixActionPointEffectAsync(context, gain: true, bounded: false);
        if (!terminal)
            await SeedSpiritualC4ConditionAsync(context);
        const string chroniclePath = "game_state/meta/character_chronicle.json";
        if (chronicleDraft)
            await context.WriteExactJsonAsync(chroniclePath, new JsonObject { ["entries"] = new JsonArray("До духовного конфликта.") }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8]);
        if (chronicleDraft)
            await context.WriteExactJsonAsync(chroniclePath, new JsonObject
            {
                ["characterChronicleUpdates"] = new JsonArray("Хранитель выдержал духовный конфликт.")
            }.ToJsonString());
        await CommitInitialC2PairAsync(context, terminal ? ResolveTerminalFixture : null);

        const string woundScene = "Чужое давление надломило волю хранителя.";
        if (materialize)
            await WriteSpiritualC4SelectedSceneAsync(context);
        AcceptedMechanicsPlan expected;
        byte[] checkpointBytes;
        byte[] pendingBytes;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            Assert.Equal("offer", opened.Disposition);
            using var offered = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            var submitted = await offered.SubmitDecisionAsync(lease,
                materialize ? OriginalSpiritualWoundDecision(offered.Offer!.OpportunityRef) :
                    JsonSerializer.SerializeToElement(new
                    {
                        opportunityRef = offered.Offer!.OpportunityRef, decision = "none"
                    }), materialize ? woundScene : null);
            AssertNoConflictFrameErrors(submitted.Issues);
            Assert.Equal("completed_unpublished", submitted.Disposition);
            using var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(submitted.Session);
            var reduced = await completed.ReduceCompletedDecisionsAsync(lease);
            AssertNoConflictFrameErrors(reduced.Issues);
            var ordinary = reduced.Reduction!;
            var composed = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
                AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                    ordinary, ordinary.Resources));
            AssertNoConflictFrameErrors(composed.Issues);
            expected = Assert.IsType<AcceptedMechanicsPlan>(composed.Plan);
            checkpointBytes = (await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath))!;
            pendingBytes = (await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath))!;
        }

        var rawIssues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoConflictFrameErrors(rawIssues);
        var retained = await PeekPlanAsync(context);
        Assert.Equal(expected.PreparedPlanFingerprint, retained.PreparedPlanFingerprint);
        Assert.True(JsonNode.DeepEquals(expected.StateAfterImage, retained.StateAfterImage));
        Assert.True(JsonNode.DeepEquals(expected.HistoryAfterImage, retained.HistoryAfterImage));
        Assert.True(JsonNode.DeepEquals(expected.EffectIdentityAfterImage, retained.EffectIdentityAfterImage));
        Assert.Equal(checkpointBytes, await context.FileSystem.ReadFileBytesAsync(
            SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(pendingBytes, await context.FileSystem.ReadFileBytesAsync(
            SpiritualWoundDecisionPendingState.StatePath));

        JsonObject acceptedLocations;
        JsonObject acceptedLocationIdentities;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var publication));
            var locations = Assert.IsType<MortalLocationAcceptedTurnPlan>(publication.MortalLocationPlan);
            acceptedLocations = locations.FinalWorldMap.DeepClone().AsObject();
            acceptedLocationIdentities = locations.FinalIdentityIndex.DeepClone().AsObject();
        }

        var manifest = JsonNode.Parse((await context.FileSystem.ReadFileAsync(
            "game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
        var backups = manifest["files"]!.AsObject().ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value!.GetValue<string>(),
            StringComparer.OrdinalIgnoreCase);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);
        AssertNoConflictFrameErrors(published.Issues);
        Assert.Same(retained, published.MechanicsPlan);
        var publishedOutput = Assert.IsType<SpiritualWoundPublishedOutput>(published.SpiritualWoundOutput);
        Assert.Equal(materialize ? 1 : 0, publishedOutput.Notifications.Count);
        await publishedOutput.RequireCurrentAsync(context.FileSystem);
        Assert.True(JsonNode.DeepEquals(acceptedLocations,
            await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath)));
        Assert.True(JsonNode.DeepEquals(acceptedLocationIdentities,
            await context.ReadJsonAsync(MortalLocationIdentityState.StatePath)));
        if (chronicleDraft)
        {
            var chronicle = (await context.ReadJsonAsync(chroniclePath))!;
            Assert.Null(chronicle["characterChronicleUpdates"]);
            Assert.Equal(new[] { "До духовного конфликта.", "Хранитель выдержал духовный конфликт." },
                chronicle["entries"]!.AsArray().Select(entry => entry!.GetValue<string>()));
        }
        Assert.Equal(expected.PreparedPlanFingerprint, retained.PreparedPlanFingerprint);
        foreach (var pair in expected.OwnerCompanionAfterImages)
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        foreach (var pair in expected.EffectCarrierAfterImages)
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        if (!terminal)
        {
            var conflict = await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath);
            Assert.Equal("c4_retained_guard_ward", Assert.Single(conflict!["activeConflict"]!["combatConditions"]!
                .AsArray())!["conditionId"]!.GetValue<string>());
        }
        Assert.True(JsonNode.DeepEquals(expected.StateAfterImage,
            await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(expected.HistoryAfterImage,
            await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        Assert.True(JsonNode.DeepEquals(expected.EffectIdentityAfterImage,
            await context.ReadJsonAsync(EffectAcceptedTurnPlan.IdentityIndexPath)));
        Assert.True(JsonNode.DeepEquals(
            expected.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath],
            await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath)));
        if (materialize)
        {
            Assert.True(JsonNode.DeepEquals(expected.WoundIdentityAfterImage,
                await context.ReadJsonAsync(WoundIdentityState.StatePath)));
            Assert.True(JsonNode.DeepEquals(expected.WoundHistoryAfterImage,
                await context.ReadJsonAsync(WoundHistoryState.HistoryPath)));
            Assert.True(JsonNode.DeepEquals(expected.WoundCarrierAfterImages[AfterlifeEntityProfileState.StatePath],
                await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath)));
        }
        foreach (var path in new[]
        {
            SpiritualWoundCaptureCheckpointState.StatePath,
            SpiritualWoundDecisionPendingState.StatePath,
            AcceptedMechanicsPlan.WoundCommandPath
        })
        {
            Assert.Contains(path, retained.ConsumedPaths);
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
        }
    }

    /// <summary>
    /// Seeds one canonical retained condition with its real art definition and effect identity before signing.
    /// </summary>
    /// <param name="context">
    /// Original spiritual fixture whose guardian participates in the pressure exchange.
    /// </param>
    private static async Task SeedSpiritualC4ConditionAsync(ResourceMaterializationTestContext context)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("afterlife_combat_condition");
        definition["allowedRealms"] = new JsonArray("chaos_sea");
        definition["allowedTargetKinds"] = new JsonArray("spiritual_conflict_side");
        definition["components"]![0]!["payload"] = new JsonObject
        {
            ["conditionKind"] = "ward", ["targetSide"] = "opposition", ["actorId"] = "guardian_frame",
            ["operations"] = new JsonArray("guard"), ["axes"] = new JsonArray("rollMode"),
            ["counterplay"] = new JsonArray("Обойти защиту давлением."), ["payoff"] = "grant_advantage"
        };
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses", ["initialUses"] = 2,
            ["consumingEventTypes"] = new JsonArray("afterlife_exchange_end")
        };
        definition["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "condition_exchange_consumed", ["eventType"] = "afterlife_exchange_end",
            ["priority"] = 100, ["componentIds"] = new JsonArray("component_001"),
            ["consumeUses"] = true, ["resolutionMode"] = "deterministic"
        });
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        var guardian = profiles["profiles"]!.AsArray().OfType<JsonObject>()
            .Single(profile => profile["actorId"]!.GetValue<string>() == "guardian_frame");
        guardian["specialArts"]!.AsArray().Add(new JsonObject
        {
            ["artId"] = "art_c4_condition", ["displayName"] = "Оберег хранителя",
            ["effectDescription"] = "Защищает при защитном действии.", ["owner"] = "guardian_frame",
            ["baseOperation"] = "guard", ["activeEffectDefinitions"] = new JsonArray(definition.DeepClone())
        });
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "afterlife_combat_condition");
        effect["effectId"] = "c4_retained_guard_ward";
        effect["realm"] = "chaos_sea";
        effect["target"] = new JsonObject
        {
            ["kind"] = "spiritual_conflict_side", ["targetId"] = "conflict_resource_cost:opposition"
        };
        effect["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art", ["sourceId"] = "art_c4_condition",
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses", ["remainingUses"] = 2,
            ["consumingTriggerIds"] = new JsonArray("condition_exchange_consumed")
        };
        effect["chronology"]!["lastTransitionId"] = "transition_c4_retained_guard_ward";
        effect["chronology"]!["createdEventRef"] = "turn_42:c4_guard_ward";
        Assert.True(AfterlifeSpiritualConflictState.TryProjectCombatCondition(effect, out var condition, out var reason), reason);
        var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
        conflict["activeConflict"]!["combatConditions"] = new JsonArray(condition);
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
        var identity = (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.AsObject();
        var entry = EffectMaterializationTestFixture.CreateIdentityIndex(effect)["entries"]![0]!.DeepClone();
        entry["transitions"]![0]!["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
        entry["transitions"]![0]!["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
        identity["entries"]!.AsArray().Add(entry);
        await context.WriteExactJsonAsync(EffectIdentityState.StatePath, identity.ToJsonString());
    }
}
