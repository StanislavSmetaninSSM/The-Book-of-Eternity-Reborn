using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    private void RecordStopFailure(Exception failure,string stage,string? predicate=null,Task? inputDrain=null)
    {
#if DEBUG
        try {
            // The current original owner only; no root lookup, last owner or recovery authority.
            var original=_mainRun;
            var stop=_terminalStopTask;
            original?.RecordStopFailure(failure,stage,predicate,inputDrain,
                stop?.IsCompletedSuccessfully==true?stop.Result:null);
        } catch { }
#endif
    }
    private static void ReportStopFailure(Exception failure)
    {
#if DEBUG
        try {
            var report=GmSessionRunCoordinator.GetStopFailureReport(failure);
            if(report==null)return;
            const string prefix="BOE_MAIN_STOP_FAILURE ";
            var line=prefix+JsonSerializer.Serialize(report);
            if(Encoding.UTF8.GetByteCount(line+Environment.NewLine)>32768)return;
            Console.Error.WriteLine(line);
        } catch { /* A sink/capture fault cannot replace error reply, Stop error or cleanup failure. */ }
#endif
    }
}
