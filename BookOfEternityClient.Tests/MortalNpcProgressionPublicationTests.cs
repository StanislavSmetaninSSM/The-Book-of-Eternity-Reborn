using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalNpcProgressionPublicationTests
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

    [Theory]
    [InlineData("utf8", false)]
    [InlineData("utf16le", false)]
    [InlineData("utf16be", false)]
    [InlineData("utf8", true)]
    [InlineData("utf16le", true)]
    [InlineData("utf16be", true)]
    public async Task Preparation_PreservesBomAwareTextContract(string encodingName, bool unrelated)
    {
        using var fixture = await Fixture.Create();
        var text = unrelated ? "{\"NPCsInScene\":[]}" :
            (await fixture.Files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!;
        var encoding = encodingName switch
        {
            "utf16le" => Encoding.Unicode, "utf16be" => Encoding.BigEndianUnicode, _ => new UTF8Encoding(true)
        };
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        await fixture.Files.WriteFileAtomicBytesAsync(NpcCoreChangesContract.NpcCorePath, bytes);
        Assert.Equal(text, await fixture.Files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath));
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var source = await fixture.Validator.PrepareNpcProgressionPublicationAsync(lease, bytes);
        Assert.Empty(source.Issues);
        Assert.Equal(!unrelated, source.Context is not null);
        await source.RequireCurrentAsync(fixture.Files, lease);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preparation_AuthenticatesOriginalNpcBytesNotOnlyManifest(bool delete)
    {
        using var fixture = await Fixture.Create();
        await fixture.AddUnrelatedNormalizableCommand();
        var before = await fixture.CaptureCanonical();
        var originalPath = fixture.Physical(fixture.Backups[NpcCoreChangesContract.NpcCorePath]);
        if (delete) File.Delete(originalPath);
        else File.AppendAllText(originalPath, " ", new UTF8Encoding(false));
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_core_changes_progression_source_unavailable");
        await fixture.AssertCanonicalUnchanged(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssuedSource_OriginalMutationOrDeletionIsRejectedBeforeWrites(bool delete)
    {
        using var fixture = await Fixture.Create();
        await fixture.AddUnrelatedNormalizableCommand();
        var before = await fixture.CaptureCanonical();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var source = await fixture.Validator.PrepareNpcProgressionPublicationAsync(lease,
            await fixture.Files.ReadFileBytesAsync(lease, NpcCoreChangesContract.NpcCorePath));
        Assert.Empty(source.Issues);
        var originalPath = fixture.Physical(fixture.Backups[NpcCoreChangesContract.NpcCorePath]);
        if (delete) File.Delete(originalPath);
        else File.AppendAllText(originalPath, " ", new UTF8Encoding(false));
        var bound = fixture.Normalizer.BindTo(lease).WithNpcProgressionSource(source);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => bound.NormalizeAccumulatedStateWithPlanAsync(fixture.Backups));
        Assert.Contains("original NPC core", error.Message, StringComparison.Ordinal);
        await fixture.AssertCanonicalUnchanged(before, lease);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Normalizer_RejectsMissingOrDifferentConsumedBackupBeforeWrites(bool missing)
    {
        using var fixture = await Fixture.Create();
        await fixture.AddUnrelatedNormalizableCommand();
        var before = await fixture.CaptureCanonical();
        var wrongBackups = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!missing)
        {
            const string wrong = "game_state/control/wrong_npc_core.json";
            await fixture.Files.WriteFileAtomicAsync(wrong, "{\"NPCsInScene\":[]}");
            wrongBackups[NpcCoreChangesContract.NpcCorePath] = wrong;
        }
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var source = await fixture.Validator.PrepareNpcProgressionPublicationAsync(lease,
            await fixture.Files.ReadFileBytesAsync(lease, NpcCoreChangesContract.NpcCorePath));
        Assert.Empty(source.Issues);
        var bound = fixture.Normalizer.BindTo(lease).WithNpcProgressionSource(source);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => bound.NormalizeAccumulatedStateWithPlanAsync(wrongBackups));
        Assert.Contains("reduction baseline", error.Message, StringComparison.Ordinal);
        await fixture.AssertCanonicalUnchanged(before, lease);
    }

    [Fact]
    public async Task Coordinator_PostValidatorSourceMutationRollsBackEveryCanonicalFile()
    {
        using var fixture = await Fixture.Create();
        await fixture.AddUnrelatedNormalizableCommand();
        await fixture.Files.WriteFileAtomicAsync(ValidationService.CanonicalSkillMasteryPath, "{}");
        var before = await fixture.CaptureCanonical();
        var injected = false;
        var files = fixture.CreateFiles(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (injected || path != ValidationService.CanonicalSkillMasteryPath) return Task.CompletedTask;
                var npc = JsonNode.Parse(File.ReadAllText(fixture.Physical(NpcCoreChangesContract.NpcCorePath)))!;
                if (npc["NPCCoreChanges"] is not null) return Task.CompletedTask;
                // This reader is the awaited canonical post-validator, after normalizer writes and its early fence.
                Assert.Equal(2, npc["NPCsInScene"]![0]!["level"]!.GetValue<int>());
                var quests = JsonNode.Parse(File.ReadAllText(fixture.Physical("game_state/quests/quest_history.json")))!;
                Assert.Null(quests["quests"]);
                injected = true;
                File.WriteAllText(fixture.Physical("input/turn_request.json"),
                    "{\"sessionId\":\"session_source\",\"requestId\":\"postvalidator_changed\",\"turnNumber\":43}");
                return Task.CompletedTask;
            }
        });
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
                new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance),
                new ValidationService(files, NullLogger<ValidationService>.Instance), fixture.Backups));
        Assert.True(injected);
        Assert.Contains("authenticated source changed", error.Message, StringComparison.Ordinal);
        await fixture.AssertCanonicalUnchanged(before);
    }

    internal sealed class Fixture : IDisposable
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

        internal static async Task<Fixture> Create(string realm = "Mortal World", bool includeSkillSidecar = false, bool progression = true, bool? originalSkillSidecarExists = true, int? initialStrength = null)
        {
            var fixture = new Fixture();
            try
            {
                var before = JsonNode.Parse("""
                    {"NPCsInScene":[{"NPCId":"npc_source","activeSkills":[],"passiveSkills":[],"characteristics":{},"level":1,"experience":99,"experienceForNextLevel":100,
                      "progressionProfile":{"schemaVersion":1,"mode":"standard","consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},"characteristics":{"mode":"currentWorld"}}}}]}
                    """)!.AsObject();
                if (initialStrength.HasValue)
                    before["NPCsInScene"]![0]!["characteristics"]!["strength"] = initialStrength.Value;
                var current = before.DeepClone().AsObject();
                current["NPCCoreChanges"] = JsonNode.Parse("""
                    [{"NPCId":"npc_source","reason":"Earned two XP","progression":{
                      "source":{"sessionId":"session_source","requestId":"request_source","turnNumber":42},
                      "experienceDelta":2,"level":2,"experience":1,"experienceForNextLevel":565}}]
                    """);
                if (!progression) current.Remove("NPCCoreChanges");
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
                if (includeSkillSidecar)
                {
                    const string skills = "game_state/npcs/npc_skills.json";
                    const string originalSkills = "game_state/control/pending_turn_snapshot/" + skills;
                    const string empty = "{\"NPCActiveSkillChanges\":[],\"NPCPassiveSkillChanges\":[],\"NPCSkillMasteryChanges\":[],\"NPCPassiveSkillMasteryChanges\":[]}";
                    await fixture.Files.WriteFileAtomicAsync(skills, empty);
                    if (originalSkillSidecarExists == true)
                    {
                        await fixture.Files.WriteFileAtomicAsync(originalSkills, empty);
                        fixture.Backups[skills] = originalSkills;
                        manifest["files"]![skills] = originalSkills;
                        manifest["snapshotFileHashes"]![skills] = PendingTurnSnapshotAuthority.ComputeSha256(empty);
                        manifest["rollbackBaselineFiles"]!.AsArray().Add(skills);
                    }
                    else if (originalSkillSidecarExists == false)
                    {
                        var presence = new JsonObject();
                        var signedFiles = manifest["files"]!.AsObject();
                        foreach (var logicalPath in PendingTurnSnapshotPathPresenceV1.MortalNpcSkillLogicalPaths)
                            presence[logicalPath] = signedFiles.ContainsKey(logicalPath);
                        // Match the validation manifest DTO's serialization order:
                        // snapshotFileHashes, originalPathPresenceV1, clientOwnedValidationHashes.
                        // The signing fixture hashes the authored JsonObject order.
                        var orderedManifest = new JsonObject();
                        foreach (var property in manifest)
                        {
                            orderedManifest[property.Key] = property.Value?.DeepClone();
                            if (property.Key == "snapshotFileHashes")
                                orderedManifest["originalPathPresenceV1"] = presence;
                        }
                        manifest = orderedManifest;
                    }
                }
                manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
                await fixture.Files.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
                await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fixture.Files);
                await fixture.SeedCanonicalPostValidationRoots();
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        // Keep the real postvalidators enabled, with their minimal canonical prerequisites.
        private async Task SeedCanonicalPostValidationRoots()
        {
            await Files.WriteFileAtomicAsync(MortalItemIdentityState.StatePath,
                MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
            var resources = ResourceBootstrapStateBuilder.BuildPristine();
            Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
            await Files.WriteFileAtomicAsync(ResourceMaterializationContract.DefinitionsPath,
                resources.Definitions!.ToCanonicalJson());
            await Files.WriteFileAtomicAsync(ResourceMaterializationContract.StatePath,
                resources.State!.ToCanonicalJson());
            await Files.WriteFileAtomicAsync(ResourceMaterializationContract.HistoryPath,
                resources.History!.ToCanonicalJson());
            var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                resources.Definitions, Files.ReadFileAsync, resources.State, resources.History,
                CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
            Assert.True(authority.IsValid, string.Join(Environment.NewLine, authority.Issues));
            await Files.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                authority.CanonicalAuthorityJson!);
        }

        internal string Physical(string path) => Files.ResolvePath(path);
        internal FileSystemManager CreateFiles(FileSystemManagerHooks hooks) =>
            new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);

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

        internal async Task AssertCanonicalUnchanged(IReadOnlyDictionary<string, byte[]?> before,
            FileSystemManager.CanonicalWriteLease? lease = null)
        {
            foreach (var (path, bytes) in before)
                Assert.Equal(bytes, lease is null ? await Files.ReadFileBytesAsync(path) : await Files.ReadFileBytesAsync(lease, path));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
