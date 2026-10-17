using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Real ordinary-A regression controls, without HOT/source-history claims.</summary>
public sealed class AcceptedTurnNarrativeInterfaceOrdinaryTests(ITestOutputHelper output) : IDisposable
{
    private readonly OrdinaryOutputFixture _fixture = new(output);

    [Theory]
    [InlineData("Narrative", "utf8", false)]
    [InlineData("Narrative", "utf16", false)]
    [InlineData("Narrative", "utf8", true)]
    [InlineData("Narrative", "utf16", true)]
    [InlineData("Interface", "utf8", false)]
    [InlineData("Interface", "utf16", false)]
    [InlineData("Interface", "utf8", true)]
    [InlineData("Interface", "utf16", true)]
    public async Task NoWrite_PreservesExactEncodingBytesAndMtime(string owner, string encoding, bool malformed)
    {
        await _fixture.SeedValidAsync();
        var path = OrdinaryOutputFixture.PathFor(owner);
        var text = malformed ? "{broken" : owner == "Narrative"
            ? OrdinaryOutputFixture.ValidNarrative : OrdinaryOutputFixture.ValidInterface;
        var bytes = encoding == "utf8" ? new UTF8Encoding(false).GetBytes(text)
            : Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
        _fixture.WriteRaw(path, bytes);
        var stamp = _fixture.SetOldTime(path);
        var writes = new List<string>();
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = p => { writes.Add(p); return Task.CompletedTask; }
        });
        var issues = await ValidateAsync(validator, owner);
        if (malformed)
            Assert.Contains(issues, x => x.Code == (owner == "Narrative"
                ? "accepted_turn_invalid_narrative_json" : "accepted_turn_invalid_interface_updates_json"));
        else Assert.Empty(issues);
        Assert.Empty(writes);
        Assert.Equal(bytes, File.ReadAllBytes(_fixture.Physical(path)));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(_fixture.Physical(path)));
    }

    [Theory]
    [InlineData("Narrative")]
    [InlineData("Interface")]
    public async Task Changed_PublicWriterReturnsThenPreservesExistingEncodingAndSinglePass(string owner)
    {
        await _fixture.SeedValidAsync();
        var path = OrdinaryOutputFixture.PathFor(owner);
        var input = owner == "Narrative"
            ? """{"response":"Первая`nвторая","timestamp":"2026-01-01T00:00:00Z"}"""
            : """{"dialogueOptions":[{"text":"[INK_FEATHER_ACTION: FIRST] [INK_FEATHER_ACTION: SECOND] Выбор`nдва","inputValue":"existing action"}],"timestamp":"2026-01-01T00:00:00Z"}""";
        _fixture.WriteRaw(path, Encoding.UTF8.GetBytes(input));
        var writes = new List<string>();
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = p => { writes.Add(p); return Task.CompletedTask; }
        });
        var issues = await ValidateAsync(validator, owner); // actual public write and owning release must return
        Assert.Empty(issues);
        Assert.Equal(new[] { path }, writes);
        var projected = owner == "Narrative" ? AcceptedTurnOutputProjector.ProjectNarrative(input).Json
            : AcceptedTurnOutputProjector.ProjectInterface(input, null).Json;
        var expected = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(projected)).ToArray();
        Assert.Equal(expected, File.ReadAllBytes(_fixture.Physical(path)));
        if (owner == "Interface")
        {
            var option = JsonNode.Parse(projected)!["dialogueOptions"]![0]!;
            Assert.Equal("[INK_FEATHER_ACTION: SECOND] Выбор\nдва", option["text"]!.GetValue<string>());
            Assert.Equal("existing action", option["inputValue"]!.GetValue<string>());
        }
    }

    [Theory]
    [InlineData("Narrative")]
    [InlineData("Interface")]
    public async Task RealWriteFailure_PropagatesWithoutDiagnosticCoercionOrByteSuccess(string owner)
    {
        await _fixture.SeedValidAsync();
        var path = OrdinaryOutputFixture.PathFor(owner);
        var raw = Encoding.UTF8.GetBytes(owner == "Narrative"
            ? """{"response":"Один`nдва","timestamp":"2026-01-01T00:00:00Z"}"""
            : """{"dialogueOptions":["Один`nдва"],"timestamp":"2026-01-01T00:00:00Z"}""");
        _fixture.WriteRaw(path, raw);
        var failure = new IOException("owned ordinary projection write cut");
        var writes = 0;
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = p =>
            {
                Assert.Equal(path, p);
                writes++;
                return Task.FromException(failure);
            }
        });
        var error = await Record.ExceptionAsync(() => ValidateAsync(validator, owner));
        Assert.Same(failure, error);
        Assert.Equal(1, writes);
        Assert.Equal(raw, File.ReadAllBytes(_fixture.Physical(path)));
    }

    [Fact]
    public async Task OwningCloseFailure_RemainsFaultEvenWhenActualProjectionBytesMatch()
    {
        await _fixture.SeedValidAsync();
        const string path = "output/narrative_response.json";
        const string input = """{"response":"Один`nдва","timestamp":"2026-01-01T00:00:00Z"}""";
        _fixture.WriteRaw(path, Encoding.UTF8.GetBytes(input));
        var failure = new IOException("owned ordinary closing cut");
        var closes = 0;
        var hooked = _fixture.CreateFiles(new FileSystemManagerHooks
        {
            SessionOperationClosingAsync = () => { closes++; return Task.FromException(failure); }
        });
        var validator = new ValidationService(hooked, NullLogger<ValidationService>.Instance);
        var publicOwnerReturned = false;
        var overallReturned = false;
        var error = await Record.ExceptionAsync(async () =>
        {
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(hooked, async () =>
            {
                Assert.Empty(await validator.ValidateAcceptedTurnNarrativePayloadAsync());
                publicOwnerReturned = true;
                return 17;
            }, beforeCurrentGeneration: _ => Task.CompletedTask);
            overallReturned = true;
        });
        Assert.True(publicOwnerReturned);
        Assert.False(overallReturned);
        Assert.Same(failure, error);
        Assert.Equal(1, closes);
        Assert.Equal(AcceptedTurnOutputProjector.ProjectNarrative(input).Json,
            File.ReadAllText(_fixture.Physical(path)));
        // A matching afterimage is not a successful operation-return/history observation.
    }

    [Theory]
    [InlineData("42", "interface_updates_missing_timestamp", false)]
    [InlineData("{}", "interface_updates_missing_timestamp", false)]
    [InlineData("\"not-a-time\"", "interface_updates_invalid_timestamp", false)]
    [InlineData("null", "", true)]
    [InlineData("\" \"", "", true)]
    public async Task InterfaceTimestamp_FallbackOnlyForExistingUsefulBlankPolicy(string timestamp, string code, bool changed)
    {
        await _fixture.SeedValidAsync();
        const string path = "output/interface_updates.json";
        var input = "{\"dialogueOptions\":null,\"timestamp\":" + timestamp + "}";
        _fixture.WriteRaw(path, Encoding.UTF8.GetBytes(input));
        var writes = 0;
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = p => { Assert.Equal(path, p); writes++; return Task.CompletedTask; }
        });
        var before = DateTimeOffset.UtcNow;
        var issues = await validator.ValidateAcceptedTurnInterfacePayloadAsync();
        var after = DateTimeOffset.UtcNow;
        Assert.Equal(changed ? 1 : 0, writes);
        if (!changed)
        {
            Assert.Equal(code, Assert.Single(issues).Code);
            Assert.Equal(Encoding.UTF8.GetBytes(input), File.ReadAllBytes(_fixture.Physical(path)));
            return;
        }
        Assert.Empty(issues);
        var persisted = File.ReadAllText(_fixture.Physical(path));
        var actualSample = DateTimeOffset.Parse(JsonNode.Parse(persisted)!["timestamp"]!.GetValue<string>());
        Assert.InRange(actualSample, before, after);
        Assert.Equal(AcceptedTurnOutputProjector.ProjectInterface(input, actualSample).Json, persisted);
        var bytes = File.ReadAllBytes(_fixture.Physical(path));
        Assert.Empty(await validator.ValidateAcceptedTurnInterfacePayloadAsync());
        Assert.Equal(1, writes);
        Assert.Equal(bytes, File.ReadAllBytes(_fixture.Physical(path)));
    }

    [Theory]
    [InlineData("Narrative")]
    [InlineData("Interface")]
    public async Task OrderedDiagnostics_PreserveFullFieldsAndCaseSensitiveLookups(string owner)
    {
        await _fixture.SeedValidAsync();
        var input = owner == "Narrative"
            ? """{"Response":"ignored exact lookup","timestamp":42,"z":1,"_meta":true,"a":2}"""
            : """{"dialogueOptions":null,"image_prompt":null,"timestamp":42,"z":1,"_meta":true,"a":2}""";
        _fixture.WriteRaw(OrdinaryOutputFixture.PathFor(owner), Encoding.UTF8.GetBytes(input));
        var actual = await ValidateAsync(_fixture.Validator, owner);
        var path = OrdinaryOutputFixture.PathFor(owner);
        var section = owner == "Narrative" ? "Narrative" : "InterfaceUpdates";
        var expected = new List<ValidationIssue>();
        if (owner == "Narrative") expected.Add(new ValidationIssue(path + ".response", IssueSeverity.Error,
            "Accepted GM turn должен содержать непустой narrative response", code: "accepted_turn_empty_narrative_response",
            section: section, expected: "non-empty response string", actual: "missing or empty",
            repairHint: "Поле response в output/narrative_response.json должно содержать свежий текст нарратива для текущего хода."));
        expected.Add(new ValidationIssue(path + ".timestamp", IssueSeverity.Error,
            "timestamp обязателен и должен быть ISO 8601 timestamp",
            code: owner == "Narrative" ? "narrative_response_missing_timestamp" : "interface_updates_missing_timestamp",
            section: section, expected: "ISO 8601 timestamp", actual: "Number",
            repairHint: owner == "Narrative"
                ? "Добавь в output/narrative_response.json поле timestamp в ISO 8601 формате вместе со свежим response для текущего accepted turn."
                : "Добавь в output/interface_updates.json поле timestamp в ISO 8601 формате вместе с dialogueOptions и/или image_prompt текущего accepted turn."));
        foreach (var name in new[] { "z", "a" }) expected.Add(new ValidationIssue(path + "." + name, IssueSeverity.Error,
            path + " содержит неподдерживаемое top-level поле",
            code: owner == "Narrative" ? "narrative_response_unknown_field" : "interface_updates_unknown_field",
            section: section, expected: owner == "Narrative" ? "response | timestamp" : "dialogueOptions | image_prompt | timestamp", actual: name,
            repairHint: owner == "Narrative"
                ? "Пиши в output/narrative_response.json только response и timestamp для текущего хода."
                : "Пиши в output/interface_updates.json только dialogueOptions, image_prompt и timestamp для текущего хода."));
        Assert.Equal(expected.Select(DescribeIssue), actual.Select(DescribeIssue));
    }

    [Theory]
    [InlineData("Narrative", "null", "accepted_turn_invalid_narrative_json_root")]
    [InlineData("Narrative", "[]", "accepted_turn_invalid_narrative_json_root")]
    [InlineData("Interface", "null", "accepted_turn_invalid_interface_updates_root")]
    [InlineData("Interface", "[]", "accepted_turn_invalid_interface_updates_root")]
    public async Task NonObjectRoot_ReturnsOnlyOriginalRootDiagnostic(string owner, string json, string code)
    {
        await _fixture.SeedValidAsync();
        _fixture.WriteRaw(OrdinaryOutputFixture.PathFor(owner), Encoding.UTF8.GetBytes(json));
        var issue = Assert.Single(await ValidateAsync(_fixture.Validator, owner));
        Assert.Equal(code, issue.Code);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(json == "null" ? "Null" : "Array", issue.Actual);
    }

    [Theory]
    [InlineData("\"dialogueOptions\":42", "output/interface_updates.json.dialogueOptions")]
    [InlineData("\"image_prompt\":42", "output/interface_updates.json.image_prompt")]
    [InlineData("\"dialogueOptions\":[7]", "output/interface_updates.json.dialogueOptions[0]")]
    [InlineData("\"DialogueOptions\":null", "output/interface_updates.json")]
    public async Task InterfacePayload_KeepsNullableWrongTypeAndExactPresencePolicies(string payload, string issuePath)
    {
        await _fixture.SeedValidAsync();
        var json = "{" + payload + ",\"timestamp\":\"2026-01-01T00:00:00Z\"}";
        _fixture.WriteRaw("output/interface_updates.json", Encoding.UTF8.GetBytes(json));
        var issue = Assert.Single(await _fixture.Validator.ValidateAcceptedTurnInterfacePayloadAsync());
        Assert.Equal(issuePath, issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(Encoding.UTF8.GetBytes(json), File.ReadAllBytes(_fixture.Physical("output/interface_updates.json")));
    }

    [Fact]
    public async Task NarrativeTail_LeavesAchievementDiagnosticsAfterTimestampUnknownAndLeak()
    {
        await _fixture.SeedValidAsync();
        await _fixture.Files.WriteFileAtomicAsync("game_state/meta/achievements.json",
            """{"achievementUnlocks":[{"name":"Первый след"},{"name":"Второй след"}]}""");
        _fixture.WriteRaw("output/narrative_response.json", Encoding.UTF8.GetBytes(
            """{"response":"JSON validation repair исправлен.","timestamp":42,"extra":true}"""));
        var issues = await _fixture.Validator.ValidateAcceptedTurnNarrativePayloadAsync();
        Assert.Equal(new[] { "narrative_response_missing_timestamp", "narrative_response_unknown_field",
            "narrative_response_technical_repair_leak", "achievement_unlock_marker_missing_in_narrative",
            "achievement_unlock_marker_missing_in_narrative" }, issues.Select(x => x.Code));
        Assert.Equal("[ACHIEVEMENT_UNLOCK: Первый след]", issues[3].Expected);
        Assert.Equal("[ACHIEVEMENT_UNLOCK: Второй след]", issues[4].Expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Narrative_ReadOrderAndJsonExceptionBoundaryRemainOriginal(bool failAchievements)
    {
        await _fixture.SeedValidAsync();
        var selected = new[] { "output/narrative_response.json", "game_state/meta/achievements.json",
            "output/interface_updates.json", "ready/turn_complete.json", "output/debug_logs.json",
            "game_state/player/skills_active.json", "game_state/player/skills_passive.json", "game_state/quests/regular_quests.json" };
        var reads = new List<string>();
        var failure = new IOException("current achievements read is outside its best-effort catch");
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = p =>
            {
                if (selected.Contains(p)) reads.Add(p);
                return failAchievements && p == selected[1] ? Task.FromException(failure) : Task.CompletedTask;
            }
        });
        if (failAchievements)
        {
            Assert.Same(failure, await Record.ExceptionAsync(() => validator.ValidateAcceptedTurnNarrativePayloadAsync()));
            Assert.Equal(selected[..2], reads);
        }
        else
        {
            Assert.Empty(await validator.ValidateAcceptedTurnNarrativePayloadAsync());
            Assert.Equal(selected, reads);
        }
    }

    [Fact]
    public async Task DownstreamJsonException_IsStillCaughtByNarrativeOuterBoundary()
    {
        await _fixture.SeedValidAsync();
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = p => p == "game_state/meta/achievements.json"
                ? Task.FromException(new JsonException("owned downstream parser cut")) : Task.CompletedTask
        });
        Assert.Equal("accepted_turn_invalid_narrative_json",
            Assert.Single(await validator.ValidateAcceptedTurnNarrativePayloadAsync()).Code);
    }

    [Fact]
    public async Task ReadOnlyNarrative_ReportsRequiredProjectionWithoutWriting()
    {
        await _fixture.SeedValidAsync();
        const string path = "output/narrative_response.json";
        var raw = Encoding.UTF8.GetBytes("""{"response":"Один`nдва","timestamp":"2026-01-01T00:00:00Z"}""");
        _fixture.WriteRaw(path, raw);
        var issues = await _fixture.Validator.ValidateMortalJointAcceptedNarrativeReadOnlyAsync();
        Assert.Contains(issues, x => x.Code == "mortal_joint_sealed_narrative_requires_normalization");
        Assert.Equal(raw, File.ReadAllBytes(_fixture.Physical(path)));
    }

    private static Task<List<ValidationIssue>> ValidateAsync(ValidationService validator, string owner) => owner == "Narrative"
        ? validator.ValidateAcceptedTurnNarrativePayloadAsync() : validator.ValidateAcceptedTurnInterfacePayloadAsync();

    private static string DescribeIssue(ValidationIssue issue) => JsonSerializer.Serialize(new
    {
        issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code, issue.Actor,
        issue.Section, issue.Expected, issue.Actual, issue.RepairHint, issue.RepairTargetFiles
    });

    public void Dispose() => _fixture.Dispose();
}

