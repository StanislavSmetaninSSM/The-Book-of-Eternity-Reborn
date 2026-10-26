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
