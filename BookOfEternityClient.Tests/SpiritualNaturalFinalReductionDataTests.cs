using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks operation-owned natural final-reduction DATA reuse without a world, files or live authority.
/// </summary>
public sealed class SpiritualNaturalFinalReductionDataTests
{
    /// <summary>
    /// Computes once for equal admitted DATA and keeps caller mutations outside the retained result.
    /// </summary>
    [Fact]
    public void EqualRequestsComputeOnceAndExportIndependentHistory()
    {
        var companion = new object();
        var sources = 0;
        var captures = 0;
        using var data = new SpiritualNaturalFinalReductionData(() =>
        {
            captures++;
            return new(["dependency"], [companion]);
        });
        WoundAcceptedTurnPlanningResult Compute() => SuccessfulResult(++sources);

        var first = data.Read(new(["admitted", null, ""], [companion]), Compute);
        var firstPlan = Assert.IsType<WoundAcceptedTurnPlan>(first.Plan);
        var exportedHistory = firstPlan.HistoryAfterImage;
        exportedHistory["source"] = 999;
        exportedHistory["entries"]![0]!["marker"] = "caller mutation";
        exportedHistory["extra"] = true;

        var capturesBeforeHit = captures;
        var second = data.Read(new(["admitted", null, ""], [companion]), Compute);
        var secondPlan = Assert.IsType<WoundAcceptedTurnPlan>(second.Plan);
        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(1, sources);
        Assert.True(captures > capturesBeforeHit);
        Assert.NotSame(firstPlan, secondPlan);
        Assert.NotSame(exportedHistory, secondPlan.HistoryAfterImage);
        Assert.Equal(1, SourceNumber(second));
        Assert.Equal("original", secondPlan.HistoryAfterImage["entries"]![0]!["marker"]!.GetValue<string>());
        Assert.False(secondPlan.HistoryAfterImage.ContainsKey("extra"));
        Assert.Equal("final-1", secondPlan.WoundFinalPlanFingerprint);
    }

    /// <summary>
    /// Distinguishes complete ordered input and dependency contours, with no dependency-version resurrection.
    /// </summary>
    /// <param name="changeDependency">Whether the changed contour is live dependency DATA rather than admitted input.</param>
    /// <param name="dimension">The single structural distinction changed independently by this row.</param>
    [Theory]
    [InlineData(false, "value")]
    [InlineData(false, "value-order")]
    [InlineData(false, "null-value")]
    [InlineData(false, "value-count")]
    [InlineData(false, "identity")]
    [InlineData(false, "identity-order")]
    [InlineData(false, "null-identity")]
    [InlineData(false, "identity-count")]
    [InlineData(true, "value")]
    [InlineData(true, "value-order")]
    [InlineData(true, "null-value")]
    [InlineData(true, "value-count")]
    [InlineData(true, "identity")]
    [InlineData(true, "identity-order")]
    [InlineData(true, "null-identity")]
    [InlineData(true, "identity-count")]
    public void ChangedStructuralDimensionComputesFresh(bool changeDependency, string dimension)
    {
        object firstCompanion = new string('a', 1);
        object secondCompanion = new string('b', 1);
        var original = StructuralInput("original", firstCompanion, secondCompanion);
        var changed = StructuralInput(dimension, firstCompanion, secondCompanion);
        var dependency = original;
        var admitted = original;
        var sources = 0;
        using var data = new SpiritualNaturalFinalReductionData(() => dependency);
        WoundAcceptedTurnPlanningResult Compute() => SuccessfulResult(++sources);

        Assert.Equal(1, SourceNumber(data.Read(admitted, Compute)));
        Assert.Equal(1, SourceNumber(data.Read(
            StructuralInput("original", firstCompanion, secondCompanion), Compute)));
        if (changeDependency) dependency = changed;
        else admitted = changed;
        Assert.Equal(2, SourceNumber(data.Read(admitted, Compute)));
        Assert.Equal(2, SourceNumber(data.Read(admitted, Compute)));
        Assert.Equal(2, sources);

        if (changeDependency)
        {
            dependency = StructuralInput("original", firstCompanion, secondCompanion);
            Assert.Equal(3, SourceNumber(data.Read(admitted, Compute)));
            Assert.Equal(3, SourceNumber(data.Read(admitted, Compute)));
            Assert.Equal(3, sources);
        }
    }

