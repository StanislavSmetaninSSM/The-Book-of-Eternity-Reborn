using System.Runtime.CompilerServices;
using BookOfEternityClient.Services.Caching;

namespace BookOfEternityClient.Services;

/// <summary>
/// Owns temporary computation results for one caller-defined operation.
/// </summary>
internal sealed class ValidationOperation : IDisposable
{
    private readonly object _gate = new();
    private ServiceCacheStorage<EntryKey, Entry>? _storage;
    private readonly AsyncLocal<Frame?> _flow = new();
    private readonly AsyncLocal<FreshScope?> _freshFlow = new();
    private readonly Dictionary<object, OperationStructuralDependency> _structuralDependencies =
        new(ReferenceEqualityComparer.Instance);
    private Frame? _top;
    private FreshScope? _fresh;
    private bool _closed;

    /// <summary>
    /// Creates an initially empty operation context.
    /// </summary>
    /// <param name="storage">
    /// Exclusively owned storage for this context, never shared with another operation.
    /// </param>
    internal ValidationOperation(ServiceCacheStorage<EntryKey, Entry> storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        storage.ClaimValidationOperation();
        _storage = storage;
    }

    /// <summary>
    /// Reports whether the current thread holds the coordinator monitor for typed diagnostics.
    /// </summary>
    internal bool IsStateMonitorHeld => Monitor.IsEntered(_gate);

