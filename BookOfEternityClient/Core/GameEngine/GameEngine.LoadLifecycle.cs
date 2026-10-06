using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Core;
public partial class GameEngine
{
    private int _consoleLoadInFlight;
    private GmLoadMainState _consoleLoadMainState=GmLoadMainState.NoActiveSession;

    private async Task<LoadReplacementResult> LoadSelectedSaveWithMainLifecycleAsync(string source)
    {
        if(_blockedLoadContinuation is { } blocked)return blocked;
        if(Interlocked.CompareExchange(ref _consoleLoadInFlight,1,0)!=0)
            return new(LoadReplacementDisposition.NotLoaded,null,null,false,new InvalidOperationException("Загрузка уже выполняется."));
        GmLoadSessionOperation? original=null;
        LoadReplacementResult result=new(LoadReplacementDisposition.NotLoaded,null,null,false,null);
        try {
            original=GmLoadSessionOperation.Reserve(_fs,Guid.NewGuid().ToString("N"),source,null);
            await original.StopAsync();
            _consoleLoadMainState=original.State;
            using(var admission=_fs.BeginMainAdmission()) {
                await admission.AcquireAsync(quiescentOnly:true);
                await original.ValidateLoadGenerationAsync();
                result=await LoadSelectedSaveAndRebindRuntimeAsync(source);
                result=await PrepareLoadedConsoleContinuationAsync(result);
            }
            var refreshed=result.Disposition==LoadReplacementDisposition.Committed && !result.ContinuationBlocked && !result.NeedsFollowUp;
            _consoleLoadMainState=await original.FinishAsync(refreshed,refreshed,result.EstablishedGeneration);
            if(original.HadSession && _consoleLoadMainState!=GmLoadMainState.Running)
                result=result.WithFollowUp(new InvalidOperationException("Исходный ГМ завершён; запуск нового ГМа не подтверждён."),true);
        } catch(Exception failure) {
            _consoleLoadMainState=original?.State??GmLoadMainState.Refused;
            result=result.WithFollowUp(failure,true);
            if(original?.State is GmLoadMainState.Stopped or GmLoadMainState.NoActiveSession)
                try {await original.FinishAsync(false,false,result.EstablishedGeneration);}catch {_consoleLoadMainState=GmLoadMainState.Uncertain;}
        } finally {
            if(original!=null)await original.DisposeAsync();
            Interlocked.Exchange(ref _consoleLoadInFlight,0);
        }
        RetainConsoleLoadBlock(result);
        return result;
    }
}
