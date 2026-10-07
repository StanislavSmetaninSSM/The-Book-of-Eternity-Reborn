using NAudio.Wave;
using NLayer;
namespace BookOfEternityClient.Services;

internal sealed class SdlAudioBackend(ISdlAudioApi api) : IAudioPlaybackBackend
{
    private readonly ISdlAudioApi _api = api;
    private readonly object _sync = new();
    private int _users;
    public AudioBackendKind Kind => AudioBackendKind.LinuxSdl;
    public IAudioPlaybackSession Create(string path, Func<float> volume) => new Session(this, path, volume);
    private void Acquire()
    {
        lock (_sync) { if (_users == 0) api.Initialize(); _users++; }
    }
    private void Release()
    {
        lock (_sync) { if (_users == 1) api.Quit(); _users--; }
    }
    private sealed class Session(SdlAudioBackend owner, string path, Func<float> volume) : IAudioPlaybackSession
    {
        private uint _device;
        private bool _acquired;
        private WaveFileReader? _wav;
        private MpegFile? _mp3;
        public async Task RunAsync(Action playing, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            // Discover capability before opening even a decoder; no startup device initialization.
            owner.Acquire(); _acquired = true;
            ISampleProvider? pcm = null;
            int rate, channels;
            if (Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            { _mp3 = new MpegFile(path); rate = _mp3.SampleRate; channels = _mp3.Channels; }
            else
            { _wav = new WaveFileReader(path); pcm = _wav.ToSampleProvider(); rate = pcm.WaveFormat.SampleRate; channels = pcm.WaveFormat.Channels; }
            _device = owner._api.Open(rate, channels);
            var samples = new float[checked(channels * 1024)];
            var queueLimit = checked((uint)(rate * channels * sizeof(float) / 10));
            owner._api.Pause(_device, false); playing();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (owner._api.QueuedBytes(_device) >= queueLimit) { await Task.Delay(10, token); continue; }
                var count = _mp3 != null ? _mp3.ReadSamples(samples, 0, samples.Length) : pcm!.Read(samples, 0, samples.Length);
                if (count == 0) break;
                var gain = Math.Clamp(volume(), 0, 1);
                for (var i = 0; i < count; i++) samples[i] *= gain;
                owner._api.Queue(_device, samples, count);
            }
            while (owner._api.QueuedBytes(_device) != 0) await Task.Delay(10, token);
        }
        public ValueTask DisposeAsync()
        {
            if (_device != 0)
            {
                owner._api.Pause(_device, true); owner._api.Clear(_device); owner._api.Close(_device); _device = 0;
            }
            _mp3?.Dispose(); _mp3 = null; _wav?.Dispose(); _wav = null;
            if (_acquired) { owner.Release(); _acquired = false; }
            return ValueTask.CompletedTask;
        }
    }
}
