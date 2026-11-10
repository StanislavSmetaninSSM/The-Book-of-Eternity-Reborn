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
    [InlineData("drop", false)] [InlineData("drop", true)]
    [InlineData("split", false)] [InlineData("split", true)]
    [InlineData("merge", false)] [InlineData("merge", true)]
    public async Task OriginalInventoryOwnersRetainGenuinePublicationUncertaintyOnClose(string mode, bool uncertain)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-original-inventory-close-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launchArmed = false; var acquisitionPauses = 0; var publicationPauses = 0;
        string? target = null;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
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
            });
        cut.Attach(files); files.EnsureDirectoryStructure();
        await using (var seed = await files.AcquireCanonicalWriteLeaseAsync()) files.GetOrCreateSessionGeneration(seed);
        var beforeIndex = await SeedOriginalCloseInventoryAsync(files);
        target = files.ResolvePath(InventoryEquipmentService.ItemsPath); cut.Select = (path, _) => path == target;
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        Dictionary<string, byte[]> ReadCanonicalFiles() => Directory.EnumerateFiles(Path.Combine(files.GameSessionPath, "game_state"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        var beforeFiles = ReadCanonicalFiles();
        var closeFailure = new IOException("Actual inventory original owner late close.");
        var closer = new ThrowingClose(closeFailure); FileSystemManager.CanonicalWriteLease? original = null;
        var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null && Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        var boundary = mode switch
        {
            "drop" => "InventoryManagementService+<DropAsync>",
            "split" => "InventoryManagementService+<SplitAsync>",
            _ => "InventoryManagementService+<MergeAsync>"
        };
        Task? operation = null; Exception? failure = null; InventoryManagementWriteOutcome? result = null; string? actualOwnerState = null;
        try
        {
            launchArmed = true;
            operation = mode switch
            {
                "drop" => InventoryManagementService.DropAsync(files, "itm_real"),
                "split" => InventoryManagementService.SplitAsync(files, "itm_real", 2),
                _ => InventoryManagementService.MergeAsync(files, "itm_real")
            };
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            (original, actualOwnerState) = InspectOriginalNestedOwningLease(operation, boundary);
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe; cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
            if (failure == null) result = await Assert.IsAssignableFrom<Task<InventoryManagementWriteOutcome>>(operation);
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
            attachments, closer.Calls, result, failure = failure?.ToString(), samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure), lockAvailable,
            activeAfter = original!.IsActive, ambientClosed = original.AmbientRegistration == null,
            mainClosed = original.MainAdmission == null, contextClosed = original.ExternalPublicationContext == null,
            generationBefore, generationAfter, targetAfter, beforeFiles, afterFiles, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext);
        Assert.Equal(generationBefore, generationAfter);

        if (uncertain)
        {
            Assert.Null(result); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls); Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]); cut.AssertReachedAndStopped();
            Assert.Equal(beforeFiles.Keys.Order(StringComparer.Ordinal), afterFiles.Keys.Order(StringComparer.Ordinal));
            foreach (var path in beforeFiles.Keys.Where(path => path != target)) Assert.Equal(beforeFiles[path], afterFiles[path]);
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls); Assert.False(File.Exists(cut.JournalPath));
            var actual = Assert.IsType<InventoryManagementWriteOutcome>(result);
            Assert.True(actual.Success, actual.Message); Assert.Equal("itm_real", actual.ItemIdentity); Assert.Equal("Moon herb", actual.ItemName);
            Assert.Equal(mode == "drop" ? 5 : mode == "split" ? 2 : 7, actual.Count);
            Assert.NotNull(targetAfter);
            var inventory = JsonNode.Parse(LocalSettingsPreparation.DecodeText(targetAfter))!.AsObject();
            var items = inventory["items"]!.AsArray().OfType<JsonObject>().ToArray();
            var index = MortalItemIdentityState.Parse(LocalSettingsPreparation.DecodeText(File.ReadAllBytes(files.ResolvePath(MortalItemIdentityState.StatePath))));
            Assert.Empty(index.Issues); Assert.Empty(MortalItemIdentityState.ValidateAgainst(beforeIndex, index));
            var selected = index.EntriesByItemId["itm_real"];
            if (mode == "drop")
            {
                Assert.Equal("destroyed", selected["state"]!.GetValue<string>()); Assert.Null(selected["currentCarrier"]);
                Assert.Equal("destroy", selected["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
                var remaining = Assert.Single(items); Assert.Equal("itm_second", remaining["itemId"]!.GetValue<string>()); Assert.Equal(2, remaining["count"]!.GetValue<int>());
                Assert.Null(actual.DerivedItemIdentity);
            }
            else if (mode == "split")
            {
                Assert.Equal(3, items.Length); Assert.False(string.IsNullOrWhiteSpace(actual.DerivedItemIdentity));
                Assert.Equal(3, Assert.Single(items, i => i["itemId"]!.GetValue<string>() == "itm_real")["count"]!.GetValue<int>());
                var child = Assert.Single(items, i => i["itemId"]!.GetValue<string>() == actual.DerivedItemIdentity);
                Assert.Equal(2, child["count"]!.GetValue<int>());
                Assert.Equal("active", index.EntriesByItemId[actual.DerivedItemIdentity!]["state"]!.GetValue<string>());
                Assert.Equal("split", selected["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
            }
            else
            {
                var survivor = Assert.Single(items); Assert.Equal("itm_real", survivor["itemId"]!.GetValue<string>()); Assert.Equal(7, survivor["count"]!.GetValue<int>());
                Assert.Equal("active", selected["state"]!.GetValue<string>());
                var contributor = index.EntriesByItemId["itm_second"];
                Assert.Equal("merged", contributor["state"]!.GetValue<string>()); Assert.Null(contributor["currentCarrier"]);
                Assert.Equal("itm_real", contributor["mergedIntoItemId"]!.GetValue<string>());
                Assert.Equal("merge", selected["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>()); Assert.Null(actual.DerivedItemIdentity);
            }
            var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
            Assert.Empty(await validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync());
        }
    }

    private static async Task<MortalItemIdentityParseResult> SeedOriginalCloseInventoryAsync(FileSystemManager files)
    {
        await files.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
        JsonObject Item(string id, int count)
        {
            var raw = MortalItemTestFixture.CreateRawRoot(creationRef: "new_" + id, materializationId: "mat_" + id);
            raw["name"] = "Moon herb"; raw["description"] = "Original owner fixture herb"; raw["type"] = "material"; raw["group"] = "Herbs"; raw["count"] = count;
            var receipt = MortalItemIdentityState.CreateRootReceipt(raw, id, 42);
            raw["itemId"] = id; raw["existedId"] = id; raw.Remove("creationRef"); raw["materializationReceipt"] = receipt; return raw;
        }
        var items = new[] { Item("itm_real", 5), Item("itm_second", 2) };
        await files.WriteFileAtomicAsync(InventoryEquipmentService.ItemsPath, new JsonObject { ["items"] = new JsonArray(items.Select(i => (JsonNode?)i.DeepClone()).ToArray()), ["equippedItems"] = new JsonObject() }.ToJsonString());
        var indexRoot = MortalItemTestFixture.CreateIndex(items);
        await files.WriteFileAtomicAsync(MortalItemIdentityState.StatePath, indexRoot.ToJsonString());
        var resources = ResourceBootstrapStateBuilder.BuildPristine(); Assert.True(resources.IsValid);
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(resources.Definitions, files.ReadFileAsync, resources.State, resources.History, CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(authority.IsValid, string.Join(Environment.NewLine, authority.Issues));
        await files.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, authority.CanonicalAuthorityJson!);
        var before = MortalItemIdentityState.Parse(indexRoot); Assert.Empty(before.Issues);
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync());
        return before;
    }
}
