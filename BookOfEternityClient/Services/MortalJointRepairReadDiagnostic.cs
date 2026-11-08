namespace BookOfEternityClient.Services;

/// <summary>
/// Retains private failure DATA for an explicit local diagnostic consumer, never for issue routing or authority.
/// No public properties or exception inheritance expose this payload through ordinary JSON, logs or exception walkers.
/// </summary>
internal sealed class MortalJointRepairReadDiagnostic
{
    private const string DataKey = "MortalJointRepair.ReadDiagnostic";
    private readonly ValidationIssue[] _issues;
    private readonly ValidationIssue[] _sourceIssues;

    /// <summary>Retains already detached data and exact error objects for an explicit local consumer.</summary>
    /// <param name="origin">Real source boundary.</param>
    /// <param name="issues">Detached outward issues.</param>
    /// <param name="sourceIssues">Detached earlier producer issues.</param>
    /// <param name="caughtException">Original caught failure.</param>
    /// <param name="captureFailure">Diagnostic-copy failure when capture was incomplete.</param>
    private MortalJointRepairReadDiagnostic(string origin, ValidationIssue[] issues, ValidationIssue[] sourceIssues,
        Exception? caughtException, Exception? captureFailure)
    {
        Origin = origin;
        _issues = issues;
        _sourceIssues = sourceIssues;
        CaughtException = caughtException;
        CaptureFailure = captureFailure;
    }

    /// <summary>Gets the named real read or mapped-refusal boundary.</summary>
    internal string Origin { get; }

    /// <summary>Gets independently detached copies of the unchanged outward issues in original order.</summary>
    internal IReadOnlyList<ValidationIssue> Issues => _issues.Select(CloneIssue).ToArray();

    /// <summary>Gets earlier producer issues retained separately from the outward mapped refusal.</summary>
    internal IReadOnlyList<ValidationIssue> SourceIssues => _sourceIssues.Select(CloneIssue).ToArray();

    /// <summary>Gets the exact exception caught by the original read, without joining the routed exception graph.</summary>
    internal Exception? CaughtException { get; }

    /// <summary>Gets a diagnostic-copy failure, if the original issues could not be detached.</summary>
    internal Exception? CaptureFailure { get; }

    /// <summary>Detaches issue DATA including both nested mutable item carrier paths.</summary>
    /// <param name="issue">Existing issue whose public and private fields must be preserved.</param>
    /// <returns>An independent copy with no item-carrier path shared with its source.</returns>
    private static ValidationIssue CloneIssue(ValidationIssue issue)
    {
        var clone = WoundAcceptedTurnData.CloneIssue(issue);
        if (clone.MortalItemRepairContext is { } context)
            clone.MortalItemRepairContext = context with
            {
                SourceCarrier = CloneCarrier(context.SourceCarrier),
                DestinationCarrier = CloneCarrier(context.DestinationCarrier)
            };
        return clone;
    }

    /// <summary>Copies the carrier's mutable path without altering its coordinates.</summary>
    /// <param name="carrier">Optional original source or destination carrier.</param>
    /// <returns>The same coordinate values with an independent path collection.</returns>
    private static MortalItemCarrierCoordinate? CloneCarrier(MortalItemCarrierCoordinate? carrier) =>
        carrier is null ? null : carrier with { ContainerPath = carrier.ContainerPath.ToArray() };

    /// <summary>Copies only existing issue DATA; a failed copy records its failure instead of changing refusal behavior.</summary>
    /// <param name="origin">Named source boundary.</param>
    /// <param name="issues">Original outward issues, never reordered or rewritten.</param>
    /// <param name="sourceIssues">Earlier issues discarded by an existing mapped-refusal branch, if any.</param>
    /// <param name="caughtException">Exact originally caught exception, if any.</param>
    /// <returns>Private detached diagnostics, or an explicit incomplete-capture payload.</returns>
    internal static MortalJointRepairReadDiagnostic Capture(string origin, IReadOnlyList<ValidationIssue> issues,
        IReadOnlyList<ValidationIssue>? sourceIssues, Exception? caughtException)
    {
        try
        {
            return new(origin, issues.Select(CloneIssue).ToArray(),
                sourceIssues?.Select(CloneIssue).ToArray() ?? [], caughtException, null);
        }
        catch (Exception failure) { return new(origin, [], [], caughtException, failure); }
    }

    /// <summary>Obtains existing producer issues inside the same best-effort diagnostic boundary.</summary>
    /// <param name="origin">Named actual refusal boundary.</param>
    /// <param name="issues">Unchanged outward issues.</param>
    /// <param name="readSourceIssues">Getter for already-computed producer issues, not a filesystem or authority callback.</param>
    /// <param name="caughtException">Exact original caught exception, if any.</param>
    /// <returns>Detached diagnostics, or an explicit getter-failure payload without replacing the original refusal.</returns>
    internal static MortalJointRepairReadDiagnostic CaptureFromSource(string origin, IReadOnlyList<ValidationIssue> issues,
        Func<IReadOnlyList<ValidationIssue>?> readSourceIssues, Exception? caughtException)
    {
        try { return Capture(origin, issues, readSourceIssues(), caughtException); }
        catch (Exception failure) { return new(origin, [], [], caughtException, failure); }
    }

    /// <summary>Attaches private DATA without changing the original exception type, message, inner exception or stack.</summary>
    /// <param name="failure">The existing exception that will be thrown by its normal caller.</param>
    internal void AttachTo(Exception failure)
    {
        try { failure.Data[DataKey] = this; }
        catch { /* Diagnostic attachment must never replace the established failure. */ }
    }

    /// <summary>Reads only this typed payload from one exception without traversing or reclassifying its cause.</summary>
    /// <param name="failure">The exception inspected by an explicit local diagnostic consumer.</param>
    /// <returns>The private DATA payload, or no diagnostics for an ordinary exception.</returns>
    internal static MortalJointRepairReadDiagnostic? From(Exception failure) =>
        failure.Data[DataKey] as MortalJointRepairReadDiagnostic;
}
