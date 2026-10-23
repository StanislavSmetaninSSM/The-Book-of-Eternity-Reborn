using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks complete structural invalidation over tiny independently owned DATA.
/// </summary>
public sealed class OperationStructuralDependencyTests
{
    /// <summary>
    /// Clears comparison DATA at fresh and failure boundaries without retiring a still-open provider.
    /// </summary>
    [Fact]
    public void FreshAndFailureReleasePriorComparisonData()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var dependency = operation.GetStructuralDependency(new object(), () => new(["same DATA"], []));
        Assert.True(dependency.TryCaptureVersion(out var first));
        using (operation.ForceFresh()) Assert.False(dependency.HasCapturedSnapshot);
        Assert.True(dependency.TryCaptureVersion(out var afterFresh));
        Assert.NotSame(first, afterFresh);
        var rule = new OperationComputation<int>("structural.throw", 1, value => value);
        Assert.Throws<FormatException>(() => operation.GetOrCompute(rule, new object(), [dependency],
            () => throw new FormatException("source refused")));
        Assert.False(dependency.HasCapturedSnapshot);
        Assert.True(dependency.TryCaptureVersion(out var afterFailure));
        Assert.NotSame(afterFresh, afterFailure);
    }

    /// <summary>
    /// Prevents a capture callback from rehydrating evidence after closing its owning operation.
    /// </summary>
    [Fact]
    public void CaptureCannotRepopulateAfterDisposingOperation()
    {
        var operation = new DefaultOperationValidationService().BeginOperation();
        var calls = 0;
        var dependency = operation.GetStructuralDependency(new object(), () =>
        {
            calls++;
            operation.Dispose();
            return new(["too late"], []);
        });
        Assert.False(dependency.TryCaptureVersion(out _));
        Assert.False(dependency.HasCapturedSnapshot);
        Assert.False(dependency.TryCaptureVersion(out _));
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// Binds one actual provider per owner so identical requests retain exact dependency declarations.
    /// </summary>
    [Fact]
    public void SameOwnerReusesRegisteredProvider()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var owner = new object();
        Func<OperationStructuralSnapshot?> capture = () => new(["same"], []);
        Assert.Same(operation.GetStructuralDependency(owner, capture),
            operation.GetStructuralDependency(owner, capture));
    }

    /// <summary>
    /// Releases provider comparison DATA when its whole operation closes.
    /// </summary>
    [Fact]
    public void OperationDisposalReleasesComparisonSnapshot()
    {
        var operation = new DefaultOperationValidationService().BeginOperation();
        var dependency = operation.GetStructuralDependency(new object(), () => new(["private DATA"], []));
        Assert.True(dependency.TryCaptureVersion(out _));
        Assert.True(dependency.HasCapturedSnapshot);
        operation.Dispose();
        Assert.False(dependency.HasCapturedSnapshot);
        Assert.False(dependency.TryCaptureVersion(out _));
    }

    /// <summary>
    /// Rejects a changed capture policy under an already bound owner identity.
    /// </summary>
    [Fact]
    public void SameOwnerCannotReplaceItsCapturePolicy()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var owner = new object();
        operation.GetStructuralDependency(owner, () => new(["first"], []));
        Assert.ThrowsAny<InvalidOperationException>(() =>
            operation.GetStructuralDependency(owner, () => new(["different policy"], [])));
    }

    /// <summary>
    /// Compares ordered nullable values and actual companion identities without accepting equal-looking replacements.
    /// </summary>
    [Fact]
    public void EqualValuesRequireSameCompanionIdentity()
    {
        var companion = new object();
        var original = new OperationStructuralSnapshot(["a", null, ""], [companion, null]);
        Assert.True(original.Matches(new(["a", null, ""], [companion, null])));
        Assert.False(original.Matches(new(["a", "", null], [companion, null])));
        Assert.False(original.Matches(new(["a", null, ""], [new object(), null])));
        Assert.False(original.Matches(new(["a", null], [companion, null])));
    }

    /// <summary>
    /// Owns supplied sequences so later caller mutations cannot rewrite a retained comparison.
    /// </summary>
    [Fact]
    public void SnapshotDetachesSuppliedSequences()
    {
        var companion = new object();
        string?[] values = ["original"];
        object?[] identities = [companion];
        var snapshot = new OperationStructuralSnapshot(values, identities);
        values[0] = "changed";
        identities[0] = new object();
        Assert.True(snapshot.Matches(new(["original"], [companion])));
        Assert.False(snapshot.Matches(new(values, identities)));
    }

    /// <summary>
    /// Rotates versions on changes and restoration instead of resurrecting an old token.
    /// </summary>
    [Fact]
    public void RestorationDoesNotResurrectPriorVersion()
    {
        var value = "before";
        var dependency = new OperationStructuralDependency(() => new([value], []));
        Assert.True(dependency.TryCaptureVersion(out var first));
        Assert.True(dependency.TryCaptureVersion(out var stable));
        Assert.Same(first, stable);
        value = "changed";
        Assert.True(dependency.TryCaptureVersion(out var changed));
        Assert.NotSame(first, changed);
        value = "before";
        Assert.True(dependency.TryCaptureVersion(out var restored));
        Assert.NotSame(first, restored);
        Assert.NotSame(changed, restored);
    }

    /// <summary>
    /// Inspects current structural DATA before returning a warmed cached result.
    /// </summary>
    [Fact]
    public void ChangedNestedFactCannotUseWarmedResult()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var value = "before";
        var dependency = new OperationStructuralDependency(() => new([value], []));
        var rule = new OperationComputation<string>("structural.value", 1, result => result);
        var input = new object();
        var sources = 0;
        string Read() => operation.GetOrCompute(rule, input, [dependency], () => { sources++; return value; });
        Assert.Equal("before", Read());
        Assert.Equal("before", Read());
        value = "after";
        Assert.Equal("after", Read());
        Assert.Equal("after", Read());
        Assert.Equal(2, sources);
    }

    /// <summary>
    /// Bypasses reuse for unsupported captures and establishes a new version only after eligibility returns.
    /// </summary>
    [Fact]
    public void IneligibleCaptureCannotReuseOrRetain()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var eligible = false;
        var dependency = new OperationStructuralDependency(() => eligible ? new(["value"], []) : null);
        var rule = new OperationComputation<int>("structural.ineligible", 1, value => value);
        var input = new object();
        var sources = 0;
        int Read() => operation.GetOrCompute(rule, input, [dependency], () => ++sources);
        Assert.Equal(1, Read());
        Assert.Equal(2, Read());
        eligible = true;
        Assert.Equal(3, Read());
        Assert.Equal(3, Read());
        eligible = false;
        Assert.Equal(4, Read());
    }

    /// <summary>
    /// Preserves the original source exception when malformed DATA prevents structural capture.
    /// </summary>
    [Fact]
    public void CaptureFailureUsesOriginalRefusalMapping()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var captures = 0;
        var dependency = new OperationStructuralDependency(() =>
        {
            captures++;
            throw new JsonException("malformed structural DATA");
        });
        var rule = new OperationComputation<int>("structural.failure", 1, value => value);
        var exception = Assert.Throws<FormatException>(() => operation.GetOrCompute(rule, new object(),
            [dependency], () => throw new FormatException("original refusal")));
        Assert.Equal("original refusal", exception.Message);
        Assert.True(captures > 0);
    }

    /// <summary>
    /// Captures structural evidence outside the generic cache coordinator monitor.
    /// </summary>
    [Fact]
    public void CaptureDoesNotRunUnderCoordinatorMonitor()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var captures = 0;
        var dependency = new OperationStructuralDependency(() =>
        {
            Assert.False(operation.IsStateMonitorHeld);
            captures++;
            return new(["constant"], []);
        });
        var rule = new OperationComputation<int>("structural.monitor", 1, value => value);
        var input = new object();
        Assert.Equal(7, operation.GetOrCompute(rule, input, [dependency], () => 7));
        Assert.Equal(7, operation.GetOrCompute(rule, input, [dependency], () => 8));
        Assert.True(captures >= 2);
    }

    /// <summary>
    /// Refuses a source result whose actual structural DATA changed during computation.
    /// </summary>
    [Fact]
    public void SourceCannotReturnChangedDataAsCurrent()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var value = "before";
        var dependency = new OperationStructuralDependency(() => new([value], []));
        var rule = new OperationComputation<int>("structural.changed-source", 1, item => item);
        Assert.ThrowsAny<InvalidOperationException>(() => operation.GetOrCompute(rule, new object(), [dependency],
            () => { value = "changed"; return 7; }));
    }
}
