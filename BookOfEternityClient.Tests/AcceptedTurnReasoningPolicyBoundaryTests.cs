using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Ordinary Full prefix prerequisites; no HOT/source-history authority.</summary>
public sealed class AcceptedTurnReasoningPolicyBoundaryTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PublicFullOwner_ConsumesSharedDocumentAndScopePrefix()
    {
        using var fixture = new OrdinaryReasoningFixture(output);
        await fixture.SeedValidAsync();
        Assert.Empty(await fixture.Validator.ValidateAcceptedTurnReasoningAsync());
        fixture.AssertSeedBytesUnchanged();
        output.WriteLine("REAL_PUBLIC_FULL_PREFIX_PREREQUISITES_COMPLETE");

        var root = TestRepoPaths.RepoRoot;
        var helper = File.ReadAllText(Path.Combine(root,
            "BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnReasoningPrefix.cs"));
        var prefixAt = helper.IndexOf("EvaluateAcceptedReasoningDocumentPrefix(", StringComparison.Ordinal);
        var timestampAt = helper.IndexOf("ValidateRequiredIsoTimestampField(", StringComparison.Ordinal);
        var unknownAt = helper.IndexOf("AppendAcceptedReasoningUnknownFields(", StringComparison.Ordinal);
        var scopeAt = helper.IndexOf("EvaluateAcceptedReasoningScopePrefix(", StringComparison.Ordinal);
        Assert.True(prefixAt >= 0 && prefixAt < timestampAt && timestampAt < unknownAt && unknownAt < scopeAt);
        var source = File.ReadAllText(Path.Combine(root,
            "BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs"));
        var start = source.IndexOf("private async Task<List<ValidationIssue>> ValidateAcceptedTurnReasoningInternalAsync(", StringComparison.Ordinal);
        var end = source.IndexOf("\n    private ", start + 1, StringComparison.Ordinal);
        var body = source[start..end];
        var callAt = body.IndexOf("await EvaluateAcceptedReasoningPrefixAsync(null)", StringComparison.Ordinal);
        var terminalAt = body.IndexOf("if (prefix.Terminal) return issues;", StringComparison.Ordinal);
        var dependenciesAt = body.IndexOf("await ResolveGuardianPolicyContextAsync()", StringComparison.Ordinal);
        Assert.True(callAt >= 0 && callAt < terminalAt && terminalAt < dependenciesAt);
        Assert.DoesNotContain("if (prefix.Issues.Any", body);
    }

    [Fact]
    public void SharedReasoningExtraction_ComposesWithStrictHistoricalOrdinaryInverse()
    {
        var inventory = ReasoningPolicySourceInventory.Read();
        var source = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,
            inventory["sourcePath"]!.GetValue<string>()));
        source = DebugPrefixSourceInventory.ReverseIfPresent(source);
        source = NarrativePolicySourceInventory.ReverseIfPresent(source);
        // Require the new extraction, rather than accepting the unchanged baseline branch.
        ReasoningPolicySourceInventory.AssertExtractionAndReverse(source);
    }
}

/// <summary>Exact frozen source composition, not a substitute for behavioral tests.</summary>
internal static class ReasoningPolicySourceInventory
{
    internal static JsonObject Read() => JsonNode.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,
        "BookOfEternityClient.Tests/TestData/t179pd3e-ordinary-source-segments.json")))!.AsObject();

    internal static string ReverseIfPresent(string source)
    {
        var inventory = Read();
        // The tests-only checkpoint has no production helper yet. This branch permits
        // exactly the accepted 3d A bytes; every changed A must satisfy the full inverse.
        if (Sha(source) == inventory["sourceSha256"]!.GetValue<string>())
            return source;
        return AssertExtractionAndReverse(source);
    }

    internal static string AssertExtractionAndReverse(string source)
    {
        var inventory = Read();
        var segments = inventory["segments"]!.AsObject();
        var adapters = inventory["adapters"]!.AsObject();
        string Segment(string key) => segments[key]!.GetValue<string>();
        var path = Path.Combine(TestRepoPaths.RepoRoot, inventory["newPartialPath"]!.GetValue<string>());
        Assert.True(File.Exists(path), "The ordinary reasoning prefix partial has not been implemented.");
        var document = "            gmThoughts = string.Empty;\n" + Segment("documentPrefix")
            .Replace("return issues;", "return false;", StringComparison.Ordinal);
        string DedentDocument(string body) => "        " + body.Replace("doc.RootElement", "root", StringComparison.Ordinal)
            .Replace("\n            ", "\n        ", StringComparison.Ordinal).TrimStart(' ');
        var scope = "        normalizedThoughts = string.Empty;\n        scope = default!;\n" +
            "        scopeMode = default;\n        actorType = string.Empty;\n" + Segment("scopePrefix")
                .Replace("return issues;", "return false;", StringComparison.Ordinal)
                .Replace("var normalizedThoughts =", "normalizedThoughts =", StringComparison.Ordinal)
                .Replace("out var scope", "out scope", StringComparison.Ordinal)
                .Replace("var scopeMode =", "scopeMode =", StringComparison.Ordinal)
                .Replace("var actorType =", "actorType =", StringComparison.Ordinal);
        var expected = "using System.Text.Json;\n\nnamespace BookOfEternityClient.Services;\n\npublic partial class ValidationService\n{\n" +
            "    private bool EvaluateAcceptedReasoningDocumentPrefix(JsonElement root, List<ValidationIssue> issues, out string gmThoughts)\n    {\n" +
            DedentDocument(document) + "        return true;\n    }\n\n" +
            "    private void AppendAcceptedReasoningUnknownFields(JsonElement root, List<ValidationIssue> issues)\n    {\n" +
            DedentDocument(Segment("unknownFields")) + "    }\n\n" +
            "    private bool EvaluateAcceptedReasoningScopePrefix(string gmThoughts, List<ValidationIssue> issues,\n" +
            "        out string normalizedThoughts, out ReasoningScopeManifest scope,\n" +
            "        out ReasoningScopeMode scopeMode, out string actorType)\n    {\n" +
            scope + "        return true;\n    }\n}\n";
        Assert.Equal(expected, File.ReadAllText(path));
        foreach (var (key, node) in adapters)
        {
            var adapter = node!.GetValue<string>();
            Assert.Equal(1, source.Split(adapter, StringSplitOptions.None).Length - 1);
            source = source.Replace(adapter, Segment(key), StringComparison.Ordinal);
        }
        Assert.Equal(inventory["sourceSha256"]!.GetValue<string>(), Sha(source));
        return source;
    }

    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

