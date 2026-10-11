using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    private void RecordStopFailure(GmSessionRunCoordinator? original,IOwnedTerminalSession? originalSession,
        Exception failure,string stage,string? predicate=null,Task? inputDrain=null)
    {
#if DEBUG
        try {
            // Frozen StopCore entry refs only; a new/current owner never receives the old failure.
            lock(_sync)if(!ReferenceEquals(_mainRun,original) || !ReferenceEquals(_pty,originalSession))return;
            if(original==null || !original.MatchesStopDiagnosticOwner(originalSession))return;
            var stop=_terminalStopTask;
            var proof=stop?.IsCompletedSuccessfully==true?stop.Result:null;
            if(proof!=null && (originalSession==null || proof.Identity!=originalSession.Identity ||
                proof.Identity.RunId!=original.Record?.Identity.RunId))return;
            original.RecordStopFailure(failure,stage,predicate,inputDrain,proof);
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
