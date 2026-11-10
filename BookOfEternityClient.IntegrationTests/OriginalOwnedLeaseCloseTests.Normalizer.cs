using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    [Theory]
    [InlineData("generic", false)] [InlineData("generic", true)]
    [InlineData("with_plan", false)] [InlineData("with_plan", true)]
    [InlineData("bootstrap", false)] [InlineData("bootstrap", true)]
    [InlineData("accepted_mechanics", false)] [InlineData("accepted_mechanics", true)]
    public async Task OriginalNormalizerOwnersRetainGenuinePublicationUncertaintyOnClose(string mode, bool uncertain)
    {
        Assert.True(OperatingSystem.IsLinux());
        using var cut = new CleanupPublicationCut();
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launchArmed = false; var acquisitionPauses = 0; var publicationPauses = 0;
        string? target = null;
        var hooks = new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (launchArmed && phase == TrustedLocalPublicationPhase.IntentPublished && publicationPauses == 0)
                    {
                        using var actual = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
                        if (actual.RootElement.GetProperty("Members").EnumerateArray().Any(m => m.GetProperty("Path").GetString() == target))
                        { publicationPauses++; entered.TrySetResult(); allow.Task.GetAwaiter().GetResult(); }
                    }
                    cut.Hooks.LocalPublicationObserver!(phase, index);
                },
                BeforeCanonicalWriteLockOpenAsync = async () =>
                {
                    await cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                    if (launchArmed && acquisitionPauses == 0)
                    { acquisitionPauses++; acquireEntered.TrySetResult(); await allowAcquire.Task; }
                },
                BeforeCanonicalMutationBoundaryAsync = cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
                AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
                LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
                SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync
            };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        var files = context.FileSystem; var root = context.RootPath;
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        cut.Attach(files);
        AcceptedMechanicsPlan? expectedPlan = null;
        if (mode is "with_plan" or "accepted_mechanics")
        {
            await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
            if (mode == "with_plan")
                await files.WriteFileAtomicAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
            await context.CaptureValidatedPendingSnapshotAsync();
            if (mode == "with_plan")
            {
                var itemIssues = await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
                Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
            }
            await context.WriteExactJsonAsync(ResourceMaterializationTestContext.CommandsPath, ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());
            var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
            Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
            await using var probe = await files.AcquireCanonicalWriteLeaseAsync();
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, probe, out var binding, out var planning));
            Assert.True(planning.Success); expectedPlan = Assert.IsType<AcceptedMechanicsPlan>(planning.Plan);
            if (mode == "with_plan")
            {
                Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(files, probe,
                    binding.SessionId, binding.SnapshotToken, binding.Turn, out var snapshot));
                Assert.True(snapshot.MatchesAcceptedOwnerAuthority(expectedPlan.OwnerAuthority));
            }
            target = files.ResolvePath(ResourceMaterializationContract.DefinitionsPath);
        }
        else if (mode == "bootstrap")
        {
            await files.WriteFileAtomicAsync("input/turn_request.json", "{\"sessionId\":\"session_bootstrap_owner\",\"requestId\":\"request_bootstrap_owner\",\"turnNumber\":42,\"playerAction\":\"Initialize supported test item.\"}");
            await files.WriteFileAtomicAsync(InventoryEquipmentService.ItemsPath, new JsonObject { ["items"] = new JsonArray(), ["equipment"] = new JsonObject(), ["UpdateInventory"] = new JsonArray(MortalItemTestFixture.CreateRawRoot()) }.ToJsonString());
            await files.WriteFileAtomicAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
            target = files.ResolvePath(InventoryEquipmentService.ItemsPath);
        }
        else
        {
            await files.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", new JsonObject { ["NPCJournals"] = new JsonArray(new JsonObject { ["NPCId"] = "npc_journal", ["note"] = "Current authored note", ["timestamp"] = "2026-10-09T00:00:00Z", ["journalEntries"] = new JsonArray("Current authored entry") }) }.ToJsonString());
            target = files.ResolvePath("game_state/npcs/npc_journals.json");
        }
        cut.Select = (path, _) => path == target;
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        Dictionary<string, byte[]> ReadCanonicalFiles() => Directory.EnumerateFiles(Path.Combine(files.GameSessionPath, "game_state"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        var beforeFiles = ReadCanonicalFiles();
        var closeFailure = new IOException("Actual normalizer original owner late close.");
        var closer = new ThrowingClose(closeFailure); FileSystemManager.CanonicalWriteLease? original = null;
        var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null && Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        var method = mode switch
        {
            "generic" => "NormalizeAccumulatedStateAsync",
            "with_plan" => "NormalizeAccumulatedStateWithPlanAsync",
            "bootstrap" => "NormalizeClientOwnedBootstrapAccumulatedStateAsync",
            _ => "NormalizeAcceptedMechanicsAsync"
        };
        var boundary = "CanonicalStateNormalizer+<" + method + ">";
        Task? operation = null; Exception? failure = null; AcceptedMechanicsPlan? result = null; Dictionary<string, byte[]>? atIntentFiles = null; string? actualOwnerState = null;
        try
        {
            launchArmed = true;
            operation = mode switch
            {
                "generic" => context.Normalizer.NormalizeAccumulatedStateAsync(),
                "with_plan" => context.Normalizer.NormalizeAccumulatedStateWithPlanAsync(),
                "bootstrap" => context.Normalizer.NormalizeClientOwnedBootstrapAccumulatedStateAsync(),
                _ => context.Normalizer.NormalizeAcceptedMechanicsAsync(backups: null)
            };
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            atIntentFiles = ReadCanonicalFiles();
            (original, actualOwnerState) = InspectOriginalNestedOwningLease(operation, boundary);
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe; cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
            if (failure == null && (mode is "with_plan" or "accepted_mechanics")) result = await Assert.IsAssignableFrom<Task<AcceptedMechanicsPlan?>>(operation);
        }
        finally
        {
            allowAcquire.TrySetResult(); allow.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(() => operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
        }
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var targetAfter = CleanupPublicationCut.ReadOptional(target);
        var generationAfter = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        var afterFiles = ReadCanonicalFiles();
        output.WriteLine(JsonSerializer.Serialize(new { mode, uncertain, root, boundary, actualOwnerState, acquisitionPauses, publicationPauses,
            attachments, closer.Calls, resultPresent = result != null, sameActualPlan = result != null && ReferenceEquals(result, expectedPlan), failure = failure?.ToString(), samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure), lockAvailable,
            activeAfter = original!.IsActive, ambientClosed = original.AmbientRegistration == null,
            mainClosed = original.MainAdmission == null, contextClosed = original.ExternalPublicationContext == null,
            generationBefore, generationAfter, targetAfter, beforeFiles, atIntentFiles, afterFiles, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext);
        Assert.Equal(generationBefore, generationAfter);

        if (uncertain)
        {
            Assert.Null(result); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls); Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]); cut.AssertReachedAndStopped(); Assert.Equal(0, cut.ClosingLeases);
            Assert.NotNull(atIntentFiles);
            Assert.Equal(atIntentFiles.Keys.Order(StringComparer.Ordinal), afterFiles.Keys.Order(StringComparer.Ordinal));
            foreach (var path in atIntentFiles.Keys.Where(path => path != target)) Assert.Equal(atIntentFiles[path], afterFiles[path]);
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls); Assert.False(File.Exists(cut.JournalPath));
            if (mode is "with_plan" or "accepted_mechanics")
            {
                Assert.Same(expectedPlan, Assert.IsType<AcceptedMechanicsPlan>(result));
                foreach (var pair in new[] { (ResourceMaterializationContract.DefinitionsPath, result.DefinitionAfterImage), (ResourceMaterializationContract.StatePath, result.StateAfterImage), (ResourceMaterializationContract.HistoryPath, result.HistoryAfterImage) })
                    Assert.True(JsonNode.DeepEquals(pair.Item2, await context.ReadJsonAsync(pair.Item1)));
                var definitions = Assert.IsType<JsonObject>(await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath));
                Assert.Equal("mana", Assert.Single(definitions["definitions"]!.AsArray())["resourceKey"]!.GetValue<string>());
                Assert.False(File.Exists(files.ResolvePath(ResourceMaterializationTestContext.CommandsPath)));
                Assert.Empty(await context.Validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync());
            }
            else if (mode == "bootstrap")
            {
                Assert.Null(result);
                var items = JsonNode.Parse((await files.ReadFileAsync(InventoryEquipmentService.ItemsPath))!)!.AsObject();
                Assert.False(items.ContainsKey("UpdateInventory")); var item = Assert.Single(items["items"]!.AsArray().OfType<JsonObject>());
                var itemId = item["itemId"]!.GetValue<string>(); Assert.StartsWith("itm_", itemId, StringComparison.Ordinal); Assert.Equal(itemId, item["existedId"]!.GetValue<string>()); Assert.False(item.ContainsKey("creationRef"));
                var receipt = item["materializationReceipt"]!.AsObject(); Assert.Equal(42, receipt["acceptedAtTurn"]!.GetValue<int>());
                var index = MortalItemIdentityState.Parse(await files.ReadFileAsync(MortalItemIdentityState.StatePath)); Assert.Empty(index.Issues);
                var entry = Assert.Single(index.EntriesByItemId).Value; Assert.Equal(itemId, entry["itemId"]!.GetValue<string>()); Assert.Equal(receipt["receiptId"]!.GetValue<string>(), entry["receiptId"]!.GetValue<string>());
                Assert.Equal("active", entry["state"]!.GetValue<string>()); Assert.Equal("player_inventory", entry["currentCarrier"]!["kind"]!.GetValue<string>());
                Assert.Equal("create", Assert.Single(entry["transitions"]!.AsArray())["kind"]!.GetValue<string>());
                Assert.Empty(await context.Validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync());
            }
            else
            {
                Assert.Null(result); Assert.NotNull(targetAfter);
                var journal = JsonNode.Parse(LocalSettingsPreparation.DecodeText(targetAfter))!["NPCJournals"]![0]!;
                Assert.Equal("Current authored note", journal["lastJournalNote"]!.GetValue<string>());
                var entry = Assert.Single(journal["journalEntries"]!.AsArray()); Assert.Equal("Current authored entry", entry["description"]!.GetValue<string>());
                Assert.Equal("2026-10-09T00:00:00Z", entry["timestamp"]!.GetValue<string>());
            }
        }
    }
}
