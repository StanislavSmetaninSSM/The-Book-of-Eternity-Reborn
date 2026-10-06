using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Services.GmRuntime;

internal static class OwnedTerminalSessionFactory
{
    internal static async Task<IOwnedTerminalSession> StartNeutralAsync(string package, string scratch, CancellationToken token, Action<int>? observeHeldRoot = null)
    {
        var supervisor = GmWorkerNativePackage.Validate(package);
        var manifest = Path.Combine(package, "neutral-terminal-manifest.json");
        if (new FileInfo(manifest).Length > 8192) throw new InvalidDataException("Neutral fixture manifest exceeds bound.");
        using var doc = JsonDocument.Parse(File.ReadAllBytes(manifest)); var r = doc.RootElement;
        if (r.GetProperty("schemaVersion").GetInt32() != 1 || r.GetProperty("fixture").GetString() != "neutral-cli" ||
            r.GetProperty("source").GetString() != "tests/fixtures/LinuxTerminal/neutral-cli.c" ||
            r.GetProperty("profile").GetString() != "neutral-v1" || r.GetProperty("argv").GetArrayLength() != 0)
            throw new InvalidDataException("Only the fixed neutral fixture is admitted.");
        var cli = Path.GetFullPath(Path.Combine(package, "neutral-cli"));
        using (var f = File.OpenRead(cli))
            if (Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant() != r.GetProperty("binarySha256").GetString()) throw new InvalidDataException("Neutral fixture binary mismatch.");
        var start = new ProcessStartInfo(cli) { WorkingDirectory = Path.GetFullPath(scratch), UseShellExecute = false };
        var owner = await NativeLineageOwner.StartTerminalAsync(start, supervisor, 80, 25, token, observeHeldRoot);
        try { return new LinuxOwnedTerminalSession(owner); }
        catch (Exception ex) { throw new GmWorkerOwnedLaunchException("Terminal adapter retains original owner.", owner, ex); }
    }
}
