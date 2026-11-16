using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    // #1515 closure audit: real accepted-turn owner, not only narrative validation.
    // Three independent roots retain the existing genuine treatment baseline;
    // the only changed input is a specific transport/storage assertion in prose.
    [Theory]
    [InlineData("control")]
    [InlineData("vehicle")]
    [InlineData("storage")]
    public async Task MortalTransportProseAudit_RequiresCanonicalEffectBeforeAcceptance(string kind)
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(publishNpcSkillChange: true),
            composePublication: false);
        await context.ReleaseLeaseAsync();
        const string narrativePath = "output/narrative_response.json";
        var narrative = await ReadJsonObjectAsync(context.FileSystem, narrativePath);
        var assertion = kind switch
        {
            "vehicle" => " После лечения вы получили в собственность новую повозку «Аудитный возок». Она стоит рядом с вами, исправна, доступна для поездок и перевозки ваших вещей.",
            "storage" => " После лечения вам предоставили новое личное хранилище «Аудитный склад» здесь, у лечебницы. Оно уже открыто для вас: можно складывать и забирать свои вещи.",
            _ => " После лечения вы спокойно осматриваетесь."
        };
        narrative["response"] = narrative["response"]!.GetValue<string>() + assertion;
        await context.FileSystem.WriteFileAtomicAsync(narrativePath, narrative.ToJsonString());
        var logger = new SpiritualLifecycleTestLogger();
        var input = new NpcTreatmentRepairDiagnosticInput(context.FileSystem);
        var engine = CreateGameEngine(input, fileSystem: context.FileSystem, logger: logger);
        var disposition = await engine.ValidatePreparedNpcProgressionOutcomeAsync(
            "#1515 prose-only transport/storage negative accepted-turn audit", HeldTreatmentPipelineContext.Turn);
        var vehicle = await context.FileSystem.ReadFileAsync(StorageTransportMoveService.VehiclesPath) ?? "";
        var location = await context.FileSystem.ReadFileAsync(StorageTransportMoveService.CurrentLocationPath) ?? "";
        var map = await context.FileSystem.ReadFileAsync(MortalLocationMaterializationContract.WorldMapPath) ?? "";
        Assert.DoesNotContain("Аудитный возок", vehicle);
        Assert.DoesNotContain("Аудитный склад", location + map);
        var diagnostic = $"kind={kind}; disposition={disposition}; canonical audit entity absent.\n{logger.Describe()}\nRepair:\n{input.RepairBeforeEscape ?? "<absent>"}";
        _directGachaOutput?.WriteLine(diagnostic);
        if (kind == "control")
            Assert.True(disposition == AcceptedTurnValidationDisposition.Accepted, diagnostic);
        else
            Assert.True(disposition != AcceptedTurnValidationDisposition.Accepted,
                "A transport/storage grant claimed only in prose was accepted without its canonical effect.\n" + diagnostic);
    }
}