    /// <summary>
    /// Leaves null captures unretained and does not restore an older hit after eligibility returns.
    /// </summary>
    [Fact]
    public void NullCaptureComputesFreshUntilValidAndInvalidatesPriorData()
    {
        OperationStructuralSnapshot? current = null;
        var admitted = new OperationStructuralSnapshot(["admitted"], []);
        var sources = 0;
        using var data = new SpiritualNaturalFinalReductionData(() => current);
        WoundAcceptedTurnPlanningResult Compute() => SuccessfulResult(++sources);

        Assert.Equal(1, SourceNumber(data.Read(admitted, Compute)));
        Assert.Equal(2, SourceNumber(data.Read(admitted, Compute)));
        current = new(["valid"], []);
        Assert.Equal(3, SourceNumber(data.Read(admitted, Compute)));
        Assert.Equal(3, SourceNumber(data.Read(admitted, Compute)));
        current = null;
        Assert.Equal(4, SourceNumber(data.Read(admitted, Compute)));
        Assert.Equal(5, SourceNumber(data.Read(admitted, Compute)));
        current = new(["valid"], []);
        Assert.Equal(6, SourceNumber(data.Read(admitted, Compute)));
        Assert.Equal(6, SourceNumber(data.Read(admitted, Compute)));
        Assert.Equal(6, sources);
    }

    /// <summary>
    /// Returns the original refusal with its issues, retains no failure and retires previously warmed DATA.
    /// </summary>
    [Fact]
    public void SourceRefusalPreservesOriginalIssuesAndRecoversFresh()
    {
        var firstInput = new OperationStructuralSnapshot(["first input"], []);
        var refusedInput = new OperationStructuralSnapshot(["refused input"], []);
        var original = new WoundAcceptedTurnPlanningResult(null,
            [WoundAcceptedTurnPlannerCore.NewIssue("natural_data_refusal", "original refusal", "expected", "actual")]);
        var sources = 0;
        using var data = new SpiritualNaturalFinalReductionData(() => new(["dependency"], []));
        WoundAcceptedTurnPlanningResult Compute() => SuccessfulResult(++sources);
        WoundAcceptedTurnPlanningResult Refuse() { sources++; return original; }

        Assert.Equal(1, SourceNumber(data.Read(firstInput, Compute)));
        Assert.Same(original, data.Read(refusedInput, Refuse));
        Assert.Same(original, data.Read(refusedInput, Refuse));
        Assert.Equal(3, sources);
        Assert.False(original.Success);
        var issue = Assert.Single(original.Issues);
        Assert.Equal(("natural_data_refusal", "original refusal", "expected", "actual"),
            (issue.Code, issue.Message, issue.Expected, issue.Actual));
        Assert.Equal(4, SourceNumber(data.Read(firstInput, Compute)));
        Assert.Equal(4, SourceNumber(data.Read(firstInput, Compute)));
        Assert.Equal(5, SourceNumber(data.Read(refusedInput, Compute)));
        Assert.Equal(5, SourceNumber(data.Read(refusedInput, Compute)));
        Assert.Equal(5, sources);
    }

