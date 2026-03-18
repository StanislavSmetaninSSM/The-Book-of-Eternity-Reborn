using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityGMBridge;

internal sealed class ConPtySession : IDisposable
{
    private IntPtr _pseudoConsole;
    private IntPtr _processHandle;
    private IntPtr _threadHandle;
    private Process? _process;

    public Stream InputWriter { get; }
    public Stream OutputReader { get; }
    public int ProcessId { get; }
    public bool HasExited => _process?.HasExited ?? true;
    public int? ExitCode => _process is { HasExited: true } ? _process.ExitCode : null;

    private ConPtySession(
        IntPtr pseudoConsole,
        SafeFileHandle inputWriterHandle,
        SafeFileHandle outputReaderHandle,
        IntPtr processHandle,
        IntPtr threadHandle,
        int processId)
    {
        _pseudoConsole = pseudoConsole;
        _processHandle = processHandle;
        _threadHandle = threadHandle;
        ProcessId = processId;
        _process = Process.GetProcessById(processId);
        InputWriter = new FileStream(inputWriterHandle, FileAccess.Write, 4096, isAsync: false);
        OutputReader = new FileStream(outputReaderHandle, FileAccess.Read, 4096, isAsync: false);
    }

    public static ConPtySession Start(string shellExe, string shellArguments, string workingDirectory, short width, short height)
    {
        if (!ConPtyNativeMethods.CreatePipe(out var inputReadPipe, out var inputWritePipe, IntPtr.Zero, 0))
            throw new InvalidOperationException($"CreatePipe(input) failed: {Marshal.GetLastWin32Error()}");

        if (!ConPtyNativeMethods.CreatePipe(out var outputReadPipe, out var outputWritePipe, IntPtr.Zero, 0))
        {
            ConPtyNativeMethods.CloseHandle(inputReadPipe);
            ConPtyNativeMethods.CloseHandle(inputWritePipe);
            throw new InvalidOperationException($"CreatePipe(output) failed: {Marshal.GetLastWin32Error()}");
        }

        var size = new ConPtyNativeMethods.COORD { X = width, Y = height };
        var hr = ConPtyNativeMethods.CreatePseudoConsole(size, inputReadPipe, outputWritePipe, 0, out var pseudoConsole);

        ConPtyNativeMethods.CloseHandle(inputReadPipe);
        ConPtyNativeMethods.CloseHandle(outputWritePipe);

        if (hr != 0)
        {
            ConPtyNativeMethods.CloseHandle(inputWritePipe);
            ConPtyNativeMethods.CloseHandle(outputReadPipe);
            throw new InvalidOperationException($"CreatePseudoConsole failed: HRESULT=0x{hr:X8}");
        }

        var siEx = new ConPtyNativeMethods.STARTUPINFOEX();
        siEx.StartupInfo.cb = Marshal.SizeOf<ConPtyNativeMethods.STARTUPINFOEX>();

        var attrListSize = IntPtr.Zero;
        ConPtyNativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrListSize);
        siEx.lpAttributeList = Marshal.AllocHGlobal(attrListSize);

        try
        {
            if (!ConPtyNativeMethods.InitializeProcThreadAttributeList(siEx.lpAttributeList, 1, 0, ref attrListSize))
                throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");

            if (!ConPtyNativeMethods.UpdateProcThreadAttribute(
                    siEx.lpAttributeList,
                    0,
                    (IntPtr)ConPtyNativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    pseudoConsole,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");
            }

            var commandLine = $"\"{shellExe}\" {shellArguments}";
            if (!ConPtyNativeMethods.CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    ConPtyNativeMethods.EXTENDED_STARTUPINFO_PRESENT | ConPtyNativeMethods.CREATE_UNICODE_ENVIRONMENT,
                    IntPtr.Zero,
                    workingDirectory,
                    ref siEx,
                    out var processInfo))
            {
                throw new InvalidOperationException($"CreateProcess failed: {Marshal.GetLastWin32Error()}");
            }

            var inputWriteSafe = new SafeFileHandle(inputWritePipe, ownsHandle: true);
            var outputReadSafe = new SafeFileHandle(outputReadPipe, ownsHandle: true);
            return new ConPtySession(
                pseudoConsole,
                inputWriteSafe,
                outputReadSafe,
                processInfo.hProcess,
                processInfo.hThread,
                unchecked((int)processInfo.dwProcessId));
        }
        finally
        {
            if (siEx.lpAttributeList != IntPtr.Zero)
            {
                ConPtyNativeMethods.DeleteProcThreadAttributeList(siEx.lpAttributeList);
                Marshal.FreeHGlobal(siEx.lpAttributeList);
            }
        }
    }

    public void Dispose()
    {
        try
        {
            InputWriter.Dispose();
            OutputReader.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            if (_process != null && !_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignored
        }

        _process?.Dispose();

        if (_threadHandle != IntPtr.Zero)
            ConPtyNativeMethods.CloseHandle(_threadHandle);
        if (_processHandle != IntPtr.Zero)
            ConPtyNativeMethods.CloseHandle(_processHandle);
        if (_pseudoConsole != IntPtr.Zero)
            ConPtyNativeMethods.ClosePseudoConsole(_pseudoConsole);

        _threadHandle = IntPtr.Zero;
        _processHandle = IntPtr.Zero;
        _pseudoConsole = IntPtr.Zero;
    }

    public void Resize(short width, short height)
    {
        if (_pseudoConsole == IntPtr.Zero)
            return;

        var size = new ConPtyNativeMethods.COORD { X = width, Y = height };
        var hr = ConPtyNativeMethods.ResizePseudoConsole(_pseudoConsole, size);
        if (hr != 0)
            throw new InvalidOperationException($"ResizePseudoConsole failed: HRESULT=0x{hr:X8}");
    }
}

internal static class ConPtyNativeMethods
{
    public const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
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
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        int dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        [In] ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

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
