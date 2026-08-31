using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedMechanicsPlanCacheTests
{
    [Fact]
    public async Task WoundDirectCommonAuthority_SealsRetriesAndPublishesOnlyThroughNormalizer()
    {
        var seed = CreateWoundCommonInput("direct_common");
        using var canonical = DirectWoundCommonFixture.Create(
            seed.Binding,
            1260L);
        var stages = CreateAnchoredMortalCreateStages(
            "direct_common",
            canonical.SnapshotToken);
        var beforeComposition = canonical.CaptureTreeBytes();

        var first = AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(
            canonical.FileSystem,
            canonical.Lease,
            stages.Bundle);

        Assert.True(
            first.Success,
            string.Join(Environment.NewLine, first.Issues.Select(
                static issue => $"{issue.Code}@{issue.FilePath}: {issue.Message}")));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(first.Plan);
        Assert.NotNull(plan.DirectWoundPublicationAuthority);
        Assert.All(
            EffectAcceptedTurnInputComposer.SourceAuthorityPaths,
            path => Assert.Contains(path, plan.BeforeImages.Keys));
        canonical.AssertTreeBytesEqual(beforeComposition);

        var retry = AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(
            canonical.FileSystem,
            canonical.Lease,
            stages.Bundle);

        Assert.True(retry.Success);
        Assert.Same(plan, retry.Plan);
        canonical.AssertTreeBytesEqual(beforeComposition);

        var published = await new CanonicalStateNormalizer(
                canonical.FileSystem,
                NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(canonical.Lease)
            .NormalizeAcceptedMechanicsAsync(backups: null);

        Assert.Same(plan, published);
        var woundRoot = JsonNode.Parse(File.ReadAllText(
            canonical.FileSystem.ResolvePath(
                WoundCarrierCatalog.PlayerPath)))!.AsObject();
        var catalog = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            woundRoot,
            null,
            null,
            null,
            null));
        Assert.Empty(catalog.Issues);
        Assert.True(catalog.TryResolveOne(
            Assert.Single(stages.Final.AllocatedWoundIds),
            out var occurrence));
        var transitionId = Assert.Single(stages.Final.AllocatedTransitionIds);
        Assert.Equal(
            new WoundRecoveryAnchor("creation", 1260L, transitionId),
            occurrence.Wound.Recovery.RecoveryAnchor);
        Assert.Equal(
            new WoundDeteriorationAnchor(
                "not_stabilized",
                1260L,
                transitionId),
            occurrence.Wound.Recovery.DeteriorationAnchor);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    public void WoundDirectCommonAuthority_RejectsInvalidCanonicalClock(
        string mutation)
    {
        var suffix = "direct_clock_" + mutation;
        var seed = CreateWoundCommonInput(suffix);
        using var canonical = DirectWoundCommonFixture.Create(
            seed.Binding,
            1260L);
        var stages = CreateAnchoredMortalCreateStages(
            suffix,
            canonical.SnapshotToken);
        canonical.MutateClock(mutation);
        var beforeComposition = canonical.CaptureTreeBytes();

        var result = AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(
            canonical.FileSystem,
            canonical.Lease,
            stages.Bundle);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code ==
                "mortal_wound_recovery_anchor_clock_invalid");
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            canonical.FileSystem,
            canonical.Lease,
            out _,
            out _));
        canonical.AssertTreeBytesEqual(beforeComposition);
    }

    [Theory]
    [InlineData("turn", "accepted_mechanics_wound_turn_binding_mismatch")]
    [InlineData("realm", "accepted_mechanics_wound_turn_binding_mismatch")]
    [InlineData("snapshot", "accepted_mechanics_wound_snapshot_binding_mismatch")]
    [InlineData("wound", "accepted_mechanics_wound_live_before_image_mismatch")]
    public void WoundDirectCommonAuthority_RejectsForeignLiveAuthority(
        string mutation,
        string expectedCode)
    {
        var suffix = "direct_foreign_" + mutation;
        var seed = CreateWoundCommonInput(suffix);
        using var canonical = DirectWoundCommonFixture.Create(
            seed.Binding,
            1260L,
            currentRealmOverride: string.Equals(
                mutation,
                "realm",
                StringComparison.Ordinal)
                ? "Chaos Sea"
                : null);
        var stages = CreateAnchoredMortalCreateStages(
            suffix,
            string.Equals(mutation, "snapshot", StringComparison.Ordinal)
                ? canonical.SnapshotToken + "_foreign"
                : canonical.SnapshotToken);
        if (string.Equals(mutation, "turn", StringComparison.Ordinal))
        {
            canonical.WriteJson(
                LiveTurnPreparationService.TurnRequestPath,
                new JsonObject
                {
                    ["sessionId"] = stages.Input.Binding.SessionId,
                    ["requestId"] = stages.Input.Binding.RequestId,
                    ["turnNumber"] = stages.Input.Binding.Turn + 1,
                    ["gameMode"] = "normal",
                    ["preGeneratedDices1d20"] = new JsonArray(17)
                });
        }
        else if (string.Equals(mutation, "wound", StringComparison.Ordinal))
        {
            canonical.WriteJson(
                WoundCarrierCatalog.PlayerPath,
                new JsonObject
                {
                    ["activeWounds"] = new JsonArray(new JsonObject
                    {
                        ["foreignWoundId"] = "wound_foreign_live_authority"
                    })
                });
        }
        var beforeComposition = canonical.CaptureTreeBytes();

        var result = AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(
            canonical.FileSystem,
            canonical.Lease,
            stages.Bundle);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.True(
            result.Issues.Any(issue => string.Equals(
                issue.Code,
                expectedCode,
                StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Issues.Select(
                static issue =>
                    $"{issue.Code}@{issue.FilePath}: {issue.Message} " +
                    $"(expected={issue.Expected}; actual={issue.Actual})")));
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            canonical.FileSystem,
            canonical.Lease,
            out _,
            out _));
        canonical.AssertTreeBytesEqual(beforeComposition);
    }

    [Fact]
    public async Task WoundDirectCommonAuthority_RejectsSourceRootDriftBeforePublication()
    {
        var seed = CreateWoundCommonInput("direct_source_drift");
        using var canonical = DirectWoundCommonFixture.Create(
            seed.Binding,
            1260L);
        var stages = CreateAnchoredMortalCreateStages(
            "direct_source_drift",
            canonical.SnapshotToken);
        var result = AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(
            canonical.FileSystem,
            canonical.Lease,
            stages.Bundle);
        Assert.True(result.Success);

        canonical.WriteJson(
            "game_state/player/skills_active.json",
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray()
            });

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new CanonicalStateNormalizer(
                    canonical.FileSystem,
                    NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(canonical.Lease)
                .NormalizeAcceptedMechanicsAsync(backups: null));

        Assert.Contains(
            "game_state/player/skills_active.json",
            exception.Message,
            StringComparison.Ordinal);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            canonical.FileSystem,
            canonical.Lease,
            out _,
            out _));
    }

    [Fact]
    public void WoundAnchorProjection_AtomicallyRebindsCarrierIdentityAndHistory()
    {
        var stages = CreateAnchoredMortalCreateStages("anchor_projection");
        using var clock = CanonicalClockFixture.Create(1260L);
        var anchorResult = MortalWoundCanonicalAnchorPlan.Create(
            clock.FileSystem,
            clock.Lease,
            stages.Bundle);
        Assert.True(
            anchorResult.Success,
            string.Join(Environment.NewLine, anchorResult.Issues.Select(
                static issue => $"{issue.Code}@{issue.FilePath}: {issue.Message}")));
        var anchorPlan = Assert.IsType<MortalWoundCanonicalAnchorPlan>(
            anchorResult.Plan);
        var allocation = Assert.Single(anchorPlan.Allocations);
        var createdWoundId = Assert.Single(stages.Final.AllocatedWoundIds);
        var createTransitionId = Assert.Single(stages.Final.AllocatedTransitionIds);
        Assert.Equal(createdWoundId, allocation.WoundId);
        Assert.Equal(createTransitionId, allocation.TransitionId);
        Assert.Equal(
            new WoundRecoveryAnchor(
                "creation",
                1260L,
                createTransitionId),
            allocation.RecoveryAnchor);
        Assert.Equal(
            new WoundDeteriorationAnchor(
                "not_stabilized",
                1260L,
                createTransitionId),
            allocation.DeteriorationAnchor);

        var originalBundleFingerprint = stages.Bundle.BundleFingerprint;
        var originalFinalFingerprint = stages.Bundle.WoundFinalPlanFingerprint;
        var originalCreated = Assert.IsType<WoundMaterializationEnvelope>(
            Assert.Single(Assert.Single(stages.Bundle.FinalPlan.CarrierContributions)
                .Mutations).AfterWound);
        Assert.Null(originalCreated.Recovery.RecoveryAnchor);
        Assert.Null(originalCreated.Recovery.DeteriorationAnchor);

        var acceptedInput = CreateWoundCommonAcceptedInput(
            stages.Bundle,
            woundAnchorPlan: anchorPlan);
        var cache = new AcceptedMechanicsPlanCache(
            AcceptedMechanicsPlanner.BuildAcceptedPlan);

        var result = cache.GetOrBuildValidated(acceptedInput);

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(
                static issue => $"{issue.Code}@{issue.FilePath}: {issue.Message}")));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
        Assert.Equal(anchorPlan.Fingerprint, plan.WoundAnchorPlanFingerprint);
        Assert.Equal(originalBundleFingerprint, plan.WoundStageBundle!.BundleFingerprint);
        Assert.Equal(
            originalFinalFingerprint,
            plan.WoundStageBundle.WoundFinalPlanFingerprint);

        var carriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            plan.WoundCarrierAfterImages[WoundCarrierCatalog.PlayerPath],
            null,
            null,
            null,
            null));
        Assert.Empty(carriers.Issues);
        Assert.True(carriers.TryResolveOne(createdWoundId, out var occurrence));
        Assert.Equal(allocation.RecoveryAnchor, occurrence.Wound.Recovery.RecoveryAnchor);
        Assert.Equal(
            allocation.DeteriorationAnchor,
            occurrence.Wound.Recovery.DeteriorationAnchor);

        var identity = WoundIdentityState.Parse(
            plan.WoundIdentityAfterImage!.ToJsonString(),
            WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            plan.WoundHistoryAfterImage!.ToJsonString(),
            WoundHistoryState.HistoryPath);
        Assert.True(identity.IsValid);
        Assert.True(history.IsValid);
        Assert.True(identity.State!.TryGetEntry(createdWoundId, out var identityEntry));
        Assert.Empty(WoundIdentityState.ValidateActiveAgreement(
            identityEntry!,
            occurrence.Wound,
            "anchorProjection"));
        Assert.Empty(history.State!.ValidateAgreement(identity.State, carriers));
        var historyRow = Assert.Single(
            history.State.Transitions,
            transition => transition.TransitionId == createTransitionId);
        Assert.Equal(
            WoundIdentityState.ComputeSemanticFingerprint(occurrence.Wound),
            historyRow.AfterFingerprint);

        Assert.Equal(originalBundleFingerprint, stages.Bundle.BundleFingerprint);
        Assert.Equal(originalFinalFingerprint, stages.Bundle.WoundFinalPlanFingerprint);
    }

    [Fact]
    public void WoundAnchorProjection_RejectsCrossSwappedBundleAuthority()
    {
        var first = CreateAnchoredMortalCreateStages("anchor_first");
        var second = CreateAnchoredMortalCreateStages("anchor_second");
        using var clock = CanonicalClockFixture.Create(90L);
        var foreign = Assert.IsType<MortalWoundCanonicalAnchorPlan>(
            MortalWoundCanonicalAnchorPlan.Create(
                clock.FileSystem,
                clock.Lease,
                second.Bundle).Plan);
        var cache = new AcceptedMechanicsPlanCache(
            AcceptedMechanicsPlanner.BuildAcceptedPlan);

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateWoundCommonAcceptedInput(
                first.Bundle,
                woundAnchorPlan: foreign));

        Assert.Contains("exact wound stage bundle", exception.Message);
        Assert.False(cache.HasValidated);
    }

    [Fact]
    public void WoundDirectCommonAuthority_RejectsMixedCreateAndWorsenPhase()
    {
        var seed = WoundEffectBatchPlannerTests.CreateInputForAcceptedCache(2);
        using var canonical = DirectWoundCommonFixture.Create(
            seed.Binding,
            1260L);
        var stages = CreateMixedMortalCreateAndWorsenStages(
            canonical.SnapshotToken);
        var beforeComposition = canonical.CaptureTreeBytes();

        var result = AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(
            canonical.FileSystem,
            canonical.Lease,
            stages.Bundle);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_anchor_phase_mixed");
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            canonical.FileSystem,
            canonical.Lease,
            out _,
            out _));
        canonical.AssertTreeBytesEqual(beforeComposition);
    }

    private static WoundCommonStageFixture CreateAnchoredMortalCreateStages(
        string suffix,
        string? snapshotToken = null)
    {
        var input = CreateWoundCommonInput(suffix);
        if (snapshotToken is not null)
        {
            input = input with
            {
                Binding = input.Binding with
                {
                    SnapshotToken = snapshotToken
                },
                Opportunities = input.Opportunities.Select(value =>
                    ResealWoundOpportunity(value with
                    {
                        SnapshotToken = snapshotToken
                    })).ToArray()
            };
        }
        var transition = Assert.Single(input.Transitions);
        var wound = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(
                transition.ProposedAfter))!.AsObject();
        wound["care"]!["state"] = "untreated";
        wound["care"]!["stabilizedAtTurn"] = null;
        wound["recovery"]!["blockers"] = new JsonArray("not_stabilized");
        wound["recovery"]!["deteriorationPolicy"] = new JsonObject
        {
            ["policyRef"] = "untreated_infection",
            ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = 30L,
            ["cadenceMinutes"] = 10L,
            ["result"] = new JsonObject { ["kind"] = "increase_severity" }
        };
        wound["recovery"]!.AsObject().Remove("recoveryAnchor");
        wound["recovery"]!.AsObject().Remove("deteriorationAnchor");
        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            "anchorProposal");
        Assert.True(parsed.IsValid);
        input = input with
        {
            Transitions = new[]
            {
                transition with { ProposedAfter = parsed.Wound! }
            }
        };
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        return CreateWoundCommonStages(input, prepared, suffix);
    }

    private static WoundCommonStageFixture
        CreateMixedMortalCreateAndWorsenStages(string? snapshotToken = null)
    {
        const string existingWoundId = "wound_existing_anchor_mixed";
        var input = WoundEffectBatchPlannerTests.CreateInputForAcceptedCache(2);
        if (snapshotToken is not null)
        {
            input = input with
            {
                Binding = input.Binding with
                {
                    SnapshotToken = snapshotToken
                },
                Opportunities = input.Opportunities.Select(value =>
                    ResealWoundOpportunity(value with
                    {
                        SnapshotToken = snapshotToken
                    })).ToArray()
            };
        }
        var owner = input.Transitions[0].ProposedAfter.Owner;
        var existingJson = WoundContractTestData.CreateActiveWound(
            existingWoundId,
            owner.Realm,
            owner.OwnerKind,
            owner.OwnerId,
            owner.CarrierPath,
            domain: "physical");
        var priorEventRef = $"turn_{input.Binding.Turn - 1}:existing:{existingWoundId}";
        var priorTransitionId = "wound_transition_existing_anchor_mixed";
        existingJson["origin"]!["eventRef"] = priorEventRef;
        existingJson["origin"]!["createdAtTurn"] = input.Binding.Turn - 1;
        existingJson["origin"]!["opportunityId"] =
            "opportunity_existing_anchor_mixed";
        existingJson["severity"]!["value"] = "I";
        existingJson["severity"]!["rank"] = 1;
        existingJson["severity"]!["maximumAtCreation"] = "II";
        existingJson["severity"]!["lastChangeEventRef"] = priorEventRef;
        existingJson["lastTransition"]!["transitionId"] = priorTransitionId;
        existingJson["lastTransition"]!["turn"] = input.Binding.Turn - 1;
        existingJson["consequences"] = new JsonObject
        {
            ["slotBudget"] = 2,
            ["slotsUsed"] = 0,
            ["entries"] = new JsonArray(),
            ["ownedEffectSources"] = new JsonObject
            {
                ["definitions"] = new JsonArray(),
                ["rootBindings"] = new JsonArray()
            }
        };
        var existingParse = WoundMaterializationContract.Parse(
            existingJson.ToJsonString(),
            "mixedExistingWound");
        Assert.True(
            existingParse.IsValid,
            string.Join(Environment.NewLine, existingParse.Issues.Select(
                static issue => $"{issue.Code}: {issue.Message}")));
        var existing = Assert.IsType<WoundMaterializationEnvelope>(
            existingParse.Wound);
        var existingFingerprint =
            WoundIdentityState.ComputeSemanticFingerprint(existing);

        var historyRow = WoundContractTestData.CreateTransition();
        historyRow["transitionId"] = priorTransitionId;
        historyRow["woundId"] = existingWoundId;
        historyRow["turn"] = input.Binding.Turn - 1;
        historyRow["eventRef"] = priorEventRef;
        historyRow["operationKey"] = "operation_existing_anchor_mixed";
        historyRow["beforeFingerprint"] =
            WoundHistoryState.ComputeNonexistentBeforeFingerprint(existingWoundId);
        historyRow["afterFingerprint"] = existingFingerprint;
        historyRow["sourceFingerprint"] =
            WoundAcceptedTurnFingerprintWriter.Compute(
                new[] { "existing-anchor-mixed" });
        historyRow["attemptId"] = null;

        var secondOpportunity = input.Opportunities[1];
        var worseningOpportunity = ResealWoundOpportunity(
            secondOpportunity with
            {
                WorseningTarget = new WoundOpportunityWorseningTargetAuthority(
                    existing,
                    "retrauma",
                    existingFingerprint)
            });
        var secondDraft = input.Transitions[1];
        var proposedJson = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(existing))!
            .AsObject();
        proposedJson["severity"]!["value"] = "II";
        proposedJson["severity"]!["rank"] = 2;
        proposedJson["severity"]!["lastChangeEventRef"] =
            secondOpportunity.EventRef;
        proposedJson["lastTransition"]!["transitionId"] =
            secondDraft.LocalTransitionRef;
        proposedJson["lastTransition"]!["ordinal"] = 2;
        proposedJson["lastTransition"]!["turn"] = input.Binding.Turn;
        proposedJson["lastTransition"]!["kind"] = "worsen";
        var proposedParse = WoundMaterializationContract.Parse(
            proposedJson.ToJsonString(),
            "mixedWorseningProposal");
        Assert.True(
            proposedParse.IsValid,
            string.Join(Environment.NewLine, proposedParse.Issues.Select(
                static issue => $"{issue.Code}: {issue.Message}")));

        var existingCanonical = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(existing))!
            .AsObject();
        input = input with
        {
            Opportunities = new[]
            {
                input.Opportunities[0],
                worseningOpportunity
            },
            Transitions = new[]
            {
                input.Transitions[0],
                new WoundAcceptedTransitionDraft(
                    "worsen",
                    secondDraft.OperationKey,
                    "draft_worsen_existing_anchor_mixed",
                    secondDraft.LocalTransitionRef,
                    worseningOpportunity.OpportunityId,
                    "Worsen the exact existing wound in the mixed phase.",
                    Assert.IsType<WoundMaterializationEnvelope>(
                        proposedParse.Wound),
                    Array.Empty<WoundAcceptedEffectDefinitionDraft>(),
                    Array.Empty<WoundAcceptedRootApplicationDraft>(),
                    Array.Empty<WoundAcceptedConsequenceSlotBinding>())
            },
            PreTurnCarriers = input.PreTurnCarriers with
            {
                PlayerWounds = WoundContractTestData.CreatePlayerCarrier(
                    existingCanonical)
            },
            PreTurnIdentityIndex = WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry(
                    existingWoundId,
                    owner.Realm,
                    owner.OwnerKind,
                    owner.OwnerId,
                    owner.CarrierPath,
                    "physical",
                    createdAtTurn: input.Binding.Turn - 1,
                    createdEventRef: priorEventRef,
                    semanticFingerprint: existingFingerprint)),
            PreTurnHistory = WoundContractTestData.CreateHistory(historyRow),
            PreTurnEffectCarriers = new EffectCarrierCatalogInput(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["activeEffects"] = new JsonArray()
                },
                null,
                null,
                null,
                null,
                null),
            PreTurnEffectIdentityIndex = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            }
        };

        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        return CreateWoundCommonStages(input, prepared, "anchor_mixed");
    }

    private sealed class CanonicalClockFixture : IDisposable
    {
        private CanonicalClockFixture(
            string root,
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease)
        {
            Root = root;
            FileSystem = fileSystem;
            Lease = lease;
        }

        internal string Root { get; }
        internal FileSystemManager FileSystem { get; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; }

        internal static CanonicalClockFixture Create(long minute)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "boe-t070a-clock-" + Guid.NewGuid().ToString("N"));
            var fileSystem = new FileSystemManager(
                root,
                NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            var path = fileSystem.ResolvePath(
                EffectAcceptedTurnInputComposer.WorldTimePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(
                path,
                new JsonObject
                {
                    ["currentTimeInMinutes"] = minute
                }.ToJsonString());
            var lease = fileSystem.AcquireCanonicalWriteLeaseAsync()
                .GetAwaiter()
                .GetResult();
            return new CanonicalClockFixture(root, fileSystem, lease);
        }

        public void Dispose()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class DirectWoundCommonFixture : IDisposable
    {
        private DirectWoundCommonFixture(
            string root,
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease,
            string snapshotToken)
        {
            Root = root;
            FileSystem = fileSystem;
            Lease = lease;
            SnapshotToken = snapshotToken;
        }

        private string Root { get; }
        internal FileSystemManager FileSystem { get; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; }
        internal string SnapshotToken { get; }

        internal static DirectWoundCommonFixture Create(
            WoundAcceptedTurnBinding binding,
            long minute,
            string? currentRealmOverride = null)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "boe-t070a-direct-" + Guid.NewGuid().ToString("N"));
            var fileSystem = new FileSystemManager(
                root,
                NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            WriteJsonBeforeLease(
                fileSystem,
                EffectAcceptedTurnInputComposer.WorldTimePath,
                new JsonObject
                {
                    ["currentTimeInMinutes"] = minute
                });
            _ = new LiveTurnPreparationService(fileSystem)
                .PrepareAsync(new LiveTurnPreparationOptions
                {
                    SessionId = binding.SessionId,
                    RequestId = binding.RequestId,
                    TurnNumber = binding.Turn,
                    CurrentRealm = currentRealmOverride ?? binding.Realm switch
                    {
                        "mortal_world" => "Mortal World",
                        "chaos_sea" => "Chaos Sea",
                        "shining_abode" => "Shining Abode",
                        _ => binding.Realm
                    },
                    PlayerAction = "Validate canonical Mortal wound anchors.",
                    PreGeneratedDices1d20 = new[] { 17 },
                    Timestamp = "2026-08-31T00:00:00Z"
                })
                .GetAwaiter()
                .GetResult();
            var manifest = JsonNode.Parse(File.ReadAllText(
                fileSystem.ResolvePath(
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath)))!
                .AsObject();
            var snapshotToken = manifest["manifestPayloadHash"]!
                .GetValue<string>();
            var lease = fileSystem.AcquireCanonicalWriteLeaseAsync()
                .GetAwaiter()
                .GetResult();
            return new DirectWoundCommonFixture(
                root,
                fileSystem,
                lease,
                snapshotToken);
        }

        internal void MutateClock(string mutation)
        {
            switch (mutation)
            {
                case "missing":
                    FileSystem.DeleteFile(
                        Lease,
                        EffectAcceptedTurnInputComposer.WorldTimePath);
                    return;
                case "malformed":
                    WriteJson(
                        EffectAcceptedTurnInputComposer.WorldTimePath,
                        new JsonObject
                        {
                            ["currentTimeInMinutes"] = "not-a-minute"
                        });
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mutation),
                        mutation,
                        null);
            }
        }

        internal void WriteJson(string path, JsonObject root) =>
            FileSystem.WriteFileAtomicAsync(
                    Lease,
                    path,
                    root.ToJsonString())
                .GetAwaiter()
                .GetResult();

        internal IReadOnlyDictionary<string, byte[]> CaptureTreeBytes() =>
            Directory.EnumerateFiles(
                    FileSystem.GameSessionPath,
                    "*",
                    SearchOption.AllDirectories)
                .Select(path => new
                {
                    FullPath = path,
                    RelativePath = Path.GetRelativePath(
                            FileSystem.GameSessionPath,
                            path)
                        .Replace('\\', '/')
                })
                .Where(static file => !file.RelativePath.StartsWith(
                    ".boe_runtime/",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
                .ToDictionary(
                    static file => file.RelativePath,
                    static file => File.ReadAllBytes(file.FullPath),
                    StringComparer.Ordinal);

        internal void AssertTreeBytesEqual(
            IReadOnlyDictionary<string, byte[]> expected)
        {
            var actual = CaptureTreeBytes();
            Assert.Equal(
                expected.Keys.OrderBy(static path => path, StringComparer.Ordinal),
                actual.Keys.OrderBy(static path => path, StringComparer.Ordinal));
            foreach (var pair in expected)
            {
                Assert.True(actual.TryGetValue(pair.Key, out var bytes), pair.Key);
                Assert.True(pair.Value.AsSpan().SequenceEqual(bytes), pair.Key);
            }
        }

        public void Dispose()
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Directory.Delete(Root, recursive: true);
        }

        private static void WriteJsonBeforeLease(
            FileSystemManager fileSystem,
            string path,
            JsonObject root)
        {
            var fullPath = fileSystem.ResolvePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, root.ToJsonString());
        }
    }
}
