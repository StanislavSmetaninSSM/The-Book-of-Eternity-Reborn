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
    public Task MortalTransportProseAudit_RequiresCanonicalEffectBeforeAcceptance(string kind) =>
        RunNpcProgressionAndTreatmentSkillSharedRootAsync(4, kind);

    private static string TransportProseAuditText(string kind)
    {
        var assertion = kind switch
        {
            "vehicle" => " После лечения вы получили в собственность новую повозку «Аудитный возок». Она стоит рядом с вами, исправна, доступна для поездок и перевозки ваших вещей.",
            "storage" => " После лечения вам предоставили новое личное хранилище «Аудитный склад» здесь, у лечебницы. Оно уже открыто для вас: можно складывать и забирать свои вещи.",
            _ => " После лечения вы спокойно осматриваетесь."
        };
        return HeldTreatmentPipelineContext.FinalSceneText + assertion;
    }

    private async Task AssertTransportProseAuditAsync(HeldTreatmentPipelineContext context, string kind,
        AcceptedTurnValidationDisposition disposition, SpiritualLifecycleTestLogger logger,
        NpcTreatmentRepairDiagnosticInput input)
    {
        var vehicle = await context.FileSystem.ReadFileAsync(StorageTransportMoveService.VehiclesPath) ?? "";
        var location = await context.FileSystem.ReadFileAsync(StorageTransportMoveService.CurrentLocationPath) ?? "";
        var map = await context.FileSystem.ReadFileAsync(MortalLocationMaterializationContract.WorldMapPath) ?? "";
        Assert.DoesNotContain("Аудитный возок", vehicle);
        Assert.DoesNotContain("Аудитный склад", location + map);
        var validator = new ValidationService(context.FileSystem,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidationService>.Instance);
        var rawLocation = await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();
        var rawItem = await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var rawResource = await validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        _directGachaOutput?.WriteLine("RAW LOCATION: " + DescribeValidationIssues(rawLocation));
        _directGachaOutput?.WriteLine("RAW ITEM: " + DescribeValidationIssues(rawItem));
        _directGachaOutput?.WriteLine("RAW RESOURCE: " + DescribeValidationIssues(rawResource));
        var diagnostic = $"kind={kind}; disposition={disposition}; canonical audit entity absent.\n{logger.Describe()}\nRepair:\n{input.RepairBeforeEscape ?? "<absent>"}";
        _directGachaOutput?.WriteLine(diagnostic);
        if (kind == "control")
            Assert.True(disposition == AcceptedTurnValidationDisposition.Accepted, diagnostic);
        else
            Assert.True(disposition != AcceptedTurnValidationDisposition.Accepted,
                "A transport/storage grant claimed only in prose was accepted without its canonical effect.\n" + diagnostic);
    }
}
