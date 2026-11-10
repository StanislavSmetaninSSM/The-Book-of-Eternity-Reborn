using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static class PreparedRemoteOutcomeScenario
{
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    internal static async Task ExerciseAsync(string mode,FileSystemManager seed,GmSessionRunCoordinator owner,
        object host,Type hostType,Func<Task> stop,Dictionary<string,object?> evidence)
    {
        const string member="game_state/prepared-remote.bin";
        byte[] before=[17,29,41],after=[53,67,79],foreign=[83,97,101];
        var target=seed.ResolvePath(member);
        var journal=Path.Combine(seed.BasePath,".boe_runtime/trusted-local-publication-v1/active.json");
        var lockPath=seed.ResolvePath(LocalUiSessionLockService.LockPath);
        byte[]? journalAtCut=null,lockAtPrepare=null;int cuts=0,commits=0,applies=0,readsAfter=0,mutationsAfter=0;
        bool unknownCut=false;FileSystemManager.MainAdmission? original=null;MainOperationClose? admitted=null;
        var receipts=new List<MainOperationClose>();var cleanup=new List<string>();
        BrowserPreparedWriteResult? result=null;
        var hook=hostType.GetField("BeforeMainCloseReply",BindingFlags.Instance|BindingFlags.NonPublic)!;
        try
        {
            Require(owner.Record.Disposition==GmSessionRunDisposition.Running,"Original neutral main is not Running.");
            await owner.RunOperationAsync(async()=>{await using var lease=await seed.AcquireCanonicalWriteLeaseAsync();
                await seed.WriteFileAtomicBytesAsync(lease,member,before);return 0;});
            Require(GmSessionRunCoordinator.Current==null,"Tested call inherited a local main pin.");
            hook.SetValue(host,(Func<MainOperationClose,Task>)(close=>{receipts.Add(close);return Task.CompletedTask;}));
            var files=new FileSystemManager(seed.BasePath,NullLogger<FileSystemManager>.Instance,PhysicalLoadTransactionOperations.Instance,new FileSystemManagerHooks {
                BeforeCanonicalReadOpenAsync=_=>{if(unknownCut)readsAfter++;return Task.CompletedTask;},
                BeforeCanonicalMutationBoundaryAsync=_=>{if(unknownCut)mutationsAfter++;return Task.CompletedTask;},
                LocalPublicationObserver=(phase,index)=>{
                    if(phase is not (TrustedLocalPublicationPhase.MemberPublished or TrustedLocalPublicationPhase.Committed))return;
                    using var doc=JsonDocument.Parse(File.ReadAllBytes(journal));var members=doc.RootElement.GetProperty("Members");
                    if(members.GetArrayLength()!=1||members[0].GetProperty("Path").GetString()!=target)return;
                    if(phase==TrustedLocalPublicationPhase.Committed){commits++;return;}
                    Require(index==0&&File.ReadAllBytes(target).SequenceEqual(after),"Wrong actual prepared member/image.");
                    Require(++cuts==1,"Repeated actual prepared cut.");journalAtCut=File.ReadAllBytes(journal);
                    if(mode=="unknown"){File.WriteAllBytes(target,foreign);unknownCut=true;}
                    if(mode!="committed")throw new InvalidOperationException("Actual nontransient prepared publication cut.");
                }
            });
            var coordinator=new BrowserLocalWriteCoordinator(files,new LocalUiSessionLockService(files));
            result=await coordinator.ExecutePreparedAsync(new BrowserLocalWriteRequest("remote-settings","Remote settings","Prepared publication"),lease=>{
                Require(GmSessionRunCoordinator.Current==null,"Prepare inherited a local main pin.");
                for(var frame=lease.MainAdmission;frame!=null;frame=frame.Parent)if(frame.OwnsRemote){original=frame;break;}
                Require(original is {WasRemote:true,OwnsRemote:true}&&original.Root==files.BasePath,"Actual owning remote admission missing.");
                admitted=original!.DescribeClose(MainOperationOutcome.Completed,false);
                Require(admitted!=null&&GmSessionRunValidation.IdentityMatches(admitted.Identity,owner.Identity)&&
                    files.ReadExistingSessionGeneration(lease)==owner.Identity.GenerationId,"Original admission generation/identity changed.");
                lockAtPrepare=File.ReadAllBytes(lockPath);
                return Task.FromResult(new PreparedBrowserLocalWrite([new(member,before,after)],()=>{applies++;return Task.CompletedTask;}));
            });
            // Raw decision and actual ACK are captured before desired outcome assertions or fixture repair.
            var close=original?.TerminalClose;
            evidence["PreparedRemote"]=new {Mode=mode,Result=result,Cuts=cuts,CommittedPhases=commits,ApplyCount=applies,
                WasRemote=original?.WasRemote,OwnsRemote=original?.OwnsRemote,Admitted=admitted,TerminalClose=close,
                CloseObserved=original?.CloseObserved,ServerReceipts=receipts.ToArray(),
                OriginalQuery=close==null?null:owner.QueryRemoteOperation(close),
                Target=File.ReadAllBytes(target),LockAtPrepare=lockAtPrepare,
                LockAfter=File.Exists(lockPath)?File.ReadAllBytes(lockPath):null,JournalAtCut=journalAtCut,
                JournalAfter=File.Exists(journal)?File.ReadAllBytes(journal):null,
                LaterPreOpenReadHookAttempts=readsAfter,LaterMutationAttempts=mutationsAfter};
            Require(cuts==1&&original is {WasRemote:true,CloseObserved:true}&&close!=null&&admitted!=null,"Original publication/remote ACK not reached.");
            Require(receipts.Count==1&&receipts[0]==close&&close.PinId==admitted.PinId&&close.CloseId==admitted.CloseId&&
                close.OperationId==admitted.OperationId&&GmSessionRunValidation.IdentityMatches(close.Identity,admitted.Identity)&&
                !close.ClosingFailed&&owner.QueryRemoteOperation(close).State==MainOperationState.ClosedObserved,"Original receipt identity/retirement changed.");
            var expected=mode=="unknown"?BrowserPreparedWriteDisposition.Uncertain:mode=="rollback"?BrowserPreparedWriteDisposition.RolledBack:BrowserPreparedWriteDisposition.Committed;
            Require(result.Disposition==expected&&applies==(mode=="committed"?1:0),"Actual publication result/application wrong.");
            Require(File.ReadAllBytes(target).SequenceEqual(mode=="unknown"?foreign:mode=="rollback"?before:after),"Actual prepared bytes wrong.");
            if(mode=="unknown")Require(result.NeedsFollowUp&&readsAfter==0&&mutationsAfter==0&&
                File.ReadAllBytes(journal).SequenceEqual(journalAtCut!)&&File.ReadAllBytes(lockPath).SequenceEqual(lockAtPrepare!),"Unknown evidence/stop changed.");
            else Require(!File.Exists(journal)&&!File.Exists(lockPath)&&commits==(mode=="committed"?1:0),"Known publication/lock cleanup missing.");
            var expectedClose=mode=="unknown"?MainOperationOutcome.Uncertain:mode=="rollback"?MainOperationOutcome.RolledBack:MainOperationOutcome.Committed;
            Require(close.Outcome==expectedClose,"Prepared actual disposition was replaced in original remote terminal close.");
        }
        finally
        {
            hook.SetValue(host,null);
            try {
                if(mode=="unknown"&&File.Exists(journal)){
                    evidence["PreparedFixtureExactBeforeRepairAfterReceiptCapture"]=evidence.ContainsKey("PreparedRemote");
                    File.WriteAllBytes(target,before);
                    await owner.RunOperationAsync(async()=>{await using var lease=await seed.AcquireCanonicalWriteLeaseAsync();return 0;});
                }
            }catch(Exception failure){cleanup.Add("owned journal cleanup: "+failure);}
            try {await stop();evidence["OriginalPreparedOwnerRetired"]=owner.Record.Disposition==GmSessionRunDisposition.Stopped&&!owner.RetainsAuthority;
                Require((bool)evidence["OriginalPreparedOwnerRetired"]!,"Original owner did not retire.");}
            catch(Exception failure){cleanup.Add("original main: "+failure);}
            if(cleanup.Count!=0)evidence["PreparedCleanupFailures"]=cleanup;
        }
        Require(cleanup.Count==0,"Prepared original owner cleanup failed.");
    }
}