    /// <summary>
    /// Binds one complete structural capture policy to an exact owner for this operation's lifetime.
    /// </summary>
    /// <param name="inputIdentity">
    /// Exact owner identity, compared only by reference, for one complete structural capture.
    /// </param>
    /// <param name="capture">
    /// The complete current structural capture for that owner; subsequent requests must supply this exact delegate.
    /// </param>
    /// <returns>
    /// The operation-owned provider already bound to the owner, or a newly registered provider.
    /// </returns>
    internal OperationStructuralDependency GetStructuralDependency(object inputIdentity,
        Func<OperationStructuralSnapshot?> capture)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(inputIdentity);
            ArgumentNullException.ThrowIfNull(capture);
            lock (_gate)
            {
                RequireOpen();
                if (!ReferenceEquals(_top, _flow.Value) || !ReferenceEquals(_fresh, _freshFlow.Value))
                    throw new InvalidOperationException("Structural dependencies require the operation's active serial flow.");
                if (_structuralDependencies.TryGetValue(inputIdentity, out var dependency))
                {
                    if (!dependency.HasCapturePolicy(capture))
                        throw new InvalidOperationException("An operation owner cannot replace its structural capture policy.");
                    return dependency;
                }
                dependency = new OperationStructuralDependency(capture);
                _structuralDependencies.Add(inputIdentity, dependency);
                return dependency;
            }
        }
        catch { InvalidateAfterFailure(); throw; }
    }

    /// <summary>
    /// Clears prior results and selects fresh computation without cache insertion throughout a nested scope.
    /// </summary>
    /// <returns>
    /// A scope that the caller disposes after all of its nested computations settle.
    /// </returns>
    internal IDisposable ForceFresh()
    {
        try
        {
            FreshScope scope;
            lock (_gate)
            {
                RequireOpen();
                if (!ReferenceEquals(_top, _flow.Value) || !ReferenceEquals(_fresh, _freshFlow.Value))
                    throw new InvalidOperationException("A fresh scope requires the operation's active serial flow.");
                InvalidateAndPoison();
                scope = new FreshScope(this, _fresh);
                _fresh = scope;
            }
            _freshFlow.Value = scope;
            ClearEntries();
            return scope;
        }
        catch { InvalidateAfterFailure(); throw; }
    }

    /// <summary>
    /// Captures retained payload counts without exporting input or result references.
    /// </summary>
    /// <returns>
    /// The actual retained entry/dependency counts and the context's closed state.
    /// </returns>
    internal ValidationOperationState CaptureState()
    {
        ServiceCacheStorage<EntryKey, Entry> storage;
        lock (_gate)
        {
            if (_closed) return new(0, 0, true);
            storage = _storage!;
        }
        KeyValuePair<EntryKey, Entry>[] entries;
        try { entries = storage.Cache.GetSnapshot(); }
        catch
        {
            lock (_gate) { if (_closed) return new(0, 0, true); }
            Dispose();
            throw;
        }
        lock (_gate)
        {
            if (_closed) return new(0, 0, true);
            var domains = new HashSet<IOperationDependency>(ReferenceEqualityComparer.Instance);
            foreach (var entry in entries)
                foreach (var dependency in entry.Value.Versions) domains.Add(dependency.Key);
            for (var frame = _top; frame is not null; frame = frame.Parent)
                foreach (var dependency in frame.Versions.Keys) domains.Add(dependency);
            return new(entries.Length, domains.Count, false);
        }
    }

    /// <summary>
    /// Computes or reuses synchronous DATA through the shared operation protocol without waiting for asynchronous work.
    /// </summary>
    /// <typeparam name="TResult">
    /// The registered result type, including permitted null and default values.
    /// </typeparam>
    /// <param name="computation">
    /// Exact registered computation identity and defensive copy policy.
    /// </param>
    /// <param name="inputIdentity">
    /// Complete stable input and policy identity.
    /// </param>
    /// <param name="dependencies">
    /// Complete ordered direct dependency declaration.
    /// </param>
    /// <param name="compute">
    /// Original synchronous source invoked only when an eligible result is unavailable.
    /// </param>
    /// <returns>
    /// A detached successful result under the same ownership, dependency and failure checks as asynchronous requests.
    /// </returns>
    internal TResult GetOrCompute<TResult>(OperationComputation<TResult> computation, object inputIdentity,
        IReadOnlyList<IOperationDependency> dependencies, Func<TResult> compute)
    {
        if (compute is null)
        {
            InvalidateAfterFailure();
            throw new ArgumentNullException(nameof(compute));
        }
        var request = GetOrLookupCoreAsync(computation, inputIdentity, dependencies,
            _ => Task.FromResult(compute()));
        if (!request.IsCompleted)
        {
            InvalidateAfterFailure();
            throw new InvalidOperationException("A synchronous computation did not settle synchronously.");
        }
        return request.GetAwaiter().GetResult().Value;
    }

    /// <summary>
    /// Reuses or computes a detached typed result under complete input and dependency declarations.
    /// </summary>
    /// <typeparam name="TResult">
    /// The computation result type, which may legitimately contain its default value.
    /// </typeparam>
    /// <param name="computation">
    /// Stable registered algorithm and copy policy.
    /// </param>
    /// <param name="inputIdentity">
    /// Stable identity covering the complete input and policy; mutable inputs need live validation or versions.
    /// </param>
    /// <param name="dependencies">
    /// Complete ordered direct provider/domain declarations, or an empty list for immutable input alone.
    /// </param>
    /// <param name="compute">
    /// Original computation invoked when a reusable value is unavailable.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed before and after the original computation.
    /// </param>
    /// <returns>
    /// A detached successful result, including legitimate default values; failed computations are never retained.
    /// </returns>
    internal async Task<TResult> GetOrComputeAsync<TResult>(OperationComputation<TResult> computation,
        object inputIdentity, IReadOnlyList<IOperationDependency> dependencies,
        Func<CancellationToken, Task<TResult>> compute, CancellationToken cancellationToken = default)
    {
        if (compute is null)
        {
            InvalidateAfterFailure();
            throw new ArgumentNullException(nameof(compute));
        }
        return (await GetOrLookupCoreAsync(computation, inputIdentity, dependencies, compute, cancellationToken).ConfigureAwait(false)).Value;
    }

    /// <summary>
    /// Shares exact dependency, copying and serial ownership rules between computing and noncomputing requests.
    /// </summary>
    /// <typeparam name="TResult">
    /// Registered result type, including legitimate default values.
    /// </typeparam>
    /// <param name="computation">
    /// Exact computation identity and copy policy.
    /// </param>
    /// <param name="inputIdentity">
    /// Complete stable input and policy identity.
    /// </param>
    /// <param name="dependencies">
    /// Ordered complete direct dependency declaration.
    /// </param>
    /// <param name="compute">
    /// Original source computation, or <see langword="null"/> for a noncomputing lookup.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed throughout lookup, source computation and copying.
    /// </param>
    /// <returns>
    /// A detached found result, or a miss when no source was requested and no eligible entry exists.
    /// </returns>
    private async ValueTask<OperationLookup<TResult>> GetOrLookupCoreAsync<TResult>(OperationComputation<TResult> computation,
        object inputIdentity, IReadOnlyList<IOperationDependency> dependencies,
        Func<CancellationToken, Task<TResult>>? compute, CancellationToken cancellationToken = default)
    {
        var parent = _flow.Value;
        Frame? frame = null;
        try
        {
            ArgumentNullException.ThrowIfNull(computation);
            ArgumentNullException.ThrowIfNull(inputIdentity);
            ArgumentNullException.ThrowIfNull(dependencies);
            var declaration = dependencies.ToArray();
            foreach (var dependency in declaration) ArgumentNullException.ThrowIfNull(dependency);
            var key = new EntryKey(computation, inputIdentity);
            frame = Enter(key, parent);
            _flow.Value = frame;
            cancellationToken.ThrowIfCancellationRequested();

            var versions = CaptureVersions(declaration, out var eligible);
            ServiceCacheStorage<EntryKey, Entry> storage;
            bool lookup;
            lock (_gate)
            {
                RequireActive(frame);
                frame.Eligible &= eligible;
                MergeVersions(frame, versions);
                storage = _storage!;
                lookup = frame.Eligible && frame.Fresh is null;
            }
            Entry? entry = null;
            if (lookup && storage.Cache.TryGet(key, out var candidate))
            {
                lock (_gate)
                {
                    RequireActive(frame);
                    if (frame.Eligible && frame.Fresh is null && candidate is not null &&
                        EntryKeyComparer.Same(key, candidate.Key) && SameDeclaration(declaration, candidate.Declaration))
                        entry = candidate;
                }
            }
            if (entry is not null && VersionsAreCurrent(entry.Versions))
            {
                bool reuse;
                lock (_gate)
                {
                    RequireActive(frame);
                    reuse = frame.Eligible && frame.Fresh is null;
                    if (reuse) MergeVersions(frame, entry.Versions);
                }
                if (reuse)
                {
                    var reused = computation.Copy((TResult)entry.Value!);
                    cancellationToken.ThrowIfCancellationRequested();
                    RequireCurrent(frame);
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (_gate)
                    {
                        RequireActive(frame);
                        if (frame.Eligible && frame.Fresh is null) return new(true, reused);
                    }
                }
                // Callbacks may retire a selected hit while still permitting an uncached fresh computation.
            }

            if (compute is null)
            {
                RequireCurrent(frame);
                cancellationToken.ThrowIfCancellationRequested();
                return new(false, default!);
            }

            // A stale logical key is replaced, never retained once for every historical version.
            lock (_gate)
            {
                RequireActive(frame);
            }
            storage.Cache.Remove(key);
            var computed = await compute(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            RequireCurrent(frame);
            bool retain;
            lock (_gate) retain = frame.Eligible && frame.Fresh is null;
            var stored = computation.Copy(computed);
            var result = retain ? computation.Copy(stored) : stored;
            cancellationToken.ThrowIfCancellationRequested();
            RequireCurrent(frame);
            Entry? replacement = null;
            lock (_gate)
            {
                RequireActive(frame);
                if (retain && frame.Eligible && frame.Fresh is null)
                    replacement = new Entry(key, stored, declaration, frame.Versions.ToArray());
            }
            if (replacement is not null)
            {
                storage.Cache.Set(key, replacement);
                bool remove;
                lock (_gate)
                {
                    RequireActive(frame);
                    remove = !frame.Eligible || frame.Fresh is not null;
                }
                if (remove) storage.Cache.Remove(key);
            }
            RequireCurrent(frame);
            cancellationToken.ThrowIfCancellationRequested();
            return new(true, result);
        }
        catch
        {
            InvalidateAfterFailure();
            throw;
        }
        finally
        {
            if (frame is not null) Finish(frame);
            _flow.Value = parent;
        }
    }

    /// <summary>
    /// Looks up an eligible detached result without invoking a source computation.
    /// </summary>
    /// <typeparam name="TResult">
    /// Registered result type, including permitted null and default values.
    /// </typeparam>
    /// <param name="computation">
    /// Exact registered computation and copy policy.
    /// </param>
    /// <param name="inputIdentity">
    /// Complete stable input and policy identity.
    /// </param>
    /// <param name="dependencies">
    /// Exact ordered direct provider declarations required by the stored result.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed before and after lookup and copying.
    /// </param>
    /// <returns>
    /// A found detached value or a miss without computing a replacement.
    /// </returns>
    internal Task<OperationLookup<TResult>> TryGetAsync<TResult>(OperationComputation<TResult> computation,
        object inputIdentity, IReadOnlyList<IOperationDependency> dependencies, CancellationToken cancellationToken = default) =>
        GetOrLookupCoreAsync(computation, inputIdentity, dependencies, null, cancellationToken).AsTask();

    /// <summary>
    /// Records only a validation that completes normally without a refusal.
    /// </summary>
    /// <param name="rule">
    /// Stable identity of the throw-on-failure validation.
    /// </param>
    /// <param name="inputIdentity">
    /// Complete stable identity of its inputs and policy.
    /// </param>
    /// <param name="dependencies">
    /// Complete ordered direct dependency declarations.
    /// </param>
    /// <param name="validate">
    /// Actual validation, which must throw on refusal rather than return a discarded Boolean.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed before and after validation.
    /// </param>
    /// <returns>
    /// A task completing only after a successful validation result is obtained.
    /// </returns>
    internal async Task ValidateAsync(OperationValidationRule rule, object inputIdentity,
        IReadOnlyList<IOperationDependency> dependencies, Func<CancellationToken, Task> validate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(validate);
        await GetOrComputeAsync(rule.Computation, inputIdentity, dependencies, async token =>
        {
            await validate(token);
            return new ValidationSucceeded();
        }, cancellationToken);
    }

    /// <summary>
    /// Clears operation-owned references and prevents late requests or in-flight work from retaining a result.
    /// </summary>
    public void Dispose()
    {
        ServiceCacheStorage<EntryKey, Entry>? storage;
        lock (_gate) storage = Close();
        DisposeStorage(storage);
        _flow.Value = null;
        _freshFlow.Value = null;
    }

    /// <summary>
    /// Enters one serial request without mistaking inherited routing for current ownership.
    /// </summary>
    /// <param name="key">
    /// Exact registered algorithm and immutable or independently guarded input identity.
    /// </param>
    /// <param name="parent">
    /// Caller-flow parent, which must still be the operation's active top frame.
    /// </param>
    /// <returns>
    /// A new request frame owned by this flow.
    /// </returns>
    private Frame Enter(EntryKey key, Frame? parent)
    {
        lock (_gate)
        {
            RequireOpen();
            if (!ReferenceEquals(_top, parent) || !ReferenceEquals(_fresh, _freshFlow.Value))
                throw new InvalidOperationException("Overlapping or stale operation consumers cannot share a request flow.");
            for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
                if (ancestor.Key is { } active && EntryKeyComparer.Same(active, key))
                    throw new InvalidOperationException("A computation cannot recursively request its own pending key.");
            return _top = new Frame(parent, key, _fresh);
        }
    }

    /// <summary>
    /// Captures declared versions without holding an operation monitor while calling providers.
    /// </summary>
    /// <param name="declaration">
    /// Detached ordered direct dependency identities.
    /// </param>
    /// <param name="eligible">
    /// Whether every declared source supplied an eligible non-null version token.
    /// </param>
    /// <returns>
    /// All known eligible source versions; an unknown domain prevents retention rather than granting success.
    /// </returns>
    private static Dictionary<IOperationDependency, object> CaptureVersions(IOperationDependency[] declaration,
        out bool eligible)
    {
        var versions = new Dictionary<IOperationDependency, object>(ReferenceEqualityComparer.Instance);
        eligible = true;
        foreach (var dependency in declaration)
        {
            if (!dependency.TryCaptureVersion(out var version) || version is null)
            {
                eligible = false;
                continue;
            }
            if (versions.TryGetValue(dependency, out var previous) && !ReferenceEquals(previous, version))
                throw new InvalidOperationException("A source version changed while its declaration was captured.");
            versions[dependency] = version;
        }
        return versions;
    }

    /// <summary>
    /// Compares dependency declarations by stable provider/domain identity and original order.
    /// </summary>
    /// <param name="left">
    /// The current request's detached declaration.
    /// </param>
    /// <param name="right">
    /// The declaration that issued an earlier result.
    /// </param>
    /// <returns>
    /// True only when both lists contain the same identities in the same order.
    /// </returns>
    private static bool SameDeclaration(IOperationDependency[] left, IOperationDependency[] right)
    {
        if (left.Length != right.Length) return false;
        for (var index = 0; index < left.Length; index++)
            if (!ReferenceEquals(left[index], right[index])) return false;
        return true;
    }

    /// <summary>
    /// Observes every retained direct or transitive token without holding an operation lock.
    /// </summary>
    /// <param name="versions">
    /// Detached expected source versions.
    /// </param>
    /// <returns>
    /// True only when every provider remains eligible at the exact same version.
    /// </returns>
    private static bool VersionsAreCurrent(IEnumerable<KeyValuePair<IOperationDependency, object>> versions)
    {
        foreach (var pair in versions)
            if (!pair.Key.TryCaptureVersion(out var current) || !ReferenceEquals(current, pair.Value)) return false;
        return true;
    }

    /// <summary>
    /// Imports child or direct versions under the operation monitor, detecting incompatible observations.
    /// </summary>
    /// <param name="frame">
    /// Current destination frame.
    /// </param>
    /// <param name="versions">
    /// Detached versions supplied by a dependency capture or successful child hit.
    /// </param>
    private static void MergeVersions(Frame frame, IEnumerable<KeyValuePair<IOperationDependency, object>> versions)
    {
        foreach (var pair in versions)
        {
            if (frame.Versions.TryGetValue(pair.Key, out var previous) && !ReferenceEquals(previous, pair.Value))
                throw new InvalidOperationException("Nested computations observed different versions of one source domain.");
            frame.Versions[pair.Key] = pair.Value;
        }
    }

    /// <summary>
    /// Checks both live frame ownership and all known versions before a value can escape or be stored.
    /// </summary>
    /// <param name="frame">
    /// Exact request frame whose source observations must still apply.
    /// </param>
    private void RequireCurrent(Frame frame)
    {
        KeyValuePair<IOperationDependency, object>[] versions;
        lock (_gate)
        {
            RequireActive(frame);
            versions = frame.Versions.ToArray();
        }
        if (!VersionsAreCurrent(versions))
            throw new InvalidOperationException("A computation's source version changed before completion.");
        lock (_gate) RequireActive(frame);
    }

    /// <summary>
    /// Checks a request's exact active identities while the operation monitor is held.
    /// </summary>
    /// <param name="frame">
    /// Expected serial request frame.
    /// </param>
    private void RequireActive(Frame frame)
    {
        RequireOpen();
        if (!ReferenceEquals(_top, frame) || !ReferenceEquals(_flow.Value, frame) ||
            !ReferenceEquals(_fresh, _freshFlow.Value) || frame.Fresh is { Closed: true })
            throw new InvalidOperationException("The computation lost its active operation or fresh-scope flow.");
    }

    /// <summary>
    /// Refuses use after close while the operation monitor is held.
    /// </summary>
    private void RequireOpen() => ObjectDisposedException.ThrowIf(_closed, this);

    /// <summary>
    /// Clears structural comparison evidence and prevents active ancestors from republishing prior successes.
    /// Active structural observations receive a new token after recapture and therefore require a new request.
    /// </summary>
    private void InvalidateAndPoison()
    {
        for (var frame = _top; frame is not null; frame = frame.Parent) frame.Eligible = false;
        foreach (var dependency in _structuralDependencies.Values) dependency.ClearCapturedSnapshot();
    }

    /// <summary>
    /// Clears retained results outside the coordinator monitor.
    /// </summary>
    private void ClearEntries()
    {
        ServiceCacheStorage<EntryKey, Entry>? storage;
        lock (_gate) storage = _storage;
        storage?.Cache.Clear();
    }

    /// <summary>
    /// Invalidates a failed request without allowing cleanup to replace its primary error.
    /// </summary>
    private void InvalidateAfterFailure()
    {
        lock (_gate) InvalidateAndPoison();
        try { ClearEntries(); }
        catch { Dispose(); }
    }

    /// <summary>
    /// Propagates dependencies and eligibility to the parent and removes completed-frame payload references.
    /// </summary>
    /// <param name="frame">
    /// The settled request frame, including one whose computation failed.
    /// </param>
    private void Finish(Frame frame)
    {
        ServiceCacheStorage<EntryKey, Entry>? retired = null;
        lock (_gate)
        {
            if (!_closed)
            {
                if (!ReferenceEquals(_top, frame)) retired = Close();
                else
                {
                    var parent = frame.Parent;
                    if (parent is not null)
                    {
                        parent.Eligible &= frame.Eligible;
                        foreach (var pair in frame.Versions)
                        {
                            if (parent.Versions.TryGetValue(pair.Key, out var previous) && !ReferenceEquals(previous, pair.Value))
                                parent.Eligible = false;
                            else parent.Versions[pair.Key] = pair.Value;
                        }
                    }
                    _top = parent;
                }
            }
            frame.Clear();
        }
        DisposeStorage(retired);
    }

    /// <summary>
    /// Releases a fresh scope without allowing an out-of-order close to revive an older context.
    /// </summary>
    /// <param name="scope">
    /// Exact fresh scope being disposed.
    /// </param>
    private void ReleaseFresh(FreshScope scope)
    {
        FreshScope? previous;
        ServiceCacheStorage<EntryKey, Entry>? retired = null;
        lock (_gate)
        {
            previous = scope.Previous;
            if (!_closed && (!ReferenceEquals(_fresh, scope) || !ReferenceEquals(_freshFlow.Value, scope))) retired = Close();
            if (!_closed) _fresh = previous;
            scope.Retire();
        }
        DisposeStorage(retired);
        _freshFlow.Value = _closed ? null : previous;
    }

    /// <summary>
    /// Atomically clears operation-owned references without disposing caller-owned values or throwing on cleanup.
    /// </summary>
    /// <returns>
    /// The retired storage owner to dispose after releasing the coordinator monitor, or null after an earlier close.
    /// </returns>
    private ServiceCacheStorage<EntryKey, Entry>? Close()
    {
        if (_closed) return null;
        _closed = true;
        var storage = _storage;
        _storage = null;
        foreach (var dependency in _structuralDependencies.Values) dependency.Retire();
        _structuralDependencies.Clear();
        var frame = _top;
        _top = null;
        while (frame is not null)
        {
            var parent = frame.Parent;
            frame.Clear();
            frame = parent;
        }
        var scope = _fresh;
        _fresh = null;
        while (scope is not null)
        {
            var previous = scope.Previous;
            scope.Retire();
            scope = previous;
        }
        return storage;
    }

    /// <summary>
    /// Retires an owned backend outside the coordinator monitor without masking a primary failure.
    /// </summary>
    /// <param name="storage">
    /// Exclusively owned retired storage, or null if already released.
    /// </param>
    private static void DisposeStorage(ServiceCacheStorage<EntryKey, Entry>? storage)
    {
        try { storage?.Dispose(); }
        catch { /* The context and backend are closed; preserve the caller's primary error. */ }
    }

    /// <summary>
    /// Keys results by object identity without invoking mutable input equality or content hashing.
    /// </summary>
    /// <param name="Computation">
    /// Registered algorithm descriptor instance.
    /// </param>
    /// <param name="Input">
    /// Complete stable input and policy identity.
    /// </param>
    internal readonly record struct EntryKey(object Computation, object Input)
    {
        /// <inheritdoc />
        public bool Equals(EntryKey other) => EntryKeyComparer.Same(this, other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(Computation), RuntimeHelpers.GetHashCode(Input));
    }

    /// <summary>
    /// Compares and hashes only stable reference identities of a logical key.
    /// </summary>
    internal sealed class EntryKeyComparer : IEqualityComparer<EntryKey>
    {
        /// <inheritdoc />
        public bool Equals(EntryKey x, EntryKey y) => Same(x, y);

        /// <inheritdoc />
        public int GetHashCode(EntryKey obj) => HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Computation), RuntimeHelpers.GetHashCode(obj.Input));

        /// <summary>
        /// Compares two request keys without calling input equality implementations.
        /// </summary>
        /// <param name="left">
        /// First request key.
        /// </param>
        /// <param name="right">
        /// Second request key.
        /// </param>
        /// <returns>
        /// True only for the exact same algorithm and input objects.
        /// </returns>
        internal static bool Same(EntryKey left, EntryKey right) =>
            ReferenceEquals(left.Computation, right.Computation) && ReferenceEquals(left.Input, right.Input);
    }

    /// <summary>
    /// Retains only a detached completed value, detached declaration and exact dependency tokens.
    /// </summary>
    /// <param name="Key">
    /// Exact original algorithm/input identities, checked even if a configured backend merges keys.
    /// </param>
    /// <param name="Value">
    /// Owned immutable or defensively copied result, including a legitimate null.
    /// </param>
    /// <param name="Declaration">
    /// Detached ordered direct provider/domain identities.
    /// </param>
    /// <param name="Versions">
    /// Detached complete direct and transitive versions; no computation delegate is retained.
    /// </param>
    internal sealed record Entry(EntryKey Key, object? Value, IOperationDependency[] Declaration,
        KeyValuePair<IOperationDependency, object>[] Versions);

    /// <summary>
    /// Tracks one serial request until its dependencies have been propagated and its temporary references cleared.
    /// </summary>
    /// <param name="parent">
    /// Exact active caller frame, or null at the operation root.
    /// </param>
    /// <param name="key">
    /// Stable current algorithm and input identities.
    /// </param>
    /// <param name="fresh">
    /// Fresh scope active on entry, or null for ordinary reuse eligibility.
    /// </param>
    private sealed class Frame(Frame? parent, EntryKey key, FreshScope? fresh)
    {
        internal Frame? Parent = parent;
        internal EntryKey? Key = key;
        internal FreshScope? Fresh = fresh;
        internal bool Eligible = true;
        internal readonly Dictionary<IOperationDependency, object> Versions = new(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// Removes request-owned input, parent and dependency references on settlement or operation close.
        /// </summary>
        internal void Clear()
        {
            Parent = null;
            Key = null;
            Fresh = null;
            Eligible = false;
            Versions.Clear();
            Versions.TrimExcess();
        }
    }

    /// <summary>
    /// Keeps a fresh subtree distinct from ordinary reuse while holding no computation payload itself.
    /// </summary>
    /// <param name="owner">
    /// Context that issued this scope.
    /// </param>
    /// <param name="previous">
    /// Enclosing fresh scope, or null at the fresh root.
    /// </param>
    private sealed class FreshScope(ValidationOperation owner, FreshScope? previous) : IDisposable
    {
        private ValidationOperation? _owner = owner;
        internal FreshScope? Previous = previous;
        internal bool Closed;

        /// <summary>
        /// Retires scope-owned routing references without changing the caller's operation result.
        /// </summary>
        internal void Retire()
        {
            Closed = true;
            _owner = null;
            Previous = null;
        }

        /// <summary>
        /// Releases this scope once; cleanup never replaces a primary computation failure.
        /// </summary>
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ReleaseFresh(this);
    }
}

/// <summary>
/// Reports operation-owned retained payload counts without retaining those payloads itself.
/// </summary>
/// <param name="Entries">
/// Number of completed values or validation facts retained by the operation.
/// </param>
/// <param name="Dependencies">
/// Number of distinct source domains retained by its current entries and active frames.
/// </param>
/// <param name="Closed">
/// Whether the owning operation has been closed and no further result may escape it.
/// </param>
internal sealed record ValidationOperationState(int Entries, int Dependencies, bool Closed);
