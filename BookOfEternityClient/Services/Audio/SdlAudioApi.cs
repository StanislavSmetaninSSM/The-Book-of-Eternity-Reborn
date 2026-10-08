using System.Runtime.InteropServices;
namespace BookOfEternityClient.Services;

internal interface ISdlAudioApi
{
    void Initialize();
    void Quit();
    uint Open(int rate, int channels);
    void Pause(uint device, bool paused);
    void Queue(uint device, float[] samples, int count);
    uint QueuedBytes(uint device);
    void Clear(uint device);
    void Close(uint device);
}
internal sealed class SdlAudioApi : ISdlAudioApi
{
    // Test-only instance seam selects dummy itself; it never initializes a default driver afterward.
    private readonly bool _dummyOnly;
    internal SdlAudioApi(bool dummyOnly = false) => _dummyOnly = dummyOnly;
    public void Initialize()
    {
        try
        {
            var result = _dummyOnly ? Native.AudioInit("dummy") : Native.InitSubSystem(0x10);
            if (result != 0) throw new AudioCapabilityException(AudioOutcome.DeviceUnavailable);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { throw new AudioCapabilityException(AudioOutcome.BackendUnavailable); }
    }
    public void Quit() { if (_dummyOnly) Native.AudioQuit(); else Native.QuitSubSystem(0x10); }
    public uint Open(int rate, int channels)
    {
        if (_dummyOnly && Marshal.PtrToStringUTF8(Native.GetCurrentAudioDriver()) != "dummy")
            throw new InvalidOperationException("Owned SDL fixture refuses a non-dummy driver.");
        var spec = new Native.Spec { Frequency = rate, Format = 0x8120, Channels = checked((byte)channels), Samples = 1024 };
        var device = Native.OpenAudioDevice(IntPtr.Zero, 0, ref spec, out _, 0);
        if (device == 0) throw new AudioCapabilityException(AudioOutcome.DeviceUnavailable);
        return device;
    }
    public void Pause(uint device, bool paused) => Native.PauseAudioDevice(device, paused ? 1 : 0);
    public void Queue(uint device, float[] samples, int count)
    { if (Native.QueueAudio(device, samples, checked((uint)(count * sizeof(float)))) != 0) throw new IOException("Audio queue failed."); }
    public uint QueuedBytes(uint device) => Native.GetQueuedAudioSize(device);
    public void Clear(uint device) => Native.ClearQueuedAudio(device);
    public void Close(uint device) => Native.CloseAudioDevice(device);
    private static class Native
    {
        private const string Library = "libSDL2-2.0.so.0";
        [StructLayout(LayoutKind.Sequential)]
        internal struct Spec
        {
            internal int Frequency;
            internal ushort Format;
            internal byte Channels, Silence;
            internal ushort Samples, Padding;
            internal uint Size;
            internal IntPtr Callback, Userdata;
        }
        [DllImport(Library, EntryPoint = "SDL_InitSubSystem", CallingConvention = CallingConvention.Cdecl)] internal static extern int InitSubSystem(uint flags);
        [DllImport(Library, EntryPoint = "SDL_QuitSubSystem", CallingConvention = CallingConvention.Cdecl)] internal static extern void QuitSubSystem(uint flags);
        [DllImport(Library, EntryPoint = "SDL_AudioInit", CallingConvention = CallingConvention.Cdecl)] internal static extern int AudioInit([MarshalAs(UnmanagedType.LPUTF8Str)] string driver);
        [DllImport(Library, EntryPoint = "SDL_AudioQuit", CallingConvention = CallingConvention.Cdecl)] internal static extern void AudioQuit();
        [DllImport(Library, EntryPoint = "SDL_GetCurrentAudioDriver", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr GetCurrentAudioDriver();
        [DllImport(Library, EntryPoint = "SDL_OpenAudioDevice", CallingConvention = CallingConvention.Cdecl)] internal static extern uint OpenAudioDevice(IntPtr name, int capture, ref Spec desired, out Spec obtained, int allowedChanges);
        [DllImport(Library, EntryPoint = "SDL_PauseAudioDevice", CallingConvention = CallingConvention.Cdecl)] internal static extern void PauseAudioDevice(uint device, int pause);
        [DllImport(Library, EntryPoint = "SDL_QueueAudio", CallingConvention = CallingConvention.Cdecl)] internal static extern int QueueAudio(uint device, [In] float[] samples, uint length);
        [DllImport(Library, EntryPoint = "SDL_GetQueuedAudioSize", CallingConvention = CallingConvention.Cdecl)] internal static extern uint GetQueuedAudioSize(uint device);
        [DllImport(Library, EntryPoint = "SDL_ClearQueuedAudio", CallingConvention = CallingConvention.Cdecl)] internal static extern void ClearQueuedAudio(uint device);
        [DllImport(Library, EntryPoint = "SDL_CloseAudioDevice", CallingConvention = CallingConvention.Cdecl)] internal static extern void CloseAudioDevice(uint device);
    }
}
