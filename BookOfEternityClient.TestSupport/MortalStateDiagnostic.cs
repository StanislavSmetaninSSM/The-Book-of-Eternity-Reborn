using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>One strict validator invocation over explicitly pinned relocated DATA, never live authority.</summary>
internal static class MortalStateDiagnostic
{
    internal sealed record Entry(string Path, string Source, string SHA256);
    internal sealed record Manifest(string SourceRoot, string ProbePath, string ProbeSHA256, Entry[] Files);
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static void NoLinks(string path)
    {
        for (var p = new FileInfo(Path.GetFullPath(path)); p != null; p = p.Directory == null ? null : new FileInfo(p.Directory.FullName))
            if (p.LinkTarget != null || ((File.Exists(p.FullName) || Directory.Exists(p.FullName)) && (File.GetAttributes(p.FullName) & FileAttributes.ReparsePoint) != 0))
                throw new InvalidDataException("Linked path: " + path);
    }
    private static bool Under(string path, string root) => path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    private static string Member(string root, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\\') || Path.IsPathRooted(name) || name.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Noncanonical DATA path");
        return Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar));
    }
    internal static Dictionary<string,string> Inventory(string root) => Directory.Exists(root)
        ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)
            .ToDictionary(p => Path.GetRelativePath(root,p).Replace('\\','/'), Hash, StringComparer.Ordinal)
        : new(StringComparer.Ordinal);

    /// <summary>Checks every source and destination before copying; source probe pins define the exact DATA set.</summary>
    internal static Manifest Prepare(string manifestPath, string destination)
    {
        NoLinks(manifestPath); NoLinks(destination);
        if (File.Exists(destination) || Directory.Exists(destination)) throw new InvalidDataException("Diagnostic destination must not exist");
        var m = JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(manifestPath)) ?? throw new InvalidDataException("Missing manifest");
        var source = Path.GetFullPath(m.SourceRoot); var dest = Path.GetFullPath(destination);
        if (source != m.SourceRoot || dest != destination || Under(dest,source) || Under(source,dest) || dest == source)
            throw new InvalidDataException("Aliased or overlapping roots");
        NoLinks(source); NoLinks(m.ProbePath);
        if (!Under(m.ProbePath,source) || Hash(m.ProbePath) != m.ProbeSHA256) throw new InvalidDataException("Source probe mismatch");
        using var probe = JsonDocument.Parse(File.ReadAllBytes(m.ProbePath));
        if (!probe.RootElement.GetProperty("Success").GetBoolean() || probe.RootElement.GetProperty("AcceptedGameTurns").GetInt32()!=1)
            throw new InvalidDataException("Not accepted source DATA");
        var expected=probe.RootElement.GetProperty("FinalWorldPins").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetString()!,StringComparer.Ordinal);
        if (expected.Count != 106) throw new InvalidDataException("Expected exact 106 source DATA pins");
        expected.Add("config.json",probe.RootElement.GetProperty("FinalConfigSHA256").GetString()!);
        if (m.Files.Length != expected.Count || m.Files.Select(e=>e.Path).Distinct(StringComparer.Ordinal).Count()!=expected.Count)
            throw new InvalidDataException("DATA inventory differs");
        foreach(var e in m.Files)
        {
            _=Member(dest,e.Path);
            if (!(e.Path.StartsWith("game_state/",StringComparison.Ordinal)||e.Path.StartsWith("stories/",StringComparison.Ordinal)||e.Path.StartsWith("lore/",StringComparison.Ordinal)||e.Path.StartsWith("output/",StringComparison.Ordinal)||e.Path=="config.json") ||
                e.Path.Split('/').Any(p=>p is "_runtime" or ".boe_runtime" or "pending_turn_snapshot" or "pending_turn_snapshot.json" or "pending_turn_snapshot.authority.json"))
                throw new InvalidDataException("Operational authority path refused");
            if (!expected.TryGetValue(e.Path,out var digest)||digest!=e.SHA256) throw new InvalidDataException("Probe DATA join differs");
            NoLinks(e.Source);
            if (Path.GetFullPath(e.Source)!=e.Source || !Under(e.Source,source)||Hash(e.Source)!=digest) throw new InvalidDataException("Source DATA mismatch");
        }
        // Exclusive reservation prevents another diagnostic from sharing this destination.
        using var reservation=new FileStream(dest+".reservation",FileMode.CreateNew,FileAccess.Write,FileShare.None);
        Directory.CreateDirectory(dest);
        foreach(var e in m.Files)
        {
            var target=Member(Path.Combine(dest,"game_session"),e.Path); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var bytes=File.ReadAllBytes(e.Source);
            if(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=e.SHA256) throw new InvalidDataException("Source changed during copy");
            using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)) output.Write(bytes);
            if(Hash(target)!=e.SHA256) throw new InvalidDataException("DATA readback differs");
        }
        return m;
    }

    /// <summary>Retains all diagnostic evidence, including exceptions; zero issues does not authorize a game action.</summary>
    internal static async Task<int> RunAsync(string manifestPath,string destination,string resultPath)
    {
        NoLinks(resultPath);
        if(File.Exists(resultPath)||Directory.Exists(resultPath)||Under(Path.GetFullPath(resultPath),Path.GetFullPath(destination))) throw new InvalidDataException("Result must be new and outside DATA root");
        var declared=JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(manifestPath)) ?? throw new InvalidDataException("Missing manifest");
        if (Under(Path.GetFullPath(resultPath),Path.GetFullPath(declared.SourceRoot))) throw new InvalidDataException("Result overlaps original source");
        object? issues=null; string? failure=null; Manifest? manifest=null;
        Dictionary<string,string>? before=null,afterConstructor=null,after=null;
        try
        {
            manifest=Prepare(manifestPath,destination); before=Inventory(destination);
            var fs=new FileSystemManager(destination,NullLogger<FileSystemManager>.Instance);
            afterConstructor=Inventory(destination);
            var validator=new ValidationService(fs,NullLogger<ValidationService>.Instance);
            var found=await validator.ValidateGameStateAsync();
            issues=found.Select(x=>new {x.Code,x.FilePath,x.Severity,x.Category,x.Message,x.Actor,x.Section,x.Expected,x.Actual,x.RepairHint}).ToArray();
        }
        catch(Exception ex) { failure=ex.ToString(); }
        finally { after=Inventory(destination); }
        var sourceUnchanged=manifest?.Files.All(e=>File.Exists(e.Source)&&Hash(e.Source)==e.SHA256);
        var result=new { Kind="strict-relocated-state-diagnostic",PublicationAuthority=false,LiveCauseQualified=false,ManifestSHA256=Hash(manifestPath),Issues=issues,Exception=failure,Before=before,AfterConstructor=afterConstructor,AfterValidation=after,SourceUnchanged=sourceUnchanged };
        await using(var output=new FileStream(resultPath,FileMode.CreateNew,FileAccess.Write,FileShare.None)) await JsonSerializer.SerializeAsync(output,result,new JsonSerializerOptions {WriteIndented=true});
        return failure is null && sourceUnchanged==true ? 0 : 1;
    }
}
