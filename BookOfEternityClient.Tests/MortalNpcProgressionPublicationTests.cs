using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionPublicationTests
{
    [Fact]
    public async Task Coordinator_NoPlan_AuthenticatedProgressionAppliesAndConsumesCommand()
    {
        using var fixture = await Fixture.Create();
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);
        Assert.Empty(result.Issues);
        Assert.Null(result.MechanicsPlan);
        var root = JsonNode.Parse((await fixture.Files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!;
        Assert.Null(root["NPCCoreChanges"]);
        Assert.Equal(2, root["NPCsInScene"]![0]!["level"]!.GetValue<int>());
        Assert.Equal(1, root["NPCsInScene"]![0]!["experience"]!.GetValue<int>());
        var accepted = root.ToJsonString();
        var repeat = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);
        Assert.Empty(repeat.Issues);
        Assert.Equal(accepted, JsonNode.Parse((await fixture.Files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.ToJsonString());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("stale")]
    [InlineData("realm")]
    public async Task Coordinator_SourceRefusalPrecedesEveryNormalizerWrite(string failure)
    {
        using var fixture = await Fixture.Create(failure == "realm" ? "Chaos Sea" : "Mortal World");
        if (failure == "missing") fixture.Files.DeleteFile(PendingTurnSnapshotAuthority.AuthorityPath);
        if (failure == "stale")
            await fixture.Files.WriteFileAtomicAsync("input/turn_request.json", """
                {"sessionId":"session_source","requestId":"new_request","turnNumber":43}
                """);
        await fixture.AddUnrelatedNormalizableCommand();
        var original = await fixture.CaptureCanonical();
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);
        Assert.Contains(result.Issues, issue => issue.Code is
            "npc_core_changes_progression_source_unavailable" or "npc_core_changes_progression_realm_invalid");
        await fixture.AssertCanonicalUnchanged(original);
    }

    [Fact]
    public async Task GenericNormalizer_WithoutPublicationContextRefusesBeforeWrites()
    {
        using var fixture = await Fixture.Create();
        await fixture.AddUnrelatedNormalizableCommand();
        var original = await fixture.CaptureCanonical();
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Normalizer.NormalizeAccumulatedStateAsync(fixture.Backups));
        await fixture.AssertCanonicalUnchanged(original);
    }

    [Fact]
    public async Task RawValidation_UsesAuthenticatedRealmAndRequestCorrelation()
    {
        using var fixture = await Fixture.Create();
        Assert.Empty(await fixture.Validator.ValidateNpcCoreChangesBeforeNormalizationAsync());
        var root = JsonNode.Parse((await fixture.Files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!;
        root["NPCCoreChanges"]![0]!["progression"]!["source"]!["requestId"] = "stale_request";
        await fixture.Files.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, root.ToJsonString());
        Assert.Contains(await fixture.Validator.ValidateNpcCoreChangesBeforeNormalizationAsync(),
            issue => issue.Code == "npc_core_changes_progression_source_invalid");
    }

    [Theory]
    [InlineData("request")]
    [InlineData("manifest")]
    public async Task PublicationSource_SameLeaseMutationCannotHideBehindPrevalidatedDto(string mutation)
    {
        using var fixture = await Fixture.Create();
        var manifest = JsonNode.Parse((await fixture.Files.ReadFileAsync("game_state/control/pending_turn_snapshot.json"))!)!;
        using var suppliedData = fixture.Validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var bytes = await fixture.Files.ReadFileBytesAsync(lease, NpcCoreChangesContract.NpcCorePath);
        var source = await fixture.Validator.PrepareNpcProgressionPublicationAsync(lease, bytes);
        Assert.Empty(source.Issues);
        Assert.NotNull(source.Context);
        await source.RequireCurrentAsync(fixture.Files, lease);
        if (mutation == "request")
            await fixture.Files.WriteFileAtomicAsync(lease, "input/turn_request.json", """
                {"sessionId":"session_source","requestId":"changed_request","turnNumber":43}
                """);
        else
        {
            manifest["turnNumber"] = 43;
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        }
        var bound = fixture.Normalizer.BindTo(lease).WithNpcProgressionSource(source);
        await Assert.ThrowsAsync<InvalidDataException>(() => bound.NormalizeAccumulatedStateWithPlanAsync(fixture.Backups));
        Assert.Equal(bytes, await fixture.Files.ReadFileBytesAsync(lease, NpcCoreChangesContract.NpcCorePath));
    }

    [Fact]
    public async Task PublicationSource_CannotMoveToAnotherLease()
    {
        using var fixture = await Fixture.Create();
        ValidationService.NpcProgressionPublicationSource source;
        await using (var first = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            source = await fixture.Validator.PrepareNpcProgressionPublicationAsync(first,
                await fixture.Files.ReadFileBytesAsync(first, NpcCoreChangesContract.NpcCorePath));
            Assert.Empty(source.Issues);
        }
        await using var replacement = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => source.RequireCurrentAsync(fixture.Files, replacement));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT") ?? Path.GetTempPath(),
            "npc-progression-source-" + Guid.NewGuid().ToString("N"));
        internal FileSystemManager Files { get; }
        internal ValidationService Validator { get; }
        internal CanonicalStateNormalizer Normalizer { get; }
        internal Dictionary<string, string> Backups { get; } = new(StringComparer.Ordinal);

        private Fixture()
        {
            Directory.CreateDirectory(_root);
            Files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
            Files.EnsureDirectoryStructure();
            Validator = new ValidationService(Files, NullLogger<ValidationService>.Instance);
            Normalizer = new CanonicalStateNormalizer(Files, NullLogger<CanonicalStateNormalizer>.Instance);
        }

        internal static async Task<Fixture> Create(string realm = "Mortal World")
        {
            var fixture = new Fixture();
            try
            {
                var before = JsonNode.Parse("""
                    {"NPCsInScene":[{"NPCId":"npc_source","level":1,"experience":99,"experienceForNextLevel":100,
                      "progressionProfile":{"schemaVersion":1,"mode":"standard"}}]}
                    """)!.AsObject();
                var current = before.DeepClone().AsObject();
                current["NPCCoreChanges"] = JsonNode.Parse("""
                    [{"NPCId":"npc_source","reason":"Earned two XP","progression":{
                      "source":{"sessionId":"session_source","requestId":"request_source","turnNumber":42},
                      "experienceDelta":2,"level":2,"experience":1,"experienceForNextLevel":565}}]
                    """);
                const string path = NpcCoreChangesContract.NpcCorePath;
                var snapshotPath = "game_state/control/pending_turn_snapshot/" + path;
                await fixture.Files.WriteFileAtomicAsync(path, current.ToJsonString());
                await fixture.Files.WriteFileAtomicAsync(snapshotPath, before.ToJsonString());
                fixture.Backups[path] = snapshotPath;
                await fixture.Files.WriteFileAtomicAsync("game_state/meta/soul_state.json",
                    new JsonObject { ["currentRealm"] = realm, ["currentIncarnation"] = 1 }.ToJsonString());
                await fixture.Files.WriteFileAtomicAsync("input/turn_request.json", """
                    {"sessionId":"session_source","requestId":"request_source","turnNumber":42,"playerAction":"Earn XP"}
                    """);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var manifest = new JsonObject
                {
                    ["sessionId"] = "session_source", ["requestId"] = "request_source", ["turnNumber"] = 42,
                    ["requestTimestamp"] = "2026-11-13T00:00:00Z", ["playerAction"] = "Earn XP",
                    ["progressionControl"] = JsonSerializer.SerializeToNode(new ProgressionControl { CurrentRealm = realm }, options),
                    ["files"] = new JsonObject { [path] = snapshotPath },
                    ["snapshotFileHashes"] = new JsonObject { [path] = PendingTurnSnapshotAuthority.ComputeSha256(before.ToJsonString()) },
                    ["clientOwnedValidationHashes"] = new JsonObject(), ["rollbackBackups"] = new JsonObject(),
                    ["rollbackBaselineFiles"] = new JsonArray(path), ["sourceLabel"] = "NPC source binding test",
                    ["manifestPayloadHash"] = ""
                };
                manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
                await fixture.Files.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
                await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fixture.Files);
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        internal async Task AddUnrelatedNormalizableCommand()
        {
            var command = JsonNode.Parse("""
                {"quests":[{"questId":"quest_source_guard","questName":"Preserve the unrelated history","status":"completed"}]}
                """)!;
            Assert.False(JsonNode.DeepEquals(command, MortalItemPublicationTailTransforms.QuestHistory(command, null)));
            await Files.WriteFileAtomicAsync("game_state/quests/quest_history.json", command.ToJsonString());
        }

        internal async Task<Dictionary<string, byte[]?>> CaptureCanonical()
        {
            var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            foreach (var path in CanonicalStateNormalizer.CanonicalAccumulatedFiles.Distinct(StringComparer.Ordinal))
                result.Add(path, await Files.ReadFileBytesAsync(path));
            return result;
        }

        internal async Task AssertCanonicalUnchanged(IReadOnlyDictionary<string, byte[]?> before)
        {
            foreach (var (path, bytes) in before)
                Assert.Equal(bytes, await Files.ReadFileBytesAsync(path));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
