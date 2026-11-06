using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Exercises ordinary13/II to actual23/IV through one real cold offer, none/C3, common C4 and signed47.</summary>
    [Fact]
    public async Task SpiritualPressurePair_OrdinaryTwoActualFourPublishesWithColdNoneAndSignedNext()
        => await WithPressurePairPublicationAsync(new("pair_boundary_publication", AdvanceExpected: true), async setup =>
        {
            var ordinary = new SpiritualWoundCalculation(
                new(8, 0, 0, "clear", "fractured", "hostile", 4), 0, 2, 1, 13, 2, 2, 4, true, 2);
            var completed = await CompletePressurePairColdC3Async(setup, setup.Copy.Context.Validator, ordinary);
            await PublishPressurePairAndReadNextAsync(setup, setup.Copy.Context.Validator, ordinary, completed);
        });

    /// <summary>Independently exercises ordinary18/III plus two, capped to actual23/IV, without any IV wound draft.</summary>
    [Fact]
    public async Task SpiritualPressurePair_OrdinaryThreeCappedActualFourPublishesWithColdNoneAndSignedNext()
        => await WithPressurePairPublicationAsync(new("pair_overflow_publication", After: "overwhelmed", AdvanceExpected: true), async setup =>
        {
            var ordinary = new SpiritualWoundCalculation(
                new(8, 0, 0, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 18, 3, 3, 4, true, 3);
            var completed = await CompletePressurePairColdC3Async(setup, setup.Copy.Context.Validator, ordinary);
            await PublishPressurePairAndReadNextAsync(setup, setup.Copy.Context.Validator, ordinary, completed);
        });

    /// <summary>Per-Fact root and immutable signed/authored DATA; no execution lease or active capture crosses a phase.</summary>
    private sealed record PressurePairPublicationSetup(PressureGroupCase Spec, PressureOrdinaryOwnerCopy Copy,
        PressurePairSignedOrigin Origin, IReadOnlyList<string> Parents, IReadOnlyDictionary<string, byte[]?> Authored);

    /// <summary>Exact physical signed rollback origin, captured before any ordinary conflict/output writes.</summary>
    private sealed record PressurePairSignedOrigin(IReadOnlyDictionary<string, byte[]> Files,
        IReadOnlyDictionary<string, byte[]?> Tracked, byte[] Generation, string ReceiptJson, string IdentityJson);

    /// <summary>Detached completed DATA only; publication still requires its independently registered current authority.</summary>
    private sealed record PressurePairCompletedData(string ActualRootJson, string WitnessJson, string ReceiptJson,
        AcceptedMechanicsResourcePlanningResult Resources, string EffectPayloadFingerprint);

    /// <summary>Allocates independent mutable state, closes setup before execution, and owns cleanup even if allocation/setup fails.</summary>
    private async Task WithPressurePairPublicationAsync(PressureGroupCase spec,
        Func<PressurePairPublicationSetup, Task> work, FileSystemManagerHooks? hooks = null)
    {
        ResourceMaterializationTestContext? context = null;
        Assert.Equal(2, spec.Parents);
        var data = SpiritualPressureGroupOwnerCaseData.Create(2, spec.SelectedCost, spec.OtherCost,
            spec.PressureRoll, spec.Ranks, rankFourPressureArt: spec.RankFourPressureArt);
        var parents = Array.AsReadOnly(data.Parents.OrderBy(row => row["woundId"]!.GetValue<string>(), StringComparer.Ordinal)
            .Select(row => row.ToJsonString()).ToArray());
        try
        {
            var started = Stopwatch.GetTimestamp();
            context = await CreatePressureGroupSignedContextAsync(spec, data, allocated => context = allocated, hooks);
            WritePressureOwnerTiming("pair_publication_signed_setup", spec.Name, started);
            PressurePairPublicationSetup setup;
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                var signed = await AssertPressureGroupSignedInputsAsync(context, lease, spec, data);
                var origin = await ReadPressurePairSignedOriginAsync(context, lease);
                var copy = await AuthorPressureGroupOrdinaryAsync(context, lease, spec, signed);
                var authored = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
                foreach (var path in PressureAuthoredDraftPaths)
                    authored[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
                Assert.NotEqual(origin.Tracked[AfterlifeSpiritualConflictState.StatePath],
                    authored[AfterlifeSpiritualConflictState.StatePath]);
                setup = new(spec, copy, origin, parents, authored);
            }
            await work(setup);
        }
        finally
        {
            if (context is not null)
            {
                var started = Stopwatch.GetTimestamp();
                try { await context.DisposeAsync(); }
                finally
                {
                    WritePressureOwnerTiming("pair_publication_owned_cleanup", spec.Name, started);
                    Assert.False(Directory.Exists(context.RootPath));
                }
            }
        }
    }

    /// <summary>Captures all signed original bytes and explicit tracked absences, never post-author window.Files.</summary>
    private static async Task<PressurePairSignedOrigin> ReadPressurePairSignedOriginAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease)
    {
        var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        var tracked = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[]
                 { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                     AcceptedMechanicsPlan.WoundCommandPath, SpiritualWoundOpportunityReceiptState.StatePath,
                     ProjectionNarrativePath, ProjectionInterfacePath }).Distinct(StringComparer.Ordinal))
            tracked[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
        var active = JsonNode.Parse(tracked[AfterlifeSpiritualConflictState.StatePath]!)!["activeConflict"]!;
        Assert.Empty(active["exchangeLog"]!.AsArray());
        Assert.Equal("clear", active["playerSideStrain"]!.GetValue<string>());
        var balance = AfterlifeConflictActionPointProjectionService.Resolve(
            (await context.FileSystem.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath))!,
            (await context.FileSystem.ReadFileAsync(lease, ResourceMaterializationContract.StatePath))!, active.AsObject());
        AssertNoConflictFrameErrors(balance.Issues);
        Assert.Equal((3m, 10m, 3m, 9m), (balance.Projection!.Player.Current, balance.Projection.Player.Maximum,
            balance.Projection.Opposition.Current, balance.Projection.Opposition.Maximum));
        AssertPressureNoPrivatePair(context, lease);
        var receipts = JsonNode.Parse(tracked[SpiritualWoundOpportunityReceiptState.StatePath]!)!;
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Assert.Equal(2, receipts[field]!.AsArray().Count);
        return new(files, tracked, File.ReadAllBytes(context.FileSystem.SessionGenerationPath), receipts.ToJsonString(),
            JsonNode.Parse(tracked[EffectIdentityState.StatePath]!)!.ToJsonString());
    }

    /// <summary>Owns the entire warm C1, first C2, one explicit cold Open, none and genuine C3 phase under one fresh lease.</summary>
    private async Task<PressurePairCompletedData> CompletePressurePairColdC3Async(PressurePairPublicationSetup setup,
        ValidationService validator, SpiritualWoundCalculation ordinary,
        Action<ValidationService.SpiritualOriginalTurnCapture, ValidationService.SpiritualC2PrivateSession,
            ValidationService.SpiritualC2PrivateSession>? retainDisposedForRevocationCheck = null)
    {
        var context = setup.Copy.Context;
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        using var warm = await BeginPressureOrdinaryOwnerAsync(setup.Copy, lease, setup.Spec.Name, validator);
        // Genuine prefix closure may execute lawful owner_turn_end work. Only now establish inertness witnesses.
        var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
        var frontier = await ReadPressureOwnerSnapshotAsync(warm, lease, setup.Spec.Name + "_publication_before");
        var window = new PressureGroupWindow(setup.Spec, setup.Copy, lease, warm, files, generation, frontier)
            { ExpectedParentJson = setup.Parents };
        AssertPressureGroupFrontier(setup.Spec, setup.Copy, warm, lease, frontier);
        await AssertPressurePairActualAdmissionAsync(window, ordinary,
            "The independently signed pair must admit its exact actual23/IV exchange before publication.");
        // Repeating the existing admission returns its real interval/source, never a reconstructed capability.
        var admitted = await warm.AdmitLatestClosedWoundSourceAsync(lease, "player");
        AssertNoConflictFrameErrors(admitted.Issues);
        var admission = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission>(admitted.Admission);
        Assert.True(warm.OwnsWoundAdmission(admission));
        var opportunityResult = await warm.ReadWoundOpportunityAsync(lease, admission);
        AssertNoConflictFrameErrors(opportunityResult.Issues);
        Assert.Null(opportunityResult.Satisfaction);
        var opportunity = Assert.IsType<WoundOpportunityAuthority>(opportunityResult.Opportunity);
        Assert.Null(opportunity.WorseningTarget);
        Assert.Null(opportunity.RequiredSeverityRank);
        var interval = admission.Interval;
        var source = admission.Source;
        var prefix = warm.ReadClosedResourceCandidatePrefix(lease, interval);
        var actualRoot = setup.Copy.Conflict.DeepClone().AsObject();
        actualRoot["activeConflict"]!["exchangeLog"]![0] = ReadPressurePairAdmissionExchange("broken");
        actualRoot["activeConflict"]!["playerSideStrain"] = "broken";
        Assert.True(JsonNode.DeepEquals(actualRoot, prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        var commitStarted = Stopwatch.GetTimestamp();
        var committed = await warm.CommitC2FirstTransportAsync(lease, interval);
        var expectedCold = ReadExpectedSpiritualColdOfferData(warm, lease, committed, source);
        Assert.Equal(setup.Authored[AfterlifeSpiritualConflictState.StatePath], Convert.FromBase64String(expectedCold.OriginalConflictBytesBase64));
        var witness = Assert.Single(JsonNode.Parse(expectedCold.ReconstructedPendingJson)!["pending"]!["sources"]!.AsArray())!.AsObject();
        AssertPressurePairPublicationWitness(setup, ordinary, witness);
        warm.Dispose();
        Assert.False(warm.IsCurrentOwner);
        WritePressureOwnerTiming("pair_publication_first_c2_commit", setup.Spec.Name, commitStarted);

        var coldStarted = Stopwatch.GetTimestamp();
        var coldValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await coldValidator.OpenC2PrivateSessionAsync(lease); // Exactly one explicit cold Open per attempt.
        using var openedSession = opened.Session;
        AssertNoConflictFrameErrors(opened.Issues);
        Assert.Equal("offer", opened.Disposition);
        var choice = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(openedSession);
        var offer = Assert.IsType<ValidationService.SpiritualC2PrivateOffer>(choice.Offer);
        Assert.Equal(opportunity.PublicRef, offer.OpportunityRef);
        Assert.Equal((1, 4), (offer.MinimumSeverityRank, offer.MaximumSeverityRank));
        Assert.Null(offer.RequiredSeverityRank);
        Assert.Equal(new[] { "none", "materialize" }, offer.AllowedDecisions);
        var coldFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        var coldGeneration = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
        WritePressureOwnerTiming("pair_publication_single_cold_open", setup.Spec.Name, coldStarted);
        var observationStarted = Stopwatch.GetTimestamp();
        var observed = await choice.ReadOfferReplayEvidenceAsync(lease);
        AssertSpiritualColdOfferEvidence(expectedCold, observed);
        AssertExistingPressureInputInert(context, lease, coldFiles, coldGeneration);
        WritePressureOwnerTiming("pair_publication_six_field_observation", setup.Spec.Name, observationStarted);

        var c3Started = Stopwatch.GetTimestamp();
        var declined = await choice.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
            { opportunityRef = offer.OpportunityRef, decision = "none" }), null);
        using var declinedSession = declined.Session;
        AssertNoConflictFrameErrors(declined.Issues);
        Assert.Equal("completed_unpublished", declined.Disposition);
        var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(declinedSession);
        Assert.Null(completed.Offer);
        var pending = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, SpiritualWoundDecisionPendingState.StatePath))!)!["pending"]!;
        Assert.Equal(1, pending["cursor"]!["nextSourceOrdinal"]!.GetValue<int>());
        var staged = Assert.Single(pending["stagedDecisions"]!.AsArray())!;
        Assert.Equal((offer.OpportunityRef, witness["sourceId"]!.GetValue<string>(), "none"),
            (staged["opportunityRef"]!.GetValue<string>(), staged["sourceId"]!.GetValue<string>(), staged["decision"]!.GetValue<string>()));
        foreach (var field in new[] { "selectedSeverityRank", "woundDraftBase64", "woundDraftFingerprint" }) Assert.Null(staged[field]);
        var reduced = await completed.ReduceCompletedDeclinesAsync(lease);
        AssertNoConflictFrameErrors(reduced.Issues);
        Assert.True(reduced.Success);
        Assert.NotNull(reduced.ReceiptProof);
        var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(reduced.Reduction);
        Assert.True(reduction.RequiresSatisfactionReceipt);
        Assert.True(reduction.HasLiveDecisionReceipt); // Set only after the capture-issued proof matches this exact reduction.
        Assert.False(reduction.HasLiveWoundWork);
        Assert.Null(reduction.LiveWoundCompletion);
        Assert.False(reduced.ReceiptProof!.Matches(null, null,
            new CanonicalBeforeImage(true, setup.Origin.Tracked[SpiritualWoundOpportunityReceiptState.StatePath]), reduced.ReceiptAfterImage!));
        Assert.True(JsonNode.DeepEquals(actualRoot, reduction.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson), JsonNode.Parse(reduction.Resources.StateAfterImage!.ToCanonicalJson())));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson), JsonNode.Parse(reduction.Resources.HistoryAfterImage!.ToCanonicalJson())));
        Assert.Equal(prefix.AppliedTransitions, reduction.Resources.AppliedTransitions);
        Assert.Empty(reduction.Resources.ReplayTransitions);
        Assert.Equal(interval.Events, reduction.Resources.Events);
        var effects = Assert.IsType<EffectAcceptedTurnPlan>(reduction.Effects);
        Assert.True(effects.IsAcceptedBoundaryComplete);
        Assert.False(effects.HasPressureConflictCarrierCollision());
        AssertPressurePairCompletedParents(window, effects, reduction.Resources);
        Assert.Equal(offer.OpportunityRef, reduced.ReceiptAfterImage!["decisions"]![2]!["opportunityRef"]!.GetValue<string>());
        AssertPressurePairReceiptAppend(setup, reduced.ReceiptAfterImage!, witness);
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            Assert.Equal(setup.Origin.Tracked[path], await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.Equal(setup.Authored[AfterlifeSpiritualConflictState.StatePath],
            await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
        AssertPressurePrivateTransportOnly(context, files, generation);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        var result = new PressurePairCompletedData(actualRoot.ToJsonString(), witness.ToJsonString(),
            reduced.ReceiptAfterImage!.ToJsonString(), reduction.Resources,
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects));
        choice.Dispose();
        completed.Dispose();
        await AssertPressurePairRetiredOwnersAsync(warm, choice, completed, lease);
        retainDisposedForRevocationCheck?.Invoke(warm, choice, completed);
        WritePressureOwnerTiming("pair_publication_none_and_c3", setup.Spec.Name, c3Started);
        return result;
    }

    /// <summary>Validates full persisted v4 DATA independently, including both complete parent anchors and the literal cap calculation.</summary>
    private static void AssertPressurePairPublicationWitness(PressurePairPublicationSetup setup,
        SpiritualWoundCalculation ordinary, JsonObject witness)
    {
        var actual = new SpiritualWoundCalculation(
            new(8, 0, 0, "clear", "broken", "hostile", 4), 0, 4, 3, 23, 4, 4, 4, true, 4);
        Assert.Equal(actual, SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var strain));
        Assert.NotNull(strain);
        Assert.Equal(4, strain.Version);
        Assert.Equal(ordinary, strain.OrdinaryCalculation);
        Assert.Equal(actual, strain.ActualCalculation);
        Assert.Equal("/activeConflict/exchangeLog/0", strain.OriginalExchangePointer);
        Assert.Equal(new CanonicalBeforeImage(true, setup.Authored[AfterlifeSpiritualConflictState.StatePath]).Fingerprint,
            strain.OriginalInputFingerprint);
        Assert.True(JsonNode.DeepEquals(ReadPressurePairAdmissionExchange("broken"), strain.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(ReadPressurePairAdmissionExchange(setup.Spec.After)["after"],
            strain.ReadOrdinaryAfter(ReadPressurePairAdmissionExchange("broken"))));
        Assert.Equal(2, strain.Parents.Count);
        for (var index = 0; index < strain.Parents.Count; index++)
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(setup.Parents[index]),
                JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(strain.Parents[index].ParentWound))));
        Assert.Null(witness["guaranteedSeverityRank"]);
        Assert.Null(witness["retraumaWoundId"]);
        Assert.Equal((46, 0, 0, "activeConflict.exchangeLog[0]"), (witness["turnEvidence"]!["turn"]!.GetValue<int>(),
            witness["exchangeOrdinal"]!.GetValue<int>(), witness["sourceOrdinal"]!.GetValue<int>(), witness["coordinate"]!.GetValue<string>()));
    }

    /// <summary>Checks exact old lifecycle/source/decision prefixes plus one actual-v4 none decision and all-parent History joins.</summary>
    private static void AssertPressurePairReceiptAppend(PressurePairPublicationSetup setup, JsonObject receipt, JsonObject witness)
    {
        var before = JsonNode.Parse(setup.Origin.ReceiptJson)!;
        foreach (var field in new[] { "sources", "decisions" })
        {
            Assert.Equal(2, before[field]!.AsArray().Count);
            Assert.Equal(3, receipt[field]!.AsArray().Count);
            for (var index = 0; index < 2; index++) Assert.True(JsonNode.DeepEquals(before[field]![index], receipt[field]![index]));
        }
        foreach (var field in new[] { "instances", "closures" })
        {
            for (var index = 0; index < before[field]!.AsArray().Count; index++)
                Assert.True(JsonNode.DeepEquals(before[field]![index], receipt[field]![index]));
        }
        Assert.Equal(before["closures"]!.AsArray().Count, receipt["closures"]!.AsArray().Count);
        Assert.Equal(before["instances"]!.AsArray().Count + 1, receipt["instances"]!.AsArray().Count);
        Assert.True(JsonNode.DeepEquals(witness, receipt["sources"]![2]!["witness"]));
        var decision = receipt["decisions"]![2]!;
        Assert.Equal("none", decision["decision"]!.GetValue<string>());
        Assert.Equal(witness["sourceId"]!.GetValue<string>(), decision["sourceId"]!.GetValue<string>());
        Assert.Null(decision["woundId"]);
        Assert.Null(decision["selectedSeverityRank"]);
        var history = WoundHistoryState.Parse(System.Text.Encoding.UTF8.GetString(setup.Origin.Tracked[WoundHistoryState.HistoryPath]!), WoundHistoryState.HistoryPath);
        var ledger = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.True(ledger.IsValid, string.Join("\n", ledger.Issues));
        Assert.True(ledger.State!.HasExactStrainHistory(history.State!));
    }

    /// <summary>Requires disposed warm/choice/completion handles to stay terminal without reviving their physical pair.</summary>
    private static async Task AssertPressurePairRetiredOwnersAsync(ValidationService.SpiritualOriginalTurnCapture warm,
        ValidationService.SpiritualC2PrivateSession choice, ValidationService.SpiritualC2PrivateSession completed,
        FileSystemManager.CanonicalWriteLease lease)
    {
        Assert.False(warm.IsCurrentOwner);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await warm.ReadExistingPressureOrdinaryProbeStateAsync(lease));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await choice.ReadOfferReplayEvidenceAsync(lease));
        var unavailable = await completed.ReduceCompletedDeclinesAsync(lease);
        Assert.False(unavailable.Success);
        Assert.Null(unavailable.ReceiptProof);
        Assert.Equal("spiritual_c3_decline_session_unavailable", Assert.Single(unavailable.Issues).Code);
    }

    /// <summary>Uses the common assembler/publication route only after every C1–C3 owner and its lease have been disposed.</summary>
    private async Task PublishPressurePairAndReadNextAsync(PressurePairPublicationSetup setup, ValidationService validator,
        SpiritualWoundCalculation ordinary, PressurePairCompletedData completed)
    {
        var context = setup.Copy.Context;
        var started = Stopwatch.GetTimestamp();
        AssertNoConflictFrameErrors(await validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var plan = await PeekPlanAsync(context);
        await AssertPressurePairRegisteredPlanAsync(setup, completed, plan);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.Same(plan, published.MechanicsPlan);
        await AssertPressurePairPublishedAsync(setup, ordinary, completed, plan);
        WritePressureOwnerTiming("pair_publication_common_c4", setup.Spec.Name, started);
        await ReadPressurePairSignedNextAsync(setup, validator);
    }

    /// <summary>Checks full completed actual resources, effects and receipt DATA against the one registered common plan.</summary>
    private static Task AssertPressurePairRegisteredPlanAsync(PressurePairPublicationSetup setup,
        PressurePairCompletedData completed, AcceptedMechanicsPlan plan)
    {
        var actualRoot = JsonNode.Parse(completed.ActualRootJson)!.AsObject();
        AssertPressureExistingResourcePlan(plan, actualRoot);
        Assert.True(JsonNode.DeepEquals(actualRoot, plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.ReceiptJson), plan.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.Resources.StateAfterImage!.ToCanonicalJson()), plan.StateAfterImage));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.Resources.HistoryAfterImage!.ToCanonicalJson()), plan.HistoryAfterImage));
        // The common plan canonicalizes reducer events; compare every complete record in that order.
        Assert.Equal(completed.Resources.Events.OrderBy(static row => row.Turn)
            .ThenBy(static row => row.ExecutionSequence)
            .ThenBy(static row => row.EventKind, StringComparer.Ordinal)
            .ThenBy(static row => row.OperationId, StringComparer.Ordinal), plan.ResourceEvents);
        Assert.Equal(completed.EffectPayloadFingerprint,
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan)));
        Assert.False(plan.EffectPlan!.HasPressureConflictCarrierCollision());
        return Task.CompletedTask;
    }

    /// <summary>Reads every published after-image and proves unchanged parents/History plus one lawful owner-turn-end identity append per root.</summary>
    private static async Task AssertPressurePairPublishedAsync(PressurePairPublicationSetup setup,
        SpiritualWoundCalculation ordinary, PressurePairCompletedData completed, AcceptedMechanicsPlan plan)
    {
        var context = setup.Copy.Context;
        foreach (var row in plan.WoundCarrierAfterImages.Concat(plan.EffectCarrierAfterImages).Concat(plan.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(row.Value, await context.ReadJsonAsync(row.Key)), row.Key);
        Assert.True(JsonNode.DeepEquals(plan.StateAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(plan.HistoryAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        Assert.True(JsonNode.DeepEquals(plan.EffectIdentityAfterImage, await context.ReadJsonAsync(EffectIdentityState.StatePath)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.ActualRootJson), await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath)));
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            Assert.Equal(setup.Origin.Tracked[path], await context.FileSystem.ReadFileBytesAsync(path));
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))![AfterlifeEntityProfileState.ProfilesProperty]!.AsArray();
        var player = Assert.Single(profiles, row => row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
        Assert.Equal(2, player["activeWounds"]!.AsArray().Count);
        var roots = new List<string>();
        foreach (var parentJson in setup.Parents)
        {
            var parent = JsonNode.Parse(parentJson)!;
            Assert.True(JsonNode.DeepEquals(parent, Assert.Single(player["activeWounds"]!.AsArray(), row => row!["woundId"]!.GetValue<string>() == parent["woundId"]!.GetValue<string>())));
            roots.Add(parent["consequences"]!["ownedEffectSources"]!["rootBindings"]![0]!["effectId"]!.GetValue<string>());
        }
        AssertPressureGuardRecoveryIdentityAppend(JsonNode.Parse(setup.Origin.IdentityJson)!.AsObject(),
            (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.AsObject(), roots, completed.Resources, plan, turn: 46);
        var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.AsObject();
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.ReceiptJson), receipt));
        AssertPressurePairReceiptAppend(setup, receipt, JsonNode.Parse(completed.WitnessJson)!.AsObject());
        AssertPressurePairPublicationWitness(setup, ordinary, receipt["sources"]![2]!["witness"]!.AsObject());
        var mechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
        Assert.True(mechanics.IsAccepted, string.Join("\n", mechanics.Issues));
        foreach (var root in roots) Assert.Contains(mechanics.Components, row => row.EffectId == root);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        AssertPressureNoPrivatePair(context, lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>Signs the genuinely published v4 state as turn47 and reads the active signed frame without driving a new action.</summary>
    private async Task ReadPressurePairSignedNextAsync(PressurePairPublicationSetup setup, ValidationService validator)
    {
        var context = setup.Copy.Context;
        var started = Stopwatch.GetTimestamp();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 47, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5],
            requestId: "request_" + setup.Spec.Name + "_published_read_47");
        var manifest = (await context.ReadJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!;
        Assert.Equal(47, manifest["turnNumber"]!.GetValue<int>());
        foreach (var path in PressureOrdinarySignedPaths)
            Assert.Equal(await context.FileSystem.ReadFileBytesAsync(path),
                await context.FileSystem.ReadFileBytesAsync(manifest["files"]![path]!.GetValue<string>()));
        AssertNoConflictFrameErrors(await validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState)));
        WritePressureOwnerTiming("pair_publication_signed47_read", setup.Spec.Name, started);
    }
}
