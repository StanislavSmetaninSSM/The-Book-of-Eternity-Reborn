using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

/// <summary>
/// Contains passive operation counters; none of these values establishes authority, eligibility or data validity.
/// Root transition totals observe invalidations directly rather than inferring them from cache misses.
/// </summary>
internal sealed record RecoveryValidationMetrics
{
    /// <summary>
    /// Gets the unique diagnostic segment identity; it grants no authority.
    /// </summary>
    public Guid OperationId { get; init; }

    /// <summary>
    /// Gets functional DATA requests dispatched through this segment.
    /// </summary>
    public long Requests { get; init; }

    /// <summary>
    /// Gets actual original DATA body invocations, including refusals.
    /// </summary>
    public long Sources { get; init; }

    /// <summary>
    /// Gets successful requests completed without invoking their DATA body.
    /// </summary>
    public long Reuses { get; init; }

    /// <summary>
    /// Gets requests routed directly to source because the segment or lease was ineligible.
    /// </summary>
    public long FreshFallbacks { get; init; }

    /// <summary>
    /// Gets source invocations in an explicit fresh boundary or ineligible fallback.
    /// </summary>
    public long FreshSources { get; init; }

    /// <summary>
    /// Gets explicit fresh-boundary requests, including requests after failure closure.
    /// </summary>
    public long FreshBoundaries { get; init; }

    /// <summary>
    /// Gets zero before failure retirement or one after it; repeated refusals do not create another segment.
    /// </summary>
    public int FailureClosures { get; init; }

    /// <summary>
    /// Gets observed root epoch transitions before supported mutation effects, including other root managers.
    /// </summary>
    public long MutationBegins { get; init; }

    /// <summary>
    /// Gets observed root epoch transitions after supported mutation cleanup, including other root managers.
    /// </summary>
    public long MutationEnds { get; init; }

    /// <summary>
    /// Gets currently registered root mutation identities at this snapshot.
    /// </summary>
    public int ActiveMutations { get; init; }

    /// <summary>
    /// Gets whether the root generation revision differs from the segment opening revision.
    /// </summary>
    public bool GenerationChanged { get; init; }

    /// <summary>
    /// Gets whether this recovery scope has been disposed.
    /// </summary>
    public bool Closed { get; init; }

    /// <summary>
    /// Gets retained DATA and dependency counts plus general-service storage closure.
    /// </summary>
    public required ValidationOperationState State { get; init; }

}