    /// <summary>
    /// Rethrows the original source exception, retires warmed DATA and permits a fresh successful retry.
    /// </summary>
    [Fact]
    public void SourceExceptionInvalidatesWarmedDataAndRecoversFresh()
    {
        var firstInput = new OperationStructuralSnapshot(["first input"], []);
        var failedInput = new OperationStructuralSnapshot(["failed input"], []);
        var expected = new FormatException("original natural reduction failure");
        var sources = 0;
        using var data = new SpiritualNaturalFinalReductionData(() => new(["dependency"], []));
        WoundAcceptedTurnPlanningResult Compute() => SuccessfulResult(++sources);
        WoundAcceptedTurnPlanningResult Fail() { sources++; throw expected; }

        Assert.Equal(1, SourceNumber(data.Read(firstInput, Compute)));
        Assert.Same(expected, Assert.Throws<FormatException>(() => data.Read(failedInput, Fail)));
        Assert.Equal(2, sources);
        Assert.Equal(3, SourceNumber(data.Read(firstInput, Compute)));
        Assert.Equal(3, SourceNumber(data.Read(firstInput, Compute)));
        Assert.Equal(4, SourceNumber(data.Read(failedInput, Compute)));
        Assert.Equal(4, SourceNumber(data.Read(failedInput, Compute)));
        Assert.Equal(4, sources);
    }

    /// <summary>
    /// Rejects a reduction whose dependency changes during its source, then recovers without retaining that result.
    /// </summary>
    [Fact]
    public void DependencyChangeDuringSourceRefusesResultAndRecoversFresh()
    {
        var current = new OperationStructuralSnapshot(["before"], []);
        var admitted = new OperationStructuralSnapshot(["admitted"], []);
        var sources = 0;
        using var data = new SpiritualNaturalFinalReductionData(() => current);
        WoundAcceptedTurnPlanningResult Compute() => SuccessfulResult(++sources);

        Assert.ThrowsAny<InvalidOperationException>(() => data.Read(admitted, () =>
        {
            var result = Compute();
            current = new(["after"], []);
            return result;
        }));
        Assert.Equal(1, sources);
        Assert.Equal(2, SourceNumber(data.Read(admitted, Compute)));
        Assert.Equal(2, SourceNumber(data.Read(admitted, Compute)));
        Assert.Equal(2, sources);
    }

    /// <summary>
    /// Makes explicit invalidation a cold boundary even when every structural value and reference stays unchanged.
    /// </summary>
    [Fact]
    public void ExplicitInvalidationForcesFreshSource()
    {
        var snapshot = new OperationStructuralSnapshot(["unchanged"], []);
        var sources = 0;
        using var data = new SpiritualNaturalFinalReductionData(() => snapshot);
        WoundAcceptedTurnPlanningResult Compute() => SuccessfulResult(++sources);

        Assert.Equal(1, SourceNumber(data.Read(snapshot, Compute)));
        Assert.Equal(1, SourceNumber(data.Read(snapshot, Compute)));
        data.Invalidate();
        Assert.Equal(2, SourceNumber(data.Read(snapshot, Compute)));
        Assert.Equal(2, SourceNumber(data.Read(snapshot, Compute)));
        Assert.Equal(2, sources);
    }

    /// <summary>
    /// Refuses both a warmed read and a cold input after permanent disposal without executing either source.
    /// </summary>
    [Fact]
    public void DisposedOwnerRefusesReuseAndNewComputations()
    {
        var snapshot = new OperationStructuralSnapshot(["unchanged"], []);
        var sources = 0;
        using var data = new SpiritualNaturalFinalReductionData(() => snapshot);
        WoundAcceptedTurnPlanningResult Compute() => SuccessfulResult(++sources);

        Assert.Equal(1, SourceNumber(data.Read(snapshot, Compute)));
        data.Dispose();
        data.Dispose();
        Assert.Throws<ObjectDisposedException>(() => data.Read(snapshot, Compute));
        Assert.Throws<ObjectDisposedException>(() => data.Read(new(["new input"], []), Compute));
        Assert.Equal(1, sources);
    }

