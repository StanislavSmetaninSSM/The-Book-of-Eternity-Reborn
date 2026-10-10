using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardianMusingsPublication_TreatmentAcceptedHandoffReachesOriginalPostValidation(bool questSurface)
    {
        const string path = "game_state/meta/guardians.json";
        await using var context = await CreateHeldTreatmentPipelineContextAsync(null, composePublication: false,
            configureBeforePreparation: GuardianSystemRegressionTests.PrepareCurrentMusingsPublicationBaselineAsync);
        var root = JsonNode.Parse((await context.FileSystem.ReadFileAsync(context.Lease, path))!)!.AsObject();
        var prefix = root["guardians"]![0]!["musings"]!.DeepClone();
        root["UpdateGuardians"] = new JsonArray(new JsonObject
        {
            ["command"] = "addMusings", ["guardianId"] = "guard_freeform_guardian_001",
            ["musings"] = new JsonArray(new JsonObject
            {
                ["turn"] = HeldTreatmentPipelineContext.Turn, ["topic"] = "soul_assessment",
                ["mood"] = "intrigued", ["text"] = "Я сохраню решение о помощи раненому."
            })
        });
        if (questSurface) root[GuardianProjectState.QuestProgressUpdatesProperty] = new JsonArray();
        else root.Remove(GuardianProjectState.QuestProgressUpdatesProperty);
        Assert.Equal(questSurface, root.ContainsKey(GuardianProjectState.QuestProgressUpdatesProperty));
        await context.FileSystem.WriteFileAtomicAsync(context.Lease, path, root.ToJsonString());
        await RestoreHeldTreatmentAndComposeSameSemanticPlanAsync(context);
        await context.ReleaseLeaseAsync();
        var logger = new SpiritualLifecycleTestLogger();
        var (engine, snapshot) = await CreateHeldTreatmentValidationEngineAsync(context, logger);
        var spendsBefore = CountHeldTreatmentEnergySpends(await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync", "guardian original treatment handoff", snapshot, null,
            HeldTreatmentPipelineContext.Turn, null);
        Assert.True(disposition == AcceptedTurnValidationDisposition.Accepted,
            disposition + ": " + await context.FileSystem.ReadFileAsync("game_state/control/validation_repair_request.json") + "\n" + logger.Describe());
        Assert.Equal(spendsBefore + 1, CountHeldTreatmentEnergySpends(await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        var handoff = typeof(GameEngine).GetField("_acceptedTurnGuardianMusingsValidation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine);
        Assert.NotNull(handoff);
        Assert.True(await InvokePrivateAsync<bool>(engine, "ValidatePostAcceptedMaterializedStateWithRepairLoopAsync", (object?)null));
        var published = JsonNode.Parse((await context.FileSystem.ReadFileAsync(path))!)!;
        var musings = published["guardians"]![0]!["musings"]!.AsArray();
        Assert.Equal(prefix.AsArray().Count + 1, musings.Count);
        for (var i = 0; i < prefix.AsArray().Count; i++) Assert.True(JsonNode.DeepEquals(prefix[i], musings[i]));
        Assert.True(JsonNode.DeepEquals(musings, published["activeGuardian"]!["musings"]));
        Assert.Null(published["UpdateGuardians"]);
        Assert.Null(published[GuardianProjectState.QuestProgressUpdatesProperty]);
        Assert.Equal(spendsBefore + 1, CountHeldTreatmentEnergySpends(await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardianMusingsPublication_OriginalQuestCleanupAdvancesOnlyExactCompletedOutput(bool substituted)
    {
        using var fixture = new GuardianSystemRegressionTests();
        await fixture.PrepareMusingsPublicationAsync(1);
        var files = fixture.MusingsPublicationFiles;
        const string path = "game_state/meta/guardians.json";
        var root = JsonNode.Parse((await files.ReadFileAsync(path))!)!.AsObject();
        root[GuardianProjectState.QuestProgressUpdatesProperty] = new JsonArray();
        await files.WriteFileAtomicAsync(path, root.ToJsonString());
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        var refresh = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
            new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance), validator,
            await GuardianSystemRegressionTests.ReadOriginalMusingsPublicationBackupsAsync(files));
        Assert.True(!refresh.Issues.Any(issue => issue.Severity == IssueSeverity.Error), DescribeValidationIssues(refresh.Issues));
        Assert.NotNull(refresh.GuardianMusingsValidation);
        if (substituted)
        {
            root = JsonNode.Parse((await files.ReadFileAsync(path))!)!.AsObject();
            root["guardians"]![0]!["domain"] = "War";
            root["activeGuardian"]!["domain"] = "War";
            await files.WriteFileAtomicAsync(path, root.ToJsonString());
        }
        var before = await files.ReadFileBytesAsync(path);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: files);
        var method = typeof(GameEngine).GetMethod("CleanupAcceptedTurnCommandSurfacesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        // Invoke the actual old or new owner: missing handoff parameters are not the RED oracle.
        object?[] args = method.GetParameters().Length == 1 ? [null] : [null, refresh.GuardianMusingsValidation];
        if (substituted)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => InvokePrivateTaskNullableResultAsync(engine,
                "CleanupAcceptedTurnCommandSurfacesAsync", args));
            Assert.Equal(before, await files.ReadFileBytesAsync(path));
            return;
        }
        var advanced = await InvokePrivateTaskNullableResultAsync(engine, "CleanupAcceptedTurnCommandSurfacesAsync", args)
            as ValidationService.GuardianMusingsPublicationCapture.GuardianMusingsCompletedValidation ?? refresh.GuardianMusingsValidation;
        using (validator.UseCompletedGuardianMusingsValidationScope(advanced))
            Assert.DoesNotContain(await validator.ValidateGameStateAsync(new GameStateValidationSelection(
                GameStateValidationPhase.MetaMiscStateFiles, [path])), issue => issue.Severity == IssueSeverity.Error);
        Assert.Null(JsonNode.Parse((await files.ReadFileAsync(path))!)![GuardianProjectState.QuestProgressUpdatesProperty]);
    }
}
