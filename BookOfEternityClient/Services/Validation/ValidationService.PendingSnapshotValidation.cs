namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Runs one pending-snapshot validation body; the forwarding baseline exposes repeated authentication to regression tests.
    /// </summary>
    /// <param name="validate">
    /// Consumer operation whose issues are returned.
    /// </param>
    /// <returns>
    /// The consumer's ordered issues.
    /// </returns>
    private Task<List<ValidationIssue>> RunPendingSnapshotValidationAsync(Func<Task<List<ValidationIssue>>> validate) => validate();
}
