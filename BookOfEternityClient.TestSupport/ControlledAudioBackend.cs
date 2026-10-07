using System.Collections.Concurrent;
using BookOfEternityClient.Services;
namespace BookOfEternityClient.Tests;

internal sealed class ControlledAudioBackend : IAudioPlaybackBackend
{
    internal readonly ConcurrentQueue<Session> Sessions = new();
    internal bool IgnoreCancellation, FailRun, FailDispose;
    internal bool HadOverlap;
    public AudioBackendKind Kind => AudioBackendKind.LinuxSdl;
    public IAudioPlaybackSession Create(string path, Func<float> volume)
    {
        HadOverlap |= Sessions.Any(s => !s.Disposed);
        var session = new Session(path, volume, IgnoreCancellation, FailRun, FailDispose);
        Sessions.Enqueue(session); return session;
    }
    internal sealed class Session(string path, Func<float> volume, bool ignoreCancellation, bool failRun, bool failDispose) : IAudioPlaybackSession
    {
        internal string Path = path;
        internal readonly TaskCompletionSource End = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal float ObservedVolume;
        internal bool Canceled, Disposed;
        public async Task RunAsync(Action playing, CancellationToken token)
        {
            if (failRun) throw new IOException("Synthetic playback failure.");
            playing(); Started.TrySetResult();
            try
            {
                while (!End.Task.IsCompleted)
                {
                    ObservedVolume = volume();
                    await Task.Delay(5, ignoreCancellation ? CancellationToken.None : token);
                }
            }
            catch (OperationCanceledException) { Canceled = true; throw; }
        }
        public ValueTask DisposeAsync()
        {
            if (failDispose) throw new IOException("Synthetic cleanup failure.");
            Disposed = true; return ValueTask.CompletedTask;
        }
    }
}