/// <summary>One owned ordinary root; optional original is issued only by the real preparer.</summary>
internal sealed class OrdinaryReasoningFixture : IDisposable
{
    internal const string ValidThoughts = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: none\n" +
        "- Why relevant: No non-player actor participates.\n- Actors outside scope: none\n- Why outside scope: No other actor is addressed.\n";
    internal const string DebugPath = "output/debug_logs.json";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-pd3e-reasoning-" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;
    private readonly Dictionary<string, byte[]> _seed = new();
    internal FileSystemManager Files { get; }
    internal ValidationService Validator { get; }

    internal OrdinaryReasoningFixture(ITestOutputHelper output)
    {
        _output = output;
        Files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        Files.EnsureDirectoryStructure();
        Validator = new ValidationService(Files, NullLogger<ValidationService>.Instance);
    }

    internal async Task SeedValidAsync()
    {
        await Files.WriteFileAtomicAsync(DebugPath, Debug(ValidThoughts));
        await Files.WriteFileAtomicAsync("game_state/meta/soul_state.json", """{"currentRealm":"Mortal World","currentIncarnation":1}""");
        await Files.WriteFileAtomicAsync("game_state/meta/guardians.json", """{"guardians":[]}""");
        // Present empty objects keep ordinary physical read occurrences inspectable.
        foreach (var path in new[] { "game_state/npcs/npc_core.json", "game_state/npcs/npc_journals.json",
            "game_state/meta/guardian_abode_residents.json", "game_state/meta/afterlife_entity_profiles.json",
            "game_state/meta/shining_abode_state.json", "game_state/control/next_life_scenario_core.json",
            "game_state/factions/faction_core.json", "game_state/quests/regular_quests.json",
            "game_state/inventory/items.json", "input/turn_request.json" })
            await Files.WriteFileAtomicAsync(path, "{}");
        CaptureSeed();
    }

    internal static string Debug(string thoughts) => JsonSerializer.Serialize(new
        { gm_thoughts_markdown = thoughts, timestamp = "2026-01-01T00:00:00Z" });
    internal string Physical(string path) => Files.ResolvePath(path);
    internal FileSystemManager CreateFiles(FileSystemManagerHooks hooks) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
    internal ValidationService CreateValidator(FileSystemManagerHooks hooks) =>
        new(CreateFiles(hooks), NullLogger<ValidationService>.Instance);
    internal void WriteDebugRaw(byte[] bytes) => File.WriteAllBytes(Physical(DebugPath), bytes);
    internal async Task<string> PrepareOriginalAsync()
    {
        var prepared = await new LiveTurnPreparationService(Files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "pd3e-owned-session",
            RequestId = "pd3e-" + Guid.NewGuid().ToString("N"),
            TurnNumber = 2,
            CurrentRealm = "Mortal World",
            PlayerAction = "Осмотреть пустую комнату.",
            PreGeneratedDices1d20 = new[] { 14, 8, 17 }
        });
        var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
            (await Files.ReadFileAsync(prepared.ManifestPath))!, LiveTurnPreparationService.ManifestJsonOptions);
        Assert.NotNull(manifest);
        Assert.Equal(prepared.SessionId, manifest!.SessionId);
        Assert.Equal(prepared.RequestId, manifest.RequestId);
        var snapshotPath = manifest.Files[DebugPath];
        Assert.Equal(File.ReadAllBytes(Physical(DebugPath)), File.ReadAllBytes(Physical(snapshotPath)));
        _output.WriteLine(JsonSerializer.Serialize(new
        {
            kind = "PD3E_REAL_PREPARER_ORIGINAL",
            prepared.SessionId, prepared.RequestId, prepared.TurnNumber,
            snapshotPath,
            manifestBase64 = Convert.ToBase64String(File.ReadAllBytes(Physical(prepared.ManifestPath))),
            authorityBase64 = Convert.ToBase64String(File.ReadAllBytes(Physical(prepared.AuthorityPath))),
            requestBase64 = Convert.ToBase64String(File.ReadAllBytes(Physical(prepared.TurnRequestPath))),
            debugOriginalBase64 = Convert.ToBase64String(File.ReadAllBytes(Physical(snapshotPath)))
        }));
        return snapshotPath;
    }
    internal void CaptureSeed()
    {
        _seed.Clear();
        foreach (var path in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            _seed[path] = File.ReadAllBytes(path);
    }
    internal void AssertSeedBytesUnchanged()
    {
        Assert.Equal(_seed.Keys.Order(), Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Order());
        foreach (var (path, bytes) in _seed) Assert.Equal(bytes, File.ReadAllBytes(path));
    }
    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        var removed = !Directory.Exists(_root);
        _output.WriteLine(JsonSerializer.Serialize(new { kind = "PD3E_REASONING_STRICT_CLEANUP", root = _root, removed }));
        Assert.True(removed);
    }
}
