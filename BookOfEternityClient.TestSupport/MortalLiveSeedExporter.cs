using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Test-only initial setup. Never an accepted GM turn or a restoration API.
internal static class MortalLiveSeedExporter
{
    internal const int SetupTurn = 41;
    internal const string StoryPath = "stories/mortal_life_2.jsonl";
    internal static async Task ExportAsync(string basePath, string exampleSessionPath, string manifestPath, bool offscreen = false)
    {
        basePath = Path.GetFullPath(basePath); manifestPath = Path.GetFullPath(manifestPath);
        if (File.Exists(basePath) || (Directory.Exists(basePath) && Directory.EnumerateFileSystemEntries(basePath).Any()))
            throw new InvalidOperationException("Seed export requires a new or empty base; never overwrite a world.");
        if (File.Exists(manifestPath) || Directory.Exists(manifestPath))
            throw new InvalidOperationException("Seed manifest already exists.");
        // Both refusals above precede directory creation, copying and validators.
        Directory.CreateDirectory(basePath);
        var fs = new FileSystemManager(basePath, NullLogger<FileSystemManager>.Instance);
        await MortalLiveSeedBuilder.PrepareAsync(fs, exampleSessionPath,
            offscreen ? MortalLiveSeedBuilder.SeedOffscreenActorsAsync : MortalLiveSeedBuilder.SeedActorsAsync, stateOnly: true);
        var sessionId = "seeded_live_mortal_" + Guid.NewGuid().ToString("N");
        await fs.WriteFileAtomicAsync("game_state/history/chat_log.json", new JsonObject
        { ["sessionId"] = sessionId, ["entries"] = new JsonArray() }.ToJsonString());
        var storyDirectory = Path.Combine(fs.GameSessionPath, "stories");
        if (Directory.Exists(storyDirectory) && Directory.EnumerateFileSystemEntries(storyDirectory).Any())
            throw new InvalidDataException("Initial setup must not contain stories from another world.");
        var entry = JsonSerializer.Serialize(new StoryEntry
        {
            Turn = SetupTurn, Timestamp = DateTimeOffset.UtcNow.ToString("O"), Realm = "Mortal World",
            Player = "[TEST SETUP ONLY]", Narrative = "Initial test arrangement, not an accepted GM response or player action.",
            Location = "loc_valmont_private_chambers"
        });
        await fs.WriteFileAtomicAsync(StoryPath, entry + "\n");
        var storyFile = Path.Combine(fs.GameSessionPath, StoryPath);
        // FileSystemManager's canonical UTF-8 writer includes one preamble.
        var expected = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(entry + "\n")).ToArray();
        var beforeValidation = await File.ReadAllBytesAsync(storyFile);
        if (!beforeValidation.AsSpan().SequenceEqual(expected))
            throw new InvalidDataException("Unexpected canonical setup story encoding.");
        AssertNoRuntimeArtifacts(basePath, fs.GameSessionPath);
        var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        // Off-screen rows in UpdateNPCs are settled NPC DATA, not a GM
        // full-object resend. Use the production idle/canonical read reservation.
        var issues = offscreen
            ? await SessionOperationContext.RunParticipatingCurrentSessionAsync(fs,
                validator.ValidateOrdinaryPreSendGameStateAsync)
            : await validator.ValidateGameStateAsync();
        if (issues.Count != 0) throw new InvalidDataException("Initial full validation refused: " + JsonSerializer.Serialize(issues));
        AssertNoRuntimeArtifacts(basePath, fs.GameSessionPath);
        var storyFiles = Directory.GetFiles(storyDirectory, "*", SearchOption.AllDirectories);
        if (storyFiles.Length != 1 || Path.GetFullPath(storyFiles[0]) != Path.GetFullPath(Path.Combine(fs.GameSessionPath, StoryPath)))
            throw new InvalidDataException("Unexpected initial story inventory.");
        var raw = await File.ReadAllBytesAsync(storyFiles[0]);
        if (!raw.AsSpan().SequenceEqual(beforeValidation))
            throw new InvalidDataException("Setup story changed during validation.");
        var manifest = new
        {
            schemaVersion = 1, kind = "seeded-live-mortal", acceptedGmTurn = false,
            basePath, sessionId, setupTurn = SetupTurn, storyPath = StoryPath,
            setupStoryEntrySha256 = Convert.ToHexString(SHA256.HashData(raw.AsSpan(0, raw.Length - 1))).ToLowerInvariant(),
            storyFiles = new[] { new { path = StoryPath, sha256 = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant() } },
            validationIssues = 0, currentRealm = "Mortal World", currentIncarnation = 2
        };
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await using var output = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(output, manifest, new JsonSerializerOptions { WriteIndented = true });
    }

    private static void AssertNoRuntimeArtifacts(string root, string session)
    {
        // Empty standard scaffolding is not pending work; never export a file in it.
        foreach (var folder in new[] { "input", "output", "ready" })
        {
            var path = Path.Combine(session, folder);
            if (File.Exists(path) || (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any()))
                throw new InvalidDataException("Unexpected runtime contents: " + folder);
        }
        foreach (var path in new[] { "queue", "_runtime", "game_state/control/pending_turn_snapshot",
            "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json",
            "game_state/control/validation_repair_request.json", "game_state/control/gm_bridge_status.json",
            "game_state/control/gm_turn_helper.bootstrap.ps1" })
            if (File.Exists(Path.Combine(session, path)) || Directory.Exists(Path.Combine(session, path)))
                throw new InvalidDataException("Unexpected exported runtime artifact: " + path);
        if (Directory.Exists(Path.Combine(root, ".boe_runtime/gm-runs")))
            throw new InvalidDataException("Seed contains main ownership artifacts.");
    }
}
