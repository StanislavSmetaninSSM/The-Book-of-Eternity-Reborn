using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GuardianSystemRegressionTests
{
    private const string MusingsRootPath = "game_state/meta/guardians.json";
    private const string OldMusings = """[{"turn":11,"topic":"soul_assessment","mood":"intrigued","thought":"Я сохраню прежнее наблюдение без изменений."}]""";

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GuardianMusingsPublication_OriginalDeltaSurvivesPhysicalCloseAndLateValidationOnce(int count)
    {
        await PrepareMusingsPublicationAsync(count);
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var refresh = await RefreshMusingsPublicationAsync(validator);
        Assert.NotNull(refresh.GuardianMusingsValidation);
        var published = await _fs.ReadFileBytesAsync(MusingsRootPath);
        var root = JsonNode.Parse(published!)!;
        var musings = root["guardians"]![0]!["musings"]!.AsArray();
        Assert.Equal(count + 1, musings.Count);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(OldMusings)![0], musings[0]));
        Assert.True(JsonNode.DeepEquals(musings, root["activeGuardian"]!["musings"]));
        Assert.Null(root["UpdateGuardians"]);
        Assert.Contains(await ValidateMusingsStateAsync(validator), issue => issue.Code == "guardian_materialized_state_outside_authority");
        using (validator.UseCompletedGuardianMusingsValidationScope(refresh.GuardianMusingsValidation))
        {
            Assert.DoesNotContain(await ValidateMusingsStateAsync(validator), issue => issue.Severity == IssueSeverity.Error);
            using (validator.UseCompletedGuardianMusingsValidationScope(null))
                Assert.Contains(await ValidateMusingsStateAsync(validator), issue => issue.Code == "guardian_materialized_state_outside_authority");
            Assert.DoesNotContain(await ValidateMusingsStateAsync(validator), issue => issue.Severity == IssueSeverity.Error);
        }
        Assert.Equal(published, await _fs.ReadFileBytesAsync(MusingsRootPath));
        var repeated = await RefreshMusingsPublicationAsync(validator);
        Assert.Null(repeated.GuardianMusingsValidation);
        Assert.True(JsonNode.DeepEquals(musings, JsonNode.Parse((await _fs.ReadFileAsync(MusingsRootPath))!)!["guardians"]![0]!["musings"]));
        // A cold validator cannot recreate a consumed original delta from canonical musings.
        var cold = new ValidationService(new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance), NullLogger<ValidationService>.Instance);
        Assert.Contains(await ValidateMusingsStateAsync(cold), issue => issue.Code == "guardian_materialized_state_outside_authority");
    }

    [Theory]
    [InlineData("request")]
    [InlineData("generation")]
    [InlineData("snapshot")]
    [InlineData("authority")]
    [InlineData("old-prefix")]
    [InlineData("mirror")]
    [InlineData("raw-command")]
    public async Task GuardianMusingsPublication_DriftNeverBorrowsCompletedOriginalAuthority(string damage)
    {
        await PrepareMusingsPublicationAsync(1);
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var refresh = await RefreshMusingsPublicationAsync(validator);
        Assert.NotNull(refresh.GuardianMusingsValidation);
        if (damage == "generation") File.WriteAllText(_fs.SessionGenerationPath, System.Text.Json.JsonSerializer.Serialize(new { SchemaVersion = 1, GenerationId = Guid.NewGuid().ToString("N") }));
        else if (damage == "authority")
            await _fs.WriteFileAtomicAsync(PendingTurnSnapshotAuthority.AuthorityPath, "{\"substituted\":true}");
        else if (damage == "snapshot")
        {
            var manifest = JsonNode.Parse((await _fs.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!)!;
            await _fs.WriteFileAtomicAsync(manifest["files"]![MusingsRootPath]!.GetValue<string>(), "{\"changed\":true}");
        }
        else
        {
            var path = damage == "request" ? LiveTurnPreparationService.TurnRequestPath : MusingsRootPath;
            var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!;
            if (damage == "request") root["playerAction"] = "Подменённое исходное действие.";
            else if (damage == "old-prefix") root["guardians"]![0]!["musings"]![0]!["thought"] = "Переписанный старый журнал.";
            else if (damage == "mirror") root["activeGuardian"]!["musings"] = new JsonArray();
            else root["UpdateGuardians"] = MusingsCommands(1);
            await _fs.WriteFileAtomicAsync(path, root.ToJsonString());
        }
        var afterDamage = Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        using (validator.UseCompletedGuardianMusingsValidationScope(refresh.GuardianMusingsValidation))
            Assert.Contains(await ValidateMusingsStateAsync(validator), issue => issue.Severity == IssueSeverity.Error);
        foreach (var pair in afterDamage) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
    }

    [Theory]
    [InlineData("count")]
    [InlineData("turn")]
    [InlineData("topic")]
    [InlineData("mood")]
    [InlineData("text")]
    [InlineData("guardian")]
    public async Task GuardianMusingsPublication_InvalidOriginalCommandCannotIssueHandoff(string damage)
    {
        await PrepareMusingsPublicationAsync(1);
        var root = JsonNode.Parse((await _fs.ReadFileAsync(MusingsRootPath))!)!;
        var command = root["UpdateGuardians"]![0]!;
        var row = command["musings"]![0]!;
        if (damage == "count") command["musings"] = new JsonArray();
        else if (damage == "turn") row["turn"] = 12.5;
        else if (damage == "topic") row["topic"] = "unsupported";
        else if (damage == "mood") row["mood"] = "unsupported";
        else if (damage == "text") row.AsObject().Remove("text");
        else command["guardianId"] = "unknown_original_guardian";
        await _fs.WriteFileAtomicAsync(MusingsRootPath, root.ToJsonString());
        var original = await _fs.ReadFileBytesAsync(MusingsRootPath);
        var refusal = await Assert.ThrowsAsync<InvalidDataException>(() => RefreshMusingsPublicationAsync(new ValidationService(_fs, NullLogger<ValidationService>.Instance)));
        Assert.Contains("Original addMusings authorization failed", refusal.Message);
        Assert.Contains(damage switch
        {
            "count" => "guardian_add_musings_invalid_count",
            "turn" => "guardian_musing_missing_turn",
            "topic" => "guardian_musing_invalid_topic",
            "mood" => "guardian_musing_invalid_mood",
            "text" => "guardian_musing_missing_text",
            _ => "guardian_non_create_unknown_guardian"
        }, refusal.Message);
        Assert.Equal(original, await _fs.ReadFileBytesAsync(MusingsRootPath));
    }

    [Fact]
    public async Task GuardianMusingsPublication_RepairInvalidatesDeltaBeforeAnyMutation()
    {
        await PrepareMusingsPublicationAsync(1);
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var refresh = await RefreshMusingsPublicationAsync(validator);
        using var scope = validator.UseCompletedGuardianMusingsValidationScope(refresh.GuardianMusingsValidation);
        Assert.DoesNotContain(await ValidateMusingsStateAsync(validator), issue => issue.Severity == IssueSeverity.Error);
        scope.Invalidate();
        Assert.Contains(await ValidateMusingsStateAsync(validator), issue => issue.Code == "guardian_materialized_state_outside_authority");
        Assert.Null((await RefreshMusingsPublicationAsync(validator)).GuardianMusingsValidation);
    }

    private async Task PrepareMusingsPublicationAsync(int count)
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(OldMusings, OldMusings);
        await _fs.WriteFileAtomicAsync(MusingsRootPath, BuildCurrentMusingsPublicationBaseline());
        // This boundary requires the exact reader contract; the older actor-brain
        // fixture's legacy text-hash snapshot is not publication authority.
        await new LiveTurnPreparationService(_fs).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "test-session", RequestId = "test-request", TurnNumber = 12,
            CurrentRealm = "Chaos Sea", PlayerAction = "Спросить Элиару, как защитить память.",
            PreGeneratedDices1d20 = [3, 17]
        });
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            _fs.GetOrCreateSessionGeneration(lease);
            var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, [MusingsRootPath, "game_state/meta/soul_state.json"]);
            Assert.True(read.Success, string.Join("; ", read.Issues.Select(issue => issue.Code + ": " + issue.Actual)));
        }
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var baseline = await validator.DebugResolveGuardianPolicyContextAsync();
        Assert.Equal("Resolved", baseline.GenericSharedStrictPreTurnGuardianAuthorityStatus);
        Assert.DoesNotContain(await ValidateMusingsStateAsync(validator), issue => issue.Severity == IssueSeverity.Error);
        var root = JsonNode.Parse((await _fs.ReadFileAsync(MusingsRootPath))!)!;
        root["UpdateGuardians"] = MusingsCommands(count);
        await _fs.WriteFileAtomicAsync(MusingsRootPath, root.ToJsonString());
    }

    internal static string BuildCurrentMusingsPublicationBaseline()
    {
        var root = JsonNode.Parse(NormalizeGuardianStateJson(BuildActorBrainGuardianState(OldMusings)))!.AsObject();
        foreach (var guardian in root["guardians"]!.AsArray().OfType<JsonObject>().Append(root["activeGuardian"]!.AsObject()))
            guardian["gachaSystem"] = new JsonObject { ["currentReturnCycleId"] = "", ["gachaHistory"] = new JsonArray() };
        return root.ToJsonString();
    }

    private static JsonArray MusingsCommands(int count) => new(new JsonObject
    {
        ["command"] = "addMusings", ["guardianId"] = "guard_freeform_guardian_001",
        ["musings"] = new JsonArray(Enumerable.Range(0, count).Select(index => (JsonNode)new JsonObject
        {
            ["turn"] = 12, ["topic"] = "soul_assessment", ["mood"] = "intrigued", ["text"] = "Я запомню новое самостоятельное решение " + index + "."
        }).ToArray())
    });

    private async Task<AcceptedTurnCanonicalStateRefresh.Result> RefreshMusingsPublicationAsync(ValidationService validator)
    {
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(_fs,
            new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance), validator,
            new Dictionary<string, string>());
        Assert.True(!result.Issues.Any(issue => issue.Severity == IssueSeverity.Error),
            string.Join("; ", result.Issues.Select(issue => issue.Code + ": " + issue.Actual)));
        return result;
    }

    private static Task<List<ValidationIssue>> ValidateMusingsStateAsync(ValidationService validator) =>
        validator.ValidateGameStateAsync(new GameStateValidationSelection(GameStateValidationPhase.MetaMiscStateFiles, [MusingsRootPath]));
}
