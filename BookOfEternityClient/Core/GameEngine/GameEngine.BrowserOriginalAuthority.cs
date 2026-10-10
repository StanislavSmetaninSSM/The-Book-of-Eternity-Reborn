using BookOfEternityClient.Services;
namespace BookOfEternityClient.Core;
public partial class GameEngine
{
    internal static PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload ValidateOriginalBrowserAuthority(
        PendingTurnSnapshotManifest manifest,string authority,Func<string,byte[]?> read)
    {
        if(!PendingTurnSnapshotAuthority.TryValidateManifestForDestructiveAuthority(manifest,authority,SnapshotHashJsonOpts,
            static m=>m.ManifestPayloadHash,static (m,h)=>m.ManifestPayloadHash=h,
            static m=>m.SessionId,static m=>m.RequestId,static m=>m.TurnNumber,
            static m=>m.Files,static m=>m.SnapshotFileHashes,static m=>m.ClientOwnedValidationHashes,
            static m=>m.RollbackBaselineFiles,static m=>m.SourceLabel,static m=>m.RollbackBackups,read,out var payload,out _)
            || payload==null)throw BrowserOriginalMainCondition.Invalid();
        return payload;
    }
}
