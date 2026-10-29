using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact]
    public async Task HealthRefusedVisitCannotPoisonTheNextFreeformOrdinaryTurn()
    {
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(3, 20,
            engine: true, remoteElyara: true);
        var files = fixture.Files; var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger();
        var input = new RepairPlayerInput(diagnostics); var console = new TestExplorerConsole();
        var responded = false;
        const string ordinary = "Спокойно подождать в текущей Обители.";
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, inputSource: input, logger: diagnostics,
            explorerConsole: console, finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted || responded) return;
                    await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                    var turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath))!,
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                    Assert.Equal(ordinary, turn.PlayerAction);
                    Assert.Equal("natural", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                    await WriteNativeCompletionAsync(files, lease, turn,
                        AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(wound, turn.SpiritualHealing["destinationGraphs"]!.AsArray()));
                    responded = true;
                }
            });
        try
        {
            runtime.GameLoop.SetSession("session_elyara_refused_visit", 42);
            await runtime.StateManager.RefreshGameStateAsync();
            console.QueueAnyConfirmResponse(true);
            var visit = await runtime.Explorer.TryProcessCommand("/элиара");
            Assert.True(ElyaraVisitService.HasMarker(visit!));
            var path = ElyaraVisitService.SourcePaths[2]; var valid = (await files.ReadFileBytesAsync(path))!;
            var invalid = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(valid))!;
            invalid["guardians"]!.AsArray().Add(invalid["guardians"]![0]!.DeepClone()); // Definite pre-stage canonical rejection.
            await files.WriteFileAtomicAsync(path, invalid.ToJsonString());
            input.RequestEscape(); // Ordinary error acknowledgement only; no repair callback or retry.
            await runtime.Engine.ProcessPlayerTurn(visit!);
            Assert.Equal(42, runtime.GameLoop.TurnNumber); Assert.False(responded);
            Assert.Contains("перед отправкой хода", diagnostics.Describe());
            Assert.Null(await files.ReadFileBytesAsync(LiveTurnPreparationService.TurnRequestPath));
            Assert.Null(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
            await files.WriteFileAtomicBytesAsync(path, valid); // Restore this test's deliberate invalid input, not an accepted game turn.
            Assert.Null(await runtime.Explorer.TryProcessCommand(ordinary)); // Freeform does not run recognized-command reset.
            await runtime.Engine.ProcessPlayerTurn(ordinary);
            Assert.True(responded, "Abandoned visit prevented the unrelated ordinary turn.\n" + diagnostics.Describe());
            Assert.Equal(43, runtime.GameLoop.TurnNumber);
            Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
            Assert.Equal(500, JsonNode.Parse((await files.ReadFileAsync(ElyaraVisitService.SourcePaths[0]))!)!["inkFeathers"]!["current"]!.GetValue<int>());
            foreach (var transient in new[] { PendingPlayerActionService.PendingPath, AcceptedMechanicsPlan.WoundCommandPath,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath, ProgressionScheduleService.ReportPath })
                Assert.Null(await files.ReadFileBytesAsync(transient));
            fixture.AssertResourcesUnchanged();
            output.WriteLine("Actual health refusal42/no request, next freeform ordinary cycle42→43, no visit/treatment/fee carried across refusal.");
        }
        finally { output.WriteLine(diagnostics.Describe()); await runtime.AudioService.DisposeAsync(); }
    }
}
