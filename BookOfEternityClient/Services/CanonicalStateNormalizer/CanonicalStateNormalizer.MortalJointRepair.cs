namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    internal sealed class JointRepairPublicationPreflight
    {
        private JointRepairPublicationPreflight(AcceptedMechanicsNormalizationPreflight.Validated validated,
            MortalJointRepairPublicationPreparation preparation)
        { Validated = validated; Preparation = preparation; }
        internal AcceptedMechanicsNormalizationPreflight.Validated Validated { get; }
        internal MortalJointRepairPublicationPreparation Preparation { get; }

        internal static async Task<JointRepairPublicationPreflight> CreateAsync(CanonicalStateNormalizer owner,
            AcceptedMechanicsNormalizationPreflight.Validated validated)
        {
            var lease = owner._writeLease ?? throw new InvalidOperationException("Joint coordinator requires the participating issuing lease.");
            var preparation = await MortalJointRepairPublicationPreparation.CreateAsync(owner._fs, lease,
                validated.Plan, validated.Binding);
            return new(validated, preparation);
        }
    }

    // The distinct coordinator must retain this SAME bound lease while building,
    // taking, publishing, reading back and settling. The ordinary coordinator's
    // separately acquired lease cannot inherit the mechanical admission proof.
    internal async Task<JointRepairPublicationPreflight?> PrevalidateJointRepairPublicationTransactionAsync()
    {
        var preflight = await PrevalidateAcceptedMechanicsBeforeNormalizationAsync();
        return preflight is AcceptedMechanicsNormalizationPreflight.Validated validated &&
            validated.Plan.JointRepairCommonAuthority is not null
            ? await JointRepairPublicationPreflight.CreateAsync(this, validated) : null;
    }

    internal Task<AcceptedTurnAuthorityRegistry.JointRepairPublicationTakeResult> TakeJointRepairPublicationAsync(
        JointRepairPublicationPreflight preflight) =>
        AcceptedTurnAuthorityRegistry.TakeJointRepairPublicationAsync(_fs,
            _writeLease ?? throw new InvalidOperationException("Joint take requires its participating issuing lease."),
            preflight.Preparation);
}
