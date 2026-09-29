using System.Text.Json;

namespace BookOfEternityClient.Services;

/// <summary>
/// Journals resource IDs through their real typed operation keys without changing resource execution.
/// The owning capture remains responsible for committing only successfully validated execution.
/// </summary>
internal sealed partial class SpiritualWoundResourceIdentityFactory : AcceptedMechanicsIdentityFactory
{
    private readonly SpiritualWoundReplayJournal _journal;
    private readonly AcceptedMechanicsIdentityFactory _underlying;

    /// <summary>
    /// Binds an attempt's replay stream to the ordinary owner allocation factory.
    /// </summary>
    /// <param name="journal">
    /// Non-null recording, append or strict replay stream for this capture attempt.
    /// </param>
    /// <param name="underlying">
    /// Non-null ordinary factory called only for genuinely new allocations.
    /// </param>
    internal SpiritualWoundResourceIdentityFactory(
        SpiritualWoundReplayJournal journal, AcceptedMechanicsIdentityFactory underlying)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
    }

    /// <summary>
    /// Rejects allocation lacking its typed causal operation.
    /// </summary>
    /// <returns>
    /// Never returns an unjournaled identity.
    /// </returns>
    internal override string CreateOperationId() => RejectUnbound();

    /// <summary>
    /// Rejects transition allocation lacking its typed causal operation.
    /// </summary>
    /// <returns>
    /// Never returns an unjournaled identity.
    /// </returns>
    internal override string CreateTransitionId() => RejectUnbound();

    /// <summary>
    /// Retains or replays an ordinary resource mutation operation identity.
    /// </summary>
    /// <param name="intent">
    /// Non-null mutation whose complete typed key identifies the allocation.
    /// </param>
    /// <returns>
    /// Exact recorded or newly generated operation identity.
    /// </returns>
    internal override string CreateOperationId(ResourceMutationIntent intent) => Allocate(
        "resource_operation", "resource_mutation_key", () => intent.Key,
        () => _underlying.CreateOperationId(intent));

    /// <summary>
    /// Retains or replays an ordinary resource mutation transition identity.
    /// </summary>
    /// <param name="intent">
    /// Non-null mutation whose complete typed key identifies the allocation.
    /// </param>
    /// <returns>
    /// Exact recorded or newly generated transition identity.
    /// </returns>
    internal override string CreateTransitionId(ResourceMutationIntent intent) => Allocate(
        "resource_transition", "resource_mutation_key", () => intent.Key,
        () => _underlying.CreateTransitionId(intent));

    /// <summary>
    /// Retains or replays a resource capacity operation identity.
    /// </summary>
    /// <param name="intent">
    /// Non-null capacity intent whose complete typed key identifies the allocation.
    /// </param>
    /// <returns>
    /// Exact recorded or newly generated operation identity.
    /// </returns>
    internal override string CreateOperationId(ResourceCapacityIntent intent) => Allocate(
        "resource_operation", "resource_capacity_key", () => intent.Key,
        () => _underlying.CreateOperationId(intent));

    /// <summary>
    /// Retains or replays a resource capacity transition identity.
    /// </summary>
    /// <param name="intent">
    /// Non-null capacity intent whose complete typed key identifies the allocation.
    /// </param>
    /// <returns>
    /// Exact recorded or newly generated transition identity.
    /// </returns>
    internal override string CreateTransitionId(ResourceCapacityIntent intent) => Allocate(
        "resource_transition", "resource_capacity_key", () => intent.Key,
        () => _underlying.CreateTransitionId(intent));

    /// <summary>
    /// Compares a domain-separated complete key while leaving the generated ID random.
    /// </summary>
    /// <typeparam name="TKey">
    /// Typed allocation coordinate serialized by its actual owner contract.
    /// </typeparam>
    /// <param name="kind">
    /// Journal allocation family.
    /// </param>
    /// <param name="domain">
    /// Separate comparison domain for the allocation coordinate family.
    /// </param>
    /// <param name="readKey">
    /// Reads the exact owner allocation coordinate; failure revokes the journal attempt.
    /// </param>
    /// <param name="allocate">
    /// Ordinary underlying factory callback, never called during strict replay.
    /// </param>
    /// <returns>
    /// Retained or newly generated identity.
    /// </returns>
    private string Allocate<TKey>(string kind, string domain, Func<TKey> readKey, Func<string> allocate)
    {
        try
        {
            var key = JsonSerializer.SerializeToNode(readKey())!.AsObject();
            var coordinate = SpiritualWoundStateJson.Hash(key, domain);
            return _journal.Request(kind, "resources", coordinate, allocate);
        }
        catch
        {
            _journal.Invalidate();
            throw;
        }
    }

    /// <summary>
    /// Revokes the attempt instead of falling back to an unbound random call.
    /// </summary>
    /// <returns>
    /// Never returns a value.
    /// </returns>
    private string RejectUnbound()
    {
        _journal.Invalidate();
        throw new InvalidOperationException("A typed resource operation is required for journaled allocation.");
    }
}
