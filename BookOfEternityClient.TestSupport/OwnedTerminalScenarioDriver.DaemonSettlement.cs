using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace BookOfEternityClient.Tests;
internal static partial class OwnedTerminalScenarioDriver
{
    // Test-only observation. These persisted bytes never grant ownership or authorize recovery.
    private sealed record DaemonSettlementSnapshot(string Owner, JsonElement Status, JsonElement Guard);
    private static DaemonSettlementSnapshot CaptureDaemonSettlement(string folder, string phase,
        Dictionary<string,object?> evidence)
    {
        var session = Path.GetFullPath(Path.Combine(folder,"root/game_session"));
        var paths = new[] { Path.Combine(session,"game_state/control/gm_daemon_status.json"),
            Path.Combine(folder,"root/.boe_runtime/locks/gm-daemon-instance.lock") };
        var captured = new byte[2][];
        var capture = new List<object>();
        // Preserve both raw inputs before parsing, including malformed/duplicate evidence.
        for(var i=0;i<paths.Length;i++)
        {
            var destination = Path.Combine(folder,$"daemon-{phase}-{(i==0?"status":"guard")}.raw.json");
            try
            {
                captured[i] = File.ReadAllBytes(paths[i]);
                File.WriteAllBytes(destination,captured[i]);
                capture.Add(new { Path=paths[i], RawPath=destination,
                    SHA256=Convert.ToHexString(SHA256.HashData(captured[i])).ToLowerInvariant() });
            }
            catch(Exception failure) { capture.Add(new { Path=paths[i], Failure=failure.ToString() }); }
        }
        evidence["DaemonSettlementCapture-"+phase] = capture;
        if(captured.Any(raw=>raw is null)) throw new IOException("Daemon settlement input unavailable: "+phase);
        JsonElement Read(byte[] raw)
        {
            // Match canonical UTF-8 JSON BOM allowance without replacing malformed UTF-8.
            var offset = raw.AsSpan().StartsWith(new byte[]{0xef,0xbb,0xbf}) ? 3 : 0;
            using var document = JsonDocument.Parse(new UTF8Encoding(false,true).GetString(raw,offset,raw.Length-offset));
            void Unique(JsonElement node)
            {
                if(node.ValueKind==JsonValueKind.Object)
                {
                    var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach(var property in node.EnumerateObject())
                    { if(!names.Add(property.Name)) throw new InvalidDataException("Duplicate daemon evidence field: "+property.Name); Unique(property.Value); }
                }
                else if(node.ValueKind==JsonValueKind.Array) foreach(var item in node.EnumerateArray()) Unique(item);
            }
            Unique(document.RootElement);
            if(document.RootElement.ValueKind!=JsonValueKind.Object) throw new InvalidDataException("Daemon evidence must be an object.");
            return document.RootElement.Clone();
        }
        var status=Read(captured[0]); var guard=Read(captured[1]);
        string ExactString(JsonElement value,string name)
        {
            var field=value.GetProperty(name);
            if(field.ValueKind!=JsonValueKind.String || string.IsNullOrEmpty(field.GetString()))
                throw new InvalidDataException("Expected nonempty daemon string: "+name);
            return field.GetString()!;
        }
        void Version(JsonElement value,string name)
        {
            var field=value.GetProperty(name);
            if(field.ValueKind!=JsonValueKind.Number || !field.TryGetInt32(out var number) || number!=1)
                throw new InvalidDataException("Unsupported daemon evidence version.");
        }
        Version(status,"daemonGuardVersion"); Version(guard,"version");
        var owner=ExactString(status,"daemonGuardOwnerId");
        if(!Guid.TryParseExact(owner,"N",out _) || owner!=ExactString(guard,"ownerId") ||
            session!=ExactString(status,"sessionPath") || session!=ExactString(guard,"sessionPath"))
            throw new InvalidDataException("Daemon evidence original session/owner mismatch.");
        evidence["DaemonSettlementParsed-"+phase] = new { Status=status, Guard=guard };
        return new(owner,status,guard);
    }
    private static void RequireDaemonSettlement(DaemonSettlementSnapshot value, bool settled, string? expectedOwner=null)
    {
        var expected = settled ? JsonValueKind.True : JsonValueKind.False;
        if((expectedOwner is not null && value.Owner!=expectedOwner) ||
            value.Status.GetProperty("status").ValueKind!=JsonValueKind.String ||
            value.Status.GetProperty("status").GetString()!=(settled?"stopped":"running") ||
            value.Status.GetProperty("terminalSettled").ValueKind!=expected ||
            value.Guard.GetProperty("settled").ValueKind!=expected)
            throw new InvalidDataException(settled ? "Original daemon terminal settlement is unconfirmed." : "Fresh original daemon running ownership is unconfirmed.");
    }
}
