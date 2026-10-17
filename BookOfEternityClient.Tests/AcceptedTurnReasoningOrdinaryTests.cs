using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Actual ordinary Full prefix controls, without HOT/source-evidence claims.</summary>
public sealed class AcceptedTurnReasoningOrdinaryTests(ITestOutputHelper output) : IDisposable
{
    private readonly OrdinaryReasoningFixture _fixture = new(output);

    [Theory]
    [InlineData("absent", "missing_gm_thoughts")]
    [InlineData("blank", "missing_gm_thoughts")]
    [InlineData("malformed", "invalid_debug_logs_json")]
    [InlineData("array", "invalid_debug_logs_json_root")]
    [InlineData("null-root", "invalid_debug_logs_json_root")]
    [InlineData("empty-object", "debug_logs_missing_timestamp,missing_gm_thoughts")]
    [InlineData("null-thoughts", "missing_gm_thoughts")]
    [InlineData("numeric-thoughts", "missing_gm_thoughts")]
    [InlineData("wrong-case-thoughts", "missing_gm_thoughts")]
    [InlineData("duplicate-last-empty", "missing_gm_thoughts")]
    [InlineData("duplicate-last-valid", "")]
    [InlineData("underscore", "")]
    public async Task PublicFullDocument_PreservesExistingRootLookupAndEarlyReturns(string scenario, string codes)
    {
        await _fixture.SeedValidAsync();
        var thoughts = JsonSerializer.Serialize(OrdinaryReasoningFixture.ValidThoughts);
        var raw = scenario switch
        {
            "absent" => null,
            "blank" => " \r\n ",
            "malformed" => "{",
            "array" => "[]",
            "null-root" => "null",
            "empty-object" => "{}",
            "null-thoughts" => """{"gm_thoughts_markdown":null,"timestamp":"2026-01-01T00:00:00Z"}""",
            "numeric-thoughts" => """{"gm_thoughts_markdown":42,"timestamp":"2026-01-01T00:00:00Z"}""",
            "wrong-case-thoughts" => "{\"GM_THOUGHTS_MARKDOWN\":" + thoughts + ",\"timestamp\":\"2026-01-01T00:00:00Z\"}",
            "duplicate-last-empty" => "{\"gm_thoughts_markdown\":" + thoughts + ",\"gm_thoughts_markdown\":\"\",\"timestamp\":\"2026-01-01T00:00:00Z\"}",
            "duplicate-last-valid" => "{\"gm_thoughts_markdown\":\"\",\"gm_thoughts_markdown\":" + thoughts + ",\"timestamp\":\"2026-01-01T00:00:00Z\"}",
            "underscore" => "{\"gm_thoughts_markdown\":" + thoughts + ",\"timestamp\":\"2026-01-01T00:00:00Z\",\"_private\":null}",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        if (raw is null) File.Delete(_fixture.Physical(OrdinaryReasoningFixture.DebugPath));
        else _fixture.WriteDebugRaw(Encoding.UTF8.GetBytes(raw));
        _fixture.CaptureSeed();
        var issues = await _fixture.Validator.ValidateAcceptedTurnReasoningAsync();
        Assert.Equal(SplitCodes(codes), issues.Select(x => x.Code));
        _fixture.AssertSeedBytesUnchanged();
    }

    [Fact]
    public async Task PublicFullDocument_KeepsTimestampUnknownAndMissingIssueOrder()
    {
        await _fixture.SeedValidAsync();
        _fixture.WriteDebugRaw(Encoding.UTF8.GetBytes("""{"gm_thoughts_markdown":null,"timestamp":"bad-time","z":1,"a":2}"""));
        var issues = await _fixture.Validator.ValidateAcceptedTurnReasoningAsync();
        Assert.Equal(new[] { "debug_logs_invalid_timestamp", "debug_logs_unknown_field", "debug_logs_unknown_field", "missing_gm_thoughts" }, issues.Select(x => x.Code));
        Assert.Equal(new[] { "output/debug_logs.json.timestamp", "output/debug_logs.json.z", "output/debug_logs.json.a", "output/debug_logs.json" }, issues.Select(x => x.FilePath));
        Assert.All(issues, x => Assert.Equal(IssueSeverity.Error, x.Severity));
        Assert.Equal(new[] { "gm_thoughts_markdown", "gm_thoughts_markdown", "gm_thoughts_markdown", "gm_thoughts_markdown" }, issues.Select(x => x.Section));
        Assert.Equal(new[] { "z", "a" }, issues.Where(x => x.Code == "debug_logs_unknown_field").Select(x => x.Actual));
        var expected = new[]
        {
            new ValidationIssue("output/debug_logs.json.timestamp", IssueSeverity.Error,
                "timestamp должен быть ISO 8601 timestamp", code: "debug_logs_invalid_timestamp",
                section: "gm_thoughts_markdown", expected: "ISO 8601 timestamp", actual: "bad-time",
                repairHint: "Добавь в output/debug_logs.json поле timestamp в ISO 8601 формате вместе с актуальным gm_thoughts_markdown."),
            Unknown("z"), Unknown("a"),
            new ValidationIssue("output/debug_logs.json", IssueSeverity.Error,
                "Отсутствует gm_thoughts_markdown при обязательной проверке actor reasoning scope",
                code: "missing_gm_thoughts", section: "gm_thoughts_markdown",
                expected: "gm_thoughts_markdown with NPC scope declaration", actual: "missing or empty",
                repairHint: "Добавь debug_logs.json.gm_thoughts_markdown с секцией 'Охват NPC-анализа' и reasoning blocks.")
        };
        Assert.Equal(expected.Select(DescribeIssue), issues.Select(DescribeIssue));

        static ValidationIssue Unknown(string name) => new($"output/debug_logs.json.{name}", IssueSeverity.Error,
            "output/debug_logs.json содержит неподдерживаемое top-level поле", code: "debug_logs_unknown_field",
            section: "gm_thoughts_markdown", expected: "gm_thoughts_markdown | timestamp", actual: name,
            repairHint: "Пиши в output/debug_logs.json только gm_thoughts_markdown и timestamp для текущего хода.");
    }

    [Fact]
    public async Task ScopeHeadingOnly_PreservesFiveMissingFieldsInOrder()
    {
        await _fixture.SeedValidAsync();
        await _fixture.Files.WriteFileAtomicAsync(OrdinaryReasoningFixture.DebugPath,
            OrdinaryReasoningFixture.Debug("## NPC Scope\n"));
        Assert.Equal(new[] { "missing_scope_mode", "missing_relevant_actors_field", "missing_scope_relevance_reason",
            "missing_out_of_scope_actors_field", "missing_scope_out_of_scope_reason" },
            (await _fixture.Validator.ValidateAcceptedTurnReasoningAsync()).Select(x => x.Code));
    }

    [Theory]
    [InlineData("World-progression")]
    [InlineData("Guardian-centric")]
    [InlineData("Mixed")]
    public async Task NonSceneModes_KeepEmptyRelevantScopeAndExistingTailDiagnostics(string mode)
    {
        await _fixture.SeedValidAsync();
        await _fixture.Files.WriteFileAtomicAsync(OrdinaryReasoningFixture.DebugPath,
            OrdinaryReasoningFixture.Debug(OrdinaryReasoningFixture.ValidThoughts.Replace("Scene-local", mode, StringComparison.Ordinal)));
        var issues = await _fixture.Validator.ValidateAcceptedTurnReasoningAsync();
        var prefix = issues[0];
        Assert.Equal("empty_relevant_actors_for_mode", prefix.Code);
        Assert.Equal($"Режим '{mode}' требует непустой список релевантных акторов", prefix.Message);
        Assert.Equal("At least one relevant actor", prefix.Expected);
        Assert.Equal("empty relevant actor list", prefix.Actual);
        // Guardian-centric additionally retains its actual downstream authority/identity
        // diagnostics; the extraction must neither suppress nor promote them.
        Assert.Equal(mode == "Guardian-centric"
                ? new[] { "empty_relevant_actors_for_mode", "guardian_scope_invalid_validated_snapshot_context", "guardian_scope_missing_active_guardian" }
                : new[] { "empty_relevant_actors_for_mode" },
            issues.Select(x => x.Code));
    }

    [Theory]
    [InlineData("no-heading", "missing_npc_scope")]
    [InlineData("missing-mode", "missing_scope_mode")]
    [InlineData("unknown-mode", "invalid_scope_mode")]
    [InlineData("no-relevant-field", "missing_relevant_actors_field")]
    [InlineData("no-relevance-reason", "missing_scope_relevance_reason")]
    [InlineData("no-outside-field", "missing_out_of_scope_actors_field")]
    [InlineData("no-outside-reason", "missing_scope_out_of_scope_reason")]
    public async Task PublicFullScope_KeepsDeclarationDiagnostics(string scenario, string code)
    {
        await _fixture.SeedValidAsync();
        var thoughts = OrdinaryReasoningFixture.ValidThoughts;
        thoughts = scenario switch
        {
            "no-heading" => "Ordinary scene reasoning without a scope heading.",
            "missing-mode" => thoughts.Replace("- Mode: Scene-local\n", "", StringComparison.Ordinal),
            "unknown-mode" => thoughts.Replace("Scene-local", "opaque", StringComparison.Ordinal),
            "no-relevant-field" => thoughts.Replace("- Relevant actors: none\n", "", StringComparison.Ordinal),
            "no-relevance-reason" => thoughts.Replace("- Why relevant: No non-player actor participates.\n", "", StringComparison.Ordinal),
            "no-outside-field" => thoughts.Replace("- Actors outside scope: none\n", "", StringComparison.Ordinal),
            "no-outside-reason" => thoughts.Replace("- Why outside scope: No other actor is addressed.\n", "", StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        await _fixture.Files.WriteFileAtomicAsync(OrdinaryReasoningFixture.DebugPath, OrdinaryReasoningFixture.Debug(thoughts));
        Assert.Equal(code, Assert.Single(await _fixture.Validator.ValidateAcceptedTurnReasoningAsync()).Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScopeParser_UsesFirstHeadingAndDeduplicatesActors(bool crlf)
    {
        await _fixture.SeedValidAsync();
        var thoughts = OrdinaryReasoningFixture.ValidThoughts.Replace("Relevant actors: none", "Relevant actors: Иветта, иветта; Иветта", StringComparison.Ordinal) +
            "\n## NPC Scope\n- Mode: Mixed\n";
        if (crlf) thoughts = thoughts.Replace("\n", "\r\n", StringComparison.Ordinal);
        await _fixture.Files.WriteFileAtomicAsync(OrdinaryReasoningFixture.DebugPath, OrdinaryReasoningFixture.Debug(thoughts));
        var issues = await _fixture.Validator.ValidateAcceptedTurnReasoningAsync();
        Assert.Equal("Иветта", Assert.Single(issues.Where(x => x.Code == "missing_actor_block")).Actor);
        Assert.Single(issues.Where(x => x.Code == "missing_actor_reasoning_section"));
        Assert.DoesNotContain(issues, x => x.Code is "missing_scope_mode" or "invalid_scope_mode" or "empty_relevant_actors_for_mode");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingScopeStopsButParsedScopeErrorContinuesToActualDependencies(bool parsedScope)
    {
        await _fixture.SeedValidAsync();
        var thoughts = parsedScope ? OrdinaryReasoningFixture.ValidThoughts.Replace("Scene-local", "opaque", StringComparison.Ordinal) : "No scope heading.";
        await _fixture.Files.WriteFileAtomicAsync(OrdinaryReasoningFixture.DebugPath, OrdinaryReasoningFixture.Debug(thoughts));
        var failure = new IOException("actual current NPC read before extraction catch");
        var reads = 0;
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (path != "game_state/npcs/npc_core.json") return Task.CompletedTask;
                reads++;
                return Task.FromException(failure);
            }
        });
        if (parsedScope) Assert.Same(failure, await Record.ExceptionAsync(() => validator.ValidateAcceptedTurnReasoningAsync()));
        else Assert.Equal("missing_npc_scope", Assert.Single(await validator.ValidateAcceptedTurnReasoningAsync()).Code);
        Assert.Equal(parsedScope ? 1 : 0, reads);
    }

    [Fact]
    public async Task EmptySceneLocalFull_StillReadsStructuredFamiliesInOriginalOrder()
    {
        await _fixture.SeedValidAsync();
        var reads = new List<string>();
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path => { reads.Add(path); return Task.CompletedTask; }
        });
        Assert.Empty(await validator.ValidateAcceptedTurnReasoningAsync());
        var paths = new[] { "game_state/meta/guardians.json", "game_state/npcs/npc_core.json",
            "game_state/meta/guardian_abode_residents.json", "game_state/meta/afterlife_entity_profiles.json", "game_state/meta/shining_abode_state.json" };
        var indices = paths.Select(path => reads.IndexOf(path)).ToArray();
        Assert.All(indices, x => Assert.True(x >= 0));
        Assert.Equal(indices.Order(), indices);
        Assert.True(reads.Count(x => x == "game_state/npcs/npc_core.json") >= 2, "Repeated ordinary reads must not collapse to one cached path value.");
    }

    [Fact]
    public async Task MalformedStructuredNpc_RemainsInsideExistingBestEffortDomainCatch()
    {
        await _fixture.SeedValidAsync();
        await _fixture.Files.WriteFileAtomicAsync("game_state/npcs/npc_core.json", "{");
        _fixture.CaptureSeed();
        Assert.Empty(await _fixture.Validator.ValidateAcceptedTurnReasoningAsync());
        _fixture.AssertSeedBytesUnchanged();
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16")]
    [InlineData("malformed-utf8")]
    [InlineData("malformed-utf16")]
    public async Task PublicFull_NoWritePreservesRawEncodingAndMtime(string encoding)
    {
        await _fixture.SeedValidAsync();
        var json = OrdinaryReasoningFixture.Debug(OrdinaryReasoningFixture.ValidThoughts);
        byte[] raw;
        if (encoding == "utf8") raw = Encoding.UTF8.GetBytes(json);
        else if (encoding == "utf16") raw = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(json)).ToArray();
        else
        {
            var prefix = json[..^1] + ",\"_opaque\":\"";
            raw = encoding == "malformed-utf8"
                ? Encoding.UTF8.GetBytes(prefix).Concat(new byte[] { 0xff }).Concat(Encoding.UTF8.GetBytes("\"}")).ToArray()
                : Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(prefix)).Concat(new byte[] { 0, 0xd8 }).Concat(Encoding.Unicode.GetBytes("\"}")).ToArray();
        }
        _fixture.WriteDebugRaw(raw);
        var path = _fixture.Physical(OrdinaryReasoningFixture.DebugPath);
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        var timestamp = File.GetLastWriteTimeUtc(path);
        var writes = new List<string>();
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = p => { writes.Add(p); return Task.CompletedTask; }
        });
        Assert.Empty(await validator.ValidateAcceptedTurnReasoningAsync());
        Assert.Empty(writes);
        Assert.Equal(raw, File.ReadAllBytes(path));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentDebugReadFault_RemainsOutsideJsonParseCatch(bool jsonFailure)
    {
        await _fixture.SeedValidAsync();
        Exception failure = jsonFailure ? new JsonException("actual outside-parse debug read") : new IOException("actual outside-parse debug read");
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = p => p == OrdinaryReasoningFixture.DebugPath ? Task.FromException(failure) : Task.CompletedTask
        });
        Assert.Same(failure, await Record.ExceptionAsync(() => validator.ValidateAcceptedTurnReasoningAsync()));
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("changed")]
    [InlineData("corrupt-original")]
    public async Task GenuinePreparedOriginal_PreservesFreshnessWarningAndPolicyNull(string scenario)
    {
        await _fixture.SeedValidAsync();
        var snapshotPath = await _fixture.PrepareOriginalAsync();
        var validator = _fixture.Validator;
        var original = await ReadOriginalDebugAsync(validator);
        Assert.Equal(await _fixture.Files.ReadFileAsync(OrdinaryReasoningFixture.DebugPath), original);
        if (scenario == "changed")
            _fixture.WriteDebugRaw(Encoding.UTF8.GetBytes(OrdinaryReasoningFixture.Debug(OrdinaryReasoningFixture.ValidThoughts)[..^1] + ",\"_new\":1}"));
        if (scenario == "corrupt-original")
        {
            File.AppendAllText(_fixture.Physical(snapshotPath), " ", new UTF8Encoding(false));
            Assert.Null(await ReadOriginalDebugAsync(validator));
        }
        _fixture.CaptureSeed();
        var issues = await validator.ValidateAcceptedTurnReasoningAsync();
        if (scenario == "unchanged")
        {
            var stale = Assert.Single(issues);
            Assert.Equal("accepted_turn_stale_debug_logs", stale.Code);
            Assert.Equal(IssueSeverity.Warning, stale.Severity);
        }
        else Assert.Empty(issues); // Current policy-null is not a new ordinary Error.
        _fixture.AssertSeedBytesUnchanged();
    }

    [Fact]
    public async Task GenuineOriginal_FreshnessAndParseKeepSeparateCurrentReads()
    {
        await _fixture.SeedValidAsync();
        var snapshotPath = await _fixture.PrepareOriginalAsync();
        var snapshot = File.ReadAllBytes(_fixture.Physical(snapshotPath));
        var reads = 0;
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = p =>
            {
                if (p == OrdinaryReasoningFixture.DebugPath && ++reads == 2)
                    File.WriteAllText(_fixture.Physical(p), "{", new UTF8Encoding(false));
                return Task.CompletedTask;
            }
        });
        var issues = await validator.ValidateAcceptedTurnReasoningAsync();
        Assert.Equal(2, reads);
        Assert.Equal(new[] { "accepted_turn_stale_debug_logs", "invalid_debug_logs_json" }, issues.Select(x => x.Code));
        Assert.Equal(IssueSeverity.Warning, issues[0].Severity);
        Assert.Equal(IssueSeverity.Error, issues[1].Severity);
        Assert.Equal(snapshot, File.ReadAllBytes(_fixture.Physical(snapshotPath)));
        Assert.Equal("{", File.ReadAllText(_fixture.Physical(OrdinaryReasoningFixture.DebugPath)));
    }

    private static async Task<string?> ReadOriginalDebugAsync(ValidationService validator)
    {
        var method = typeof(ValidationService).GetMethod("ReadValidatedCurrentPreTurnTrackedFileAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return await Assert.IsAssignableFrom<Task<string?>>(method!.Invoke(validator, new object[] { OrdinaryReasoningFixture.DebugPath }));
    }

    private static string[] SplitCodes(string value) => value.Length == 0 ? Array.Empty<string>() : value.Split(',');
    private static string DescribeIssue(ValidationIssue issue) => JsonSerializer.Serialize(new
    {
        issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code, issue.Actor,
        issue.Section, issue.Expected, issue.Actual, issue.RepairHint, issue.RepairTargetFiles
    });
    public void Dispose() => _fixture.Dispose();
}
