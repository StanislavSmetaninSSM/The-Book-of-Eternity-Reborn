#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

// Offline only: reads a private report and hash-bound PE/PDB. Never requests a dump.
internal static class MapFrames
{
    static string Hash(string p) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant();
    static uint Hex(JsonElement e, string key) => uint.Parse(e.GetProperty(key).GetString()!.Replace("0x", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    public static int Main(string[] args)
    {
        if (args.Length != 3) return 2; // report, immutable module manifest, private sanitized output
        using var report = JsonDocument.Parse(File.ReadAllBytes(args[0]));
        using var settings = JsonDocument.Parse(File.ReadAllBytes(args[1]));
        var sourceRoot = Path.GetFullPath(settings.RootElement.GetProperty("SourceRoot").GetString()!);
        var frames = new List<object>(); var limitations = new List<string>();
        var all = report.RootElement.GetProperty("payload").GetProperty("threads").EnumerateArray()
            .SelectMany(t => t.GetProperty("stack_frames").EnumerateArray().Select(f => (Thread: t, Frame: f)))
            .Where(x => x.Frame.TryGetProperty("is_managed", out var m) && (m.ValueKind == JsonValueKind.True || m.ValueKind == JsonValueKind.String && m.GetString() == "true"))
            .ToArray();
        foreach (var module in settings.RootElement.GetProperty("MapModules").EnumerateArray())
        {
            var dll = module.GetProperty("Dll").GetString()!;
            if (Hash(dll) != module.GetProperty("DllSha256").GetString()) throw new InvalidDataException("PE hash differs");
            using var pe = new PEReader(File.OpenRead(dll));
            var md = pe.GetMetadataReader();
            var mvid = md.GetGuid(md.GetModuleDefinition().Mvid);
            MetadataReaderProvider? pdbOwner = null; MetadataReader? pdb = null;
            try
            {
                if (module.TryGetProperty("Pdb", out var pdbPath) && pdbPath.ValueKind == JsonValueKind.String)
                {
                    var path = pdbPath.GetString()!;
                    if (Hash(path) != module.GetProperty("PdbSha256").GetString()) throw new InvalidDataException("PDB hash differs");
                    pdbOwner = MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(path));
                    pdb = pdbOwner.GetMetadataReader();
                    var id = new Guid(pdb.DebugMetadataHeader!.Id.Take(16).ToArray());
                    var codeView = pe.ReadDebugDirectory().Where(d => d.Type == DebugDirectoryEntryType.CodeView).ToArray();
                    if (codeView.Length != 1 || pe.ReadCodeViewDebugDirectoryData(codeView[0]).Guid != id)
                        throw new InvalidDataException("PDB does not match PE");
                }
                foreach (var item in all.Where(x => x.Frame.TryGetProperty("filename", out var f) && Path.GetFileName(f.GetString()) == Path.GetFileName(dll)))
                {
                    if (frames.Count >= 256) { limitations.Add("SANITIZED_FRAME_LIMIT256"); break; }
                    var f = item.Frame;
                    if (!Guid.TryParse(f.GetProperty("guid").GetString(), out var actual) || actual != mvid)
                    { limitations.Add("FRAME_MODULE_MVID_MISMATCH"); continue; }
                    uint token, il;
                    try { token = Hex(f, "token"); il = Hex(f, "il_offset"); }
                    catch (Exception e) when (e is FormatException or OverflowException or InvalidOperationException)
                    { limitations.Add("INVALID_METHOD_OR_IL"); continue; }
                    var row = (int)(token & 0x00ffffff);
                    if ((token & 0xff000000) != 0x06000000 || row == 0 || row > md.MethodDefinitions.Count || il > int.MaxValue)
                    { limitations.Add("UNMAPPABLE_METHODDEF_OR_IL"); continue; }
                    var method = md.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
                    var type = md.GetTypeDefinition(method.GetDeclaringType());
                    string? source = null; int? line = null;
                    if (pdb is not null && row <= pdb.GetTableRowCount(TableIndex.MethodDebugInformation))
                    {
                        var info = pdb.GetMethodDebugInformation(MetadataTokens.MethodDebugInformationHandle(row));
                        var points = info.GetSequencePoints().Where(p => !p.IsHidden && p.Offset <= il).OrderBy(p => p.Offset).ToArray();
                        if (points.Length != 0)
                        {
                            var point = points[^1];
                            var document = point.Document.IsNil ? info.Document : point.Document;
                            if (document.IsNil) { limitations.Add("NO_SOURCE_DOCUMENT"); continue; }
                            var name = pdb.GetString(pdb.GetDocument(document).Name);
                            var full = Path.GetFullPath(name, sourceRoot);
                            if (full.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                            { source = Path.GetRelativePath(sourceRoot, full); line = point.StartLine; }
                        }
                    }
                    var tidText = item.Thread.GetProperty("native_thread_id").GetString()!;
                    if (!ulong.TryParse(tidText.Replace("0x", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var tid))
                    { limitations.Add("INVALID_NATIVE_TID"); continue; }
                    frames.Add(new { nativeTid = tid, module = Path.GetFileName(dll), moduleMvid = mvid,
                        methodDef = "0x" + token.ToString("x8"), ilOffset = "0x" + il.ToString("x"),
                        type = md.GetString(type.Namespace) + "." + md.GetString(type.Name), method = md.GetString(method.Name),
                        sourceFile = source, sourceLine = line });
                }
            }
            finally { pdbOwner?.Dispose(); }
        }
        var result = new { kind = "G01_SANITIZED_MANAGED_MAPPING", managedFrameCandidates = all.Length,
            mappedFrames = frames, limitations = limitations.Distinct().ToArray(),
            capturePhase = "UNKNOWN", suspensionDuration = "UNKNOWN",
            interpretation = frames.Count == 0 ? "NO_BOUND_MANAGED_METHOD" : "BOUND_METHODDEF_IL_ONLY_REVIEW_SOURCE_POSITIONS",
            requests = 0, offlineOnly = true };
        File.WriteAllText(args[2], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return 0;
    }
}
