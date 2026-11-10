using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

internal static class L01OwnedSynthetic
{
    private static int _stop;
    private static int _cpuTid;
    private static long _iterations;
    [DllImport("libc")] private static extern int gettid();
    [DllImport("libc")] private static extern uint getuid();

    private static void Emit(object value) => Console.WriteLine("L01_SYNTHETIC " + JsonSerializer.Serialize(value));

    private static int Main(string[] args)
    {
        if (args.Length != 1 || args[0].Length != 32 || !Guid.TryParseExact(args[0], "N", out _)) return 64;
        using var watchdog = new Timer(_ => Environment.Exit(124), null, 60000, Timeout.Infinite);
        var worker = new Thread(KnownCpuOuter) { Name = "l01-owned-known-cpu", IsBackground = true };
        worker.Start();
        if (!SpinWait.SpinUntil(() => Interlocked.Read(ref _iterations) >= 1000, 3000)) return 66;
        var validStop = KnownWaitOuter(args[0]);
        Volatile.Write(ref _stop, 1);
        if (!worker.Join(2000)) return 67;
        Emit(new { Stage = "done", Nonce = args[0], Iterations = Interlocked.Read(ref _iterations), ValidStop = validStop });
        return validStop ? 0 : 65;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void KnownCpuOuter()
    {
        KnownCpuLeaf();
        GC.KeepAlive(typeof(L01OwnedSynthetic));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void KnownCpuLeaf()
    {
        Volatile.Write(ref _cpuTid, gettid());
        while (Volatile.Read(ref _stop) == 0) Interlocked.Increment(ref _iterations);
        GC.KeepAlive(typeof(L01OwnedSynthetic));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool KnownWaitOuter(string nonce)
    {
        var value = KnownWaitLeaf(nonce);
        GC.KeepAlive(nonce);
        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool KnownWaitLeaf(string nonce)
    {
        var stat = File.ReadAllText("/proc/self/stat");
        var fields = stat.Substring(stat.LastIndexOf(')') + 2).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Emit(new { Stage = "ready", Nonce = nonce, Pid = Environment.ProcessId,
            BirthTick = ulong.Parse(fields[19], CultureInfo.InvariantCulture), Uid = getuid(),
            WaitTid = gettid(), CpuTid = Volatile.Read(ref _cpuTid), Iterations = Interlocked.Read(ref _iterations),
            Mvid = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId,
            Runtime = RuntimeInformation.FrameworkDescription });
        return Console.ReadLine() == "STOP:" + nonce;
    }
}
