using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks synchronous computation through the same operation ownership and dependency contract.
/// </summary>
public sealed class ValidationOperationSynchronousTests
{
    /// <summary>
    /// Reuses one source result for eight exact synchronous requests.
    /// </summary>
    [Fact]
    public void EightRequestsExecuteOneSource()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("sync.value", 1, value => value);
        var input = new object();
        var sources = 0;
        for (var i = 0; i < 8; i++)
            Assert.Equal(42, operation.GetOrCompute(rule, input, [], () => { sources++; return 42; }));
        Assert.Equal(1, sources);
        Assert.Equal(1, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Retains legitimate default values independently of source invocation count.
    /// </summary>
    /// <param name="value">
    /// The immutable value, including null, false and zero.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(0)]
    public void DefaultsAreReusable(object? value)
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<object?>("sync.default", 1, item => item);
        var input = new object();
        var sources = 0;
        for (var i = 0; i < 8; i++)
            Assert.Equal(value, operation.GetOrCompute(rule, input, [], () => { sources++; return value; }));
        Assert.Equal(1, sources);
    }

    /// <summary>
    /// Recomputes after a dependency changes and replaces the prior logical entry.
    /// </summary>
    [Fact]
    public void ChangedDependencyRecomputesThenReuses()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("sync.version", 1, value => value);
        var input = new object();
        var dependency = new Dependency();
        var sources = 0;
        Assert.Equal(1, operation.GetOrCompute(rule, input, [dependency], () => ++sources));
        Assert.Equal(1, operation.GetOrCompute(rule, input, [dependency], () => ++sources));
        dependency.Advance();
        Assert.Equal(2, operation.GetOrCompute(rule, input, [dependency], () => ++sources));
        Assert.Equal(2, operation.GetOrCompute(rule, input, [dependency], () => ++sources));
        Assert.Equal(1, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Propagates child versions when the parent consumes a cached child value.
    /// </summary>
    [Fact]
    public void ChildHitRegistersDependencyOnParent()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var child = new OperationComputation<int>("sync.child", 1, value => value);
        var parent = new OperationComputation<int>("sync.parent", 1, value => value);
        var childInput = new object();
        var parentInput = new object();
        var dependency = new Dependency();
        var sources = 0;
        int ReadChild() => operation.GetOrCompute(child, childInput, [dependency], () => ++sources);
        int ReadParent() => operation.GetOrCompute(parent, parentInput, [], () => ReadChild() * 10);
        Assert.Equal(1, ReadChild());
        Assert.Equal(10, ReadParent());
        Assert.Equal(10, ReadParent());
        dependency.Advance();
        Assert.Equal(20, ReadParent());
        Assert.Equal(20, ReadParent());
        Assert.Equal(2, sources);
    }

    /// <summary>
    /// Executes original sources throughout a fresh scope and resumes caching afterward.
    /// </summary>
    [Fact]
    public void FreshScopeDoesNotInsertResults()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("sync.fresh", 1, value => value);
        var input = new object();
        var sources = 0;
        int Read() => operation.GetOrCompute(rule, input, [], () => ++sources);
        Assert.Equal(1, Read());
        Assert.Equal(1, Read());
        using (operation.ForceFresh())
        {
            Assert.Equal(2, Read());
            Assert.Equal(3, Read());
        }
        Assert.Equal(4, Read());
        Assert.Equal(4, Read());
    }

    /// <summary>
    /// Rejects closed operations before invoking a synchronous source.
    /// </summary>
    [Fact]
    public void ClosedOperationCannotInvokeSource()
    {
        var operation = new DefaultOperationValidationService().BeginOperation();
        operation.Dispose();
        var sources = 0;
        var rule = new OperationComputation<int>("sync.closed", 1, value => value);
        Assert.ThrowsAny<InvalidOperationException>(() =>
            operation.GetOrCompute(rule, new object(), [], () => ++sources));
        Assert.Equal(0, sources);
    }

    /// <summary>
    /// Rejects a dependency changed by the selected-hit copy callback outside the coordinator monitor.
    /// </summary>
    [Fact]
    public void CopyCallbackCannotReturnSupersededData()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var dependency = new Dependency();
        var invalidate = false;
        var rule = new OperationComputation<byte[]>("sync.copy", 1, bytes =>
        {
            Assert.False(operation.IsStateMonitorHeld);
            if (invalidate) dependency.Advance();
            return (byte[])bytes.Clone();
        });
        var input = new object();
        operation.GetOrCompute(rule, input, [dependency], () => new byte[] { 7 });
        invalidate = true;
        Assert.ThrowsAny<InvalidOperationException>(() =>
            operation.GetOrCompute(rule, input, [dependency], () => new byte[] { 8 }));
    }

    /// <summary>
    /// Preserves source failures and prevents earlier successful entries from surviving them.
    /// </summary>
    [Fact]
    public void FailureInvalidatesEarlierResults()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("sync.failure", 1, value => value);
        var input = new object();
        var sources = 0;
        Assert.Equal(1, operation.GetOrCompute(rule, input, [], () => ++sources));
        Assert.Throws<FormatException>(() => operation.GetOrCompute(rule, new object(), [],
            () => throw new FormatException("original source failure")));
        Assert.Equal(2, operation.GetOrCompute(rule, input, [], () => ++sources));
        Assert.Equal(2, operation.GetOrCompute(rule, input, [], () => ++sources));
    }

    /// <summary>
    /// Refuses recursive computation of the same logical key before invoking its second source.
    /// </summary>
    [Fact]
    public void RecursiveSameKeyCannotComputeAgain()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("sync.cycle", 1, value => value);
        var input = new object();
        var innerSources = 0;
        Assert.ThrowsAny<InvalidOperationException>(() => operation.GetOrCompute(rule, input, [],
            () => operation.GetOrCompute(rule, input, [], () => ++innerSources)));
        Assert.Equal(0, innerSources);
    }

    /// <summary>
    /// Refuses a source result after its callback disposes the operation.
    /// </summary>
    [Fact]
    public void SourceCannotReturnAfterDisposal()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("sync.dispose", 1, value => value);
        Assert.ThrowsAny<InvalidOperationException>(() => operation.GetOrCompute(rule, new object(), [],
            () => { operation.Dispose(); return 9; }));
    }

    /// <summary>
    /// Supplies an independently advancing in-memory DATA version.
    /// </summary>
    private sealed class Dependency : IOperationDependency
    {
        private object _version = new();

        /// <summary>
        /// Replaces the current token without reusing a prior version.
        /// </summary>
        internal void Advance() => _version = new();

        /// <inheritdoc />
        public bool TryCaptureVersion(out object? version)
        {
            version = _version;
            return true;
        }
    }
}
