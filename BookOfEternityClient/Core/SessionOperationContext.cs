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

            return await RunWithinBindingAsync(existing, fileSystem, operation);
        }

        var state = new BindingState(normalizedRoot, expectedGeneration);
        var previous = CurrentFrame.Value;
        CurrentFrame.Value = new Frame(state, previous);
        try
        {
            return await RunWithinBindingAsync(
                state,
                fileSystem,
                operation,
                verifyAfterOperation: false);
        }
        finally
        {
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
            finally
            {
                state.Close();
                CurrentFrame.Value = previous;
            }
        }
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
        bool verifyAfterOperation = true)
    {
        state.ThrowIfInvalid();
        try
        {
            var result = await operation();
            if (verifyAfterOperation)
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

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
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