    /// <summary>
    /// Keeps identical DATA and the same capture callback private to each independent disposable owner.
    /// </summary>
    [Fact]
    public void IndependentOwnersNeverShareRetainedData()
    {
        var snapshot = new OperationStructuralSnapshot(["same input and dependency"], []);
        Func<OperationStructuralSnapshot?> capture = () => snapshot;
        var sources = 0;
        using var first = new SpiritualNaturalFinalReductionData(capture);
        using var second = new SpiritualNaturalFinalReductionData(capture);
        WoundAcceptedTurnPlanningResult Compute() => SuccessfulResult(++sources);

        Assert.Equal(1, SourceNumber(first.Read(snapshot, Compute)));
        Assert.Equal(2, SourceNumber(second.Read(snapshot, Compute)));
        Assert.Equal(1, SourceNumber(first.Read(snapshot, Compute)));
        Assert.Equal(2, SourceNumber(second.Read(snapshot, Compute)));
        first.Invalidate();
        Assert.Equal(2, SourceNumber(second.Read(snapshot, Compute)));
        Assert.Equal(3, SourceNumber(first.Read(snapshot, Compute)));
        first.Dispose();
        Assert.Equal(2, SourceNumber(second.Read(snapshot, Compute)));
        Assert.Equal(3, sources);
    }

    /// <summary>
    /// Builds one independent contour with exactly the named ordered-value or actual-reference distinction.
    /// </summary>
    /// <param name="dimension">The changed dimension, or original for the unchanged baseline.</param>
    /// <param name="first">The original first companion, an equal-looking string with a distinct reference.</param>
    /// <param name="second">The original second companion, retained independently from the first.</param>
    /// <returns>A detached full structural snapshot, not an authority grant.</returns>
    private static OperationStructuralSnapshot StructuralInput(string dimension, object first, object second) => dimension switch
    {
        "original" => new(["prepared", null, ""], [first, second, null]),
        "value" => new(["changed", null, ""], [first, second, null]),
        "value-order" => new(["", null, "prepared"], [first, second, null]),
        "null-value" => new(["prepared", "", ""], [first, second, null]),
        "value-count" => new(["prepared", null], [first, second, null]),
        "identity" => new(["prepared", null, ""], [new string('a', 1), second, null]),
        "identity-order" => new(["prepared", null, ""], [second, first, null]),
        "null-identity" => new(["prepared", null, ""], [first, second, new object()]),
        "identity-count" => new(["prepared", null, ""], [first, second]),
        _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unknown structural distinction.")
    };

    /// <summary>
    /// Creates minimal typed final-plan DATA whose source ordinal exposes stale reuse without game preparation.
    /// </summary>
    /// <param name="source">The independent source execution ordinal embedded in the final history.</param>
    /// <returns>A successful immutable result envelope that carries no admission or publication authority.</returns>
    private static WoundAcceptedTurnPlanningResult SuccessfulResult(int source)
    {
        var binding = new WoundAcceptedTurnBinding("session", "request", "snapshot", "chaos_sea", 43, [], "events");
        var plan = new WoundAcceptedTurnPlan(binding, "binding", "input", "preparation", "effect-input", "effect-plan",
            "final-" + source, [], [], [], new JsonObject(), new JsonObject
            {
                ["source"] = source,
                ["entries"] = new JsonArray(new JsonObject { ["marker"] = "original" })
            }, []);
        return new(plan, []);
    }

    /// <summary>
    /// Reads the independent source marker through the result's normal detached plan export.
    /// </summary>
    /// <param name="result">The actual DATA result returned by the owner under test.</param>
    /// <returns>The original source execution ordinal retained in its history.</returns>
    private static int SourceNumber(WoundAcceptedTurnPlanningResult result)
    {
        Assert.True(result.Success);
        Assert.Empty(result.Issues);
        return Assert.IsType<WoundAcceptedTurnPlan>(result.Plan).HistoryAfterImage["source"]!.GetValue<int>();
    }
}
