using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Restricts the distinct fixture-generation opt-in without relaxing ordinary recovery cases.
/// </summary>
internal static class JointInterfaceFixtureAcquisition
{
    /// <summary>
    /// Gets the diagnostic exit code used only after real applied input has been exported.
    /// </summary>
    internal const int CapturedExitCode = 74;

    /// <summary>
    /// Reads the fixture-generation opt-in and rejects unsupported requested scopes.
    /// </summary>
    /// <param name="parent">
    /// Parent metadata with an optional Boolean FixtureAcquisitionOnly member.
    /// </param>
    /// <param name="windows">
    /// Whether the actual execution platform is Windows.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for the explicit seeded ordinary Windows cut0 probe;
    /// <see langword="false"/> when absent or disabled. Malformed or unsupported opt-ins throw.
    /// </returns>
    internal static bool ReadAndValidate(JsonObject parent, bool windows)
    {
        if (!parent.ContainsKey("FixtureAcquisitionOnly")) return false;
        Require(parent["FixtureAcquisitionOnly"] is JsonValue flag && flag.TryGetValue<bool>(out _));
        if (!parent["FixtureAcquisitionOnly"]!.GetValue<bool>()) return false;
        Require(windows && parent["Cut"] is JsonValue cut && cut.TryGetValue<int>(out var index) && index == 0 &&
            parent["Diagnostic"] is JsonValue diagnostic && diagnostic.TryGetValue<bool>(out var enabled) && !enabled &&
            parent["Fault"] is null && parent["Source42Seed"] is JsonObject selection && !selection.ContainsKey("Usage"));
        return true;
    }

    /// <summary>
    /// Captures detached observed DATA at one of the two real harness boundaries.
    /// </summary>
    /// <param name="root">
    /// The actual owned game root to observe without modifying it.
    /// </param>
    /// <param name="directory">
    /// The export directory outside <paramref name="root"/>; each export has a new name.
    /// </param>
    /// <param name="role">
    /// PreparedOriginal or AppliedBeforeStory, describing the observed boundary only.
    /// </param>
    /// <param name="parent">
    /// Detached parent metadata including the actual nonce and source binding.
    /// </param>
    /// <param name="original">
    /// Actual original tuple at the applied checkpoint; <see langword="null"/> for prepared DATA.
    /// </param>
    /// <returns>
    /// Archive path, hash and descriptive role; no authority is created or transferred.
    /// </returns>
    internal static JsonObject Capture(string root, string directory, string role, JsonObject parent, JsonObject? original = null)
    {
        var operation = OperationForRole(role);
        Require((role == "AppliedBeforeStory") == (original is not null));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".zip");
        JointCleanupDataCapture.Capture(root, path, new JsonObject
        {
            ["Role"] = role, ["Operation"] = operation, ["Root"] = root,
            ["Parent"] = parent.DeepClone(), ["Original"] = original?.DeepClone()
        });
        var result = new JsonObject { ["Role"] = role, ["Archive"] = path,
            ["Sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_fixture_data_capture", Export = result }));
        return result;
    }

    /// <summary>
    /// Verifies both exported roles, archive contents and the producing child metadata.
    /// </summary>
    /// <param name="exports">
    /// The prepared and applied export indexes from the distinct child result, in that order.
    /// </param>
    /// <param name="directory">
    /// The expected external export directory.
    /// </param>
    /// <param name="root">
    /// The actual root observed by the child.
    /// </param>
    /// <param name="pid">
    /// The independently observed resumer process ID.
    /// </param>
    /// <param name="parent">
    /// Exact parent metadata to compare with each archive's detached provenance.
    /// </param>
    /// <returns>
    /// The applied archive's detached original tuple after verification; it grants no authority.
    /// </returns>
    internal static JsonObject Verify(JsonArray exports, string directory, string root, int pid, JsonObject parent)
    {
        Require(exports.Count == 2);
        JsonObject? original = null;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < exports.Count; i++)
        {
            var item = exports[i]!.AsObject();
            var role = i == 0 ? "PreparedOriginal" : "AppliedBeforeStory";
            Require(item["Role"]!.GetValue<string>() == role);
            var path = Path.GetFullPath(item["Archive"]!.GetValue<string>());
            Require(string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase) && paths.Add(path));
            Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) == item["Sha256"]!.GetValue<string>());
            using var archive = ZipFile.OpenRead(path);
            Require(archive.Entries.Select(entry => entry.FullName).Distinct(StringComparer.Ordinal).Count() == archive.Entries.Count);
            using var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open());
            var manifest = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
            Require(manifest["Kind"]!.GetValue<string>() == "joint_cleanup_observed_data_v1" &&
                !manifest["GrantsAuthority"]!.GetValue<bool>() && manifest["PID"]!.GetValue<int>() == pid);
            var provenance = manifest["Provenance"]!.AsObject();
            Require(provenance["Role"]!.GetValue<string>() == role && provenance["Operation"]!.GetValue<string>() == OperationForRole(role) &&
                provenance["Root"]!.GetValue<string>() == root && JsonNode.DeepEquals(provenance["Parent"], parent));
            if (i == 0) Require(provenance["Original"] is null);
            else
            {
                original = provenance["Original"]!.AsObject();
                Require(original["TurnNumber"]!.GetValue<int>() == 43 &&
                    new[] { "SessionId", "RequestId", "SnapshotToken" }.All(key => !string.IsNullOrEmpty(original[key]!.GetValue<string>())));
            }
            var files = manifest["Files"]!.AsArray();
            Require(files.Count + 1 == archive.Entries.Count);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                var name = file!["Path"]!.GetValue<string>();
                Require(names.Add(name) && (name.StartsWith("game_session/", StringComparison.Ordinal) ||
                    name == ".boe_runtime/session-generation/current.json"));
                using var stream = archive.GetEntry("payload/" + name)!.Open();
                using var bytes = new MemoryStream(); stream.CopyTo(bytes);
                Require(bytes.Length == file["Length"]!.GetValue<long>() &&
                    Convert.ToHexString(SHA256.HashData(bytes.ToArray())) == file["Sha256"]!.GetValue<string>());
            }
            Require(names.Contains(".boe_runtime/session-generation/current.json"));
        }
        return original!;
    }

    /// <summary>
    /// Maps a descriptive export role to its actual harness boundary.
    /// </summary>
    /// <param name="role">
    /// PreparedOriginal or AppliedBeforeStory; other values are rejected.
    /// </param>
    /// <returns>
    /// The exact operation or checkpoint name recorded in the export.
    /// </returns>
    private static string OperationForRole(string role) => role switch
    {
        "PreparedOriginal" => "Resumer.FirstCurrentOwnerCall",
        "AppliedBeforeStory" => "AcceptedOutcomeValidatedBeforeMaterialization",
        _ => throw new InvalidDataException("Unknown fixture DATA role.")
    };

    /// <summary>
    /// Rejects incomplete fixture selection or export evidence.
    /// </summary>
    /// <param name="condition">
    /// The required condition; <see langword="false"/> throws <see cref="InvalidDataException"/>.
    /// </param>
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException("Fixture acquisition metadata or DATA does not match the observed probe.");
    }
}
