using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class WoundDiagnosisReadInterfaceTests(ITestOutputHelper output)
{
    // The real handlers must retain diagnostic conditions without staging or repairing a read.
    [Theory]
    [InlineData("/раны")]
    [InlineData("/статус")]
    public async Task KnownDiagnosticConditionsAreReadableAndInertThroughBothHandlers(string command)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["treatment"]!["diagnosisPaths"] = new JsonArray(new JsonObject
        {
            ["diagnosisPathId"] = "diagnosis_interface_private", ["displayName"] = "Осторожный осмотр", ["visibility"] = "public",
            ["requiresKnownFacts"] = new JsonArray(),
            ["requirements"] = new JsonArray(new JsonObject
            { ["kind"] = "resource_quantity", ["resourceRef"] = "energy", ["quantity"] = 17, ["ownerRole"] = "target" }),
            ["check"] = new JsonObject(), ["reveals"] = new JsonArray("route:clean_and_suture"), ["failurePolicy"] = "no_reveal"
        });
        var reads = new CanonicalReadActivityAccumulator(); var mutations = new List<string>(); var armed = false;
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output,
            suppliedInput: WoundPlayerProjectionTestData.Input(wound), reads: reads,
            beforeMutation: path => { if (armed) mutations.Add(path); });
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            _ = fixture.Files.GetOrCreateSessionGeneration(lease);
        var repairs = 0;
        var state = new StateManager(fixture.Files, new GameSettings(), NullLogger<StateManager>.Instance,
            new StateManagerHooks { AfterPlayerSoulProfileInputsReadAsync = () => { repairs++; return Task.CompletedTask; } });
        var before = fixture.Snapshot();
        var paths = new[] { WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath, ResourceMaterializationContract.DefinitionsPath };
        var counts = paths.Select(reads.ReadCount).ToArray(); armed = true;
        var console = new TestExplorerConsole();
        if (command == "/раны") console.QueueAnySelection("Закрыть");
        var locks = new LocalUiSessionLockService(fixture.Files);
        var explorer = new ExplorerMode(state, fixture.Files, new LocalizationManager(), console: console,
            localUiSessionLockService: locks,
            localUiSessionLockOwner: new("diagnosis_read_console", "console", "Условия осмотра", TimeSpan.FromSeconds(120)));
        Assert.Equal("", await explorer.TryProcessCommand(command));
        Assert.Empty(console.ConfirmPrompts);
        Assert.Equal(command == "/раны" ? 1 : 0, console.SelectionTitles.Count);
        if (await locks.InspectAsync() is { } owned)
        {
            Assert.Equal("diagnosis_read_console", owned.OwnerId);
            Assert.True(await locks.ReleaseAsync(new(owned.SessionGeneration, owned.OwnerId, owned.LeaseToken)));
        }
        Assert.Equal(before, fixture.Snapshot());
        var consoleReads = paths.Select((path, i) => (path, count: reads.ReadCount(path) - counts[i]))
            .ToDictionary(row => row.path, row => row.count);
        // Existing Mortal status has two distinct resource consumers: header
        // projection (definitions/state/History) and strict known-wound labels.
        Assert.All(consoleReads, row => Assert.Equal(command == "/статус" &&
            row.Key == ResourceMaterializationContract.DefinitionsPath ? 2L : 1L, row.Value));
        counts = paths.Select(reads.ReadCount).ToArray();
        var commands = new ExplorerWebCommandService(fixture.Files, state, new LocalizationManager(),
            new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance));
        var browser = await commands.ExecuteAsync(new(command, "diagnosis_read_browser"));
        Assert.Equal(CommandExecutionState.Completed, browser.State); Assert.Empty(browser.Prompts);
        Assert.Null(browser.InteractiveSession); Assert.Null(browser.PendingGmAction);
        Assert.Equal(before, fixture.Snapshot()); Assert.Equal(0, repairs);
        var browserReads = paths.Select((path, i) => (path, count: reads.ReadCount(path) - counts[i]))
            .ToDictionary(row => row.path, row => row.count);
        Assert.All(browserReads, row => Assert.Equal(command == "/статус" &&
            row.Key == ResourceMaterializationContract.DefinitionsPath ? 2L : 1L, row.Value));
        Assert.All(mutations, path => Assert.Equal(LocalUiSessionLockService.LockPath, path));
        foreach (var text in new[] { Render(console), JsonSerializer.Serialize(browser,
                     new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) })
        {
            Assert.Contains("Осторожный осмотр", text); Assert.Contains("17 ед.", text);
            Assert.Contains("не подтверждают наличие", text); Assert.Contains("пока недоступно", text);
            Assert.DoesNotContain("diagnosis_interface_private", text);
            Assert.DoesNotContain("clean_and_suture", text);
        }
        output.WriteLine(JsonSerializer.Serialize(new { command, SourceReadsPerHandler = new { Console = consoleReads, Browser = browserReads },
            GameplayMutations = mutations.Where(p => p != LocalUiSessionLockService.LockPath).ToArray(), repairs }));
    }

    private static string Render(TestExplorerConsole console)
    {
        using var writer = new StringWriter();
        var renderer = AnsiConsole.Create(new AnsiConsoleSettings { Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(writer) }); renderer.Profile.Width = 240;
        foreach (var item in console.Rendered) renderer.Write(item);
        foreach (var line in console.MarkupLines) renderer.MarkupLine(line);
        return writer.ToString();
    }
}
