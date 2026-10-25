namespace BookOfEternityClient.Core;

/// <summary>
/// Supplies explicit positive-directory DATA without granting namespace or storage authority.
/// </summary>
internal interface IReadPathObservations : IWindowsPathMetadataProvider
{
    /// <summary>
    /// Gets this context while its original lifetime remains eligible, otherwise <see langword="null"/>.
    /// </summary>
    IReadPathObservations? Active { get; }

    /// <summary>
    /// Classifies an exact component after the caller's original grant checks.
    /// </summary>
    /// <param name="path">
    /// Exact original component spelling; only positive directories may be retained.
    /// </param>
    /// <returns>
    /// Original entry DATA; files, absence, failures and ineligible lifetimes remain fresh.
    /// </returns>
    TrustedLocalFileScope.EntryKind ObserveTrusted(string path);

    /// <summary>
    /// Selects directory sharing only for the original manager's eligible admission.
    /// </summary>
    /// <param name="owner">
    /// Exact manager requesting admission.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if sharing remains eligible; otherwise, <see langword="false"/>.
    /// </returns>
    bool CanShareAdmission(FileSystemManager owner);

    /// <summary>
    /// Classifies an admission component while distinguishing source attempts from retained DATA.
    /// </summary>
    /// <param name="path">
    /// Exact original component spelling selected after grant validation.
    /// </param>
    /// <param name="sourceQueried">
    /// Receives <see langword="true"/> when the original classifier was attempted, including failure.
    /// </param>
    /// <returns>
    /// Original classification without file-access authority.
    /// </returns>
    TrustedLocalFileScope.EntryKind ObserveForAdmission(string path, out bool sourceQueried);
}
