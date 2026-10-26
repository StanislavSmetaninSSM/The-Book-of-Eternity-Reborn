using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static readonly object NarrativePassIssuer = new();

    /// <summary>Runs the ordinary narrative policy once and retains its actual invocation DATA.</summary>
    /// <returns>The original issues and source observations; no engine registration or publication authority.</returns>
    internal async Task<NarrativePolicyPass> ValidateAcceptedTurnNarrativePolicyPassAsync()
    {
        using var timing = _fs.MeasureOperation("NarrativePolicy.Capture");
        var source = new NarrativeInterfaceSourceSession(_fs, NarrativePassIssuer);
        try
        {
            var issues = await ValidateAcceptedTurnNarrativePayloadInternalAsync(source: source);
            source.Complete();
            return NarrativePolicyPass.Issue(NarrativePassIssuer, this, source, issues);
        }
        catch { source.Dispose(); throw; }
    }

    /// <summary>Retains one actual N invocation as disposable DATA, never an engine admission.</summary>
    internal sealed class NarrativePolicyPass : IDisposable
    {
        private readonly ValidationService _validator;
        private readonly NarrativeInterfaceSourceSession _source;
        private ValidationIssue[] _issues;
        private bool _disposed;

        private NarrativePolicyPass(object issuer, ValidationService validator, NarrativeInterfaceSourceSession source,
            IReadOnlyList<ValidationIssue> issues)
        {
            if (!ReferenceEquals(issuer, NarrativePassIssuer)) throw new InvalidOperationException("Narrative pass requires its actual validator issuer.");
            _validator = validator; _source = source;
            _issues = issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray();
        }

        /// <summary>Issues a pass only with the enclosing validator's private invocation token.</summary>
        /// <param name="issuer">The issuer retained or checked by this operation.</param>
        /// <param name="validator">The validator retained or checked by this operation.</param>
        /// <param name="source">Explicit invocation observations, or null for the unchanged ordinary route.</param>
        /// <param name="issues">The issues retained or checked by this operation.</param>
        internal static NarrativePolicyPass Issue(object issuer, ValidationService validator,
            NarrativeInterfaceSourceSession source, IReadOnlyList<ValidationIssue> issues) => new(issuer, validator, source, issues);

        /// <summary>Gets independent copies of the original ordered ordinary issues.</summary>
        internal IReadOnlyList<ValidationIssue> Issues => Array.AsReadOnly(_issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray());

        /// <summary>Rejects an expired pass or a different issuing validator.</summary>
        /// <param name="validator">The validator retained or checked by this operation.</param>
        internal void RequireIssuer(ValidationService validator)
        {
            RequireLive();
            if (!ReferenceEquals(validator, _validator)) throw new InvalidDataException("narrative_policy_pass_foreign_validator");
        }

        /// <summary>Matches actual supplied or physically selected original context DATA to the engine's authenticated frame.</summary>
        /// <param name="sessionId">The session id retained or checked by this operation.</param>
        /// <param name="requestId">The request id retained or checked by this operation.</param>
        /// <param name="snapshotToken">The snapshot token retained or checked by this operation.</param>
        /// <param name="turn">The turn retained or checked by this operation.</param>
        internal void RequireOriginalContext(string sessionId, string requestId, string snapshotToken, int turn)
        {
            RequireLive(); _source.RequireOriginalContext(sessionId, requestId, snapshotToken, turn);
        }

        /// <summary>Freshly checks retained source DATA without another semantic evaluation, projection, write or clock sample.</summary>
        /// <param name="files">The original invocation manager.</param>
        /// <param name="lease">The consumer's currently active original lease.</param>
        /// <param name="authenticatedInterfaceTransition">Only the engine-authenticated same-iteration Published I record, or null before FIRST I.</param>
        internal Task RequireCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalJointInterfacePublication.Record? authenticatedInterfaceTransition = null)
        {
            RequireLive(); return _source.RequireCurrentAsync(files, lease, authenticatedInterfaceTransition);
        }

        /// <summary>Copies all recorded occurrence and failure diagnostics without returning mutable retained buffers.</summary>
        internal NarrativeSourceDiagnostic CaptureDiagnostic() => _source.CaptureDiagnostic();

        /// <summary>Refuses use after explicit pass disposal.</summary>
        private void RequireLive()
        {
            if (_disposed) throw new InvalidDataException("narrative_policy_pass_expired");
        }

        /// <summary>Invalidates this pass and releases retained source buffers. This cannot clear an engine's refusal flag.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _issues = []; _source.Dispose();
        }
    }
}
