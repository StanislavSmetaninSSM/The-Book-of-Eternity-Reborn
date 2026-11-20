using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Win32.SafeHandles;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmRuntime;

public sealed partial class LinuxDaemonInstanceGuard
{
    internal const string LocalStatusPath = "game_state/control/gm_daemon_status.json";
    internal static bool HasLocalStatusBoundary =>
        OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64;

    internal static bool IsLocalStatusPath(string relative) =>
        HasLocalStatusBoundary && relative.Equals(LocalStatusPath, StringComparison.Ordinal);

    internal static bool IsBelowLocalStatusPath(string relative) =>
        HasLocalStatusBoundary && relative.StartsWith(LocalStatusPath + "/", StringComparison.Ordinal);

    internal sealed record LoadGuardEvidence(LinuxBridgeFifoNative.Identity Root, LinuxBridgeFifoNative.Identity Runtime,
        LinuxBridgeFifoNative.Identity Locks, LinuxBridgeFifoNative.Identity Guard, string ExactImage);

    // This is evidence of matching local bytes, not proof of liveness or authority to adopt a daemon.
    internal static LoadGuardEvidence? ObserveLoadGuard(string basePath)
    {
        using var root = LinuxBridgeFifoNative.OpenDirectory(basePath);
        using var runtime = LinuxBridgeFifoNative.OpenGuardObservationEntry(root, ".boe_runtime", directory: true);
        if (runtime == null) return null;
        using var locks = LinuxBridgeFifoNative.OpenGuardObservationEntry(runtime, "locks", directory: true);
        if (locks == null) return null;
        using var guard = LinuxBridgeFifoNative.OpenGuardObservationEntry(locks, FileName, directory: false);
        if (guard == null) return null;

        const int maxGuardBytes = 4096;
        var bytes = new byte[maxGuardBytes + 1];
        var chunk = new byte[bytes.Length];
        var used = 0;
        while (used < bytes.Length)
        {
            var count = LinuxBridgeFifoNative.Read(guard, chunk, bytes.Length - used, out var error);
            if (count < 0) throw new IOException($"Reading daemon guard evidence failed (errno {error}).");
            if (count == 0) break;
            chunk.AsSpan(0, count).CopyTo(bytes.AsSpan(used));
            used += count;
        }
        if (used == 0 || used > maxGuardBytes) throw new InvalidDataException("Invalid daemon guard evidence size.");

        using var currentRoot = LinuxBridgeFifoNative.OpenDirectory(basePath);
        var rootIdentity = LoadDirectoryIdentity(root);
        if (LoadDirectoryIdentity(currentRoot) != rootIdentity)
            throw new IOException("Daemon guard root changed while observing Load evidence.");
        LinuxBridgeFifoNative.RequireNamedIdentity(root, ".boe_runtime", runtime);
        LinuxBridgeFifoNative.RequireNamedIdentity(runtime, "locks", locks);
        LinuxBridgeFifoNative.RequireNamedIdentity(locks, FileName, guard);
        LinuxBridgeFifoNative.ValidateGuard(guard);
        return new(rootIdentity, LoadDirectoryIdentity(runtime), LoadDirectoryIdentity(locks),
            LinuxBridgeFifoNative.Stat(guard), Convert.ToBase64String(bytes, 0, used));
    }

    // Ordinary sibling directories can change an ancestor's link count without replacing it.
    private static LinuxBridgeFifoNative.Identity LoadDirectoryIdentity(SafeFileHandle handle) =>
        LinuxBridgeFifoNative.Stat(handle) with { Links = 0 };

    internal static TrustedLocalFileImage CaptureLoadStatus(TrustedLocalFileScope scope, string path)
    {
        using var source = new FileStream(scope.ValidateFile(path, allowMissing: false), FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, 4096, FileOptions.SequentialScan);
        var length = source.Length;
        if (length is < 1 or > 65536) throw new InvalidDataException("Invalid daemon status evidence size.");
        var bytes = new byte[(int)length];
        source.ReadExactly(bytes);
        if (source.Length != length)
            throw new InvalidDataException("Daemon status evidence changed during capture.");
        scope.ValidateFile(path, allowMissing: false);
        return TrustedLocalFileImage.FromBytes(bytes);
    }

    internal static void ValidateLocalLoadPair(string session, LoadGuardEvidence? guard, TrustedLocalFileImage? status)
    {
        if (guard == null && status == null) return;
        if (guard == null || status == null)
            throw new InvalidDataException("Load requires a complete local daemon guard/status pair.");
        var statusBytes = status.Bytes ?? throw new InvalidDataException("Missing captured daemon status bytes.");
        var guardDocument = Parse(new UTF8Encoding(false, true).GetString(Convert.FromBase64String(guard.ExactImage)));
        var document = Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(statusBytes));
        var owner = Text(guardDocument, "ownerId");
        if (guardDocument.Count != 4 || Number(guardDocument, "version") != 1 ||
            Text(guardDocument, "sessionPath") != session || !Guid.TryParseExact(owner, "N", out _) ||
            Number(document, "daemonGuardVersion") != 1 || Text(document, "daemonGuardOwnerId") != owner ||
            Text(document, "sessionPath") != session ||
            !ReadLoadSettlement(guardDocument, "settled", out var guardSettled) ||
            !ReadLoadSettlement(document, "terminalSettled", out var statusSettled) ||
            guardSettled != statusSettled ||
            (guardSettled ? Text(document, "status") != "stopped" : Text(document, "status") is not ("running" or "processing")))
            throw new InvalidDataException("Load refuses inconsistent local daemon guard/status evidence.");
    }

    private static bool ReadLoadSettlement(JsonObject document, string property, out bool value)
    {
        value = false;
        return document[property] is JsonValue field && field.TryGetValue(out value);
    }
}
