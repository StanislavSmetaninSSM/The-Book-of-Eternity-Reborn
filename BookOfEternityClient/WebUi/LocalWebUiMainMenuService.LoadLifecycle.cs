using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.WebUi;
public sealed partial class LocalWebUiMainMenuService
{
    private sealed class BrowserLoad(string id)
    {
        internal readonly string Id=id;
        internal readonly object Gate=new();
        internal readonly TaskCompletionSource<BrowserLoadSaveResultDto> Ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Applied=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task<BrowserLoadSaveResultDto> Execution=null!;
        internal BrowserLoadSaveResultDto? Retained;
        internal bool AwaitingApplication,CompletionSent,Cancelled,Restarting;
    }
    private readonly object _loadGate=new();
    private BrowserLoad? _browserLoad;

    public async Task<BrowserLoadSaveResultDto> LoadSaveAsync(BrowserLoadSaveRequest request,
        Func<BrowserLoadStateRequest,Task<BrowserLoadStateDto>>? buildState=null,CancellationToken cancelled=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id=request.OperationId??Guid.NewGuid().ToString("N");
        BrowserLoad operation;
        lock(_loadGate) {
            if(_browserLoad!=null)return LoadRefusal(id,"Загрузка уже выполняется или её завершение не подтверждено.");
            _browserLoad=operation=new(id);
            operation.Execution=RunOriginalBrowserLoadAsync(operation,request,buildState,cancelled);
        }
        return await operation.Ready.Task;
    }

    private async Task<BrowserLoadSaveResultDto> RunOriginalBrowserLoadAsync(BrowserLoad operation,BrowserLoadSaveRequest request,
        Func<BrowserLoadStateRequest,Task<BrowserLoadStateDto>>? buildState,CancellationToken cancelled)
    {
        GmLoadSessionOperation? original=null;
        var result=LoadRefusal(operation.Id,"Остановка исходного ГМа не подтверждена. Загрузка не начата.");
        try {
            original=GmLoadSessionOperation.Reserve(_fs,operation.Id,request.SaveId??"missing-selection",request.ExpectedGeneration);
            using var cancelRegistration=cancelled.Register(()=>CancelOriginal(operation));
            await original.StopAsync(cancelled); // no client filesystem lease during stop/IPC
            var applied=false;
            using(var admission=_fs.BeginMainAdmission()) {
                await admission.AcquireAsync(quiescentOnly:true);
                await original.ValidateLoadGenerationAsync();
                lock(operation.Gate)if(operation.Cancelled)throw new OperationCanceledException("Load cancelled before mutation.");
                result=await LoadSaveCoreAsync(request,buildState);
                result=result with {LifecycleOperationId=operation.Id,MainSessionState=original.State};
                operation.Retained=result;
                if(original.HadSession && result.Disposition==LoadReplacementDisposition.Committed && !result.ContinuationBlocked) {
                    if(result.State==null)result=BlockLoad(result,"Сохранение загружено. Полное обновление интерфейса не подготовлено; новый ГМ не запущен.");
                    else {
                        result=result with{FreshLaunchRequired=true};
                        lock(operation.Gate){operation.Retained=result;operation.AwaitingApplication=true;}
                        operation.Ready.TrySetResult(result);
                        try {applied=await operation.Applied.Task.WaitAsync(TimeSpan.FromSeconds(30));}
                        catch(TimeoutException){CancelOriginal(operation);}
                        lock(operation.Gate)applied&=!operation.Cancelled;
                        if(!applied)result=BlockLoad(result,"Сохранение загружено. Обновление текущего интерфейса не подтверждено; новый ГМ не запущен.");
                    }
                }
            } // includes original UI release/full bundle; guard unwinds before launch IPC
            lock(operation.Gate) {
                applied&=!operation.Cancelled;
                operation.Restarting=applied;
            }
            var next=await original.FinishAsync(result.Disposition==LoadReplacementDisposition.Committed,applied,result.EstablishedGeneration);
            result=result with{MainSessionState=next,FreshLaunchRequired=false};
            if(original.HadSession && next is not (GmLoadMainState.Running or GmLoadMainState.StartedNotReady))
                result=BlockLoad(result,result.Disposition==LoadReplacementDisposition.Committed
                    ? "Сохранение загружено; новый ГМ не запущен. Продолжение остановлено."
                    : "Загрузка не подтверждена; исходный ГМ завершён. Продолжение остановлено.");
        } catch(Exception) {
            // Retain a confirmed storage decision even if stop/restart/refresh closing fails.
            result=BlockLoad(operation.Retained??result,"Завершение загрузки или новой сессии ГМа не подтверждено. Не повторяйте операцию вслепую.")
                with{MainSessionState=original?.State??GmLoadMainState.Refused,FreshLaunchRequired=false};
            if(original?.State is GmLoadMainState.Stopped or GmLoadMainState.NoActiveSession)
                try {await original.FinishAsync(false,false,result.EstablishedGeneration);}catch {result=result with{MainSessionState=GmLoadMainState.Uncertain};}
        } finally {
            if(original!=null)await original.DisposeAsync();
            operation.Retained=result;operation.Ready.TrySetResult(result);
            lock(_loadGate)if(ReferenceEquals(_browserLoad,operation))_browserLoad=null;
        }
        return result;
    }

    public async Task<BrowserLoadSaveResultDto> CompleteLoadAsync(BrowserLoadCompletionRequest request)
    {
        BrowserLoad? operation;lock(_loadGate)operation=_browserLoad;
        if(operation==null || operation.Id!=request.OperationId)return LoadRefusal(request.OperationId,"Исходная операция загрузки больше не ожидает подтверждения.");
        lock(operation.Gate) {
            if(!operation.AwaitingApplication || operation.CompletionSent || operation.Retained?.FreshLaunchRequired!=true ||
                string.IsNullOrWhiteSpace(request.EstablishedGeneration) || operation.Retained.EstablishedGeneration!=request.EstablishedGeneration)
                return BlockLoad(operation.Retained??LoadRefusal(operation.Id,""),"Устаревшее или повторное подтверждение загрузки отклонено.");
            operation.CompletionSent=true;
            if(!request.RefreshConfirmed)operation.Cancelled=true;
            operation.Applied.TrySetResult(request.RefreshConfirmed && !operation.Cancelled);
        }
        return await operation.Execution;
    }
    public async Task<BrowserLoadSaveResultDto> CancelLoadAsync(BrowserLoadCompletionRequest request)
    {
        BrowserLoad? operation;lock(_loadGate)operation=_browserLoad;
        if(operation==null || operation.Id!=request.OperationId)return LoadRefusal(request.OperationId,"Исходная операция загрузки не найдена; новая сессия не затронута.");
        CancelOriginal(operation);
        return await operation.Execution;
    }
    private static void CancelOriginal(BrowserLoad operation)
    {
        lock(operation.Gate) {
            if(operation.Restarting)return; // accepted restart decision cannot be replaced by a stale callback
            operation.Cancelled=true;operation.Applied.TrySetResult(false);
        }
    }
    private static BrowserLoadSaveResultDto LoadRefusal(string id,string error)=>new(false,error,"",null,
        LoadReplacementDisposition.NotLoaded,null,null,true,true,LifecycleOperationId:id,MainSessionState:GmLoadMainState.Refused);
    private static BrowserLoadSaveResultDto BlockLoad(BrowserLoadSaveResultDto result,string error)=>result with{
        Error=error,NeedsFollowUp=true,ContinuationBlocked=true,FreshLaunchRequired=false};
}