/// <summary>One owned root per test instance; ordinary files are never HOT/signed-history fixtures.</summary>
internal sealed class OrdinaryOutputFixture : IDisposable
{
    internal const string ValidNarrative = """{"response":"Тихая сцена у реки.","timestamp":"2026-01-01T00:00:00Z"}""";
    internal const string ValidInterface = """{"dialogueOptions":null,"timestamp":"2026-01-01T00:00:00Z"}""";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-pd3d-ordinary-" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;
    private readonly Dictionary<string, byte[]> _seed = new();
    internal FileSystemManager Files { get; }
    internal ValidationService Validator { get; }

    internal OrdinaryOutputFixture(ITestOutputHelper output)
    {
        _output = output;
        Files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        Files.EnsureDirectoryStructure();
        Validator = new ValidationService(Files, NullLogger<ValidationService>.Instance);
    }

    internal async Task SeedValidAsync()
    {
        await Files.WriteFileAtomicAsync("output/narrative_response.json", ValidNarrative);
        await Files.WriteFileAtomicAsync("output/interface_updates.json", ValidInterface);
        // Present empty objects make the observed semantic reads single physical opens;
        // absent ordinary reads intentionally have an additional quiescent follow-up.
        foreach (var path in new[] { "game_state/meta/achievements.json", "ready/turn_complete.json",
            "output/debug_logs.json", "game_state/player/skills_active.json", "game_state/player/skills_passive.json",
            "game_state/quests/regular_quests.json" })
            await Files.WriteFileAtomicAsync(path, "{}");
        foreach (var owner in new[] { "Narrative", "Interface" })
            _seed[PathFor(owner)] = File.ReadAllBytes(Physical(PathFor(owner)));
    }

    internal FileSystemManager CreateFiles(FileSystemManagerHooks hooks) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
    internal ValidationService CreateValidator(FileSystemManagerHooks hooks) =>
        new(CreateFiles(hooks), NullLogger<ValidationService>.Instance);
    internal static string PathFor(string owner) => owner == "Narrative"
        ? "output/narrative_response.json" : "output/interface_updates.json";
    internal string Physical(string path) => Files.ResolvePath(path);
    internal void WriteRaw(string path, byte[] bytes) => File.WriteAllBytes(Physical(path), bytes);
    internal DateTime SetOldTime(string path)
    {
        File.SetLastWriteTimeUtc(Physical(path), new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        return File.GetLastWriteTimeUtc(Physical(path));
    }
    internal void AssertSeedBytesUnchanged()
    {
        foreach (var (path, bytes) in _seed) Assert.Equal(bytes, File.ReadAllBytes(Physical(path)));
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        var removed = !Directory.Exists(_root);
        _output.WriteLine(JsonSerializer.Serialize(new { kind = "PD3D_ORDINARY_STRICT_CLEANUP", root = _root, removed }));
        Assert.True(removed);
    }
}
