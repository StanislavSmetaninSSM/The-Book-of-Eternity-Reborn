namespace BookOfEternityClient.Core;

/// <summary>
/// Contains detached diagnostic text from one retained joint-publication issue.
/// </summary>
/// <param name="Code">
/// The issue code, or <see langword="null"/> when the original issue has no code.
/// </param>
/// <param name="FilePath">
/// The original diagnostic file spelling.
/// </param>
/// <param name="Message">
/// The full original diagnostic message.
/// </param>
internal sealed record MortalJointRuntimeIssue(string? Code, string FilePath, string Message);

/// <summary>
/// Reports existing engine flags and detached diagnostics without returning a checkpoint, plan, receipt or capability.
/// </summary>
/// <param name="CheckpointPresent">
/// Whether the engine retains a joint repair checkpoint.
/// </param>
/// <param name="CorrectionReady">
/// Whether the engine's existing correction-ready flag is set.
/// </param>
/// <param name="CanonicalRefreshPresent">
/// Whether an existing canonical refresh result is retained.
/// </param>
/// <param name="CanonicalDisposition">
/// The retained refresh disposition text, or <see langword="null"/> when no refresh exists.
/// </param>
/// <param name="Issues">
/// Full detached issue text, or <see langword="null"/> when no refresh exists.
/// </param>
/// <param name="FreshCheckpointPresent">
/// Whether the existing fresh interface checkpoint marker is present.
/// </param>
/// <param name="ReadyPresent">
/// Whether the engine retains an interface-ready owner.
/// </param>
/// <param name="FreshConsumed">
/// Whether the existing fresh-consumed flag is set.
/// </param>
/// <param name="PreparationHeld">
/// Whether the existing interface preparation-held flag is set.
/// </param>
/// <param name="NarrativeContribution">
/// Last detached N contribution disposition, including unavailable historical N on v3 resume.
/// This text never grants source or publication authority.
/// </param>
internal sealed record MortalJointRuntimeObservation(bool CheckpointPresent, bool CorrectionReady,
    bool CanonicalRefreshPresent, string? CanonicalDisposition, IReadOnlyList<MortalJointRuntimeIssue>? Issues,
    bool FreshCheckpointPresent, bool ReadyPresent, bool FreshConsumed, bool PreparationHeld,
    string NarrativeContribution);

public partial class GameEngine
{
    /// <summary>
    /// Copies existing joint-publication flags and diagnostic text from the current lifecycle execution context.
    /// It performs no filesystem admission, authority initialization, ownership change or concurrent snapshot fencing.
    /// </summary>
    /// <returns>
    /// Detached diagnostic DATA; its flags cannot authorize publication or replace current proof checks.
    /// </returns>
    internal MortalJointRuntimeObservation CaptureMortalJointRuntimeObservation()
    {
        var refresh = _mortalJointRepairCanonicalRefresh;
        return new(_mortalJointRepairCheckpoint is not null, _mortalJointRepairCorrectionReady,
            refresh is not null, refresh?.Disposition.ToString(), refresh is null ? null :
                Array.AsReadOnly(refresh.Issues.Select(issue => new MortalJointRuntimeIssue(
                    issue.Code, issue.FilePath, issue.Message)).ToArray()),
            _mortalJointInterfaceFreshCheckpoint is not null, _mortalJointInterfaceReady is not null,
            _mortalJointInterfaceFreshConsumed, _mortalJointInterfacePreparationHeld, _mortalJointNarrativeDisposition);
    }
}
