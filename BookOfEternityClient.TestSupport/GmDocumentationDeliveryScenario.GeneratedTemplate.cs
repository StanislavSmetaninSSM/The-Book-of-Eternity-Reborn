using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

internal static partial class GmDocumentationDeliveryScenario
{
    private static JsonObject CaptureGeneratedTemplate(byte[] source, string session, JsonObject manifest, string artifacts, Clock clock)
    {
        _ = clock.Work;
        var definition = GmDocumentationGeneratedTemplateSource.Parse(source, GmDocumentationGeneratedTemplateSource.RelativePath);
        var rows = manifest["templates"]!.AsArray().Where(row =>
            row?["relativePath"]?.GetValue<string>() == GmDocumentationGeneratedTemplateSource.RelativePath).ToArray();
        if (rows.Length != 1 || rows[0]!["generated"]?.GetValue<bool>() != true ||
            rows[0]!["role"]?.GetValue<string>() != GmDocumentationGeneratedTemplateSource.Role)
            throw new InvalidDataException("generated.manifest_identity");
        var sessionRoot = Path.GetFullPath(session);
        var context = Path.GetFullPath(Path.Combine(sessionRoot, "game_state/control/gm_context_pack"));
        var expected = Path.GetFullPath(Path.Combine(context, GmDocumentationGeneratedTemplateSource.RelativePath));
        if (manifest["contextPackRoot"]?.GetValue<string>() != context ||
            rows[0]!["sessionPath"]?.GetValue<string>() != expected ||
            !expected.StartsWith(context + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("generated.session_path");
        if (!File.Exists(expected)) throw new InvalidDataException("generated.physical_missing");
        for (FileSystemInfo? entry = new FileInfo(expected); entry is not null && entry.FullName != sessionRoot;
             entry = entry is FileInfo file ? file.Directory : ((DirectoryInfo)entry).Parent)
            if (entry.LinkTarget is not null) throw new InvalidDataException("generated.symlink");
        if (new DirectoryInfo(sessionRoot).LinkTarget is not null) throw new InvalidDataException("generated.session_symlink");
        var bytes = File.ReadAllBytes(expected);
        if (!bytes.SequenceEqual(definition.FileBytes)) throw new InvalidDataException("generated.physical_body");
        var retained = Path.Combine(artifacts, "generated-MORTAL_NPC_UPDATE_TEMPLATE.md");
        File.WriteAllBytes(retained, bytes);
        return new JsonObject
        {
            ["relativePath"] = GmDocumentationGeneratedTemplateSource.RelativePath,
            ["role"] = GmDocumentationGeneratedTemplateSource.Role, ["generated"] = true,
            ["contextPackRoot"] = context, ["sessionPath"] = expected,
            ["physicalPresent"] = true, ["containedWithoutSymlink"] = true,
            ["bytes"] = bytes.Length, ["sha256"] = Hash(bytes),
            ["expectedSha256"] = Hash(definition.FileBytes), ["sourceSha256"] = definition.SourceSha256,
            ["retainedRawPath"] = retained
        };
    }
}
