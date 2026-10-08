using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Retires a genuinely signed inactive cohort with missing rollback bytes into exact, repeat-safe diagnostic copies.
    /// </summary>
    /// <returns>
    /// A task completing after preservation, narrow removal and a mutation-free repeated attempt are checked.
    /// </returns>
    [Fact]
    public async Task InactiveSnapshotEvidence_ArchivesExactCohortWithoutTouchingWorldOrRollbackFiles()
    {
        var fixture = await CreateInactiveSnapshotEvidenceFixtureAsync();
        await using var context = fixture.Context;
        await context.WriteExactJsonAsync("game_state/core/unrelated.json.rollback.keep", "{\"keep\":true}");
        var before = await ReadSpiritualEntryGuardFilesAsync(context);

        Assert.True(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(
            fixture.Engine, "ArchiveInactivePendingSnapshotEvidenceAsync")));

        var after = await ReadSpiritualEntryGuardFilesAsync(context);
        AssertInactiveSnapshotEvidenceArchive(before, after);
        foreach (var pair in before.Where(pair => !IsInactiveSnapshotEvidenceSource(pair.Key)))
            Assert.Equal(pair.Value, after[pair.Key]);
        Assert.DoesNotContain(after.Keys, IsInactiveSnapshotEvidenceSource);
        Assert.False(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(
            fixture.Engine, "ArchiveInactivePendingSnapshotEvidenceAsync")));
        await AssertSpiritualEntryGuardFilesAsync(context, after);
    }

    /// <summary>
    /// Refuses active controls, foreign or future tuples and malformed or ambiguous genuine metadata without changing files.
    /// </summary>
    /// <param name="variant">
    /// The independent active-control or metadata condition which makes retirement ineligible.
    /// </param>
    /// <returns>
    /// A task completing after the complete physical inventory remains byte-identical.
    /// </returns>
    [Theory]
    [InlineData("input/turn_request.json")]
    [InlineData("ready/turn_complete.json")]
    [InlineData("ready/turn_error.json")]
    [InlineData("game_state/control/validation_repair_request.json")]
    [InlineData("game_state/control/validation_repair_ready.json")]
    [InlineData("game_state/control/terminal_protocol_failure_request.json")]
    [InlineData("game_state/control/spiritual_wound_capture_checkpoint.json")]
    [InlineData("game_state/control/pending_spiritual_wound_decisions.json")]
    [InlineData("game_state/wounds/wound_commands.json")]
    [InlineData("foreign_session")]
    [InlineData("future_turn")]
    [InlineData("malformed_manifest")]
    [InlineData("duplicate_manifest")]
    [InlineData("duplicate_authority")]
    [InlineData("duplicate_payload")]
    [InlineData("null_payload_map")]
    [InlineData("missing_authority")]
    [InlineData("untrusted_manifest_path")]
    public async Task InactiveSnapshotEvidence_RefusesActiveForeignAndAmbiguousCohorts(string variant)
    {
        var fixture = await CreateInactiveSnapshotEvidenceFixtureAsync();
        await using var context = fixture.Context;
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        if (variant.Contains('/'))
            await context.WriteExactJsonAsync(variant, "{}");
        else if (variant == "foreign_session")
            GetPrivateField<GameLoop>(fixture.Engine, "_gameLoop").SetSession("foreign_session", 42);
        else if (variant == "future_turn")
            GetPrivateField<GameLoop>(fixture.Engine, "_gameLoop").SetSession("session_entry_guard", 41);
        else if (variant == "missing_authority")
            context.FileSystem.DeleteFile(PendingTurnSnapshotAuthority.AuthorityPath);
        else
        {
            var path = variant is "duplicate_authority" or "duplicate_payload" or "null_payload_map"
                ? PendingTurnSnapshotAuthority.AuthorityPath : manifestPath;
            var original = (await context.FileSystem.ReadFileAsync(path))!;
            var replacement = original;
            switch (variant)
            {
                case "malformed_manifest":
                    replacement = "{\"sessionId\":";
                    break;
                case "duplicate_manifest":
                    replacement = "{\"sessionId\":\"foreign\"," + original.TrimStart()[1..];
                    break;
                case "duplicate_authority":
                    replacement = "{\"formatVersion\":0," + original.TrimStart()[1..];
                    break;
                case "duplicate_payload":
                case "null_payload_map":
                    var envelope = Assert.IsType<JsonObject>(JsonNode.Parse(original));
                    var payload = Encoding.UTF8.GetString(Convert.FromBase64String(envelope["payloadJsonBase64"]!.GetValue<string>()));
                    var payloadText = "{\"sessionId\":\"foreign\"," + payload.TrimStart()[1..];
                    if (variant == "null_payload_map")
                    {
                        var payloadObject = Assert.IsType<JsonObject>(JsonNode.Parse(payload));
                        payloadObject["files"] = null;
                        payloadText = payloadObject.ToJsonString();
                    }
                    var bytes = Encoding.UTF8.GetBytes(payloadText);
                    envelope["payloadJsonBase64"] = Convert.ToBase64String(bytes);
                    envelope["payloadSha256"] = Convert.ToHexString(SHA256.HashData(bytes));
                    replacement = envelope.ToJsonString();
                    break;
                case "untrusted_manifest_path":
                    var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(original));
                    manifest["files"]!.AsObject()["game_state/core/soul_state.json"] = "../../outside.json";
                    replacement = manifest.ToJsonString();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown evidence refusal.");
            }
            await context.WriteExactJsonAsync(path, replacement);
        }
        var before = await ReadSpiritualEntryGuardFilesAsync(context);

        Assert.False(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(
            fixture.Engine, "ArchiveInactivePendingSnapshotEvidenceAsync")));

        await AssertSpiritualEntryGuardFilesAsync(context, before);
    }

    /// <summary>
    /// Keeps all original evidence on copy, verification or source-drift failure and retains the full archive on partial removal.
    /// </summary>
    /// <param name="fault">
    /// The deterministic copy, readback, source drift or final metadata deletion boundary to interrupt.
    /// </param>
    /// <returns>
    /// A task completing after the injected boundary and its exact evidence-preservation outcome are verified.
    /// </returns>
    [Theory]
    [InlineData("copy")]
    [InlineData("readback")]
    [InlineData("readback_drift")]
    [InlineData("source_drift")]
    [InlineData("partial_removal")]
    public async Task InactiveSnapshotEvidence_RetainsEvidenceWhenArchivalCannotFinish(string fault)
    {
        var fixture = await CreateInactiveSnapshotEvidenceFixtureAsync();
        await using var context = fixture.Context;
        var before = await ReadSpiritualEntryGuardFilesAsync(context);
        var source = before.Keys.First(path => path.Replace('\\', '/').StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.Ordinal));
        var changedBytes = Encoding.UTF8.GetBytes("{\"changedOutsideLease\":true}");
        var triggered = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = async path =>
            {
                if (!triggered && path.StartsWith("diagnostics/", StringComparison.Ordinal) && fault is "copy" or "source_drift")
                {
                    triggered = true;
                    if (fault == "copy")
                        throw new InvalidDataException("Injected evidence copy failure.");
                    await File.WriteAllBytesAsync(Path.Combine(context.FileSystem.GameSessionPath, source), changedBytes);
                }
                if (fault == "partial_removal" && path == "game_state/control/pending_turn_snapshot.json")
                {
                    triggered = true;
                    throw new InvalidDataException("Injected final manifest removal failure.");
                }
            },
            BeforeCanonicalReadOpenAsync = async path =>
            {
                if (fault is "readback" or "readback_drift" && path.StartsWith("diagnostics/", StringComparison.Ordinal) &&
                    File.Exists(Path.Combine(context.FileSystem.GameSessionPath, path)))
                {
                    triggered = true;
                    if (fault == "readback")
                        throw new InvalidDataException("Injected evidence readback failure.");
                    await File.WriteAllBytesAsync(Path.Combine(context.FileSystem.GameSessionPath, path), changedBytes);
                }
            }
        };
        var fileSystem = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, hooks);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: fileSystem);
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession("session_entry_guard", 42);

        Assert.False(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(engine, "ArchiveInactivePendingSnapshotEvidenceAsync")));

        Assert.True(triggered);
        var after = await ReadSpiritualEntryGuardFilesAsync(context);
        if (fault == "partial_removal")
        {
            AssertInactiveSnapshotEvidenceArchive(before, after);
            Assert.True(after.ContainsKey(Path.Combine("game_state", "control", "pending_turn_snapshot.json")));
            Assert.True(after.Keys.Count(IsInactiveSnapshotEvidenceSource) < before.Keys.Count(IsInactiveSnapshotEvidenceSource));
            Assert.False(after.ContainsKey(PendingTurnSnapshotAuthority.AuthorityPath));
            // Diagnostics never become authority to finish an interrupted removal.
            Assert.False(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(
                fixture.Engine, "ArchiveInactivePendingSnapshotEvidenceAsync")));
            await AssertSpiritualEntryGuardFilesAsync(context, after);
            foreach (var pair in before.Where(pair => !IsInactiveSnapshotEvidenceSource(pair.Key)))
                Assert.Equal(pair.Value, after[pair.Key]);
        }
        else
        {
            foreach (var pair in before)
                Assert.Equal(fault == "source_drift" && pair.Key == source ? changedBytes : pair.Value, after[pair.Key]);
            Assert.Equal(before.Keys.Order(StringComparer.Ordinal),
                after.Keys.Where(path => !path.Replace('\\', '/').StartsWith("diagnostics/", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        }
        if (fault == "readback")
        {
            Assert.Contains(after.Keys, path => path.Replace('\\', '/').StartsWith("diagnostics/", StringComparison.Ordinal));
            // The first verified index remains at its content-addressed location; a normal retry reuses it exactly.
            Assert.True(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(
                fixture.Engine, "ArchiveInactivePendingSnapshotEvidenceAsync")));
            AssertInactiveSnapshotEvidenceArchive(before, await ReadSpiritualEntryGuardFilesAsync(context));
        }
        if (fault == "readback_drift")
        {
            Assert.Contains(after.Keys, path => path.Replace('\\', '/').StartsWith("diagnostics/", StringComparison.Ordinal));
            // A conflicting existing diagnostic copy is not overwritten and cannot justify removing originals.
            Assert.False(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(
                fixture.Engine, "ArchiveInactivePendingSnapshotEvidenceAsync")));
            await AssertSpiritualEntryGuardFilesAsync(context, after);
        }
    }

    /// <summary>
    /// Builds a real signed original and models an old inactive snapshot whose destructive rollback backup is unavailable.
    /// </summary>
    /// <returns>
    /// The owned disposable fixture and engine with no active input and an explicitly seeded same-session turn boundary of 42.
    /// </returns>
    private async Task<(ResourceMaterializationTestContext Context, GameEngine Engine)> CreateInactiveSnapshotEvidenceFixtureAsync()
    {
        var fixture = await CreateSpiritualEntryGuardOriginalAsync(absentConflict: false);
        GetPrivateField<GameLoop>(fixture.Engine, "_gameLoop").SetSession("session_entry_guard", 42);
        await InvokePrivateTaskResultAsync(fixture.Engine, "CaptureCurrentSessionGenerationAsync");
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(
            (await fixture.Context.FileSystem.ReadFileAsync("game_state/control/pending_turn_snapshot.json"))!));
        var backup = manifest["rollbackBackups"]!.AsObject().First().Value!.GetValue<string>();
        fixture.Context.FileSystem.DeleteFile(backup);
        fixture.Context.FileSystem.DeleteFile("input/turn_request.json");
        var resolution = await InvokePrivateTaskResultAsync(fixture.Engine, "ResolveActivePendingTurnSnapshotContextAsync");
        Assert.Equal("Unusable", resolution.GetType().GetProperty("Status")!.GetValue(resolution)!.ToString());
        return (fixture.Context, fixture.Engine);
    }

    /// <summary>
    /// Identifies only the fixed active metadata and snapshot payload directory in physical fixture inventories.
    /// </summary>
    /// <param name="path">
    /// Session-relative physical path using either platform separator.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the path belongs to the narrowly permitted evidence cohort;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsInactiveSnapshotEvidenceSource(string path)
    {
        path = path.Replace('\\', '/');
        return path is "game_state/control/pending_turn_snapshot.json" or "game_state/control/pending_turn_snapshot.authority.json" ||
               path.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves a single deterministic diagnostic index and exact blobs preserve every source path, byte and content hash.
    /// </summary>
    /// <param name="before">
    /// Genuine original physical inventory before archival began.
    /// </param>
    /// <param name="after">
    /// Physical inventory after successful archival or interrupted source removal.
    /// </param>
    private static void AssertInactiveSnapshotEvidenceArchive(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
    {
        var indexPair = Assert.Single(after, pair => pair.Key.Replace('\\', '/').StartsWith(
            "diagnostics/inactive-pending-turn-snapshots/", StringComparison.Ordinal) && Path.GetFileName(pair.Key) == "index.json");
        var root = Path.GetDirectoryName(indexPair.Key)!;
        Assert.Equal(Convert.ToHexString(SHA256.HashData(indexPair.Value)), Path.GetFileName(root));
        var index = Assert.IsType<JsonObject>(JsonNode.Parse(indexPair.Value));
        Assert.Equal("inactive-snapshot-diagnostic-only", index["purpose"]!.GetValue<string>());
        var entries = index["entries"]!.AsArray();
        Assert.Equal(before.Keys.Count(IsInactiveSnapshotEvidenceSource), entries.Count);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var source = entry!["sourcePath"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar);
            Assert.True(paths.Add(source));
            Assert.True(IsInactiveSnapshotEvidenceSource(source));
            var blob = entry["blob"]!.GetValue<string>();
            Assert.DoesNotContain(".rollback.", blob, StringComparison.OrdinalIgnoreCase);
            var bytes = after[Path.Combine(root, blob)];
            Assert.Equal(before[source], bytes);
            Assert.Equal(bytes.LongLength, entry["length"]!.GetValue<long>());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), entry["sha256"]!.GetValue<string>());
        }
        Assert.Equal(before.Keys.Where(IsInactiveSnapshotEvidenceSource).Order(StringComparer.Ordinal), paths.Order(StringComparer.Ordinal));
    }
}
