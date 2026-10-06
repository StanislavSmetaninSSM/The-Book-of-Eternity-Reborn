using System.Reflection;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmOwnedTerminalSessionTests
{
    [Fact]
    public async Task ActualStop_RetainsOriginalOwnerWhileScopedStopAndOutputAreUnfinished()
    {
        var repo = FindRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bridge = Path.Combine(repo, "BookOfEternityGMBridge/bin", configuration, "net8.0/BookOfEternityGMBridge.dll");
        var type = Assembly.LoadFrom(bridge).GetType("BookOfEternityGMBridge.BridgeHost", true)!;
        var scratch = Path.Combine(Path.GetTempPath(), "boe-owned-terminal-" + Guid.NewGuid().ToString("N"));
        var host = Activator.CreateInstance(type, [scratch, "unused-" + Guid.NewGuid().ToString("N")])!;
        using var inputCancellation = new CancellationTokenSource();
        var owner = new HeldTerminal();
        var output = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        FieldInfo Field(string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        type.GetMethod("BeginInputLifetime", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(host, [owner.InputWriter, inputCancellation]);
        Field("_pty").SetValue(host, owner);
        Field("_outputPumpTask").SetValue(host, output.Task);
        var stop = (Task)type.GetMethod("StopShellAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, null)!;
        try
        {
            await owner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Same(owner, Field("_pty").GetValue(host));
            Assert.False(owner.Disposed, "Original owner cannot be disposed before scope proof and actual output settlement.");
            var begin = type.GetMethod("BeginInputLifetime", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.Throws<TargetInvocationException>(() => begin.Invoke(host, [new MemoryStream(), inputCancellation]));
            Assert.False(stop.IsCompleted);
        }
        finally
        {
            owner.Release.TrySetResult(); output.TrySetResult();
            await stop.WaitAsync(TimeSpan.FromSeconds(8));
            ((IDisposable)host).Dispose();
            owner.InputWriter.Dispose(); owner.OutputReader.Dispose();
            Directory.Delete(scratch, true);
        }
        Assert.Null(Field("_pty").GetValue(host));
    }

    private sealed class HeldTerminal : IOwnedTerminalSession
    {
        public TerminalIdentity Identity { get; } = new(Guid.NewGuid().ToString("N"), "controlled-owner", "fixture-only", 1);
        public Stream InputWriter { get; } = new MemoryStream();
        public Stream OutputReader { get; } = new MemoryStream();
        public Task<TerminalRootExit> RootExited { get; } = new TaskCompletionSource<TerminalRootExit>().Task;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed;
        public ValueTask ResizeAsync(TerminalSize size, CancellationToken waitToken) => ValueTask.CompletedTask;
        public async Task<TerminalStopEvidence> StopAndObserveAsync(CancellationToken waitToken)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(waitToken);
            return new(Identity, GmWorkerStopState.StoppedWithinScope, "controlled-scoped-proof", true, true);
        }
        public async ValueTask DisposeAsync()
        {
            Disposed = true; Entered.TrySetResult(); await Release.Task;
        }
    }

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "BookOfEternityGMBridge/Program.cs"))) return d.FullName;
        throw new DirectoryNotFoundException("Repository source is required.");
    }
}
