using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class DarenRetirementStorageTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetiredCurrentBrowserEvidence_RefusesBeforeAnyRecovery(bool coexistingOrdinaryJournal)
    {
        var root = NewRoot();
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var packet = DarenRetirementEvidenceFixtures.Load("browser7");
        var files = await DarenRetirementEvidenceFixtures.InstantiateAsync(packet, root, output);
        var manifestPath = Path.Combine(root, packet.EvidencePath);
        var manifest = JsonNode.Parse(File.ReadAllBytes(manifestPath))!.AsObject();
        Assert.Equal(7, manifest["schemaVersion"]!.GetValue<int>());
        Assert.Equal("staged", manifest["status"]!.GetValue<string>());
        var generation = JsonNode.Parse(File.ReadAllBytes(files.SessionGenerationPath))!["generationId"]!.GetValue<string>();
        Assert.Equal(generation, manifest["generation"]!.GetValue<string>());
        Assert.Equal("daren_reward_profile", manifest["externalEntries"]![0]!["fileId"]!.GetValue<string>());
        Assert.Equal(DarenRetirementEvidenceFixtures.After, File.ReadAllBytes(Path.Combine(root, DarenRetirementEvidenceFixtures.Profile)));
        Assert.Equal(DarenRetirementEvidenceFixtures.After, File.ReadAllBytes(files.ResolvePath(DarenRetirementEvidenceFixtures.Member)));
        if (coexistingOrdinaryJournal)
            WriteSyntheticSharedJournal(files, generation, "game_state/meta/retirement-coexisting.bin", includeProfile: false);
        output.WriteLine("RetirementFixturePrerequisites=VALID_CAPTURE_DERIVED_CURRENT_BROWSER; coexisting=" + coexistingOrdinaryJournal);
        await InspectRefusalAsync(root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetiredStandaloneMember_RefusesBeforeWholeSetRecovery(bool ordinaryFirstMixedJournal)
    {
        var root = NewRoot();
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var packet = DarenRetirementEvidenceFixtures.Load("standalone1");
        var files = await DarenRetirementEvidenceFixtures.InstantiateAsync(packet, root, output);
        using (var actual = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, packet.EvidencePath))))
        {
            Assert.True(actual.RootElement.GetProperty("Committed").GetBoolean());
            Assert.Equal(Path.Combine(root, DarenRetirementEvidenceFixtures.Profile),
                actual.RootElement.GetProperty("Members")[0].GetProperty("Path").GetString());
        }
        var generation = JsonNode.Parse(File.ReadAllBytes(files.SessionGenerationPath))!["generationId"]!.GetValue<string>();
        if (ordinaryFirstMixedJournal)
            WriteSyntheticSharedJournal(files, generation, "game_state/meta/retirement-mixed.bin", includeProfile: true);
        output.WriteLine("RetirementFixturePrerequisites=VALID_CAPTURE_DERIVED_PROFILE; mixed=" + ordinaryFirstMixedJournal);
        await InspectRefusalAsync(root);
    }

    [Fact]
    public async Task RetiredOriginalSchemaShape_RefusesAtClassificationWithoutMutation()
    {
        var root = NewRoot();
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        var transaction = ExplorerLocalTurnRollbackArtifacts.Root + "/retired_original/638000000000000000_" + Guid.NewGuid().ToString("N");
        var generation = JsonNode.Parse(File.ReadAllBytes(files.SessionGenerationPath))!["generationId"]!.GetValue<string>();
        var manifest = new JsonObject
        {
            ["schemaVersion"] = 6, ["transactionKind"] = "browser_local_write", ["status"] = "staged",
            ["scope"] = "retired_original", ["createdAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["entries"] = new JsonArray(), ["cleanupDirectories"] = new JsonArray(),
            ["externalEntries"] = new JsonArray(new JsonObject
            {
                ["fileId"] = "daren_reward_profile", ["existed"] = false,
                ["backupPath"] = null, ["sha256"] = null
            })
        };
        DarenRetirementEvidenceFixtures.Put(root, "game_session/" + transaction + "/browser_write_manifest.json",
            JsonSerializer.SerializeToUtf8Bytes(manifest));
        DarenRetirementEvidenceFixtures.Put(root, DarenRetirementEvidenceFixtures.Profile, DarenRetirementEvidenceFixtures.After);
        WriteSyntheticSharedJournal(files, generation, "game_state/meta/retirement-before-original.bin", includeProfile: false);
        output.WriteLine("RetirementFixturePrerequisites=SYNTHETIC_ORIGINAL_SCHEMA6_SHAPE_NOT_WINDOWS_AUTHENTIC; exact retired identifier before original identity/recovery handler");
        await InspectRefusalAsync(root);
    }

    [Fact]
    public async Task OrdinaryBrowserJournal_StillRecoversItsExactBeforeImage()
    {
        var root = NewRoot();
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        await files.WriteFileAtomicBytesAsync(DarenRetirementEvidenceFixtures.Member, DarenRetirementEvidenceFixtures.Before);
        string manifest;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(files, lease,
                [DarenRetirementEvidenceFixtures.Member], "ordinary_retirement_neighbor");
            try
            {
                await files.WriteFileAtomicBytesAsync(lease, DarenRetirementEvidenceFixtures.Member, DarenRetirementEvidenceFixtures.After);
                manifest = files.ResolvePath(transaction.ManifestPath);
            }
            finally { transaction.LocalTransaction!.Access.Dispose(); }
        }
        Assert.True(File.Exists(manifest));
        Assert.Equal(DarenRetirementEvidenceFixtures.After, File.ReadAllBytes(files.ResolvePath(DarenRetirementEvidenceFixtures.Member)));
        await using (var recovered = await new FileSystemManager(root, NullLogger<FileSystemManager>.Instance).AcquireCanonicalWriteLeaseAsync()) { }
        Assert.Equal(DarenRetirementEvidenceFixtures.Before, File.ReadAllBytes(files.ResolvePath(DarenRetirementEvidenceFixtures.Member)));
        Assert.False(File.Exists(manifest));
        output.WriteLine("Ordinary genuine schema7 recovery preserved; no external participant.");
    }

    [Fact]
    public async Task OrdinarySharedJournal_StillRecoversItsExactBeforeImage()
    {
        var root = NewRoot();
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        var generation = JsonNode.Parse(File.ReadAllBytes(files.SessionGenerationPath))!["generationId"]!.GetValue<string>();
        WriteSyntheticSharedJournal(files, generation, DarenRetirementEvidenceFixtures.Member, includeProfile: false);
        await using (var recovered = await new FileSystemManager(root, NullLogger<FileSystemManager>.Instance).AcquireCanonicalWriteLeaseAsync()) { }
        Assert.Equal(DarenRetirementEvidenceFixtures.Before, File.ReadAllBytes(files.ResolvePath(DarenRetirementEvidenceFixtures.Member)));
        Assert.False(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        output.WriteLine("Source-derived ordinary Format1 recovery preserved; no retired member.");
    }

    private async Task InspectRefusalAsync(string root)
    {
        var before = DarenRetirementEvidenceFixtures.Snapshot(root);
        var callbacks = new List<string>();
        var fresh = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path => { callbacks.Add("mutation:" + path); return Task.CompletedTask; },
                LocalPublicationRecoveryObserver = (phase, index) => callbacks.Add("recovery:" + phase + ":" + index)
            });
        Exception? failure = null;
        try { await using var lease = await fresh.AcquireCanonicalWriteLeaseAsync(); }
        catch (Exception error) { failure = error; }
        var after = DarenRetirementEvidenceFixtures.Snapshot(root);
        var differences = before.Keys.Union(after.Keys).Where(path => !before.TryGetValue(path, out var b) ||
            !after.TryGetValue(path, out var a) || !b.AsSpan().SequenceEqual(a)).Order().ToArray();
        var issues = new List<string>();
        if (failure is not InvalidDataException || !failure.Message.Contains("retired Daren", StringComparison.OrdinalIgnoreCase))
            issues.Add("retired-member-refusal-missing-or-wrong: " + failure?.GetType().Name + ": " + failure?.Message);
        if (callbacks.Count != 0) issues.Add("recovery-or-mutation-callbacks-before-refusal");
        if (differences.Length != 0) issues.Add("retained-data-or-evidence-changed");
        output.WriteLine(JsonSerializer.Serialize(new { RetirementConsumer = root, Failure = failure?.ToString(),
            Before = before.ToDictionary(x => x.Key, x => DarenRetirementEvidenceFixtures.Hash(x.Value)),
            After = after.ToDictionary(x => x.Key, x => DarenRetirementEvidenceFixtures.Hash(x.Value)),
            Callbacks = callbacks, Differences = differences, Issues = issues }));
        // All actual owner/post-byte/callback diagnostics run before the causal oracle.
        Assert.Empty(issues);
    }

    private void WriteSyntheticSharedJournal(FileSystemManager files, string generation, string ordinary, bool includeProfile)
    {
        JsonObject Image(byte[] bytes) => new() { ["Exists"] = true, ["Bytes"] = Convert.ToBase64String(bytes), ["Sha256"] = DarenRetirementEvidenceFixtures.Hash(bytes) };
        JsonObject Member(string path) => new() { ["Path"] = path, ["Before"] = Image(DarenRetirementEvidenceFixtures.Before), ["After"] = Image(DarenRetirementEvidenceFixtures.After) };
        var members = new JsonArray(Member(files.ResolvePath(ordinary)));
        DarenRetirementEvidenceFixtures.Put(files.BasePath, "game_session/" + ordinary, DarenRetirementEvidenceFixtures.After);
        if (includeProfile)
            members.Add(Member(Path.Combine(files.BasePath, DarenRetirementEvidenceFixtures.Profile)));
        var journal = new JsonObject
        {
            ["Format"] = 1, ["TransactionId"] = Guid.NewGuid().ToString("N"), ["Committed"] = false,
            ["GenerationBefore"] = new JsonObject { ["Exists"] = true, ["Id"] = generation },
            ["GenerationAfter"] = new JsonObject { ["Exists"] = true, ["Id"] = generation }, ["Members"] = members
        };
        var relative = Path.GetRelativePath(files.BasePath, Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json"));
        DarenRetirementEvidenceFixtures.Put(files.BasePath, relative, JsonSerializer.SerializeToUtf8Bytes(journal));
        output.WriteLine(JsonSerializer.Serialize(new { Provenance = "SYNTHETIC_SHARED_PUBLISHER_FORMAT1_NOT_DAREN_PRODUCED",
            FormatSource = "TrustedLocalFilePublication.cs Journal/Member/Image; ordinary first, all physical members equal After", Ordinary = ordinary,
            RetiredMemberAdded = includeProfile, Generation = generation, Journal = DarenRetirementEvidenceFixtures.Hash(JsonSerializer.SerializeToUtf8Bytes(journal)) }));
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "boe-daren-retirement-storage-" + Guid.NewGuid().ToString("N"));
}
