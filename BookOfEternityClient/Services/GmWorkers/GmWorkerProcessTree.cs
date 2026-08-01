using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmWorkers;

internal interface IGmWorkerProcessTreeFactory
{
    IGmWorkerProcessTree Attach(Process process);
}

internal interface IGmWorkerProcessTree : IAsyncDisposable
{
    Task StopAndWaitAsync();
}

internal static class ProcessTreeTerminationConfirmation
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    internal static async Task WaitAsync(
        Process rootProcess,
        Func<bool> isBoundaryAlive,
        TimeSpan timeout,
        string boundaryName)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        using var confirmationTimeout = new CancellationTokenSource(timeout);
        try
        {
            await rootProcess.WaitForExitAsync(confirmationTimeout.Token);
            while (isBoundaryAlive())
                await Task.Delay(PollInterval, confirmationTimeout.Token);
        }
        catch (OperationCanceledException) when (confirmationTimeout.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"{boundaryName} did not confirm complete termination before the ownership deadline.");
        }
    }
}

internal sealed class GmWorkerProcessTreeFactory : IGmWorkerProcessTreeFactory
{
    private readonly Func<bool> _isWindows;
    private readonly Func<Process, IGmWorkerProcessTree> _windowsFactory;

    internal static GmWorkerProcessTreeFactory Instance { get; } = new(
        OperatingSystem.IsWindows,
        process => new WindowsJobProcessTree(process));

    internal GmWorkerProcessTreeFactory(
        Func<bool> isWindows,
        Func<Process, IGmWorkerProcessTree> windowsFactory)
    {
        _isWindows = isWindows ?? throw new ArgumentNullException(nameof(isWindows));
        _windowsFactory = windowsFactory ?? throw new ArgumentNullException(nameof(windowsFactory));
    }

    public IGmWorkerProcessTree Attach(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!_isWindows())
        {
            throw new PlatformNotSupportedException(
                "GM worker execution requires a Windows Job Object complete-process-tree boundary. " +
                "This platform is not supported and the worker command was not released.");
        }

        return _windowsFactory(process);
    }
}

internal sealed class WindowsJobProcessTree : IGmWorkerProcessTree
{
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private readonly Process _rootProcess;
    private readonly SafeJobHandle _job;
    private readonly TimeSpan _processTreeExitTimeout;
    private readonly object _sync = new();
    private Task? _stopTask;
    private bool _disposed;

    internal WindowsJobProcessTree(
        Process process,
        TimeSpan? processTreeExitTimeout = null)
    {
        _rootProcess = process;
        _processTreeExitTimeout = processTreeExitTimeout ?? ProcessTreeTerminationConfirmation.DefaultTimeout;
        if (_processTreeExitTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(processTreeExitTimeout));
        _job = NativeMethods.CreateJobObject(IntPtr.Zero, null);
        if (_job.IsInvalid)
            throw LastWin32Exception("Could not create the worker process job object.");

        try
        {
            ConfigureKillOnClose();
            if (!NativeMethods.AssignProcessToJobObject(_job, process.SafeHandle))
                throw LastWin32Exception("Could not attach the worker process to its job object.");
        }
        catch
        {
            _job.Dispose();
            throw;
        }
    }

    public Task StopAndWaitAsync()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stopTask == null ||
                _stopTask.IsFaulted ||
                _stopTask.IsCanceled)
            {
                _stopTask = StopCoreAsync();
            }

            return _stopTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task stopTask;
        lock (_sync)
        {
            if (_disposed)
                return;
            if (_stopTask == null ||
                _stopTask.IsFaulted ||
                _stopTask.IsCanceled)
            {
                _stopTask = StopCoreAsync();
            }

            stopTask = _stopTask;
        }

        try
        {
            await stopTask;
        }
        finally
        {
            lock (_sync)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _job.Dispose();
                }
            }
        }
    }

    private async Task StopCoreAsync()
    {
        if (GetActiveProcessCount() > 0 && !NativeMethods.TerminateJobObject(_job, 1))
            throw LastWin32Exception("Could not terminate the worker process job object.");

        await ProcessTreeTerminationConfirmation.WaitAsync(
            _rootProcess,
            () => GetActiveProcessCount() > 0,
            _processTreeExitTimeout,
            "Worker Windows job object");
    }

    private void ConfigureKillOnClose()
    {
        var limits = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = JobObjectLimitKillOnJobClose
            }
        };
        var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(limits, buffer, fDeleteOld: false);
            if (!NativeMethods.SetInformationJobObject(
                    _job,
                    JobObjectInformationClass.ExtendedLimitInformation,
                    buffer,
                    (uint)size))
            {
                throw LastWin32Exception("Could not configure the worker process job object.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private uint GetActiveProcessCount()
    {
        var size = Marshal.SizeOf<JobObjectBasicAccountingInformation>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!NativeMethods.QueryInformationJobObject(
                    _job,
                    JobObjectInformationClass.BasicAccountingInformation,
                    buffer,
                    (uint)size,
                    out _))
            {
                throw LastWin32Exception("Could not inspect the worker process job object.");
            }

            return Marshal.PtrToStructure<JobObjectBasicAccountingInformation>(buffer).ActiveProcesses;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static Win32Exception LastWin32Exception(string message) =>
        new(Marshal.GetLastWin32Error(), message);

    private enum JobObjectInformationClass
    {
        BasicAccountingInformation = 1,
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicAccountingInformation
    {
        internal long TotalUserTime;
        internal long TotalKernelTime;
        internal long ThisPeriodTotalUserTime;
        internal long ThisPeriodTotalKernelTime;
        internal uint TotalPageFaultCount;
        internal uint TotalProcesses;
        internal uint ActiveProcesses;
        internal uint TotalTerminatedProcesses;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        internal long PerProcessUserTimeLimit;
        internal long PerJobUserTimeLimit;
        internal uint LimitFlags;
        internal UIntPtr MinimumWorkingSetSize;
        internal UIntPtr MaximumWorkingSetSize;
        internal uint ActiveProcessLimit;
        internal UIntPtr Affinity;
        internal uint PriorityClass;
        internal uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        internal ulong ReadOperationCount;
        internal ulong WriteOperationCount;
        internal ulong OtherOperationCount;
        internal ulong ReadTransferCount;
        internal ulong WriteTransferCount;
        internal ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        internal JobObjectBasicLimitInformation BasicLimitInformation;
        internal IoCounters IoInfo;
        internal UIntPtr ProcessMemoryLimit;
        internal UIntPtr JobMemoryLimit;
        internal UIntPtr PeakProcessMemoryUsed;
        internal UIntPtr PeakJobMemoryUsed;
    }

    private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeJobHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true,
            CharSet = CharSet.Unicode)]
        internal static extern SafeJobHandle CreateJobObject(IntPtr jobAttributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetInformationJobObject(
            SafeJobHandle job,
            JobObjectInformationClass informationClass,
            IntPtr information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AssignProcessToJobObject(
            SafeJobHandle job,
            SafeProcessHandle process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TerminateJobObject(SafeJobHandle job, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryInformationJobObject(
            SafeJobHandle job,
            JobObjectInformationClass informationClass,
            IntPtr information,
            uint informationLength,
            out uint returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
