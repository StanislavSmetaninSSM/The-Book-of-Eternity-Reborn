using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class TreatmentSemanticDataTests
{
    /// <summary>Shares the full expected vector after the scalar consumer has computed it once.</summary>
    [Fact]
    public void PreparationProjection_ScalarFirstAndEightReadsUseOneActualSource()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var input = new OperationStructuralSnapshot(["actual fixed input"], [fixture.Resolution]);
        var sources = 0;
        ExpectedPreparationReplayProjection Source() { sources++; return LiteralProjection(); }
        var first = fixture.Data.ReadPreparationProjection(operation, input, Source);
        Assert.NotNull(first);
        Assert.Equal("preparation", first.WoundPreparationFingerprint);
        for (var i = 0; i < 8; i++)
            Assert.Equal(LiteralProjection().ExportValues(), fixture.Data.ReadPreparationProjection(operation, input, Source)!.ExportValues());
        Assert.Equal(1, sources);
    }

    /// <summary>Copies the ordered pairs at construction and on export without retaining a mutable plan.</summary>
    [Fact]
    public void PreparationProjection_DetachesOrderedPairExports()
    {
        var pairs = new[] { ("source-one", "seal-one"), ("source-two", "seal-two") };
        var value = new ExpectedPreparationReplayProjection("binding", "input", "preparation", "baseline-input", "baseline", pairs);
        pairs[0] = ("changed", "changed");
        var first = value.ExportValues();
        Assert.Equal(new[] { "binding", "input", "preparation", "baseline-input", "baseline", "2", "source-one", "seal-one", "source-two", "seal-two" }, first);
        first[0] = "caller changed";
        Assert.Equal("binding", value.ExportValues()[0]);
    }

    /// <summary>Separates actual ordered input DATA and real semantic owners inside one operation.</summary>
    [Fact]
    public void PreparationProjection_ChangedInputAndForeignOwnerDoNotHit()
    {
        using var first = new Fixture(_output);
        using var second = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var sources = 0;
        ExpectedPreparationReplayProjection Source() { sources++; return LiteralProjection(); }
        var a = new OperationStructuralSnapshot(["A", null, ""], [first.Resolution]);
        var b = new OperationStructuralSnapshot(["A", "", null], [first.Resolution]);
        Assert.NotNull(first.Data.ReadPreparationProjection(operation, a, Source));
        Assert.NotNull(first.Data.ReadPreparationProjection(operation, b, Source));
        Assert.NotNull(second.Data.ReadPreparationProjection(operation, a, Source));
        Assert.Equal(3, sources);
    }

    /// <summary>Invalidates changed and restored live hidden companions instead of reviving an old result.</summary>
    [Fact]
    public void PreparationProjection_LivePrivateMutationAndRestoreRecompute()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var input = new OperationStructuralSnapshot(["same input"], [fixture.Resolution]);
        var sources = 0;
        ExpectedPreparationReplayProjection Source() { sources++; return LiteralProjection(); }
        Assert.NotNull(fixture.Data.ReadPreparationProjection(operation, input, Source));
        fixture.DefinitionFacts.Add(new("changed", "{}", [], false));
        Assert.NotNull(fixture.Data.ReadPreparationProjection(operation, input, Source));
        fixture.DefinitionFacts.RemoveAt(fixture.DefinitionFacts.Count - 1);
        Assert.NotNull(fixture.Data.ReadPreparationProjection(operation, input, Source));
        Assert.Equal(3, sources);
    }

    /// <summary>Does not retain original refusal or replace the exact primary source exception.</summary>
    [Fact]
    public void PreparationProjection_RefusalAndThrowAreNotRetained()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var input = new OperationStructuralSnapshot(["same input"], []);
        var sources = 0;
        ExpectedPreparationReplayProjection? Source() { sources++; return null; }
        Assert.Null(fixture.Data.ReadPreparationProjection(operation, input, Source));
        Assert.Null(fixture.Data.ReadPreparationProjection(operation, input, Source));
        Assert.Equal(2, sources);
        Assert.Equal(0, operation.CaptureState().Entries);
        var primary = new FormatException("original source order");
        Assert.Same(primary, Assert.Throws<FormatException>(() => fixture.Data.ReadPreparationProjection(operation, input, () => throw primary)));
    }

    /// <summary>Clears warmed DATA for fallback and returns the original result or exact source error.</summary>
    [Fact]
    public void PreparationProjection_FallbackIsFreshAndPreservesPrimary()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var input = new OperationStructuralSnapshot(["same input"], []);
        Assert.NotNull(fixture.Data.ReadPreparationProjection(operation, input, LiteralProjection));
        var sources = 0;
        var result = MortalWoundTreatmentSemanticData.ReadPreparationProjectionFallback(operation,
            () => { sources++; return new ExpectedPreparationReplayProjection("original", "input", "preparation", "baseline-input", "baseline", []); });
        Assert.Equal("original", result!.BindingFingerprint);
        Assert.Equal(1, sources);
        Assert.Equal(0, operation.CaptureState().Entries);
        var primary = new InvalidDataException("original earlier graph failure");
        Assert.Same(primary, Assert.Throws<InvalidDataException>(() =>
            MortalWoundTreatmentSemanticData.ReadPreparationProjectionFallback(operation, () => throw primary)));
    }

    /// <summary>Uses the unchanged source without an owner and respects explicit fresh and closed operation boundaries.</summary>
    [Fact]
    public void PreparationProjection_NullFreshAndClosedOperationKeepTheirBoundaries()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var input = new OperationStructuralSnapshot(["same input"], []);
        var sources = 0;
        ExpectedPreparationReplayProjection Source() { sources++; return LiteralProjection(); }
        Assert.NotNull(fixture.Data.ReadPreparationProjection(null, input, Source));
        Assert.NotNull(fixture.Data.ReadPreparationProjection(null, input, Source));
        Assert.Equal(2, sources);
        Assert.NotNull(fixture.Data.ReadPreparationProjection(operation, input, Source));
        using (operation.ForceFresh())
        {
            Assert.NotNull(fixture.Data.ReadPreparationProjection(operation, input, Source));
            Assert.NotNull(fixture.Data.ReadPreparationProjection(operation, input, Source));
        }
        Assert.NotNull(fixture.Data.ReadPreparationProjection(operation, input, Source));
        Assert.Equal(6, sources);
        operation.Dispose();
        Assert.Throws<ObjectDisposedException>(() => fixture.Data.ReadPreparationProjection(operation, input, Source));
        Assert.Equal(6, sources);
    }

    /// <summary>Does not allow source-time private mutation to become a retained successful result.</summary>
    [Fact]
    public void PreparationProjection_SourceMutationRefusesCurrentResult()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var input = new OperationStructuralSnapshot(["same input"], []);
        Assert.ThrowsAny<InvalidOperationException>(() => fixture.Data.ReadPreparationProjection(operation, input, () =>
        {
            fixture.Symptoms.Add("changed while computing");
            return LiteralProjection();
        }));
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>Distinguishes hidden typed fields even when the original public fingerprint remains identical.</summary>
    [Fact]
    public void PreparationProjection_InputCaptureIncludesCanonicalOmissions()
    {
        using var fixture = new Fixture(_output);
        var wound = fixture.Outcome.Before;
        var original = ProjectionInput(wound);
        var snapshot = WoundAcceptedTurnFingerprints.CapturePreparationReplayInput(original);
        Assert.NotNull(snapshot);
        var route = wound.Treatment.Routes[0];
        var changed = wound with { Treatment = wound.Treatment with { Routes = wound.Treatment.Routes.Select((value, i) => i == 0 ? route with { SourcePath = route.SourcePath + ".changed" } : value).ToArray() } };
        var altered = ProjectionInput(changed);
        Assert.Equal(WoundAcceptedTurnFingerprints.ComputeInput(original), WoundAcceptedTurnFingerprints.ComputeInput(altered));
        Assert.False(snapshot.Matches(WoundAcceptedTurnFingerprints.CapturePreparationReplayInput(altered)!));
        var facts = wound.Consequences.OwnedEffectSources.DefinitionFacts.Append(new WoundOwnedEffectDefinitionFact("extra", "{}", [], false)).ToArray();
        changed = wound with { Consequences = wound.Consequences with { OwnedEffectSources = wound.Consequences.OwnedEffectSources with { DefinitionFacts = facts } } };
        altered = ProjectionInput(changed);
        Assert.Equal(WoundAcceptedTurnFingerprints.ComputeInput(original), WoundAcceptedTurnFingerprints.ComputeInput(altered));
        Assert.False(snapshot.Matches(WoundAcceptedTurnFingerprints.CapturePreparationReplayInput(altered)!));
        Assert.True(snapshot.Matches(WoundAcceptedTurnFingerprints.CapturePreparationReplayInput(WoundAcceptedTurnData.CloneInput(original)!)!));
    }

    /// <summary>Checks each scalar independently against actual detached plan DATA, without validating authority.</summary>
    /// <param name="index">The changed scalar in the five-field contract.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PreparationProjection_EachActualScalarIsCompared(int index)
    {
        var values = new[] { "binding", "input", "preparation", "baseline-input", "baseline" };
        var projection = new ExpectedPreparationReplayProjection(values[0], values[1], values[2], values[3], values[4], []);
        WoundPreparedAcceptedTurnPlan Plan() => new(new("session", "request", "snapshot", "mortal_world", 43, [], "events"),
            values[0], values[1], values[2], [], [], [], [],
            new(values[3], new(null, null, null, null, null), new JsonObject(), new JsonObject(), values[4]));
        Assert.True(projection.AgreesWith(Plan()));
        values[index] += ".changed";
        Assert.False(projection.AgreesWith(Plan()));
    }

    /// <summary>Does not retain cancellation as a successful computed value.</summary>
    [Fact]
    public void PreparationProjection_CancelledSourceKeepsPrimaryAndClearsData()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var input = new OperationStructuralSnapshot(["fixed"], []);
        var primary = new OperationCanceledException("actual source cancellation");
        Assert.Same(primary, Assert.Throws<OperationCanceledException>(() =>
            fixture.Data.ReadPreparationProjection(operation, input, () => throw primary)));
        Assert.Equal(0, operation.CaptureState().Entries);
        Assert.NotNull(fixture.Data.ReadPreparationProjection(operation, input, LiteralProjection));
    }

    /// <summary>Rejects overlapping serial flows and joins the real blocked source before fixture closing.</summary>
    /// <returns>Completion after the originally owned source task has settled.</returns>
    [Fact]
    public async Task PreparationProjection_OverlappingFlowCannotImportAResult()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var input = new OperationStructuralSnapshot(["fixed"], []);
        var source = Task.Run(() => Record.Exception(() => fixture.Data.ReadPreparationProjection(operation, input, () =>
        {
            entered.SetResult(); release.Wait(); return LiteralProjection();
        })));
        try
        {
            await entered.Task;
            Assert.ThrowsAny<InvalidOperationException>(() => fixture.Data.ReadPreparationProjection(operation, input, LiteralProjection));
        }
        finally
        {
            release.Set();
            var originalError = await source;
            Assert.IsAssignableFrom<InvalidOperationException>(originalError);
        }
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>Supports neither cached revival after fallback failure nor swallowed closed-owner protocol errors.</summary>
    [Fact]
    public void PreparationProjection_FallbackFailureThenRestoreNeedsNewSource()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var input = new OperationStructuralSnapshot(["fixed"], []);
        var sources = 0;
        ExpectedPreparationReplayProjection Source() { sources++; return LiteralProjection(); }
        Assert.NotNull(fixture.Data.ReadPreparationProjection(operation, input, Source));
        Assert.Null(MortalWoundTreatmentSemanticData.ReadPreparationProjectionFallback(operation, () => null));
        Assert.NotNull(fixture.Data.ReadPreparationProjection(operation, input, Source));
        Assert.Equal(2, sources);
        operation.Dispose();
        Assert.Throws<ObjectDisposedException>(() => MortalWoundTreatmentSemanticData.ReadPreparationProjectionFallback(operation, Source));
        Assert.Equal(2, sources);
    }

    /// <summary>Compares actual pair contents, count and order independently of scalar equality.</summary>
    [Fact]
    public void PreparationProjection_ActualBatchPairsAndOrderAreCompared()
    {
        var pairs = new[] { ("source-one", "seal-one"), ("source-two", "seal-two") };
        var expected = new ExpectedPreparationReplayProjection("binding", "input", "preparation", "baseline-input", "baseline", pairs);
        WoundPreparedAcceptedTurnPlan Plan(IEnumerable<(string Source, string Seal)> values) => new(
            new("session", "request", "snapshot", "mortal_world", 43, [], "events"), "binding", "input", "preparation", [], [], [],
            values.Select(pair => new WoundEffectOperationBatch("local", "wound",
                new(1, "wound", "id", "ref", "active", true, "mortal_world", new("mortal_world", "player", "player", "path"),
                    "event", "semantic", "opportunity", "fingerprint", []), [], [], [], pair.Source,
                new("input", "opportunity", "fingerprint", "operation", "summary", 1, "create", null, null, pair.Seal))).ToArray(),
            new("baseline-input", new(null, null, null, null, null), new JsonObject(), new JsonObject(), "baseline"));
        Assert.True(expected.AgreesWith(Plan(pairs)));
        Assert.False(expected.AgreesWith(Plan(pairs.Take(1))));
        Assert.False(expected.AgreesWith(Plan(pairs.Reverse())));
        Assert.False(expected.AgreesWith(Plan(new[] { ("different", "seal-one"), pairs[1] })));
        Assert.False(expected.AgreesWith(Plan(new[] { ("source-one", "different"), pairs[1] })));
    }

    /// <summary>Retains actual owned-definition order even though canonical serialization sorts it.</summary>
    [Fact]
    public void PreparationProjection_InputCaptureDistinguishesCanonicalSortedOrder()
    {
        using var fixture = new Fixture(_output);
        var wound = fixture.Outcome.Before;
        var definitions = new[] {
            System.Text.Json.JsonSerializer.SerializeToElement(new { definitionKey = "one" }),
            System.Text.Json.JsonSerializer.SerializeToElement(new { definitionKey = "two" }) };
        WoundAcceptedTurnInput Input(IEnumerable<System.Text.Json.JsonElement> values) => ProjectionInput(wound with
        {
            Consequences = wound.Consequences with
            {
                OwnedEffectSources = wound.Consequences.OwnedEffectSources with { Definitions = values.ToArray() }
            }
        });
        var a = Input(definitions);
        var b = Input(definitions.Reverse());
        Assert.Equal(WoundAcceptedTurnFingerprints.ComputeInput(a), WoundAcceptedTurnFingerprints.ComputeInput(b));
        Assert.False(WoundAcceptedTurnFingerprints.CapturePreparationReplayInput(a)!.Matches(
            WoundAcceptedTurnFingerprints.CapturePreparationReplayInput(b)!));
    }

    /// <summary>Uses original uncached work for the established unsupported two-intent contour.</summary>
    [Fact]
    public void PreparationProjection_UnsupportedSemanticContourDoesNotRetain()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var original = fixture.Resolution;
        var request = original.RequestAuthority;
        MortalWoundTreatmentOperation[] declared = [new MortalWoundStabilizeOperation(), new MortalWoundStabilizeOperation()];
        var intents = declared.Select((value, ordinal) =>
        {
            var fingerprint = MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(ordinal, value);
            return MortalWoundStabilizeOutcomeIntent.Create(ordinal, fingerprint,
                MortalWoundTreatmentOutcomeIntentComposer.IntentFingerprint(request.RequestFingerprint, ordinal, value, fingerprint));
        }).ToArray();
        var unsupported = MortalWoundTreatmentResolution.Create(original.Mode, original.Coordinates,
            original.AttemptDisposition, original.ResultCategory, original.SelectedOutcomeIndex, original.Interruption,
            declared, intents, null, original.ConsumptionTrigger, null, null, null, request,
            original.ModeEvidence, original.RouteFingerprint, original.RouteCompletion);
        var data = new MortalWoundTreatmentSemanticData(fixture.Outcome, unsupported);
        Assert.False(data.CanCapturePreparationProjection);
        var sources = 0;
        ExpectedPreparationReplayProjection Source() { sources++; return LiteralProjection(); }
        var input = new OperationStructuralSnapshot(["fixed"], []);
        Assert.NotNull(data.ReadPreparationProjection(operation, input, Source));
        Assert.NotNull(data.ReadPreparationProjection(operation, input, Source));
        Assert.Equal(2, sources);
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>Creates independent literal expected DATA, without a forged planner/owner authority.</summary>
    /// <returns>The full ordered scalar and batch-pair vector.</returns>
    private static ExpectedPreparationReplayProjection LiteralProjection() => new("binding", "input", "preparation", "baseline-input", "baseline", [("source", "seal")]);

    /// <summary>Places a real file-free wound in detached typed INPUT for structural-key checks only.</summary>
    /// <param name="wound">The fixture's actual detached wound.</param>
    /// <returns>Data without a publication or registry capability.</returns>
    private static WoundAcceptedTurnInput ProjectionInput(WoundMaterializationEnvelope wound) => new(
        new("session", "request", "snapshot", "mortal_world", 43, [], "events"), [],
        [new("create", "operation", "wound", "transition", "opportunity", "summary", wound, [], [], [])],
        new(new JsonObject(), null, null, null, null), new JsonObject(), new JsonObject());
}
