using BookOfEternityClient.Services;
using System.Runtime.ExceptionServices;
namespace BookOfEternityClient.Core;
public partial class GameEngine
{
    internal static PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload ValidateOriginalBrowserAuthority(
        PendingTurnSnapshotManifest manifest,string authority,Func<string,byte[]?> read)
    {
        Exception? readFailure=null;
        byte[]? ReadOriginal(string path) { try{return read(path);} catch(Exception failure){readFailure??=failure;throw;} }
        var valid=PendingTurnSnapshotAuthority.TryValidateManifestForDestructiveAuthority(manifest,authority,SnapshotHashJsonOpts,
            static m=>m.ManifestPayloadHash,static (m,h)=>m.ManifestPayloadHash=h,
            static m=>m.SessionId,static m=>m.RequestId,static m=>m.TurnNumber,
            static m=>m.Files,static m=>m.SnapshotFileHashes,static m=>m.ClientOwnedValidationHashes,
            static m=>m.RollbackBaselineFiles,static m=>m.SourceLabel,static m=>m.RollbackBackups,ReadOriginal,out var payload,out _);
        if(readFailure!=null)ExceptionDispatchInfo.Capture(readFailure).Throw();
        if(!valid || payload==null)throw BrowserOriginalMainCondition.Invalid();
        return payload!;
    }
    internal static void ValidateCompletedBrowserReceipt(PendingPlayerActionService.State state,PendingTurnSnapshotManifest manifest,
        Func<string,byte[]?> read,Func<string,bool> exists,string[] physicalInventory)
    {
        if(state.Phase=="accepted")ValidateAcceptedBrowserRecordCoreAsync(state,manifest,
            path=>Task.FromResult(read(path)),exists,physicalInventory).GetAwaiter().GetResult();
        else if(state.Phase=="settled")ValidateRestoredBrowserSettlementCoreAsync(PendingPlayerActionService.ReadStaged(state),manifest,
            path=>Task.FromResult(read(path)),exists,physicalInventory).GetAwaiter().GetResult();
        else throw BrowserOriginalMainCondition.Invalid();
    }
}
