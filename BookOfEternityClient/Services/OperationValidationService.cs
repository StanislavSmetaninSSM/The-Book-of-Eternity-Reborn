using BookOfEternityClient.Services.Caching;

namespace BookOfEternityClient.Services;

/// <summary>
/// Defines creation of independent temporary validation contexts.
/// </summary>
internal abstract class OperationValidationService
{
    /// <summary>
    /// Creates a context whose caller must dispose at the end of its whole operation.
    /// </summary>
    /// <returns>
    /// A new context with no relationship to results from earlier operations.
    /// </returns>
    internal abstract ValidationOperation BeginOperation();
}

/// <summary>
/// Creates contexts with configured, exclusively owned cache mechanisms.
/// </summary>
internal sealed class DefaultOperationValidationService : OperationValidationService
{
    private readonly Func<ServiceCache<ValidationOperation.EntryKey, ValidationOperation.Entry>> _cacheFactory;

    /// <summary>
    /// Selects how each context creates its own storage mechanism.
    /// </summary>
    /// <param name="cacheFactory">
    /// Returns an unclaimed backend per operation; null selects an identity-keyed dictionary.
    /// </param>
    internal DefaultOperationValidationService(Func<ServiceCache<ValidationOperation.EntryKey, ValidationOperation.Entry>>? cacheFactory = null) =>
        _cacheFactory = cacheFactory ?? (() => new DictionaryCache<ValidationOperation.EntryKey, ValidationOperation.Entry>(new ValidationOperation.EntryKeyComparer()));

    /// <inheritdoc />
    internal override ValidationOperation BeginOperation()
    {
        var storage = new ServiceCacheStorage<ValidationOperation.EntryKey, ValidationOperation.Entry>(_cacheFactory());
        try { return new ValidationOperation(storage); }
        catch
        {
            try { storage.Dispose(); }
            catch { /* Do not replace the construction failure with a secondary backend cleanup failure. */ }
            throw;
        }
    }
}

/// <summary>
/// Describes one stable algorithm and the policy used to detach its values.
/// </summary>
/// <typeparam name="TResult">
/// The result type, including legitimate default or null values allowed by the algorithm.
/// </typeparam>
internal sealed class OperationComputation<TResult>
{
    /// <summary>
    /// Registers an algorithm identity and its defensive copy contract.
    /// </summary>
    /// <param name="name">
    /// Nonempty descriptive algorithm name; this descriptor instance supplies its stable identity.
    /// </param>
    /// <param name="revision">
    /// Positive algorithm version; a changed algorithm requires a new descriptor.
    /// </param>
    /// <param name="copy">
    /// Detaches mutable values or returns immutable values unchanged, including permitted null values.
    /// The registered policy must not capture operation-owned payloads in a longer-lived descriptor.
    /// </param>
    internal OperationComputation(string name, int revision, Func<TResult, TResult> copy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(revision, 1);
        ArgumentNullException.ThrowIfNull(copy);
        Name = name;
        Revision = revision;
        Copy = copy;
    }

    /// <summary>
    /// Gets the descriptive algorithm name.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Gets the registered positive algorithm revision.
    /// </summary>
    internal int Revision { get; }

    /// <summary>
    /// Gets the copy policy that detaches retained and returned values.
    /// </summary>
    internal Func<TResult, TResult> Copy { get; }
}

/// <summary>
/// Identifies a stable provider and domain whose version can be inspected in memory.
/// </summary>
internal interface IOperationDependency
{
    /// <summary>
    /// Captures an identity token for the provider's current eligible version.
    /// </summary>
    /// <param name="version">
    /// A non-null, never-reused token on success; otherwise no reusable version is available.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the domain is eligible and the token represents its current version;
    /// otherwise <see langword="false"/>.
    /// </returns>
    bool TryCaptureVersion(out object? version);
}

/// <summary>
/// Identifies a throw-on-failure validation separately from a computed predicate.
/// </summary>
internal sealed class OperationValidationRule
{
    /// <summary>
    /// Creates a stable validation identity.
    /// </summary>
    /// <param name="name">
    /// Nonempty descriptive validation name.
    /// </param>
    /// <param name="revision">
    /// Positive version of the validation algorithm.
    /// </param>
    internal OperationValidationRule(string name, int revision) =>
        Computation = new OperationComputation<ValidationSucceeded>(name, revision, value => value);

    /// <summary>
    /// Gets the distinct successful-validation computation identity.
    /// </summary>
    internal OperationComputation<ValidationSucceeded> Computation { get; }
}

/// <summary>
/// Represents normal completion of a validation that throws on refusal.
/// </summary>
internal readonly record struct ValidationSucceeded;
