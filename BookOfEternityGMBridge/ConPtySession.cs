using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityGMBridge;

// Job ownership precedes release. The original close task lives alongside the
// live output reader until its actual completion; ClosePseudoConsole is no scope proof.
internal sealed class ConPtySession : IDisposable, IOwnedTerminalSession
{
    private IntPtr _pseudoConsole, _processHandle, _threadHandle;
    private IntPtr _unwrappedInput, _unwrappedOutput;
    private SafeFileHandle? _inputPipe, _outputPipe;
    private Process? _process;
    private WindowsJobProcessTree? _job;
    private readonly object _gate=new();
    private Task<TerminalStopEvidence>? _stopTask;
    private Task? _closeTask, _disposeTask;
    private readonly TaskCompletionSource<string> _authorityLost=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _uncertain, _stopping;
    private bool _released;
    public Stream InputWriter {get;private set;}=Stream.Null;
    public Stream OutputReader {get;private set;}=Stream.Null;
    public int ProcessId=>Identity.RootPid;
    public TerminalIdentity Identity {get;private set;}=new(Guid.NewGuid().ToString("N"),"windows-job","windows-job",0);
    public Task<TerminalRootExit> RootExited {get;private set;}=new TaskCompletionSource<TerminalRootExit>().Task;
    public Task<string> AuthorityLost=>_authorityLost.Task;
    private ConPtySession() { }
    private void Lose(string reason) { lock(_gate)_uncertain=true;_authorityLost.TrySetResult(reason); }
    private async Task<TerminalRootExit> ObserveRootAsync(Process process) { try { await process.WaitForExitAsync();return new(process.ExitCode); } catch { Lose("conpty-root-observation-fault");throw; } }
    public static ConPtySession Start(string shellExe,string shellArguments,string workingDirectory,short width,short height)
    {
        var owner=Prepare(shellExe,shellArguments,workingDirectory,width,height,Guid.NewGuid().ToString("N"));
        try {owner.ReleaseOriginal();return owner;}catch(Exception ex){owner.Lose("conpty-release-unconfirmed");throw new OwnedTerminalStartException(owner,ex);}
    }
    internal static ConPtySession Prepare(string shellExe,string shellArguments,string workingDirectory,short width,short height,string runId)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("ConPTY requires Windows.");
        var owner=new ConPtySession();
        owner.Identity=owner.Identity with{RunId=runId}; // Original resource owner exists before fallible stream acquisition.
        if(!ConPtyNativeMethods.CreatePipe(out var inputRead,out var inputWrite,IntPtr.Zero,0))throw new IOException("ConPTY input pipe failed.");
        if(!ConPtyNativeMethods.CreatePipe(out var outputRead,out var outputWrite,IntPtr.Zero,0)) {
            ConPtyNativeMethods.CloseHandle(inputRead);ConPtyNativeMethods.CloseHandle(inputWrite);throw new IOException("ConPTY output pipe failed."); }
        var hr=ConPtyNativeMethods.CreatePseudoConsole(new(){X=width,Y=height},inputRead,outputWrite,0,out var console);
        ConPtyNativeMethods.CloseHandle(inputRead);ConPtyNativeMethods.CloseHandle(outputWrite);
        if(hr!=0) { ConPtyNativeMethods.CloseHandle(inputWrite);ConPtyNativeMethods.CloseHandle(outputRead);throw new IOException("ConPTY creation failed."); }
        owner._pseudoConsole=console;owner._unwrappedInput=inputWrite;owner._unwrappedOutput=outputRead;
        var si=new ConPtyNativeMethods.STARTUPINFOEX();si.StartupInfo.cb=Marshal.SizeOf<ConPtyNativeMethods.STARTUPINFOEX>();
        var size=IntPtr.Zero;var initialized=false;
        try {
            owner._inputPipe=new SafeFileHandle(owner._unwrappedInput,true);owner._unwrappedInput=IntPtr.Zero;
            owner.InputWriter=new FaultStream(new FileStream(owner._inputPipe,FileAccess.Write,4096,false),owner.Lose,()=>!owner._stopping && !owner._uncertain && !owner.RootExited.IsCompleted);
            owner._outputPipe=new SafeFileHandle(owner._unwrappedOutput,true);owner._unwrappedOutput=IntPtr.Zero;
            owner.OutputReader=new FaultStream(new FileStream(owner._outputPipe,FileAccess.Read,4096,false),owner.Lose,()=>true);
            ConPtyNativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size);
            si.lpAttributeList=Marshal.AllocHGlobal(size);
            if(!ConPtyNativeMethods.InitializeProcThreadAttributeList(si.lpAttributeList,1,0,ref size))throw new IOException("ConPTY attribute initialization failed.");
            initialized=true;
            if(!ConPtyNativeMethods.UpdateProcThreadAttribute(si.lpAttributeList,0,(IntPtr)ConPtyNativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,console,(IntPtr)IntPtr.Size,IntPtr.Zero,IntPtr.Zero))throw new IOException("ConPTY attribute failed.");
            if(!ConPtyNativeMethods.CreateProcess(null,new StringBuilder($"\"{shellExe}\" {shellArguments}"),IntPtr.Zero,IntPtr.Zero,false,
                ConPtyNativeMethods.EXTENDED_STARTUPINFO_PRESENT|ConPtyNativeMethods.CREATE_UNICODE_ENVIRONMENT|ConPtyNativeMethods.CREATE_SUSPENDED,
                IntPtr.Zero,workingDirectory,ref si,out var process))throw new IOException("ConPTY suspended process creation failed.");
            owner._processHandle=process.hProcess;owner._threadHandle=process.hThread;
            owner.Identity=owner.Identity with{RootPid=checked((int)process.dwProcessId)};
            owner._process=Process.GetProcessById(owner.ProcessId);owner.RootExited=owner.ObserveRootAsync(owner._process);
            owner._job=new WindowsJobProcessTree(owner._process);
            return owner;
        } catch(Exception ex) { owner.Lose("partial-conpty-start");throw new OwnedTerminalStartException(owner,ex); }
        finally { if(si.lpAttributeList!=IntPtr.Zero) { if(initialized)ConPtyNativeMethods.DeleteProcThreadAttributeList(si.lpAttributeList);Marshal.FreeHGlobal(si.lpAttributeList); } }
    }
    private int _releaseConsumed;
    internal void ReleaseOriginal()
    {
        lock(_gate) {
            if(Interlocked.Exchange(ref _releaseConsumed,1)!=0 || _uncertain || _stopping || _job==null || RootExited.IsCompleted)
                throw new IOException("Original ConPTY release unavailable.");
            if(ConPtyNativeMethods.ResumeThread(_threadHandle)!=1){Lose("conpty-release-unconfirmed");throw new IOException("ConPTY original suspended thread release failed.");}
            _released=true;
        }
    }
    public ValueTask ResizeAsync(TerminalSize size,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(size.Columns is <1 or >32767 || size.Rows is <1 or >32767)throw new ArgumentOutOfRangeException(nameof(size));
        lock(_gate) {
            if(_uncertain || _stopping || RootExited.IsCompleted)throw new IOException("Original ConPTY admission is closed.");
            if(ConPtyNativeMethods.ResizePseudoConsole(_pseudoConsole,new(){X=(short)size.Columns,Y=(short)size.Rows})!=0) { Lose("conpty-resize-fault");throw new IOException("ConPTY resize failed."); }
        }
        return ValueTask.CompletedTask;
    }
    public async Task<TerminalStopEvidence> StopAndObserveAsync(CancellationToken token)
    {
        Task<TerminalStopEvidence> task;lock(_gate) { _stopping=true;task=_stopTask??=StopCoreAsync(); }
        var proof=await task.WaitAsync(token);lock(_gate)return _uncertain?proof with{State=GmWorkerStopState.Uncertain,Reason="conpty-uncertain"}:proof;
    }
    private async Task<TerminalStopEvidence> StopCoreAsync()
    {
        try {
            if(_job!=null)await _job.StopAndWaitAsync(); // actual original Job terminate/query-empty
            else if(_processHandle!=IntPtr.Zero && !_released) {
                // Failed assignment retained a never-released suspended original kernel handle.
                if(!ConPtyNativeMethods.TerminateProcess(_processHandle,1))throw new IOException("Suspended original root cleanup failed.");
                await RootExited.WaitAsync(TimeSpan.FromSeconds(5));
            } else if(_released)throw new InvalidOperationException("ConPTY Job authority lost.");
            // Do not wait for output EOF before beginning this retained close task.
            lock(_gate)_closeTask??=Task.Factory.StartNew(()=>ConPtyNativeMethods.ClosePseudoConsole(_pseudoConsole),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            return new(Identity,_uncertain?GmWorkerStopState.Uncertain:GmWorkerStopState.StoppedWithinScope,"original-job-empty",true,_uncertain);
        } catch { Lose("conpty-stop-fault");return new(Identity,GmWorkerStopState.Uncertain,"original-job-stop-unconfirmed",false,true); }
    }
    public ValueTask DisposeAsync() { lock(_gate)return new(_disposeTask??=DisposeCoreAsync()); }
    private async Task DisposeCoreAsync()
    {
        var proof=await StopAndObserveAsync(CancellationToken.None);
        if(proof.State!=GmWorkerStopState.StoppedWithinScope || !proof.CleanupComplete || proof.AuthorityRetained)throw new InvalidOperationException("Uncertain ConPTY retains original Job/session.");
        try { await Task.WhenAll(_closeTask!,RootExited,((FaultStream)InputWriter).Settlement,((FaultStream)OutputReader).Settlement).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch { Lose("conpty-close-unsettled");throw; }
        // Actual EOF and all admitted I/O, plus the one original close task, are joined.
        await InputWriter.DisposeAsync();await OutputReader.DisposeAsync();
        _inputPipe?.Dispose();_outputPipe?.Dispose();
        await _job!.DisposeAsync();_job=null;
        _process?.Dispose();
        if(_threadHandle!=IntPtr.Zero)ConPtyNativeMethods.CloseHandle(_threadHandle);
        if(_processHandle!=IntPtr.Zero)ConPtyNativeMethods.CloseHandle(_processHandle);
        _threadHandle=_processHandle=_pseudoConsole=IntPtr.Zero;
    }
    public void Dispose()=>DisposeAsync().AsTask().GetAwaiter().GetResult();
    private sealed class FaultStream(Stream stream,Action<string> fault,Func<bool> admission) : Stream
    {
        private readonly object _state=new();
        private int _active;
        private bool _disposed;
        private TaskCompletionSource _idle=Completed();
        private readonly TaskCompletionSource _eof=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static TaskCompletionSource Completed(){var t=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);t.SetResult();return t;}
        internal Task Settlement=>SettleAsync();
        private async Task SettleAsync(){if(stream.CanRead)await _eof.Task;Task idle;lock(_state)idle=_idle.Task;await idle;}
        private void Enter(){lock(_state){if(_disposed || !admission())throw new IOException("Original ConPTY I/O admission closed.");if(_active++==0)_idle=new(TaskCreationOptions.RunContinuationsAsynchronously);}}
        private void Leave(){lock(_state){if(--_active==0)_idle.TrySetResult();}}
        public override bool CanRead=>stream.CanRead;public override bool CanWrite=>stream.CanWrite;public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override async ValueTask<int> ReadAsync(Memory<byte> b,CancellationToken t=default)
        {
            if(b.Length==0 || _eof.Task.IsCompletedSuccessfully)return 0;
            Enter();try{var n=await stream.ReadAsync(b,t);if(n==0)_eof.TrySetResult();return n;}
            catch(OperationCanceledException){throw;}catch{fault("conpty-output-fault");throw;}finally{Leave();}
        }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> b,CancellationToken t=default)
        {
            Enter();try{await stream.WriteAsync(b,t);}catch(OperationCanceledException){throw;}catch{fault("conpty-input-fault");throw;}finally{Leave();}
        }
        public override Task<int> ReadAsync(byte[] b,int o,int n,CancellationToken t)=>ReadAsync(b.AsMemory(o,n),t).AsTask();
        public override Task WriteAsync(byte[] b,int o,int n,CancellationToken t)=>WriteAsync(b.AsMemory(o,n),t).AsTask();
        public override async Task FlushAsync(CancellationToken t){Enter();try{await stream.FlushAsync(t);}catch(OperationCanceledException){throw;}catch{fault("conpty-flush-fault");throw;}finally{Leave();}}
        public override int Read(byte[] b,int o,int n)=>ReadAsync(b.AsMemory(o,n)).AsTask().GetAwaiter().GetResult();
        public override void Write(byte[] b,int o,int n)=>WriteAsync(b.AsMemory(o,n)).AsTask().GetAwaiter().GetResult();
        public override void Flush()=>FlushAsync(CancellationToken.None).GetAwaiter().GetResult();public override long Seek(long n,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
        protected override void Dispose(bool disposing){if(disposing){lock(_state){if(_active!=0 || (stream.CanRead && !_eof.Task.IsCompletedSuccessfully))throw new InvalidOperationException("Original ConPTY I/O unsettled.");_disposed=true;stream.Dispose();}}base.Dispose(disposing);}
    }
}

internal static class ConPtyNativeMethods
{
    public const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    public const int CREATE_SUSPENDED = 0x00000004;
    public const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    public const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll")]
    public static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList,
        uint dwFlags,
        IntPtr attribute,
        IntPtr lpValue,
        IntPtr cbSize,
        IntPtr lpPreviousValue,
        IntPtr lpReturnSize);

    [DllImport("kernel32.dll")]
    public static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcess(
        string? lpApplicationName,
        [In,Out] StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        int dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        [In] ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll",SetLastError=true)]
    public static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll",SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TerminateProcess(IntPtr process,uint exitCode);

    [StructLayout(LayoutKind.Sequential)]
    public struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }
}
