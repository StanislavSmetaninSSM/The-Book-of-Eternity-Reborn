using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Services.GmRuntime;

internal static class OwnedTerminalSessionFactory
{
    internal static async Task<IOwnedTerminalSession> StartNeutralAsync(NeutralTerminalLaunch launch, CancellationToken token, Action<int>? observeHeldRoot = null)
    {
        var held=await PrepareNeutralAsync(launch,Guid.NewGuid().ToString("N"),token,observeHeldRoot);
        try { await held.ReleaseAsync(token);return held.Session; }
        catch(Exception ex){throw new OwnedTerminalStartException(held.Session,ex);}
    }
    internal sealed class PreparedTerminal(IOwnedTerminalSession session,NativeLineageOwner owner)
    {
        internal IOwnedTerminalSession Session=>session;
        internal Task ReleaseAsync(CancellationToken token)=>owner.ReleaseTerminalAsync(token);
    }
    internal static async Task<PreparedTerminal> PrepareProductionAsync(ProductionMainLaunch launch,string runId,CancellationToken token,Action<int>? observeHeldRoot=null)
    {
        var c=launch.Configuration; launch.Consume();
        NativeLineageOwner owner;
        try { owner=await NativeLineageOwner.PrepareTerminalAsync(c.CreateStart(),c.Supervisor,runId,c.Size.Columns,c.Size.Rows,token,observeHeldRoot); }
        catch(GmWorkerOwnedLaunchException ex) {
            owner=(NativeLineageOwner)ex.Owner;
            IOwnedTerminalSession retained;
            try { retained=new LinuxOwnedTerminalSession(owner); } catch { retained=new PartialNativeTerminalSession(owner); }
            throw new OwnedTerminalStartException(retained,ex);
        }
        try { return new(new LinuxOwnedTerminalSession(owner),owner); }
        catch(Exception ex) { throw new OwnedTerminalStartException(new PartialNativeTerminalSession(owner),ex); }
    }
    internal static async Task<PreparedTerminal> PrepareNeutralAsync(NeutralTerminalLaunch launch,string runId,CancellationToken token,Action<int>? observeHeldRoot=null)
    {
        var package=launch.Package; var scratch=launch.Scratch;
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
        launch.Consume();
        NativeLineageOwner owner;
        try { owner = await NativeLineageOwner.PrepareTerminalAsync(start, supervisor,runId, 80, 25, token, observeHeldRoot); }
        catch (GmWorkerOwnedLaunchException ex) {
            owner=(NativeLineageOwner)ex.Owner;
            IOwnedTerminalSession retained;
            try { retained=new LinuxOwnedTerminalSession(owner); }
            catch { retained=new PartialNativeTerminalSession(owner); }
            throw new OwnedTerminalStartException(retained,ex);
        }
        try { return new(new LinuxOwnedTerminalSession(owner),owner); }
        catch (Exception ex) { throw new OwnedTerminalStartException(new PartialNativeTerminalSession(owner),ex); }
    }
}
