using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OperationTimingContextTests
{
    /// <summary>
    /// Observes actual copy validation on small ordinary stages while preserving detached data and fingerprints.
    /// </summary>
    /// <param name="throwingObserver">
    /// Whether the passive receiver throws after retaining every boundary.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BundleCopy_ObservesRealValidationAndPreservesDetachment(bool throwingObserver)
    {
        using var fixture = new Fixture(_output);
        var bundle = CreateOrdinaryBundle();
        var trace = new Trace(_output, throwingObserver);
        var files = fixture.Create(trace.Observe);
        AcceptedMechanicsWoundStageBundle copy;
        using (files.EnterOperationTimingContext()) copy = bundle.DetachedCopy();

        Assert.NotSame(bundle, copy);
        Assert.Equal(bundle.BundleFingerprint, copy.BundleFingerprint);
        Assert.Equal(bundle.WoundFinalPlanFingerprint, copy.WoundFinalPlanFingerprint);
        var returnedInput = copy.Input;
        returnedInput.PreTurnIdentityIndex["diagnostic-mutation"] = true;
        Assert.False(copy.Input.PreTurnIdentityIndex.ContainsKey("diagnostic-mutation"));
        Assert.False(bundle.Input.PreTurnIdentityIndex.ContainsKey("diagnostic-mutation"));
        var rows = trace.Events.ToArray();
        Assert.Contains(rows, row => row.Name == "WoundBundle.DetachedCopy" && row.Boundary == "begin");
        Assert.Contains(rows, row => row.Name == "WoundBundle.Constructor" && row.Boundary == "begin");
        Assert.Contains(rows, row => row.Name == "WoundPlanCache.ValidatePreparedResult" && row.Boundary == "begin");
        Assert.Contains(rows, row => row.Name == "WoundEffect.AcceptEffectResult" && row.Boundary == "begin");
        Assert.Contains(rows, row => row.Name == "WoundPlanCache.ValidateFinalResult" && row.Boundary == "begin");
        Assert.Contains(rows, row => row.Name == "WoundPlanner.Finalize" && row.Boundary == "begin");
        AssertBalanced(trace);
        Assert.Null(FileSystemManager.MeasureCurrentOperation("after-copy"));
    }

    /// <summary>
    /// Retains constructor refusal and its closing boundaries without running later effect or final validation.
    /// </summary>
    [Fact]
    public void BundleConstructor_ObservesPreparedRefusalWithoutRunningLaterStages()
    {
        using var fixture = new Fixture(_output);
        var bundle = CreateOrdinaryBundle();
        var changed = bundle.Input with
        {
            Binding = bundle.Input.Binding with { RequestId = "request_diagnostic_foreign" }
        };
        var trace = new Trace(_output);
        var files = fixture.Create(trace.Observe);
        using (files.EnterOperationTimingContext())
        {
            var failure = Assert.ThrowsAny<ArgumentException>(() => new AcceptedMechanicsWoundStageBundle(
                changed, bundle.PreparedPlan, bundle.EffectBatchPlan, bundle.FinalPlan));
            Assert.Equal("preparedPlan", failure.ParamName);
        }
        var rows = trace.Events.ToArray();
        Assert.Contains(rows, row => row.Name == "WoundBundle.Constructor" && row.Boundary == "begin");
        Assert.Contains(rows, row => row.Name == "WoundPlanCache.ValidatePreparedResult" && row.Boundary == "begin");
        Assert.DoesNotContain(rows, row => row.Name is "WoundEffect.AcceptEffectResult" or "WoundPlanCache.ValidateFinalResult");
        AssertBalanced(trace);
    }

    /// <summary>
    /// Builds real ordinary stages from the existing data fixture without a game, world or canonical publication.
    /// </summary>
    /// <returns>
    /// A validated ordinary bundle for component controls; it conveys no joint repair authority.
    /// </returns>
    private AcceptedMechanicsWoundStageBundle CreateOrdinaryBundle()
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var input = WoundAcceptedTurnTestFixture.CreateNoMechanicsInput();
            var prepared = WoundAcceptedTurnPlanner.Prepare(input);
            Assert.True(prepared.Success, JsonSerializer.Serialize(prepared.Issues));
            var effect = WoundEffectBatchPlanner.Build(prepared.Plan!,
                WoundAcceptedTurnTestFixture.CreateEffectInput(prepared.Plan!), new EffectIdentityFactory());
            Assert.True(effect.Success, JsonSerializer.Serialize(effect.Issues));
            var final = WoundAcceptedTurnPlanner.Finalize(prepared.Plan!, effect);
            Assert.True(final.Success, JsonSerializer.Serialize(final.Issues));
            return new AcceptedMechanicsWoundStageBundle(input, prepared.Plan!, effect.Plan!, final.Plan!);
        }
        finally
        {
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Name = "BundleFixture.CreateOrdinaryBundle", Milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds
            }));
        }
    }

    /// <summary>
    /// Checks complete nested boundaries without imposing a diagnostic retention or duration limit.
    /// </summary>
    /// <param name="trace">
    /// The complete synchronous event sequence to verify.
    /// </param>
    private static void AssertBalanced(Trace trace)
    {
        var stack = new Stack<string>();
        foreach (var row in trace.Events)
        {
            Assert.True(double.IsFinite(row.Milliseconds) && row.Milliseconds >= 0);
            if (row.Boundary == "begin") stack.Push(row.Name);
            else
            {
                Assert.Equal("end", row.Boundary);
                Assert.NotEmpty(stack);
                Assert.Equal(stack.Pop(), row.Name);
            }
        }
        Assert.Empty(stack);
    }
}
