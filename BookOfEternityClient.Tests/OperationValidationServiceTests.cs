using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks reusable operation computations without preparing files or game objects.
/// </summary>
/// <param name="output">
/// Receiver for actual computation counts and operation lifetime observations.
/// </param>
public sealed class OperationValidationServiceTests(ITestOutputHelper output)
{
    /// <summary>
    /// Reuses an unchanged value within one operation and executes again in another operation.
    /// </summary>
    [Fact]
    public async Task RepeatedValueComputationsExecuteOncePerOperation()
    {
        var service = new DefaultOperationValidationService();
        var rule = new OperationComputation<int>("fixture.value", 1, value => value);
        var input = new object();
        var executions = 0;
        Task<int> Compute(CancellationToken _)
        {
            executions++;
            return Task.FromResult(7);
        }
        using (var operation = service.BeginOperation())
        {
            for (var request = 0; request < 8; request++)
            {
                var result = await operation.GetOrComputeAsync(rule, input, [], Compute);
                output.WriteLine($"request={request + 1}; executions={executions}; result={result}");
                Assert.Equal(7, result);
            }
            Assert.Equal(1, executions);
        }
        using (var operation = service.BeginOperation())
            Assert.Equal(7, await operation.GetOrComputeAsync(rule, input, [], Compute));
        Assert.Equal(2, executions);
    }

    /// <summary>
    /// Stores explicit successful validation independently from a computed Boolean value.
    /// </summary>
    [Fact]
    public async Task SuccessfulValidationFactsExecuteOncePerOperation()
    {
        var service = new DefaultOperationValidationService();
        var rule = new OperationValidationRule("fixture.validation", 1);
        var input = new object();
        var executions = 0;
        Task Validate(CancellationToken _)
        {
            executions++;
            return Task.CompletedTask;
        }
        using var operation = service.BeginOperation();
        for (var request = 0; request < 8; request++)
            await operation.ValidateAsync(rule, input, [], Validate);
        output.WriteLine($"requests=8; validationExecutions={executions}");
        Assert.Equal(1, executions);
    }

