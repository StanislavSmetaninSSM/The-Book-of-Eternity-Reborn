using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal enum MortalJointRepairPublicationDisposition
{
    RetryableHeld,
    TerminalReleased,
    Committed,
    PublicationUncertain
}

internal sealed record MortalJointRepairPublicationSettlement(
    MortalJointRepairPublicationDisposition Disposition, IReadOnlyList<ValidationIssue> Issues);

// Distinct from the old homogeneous treatment receipt. It cannot mint an old
// continuation, grant generic cache access or certify publication from presence.
internal sealed class MortalJointRepairPublicationTakeReceipt
{
    private int _consumed;
    private MortalJointRepairPublicationTakeReceipt(FileSystemManager files, object stateToken,
        string generation, long revision, MortalJointRepairPublicationPreparation preparation,
        AcceptedMechanicsPlanCache.ValidatedPublicationTakeSnapshot common,
        MortalItemAcceptedTurnAuthority.Cache.ValidatedPublicationTakeSnapshot items)
    {
        FileSystem = files; StateToken = stateToken; Generation = generation; GenerationRevision = revision;
        Preparation = preparation; Common = common; Items = items;
        Fingerprint = ComputeFingerprint();
    }
    internal FileSystemManager FileSystem { get; }
    internal object StateToken { get; }
    internal string Generation { get; }
    internal long GenerationRevision { get; }
    internal MortalJointRepairPublicationPreparation Preparation { get; }
    internal AcceptedMechanicsPlanCache.ValidatedPublicationTakeSnapshot Common { get; }
    internal MortalItemAcceptedTurnAuthority.Cache.ValidatedPublicationTakeSnapshot Items { get; }
    internal AcceptedMechanicsPlan Plan => Common.Plan;
    internal WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority Authority => Preparation.Authority;
    internal string Fingerprint { get; }
    internal bool IsConsumed => Volatile.Read(ref _consumed) != 0;

    internal static MortalJointRepairPublicationTakeReceipt Mint(object capability,
        FileSystemManager files, object stateToken, string generation, long revision,
        MortalJointRepairPublicationPreparation preparation,
        AcceptedMechanicsPlanCache.ValidatedPublicationTakeSnapshot common,
        MortalItemAcceptedTurnAuthority.Cache.ValidatedPublicationTakeSnapshot items)
    {
        if (!AcceptedTurnAuthorityRegistry.IsJointRepairPublicationReceiptCapability(capability) ||
            !ReferenceEquals(preparation.Plan, common.Plan) || common.Plan.JointRepairCommonAuthority is null ||
            items.TerminalReleaseOnly || generation != preparation.Generation)
            throw new InvalidOperationException("Only the current joint registry owner can mint this exact take receipt.");
        return new(files, stateToken, generation, revision, preparation, common, items);
    }

    internal bool HasValidSeal => !Items.TerminalReleaseOnly && Authority.HasValidSeal &&
        ReferenceEquals(Preparation.Plan, Common.Plan) && ReferenceEquals(Common.Plan.JointRepairCommonAuthority, Authority) &&
        Common.PreparedPlanFingerprint == Preparation.PreparedPlanFingerprint &&
        ComputeFingerprint() == Fingerprint;
    internal bool TryConsume(object capability) =>
        AcceptedTurnAuthorityRegistry.IsJointRepairPublicationReceiptCapability(capability) &&
        Interlocked.Exchange(ref _consumed, 1) == 0;

    private string ComputeFingerprint() => WoundAcceptedTurnFingerprintWriter.Compute(new string?[] {
        "book_of_eternity.mortal_joint_repair.publication_take_receipt.v1", Generation,
        GenerationRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Preparation.InventoryFingerprint, Common.BindingFingerprint, Common.PreparedPlanFingerprint,
        Items.PublicationFingerprint, Authority.Fingerprint, Authority.Request.RequestFingerprint,
        Authority.Resolution.ResultFingerprint, Authority.Finalization.FinalizationFingerprint });
}
