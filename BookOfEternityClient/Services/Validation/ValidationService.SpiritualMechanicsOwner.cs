using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Checks exact current source and resource ownership after the original resource prefix has closed.
        /// </summary>
        /// <param name="resources">
        /// Executor whose identity must match this capture.
        /// </param>
        /// <param name="source">
        /// Source session whose identity must match this capture.
        /// </param>
        /// <returns>
        /// True when both actual owners still belong to this current capture; otherwise false.
        /// </returns>
        internal bool OwnsMechanicsOwner(AcceptedMechanicsPlanner.ResourceExecutionSession resources,
            SpiritualWoundSourceSession source) =>
            !_disposed && ReferenceEquals(_validator._spiritualOriginalTurnCapture, this) &&
            ReferenceEquals(_resources, resources) && ReferenceEquals(_source, source) &&
            _originalPrefix != null && OwnsOriginalPrefix(source, _originalPrefix);

        /// <summary>
        /// Reads the installed routing identity for source ticket freshness checks.
        /// </summary>
        /// <param name="source">
        /// Exact source owner requesting its current epoch.
        /// </param>
        /// <returns>
        /// The retained epoch object, or <see langword="null"/> when ownership does not match.
        /// </returns>
        internal object? ReadMechanicsEpoch(SpiritualWoundSourceSession source) =>
            _resources != null && OwnsMechanicsOwner(_resources, source) ? _resources.Routing?.RoutingEpoch : null;

        /// <summary>
        /// Requests the actual resource owner's current mechanics context for source validation.
        /// </summary>
        /// <param name="source">
        /// Signed source owner to authenticate against this capture.
        /// </param>
        /// <param name="issues">
        /// Receives ownership and mechanics validation failures.
        /// </param>
        /// <returns>
        /// An owned context, or <see langword="null"/> when the executor is absent or rejects the request.
        /// </returns>
        internal AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? PrepareSourceMechanics(
            SpiritualWoundSourceSession source, List<ValidationIssue> issues) =>
            _resources?.CaptureSpiritualMechanics(this, source, issues);
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        private AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? _mechanicsContext;
        private readonly Dictionary<string, AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext>
            _exchangeMechanics = new(StringComparer.Ordinal);

        /// <summary>
        /// Reads detached signed conflict membership for the exact owning capture.
        /// </summary>
        /// <param name="capture">
        /// Actual current capture bound to this source's original resource prefix.
        /// </param>
        /// <returns>
        /// A detached original conflict root; foreign or revoked ownership throws.
        /// </returns>
        internal JsonObject ReadMechanicsConflict(SpiritualOriginalTurnCapture capture)
        {
            if (!IsCurrentOwner || !ReferenceEquals(capture, _prefixCapture))
                throw new InvalidOperationException("Exact signed source and original capture required.");
            return _originalConflict.DeepClone().AsObject();
        }
    }
}
