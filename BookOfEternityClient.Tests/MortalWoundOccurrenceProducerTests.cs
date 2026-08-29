using System.Reflection;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundOccurrenceProducerTests
{
    private static readonly Lazy<AcceptedMechanicsResourcePlanningResult>
        FinalizedResourceResult = new(CreateFinalizedResourceResult);

    public static TheoryData<string> AdapterKinds => new()
    {
        "formal", "qte", "combat", "trap", "check", "hazard", "narrative"
    };

    [Fact]
    public void Api_IsClosedTypedAndAcceptsNoJsonOrFingerprintArgument()
    {
        var draftType = RequiredType("IMortalWoundOccurrenceProducerDraft");
        Assert.True(draftType.IsInterface);
        var project = Assert.Single(draftType.GetMethods(), method => method.Name == "Project");
        Assert.Equal(
            new[]
            {
                typeof(AcceptedMechanicsResourcePlanningResult),
                typeof(AcceptedEffectBoundaryTranscript)
            },
            project.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(
            RequiredType("MortalWoundOccurrenceProducerProjectionResult"),
            project.ReturnType);

        var sealedResult = RequiredType("MortalWoundAcceptedProducerResult");
        var constructor = Assert.Single(
            sealedResult.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.True(constructor.IsPrivate);
        Assert.DoesNotContain(
            constructor.GetParameters(),
            parameter => parameter.Name?.Contains(
                             "fingerprint",
                             StringComparison.OrdinalIgnoreCase) == true ||
                         parameter.ParameterType.Name.Contains(
                             "Json",
                             StringComparison.Ordinal));

        var reducer = RequiredType("MortalWoundOccurrenceCandidateReducer");
        var reduce = Assert.Single(
            reducer.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name == "ReduceRegistered");
        Assert.Equal(
            new[]
            {
                draftType,
                typeof(AcceptedMechanicsResourcePlanningResult)
            },
            reduce.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(
            RequiredType("MortalWoundOccurrenceCandidateReductionResult"),
            reduce.ReturnType);
        Assert.DoesNotContain(
            reducer.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name == "Reduce" && !method.IsPrivate);
        Assert.DoesNotContain(
            reducer.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name.Contains("File", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Write", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Lease", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(AdapterKinds))]
    public void ClosedReducerLanguage_AllSevenKindsCreateOneCompleteBatch(string adapterKind)
    {
        var producer = Producer(
            adapterKind,
            Harm(0, 0, adapterKind),
            Harm(1, 1, adapterKind));

        var reduced = Reduce(producer);

        Assert.True(reduced.Success, Describe(reduced.Issues));
        Assert.Equal("harmful", reduced.Disposition);
        var batch = Assert.IsType<MortalWoundOccurrenceCandidateBatch>(reduced.Batch);
        Assert.Equal(new[] { 0, 1 }, batch.Candidates.Select(value => value.ProducerCandidateOrdinal));
        Assert.All(batch.Candidates, candidate =>
        {
            Assert.Equal(2, candidate.ProducerCandidateCount);
            Assert.Equal(adapterKind, candidate.AdapterKind);
            Assert.Equal(producer.SourceResultFingerprint, candidate.SourceResultFingerprint);
            Assert.Equal(producer.AcceptedResponse.SessionId, candidate.SourceSessionId);
            Assert.Equal(producer.AcceptedResponse.RequestId, candidate.SourceRequestId);
            Assert.Equal(producer.AcceptedResponse.Turn, candidate.SourceTurn);
            Assert.Equal(2, candidate.AcceptedEvents.Count);
        });

        var emptyPending = MortalWoundOccurrenceState.Parse(
            "{\"schemaVersion\":1,\"occurrences\":[]}",
            MortalWoundOccurrenceState.StatePath).State!;
        var emptyReceipts = MortalWoundOpportunityReceiptState.Parse(
            "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}",
            MortalWoundOpportunityReceiptState.StatePath).State!;
        var append = MortalWoundOccurrenceState.PlanAppend(emptyPending, batch, emptyReceipts);
        Assert.Equal("appended", append.Disposition);
        Assert.Empty(append.Issues);
    }

    [Fact]
    public void Reduce_EmptyHarmListIsHarmlessAndCreatesNoBatchOrOpportunity()
    {
        var producer = Producer("formal");

        var reduced = Reduce(producer);

        Assert.True(reduced.Success, Describe(reduced.Issues));
        Assert.Equal("harmless", reduced.Disposition);
        Assert.Null(reduced.Batch);
        Assert.Empty(reduced.Issues);
        Assert.Matches("^sha256:[0-9a-f]{64}$", producer.SourceResultFingerprint);
    }

    [Fact]
    public void RegisteredProducerSeam_ProjectsOnlyAgainstTheFinalizedResourceEffectBoundary()
    {
        var draft = new StubProducerDraft(
            "formal",
            new MortalWoundOccurrenceProducerProjectionResult(
                "producer_operation_81",
                Projection(),
                new[] { Harm(0, 0, "formal") },
                Array.Empty<ValidationIssue>()));

        var planned = AcceptedMechanicsPlanner.ReduceMortalWoundOccurrenceProducers(
            FinalizedResourceResult.Value,
            new[] { draft });

        Assert.True(planned.Success, Describe(planned.Issues));
        var batch = Assert.Single(planned.HarmfulBatches);
        Assert.Single(batch.Candidates);
        Assert.Same(FinalizedResourceResult.Value, draft.SeenResourceResult);
        Assert.Same(
            FinalizedResourceResult.Value.EffectBoundaryTranscript,
            draft.SeenTranscript);
    }

    [Fact]
    public void RegisteredProducerSeam_LeavesUnsupportedKindsUnregisteredAndFailsClosed()
    {
        var draft = new StubProducerDraft(
            "unsupported",
            new MortalWoundOccurrenceProducerProjectionResult(
                "producer_operation_81",
                Projection(),
                new[] { Harm(0, 0, "formal") },
                Array.Empty<ValidationIssue>()));

        var planned = AcceptedMechanicsPlanner.ReduceMortalWoundOccurrenceProducers(
            FinalizedResourceResult.Value,
            new[] { draft });

        Assert.False(planned.Success);
        Assert.Empty(planned.HarmfulBatches);
        Assert.Contains(
            planned.Issues,
            issue => issue.Code == "mortal_wound_producer_coordinate_invalid");
    }

    [Fact]
    public void RegisteredProducerSeam_IsTheOnlyProductionCallerOfTheReducer()
    {
        var productionRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient");
        var callSites = Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment =>
                segment is "bin" or "obj"))
            .Where(path => File.ReadAllText(path).Contains(
                "MortalWoundOccurrenceCandidateReducer.ReduceRegistered(",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path)
                .Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs" },
            callSites);

        var sealCallSites = Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment =>
                segment is "bin" or "obj"))
            .Where(path => File.ReadAllText(path).Contains(
                "MortalWoundAcceptedProducerResult.CreateRegistered(",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path)
                .Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[] { "BookOfEternityClient/Services/MortalWoundOccurrenceProducer.cs" },
            sealCallSites);

        var authorityBypasses = Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment =>
                segment is "bin" or "obj"))
            .Where(path => !path.EndsWith(
                "AcceptedMechanicsPlanner.cs",
                StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains(
                "ReduceMortalWoundOccurrenceProducers(",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path)
                .Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(authorityBypasses);

        var productionImplementors = Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment =>
                segment is "bin" or "obj"))
            .Where(path => Regex.IsMatch(
                File.ReadAllText(path),
                @":\s*IMortalWoundOccurrenceProducerDraft\b",
                RegexOptions.CultureInvariant))
            .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path)
                .Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(productionImplementors);
    }

    [Fact]
    public void Reduce_CanonicalizesCandidateOrderAndDerivesEverySelectedSeal()
    {
        var producer = Producer(
            "combat",
            Harm(1, 1, "combat"),
            Harm(0, 0, "combat"));

        var reduced = Reduce(producer);

        Assert.True(reduced.Success, Describe(reduced.Issues));
        var candidates = reduced.Batch!.Candidates;
        Assert.Equal(new[] { 0, 1 }, candidates.Select(value => value.ProducerCandidateOrdinal));
        Assert.Equal(candidates[0].AcceptedEvents, candidates[1].AcceptedEvents);
        for (var ordinal = 0; ordinal < candidates.Count; ordinal++)
        {
            var evidence = new WoundOpportunityEventEvidence(
                "combat",
                $"combat_harm_{ordinal}",
                $"authority_{ordinal}",
                "harmful",
                2,
                $"Проверяемая причина ранения {ordinal}.");
            Assert.Equal(
                WoundOpportunityEventEvidenceFingerprint.Compute(evidence),
                candidates[ordinal].AcceptedEvents[ordinal].SemanticFingerprint);
        }
    }

    [Fact]
    public void Reduce_AllowsSeveralCandidatesToShareOneIdenticalHarmfulEvent()
    {
        var producer = Producer(
            "hazard",
            Harm(1, 0, "hazard") with { ProfileKey = "hazard_burn_secondary" },
            Harm(0, 0, "hazard"));

        var reduced = Reduce(producer);

        Assert.True(reduced.Success, Describe(reduced.Issues));
        Assert.Equal(2, reduced.Batch!.Candidates.Count);
        Assert.All(
            reduced.Batch.Candidates,
            candidate => Assert.Equal(0, candidate.AcceptedEventOrdinal));
    }

    [Fact]
    public void Reduce_RejectsSeveralCandidatesThatDisagreeAboutOneEventSemantics()
    {
        var producer = Producer(
            "hazard",
            Harm(0, 0, "hazard"),
            Harm(1, 0, "hazard") with
            {
                Outcome = new MortalWoundOccurrenceOutcome(
                    "harmful",
                    3,
                    "Несовместимая причина того же события.")
            });

        var reduced = Reduce(producer);

        Assert.False(reduced.Success);
        Assert.Equal("invalid", reduced.Disposition);
        Assert.Null(reduced.Batch);
        Assert.Contains(
            reduced.Issues,
            issue => issue.Code == "mortal_wound_producer_shared_event_evidence_conflict");
    }

    [Fact]
    public void Reduce_PreservesCompleteGuaranteedTriggerEvidenceWithoutAcceptingItsAuthoritySeal()
    {
        var harm = Harm(0, 0, "formal");
        var activeSource = harm.Source with { State = "active" };
        var guarantee = new WoundGuaranteedTriggerEvidence(
            "guarantee_81",
            activeSource.Kind,
            activeSource.SourceId,
            activeSource.State,
            harm.Owner.Realm,
            harm.Domain,
            harm.Owner,
            2,
            80,
            Fingerprint('f'));
        var producer = Producer(
            "formal",
            harm with
            {
                Source = activeSource,
                MinimumSeverityRank = 2,
                GuaranteedTrigger = guarantee
            });

        var reduced = Reduce(producer);

        Assert.True(reduced.Success, Describe(reduced.Issues));
        var candidate = Assert.Single(reduced.Batch!.Candidates);
        Assert.Equal(2, candidate.MinimumSeverityRank);
        Assert.Equal(guarantee, candidate.GuaranteedTrigger);
        Assert.DoesNotContain(
            typeof(MortalWoundAcceptedHarmResult)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Fingerprint", StringComparison.Ordinal));
    }

    [Fact]
    public void ProducerFingerprintIsDeterministicAndChangesWithEverySemanticRegion()
    {
        var baseline = Producer("formal", Harm(0, 0, "formal"));
        var replay = Producer("formal", Harm(0, 0, "formal"));
        Assert.Equal(baseline.SourceResultFingerprint, replay.SourceResultFingerprint);

        var changed = new[]
        {
            Producer("qte", Harm(0, 0, "qte")),
            Producer("formal", Harm(0, 0, "formal") with { ProfileKey = "profile_changed" }),
            Producer("formal", Harm(0, 0, "formal") with
            {
                Outcome = new MortalWoundOccurrenceOutcome("harmful", 3, "Другая причина.")
            }),
            Producer("formal", Harm(0, 0, "formal") with
            {
                WorseningTarget = new MortalWoundOccurrenceWorseningTarget(
                    "wound_existing", "retrauma")
            })
        };
        Assert.All(changed, value =>
            Assert.NotEqual(baseline.SourceResultFingerprint, value.SourceResultFingerprint));
    }

    [Fact]
    public void ProducerFingerprint_ChangesForEverySealedCoordinateAndIsInputOrderInvariant()
    {
        var first = Harm(0, 0, "formal");
        var second = Harm(1, 1, "formal") with { ProfileKey = "profile_second" };
        var baseline = Producer("formal", first, second);
        var reordered = Producer("formal", second, first);
        Assert.Equal(baseline.SourceResultFingerprint, reordered.SourceResultFingerprint);

        var changed = new List<StubProducerDraft>
        {
            ProducerWith(
                "qte",
                "producer_operation_81",
                Projection(),
                first,
                second),
            ProducerWith("operation_changed", Projection(), first, second),
            ProducerWith("producer_operation_81", Projection(sessionId: "session_changed"), first, second),
            ProducerWith("producer_operation_81", Projection(requestId: "request_changed"), first, second),
            ProducerWith("producer_operation_81", Projection(snapshotToken: Fingerprint('b')), first, second),
            ProducerWith("producer_operation_81", Projection(turn: 82), first, second),
            ProducerWith("producer_operation_81", Projection(event0Ref: "event_changed"), first, second),
            ProducerWith("producer_operation_81", Projection(event0Kind: "formal_harm_changed"), first, second),
            ProducerWith("producer_operation_81", Projection(event0AuthorityId: "authority_changed"), first, second),
            ProducerWith(
                "producer_operation_81",
                Projection(events: Projection().Events.Concat(new[]
                {
                    new WoundAcceptedResponseEventCoordinate(
                        "event_81_2",
                        "formal_harm_2",
                        "authority_2")
                }).ToArray()),
                first,
                second),
            ProducerWith(
                "producer_operation_81",
                Projection(events: Projection().Events.Reverse().ToArray()),
                first,
                second),
            Producer("formal", first, second with { ProducerCandidateOrdinal = 2 }),
            Producer("formal", first with { AcceptedEventOrdinal = 1 }, second),
            Producer("formal", first with { Owner = first.Owner with { Realm = "realm_changed" } }, second),
            Producer("formal", first with { Owner = first.Owner with { OwnerKind = "owner_kind_changed" } }, second),
            Producer("formal", first with { Owner = first.Owner with { OwnerId = "player_changed" } }, second),
            Producer("formal", first with { Owner = first.Owner with { CarrierPath = "carrier_changed" } }, second),
            Producer("formal", first with { Domain = "domain_changed" }, second),
            Producer("formal", first with { Source = first.Source with { Kind = "source_kind_changed" } }, second),
            Producer("formal", first with { Source = first.Source with { SourceId = "source_changed" } }, second),
            Producer("formal", first with { Source = first.Source with { State = "source_state_changed" } }, second),
            Producer("formal", first with { Outcome = first.Outcome with { Kind = "outcome_changed" } }, second),
            Producer("formal", first with { Outcome = first.Outcome with { MaximumSeverityRank = 3 } }, second),
            Producer("formal", first with { Outcome = first.Outcome with { ReadableCause = "Другая причина." } }, second),
            Producer("formal", first with { HardMaximumSeverityRank = 3 }, second),
            Producer("formal", first with { MinimumSeverityRank = 1 }, second),
            Producer("formal", first with
            {
                SafeContext = new WoundOpportunitySafeContext(
                    "другая цель",
                    first.SafeContext.Cause,
                    first.SafeContext.AllowedLocationKinds)
            }, second),
            Producer("formal", first with
            {
                SafeContext = new WoundOpportunitySafeContext(
                    first.SafeContext.Target,
                    "другая причина",
                    first.SafeContext.AllowedLocationKinds)
            }, second),
            Producer("formal", first with
            {
                SafeContext = new WoundOpportunitySafeContext(
                    first.SafeContext.Target,
                    first.SafeContext.Cause,
                    new[] { "systemic", "anatomical", "other" })
            }, second),
            Producer("formal", first with
            {
                SafeContext = new WoundOpportunitySafeContext(
                    first.SafeContext.Target,
                    first.SafeContext.Cause,
                    new[] { "anatomical", "systemic" })
            }, second),
            Producer("formal", first with
            {
                WorseningTarget = new MortalWoundOccurrenceWorseningTarget(
                    "wound_changed",
                    "retrauma")
            }, second),
            Producer("formal", first with
            {
                WorseningTarget = new MortalWoundOccurrenceWorseningTarget(
                    "wound_existing",
                    "cause_changed")
            }, second),
            Producer("formal", first, second with { ProfileKey = "profile_sibling_changed" })
        };

        var guarantee = Guarantee(first);
        var guaranteed = Producer(
            "formal",
            first with { MinimumSeverityRank = 2, GuaranteedTrigger = guarantee },
            second);
        var guaranteeChanges = new[]
        {
            guarantee with { TriggerId = "guarantee_changed" },
            guarantee with { SourceKind = "source_kind_changed" },
            guarantee with { SourceId = "source_id_changed" },
            guarantee with { SourceState = "source_state_changed" },
            guarantee with { Realm = "realm_changed" },
            guarantee with { Domain = "domain_changed" },
            guarantee with { Owner = guarantee.Owner with { Realm = "owner_realm_changed" } },
            guarantee with { Owner = guarantee.Owner with { OwnerKind = "owner_kind_changed" } },
            guarantee with { Owner = guarantee.Owner with { OwnerId = "owner_changed" } },
            guarantee with { Owner = guarantee.Owner with { CarrierPath = "owner_carrier_changed" } },
            guarantee with { RequiredSeverityRank = 3 },
            guarantee with { MaterializedAtTurn = 79 },
            guarantee with { SourceContractFingerprint = Fingerprint('c') }
        };
        changed.AddRange(guaranteeChanges.Select(value => Producer(
            "formal",
            first with { MinimumSeverityRank = 2, GuaranteedTrigger = value },
            second)));

        Assert.All(changed, value =>
            Assert.NotEqual(baseline.SourceResultFingerprint, value.SourceResultFingerprint));
        Assert.All(guaranteeChanges, value => Assert.NotEqual(
            guaranteed.SourceResultFingerprint,
            Producer(
                "formal",
                first with { MinimumSeverityRank = 2, GuaranteedTrigger = value },
                second).SourceResultFingerprint));

        var worsening = first with
        {
            WorseningTarget = new MortalWoundOccurrenceWorseningTarget(
                "wound_existing",
                "retrauma")
        };
        var worseningBaseline = Producer("formal", worsening, second);
        Assert.NotEqual(
            worseningBaseline.SourceResultFingerprint,
            Producer("formal", worsening with
            {
                WorseningTarget = worsening.WorseningTarget! with
                {
                    WoundId = "wound_changed"
                }
            }, second).SourceResultFingerprint);
        Assert.NotEqual(
            worseningBaseline.SourceResultFingerprint,
            Producer("formal", worsening with
            {
                WorseningTarget = worsening.WorseningTarget! with
                {
                    CauseKind = "same_conflict"
                }
            }, second).SourceResultFingerprint);
    }

    [Fact]
    public void Reduce_RejectsIncompleteDuplicateMismatchedOrMalformedHarmBatches()
    {
        foreach (var producer in new[]
                 {
                     Producer("formal", Harm(1, 0, "formal")),
                     Producer("formal", Harm(0, 0, "formal"), Harm(0, 1, "formal")),
                     Producer("formal", Harm(0, 2, "formal")),
                     Producer("formal", Harm(0, 0, "formal") with
                     {
                         Outcome = new MortalWoundOccurrenceOutcome("harmless", 0, "Нет ранения.")
                     }),
                     Producer("unknown", Harm(0, 0, "unknown")),
                     Producer("formal", Harm(0, 0, "formal") with
                     {
                         Domain = "spiritual"
                     })
                 })
        {
            var reduced = Reduce(producer);
            Assert.False(reduced.Success);
            Assert.Equal("invalid", reduced.Disposition);
            Assert.Null(reduced.Batch);
            Assert.NotEmpty(reduced.Issues);
        }
    }

    [Fact]
    public void ProducerAndReductionDetachMutableInputCollections()
    {
        var harms = new[] { Harm(0, 0, "formal") };
        var producer = ProducerWithHarms("formal", harms);
        harms[0] = harms[0] with { ProfileKey = "changed" };

        var reduced = Reduce(producer);

        Assert.True(reduced.Success, Describe(reduced.Issues));
        Assert.Equal("mortal_formal_injury_v1", Assert.Single(producer.Harms).ProfileKey);
        Assert.Equal("mortal_formal_injury_v1", Assert.Single(reduced.Batch!.Candidates).ProfileKey);
    }

    private static StubProducerDraft Producer(
        string adapterKind,
        params MortalWoundAcceptedHarmResult[] harms) =>
        ProducerWithHarms(adapterKind, harms);

    private static StubProducerDraft ProducerWithHarms(
        string adapterKind,
        IReadOnlyList<MortalWoundAcceptedHarmResult> harms) => ProducerWith(
        "producer_operation_81",
        Projection(adapterKind: adapterKind),
        harms.ToArray());

    private static StubProducerDraft ProducerWith(
        string operationKey,
        WoundAcceptedResponseEventProjection projection,
        params MortalWoundAcceptedHarmResult[] harms) => ProducerWith(
        projection.Events[0].Kind.Split('_')[0],
        operationKey,
        projection,
        harms);

    private static StubProducerDraft ProducerWith(
        string adapterKind,
        string operationKey,
        WoundAcceptedResponseEventProjection projection,
        params MortalWoundAcceptedHarmResult[] harms) => new(
        adapterKind,
        new MortalWoundOccurrenceProducerProjectionResult(
            operationKey,
            projection,
            harms,
            Array.Empty<ValidationIssue>()));

    private static WoundAcceptedResponseEventProjection Projection(
        string adapterKind = "formal",
        string sessionId = "source_session_81",
        string requestId = "source_request_81",
        string? snapshotToken = null,
        int turn = 81,
        string event0Ref = "event_81_0",
        string? event0Kind = null,
        string event0AuthorityId = "authority_0",
        IReadOnlyList<WoundAcceptedResponseEventCoordinate>? events = null) =>
        new(
            sessionId,
            requestId,
            snapshotToken ?? Fingerprint('a'),
            turn,
            events ?? new[]
            {
                new WoundAcceptedResponseEventCoordinate(
                    event0Ref, event0Kind ?? $"{adapterKind}_harm_0", event0AuthorityId),
                new WoundAcceptedResponseEventCoordinate(
                    "event_81_1", $"{adapterKind}_harm_1", "authority_1")
            });

    private static WoundGuaranteedTriggerEvidence Guarantee(
        MortalWoundAcceptedHarmResult harm) => new(
        "guarantee_81",
        harm.Source.Kind,
        harm.Source.SourceId,
        harm.Source.State,
        harm.Owner.Realm,
        harm.Domain,
        harm.Owner,
        2,
        80,
        Fingerprint('f'));

    private static MortalWoundAcceptedHarmResult Harm(
        int candidateOrdinal,
        int eventOrdinal,
        string adapterKind) => new(
        candidateOrdinal,
        eventOrdinal,
        new WoundOwnerCoordinate(
            "mortal_world",
            "player",
            "player_current",
            "game_state/player/wounds.json"),
        "physical",
        "mortal_formal_injury_v1",
        new MortalWoundOccurrenceSource("combat_action", $"source_{eventOrdinal}", "accepted"),
        new MortalWoundOccurrenceOutcome(
            "harmful",
            2,
            $"Проверяемая причина ранения {eventOrdinal}."),
        4,
        null,
        null,
        new WoundOpportunitySafeContext(
            "пострадавшая часть тела",
            "причина происшествия",
            new[] { "anatomical", "systemic", "other" }),
        null);

    private static string Fingerprint(char value) => "sha256:" + new string(value, 64);

    private static AcceptedMechanicsResourcePlanningResult CreateFinalizedResourceResult()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var history = ResourceHistoryState.CreateValidated(
            Array.Empty<ResourceTransition>(),
            definitions);
        var sources = ResourceMutationSourceCatalog.Create(
            Array.Empty<ResourceMutationSourceExport>());
        Assert.True(history.IsValid, Describe(history.Issues));
        Assert.True(sources.IsValid, Describe(sources.Issues));
        var nextIdentity = 1;
        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                81,
                definitions,
                new ResourceStateLedger(Array.Empty<ResourceStateEntry>()),
                history.History!,
                sources.Catalog!,
                Array.Empty<ResourceMutationIntent>()),
            new AcceptedMechanicsIdentityFactory(() =>
                new Guid(nextIdentity++, 0, 0, new byte[8])));
        Assert.True(result.IsValid, Describe(result.Issues));
        return result;
    }

    private sealed class StubProducerDraft : IMortalWoundOccurrenceProducerDraft
    {
        private readonly MortalWoundOccurrenceProducerProjectionResult _projection;

        internal StubProducerDraft(
            string adapterKind,
            MortalWoundOccurrenceProducerProjectionResult projection)
        {
            AdapterKind = adapterKind;
            _projection = projection;
        }

        public string AdapterKind { get; }
        internal WoundAcceptedResponseEventProjection AcceptedResponse =>
            _projection.AcceptedResponse!;
        internal IReadOnlyList<MortalWoundAcceptedHarmResult> Harms =>
            _projection.Harms;
        internal string SourceResultFingerprint => SourceFingerprint(this);
        internal AcceptedMechanicsResourcePlanningResult? SeenResourceResult { get; private set; }
        internal AcceptedEffectBoundaryTranscript? SeenTranscript { get; private set; }

        public MortalWoundOccurrenceProducerProjectionResult Project(
            AcceptedMechanicsResourcePlanningResult resourceResult,
            AcceptedEffectBoundaryTranscript effectBoundaryTranscript)
        {
            SeenResourceResult = resourceResult;
            SeenTranscript = effectBoundaryTranscript;
            return _projection;
        }
    }

    private static MortalWoundOccurrenceCandidateReductionResult Reduce(
        StubProducerDraft producer)
    {
        var planned = AcceptedMechanicsPlanner.ReduceMortalWoundOccurrenceProducers(
            FinalizedResourceResult.Value,
            new[] { producer });
        return Assert.Single(planned.ProducerResults);
    }

    private static string SourceFingerprint(StubProducerDraft producer)
    {
        var reduced = Reduce(producer);
        Assert.Matches("^sha256:[0-9a-f]{64}$", reduced.SourceResultFingerprint);
        return reduced.SourceResultFingerprint;
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(issue =>
            $"{issue.Code}: {issue.FilePath}: {issue.Actual}"));

    private static Type RequiredType(string name) =>
        typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services." + name,
            throwOnError: false,
            ignoreCase: false) ??
        throw new Xunit.Sdk.XunitException($"T064 requires {name}.");
}
