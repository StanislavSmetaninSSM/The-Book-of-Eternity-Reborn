using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Detached comparison DATA only. Never grants publication authority.
    internal sealed record MortalJointAuditPreview(string InputFingerprint,
        string PreparedPlanFingerprint, string InventoryFingerprint);

    internal Task<MortalJointAuditPreview> CaptureCurrentMortalJointAuditPreviewAsync() =>
        throw new NotSupportedException("Joint audit preview lifecycle is not implemented.");
}