    /// <summary>
    /// Retains default values without confusing their value with cache presence.
    /// </summary>
    [Fact]
    public async Task FalseZeroNullAndEmptyArePresentValues()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var executions = 0;
        async Task Check<T>(T value)
        {
            var rule = new OperationComputation<T>("fixture.default", 1, item => item);
            var input = new object();
            var before = executions;
            Task<T> Compute(CancellationToken _) { executions++; return Task.FromResult(value); }
            Assert.Equal(value, await operation.GetOrComputeAsync(rule, input, [], Compute));
            Assert.Equal(value, await operation.GetOrComputeAsync(rule, input, [], Compute));
            Assert.Equal(before + 1, executions);
        }
        await Check(false);
        await Check(0);
        await Check<string?>(null);
        await Check("");
    }

    /// <summary>
    /// Detaches both the supplied result and returned mutable views from the retained value.
    /// </summary>
    [Fact]
    public async Task MutableResultsAreCopiedIntoAndOutOfTheCache()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var source = new[] { 1, 2 };
        var executions = 0;
        var rule = new OperationComputation<int[]>("fixture.array", 1, value => value.ToArray());
        var input = new object();
        Task<int[]> Compute(CancellationToken _) { executions++; return Task.FromResult(source); }
        var first = await operation.GetOrComputeAsync(rule, input, [], Compute);
        first[0] = 9;
        source[1] = 8;
        Assert.Equal(new[] { 1, 2 }, await operation.GetOrComputeAsync(rule, input, [], Compute));
        Assert.Equal(1, executions);
    }

    /// <summary>
    /// Separates descriptor and input identities even when their display values compare equal.
    /// </summary>
    [Fact]
    public async Task DistinctRulesAndInputsDoNotShareResults()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.identity", 1, value => value);
        var otherRule = new OperationComputation<int>("fixture.identity", 1, value => value);
        var input = new EqualInput();
        var otherInput = new EqualInput();
        var executions = 0;
        Task<int> Compute(CancellationToken _) { executions++; return Task.FromResult(7); }
        await operation.GetOrComputeAsync(rule, input, [], Compute);
        await operation.GetOrComputeAsync(rule, input, [], Compute);
        await operation.GetOrComputeAsync(otherRule, input, [], Compute);
        await operation.GetOrComputeAsync(rule, otherInput, [], Compute);
        Assert.Equal(3, executions);
    }

    /// <summary>
    /// Recomputes when a request declares a different direct domain or a new domain version.
    /// </summary>
    [Fact]
    public async Task DirectDeclarationAndVersionChangesCannotHitAnOlderEntry()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.dependencies", 1, value => value);
        var input = new object();
        var a = new Dependency();
        var b = new Dependency();
        var executions = 0;
        Task<int> Compute(CancellationToken _) { executions++; return Task.FromResult(7); }
        await operation.GetOrComputeAsync(rule, input, [a], Compute);
        await operation.GetOrComputeAsync(rule, input, [a], Compute);
        Assert.Equal(1, executions);
        await operation.GetOrComputeAsync(rule, input, [b], Compute);
        Assert.Equal(2, executions);
        b.Advance();
        await operation.GetOrComputeAsync(rule, input, [b], Compute);
        Assert.Equal(3, executions);
        await operation.GetOrComputeAsync(rule, input, [a, b], Compute);
        await operation.GetOrComputeAsync(rule, input, [b, a], Compute);
        Assert.Equal(5, executions);
    }

    /// <summary>
    /// Detaches a supplied declaration so later list edits cannot rewrite an older entry's domains.
    /// </summary>
    [Fact]
    public async Task ChangingTheSuppliedDeclarationCannotRewriteTheStoredDeclaration()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.declaration.copy", 1, value => value);
        var input = new object();
        IOperationDependency[] declaration = [new Dependency()];
        var executions = 0;
        Task<int> Compute(CancellationToken _) { executions++; return Task.FromResult(7); }
        await operation.GetOrComputeAsync(rule, input, declaration, Compute);
        declaration[0] = new Dependency();
        await operation.GetOrComputeAsync(rule, input, declaration, Compute);
        Assert.Equal(2, executions);
    }

    /// <summary>
    /// Rechecks dependencies after copying a hit instead of returning a result from a changed version.
    /// </summary>
    [Fact]
    public async Task VersionChangeWhileCopyingAHitCannotEscape()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var dependency = new Dependency();
        var changeDuringCopy = false;
        var rule = new OperationComputation<int>("fixture.hit.version", 1, value =>
        {
            if (changeDuringCopy) dependency.Advance();
            return value;
        });
        var input = new object();
        var executions = 0;
        Task<int> Compute(CancellationToken _) { executions++; return Task.FromResult(7); }
        await operation.GetOrComputeAsync(rule, input, [dependency], Compute);
        changeDuringCopy = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => operation.GetOrComputeAsync(rule, input, [dependency], Compute));
        Assert.Equal(0, operation.CaptureState().Entries);
        changeDuringCopy = false;
        await operation.GetOrComputeAsync(rule, input, [dependency], Compute);
        Assert.Equal(2, executions);
    }

    /// <summary>
    /// Carries a child's dependency through a child hit into a retained parent result.
    /// </summary>
    [Fact]
    public async Task ChildHitsContributeTransitiveDependencies()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var childRule = new OperationComputation<int>("fixture.child", 1, value => value);
        var parentRule = new OperationComputation<int>("fixture.parent", 1, value => value);
        var childInput = new object();
        var parentInput = new object();
        var dependency = new Dependency();
        var childExecutions = 0;
        var parentExecutions = 0;
        Task<int> Child(CancellationToken _) { childExecutions++; return Task.FromResult(7); }
        async Task<int> Parent(CancellationToken token)
        {
            parentExecutions++;
            return await operation.GetOrComputeAsync(childRule, childInput, [dependency], Child, token);
        }
        await operation.GetOrComputeAsync(childRule, childInput, [dependency], Child);
        await operation.GetOrComputeAsync(parentRule, parentInput, [], Parent);
        await operation.GetOrComputeAsync(parentRule, parentInput, [], Parent);
        Assert.Equal(1, childExecutions);
        Assert.Equal(1, parentExecutions);
        dependency.Advance();
        await operation.GetOrComputeAsync(parentRule, parentInput, [], Parent);
        Assert.Equal(2, childExecutions);
        Assert.Equal(2, parentExecutions);
    }

    /// <summary>
    /// Refuses to retain a parent whose child cannot declare an eligible source version.
    /// </summary>
    [Fact]
    public async Task IneligibleChildrenPreventParentCaching()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var childRule = new OperationComputation<int>("fixture.untracked.child", 1, value => value);
        var parentRule = new OperationComputation<int>("fixture.untracked.parent", 1, value => value);
        var input = new object();
        var dependency = new Dependency { Eligible = false };
        var childExecutions = 0;
        var parentExecutions = 0;
        Task<int> Child(CancellationToken _) { childExecutions++; return Task.FromResult(7); }
        async Task<int> Parent(CancellationToken token)
        {
            parentExecutions++;
            return await operation.GetOrComputeAsync(childRule, input, [dependency], Child, token);
        }
        for (var request = 0; request < 4; request++)
            Assert.Equal(7, await operation.GetOrComputeAsync(parentRule, input, [], Parent));
        Assert.Equal(4, parentExecutions);
        Assert.Equal(4, childExecutions);
    }

    /// <summary>
    /// Forces every nested computation to run and leaves no fresh-subtree result cached.
    /// </summary>
    [Fact]
    public async Task ForceFreshPropagatesAndDoesNotInsertResults()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.fresh", 1, value => value);
        var input = new object();
        var executions = 0;
        Task<int> Compute(CancellationToken _) { executions++; return Task.FromResult(7); }
        await operation.GetOrComputeAsync(rule, input, [], Compute);
        using (operation.ForceFresh())
        {
            await operation.GetOrComputeAsync(rule, input, [], Compute);
            using (operation.ForceFresh())
                await operation.GetOrComputeAsync(rule, input, [], Compute);
            await operation.GetOrComputeAsync(rule, input, [], Compute);
            Assert.Equal(0, operation.CaptureState().Entries);
        }
        await operation.GetOrComputeAsync(rule, input, [], Compute);
        await operation.GetOrComputeAsync(rule, input, [], Compute);
        Assert.Equal(5, executions);
    }

    /// <summary>
    /// Clears child and parent positives when a forced revalidation refuses or is cancelled.
    /// </summary>
    /// <param name="cancel">
    /// Whether the failed fresh request is cancelled instead of throwing a data refusal.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedFreshRequestCannotResurrectChildOrParent(bool cancel)
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var child = new OperationComputation<int>("fixture.failure.child", 1, value => value);
        var parent = new OperationComputation<int>("fixture.failure.parent", 1, value => value);
        var input = new object();
        var childExecutions = 0;
        var parentExecutions = 0;
        Task<int> Child(CancellationToken _) { childExecutions++; return Task.FromResult(7); }
        async Task<int> Parent(CancellationToken token)
        {
            parentExecutions++;
            return await operation.GetOrComputeAsync(child, input, [], Child, token);
        }
        await operation.GetOrComputeAsync(parent, input, [], Parent);
        await operation.GetOrComputeAsync(parent, input, [], Parent);
        Assert.Equal(1, parentExecutions);
        using (operation.ForceFresh())
        {
            if (cancel)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.GetOrComputeAsync(child, input, [],
                    _ => Task.FromCanceled<int>(new CancellationToken(true))));
            else
                await Assert.ThrowsAsync<InvalidDataException>(() => operation.GetOrComputeAsync(child, input, [],
                    _ => throw new InvalidDataException("actual refusal")));
        }
        await operation.GetOrComputeAsync(parent, input, [], Parent);
        Assert.Equal(2, parentExecutions);
        Assert.Equal(2, childExecutions);
    }

    /// <summary>
    /// Prevents an active parent from publishing success after catching a failed child.
    /// </summary>
    [Fact]
    public async Task CaughtChildFailurePoisonsTheActiveParent()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var parent = new OperationComputation<int>("fixture.catching.parent", 1, value => value);
        var child = new OperationComputation<int>("fixture.catching.child", 1, value => value);
        var input = new object();
        var executions = 0;
        async Task<int> Parent(CancellationToken token)
        {
            executions++;
            try
            {
                await operation.GetOrComputeAsync(child, input, [],
                    _ => throw new InvalidDataException("caught refusal"), token);
            }
            catch (InvalidDataException) { }
            return 7;
        }
        await operation.GetOrComputeAsync(parent, input, [], Parent);
        await operation.GetOrComputeAsync(parent, input, [], Parent);
        Assert.Equal(2, executions);
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Clears earlier successes even when a failing request uses another computation key.
    /// </summary>
    [Fact]
    public async Task AnyFailedComputationRetiresEarlierSuccesses()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.before.failure", 1, value => value);
        var failing = new OperationComputation<int>("fixture.failure", 1, value => value);
        var input = new object();
        var executions = 0;
        Task<int> Compute(CancellationToken _) { executions++; return Task.FromResult(7); }
        await operation.GetOrComputeAsync(rule, input, [], Compute);
        await Assert.ThrowsAsync<InvalidDataException>(() => operation.GetOrComputeAsync(failing, input, [],
            _ => throw new InvalidDataException("failure")));
        await operation.GetOrComputeAsync(rule, input, [], Compute);
        Assert.Equal(2, executions);
    }

    /// <summary>
    /// Rejects a result whose declared version changed while its actual computation awaited.
    /// </summary>
    [Fact]
    public async Task VersionChangeDuringAwaitCannotPublishSuccess()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.await", 1, value => value);
        var dependency = new Dependency();
        var input = new object();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = operation.GetOrComputeAsync(rule, input, [dependency], async _ =>
        {
            entered.SetResult();
            await release.Task;
            return 7;
        });
        await entered.Task;
        dependency.Advance();
        release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.Equal(0, operation.CaptureState().Entries);
        Assert.Equal(7, await operation.GetOrComputeAsync(rule, input, [dependency], _ => Task.FromResult(7)));
    }

    /// <summary>
    /// Rejects same-key recursion without recursively waiting for its own pending result.
    /// </summary>
    [Fact]
    public async Task RecursiveSameKeyIsRefusedWithoutAnUnboundedFixture()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.recursion", 1, value => value);
        var input = new object();
        var executions = 0;
        Task<int> Compute(CancellationToken token)
        {
            executions++;
            return executions == 1
                ? operation.GetOrComputeAsync(rule, input, [], Compute, token)
                : Task.FromResult(7);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => operation.GetOrComputeAsync(rule, input, [], Compute));
        Assert.Equal(1, executions);
    }

    /// <summary>
    /// Rejects an overlapping independent request and lets the original task settle.
    /// </summary>
    [Fact]
    public async Task IndependentParallelUseIsRefused()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.parallel", 1, value => value);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = operation.GetOrComputeAsync(rule, new object(), [], async _ =>
        {
            entered.SetResult();
            await release.Task;
            return 7;
        });
        await entered.Task;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => operation.GetOrComputeAsync(rule, new object(), [],
                _ => Task.FromResult(7)));
        }
        finally
        {
            release.SetResult();
            await first;
        }
    }

    /// <summary>
    /// Releases retained values on normal, exceptional and cancelled whole-operation exits.
    /// </summary>
    /// <param name="outcome">
    /// The caller's whole-operation completion path, independent of validation success.
    /// </param>
    [Theory]
    [InlineData("success")]
    [InlineData("error")]
    [InlineData("cancel")]
    public async Task ClosingTheWholeOperationClearsItsRetainedData(string outcome)
    {
        var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int[]>("fixture.lifetime", 1, value => value.ToArray());
        var dependency = new Dependency();
        try
        {
            using (operation)
            {
                await operation.GetOrComputeAsync(rule, new object(), [dependency], _ => Task.FromResult(new[] { 7 }));
                Assert.Equal(1, operation.CaptureState().Entries);
                Assert.Equal(1, operation.CaptureState().Dependencies);
                if (outcome == "error") throw new InvalidDataException("whole operation failed");
                if (outcome == "cancel") throw new OperationCanceledException("whole operation cancelled");
            }
        }
        catch (Exception error) when (error is InvalidDataException or OperationCanceledException) { }
        operation.Dispose();
        var state = operation.CaptureState();
        Assert.True(state.Closed);
        Assert.Equal(0, state.Entries);
        Assert.Equal(0, state.Dependencies);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => operation.GetOrComputeAsync(rule, new object(), [],
            _ => Task.FromResult(new[] { 7 })));
    }

    /// <summary>
    /// Prevents in-flight work from repopulating an operation after its owner closes it.
    /// </summary>
    [Fact]
    public async Task ClosingDuringAwaitPreventsLateInsertion()
    {
        var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.close.pending", 1, value => value);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = operation.GetOrComputeAsync(rule, new object(), [], async _ =>
        {
            entered.SetResult();
            await release.Task;
            return 7;
        });
        await entered.Task;
        operation.Dispose();
        release.SetResult();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Preserves a primary computation error when the operation also closes during its await.
    /// </summary>
    [Fact]
    public async Task ClosingDoesNotReplaceAnOriginalComputationError()
    {
        var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.primary.error", 1, value => value);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var original = new InvalidDataException("original error");
        var pending = operation.GetOrComputeAsync(rule, new object(), [], async _ =>
        {
            entered.SetResult();
            await release.Task;
            throw original;
        });
        await entered.Task;
        operation.Dispose();
        release.SetResult();
        Assert.Same(original, await Assert.ThrowsAsync<InvalidDataException>(() => pending));
    }

    /// <summary>
    /// Refuses inherited routing after its original frame ends or its whole operation closes.
    /// </summary>
    /// <param name="closeOperation">
    /// Whether to close the whole context before releasing the captured descendant.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedDescendantsCannotReviveRetiredFrames(bool closeOperation)
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.descendant", 1, value => value);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>? descendant = null;
        await operation.GetOrComputeAsync(rule, new object(), [], _ =>
        {
            descendant = Task.Run(async () =>
            {
                await release.Task;
                return await operation.GetOrComputeAsync(rule, new object(), [], _ => Task.FromResult(7));
            });
            return Task.FromResult(7);
        });
        Assert.NotNull(descendant);
        if (closeOperation) operation.Dispose();
        release.SetResult();
        if (closeOperation) await Assert.ThrowsAsync<ObjectDisposedException>(() => descendant);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => descendant);
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Observes cancellation before a hit and retires earlier positives for the cancelled request.
    /// </summary>
    [Fact]
    public async Task CancellationBeforeAHitDoesNotReturnTheCachedValue()
    {
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("fixture.cancel.hit", 1, value => value);
        var input = new object();
        var executions = 0;
        Task<int> Compute(CancellationToken _) { executions++; return Task.FromResult(7); }
        await operation.GetOrComputeAsync(rule, input, [], Compute);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.GetOrComputeAsync(rule, input, [], Compute,
            new CancellationToken(true)));
        await operation.GetOrComputeAsync(rule, input, [], Compute);
        Assert.Equal(2, executions);
    }

    /// <summary>
    /// Supplies stable domain identity and explicit replaceable version tokens to the real service.
    /// </summary>
    private sealed class Dependency : IOperationDependency
    {
        private object _version = new();

        /// <summary>
        /// Gets or sets whether the source domain is currently eligible for result reuse.
        /// </summary>
        internal bool Eligible { get; set; } = true;

        /// <summary>
        /// Replaces the version without reusing an earlier token.
        /// </summary>
        internal void Advance() => _version = new object();

        /// <summary>
        /// Captures the domain's current token when its eligibility is known.
        /// </summary>
        /// <param name="version">
        /// Current immutable version token, or null for an ineligible domain.
        /// </param>
        /// <returns>
        /// True for an eligible domain; otherwise false.
        /// </returns>
        public bool TryCaptureVersion(out object? version)
        {
            version = Eligible ? _version : null;
            return Eligible;
        }
    }

    /// <summary>
    /// Gives distinct input identities equal public values to catch accidental value-based keying.
    /// </summary>
    private sealed class EqualInput
    {
        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is EqualInput;

        /// <inheritdoc />
        public override int GetHashCode() => 0;
    }
}
