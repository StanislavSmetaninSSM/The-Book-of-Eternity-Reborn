using NAudio.Wave;
namespace BookOfEternityClient.Services;

internal sealed class WindowsAudioBackend : IAudioPlaybackBackend
{
    public AudioBackendKind Kind => AudioBackendKind.WindowsWaveOut;
    public IAudioPlaybackSession Create(string path, Func<float> volume) => new Session(path, volume);
    private sealed class Session(string path, Func<float> volume) : IAudioPlaybackSession
    {
        private AudioFileReader? _reader;
        private WaveOutEvent? _output;
        public async Task RunAsync(Action playing, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _reader = new AudioFileReader(path) { Volume = volume() };
            _output = new WaveOutEvent();
            var ended = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _output.PlaybackStopped += (_, e) => ended.TrySetResult(e.Exception);
            _output.Init(_reader); token.ThrowIfCancellationRequested();
            _output.Play(); playing();
            while (!ended.Task.IsCompleted)
            {
                if (token.IsCancellationRequested) { _output.Stop(); break; }
                _reader.Volume = volume();
                await Task.WhenAny(ended.Task, Task.Delay(20));
            }
            var error = await ended.Task;
            if (error != null) throw error;
            token.ThrowIfCancellationRequested();
        }
        public ValueTask DisposeAsync()
        {
            // Only the original operation calls this, including after Init/Play failures.
            _output?.Dispose(); _output = null;
            _reader?.Dispose(); _reader = null;
            return ValueTask.CompletedTask;
        }
    }
}
