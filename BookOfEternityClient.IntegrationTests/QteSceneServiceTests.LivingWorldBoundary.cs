using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class QteSceneServiceTests
{
    [Fact]
    public void LivingWorldQte_ResponseCommandDoesNotRequireCanonicalVehicleReceiptCollection()
    {
        using var document = JsonDocument.Parse("{\"response\":\"A local scene.\",\"experienceGained\":5}");
        var issues = new ValidationService(_fs, NullLogger<ValidationService>.Instance).ValidateResponse(document.RootElement);
        Assert.DoesNotContain(issues, issue => issue.Code == "resource_owner_vehicle_receipt_invalid");
    }

    [Fact]
    public async Task LivingWorldQte_CanonicalVehicleFileStillRequiresReceiptCollection()
    {
        await _fs.WriteFileAtomicAsync(StorageTransportMoveService.VehiclesPath, "{\"vehicles\":[]}");
        var issues = await new ValidationService(_fs, NullLogger<ValidationService>.Instance)
            .ValidateGameStateAsync(IntegrationValidationProfiles.Qte);
        Assert.Contains(issues, issue => issue.Code == "resource_owner_vehicle_receipt_invalid" &&
            issue.FilePath == StorageTransportMoveService.VehiclesPath + ".birthReceipts");
    }

    [Fact]
    public async Task LivingWorldQte_ActualOfferRefusesNewCycleBeforePlayerChoice()
    {
        await PrepareLivingWorldQteOfferAsync();
        var offer = BuildUnscoredBranchChoiceOffer();
        Assert.Single(offer.TerminalOutcomes).ResponseFragment!["timeChange"] = 240;
        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath,
            JsonSerializer.Serialize(offer, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var before = await CaptureLivingWorldQteBytesAsync();

        var issues = await new ValidationService(_fs, NullLogger<ValidationService>.Instance).ValidateAcceptedTurnQteOfferAsync();

        var issue = Assert.Single(issues.Where(issue => issue.Code == "mortal_living_world_qte_time_unsupported"));
        Assert.Contains("ordinary", issue.RepairHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        await AssertLivingWorldQteBytesAsync(before);
    }

    [Fact]
    public async Task LivingWorldQte_AcceptanceDefendsBeforeContinuationHandoff()
    {
        await PrepareLivingWorldQteOfferAsync();
        var offer = BuildUnscoredBranchChoiceOffer();
        Assert.Single(offer.TerminalOutcomes).ResponseFragment!["timeChange"] = 240;
        var before = await CaptureLivingWorldQteBytesAsync();

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => CreateRuntimeCapableService().BeginAcceptedSceneAsync(offer, 20));

        Assert.Contains("mortal_living_world_qte_time_unsupported", failure.Message);
        await AssertLivingWorldQteBytesAsync(before);
    }

    [Fact]
    public async Task LivingWorldQte_SelectedTerminalRefusesReportBeforeSealingArmedContinuation()
    {
        await PrepareLivingWorldQteOfferAsync();
        var offer = BuildUnscoredBranchChoiceOffer();
        Assert.Single(offer.TerminalOutcomes).ResponseFragment!["timeChange"] = 1;
        var service = CreateRuntimeCapableService();
        await service.BeginAcceptedSceneAsync(offer, 20);
        await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.ReportPath, "{ malformed late report");
        var before = await CaptureLivingWorldQteBytesAsync();
        var armed = JsonNode.Parse((await _fs.ReadFileAsync(QteSceneService.QteRuntimePath))!)!.AsObject();
        Assert.Equal("armed", armed["activeScene"]!["effectResolutionState"]!.GetValue<string>());

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => service.ResolveActiveActionAsync(
            "open_gate", null, 21, allowPreexistingStateIssues: true));

        Assert.Contains("mortal_living_world_qte_report_unsupported", failure.Message);
        await AssertLivingWorldQteBytesAsync(before);
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task LivingWorldQte_DirectTerminalRefusesBeforeStateDistribution()
    {
        await SeedMinimalValidatedMortalStateAsync();
        await SeedLivingWorldQteClockAsync();
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", "{\"totalExperience\":10}");
        var before = await CaptureLivingWorldQteBytesAsync();

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => CreateRuntimeCapableService()
            .ApplyTerminalOutcomeStateChangesAsync(LivingWorldQteOutcome(240)));

        Assert.Contains("mortal_living_world_qte_time_unsupported", failure.Message);
        await AssertLivingWorldQteBytesAsync(before);
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task LivingWorldQte_DirectTerminalCannotIgnoreMalformedSuppliedReport()
    {
        await SeedMinimalValidatedMortalStateAsync();
        await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.ReportPath, "{ malformed report");
        var before = await CaptureLivingWorldQteBytesAsync();

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => CreateRuntimeCapableService()
            .ApplyTerminalOutcomeStateChangesAsync(LivingWorldQteOutcome(null)));

        Assert.Contains("mortal_living_world_qte_report_unsupported", failure.Message);
        await AssertLivingWorldQteBytesAsync(before);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    public async Task LivingWorldQte_ShortTerminalDoesNotConsumeOldOriginalAssessmentOrAppendHistory(int? delta)
    {
        await SeedMinimalValidatedMortalStateAsync();
        if (delta.HasValue) await SeedLivingWorldQteClockAsync();
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", "{\"totalExperience\":10}");

        var response = await CreateRuntimeCapableService().ApplyTerminalOutcomeStateChangesAsync(LivingWorldQteOutcome(delta));

        Assert.Equal(delta, response.TimeChange);
        Assert.Null(await _fs.ReadFileBytesAsync(MortalLivingWorldSource.HistoryPath));
        Assert.Null(await _fs.ReadFileBytesAsync(MortalLivingWorldSource.ReportPath));
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task LivingWorldQte_AlreadyDueCyclesDoNotDemandAnotherOrdinaryReport()
    {
        await SeedMinimalValidatedMortalStateAsync();
        await SeedLivingWorldQteClockAsync();
        await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, "{\"currentTimeInMinutes\":1000}");
        var control = await new ProgressionScheduleService(_fs, NullLogger<ProgressionScheduleService>.Instance)
            .BuildControlForNextTurnAsync("Mortal World");
        Assert.Equal(4, control.WorldCyclesAlreadyPendingBeforeTurn);

        var response = await CreateRuntimeCapableService().ApplyTerminalOutcomeStateChangesAsync(LivingWorldQteOutcome(1));

        Assert.Equal(1, response.TimeChange);
        Assert.Null(await _fs.ReadFileBytesAsync(MortalLivingWorldSource.ReportPath));
        Assert.Null(await _fs.ReadFileBytesAsync(MortalLivingWorldSource.HistoryPath));
        AssertNoQteBackupArtifacts();
    }

    private async Task PrepareLivingWorldQteOfferAsync()
    {
        await SeedMinimalValidatedMortalStateAsync();
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", "{\"qteEventsEnabled\":true}");
        // Current supported empty vehicle state is part of the genuine offer baseline, not a fabricated birth receipt.
        await _fs.WriteFileAtomicAsync("game_state/misc/vehicles.json", MortalVehicleBirthReceiptState.CreateEmptyRoot().ToJsonString());
        var control = await SeedLivingWorldQteClockAsync();
        await new LiveTurnPreparationService(_fs).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "living-qte-session", RequestId = "living-qte-request", TurnNumber = 20,
            CurrentRealm = "Mortal World", PlayerAction = "Открыть ворота.", ProgressionControl = control,
            PreGeneratedDices1d20 = [8]
        });
        await BindOrdinaryQteSnapshotSourceAsync(_fs);
        var offer = BuildUnscoredBranchChoiceOffer();
        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath,
            JsonSerializer.Serialize(offer, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        Assert.Empty(await new ValidationService(_fs, NullLogger<ValidationService>.Instance).ValidateAcceptedTurnQteOfferAsync());
    }

    private async Task<BookOfEternityClient.Models.ProgressionControl> SeedLivingWorldQteClockAsync()
    {
        await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, "{\"currentTimeInMinutes\":0}");
        return await new ProgressionScheduleService(_fs, NullLogger<ProgressionScheduleService>.Instance)
            .BuildControlForNextTurnAsync("Mortal World");
    }

    private static QteSceneService.QteTerminalOutcome LivingWorldQteOutcome(int? delta)
    {
        var fragment = new JsonObject { ["response"] = "Сцена завершена.", ["experienceGained"] = 5 };
        if (delta.HasValue) fragment["timeChange"] = delta.Value;
        return new() { OutcomeId = "living-qte", Title = "Outcome", FinalNarrative = "Сцена завершена.",
            GmSummary = "Short local scene.", ResponseFragment = fragment };
    }

    private async Task<Dictionary<string, byte[]?>> CaptureLivingWorldQteBytesAsync()
    {
        var paths = new[] { MortalLivingWorldSource.WorldTimePath, ProgressionScheduleService.SchedulePath,
            MortalLivingWorldSource.ReportPath, MortalLivingWorldSource.HistoryPath,
            LiveTurnPreparationService.TurnRequestPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath, QteSceneService.QteOfferPath, QteSceneService.QteRuntimePath,
            QteDeferredEffectContinuation.StatePath, "game_state/player/experience.json" };
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths) result.Add(path, await _fs.ReadFileBytesAsync(path));
        return result;
    }

    private async Task AssertLivingWorldQteBytesAsync(Dictionary<string, byte[]?> expected)
    {
        foreach (var pair in expected) Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
    }
}
