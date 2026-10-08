using System.Runtime.ExceptionServices;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Core;

internal class SessionReplacedException : Exception
{
    internal SessionReplacedException(
        string message,
        string expectedGeneration,
        string? actualGeneration,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ExpectedGeneration = expectedGeneration;
        ActualGeneration = actualGeneration;
    }

    internal string ExpectedGeneration { get; }
    internal string? ActualGeneration { get; }
}

internal static class SessionOperationContext
{
    private static readonly AsyncLocal<Frame?> CurrentFrame = new();

    internal static async Task RunBoundAsync(
        FileSystemManager fileSystem,
        string expectedGeneration,
        Func<Task> operation)
    {
        await RunBoundAsync<object?>(
            fileSystem,
            expectedGeneration,
            async () =>
            {
                await operation();
                return null;
            });
    }

    internal static async Task<T> RunBoundAsync<T>(
        FileSystemManager fileSystem,
        string expectedGeneration,
        Func<Task<T>> operation)
    {
        return await RunBoundCoreAsync(
            fileSystem,
            expectedGeneration,
            operation,
            writeLease: null);
    }

    internal static async Task<T> RunBoundAsync<T>(
        FileSystemManager fileSystem,
        string expectedGeneration,
        FileSystemManager.CanonicalWriteLease writeLease,
        Func<Task<T>> operation,
        Func<MainOperationOutcome>? establishedOutcome = null)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        return await RunBoundCoreAsync(
            fileSystem,
            expectedGeneration,
            operation,
            writeLease,
            establishedOutcome);
    }

    /// <summary>
    /// Runs an operation within its generation binding and closes that binding while preserving typed storage outcomes.
    /// </summary>
    /// <typeparam name="T">
    /// The operation's result type.
    /// </typeparam>
    /// <param name="fileSystem">
    /// The manager whose canonical root and finalization lease establish the binding.
    /// </param>
    /// <param name="expectedGeneration">
    /// The nonempty generation that must remain current throughout the operation.
    /// </param>
    /// <param name="operation">
    /// The action to execute within the active binding.
    /// </param>
    /// <param name="writeLease">
    /// An existing caller-owned lease, or null to acquire a separate finalization lease before closing.
    /// </param>
    /// <returns>
    /// The completed operation result. Established session replacement takes precedence over a storage failure;
    /// ordinary closing failures retain the original typed storage outcome and a separate diagnostic.
    /// </returns>
    private static async Task<T> RunBoundCoreAsync<T>(FileSystemManager fileSystem,string expectedGeneration,Func<Task<T>> operation,FileSystemManager.CanonicalWriteLease? writeLease,Func<MainOperationOutcome>? capturedOutcome=null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);ArgumentNullException.ThrowIfNull(operation);
        if(string.IsNullOrWhiteSpace(expectedGeneration))throw new ArgumentException("A session operation requires a generation.",nameof(expectedGeneration));
        await using var main=fileSystem.BeginParticipatingMainAdmission();await main.AcquireAsync();
        Exception? failure=null;T? result=default;
        try {result=await RunBoundBodyAsync(fileSystem,expectedGeneration,operation,writeLease,capturedOutcome);}
        catch(Exception e){failure=e;throw;}
        finally {
            var outcome=failure==null?(capturedOutcome?.Invoke()??MainOperationOutcome.Completed):OutcomeFor(failure,capturedOutcome?.Invoke());
            try {await main.CompleteAsync(outcome,HasClosingFailure(failure));}
            catch(Exception close) when(failure!=null){failure.Data["MainOperationCloseFailure"]=close;}
            catch(Exception close){throw new MainOperationContinuationException<T>(result!,outcome,main.DescribeClose(outcome,false),close);}
        }
        return result!;
    }

    internal static async Task<T> RunParticipatingCurrentSessionAsync<T>(FileSystemManager files,Func<Task<T>> operation,Func<MainOperationOutcome>? establishedOutcome=null)
    {
        ArgumentNullException.ThrowIfNull(files);ArgumentNullException.ThrowIfNull(operation);
        await using var main=files.BeginParticipatingMainAdmission();await main.AcquireAsync();
        Exception? failure=null;T? result=default;MainOperationOutcome outcome=MainOperationOutcome.Completed;
        try {
            string generation;
            if(!TryGetExpectedGeneration(files.BasePath,out generation)) {
                await using var lease=await files.AcquireCanonicalWriteLeaseAsync();generation=files.GetOrCreateSessionGeneration(lease);
            }
            result=await RunBoundCoreAsync(files,generation,async()=> {
                var value=await operation();
                // Freeze the callback's established decision once, before any
                // actual finalization or close can fail. Later reads use this value.
                outcome=establishedOutcome?.Invoke()??MainOperationOutcome.Completed;
                return value;
            },null,()=>outcome);
        } catch(Exception e){failure=e;outcome=establishedOutcome?.Invoke()??outcome;throw;}
        finally {
            outcome=failure==null?outcome:OutcomeFor(failure,outcome);
            try {await main.CompleteAsync(outcome,HasClosingFailure(failure));}
            catch(Exception close) when(failure!=null){failure.Data["MainOperationCloseFailure"]=close;}
            catch(Exception close){throw new MainOperationContinuationException<T>(result!,outcome,main.DescribeClose(outcome,false),close);}
        }
        return result!;
    }

    // Bootstrap itself admits existing config and atomically publishes config +
    // generation. Acquiring its original main pin must not invent generation first.
    internal static async Task<string> RunParticipatingBootstrapAsync(FileSystemManager files,Func<Task<string>> bootstrap)
    {
        ArgumentNullException.ThrowIfNull(files);ArgumentNullException.ThrowIfNull(bootstrap);
        await using var main=files.BeginParticipatingMainAdmission();await main.AcquireAsync();
        Exception? failure=null;string? result=null;
        try {
            result=await bootstrap();
            result=await RunBoundCoreAsync(files,result,()=>Task.FromResult(result!),null);
        } catch(Exception e){failure=e;throw;}
        finally {
            var outcome=OutcomeFor(failure);
            try {await main.CompleteAsync(outcome,HasClosingFailure(failure));}
            catch(Exception close) when(failure!=null){failure.Data["MainOperationCloseFailure"]=close;}
            catch(Exception close){throw new MainOperationContinuationException<string>(result!,outcome,main.DescribeClose(outcome,false),close);}
        }
        return result!;
    }

    // A supplied decision is captured by the original callback, never inferred
    // from an exception, status/PID or storage JSON. Later authority loss cannot
    // relabel an already established browser decision when closing its main pin.
    private static MainOperationOutcome OutcomeFor(Exception? failure, MainOperationOutcome? captured=null)=>
        failure is IMainOperationContinuationFailure known?known.EstablishedOutcome:
        captured is { } decision && decision!=MainOperationOutcome.Completed?decision:
        failure is OperationCanceledException?MainOperationOutcome.Cancelled:
        failure!=null?MainOperationOutcome.Failed:MainOperationOutcome.Completed;
    private static bool HasClosingFailure(Exception? failure)=>failure?.Data.Contains("SessionFinalizationFailure")==true;

    internal static Task RunParticipatingCurrentSessionAsync(FileSystemManager files,Func<Task> operation)=>
        RunParticipatingCurrentSessionAsync<object?>(files,async()=>{await operation();return null;});

    private static async Task<T> RunBoundBodyAsync<T>(
        FileSystemManager fileSystem,
        string expectedGeneration,
        Func<Task<T>> operation,
        FileSystemManager.CanonicalWriteLease? writeLease,
        Func<MainOperationOutcome>? capturedOutcome)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(operation);
        var normalizedRoot = NormalizeRoot(fileSystem.BasePath);
        if (string.IsNullOrWhiteSpace(expectedGeneration))
            throw new ArgumentException("A session operation requires a generation.", nameof(expectedGeneration));

        var existing = FindBinding(normalizedRoot);
        if (existing != null)
        {
            if (!string.Equals(
                    existing.ExpectedGeneration,
                    expectedGeneration,
                    StringComparison.Ordinal))
            {
                throw existing.MarkReplaced(
                    expectedGeneration,
                    "A nested session operation attempted to adopt a different generation.");
            }

            return await RunWithinBindingAsync(
                existing,
                fileSystem,
                operation,
                writeLease);
        }

        var state = new BindingState(normalizedRoot, expectedGeneration);
        var previous = CurrentFrame.Value;
        CurrentFrame.Value = new Frame(state, previous);
        Exception? operationFailure = null;
        T? establishedResult=default;
        bool established=false;
        try
        {
            establishedResult=await RunWithinBindingAsync(
                state,
                fileSystem,
                operation,
                writeLease,
                verifyAfterOperation: writeLease != null);
            established=true;
            return establishedResult;
        }
        catch (Exception failure)
        {
            // RunWithinBindingAsync has already applied replacement precedence.
            operationFailure = failure;
            throw;
        }
        finally
        {
            if (writeLease != null)
            {
                state.Close();
                CurrentFrame.Value = previous;
            }
            else
            {
                fileSystem.BeginMainOperationClosing();
                state.BeginClosing();
                try
                {
                    await fileSystem.InvokeSessionOperationClosingHookAsync();
                    await using var finalizationLease = await fileSystem.AcquireCanonicalWriteLeaseAsync(
                        CanonicalWritePurpose.SessionFinalization);
                    if (!fileSystem.IsCurrentSessionGeneration(
                            finalizationLease,
                            state.ExpectedGeneration))
                    {
                        throw state.MarkReplaced(
                            actualGeneration: null,
                            "The game session was replaced before the bound operation could close.");
                    }

                    state.Close();
                }
                catch (Exception closingFailure) when (operationFailure != null)
                {
                    // The read-only finalization lease still acquires, checks
                    // generation without recovery, and disposes normally. Retain
                    // replacement even if disposal masked its check exception.
                    var replacement = closingFailure as SessionReplacedException ??
                        operationFailure as SessionReplacedException ??
                        state.GetEstablishedReplacement(operationFailure);
                    var retained = (Exception?)replacement ?? operationFailure;
                    if (replacement != null && FindStorageDecisionFailure(operationFailure) is { } storageFailure)
                        replacement.Data["SessionOperationFailure"] = storageFailure;
                    if (!ReferenceEquals(retained, closingFailure))
                        retained.Data["SessionFinalizationFailure"] = closingFailure;
                    ExceptionDispatchInfo.Capture(retained).Throw();
                    throw;
                }
                catch (Exception closingFailure)
                {
                    closingFailure.Data["SessionFinalizationFailure"] = true;
                    if(established && closingFailure is not SessionReplacedException) {
                        var outcome=capturedOutcome?.Invoke()??MainOperationOutcome.Completed;
                        var retained=new MainOperationContinuationException<T>(establishedResult!,outcome,
                            fileSystem.DescribeMainOperationClose(outcome,true),closingFailure);
                        retained.Data["SessionFinalizationFailure"]=closingFailure;
                        throw retained;
                    }
                    if(established)closingFailure.Data["EstablishedOperationResult"]=establishedResult;
                    throw;
                }
                finally
                {
                    state.Close();
                    CurrentFrame.Value = previous;
                }
            }
        }
    }

    /// <summary>
    /// Locates a typed storage decision retained directly or within a session-replacement diagnostic chain.
    /// </summary>
    /// <param name="failure">
    /// The operation failure to inspect, or null when the operation did not fail.
    /// </param>
    /// <returns>
    /// The original uncertain or committed-save failure, or null when the chain contains no such decision.
    /// </returns>
    private static Exception? FindStorageDecisionFailure(Exception? failure)
    {
        for (var current = failure; current != null; current = current.InnerException)
        {
            if (current is CoordinatedStatePublicationUncertainException or CommittedSaveContinuationException)
                return current;
            if (current.Data["SessionOperationFailure"] is Exception recorded &&
                recorded is CoordinatedStatePublicationUncertainException or CommittedSaveContinuationException)
                return recorded;
        }
        return null;
    }

    internal static bool TryGetExpectedGeneration(
        string canonicalRoot,
        out string expectedGeneration)
    {
        var state = FindBinding(NormalizeRoot(canonicalRoot));
        if (state == null)
        {
            expectedGeneration = string.Empty;
            return false;
        }

        state.ThrowIfInvalid();
        expectedGeneration = state.ExpectedGeneration;
        return true;
    }

    internal static SessionReplacedException MarkReplaced(
        string canonicalRoot,
        string? actualGeneration,
        string message)
    {
        var state = FindBinding(NormalizeRoot(canonicalRoot));
        if (state == null)
        {
            return new SessionReplacedException(
                message,
                expectedGeneration: string.Empty,
                actualGeneration);
        }

        return state.MarkReplaced(actualGeneration, message);
    }

    private static async Task<T> RunWithinBindingAsync<T>(
        BindingState state,
        FileSystemManager fileSystem,
        Func<Task<T>> operation,
        FileSystemManager.CanonicalWriteLease? writeLease = null,
        bool verifyAfterOperation = true)
    {
        state.ThrowIfInvalid();
        try
        {
            var result = await operation();
            if (verifyAfterOperation && writeLease != null)
                fileSystem.VerifyCurrentSessionOperation(writeLease);
            else if (verifyAfterOperation)
                await fileSystem.VerifyCurrentSessionOperationAsync();
            state.ThrowIfInvalid();
            return result;
        }
        catch (SessionReplacedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            state.ThrowIfInvalid(ex);
            throw;
        }
    }

    private static BindingState? FindBinding(string normalizedRoot)
    {
        for (var frame = CurrentFrame.Value; frame != null; frame = frame.Parent)
        {
            if (RootsEqual(frame.State.NormalizedRoot, normalizedRoot))
                return frame.State;
        }

        return null;
    }

    private static string NormalizeRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Canonical root is required.", nameof(root));

        return CanonicalRootIdentityInterner.NormalizeRootKey(
            Path.GetFullPath(root), OperatingSystem.IsWindows());
    }

    private static bool RootsEqual(string left, string right) =>
        string.Equals(
            left,
            right,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private sealed record Frame(BindingState State, Frame? Parent);

    private sealed class BindingState
    {
        private readonly object _sync = new();
        private bool _closing;
        private bool _closed;
        private bool _replaced;
        private string? _actualGeneration;
        private string? _replacementMessage;

        internal BindingState(string normalizedRoot, string expectedGeneration)
        {
            NormalizedRoot = normalizedRoot;
            ExpectedGeneration = expectedGeneration;
        }

        internal string NormalizedRoot { get; }
        internal string ExpectedGeneration { get; }

        internal SessionReplacedException MarkReplaced(
            string? actualGeneration,
            string message)
        {
            lock (_sync)
            {
                _replaced = true;
                _actualGeneration ??= actualGeneration;
                _replacementMessage ??= message;
                return BuildException();
            }
        }

        internal void ThrowIfInvalid(Exception? innerException = null)
        {
            lock (_sync)
            {
                if (!_replaced && !_closing && !_closed)
                    return;

                throw BuildException(innerException);
            }
        }

        /// <summary>
        /// Returns only an established replacement, independently of the binding's normal closing or closed state.
        /// </summary>
        /// <param name="innerException">
        /// The earlier operation failure to retain as the replacement cause, or null when no cause is available.
        /// </param>
        /// <returns>
        /// A replacement failure when generation replacement was recorded, or null otherwise.
        /// </returns>
        internal SessionReplacedException? GetEstablishedReplacement(Exception? innerException)
        {
            lock (_sync)
                return _replaced ? BuildException(innerException) : null;
        }

        internal void BeginClosing()
        {
            lock (_sync)
                _closing = true;
        }

        internal void Close()
        {
            lock (_sync)
                _closed = true;
        }

        private SessionReplacedException BuildException(Exception? innerException = null)
        {
            var message = _replacementMessage ??
                          "The bound game-session operation is no longer active.";
            return new SessionReplacedException(
                message,
                ExpectedGeneration,
                _actualGeneration,
                innerException);
        }
    }
}
