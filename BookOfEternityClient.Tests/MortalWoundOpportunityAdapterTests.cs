using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundOpportunityAdapterTests
{
    [Theory]
    [InlineData("formal")]
    [InlineData("qte")]
    [InlineData("combat")]
    [InlineData("trap")]
    [InlineData("check")]
    [InlineData("hazard")]
    [InlineData("narrative")]
    public async Task ComposeAcceptedResponse_UsesTheSignedOccurrenceForEveryRegisteredKind(
        string adapterKind)
    {
        await using var fixture = await Fixture.CreateAsync(adapterKind);
        var before = fixture.CaptureTree();

        var result = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());

        Assert.True(result.Success, Describe(result.Issues));
        Assert.Empty(result.Transitions);
        Assert.Single(result.DecisionReceipts);
        var command = Assert.Single(ParseCommand(result).Commands);
        Assert.Equal(
            "mortal_" + adapterKind + "_injury_v1",
            command.Opportunity.ProfileKey);
        Assert.Equal(fixture.Occurrence.OccurrenceId, command.Opportunity.OpportunityId);
        Assert.Equal(fixture.Occurrence.OpportunityRef, command.Opportunity.PublicRef);
        Assert.Equal(fixture.ActiveSnapshotToken, command.Opportunity.SnapshotToken);
        Assert.Equal(2, fixture.Occurrence.AcceptedEvents.Count);
        AssertTreeEqual(before, fixture.CaptureTree());
    }

    [Fact]
    public async Task ComposeAcceptedResponse_ExposesOnlyTheExactWriteFreeIngress()
    {
        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundOpportunityAdapter",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(type);
        Assert.True(type!.IsAbstract && type.IsSealed);
        Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == "ComposeAcceptedResponse")
            .ToArray();
        var method = Assert.Single(methods);
        Assert.Equal(typeof(WoundResponseInputCompositionResult), method.ReturnType);
        Assert.Equal(
            new[]
            {
                typeof(FileSystemManager),
                typeof(FileSystemManager.CanonicalWriteLease),
                typeof(JsonElement),
                typeof(GameResponse)
            },
            method.GetParameters().Select(parameter => parameter.ParameterType));

        await using var fixture = await Fixture.CreateAsync("formal");
        var before = fixture.CaptureTree();
        _ = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());
        AssertTreeEqual(before, fixture.CaptureTree());
    }

    [Fact]
    public async Task ComposeAcceptedResponse_RejectsUnknownAuthorityAndChangedCorrelationWithoutWrites()
    {
        await using var fixture = await Fixture.CreateAsync("formal");
        var variants = new List<JsonElement>();

        var unknown = fixture.SourceEvent.DeepClone().AsObject();
        unknown["snapshotToken"] = "caller_snapshot";
        variants.Add(JsonSerializer.SerializeToElement(unknown));

        var changedSource = fixture.SourceEvent.DeepClone().AsObject();
        changedSource["source"]!["sourceId"] = "different_source";
        variants.Add(JsonSerializer.SerializeToElement(changedSource));

        var forbiddenCap = fixture.SourceEvent.DeepClone().AsObject();
        forbiddenCap["hardMaximumSeverityRank"] = fixture.Occurrence.HardMaximumSeverityRank;
        variants.Add(JsonSerializer.SerializeToElement(forbiddenCap));

        var explicitNullTarget = fixture.SourceEvent.DeepClone().AsObject();
        explicitNullTarget["worseningTarget"] = null;
        variants.Add(JsonSerializer.SerializeToElement(explicitNullTarget));

        var raw = fixture.SourceEvent.ToJsonString();
        using var duplicateDocument = JsonDocument.Parse(
            raw.Insert(1, "\"adapterKind\":\"formal\","));
        variants.Add(duplicateDocument.RootElement.Clone());

        foreach (var sourceEvent in variants)
        {
            var before = fixture.CaptureTree();
            var result = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
                fixture.FileSystem,
                fixture.Lease,
                sourceEvent,
                fixture.NoneResponse());
            Assert.False(result.Success);
            Assert.NotEmpty(result.Issues);
            AssertTreeEqual(before, fixture.CaptureTree());
        }
    }

    [Fact]
    public async Task ComposeAcceptedResponse_ResolvesWorseningOnlyFromTheSignedCarrierBeforeImage()
    {
        await using var fixture = await Fixture.CreateAsync(
            "formal",
            worseningWoundId: "wound_adapter_existing");
        await fixture.ReplaceLivePlayerCarrierAsync(
            WoundContractTestData.CreatePlayerCarrier());
        var before = fixture.CaptureTree();

        var result = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());

        Assert.True(result.Success, Describe(result.Issues));
        var opportunity = Assert.Single(ParseCommand(result).Commands).Opportunity;
        var target = Assert.IsType<WoundOpportunityWorseningTargetAuthority>(
            opportunity.WorseningTarget);
        Assert.Equal("wound_adapter_existing", target.Wound.WoundId);
        Assert.Equal("retrauma", target.CauseKind);
        AssertTreeEqual(before, fixture.CaptureTree());
    }

    [Fact]
    public async Task ComposeAcceptedResponse_RejectsWorseningTargetDuplicatedAcrossSignedCarriers()
    {
        const string woundId = "wound_adapter_global_duplicate";
        await using var fixture = await Fixture.CreateAsync(
            "formal",
            worseningWoundId: woundId,
            duplicateWorseningInNpc: true);
        var before = fixture.CaptureTree();

        var result = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_wound_opportunity_adapter_worsening_target_unresolved");
        AssertTreeEqual(before, fixture.CaptureTree());
    }

    [Fact]
    public async Task ValidationAuthority_IndependentlyReconstructsTheCompleteEventSet()
    {
        await using var fixture = await Fixture.CreateAsync("formal");
        var composed = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());
        Assert.True(composed.Success, Describe(composed.Issues));
        var submitted = Assert.Single(ParseCommand(composed).Commands).Opportunity;

        var reconstructed = fixture.ReconstructValidationAuthority(
            new[] { submitted });

        Assert.True(reconstructed.Success, Describe(reconstructed.Issues));
        Assert.Equal(
            fixture.ActiveSnapshotToken,
            reconstructed.Binding!.SnapshotToken);
        Assert.Equal(
            fixture.Occurrence.AcceptedEvents.Select(value => value.EventRef),
            reconstructed.Binding.AcceptedEvents.Select(value => value.EventRef));
        Assert.Equal(
            submitted.AuthorityFingerprint,
            Assert.Single(reconstructed.Opportunities).AuthorityFingerprint);

        var reorderedEvents = reconstructed.Binding.AcceptedEvents
            .Reverse()
            .ToArray();
        var reorderedBinding = new WoundAcceptedTurnBinding(
            reconstructed.Binding.SessionId,
            reconstructed.Binding.RequestId,
            reconstructed.Binding.SnapshotToken,
            reconstructed.Binding.Realm,
            reconstructed.Binding.Turn,
            reorderedEvents,
            WoundAcceptedEventSetFingerprint.Compute(reorderedEvents));
        var selected = reorderedEvents.Single(value => string.Equals(
            value.EventRef,
            submitted.EventRef,
            StringComparison.Ordinal));
        var forged = WoundOpportunityAuthority.Compose(
            new WoundOpportunityBuildRequest(
                reorderedBinding,
                fixture.Occurrence.OccurrenceId,
                fixture.Occurrence.OpportunityRef,
                selected.EventRef,
                fixture.Occurrence.Owner,
                fixture.Occurrence.Domain,
                fixture.Occurrence.ProfileKey,
                fixture.Occurrence.Source.Kind,
                fixture.Occurrence.Source.SourceId,
                fixture.Occurrence.Source.State,
                new WoundOpportunityEventEvidence(
                    fixture.Occurrence.AdapterKind,
                    selected.Kind,
                    selected.AuthorityId,
                    fixture.Occurrence.Outcome.Kind,
                    fixture.Occurrence.Outcome.MaximumSeverityRank,
                    fixture.Occurrence.Outcome.ReadableCause),
                fixture.Occurrence.HardMaximumSeverityRank,
                null,
                WoundOpportunityAuthority.CloneSafeContext(
                    fixture.Occurrence.SafeContext)));
        Assert.True(forged.Success, Describe(forged.Issues));

        var rejected = fixture.ReconstructValidationAuthority(
            new[] { forged.Opportunity! });

        Assert.False(rejected.Success);
        Assert.Contains(
            rejected.Issues,
            issue => issue.Code ==
                "mortal_wound_validation_opportunity_authority_mismatch");
    }

    [Fact]
    public async Task ValidationAuthority_ResolvesWorseningOnlyFromTheSignedPreTurnCarrier()
    {
        const string woundId = "wound_validation_existing";
        await using var fixture = await Fixture.CreateAsync(
            "formal",
            worseningWoundId: woundId);
        var composed = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());
        Assert.True(composed.Success, Describe(composed.Issues));
        var submitted = Assert.Single(ParseCommand(composed).Commands).Opportunity;
        var signedCarriers = new WoundCarrierCatalogInput(
            WoundContractTestData.CreatePlayerCarrier(
                WoundContractTestData.CreateActiveWound(woundId)),
            null,
            null,
            null,
            null);

        var reconstructed = fixture.ReconstructValidationAuthority(
            new[] { submitted },
            signedCarriers);
        var missingCarrier = fixture.ReconstructValidationAuthority(
            new[] { submitted });

        Assert.True(reconstructed.Success, Describe(reconstructed.Issues));
        Assert.Equal(
            woundId,
            Assert.Single(reconstructed.Opportunities)
                .WorseningTarget!.Wound.WoundId);
        Assert.False(missingCarrier.Success);
        Assert.Contains(
            missingCarrier.Issues,
            issue => issue.Code ==
                "mortal_wound_validation_worsening_target_unresolved");
    }

    [Fact]
    public async Task ValidationAuthority_ProjectsEverySignedPriorDecisionReceipt()
    {
        await using var fixture = await Fixture.CreateAsync("formal");
        var composed = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());
        Assert.True(composed.Success, Describe(composed.Issues));
        var priorOpportunity = Assert.Single(ParseCommand(composed).Commands)
            .Opportunity;
        var evaluated = WoundOpportunityDecisionAuthority.Evaluate(
            priorOpportunity,
            new WoundOpportunityDecisionRequest(
                priorOpportunity.PublicRef,
                "none",
                null,
                null),
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(evaluated.Success, Describe(evaluated.Issues));
        var decision = evaluated.Decision!;
        var consumed = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
            fixture.SignedOccurrences,
            fixture.SignedReceipts,
            new MortalWoundOpportunityReceiptDraft(
                new MortalWoundOpportunityDecisionBinding(
                    priorOpportunity.SessionId,
                    priorOpportunity.RequestId,
                    priorOpportunity.SnapshotToken,
                    42),
                priorOpportunity.OpportunityId,
                priorOpportunity.AuthorityFingerprint,
                decision.Decision,
                decision.DecisionFingerprint,
                decision.OperationKey,
                null,
                null),
            Fixture.EmptyHistory());
        Assert.Equal("appended", consumed.Disposition);
        Assert.Empty(consumed.Issues);

        var source = fixture.Occurrence;
        var followupCandidate = new MortalWoundOccurrenceCandidate(
            source.SourceSessionId,
            source.SourceRequestId,
            source.SourceSnapshotToken,
            source.SourceTurn,
            "source_batch_adapter_followup",
            0,
            1,
            source.AdapterKind,
            source.AcceptedEventOrdinal,
            source.AcceptedEvents,
            source.Owner,
            source.Domain,
            source.ProfileKey,
            source.Source,
            source.Outcome,
            source.HardMaximumSeverityRank,
            source.MinimumSeverityRank,
            null,
            WoundOpportunityAuthority.CloneSafeContext(source.SafeContext),
            null,
            Fixture.FingerprintForTest("source-result:followup"));
        var appended = MortalWoundOccurrenceState.PlanAppend(
            consumed.OccurrenceState!,
            new MortalWoundOccurrenceCandidateBatch(new[] { followupCandidate }),
            consumed.ReceiptState!);
        Assert.Equal("appended", appended.Disposition);
        Assert.Empty(appended.Issues);
        var followup = Assert.Single(appended.State!.Occurrences);
        var rebound = WoundAcceptedEventAuthorityComposer.RebindMortalOccurrence(
            followup,
            appended.State.Occurrences,
            priorOpportunity.SessionId,
            priorOpportunity.RequestId,
            priorOpportunity.SnapshotToken,
            42);
        Assert.True(rebound.Success, Describe(rebound.Issues));
        var binding = new WoundAcceptedTurnBinding(
            priorOpportunity.SessionId,
            priorOpportunity.RequestId,
            priorOpportunity.SnapshotToken,
            "mortal_world",
            42,
            rebound.Events,
            rebound.EventsFingerprint);
        var selected = rebound.Events[followup.AcceptedEventOrdinal];
        var followupOpportunity = WoundOpportunityAuthority.Compose(
            new WoundOpportunityBuildRequest(
                binding,
                followup.OccurrenceId,
                followup.OpportunityRef,
                selected.EventRef,
                followup.Owner,
                followup.Domain,
                followup.ProfileKey,
                followup.Source.Kind,
                followup.Source.SourceId,
                followup.Source.State,
                new WoundOpportunityEventEvidence(
                    followup.AdapterKind,
                    selected.Kind,
                    selected.AuthorityId,
                    followup.Outcome.Kind,
                    followup.Outcome.MaximumSeverityRank,
                    followup.Outcome.ReadableCause),
                followup.HardMaximumSeverityRank,
                null,
                WoundOpportunityAuthority.CloneSafeContext(
                    followup.SafeContext)));
        Assert.True(followupOpportunity.Success, Describe(followupOpportunity.Issues));

        var reconstructed = MortalWoundOpportunityValidationAuthority.Reconstruct(
            binding.SessionId,
            binding.RequestId,
            binding.SnapshotToken,
            binding.Realm,
            binding.Turn,
            appended.State,
            consumed.ReceiptState!,
            new WoundCarrierCatalogInput(null, null, null, null, null),
            new[] { followupOpportunity.Opportunity! });

        Assert.True(reconstructed.Success, Describe(reconstructed.Issues));
        var priorReceipt = Assert.Single(reconstructed.PriorReceipts);
        Assert.Equal(priorOpportunity.OpportunityId, priorReceipt.OpportunityId);
        Assert.Equal(decision.DecisionFingerprint, priorReceipt.DecisionFingerprint);
        Assert.Equal(decision.OperationKey, priorReceipt.OperationKey);
    }

    [Fact]
    public async Task ComposeAcceptedResponse_ColdReplaysTheCurrentReceiptAndRejectsTheOldEventAfterNextSnapshot()
    {
        await using var fixture = await Fixture.CreateAsync("formal");
        var response = fixture.NoneResponse();
        var first = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            response);
        Assert.True(first.Success, Describe(first.Issues));
        await fixture.PublishNoneReceiptAsync(first);
        await fixture.RestartAsync();
        var beforeReplay = fixture.CaptureTree();

        var replay = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            response);

        Assert.True(replay.Success, Describe(replay.Issues));
        Assert.Empty(replay.CommandRoot!["commands"]!.AsArray());
        Assert.Empty(replay.Transitions);
        AssertTreeEqual(beforeReplay, fixture.CaptureTree());

        await fixture.PrepareNextTurnAsync();
        var beforeStale = fixture.CaptureTree();
        var stale = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            response);
        Assert.False(stale.Success);
        Assert.NotEmpty(stale.Issues);
        AssertTreeEqual(beforeStale, fixture.CaptureTree());
    }

    [Fact]
    public async Task ComposeAcceptedResponse_RebindsTheRemainingCandidateFromAMixedSignedBatch()
    {
        await using var fixture = await Fixture.CreateAsync(
            "formal",
            candidateCount: 2);
        var first = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());
        Assert.True(first.Success, Describe(first.Issues));
        await fixture.PublishNoneReceiptAsync(first);
        fixture.SelectCandidate(1);
        await fixture.PrepareNextTurnAsync();

        var remaining = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());

        Assert.True(remaining.Success, Describe(remaining.Issues));
        var command = Assert.Single(ParseCommand(remaining).Commands);
        Assert.Equal(fixture.Occurrence.OccurrenceId, command.Opportunity.OpportunityId);
        Assert.Equal(1, fixture.Occurrence.ProducerCandidateOrdinal);
    }

    [Fact]
    public async Task ComposeAcceptedResponse_RejectsAResealedReceiptWhoseDecisionDoesNotMatchItsSeal()
    {
        await using var fixture = await Fixture.CreateAsync("formal");
        var first = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());
        Assert.True(first.Success, Describe(first.Issues));
        await fixture.PublishNoneReceiptAsync(first);
        await fixture.ResealCurrentReceiptAsMaterializeAsync();

        var replay = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());

        Assert.False(replay.Success);
        Assert.Contains(replay.Issues, issue =>
            issue.Code is "mortal_wound_opportunity_receipt_history_conflict" or
                "mortal_wound_opportunity_adapter_receipt_replay_mismatch");
    }

    [Fact]
    public async Task ComposeAcceptedResponse_RejectsAResealedReceiptWithChangedOpportunityAuthority()
    {
        await using var fixture = await Fixture.CreateAsync("formal");
        var first = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());
        Assert.True(first.Success, Describe(first.Issues));
        await fixture.PublishNoneReceiptAsync(first);
        await fixture.ResealCurrentReceiptAuthorityAsync();

        var replay = MortalWoundOpportunityAdapter.ComposeAcceptedResponse(
            fixture.FileSystem,
            fixture.Lease,
            JsonSerializer.SerializeToElement(fixture.SourceEvent),
            fixture.NoneResponse());

        Assert.False(replay.Success);
        Assert.Contains(replay.Issues, issue =>
            issue.Code == "mortal_wound_opportunity_adapter_receipt_replay_mismatch");
    }

    private static WoundResponseCommandParsingResult ParseCommand(
        WoundResponseInputCompositionResult composition)
    {
        using var document = JsonDocument.Parse(composition.CommandRoot!.ToJsonString());
        var parsed = WoundResponseInputComposer.ParseCommandRoot(document.RootElement);
        Assert.True(parsed.Success, Describe(parsed.Issues));
        return parsed;
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(issue =>
            $"{issue.FilePath}: {issue.Code}: {issue.Message}"));

    private static void AssertTreeEqual(
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(
            expected.Keys.OrderBy(path => path, StringComparer.Ordinal),
            actual.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var pair in expected)
        {
            Assert.True(actual.TryGetValue(pair.Key, out var bytes), pair.Key);
            Assert.True(pair.Value.AsSpan().SequenceEqual(bytes), pair.Key);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private const int ActiveTurn = 42;
        private readonly string _root;
        private readonly MortalWoundOccurrenceState _signedOccurrences;
        private readonly MortalWoundOpportunityReceiptState _signedReceipts;

        private Fixture(
            string root,
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease,
            MortalWoundOccurrenceState signedOccurrences,
            MortalWoundOpportunityReceiptState signedReceipts,
            MortalWoundOccurrence occurrence,
            JsonObject sourceEvent,
            string activeSnapshotToken)
        {
            _root = root;
            FileSystem = fileSystem;
            Lease = lease;
            _signedOccurrences = signedOccurrences;
            _signedReceipts = signedReceipts;
            Occurrence = occurrence;
            SourceEvent = sourceEvent;
            ActiveSnapshotToken = activeSnapshotToken;
        }

        internal FileSystemManager FileSystem { get; private set; }
        internal FileSystemManager.CanonicalWriteLease Lease { get; private set; }
        internal MortalWoundOccurrence Occurrence { get; private set; }
        internal JsonObject SourceEvent { get; private set; }
        internal string ActiveSnapshotToken { get; private set; }
        internal MortalWoundOccurrenceState SignedOccurrences =>
            _signedOccurrences.DetachForReceiptPlan();
        internal MortalWoundOpportunityReceiptState SignedReceipts =>
            ParseReceipts(MortalWoundOpportunityReceiptState.SerializeCanonical(
                _signedReceipts));

        internal MortalWoundOpportunityValidationResult
            ReconstructValidationAuthority(
                IReadOnlyList<WoundOpportunityAuthority> submitted,
                WoundCarrierCatalogInput? carriers = null) =>
            MortalWoundOpportunityValidationAuthority.Reconstruct(
                "adapter_session",
                "adapter_request_" + ActiveTurn,
                ActiveSnapshotToken,
                "mortal_world",
                ActiveTurn,
                _signedOccurrences,
                _signedReceipts,
                carriers ?? new WoundCarrierCatalogInput(
                    null,
                    null,
                    null,
                    null,
                    null),
                submitted);

        internal static async Task<Fixture> CreateAsync(
            string adapterKind,
            string? worseningWoundId = null,
            int candidateCount = 1,
            bool duplicateWorseningInNpc = false)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "boe-mortal-wound-adapter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            var emptyOccurrences = ParseOccurrences(
                "{\"schemaVersion\":1,\"occurrences\":[]}");
            var emptyReceipts = ParseReceipts(
                "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}");
            Assert.InRange(candidateCount, 1, 2);
            var eventCoordinates = candidateCount == 1
                ? new[]
                {
                    new WoundAcceptedResponseEventCoordinate(
                        "event_adapter_selected",
                        adapterKind + "_resolution",
                        "authority_adapter_selected"),
                    new WoundAcceptedResponseEventCoordinate(
                        "event_adapter_sibling",
                        "resource_resolution",
                        "authority_adapter_sibling")
                }
                : new[]
                {
                    new WoundAcceptedResponseEventCoordinate(
                        "event_adapter_selected_0",
                        adapterKind + "_resolution",
                        "authority_adapter_selected_0"),
                    new WoundAcceptedResponseEventCoordinate(
                        "event_adapter_selected_1",
                        adapterKind + "_resolution",
                        "authority_adapter_selected_1"),
                    new WoundAcceptedResponseEventCoordinate(
                        "event_adapter_sibling",
                        "resource_resolution",
                        "authority_adapter_sibling")
                };
            var eventSet = WoundAcceptedEventAuthorityComposer.Compose(
                new WoundAcceptedResponseEventProjection(
                    "source_session_adapter",
                    "source_request_adapter",
                    "source_snapshot_adapter",
                    41,
                    eventCoordinates),
                Enumerable.Range(0, candidateCount)
                    .Select(index => new WoundSelectedEventEvidence(
                        index,
                        new WoundOpportunityEventEvidence(
                            adapterKind,
                            eventCoordinates[index].Kind,
                            eventCoordinates[index].AuthorityId,
                            "harmful",
                            3,
                            "Подтверждённое повреждение в принятом результате.")))
                    .ToArray());
            Assert.True(eventSet.Success, Describe(eventSet.Issues));

            var candidates = Enumerable.Range(0, candidateCount)
                .Select(index => new MortalWoundOccurrenceCandidate(
                    "source_session_adapter",
                    "source_request_adapter",
                    "source_snapshot_adapter",
                    41,
                    "source_batch_adapter_" + adapterKind,
                    index,
                    candidateCount,
                    adapterKind,
                    index,
                    eventSet.Events,
                    new WoundOwnerCoordinate(
                        "mortal_world",
                        "player",
                        "player_current",
                        WoundCarrierCatalog.PlayerPath),
                    "physical",
                    "mortal_" + adapterKind + "_injury_v1",
                    new MortalWoundOccurrenceSource(
                        adapterKind + "_source",
                        "source_adapter_accepted_" + index,
                        "active"),
                    new MortalWoundOccurrenceOutcome(
                        "harmful",
                        3,
                        "Подтверждённое повреждение в принятом результате."),
                    4,
                    null,
                    null,
                    new WoundOpportunitySafeContext(
                        "вы",
                        "принятое опасное событие",
                        new[] { "anatomical", "systemic", "other" }),
                    worseningWoundId is null
                        ? null
                        : new MortalWoundOccurrenceWorseningTarget(
                            worseningWoundId,
                            "retrauma"),
                    Fingerprint("source-result:" + adapterKind)))
                .ToArray();
            var append = MortalWoundOccurrenceState.PlanAppend(
                emptyOccurrences,
                new MortalWoundOccurrenceCandidateBatch(candidates),
                emptyReceipts);
            Assert.Equal("appended", append.Disposition);
            Assert.Empty(append.Issues);
            var signedOccurrences = append.State!;
            var occurrence = signedOccurrences.Occurrences[0];

            await fs.WriteFileAtomicAsync(
                MortalWoundOccurrenceState.StatePath,
                MortalWoundOccurrenceState.SerializeCanonical(signedOccurrences));
            await fs.WriteFileAtomicAsync(
                MortalWoundOpportunityReceiptState.StatePath,
                MortalWoundOpportunityReceiptState.SerializeCanonical(emptyReceipts));
            await fs.WriteFileAtomicAsync(
                WoundHistoryState.HistoryPath,
                WoundContractTestData.CreateHistory().ToJsonString());
            await fs.WriteFileAtomicAsync(
                WoundCarrierCatalog.NpcPath,
                (duplicateWorseningInNpc
                    ? WoundContractTestData.CreateNamedNpcCarrier(
                        "npc_duplicate_owner",
                        WoundContractTestData.CreateActiveWound(
                            worseningWoundId!,
                            ownerKind: "npc",
                            ownerId: "npc_duplicate_owner",
                            carrierPath: WoundCarrierCatalog.NpcPath))
                    : new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["entries"] = new JsonArray()
                    }).ToJsonString());
            await fs.WriteFileAtomicAsync(
                WoundCarrierCatalog.EnemiesPath,
                new JsonObject { ["enemiesData"] = new JsonArray() }.ToJsonString());
            await fs.WriteFileAtomicAsync(
                WoundCarrierCatalog.AlliesPath,
                new JsonObject { ["alliesData"] = new JsonArray() }.ToJsonString());
            await fs.WriteFileAtomicAsync(
                WoundCarrierCatalog.AfterlifeProfilesPath,
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["profiles"] = new JsonArray()
                }.ToJsonString());
            if (worseningWoundId is not null)
            {
                await fs.WriteFileAtomicAsync(
                    WoundCarrierCatalog.PlayerPath,
                    WoundContractTestData.CreatePlayerCarrier(
                        WoundContractTestData.CreateActiveWound(worseningWoundId))
                        .ToJsonString());
            }

            await PrepareAsync(fs, ActiveTurn);
            var lease = await fs.AcquireCanonicalWriteLeaseAsync();
            var snapshot = PendingTurnSnapshotReader.ReadCurrent(
                fs,
                lease,
                new[]
                {
                    MortalWoundOccurrenceState.StatePath,
                    MortalWoundOpportunityReceiptState.StatePath
                });
            Assert.True(snapshot.Success, Describe(snapshot.Issues));

            return new Fixture(
                root,
                fs,
                lease,
                signedOccurrences,
                emptyReceipts,
                occurrence,
                SourceEventFor(occurrence),
                snapshot.Snapshot!.SnapshotToken);
        }

        internal GameResponse NoneResponse() => new()
        {
            Response = "Опасность миновала без новой раны.",
            WoundDecisions = new[]
            {
                JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["opportunityRef"] = Occurrence.OpportunityRef,
                    ["decision"] = "none"
                })
            }
        };

        internal IReadOnlyDictionary<string, byte[]> CaptureTree() =>
            Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(_root, path)
                    .StartsWith(".boe_runtime", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(
                    path => Path.GetRelativePath(_root, path).Replace('\\', '/'),
                    File.ReadAllBytes,
                    StringComparer.Ordinal);

        internal Task ReplaceLivePlayerCarrierAsync(JsonObject root) =>
            FileSystem.WriteFileAtomicAsync(
                Lease,
                WoundCarrierCatalog.PlayerPath,
                root.ToJsonString());

        internal async Task PublishNoneReceiptAsync(
            WoundResponseInputCompositionResult composition)
        {
            var command = Assert.Single(ParseCommand(composition).Commands);
            var decisionReceipt = Assert.Single(composition.DecisionReceipts);
            var plan = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
                _signedOccurrences,
                _signedReceipts,
                new MortalWoundOpportunityReceiptDraft(
                    new MortalWoundOpportunityDecisionBinding(
                        command.Opportunity.SessionId,
                        command.Opportunity.RequestId,
                        command.Opportunity.SnapshotToken,
                        ActiveTurn),
                    decisionReceipt.OpportunityId,
                    command.Opportunity.AuthorityFingerprint,
                    "none",
                    decisionReceipt.DecisionFingerprint,
                    decisionReceipt.OperationKey,
                    null,
                    null),
                ParseHistory());
            Assert.Equal("appended", plan.Disposition);
            Assert.Empty(plan.Issues);
            await FileSystem.WriteFileAtomicAsync(
                Lease,
                MortalWoundOccurrenceState.StatePath,
                MortalWoundOccurrenceState.SerializeCanonical(plan.OccurrenceState!));
            await FileSystem.WriteFileAtomicAsync(
                Lease,
                MortalWoundOpportunityReceiptState.StatePath,
                MortalWoundOpportunityReceiptState.SerializeCanonical(plan.ReceiptState!));
        }

        internal void SelectCandidate(int ordinal)
        {
            Occurrence = _signedOccurrences.Occurrences.Single(value =>
                value.ProducerCandidateOrdinal == ordinal);
            SourceEvent = SourceEventFor(Occurrence);
        }

        internal async Task ResealCurrentReceiptAsMaterializeAsync()
        {
            var current = ParseReceipts(FileSystem.ReadFileSync(
                MortalWoundOpportunityReceiptState.StatePath)!);
            var original = Assert.Single(current.Receipts);
            var changed = original with
            {
                Decision = "materialize",
                WoundId = "wound_resealed_mismatch",
                TransitionId = "transition_resealed_mismatch",
                ReceiptFingerprint = string.Empty
            };
            changed = changed with
            {
                ReceiptFingerprint =
                    MortalWoundOpportunityReceiptState.ComputeReceiptFingerprint(changed)
            };
            var root = JsonNode.Parse(
                MortalWoundOpportunityReceiptState.SerializeCanonical(current))!
                .AsObject();
            var row = root["receipts"]!.AsArray()[0]!.AsObject();
            row["decision"] = changed.Decision;
            row["woundId"] = changed.WoundId;
            row["transitionId"] = changed.TransitionId;
            row["receiptFingerprint"] = changed.ReceiptFingerprint;
            await FileSystem.WriteFileAtomicAsync(
                Lease,
                MortalWoundOpportunityReceiptState.StatePath,
                root.ToJsonString());
        }

        internal async Task ResealCurrentReceiptAuthorityAsync()
        {
            var current = ParseReceipts(FileSystem.ReadFileSync(
                MortalWoundOpportunityReceiptState.StatePath)!);
            var original = Assert.Single(current.Receipts);
            var changed = original with
            {
                OpportunityAuthorityFingerprint =
                    Fingerprint("forged-opportunity-authority"),
                ReceiptFingerprint = string.Empty
            };
            changed = changed with
            {
                ReceiptFingerprint =
                    MortalWoundOpportunityReceiptState.ComputeReceiptFingerprint(changed)
            };
            var root = JsonNode.Parse(
                MortalWoundOpportunityReceiptState.SerializeCanonical(current))!
                .AsObject();
            var row = root["receipts"]!.AsArray()[0]!.AsObject();
            row["opportunityAuthorityFingerprint"] =
                changed.OpportunityAuthorityFingerprint;
            row["receiptFingerprint"] = changed.ReceiptFingerprint;
            await FileSystem.WriteFileAtomicAsync(
                Lease,
                MortalWoundOpportunityReceiptState.StatePath,
                root.ToJsonString());
        }

        internal async Task RestartAsync()
        {
            await Lease.DisposeAsync();
            FileSystem = new FileSystemManager(
                _root,
                NullLogger<FileSystemManager>.Instance);
            FileSystem.EnsureDirectoryStructure();
            Lease = await FileSystem.AcquireCanonicalWriteLeaseAsync();
        }

        internal async Task PrepareNextTurnAsync()
        {
            await Lease.DisposeAsync();
            await PrepareAsync(FileSystem, ActiveTurn + 1);
            Lease = await FileSystem.AcquireCanonicalWriteLeaseAsync();
            var read = PendingTurnSnapshotReader.ReadCurrent(
                FileSystem,
                Lease,
                new[]
                {
                    MortalWoundOccurrenceState.StatePath,
                    MortalWoundOpportunityReceiptState.StatePath
                });
            Assert.True(read.Success, Describe(read.Issues));
            ActiveSnapshotToken = read.Snapshot!.SnapshotToken;
        }

        public async ValueTask DisposeAsync()
        {
            await Lease.DisposeAsync();
            Directory.Delete(_root, recursive: true);
        }

        private static JsonObject SourceEventFor(MortalWoundOccurrence occurrence)
        {
            var result = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["adapterKind"] = occurrence.AdapterKind,
                ["acceptedEventOrdinal"] = occurrence.AcceptedEventOrdinal,
                ["opportunityRef"] = occurrence.OpportunityRef,
                ["owner"] = new JsonObject
                {
                    ["realm"] = occurrence.Owner.Realm,
                    ["ownerKind"] = occurrence.Owner.OwnerKind,
                    ["ownerId"] = occurrence.Owner.OwnerId,
                    ["carrierPath"] = occurrence.Owner.CarrierPath
                },
                ["domain"] = occurrence.Domain,
                ["profileKey"] = occurrence.ProfileKey,
                ["source"] = new JsonObject
                {
                    ["kind"] = occurrence.Source.Kind,
                    ["sourceId"] = occurrence.Source.SourceId,
                    ["state"] = occurrence.Source.State
                },
                ["outcome"] = new JsonObject
                {
                    ["kind"] = occurrence.Outcome.Kind,
                    ["maximumSeverityRank"] = occurrence.Outcome.MaximumSeverityRank,
                    ["readableCause"] = occurrence.Outcome.ReadableCause
                },
                ["safeContext"] = new JsonObject
                {
                    ["target"] = occurrence.SafeContext.Target,
                    ["cause"] = occurrence.SafeContext.Cause,
                    ["allowedLocationKinds"] = new JsonArray(
                        occurrence.SafeContext.AllowedLocationKinds
                            .Select(value => (JsonNode?)value)
                            .ToArray())
                }
            };
            if (occurrence.WorseningTarget is not null)
            {
                result["worseningTarget"] = new JsonObject
                {
                    ["woundId"] = occurrence.WorseningTarget.WoundId,
                    ["causeKind"] = occurrence.WorseningTarget.CauseKind
                };
            }
            return result;
        }

        private static Task<LiveTurnPreparationResult> PrepareAsync(
            FileSystemManager fs,
            int turn) =>
            new LiveTurnPreparationService(fs).PrepareAsync(
                new LiveTurnPreparationOptions
                {
                    SessionId = "adapter_session",
                    RequestId = "adapter_request_" + turn,
                    TurnNumber = turn,
                    CurrentRealm = "Mortal World",
                    PlayerAction = "Проверить материализацию раны."
                });

        private static MortalWoundOccurrenceState ParseOccurrences(string json)
        {
            var parsed = MortalWoundOccurrenceState.Parse(
                json,
                MortalWoundOccurrenceState.StatePath);
            Assert.True(parsed.IsValid, Describe(parsed.Issues));
            return parsed.State!;
        }

        private static MortalWoundOpportunityReceiptState ParseReceipts(string json)
        {
            var parsed = MortalWoundOpportunityReceiptState.Parse(
                json,
                MortalWoundOpportunityReceiptState.StatePath);
            Assert.True(parsed.IsValid, Describe(parsed.Issues));
            return parsed.State!;
        }

        private static WoundHistoryState ParseHistory()
        {
            var parsed = WoundHistoryState.Parse(
                WoundContractTestData.CreateHistory().ToJsonString(),
                WoundHistoryState.HistoryPath);
            Assert.True(parsed.IsValid, Describe(parsed.Issues));
            return parsed.State!;
        }

        internal static WoundHistoryState EmptyHistory() => ParseHistory();

        internal static string FingerprintForTest(string value) =>
            Fingerprint(value);

        private static string Fingerprint(string value) =>
            WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.test.mortal_wound_adapter",
                "1",
                value
            });
    }
}
