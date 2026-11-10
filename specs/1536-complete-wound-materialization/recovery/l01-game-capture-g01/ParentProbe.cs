#nullable enable
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Tests;

internal static class ParentProbe
{
    public static int SourceAnchor(int value) => value + 1;
    static void Require(bool b) { if (!b) throw new InvalidOperationException("Parent refusal/containment assertion failed"); }
    public static async Task<int> Main(string[] args)
    {
        var root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        var settings = Path.Combine(root, "settings.json");
        var run = Guid.NewGuid().ToString("N");
        File.WriteAllText(settings, JsonSerializer.Serialize(new { Contract = JointInterfaceManagedCapture.Contract,
            RunId = run, ActualRoot = root, FixtureParent = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), Python = "UNREACHED", Watchdog = "UNREACHED" }));
        Environment.SetEnvironmentVariable("L01_G01_SETTINGS", settings);
        string CorrectHash() => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(settings))).ToLowerInvariant();
        Environment.SetEnvironmentVariable("L01_G01_SETTINGS_SHA256", CorrectHash());
        var rows = new List<object>();
        void Check(string name, Action a) { a(); rows.Add(new { name, passed = true }); }
        Check("wrong_cut_refuses_before_fixture", () => {
            try { JointInterfaceManagedCapture.ClaimOwnedDirectory(0, null); throw new Exception("Wrong cut admitted"); }
            catch (InvalidDataException) { }
        });
        Check("settings_hash_refuses_before_fixture", () => {
            Environment.SetEnvironmentVariable("L01_G01_SETTINGS_SHA256", new string('0',64));
            try { JointInterfaceManagedCapture.ClaimOwnedDirectory(1, null); throw new Exception("Wrong hash admitted"); }
            catch (InvalidDataException) { }
            finally { Environment.SetEnvironmentVariable("L01_G01_SETTINGS_SHA256", CorrectHash()); }
        });
        var own = JointInterfaceManagedCapture.ClaimOwnedDirectory(1,null);
        Check("one_fixture_claim_only", () => {
            try { JointInterfaceManagedCapture.ClaimOwnedDirectory(1,null); throw new Exception("Duplicate claim admitted"); }
            catch (IOException) { }
        });
        var nonce = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(own,"parent.json"), "{}");
        using var self = Process.GetCurrentProcess();
        var tick = ulong.Parse(File.ReadAllText($"/proc/{self.Id}/stat").Split(')')[^1].Split(' ',StringSplitOptions.RemoveEmptyEntries)[19]);
        var delivery = new JointInterfaceDiagnosticDelivery(new(self.Id,tick,own,nonce,1));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var deadline = Stopwatch.GetTimestamp()+220*Stopwatch.Frequency;
        var result = await JointInterfaceManagedCapture.ObserveAsync(self,tick,own,nonce,1,own,"UNREACHED",[],deadline,delivery,cancelled.Token);
        Check("cancel_before_trigger_never_launches_watchdog", () => Require(!result.WatchdogLaunched && result.ObserverErrors.Length==0));
        using (var record = JsonDocument.Parse(File.ReadAllText(Path.Combine(own,"capture/parent-capture.json")) ))
            Check("cancel_report_retains_unknown_phase", () => Require(record.RootElement.GetProperty("Cancelled").GetBoolean() && record.RootElement.GetProperty("CapturePhase").GetString()=="UNKNOWN"));
        var broken = Path.Combine(root,"broken"); Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken,"capture"),"not a directory");
        var failure = await JointInterfaceManagedCapture.ObserveAsync(self,tick,broken,nonce,1,broken,"UNREACHED",[],deadline,delivery,cancelled.Token);
        Check("observer_output_fault_is_contained_and_reported", () => Require(!failure.WatchdogLaunched && failure.ObserverErrors.Length>0));
        Console.WriteLine(JsonSerializer.Serialize(new { kind="G01_PARENT_PURE_PROOF", cases=rows, passed=rows.Count,
            gameProcesses=0, fixtureChildren=0, requests=0, clockStamp=Stopwatch.GetTimestamp(), clockFrequency=Stopwatch.Frequency,
            moduleMvid=typeof(ParentProbe).Assembly.ManifestModule.ModuleVersionId,
            methodToken=typeof(ParentProbe).GetMethod(nameof(SourceAnchor))!.MetadataToken,
            methodIlBytes=typeof(ParentProbe).GetMethod(nameof(SourceAnchor))!.GetMethodBody()!.GetILAsByteArray()!.Length }));
        return 0;
    }
}
