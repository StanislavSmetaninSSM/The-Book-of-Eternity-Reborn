using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Describes the existing scenario switches relevant to an explicit test storage parent.
/// </summary>
/// <param name="Windows">
/// Whether the scenario selects Windows.
/// </param>
/// <param name="ExtendedWindowsCrash">
/// Whether the existing shared Windows crash observation window is selected.
/// </param>
/// <param name="PackageSelected">
/// Whether the explicit five-cut package is selected.
/// </param>
/// <param name="RawSeedSelected">
/// Whether a legacy raw seed artifact is also selected.
/// </param>
/// <param name="AcquisitionOnly">
/// Whether the distinct DATA acquisition probe is selected.
/// </param>
/// <param name="ManagedCapture">
/// Whether the existing managed capture owns the destination.
/// </param>
/// <param name="Mode">
/// The existing cut, diagnostic and integrity-fault selection.
/// </param>
internal sealed record JointInterfaceCrashStorageSelection(bool Windows, bool ExtendedWindowsCrash,
    bool PackageSelected, bool RawSeedSelected, bool AcquisitionOnly, bool ManagedCapture,
    JointInterfaceCrashMode Mode);

/// <summary>
/// Selects test-owned crash storage without changing application file validation.
/// </summary>
internal static class JointInterfaceCrashStorage
{
    /// <summary>
    /// Selects the original temporary branch or an explicitly requested ordinary Windows package parent.
    /// </summary>
    /// <param name="temporaryParent">
    /// The original temporary directory used when <paramref name="requestedParent"/> is <see langword="null"/>.
    /// </param>
    /// <param name="requestedParent">
    /// An explicit existing ordinary parent, or <see langword="null"/> for unchanged placement.
    /// </param>
    /// <param name="nonce">
    /// The fresh lowercase hexadecimal GUID suffix chosen by the caller.
    /// </param>
    /// <param name="selection">
    /// The non-null existing scenario switches and mode; an override requires the ordinary Windows five-cut package alone.
    /// </param>
    /// <returns>
    /// The selected child name without allocating storage. Explicit selection refuses an observed existing name.
    /// This observation does not promise protection against a hostile concurrent computer owner.
    /// </returns>
    internal static string SelectOwnedRoot(string temporaryParent, string? requestedParent, string nonce,
        JointInterfaceCrashStorageSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(selection.Mode);
        if (requestedParent is null)
            return Path.Combine(temporaryParent, "boe-joint-interface-crash-" + nonce);

        if (!selection.Windows || !selection.ExtendedWindowsCrash || !selection.PackageSelected ||
            selection.RawSeedSelected || selection.AcquisitionOnly || selection.ManagedCapture ||
            selection.Mode.Cut is < 0 or > 4 || selection.Mode.Diagnostic || selection.Mode.Fault is not null)
            throw new InvalidDataException("An explicit crash parent requires only the ordinary Windows five-cut package selection.");
        if (string.IsNullOrWhiteSpace(requestedParent) || !Path.IsPathFullyQualified(requestedParent))
            throw new InvalidDataException("The explicit crash parent must be an absolute ordinary directory.");
        if (nonce.Length != 32 || nonce.Any(value => value is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidDataException("The explicit crash child requires a lowercase hexadecimal GUID suffix.");

        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(requestedParent));
        var scope = new TrustedLocalFileScope([parent]);
        scope.ValidateDirectory(parent, allowMissing: false);
        var candidate = Path.Combine(parent, "boe-joint-interface-crash-" + nonce);
        if (scope.ObserveNamespace(candidate).Kind != TrustedLocalNamespaceKind.Missing)
            throw new IOException("The selected crash child already exists: " + candidate);
        return candidate;
    }
}
