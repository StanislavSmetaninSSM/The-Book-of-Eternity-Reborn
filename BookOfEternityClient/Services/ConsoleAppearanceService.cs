using System.Runtime.InteropServices;
using BookOfEternityClient.Configuration;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>
/// Applies client-side console appearance preferences such as font size.
/// Runtime font changes are only supported on classic Windows console hosts.
/// </summary>
public sealed class ConsoleAppearanceService
{
    private const int StdOutputHandle = -11;
    private readonly GameSettings _settings;
    private readonly ILogger<ConsoleAppearanceService> _logger;

    public ConsoleAppearanceService(GameSettings settings, ILogger<ConsoleAppearanceService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public int NormalizeFontSize(int requestedSize)
    {
        return Math.Clamp(requestedSize, 14, 32);
    }

    public bool ApplyConfiguredFontSize()
    {
        return TryApplyFontSize(_settings.ConsoleFontSize);
    }

    public bool TryApplyFontSize(int requestedSize)
    {
        var normalized = NormalizeFontSize(requestedSize);
        _settings.ConsoleFontSize = normalized;

        if (!OperatingSystem.IsWindows() || Console.IsOutputRedirected)
            return false;

        try
        {
            var handle = GetStdHandle(StdOutputHandle);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                return false;

            var font = new ConsoleFontInfoEx
            {
                cbSize = (uint)Marshal.SizeOf<ConsoleFontInfoEx>()
            };

            if (!GetCurrentConsoleFontEx(handle, false, ref font))
                return false;

            font.dwFontSize.Y = (short)normalized;
            if (font.dwFontSize.X < 0)
                font.dwFontSize.X = 0;

            var applied = SetCurrentConsoleFontEx(handle, false, ref font);
            if (!applied)
                _logger.LogDebug("Не удалось применить размер консольного шрифта {FontSize}", normalized);

            return applied;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось применить размер консольного шрифта {FontSize}", normalized);
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ConsoleFontInfoEx
    {
        public uint cbSize;
        public uint nFont;
        public Coord dwFontSize;
        public int FontFamily;
        public int FontWeight;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FaceName;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetCurrentConsoleFontEx(
        IntPtr hConsoleOutput,
        bool bMaximumWindow,
        ref ConsoleFontInfoEx lpConsoleCurrentFontEx);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetCurrentConsoleFontEx(
        IntPtr consoleOutput,
        bool maximumWindow,
        ref ConsoleFontInfoEx consoleCurrentFontEx);
}
