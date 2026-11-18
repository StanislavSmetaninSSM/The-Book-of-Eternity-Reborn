using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityGMBridge;

namespace BookOfEternityClient.Tests;

internal static partial class GmBridgeConsumerScenarioDriver
{
    /// <summary>Exercises the actual neutral owner's protocol with the real Explorer menu; no owner or receipt is manufactured.</summary>
    private static async Task VerifyExplorerDispatchAsync(ExplorerParticipationFixture fixture, GmSessionRunRecord original,
        Dictionary<string, object?> result, CancellationToken token)
    {
        var files = fixture.Files;
        RequireNoExplorerAmbient(files);
        var before = fixture.GameInventory();
        var direct = fixture.Menu();await direct.Explorer.TryProcessCommand("/storage_move").WaitAsync(token);
        result["DirectConsole"] = direct.Console.MarkupLines;
        if (direct.Console.SelectionTitles.Count != 0 || !direct.Console.MarkupLines.Any(line => line.Contains("IOException", StringComparison.Ordinal)))
            throw new IOException("Direct-dispatch control did not reach the existing caught admission refusal.");
        RequireExplorerBytes(fixture, before);
        RequireNoExplorerAmbient(files);
        MainOperationClose? close = null;bool observed = false, remote = false;
        var participating = fixture.Menu();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(files,
            () => participating.Explorer.TryProcessCommand("/storage_move"),
            observeOriginalClose: (value, closed, wasRemote) => { close = value;observed = closed;remote = wasRemote; }).WaitAsync(token);
        result["ParticipatingConsole"] = participating.Console.MarkupLines;
        result["ParticipatingSelections"] = participating.Console.SelectionChoicesHistory;
        result["ControlOriginalClose"] = close;
        if (!ExplorerParticipationFixture.MenuObserved(participating.Console) || !observed || !remote || close?.Identity != original.Identity)
            throw new IOException("Existing participation control did not establish menu plus exact original protocol close.");
        RequireExplorerBytes(fixture, before);RequireNoExplorerAmbient(files);
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(files, () => Task.CompletedTask).WaitAsync(token);
        RequireNoExplorerAmbient(files);
        var desired = fixture.Menu();
        var returned = await GameEngine.DispatchExplorerForCurrentSessionAsync(files, desired.Explorer, "/storage_move").WaitAsync(token);
        result["SharedConsole"] = desired.Console.MarkupLines;
        result["SharedSelections"] = desired.Console.SelectionChoicesHistory;
        result["DesiredReturned"] = returned;
        RequireExplorerBytes(fixture, before);RequireNoExplorerAmbient(files);
        if (!ExplorerParticipationFixture.MenuObserved(desired.Console))
            throw new IOException("Shared GameEngine dispatch did not reach the actual inventory menu under the original Running owner.");
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(files, () => Task.CompletedTask).WaitAsync(token);
        RequireExplorerBytes(fixture, before);RequireNoExplorerAmbient(files);
        result["SharedMenuAndFreshPostReturnOperation"] = true;
    }

    /// <summary>Retains one genuine original pin until actual Stopping readback, then verifies no menu or gameplay effect before releasing it.</summary>
    private static async Task VerifyExplorerStoppingAsync(ExplorerParticipationFixture fixture, BridgeHost host,
        GmSessionRunRecord original, string output, Dictionary<string, object?> result, CancellationToken token)
    {
        var files = fixture.Files;RequireNoExplorerAmbient(files);var before = fixture.GameInventory();
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.ObserveMainMetadata = stage =>
        {
            if (stage != MainRunIoStage.Readback) return;
            var record = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(files.BasePath) ?? throw new IOException("Missing original metadata."));
            if (record.Identity == original.Identity && record.Disposition == GmSessionRunDisposition.Stopping) stopping.TrySetResult();
        };
        GmMainOperationClient? held = null;Task<JsonDocument>? shutdown = null;Exception? primary = null;
        try
        {
            held = await GmMainOperationClient.OpenAsync(files, token, original.Identity);
            shutdown = LauncherAsync("shutdown-bridge", files.GameSessionPath, output, token);
            await stopping.Task.WaitAsync(token);
            var menu = fixture.Menu();Exception? refusal = null;
            try { await GameEngine.DispatchExplorerForCurrentSessionAsync(files, menu.Explorer, "/storage_move").WaitAsync(token); }
            catch (IOException error) { refusal = error; }
            result["StoppingConsole"] = menu.Console.MarkupLines;result["StoppingRefusal"] = refusal?.ToString();
            if (menu.Console.SelectionTitles.Count != 0 || (refusal is null && !menu.Console.MarkupLines.Any(line => line.Contains("IOException", StringComparison.Ordinal))))
                throw new IOException("Stopping original unexpectedly admitted the Explorer menu.");
            RequireExplorerBytes(fixture, before);
            await held.CompleteAsync(MainOperationOutcome.Completed, false);
            if (!held.CloseObserved || held.TerminalClose?.Identity != original.Identity) throw new IOException("Held original did not close on its own channel.");
            await held.DisposeAsync();held = null;
            using var receipt = await shutdown.WaitAsync(token);
            if (!receipt.RootElement.GetProperty("ok").GetBoolean() || receipt.RootElement.GetProperty("fallbackUsed").GetBoolean() ||
                receipt.RootElement.GetProperty("status").GetString() != "original-scoped-stopped") throw new IOException("Original shutdown receipt missing.");
            result["StoppingRefusalAndOwnedClose"] = true;
        }
        catch (Exception failure) { primary = failure;throw; }
        finally
        {
            host.ObserveMainMetadata = null;
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var failures = new List<Exception>();
            if (held is not null)
                try { await held.DisposeAsync().AsTask().WaitAsync(cleanup.Token); }
                catch (Exception failure) { failures.Add(failure); }
            if (shutdown is not null && !shutdown.IsCompleted)
                try { using var receipt = await shutdown.WaitAsync(cleanup.Token); }
                catch (Exception failure) { failures.Add(failure); }
            result["StoppingControlCleanupFailures"] = failures.Select(value => value.ToString()).ToArray();
            if (failures.Count != 0)
            {
                if (primary is not null) primary.Data["StoppingControlCleanupFailures"] = failures;
                else throw new AggregateException("Stopping control cleanup failed.", failures);
            }
        }
    }

    /// <summary>Prevents an already-open local/remote parent from making direct RED dispatch pass accidentally.</summary>
    private static void RequireNoExplorerAmbient(FileSystemManager files)
    {
        if (GmSessionRunCoordinator.Current is not null || SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out _))
            throw new InvalidOperationException("Explorer fixture unexpectedly inherited an owner or generation scope.");
    }

    /// <summary>Checks exact seeded gameplay/request bytes without treating changing host diagnostic status as gameplay.</summary>
    private static void RequireExplorerBytes(ExplorerParticipationFixture fixture, string[] before)
    {
        if (!before.SequenceEqual(fixture.GameInventory())) throw new IOException("Explorer observation changed gameplay or request bytes.");
    }
}
