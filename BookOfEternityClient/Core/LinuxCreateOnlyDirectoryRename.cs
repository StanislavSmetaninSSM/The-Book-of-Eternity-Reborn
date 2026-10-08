using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BookOfEternityClient.Core;

internal static class LinuxCreateOnlyDirectoryRename
{
    // Native primitive only. The caller owns lease, namespace and tree admission.
    // Never emulate this with a check followed by overwrite-capable rename/copy.
    internal static void Move(string source, string destination)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("renameat2 requires Linux.");
        if (!Path.IsPathFullyQualified(source) || !Path.IsPathFullyQualified(destination))
            throw new InvalidDataException("Create-only bundle rename requires absolute paths.");
        const int atCurrentDirectory = -100;
        const uint noReplace = 1;
        int result;
        try { result = RenameAt2(atCurrentDirectory, source, atCurrentDirectory, destination, noReplace); }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        {
            throw new PlatformNotSupportedException("The required create-only bundle rename is unavailable.", error);
        }
        if (result != 0)
        {
            var error = Marshal.GetLastPInvokeError();
            throw new IOException($"Create-only bundle rename failed (errno {error}); no fallback was attempted.",
                new Win32Exception(error));
        }
    }

    [DllImport("libc", EntryPoint = "renameat2", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int RenameAt2(int sourceDirectory, string source, int destinationDirectory, string destination, uint flags);
}
