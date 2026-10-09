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
    [InlineData("guardian_write", false)] [InlineData("guardian_write", true)]
    [InlineData("guardian_scoped", false)] [InlineData("guardian_scoped", true)]
    [InlineData("guardian_clear", false)] [InlineData("guardian_clear", true)]
    [InlineData("guardian_health", false)] [InlineData("guardian_health", true)]
    [InlineData("shining_write", false)] [InlineData("shining_write", true)]
    [InlineData("shining_scoped", false)] [InlineData("shining_scoped", true)]
    [InlineData("shining_collection", false)] [InlineData("shining_collection", true)]
    [InlineData("shining_replace", false)] [InlineData("shining_replace", true)]
    [InlineData("shining_health", false)] [InlineData("shining_health", true)]
    [InlineData("journal_append", false)] [InlineData("journal_append", true)]
    [InlineData("journal_repair", false)] [InlineData("journal_repair", true)]
    public async Task OriginalPendingControlAndJournalOwnersRetainGenuineUncertaintyOnClose(string mode, bool uncertain)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-original-pending-close-" + Guid.NewGuid().ToString("N"));
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
        var guardianRequest = new GuardianTradeRequestState.PendingGuardianTradeRequest
        {
            RequestId = "guardian-request-real", GuardianId = "guardian-real", GuardianName = "Guardian",
            AbodeId = "abode-real", ReturnCycleId = "cycle-real", DerivedTradeSlotCount = 1,
            CreatedAtTurn = 1, CreatedAtUtc = "2026-10-09T00:00:00Z"
        };
        var shiningRequest = new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
        {
            RequestId = "shining-request-real", FactionId = "faction-real", FactionName = "Faction",
            TradeCycleId = "cycle-real", DerivedTradeSlotCount = 1, DerivedTradeTier = 1,
            DerivedRarityCeiling = "Common", DerivedServiceMultiplier = 1,
            CreatedAtTurn = 1, CreatedAtUtc = "2026-10-09T00:00:00Z"
        };
        LocalInteractionScope? scope = null; string? expected = null;
        if (mode == "guardian_scoped")
        {
            await files.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Chaos Sea\"}");
            await files.WriteFileAtomicAsync("game_state/meta/guardians.json", """
                {"chaosSeaNavigation":{"currentAbodeId":"abode-real","currentGuardianId":"guardian-real"},
                 "activeGuardianId":"guardian-real","guardians":[{"guardianId":"guardian-real","guardianName":"Guardian",
                 "abode":{"abodeId":"abode-real","name":"Abode"}}]}
                """);
            scope = await new LocalInteractionScopeService(files).ResolveAsync();
            Assert.True(scope.IsResolved); Assert.Equal(LocalInteractionRealmKind.ChaosSea, scope.RealmKind);
            Assert.Equal("guardian-real", scope.CurrentGuardianId); Assert.NotEmpty(scope.AuthoritySnapshots!);
            expected = await files.ReadFileAsync("game_state/meta/guardians.json");
        }
        if (mode == "shining_scoped")
        {
            await files.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Shining Abode\"}");
            var faction = ShiningFactionTestMaterialization.Apply(JsonNode.Parse("""
                {"factionId":"faction-real","factionName":"Faction","hallId":"hall-real","visibility":"revealed"}
                """)!.AsObject(), 1, hasResidentAffiliations: false, canTrade: true);
            var shining = new JsonObject
            {
                ["currentHallId"] = "hall-real",
                ["halls"] = new JsonArray(new JsonObject { ["hallId"] = "hall-real", ["hallName"] = "Hall" }),
                ["factions"] = new JsonArray(faction)
            };
            await files.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shining.ToJsonString());
            scope = await new LocalInteractionScopeService(files).ResolveAsync();
            Assert.True(scope.IsResolved); Assert.Equal(LocalInteractionRealmKind.ShiningAbode, scope.RealmKind);
            Assert.Equal("hall-real", scope.LocationId); Assert.Contains("faction-real", scope.LocalFactionIds); Assert.NotEmpty(scope.AuthoritySnapshots!);
        }
        if (mode is "guardian_clear" or "guardian_health") await GuardianTradeRequestState.WriteAsync(files, guardianRequest);
        if (mode == "shining_replace")
        {
            await ShiningTradeRequestState.WriteRequestAsync(files, shiningRequest);
            expected = await files.ReadFileAsync(ShiningTradeRequestState.PendingRequestsPath);
        }
        if (mode == "shining_health") await files.WriteFileAtomicAsync(ShiningTradeRequestState.PendingRequestsPath, "{\"requests\":[]}");
        var entry = JsonNode.Parse("""
            {"eventId":"event-real","guardianId":"guardian-real","reasonType":"offering","sourceId":"source-real",
             "sourceSurface":"offering","delta":1,"visibility":"player_known","turn":1}
            """)!.AsObject();
        if (mode == "journal_repair")
        {
            await files.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
                {"activeProjects":[{"guardianId":"guardian-real","project":{"projectId":"project-real","projectName":"Project",
                  "projectType":"counter_rival_operation","projectTier":"minor"}}],"completedProjects":[]}
                """);
            entry["reasonType"] = "project_assist"; entry["sourceId"] = "project-real"; entry["sourceSurface"] = "assistGuardianProject";
            await files.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, new JsonObject { ["entries"] = new JsonArray(entry.DeepClone()) }.ToJsonString());
        }
        var relative = mode.StartsWith("guardian", StringComparison.Ordinal) ? GuardianTradeRequestState.PendingRequestPath
            : mode.StartsWith("shining", StringComparison.Ordinal) ? ShiningTradeRequestState.PendingRequestsPath : GuardianPowerEventState.JournalPath;
        target = files.ResolvePath(relative); cut.Select = (path, _) => path == target;
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        Dictionary<string, byte[]> ReadCanonicalFiles() => Directory.EnumerateFiles(Path.Combine(files.GameSessionPath, "game_state"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        var beforeFiles = ReadCanonicalFiles();
        var closeFailure = new IOException("Actual pending/control original owner late close.");
        var closer = new ThrowingClose(closeFailure); FileSystemManager.CanonicalWriteLease? original = null;
        var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null && Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        var boundary = mode switch
        {
            "guardian_write" => "GuardianTradeRequestState+<WriteAsync>",
            "guardian_scoped" => "GuardianTradeRequestState+<TryWriteScopedAsync>",
            "guardian_clear" => "GuardianTradeRequestState+<ClearIfMatchesAsync>",
            "guardian_health" => "GuardianTradeRequestState+<EnsureHealthyAsync>",
            "shining_write" => "ShiningTradeRequestState+<WriteRequestAsync>",
            "shining_scoped" => "ShiningTradeRequestState+<TryWriteScopedRequestAsync>",
            "shining_collection" => "ShiningTradeRequestState+<WriteRequestsAsync>",
            "shining_replace" => "ShiningTradeRequestState+<TryReplaceRequestsSnapshotAsync>",
            "shining_health" => "ShiningTradeRequestState+<EnsureHealthyAsync>",
            "journal_append" => "GuardianPowerEventState+<AppendJournalEntriesAsync>",
            _ => "GuardianPowerEventState+<RepairJournalAsync>"
        };
        Task? operation = null; Exception? failure = null; string? actualOwnerState = null;
        try
        {
            launchArmed = true;
            operation = mode switch
            {
                "guardian_write" => GuardianTradeRequestState.WriteAsync(files, guardianRequest),
                "guardian_scoped" => GuardianTradeRequestState.TryWriteScopedAsync(files, guardianRequest, scope!, expected!),
                "guardian_clear" => GuardianTradeRequestState.ClearIfMatchesAsync(files, guardianRequest),
                "guardian_health" => GuardianTradeRequestState.EnsureHealthyAsync(files, "Mortal World"),
                "shining_write" => ShiningTradeRequestState.WriteRequestAsync(files, shiningRequest),
                "shining_scoped" => ShiningTradeRequestState.TryWriteScopedRequestAsync(files, shiningRequest, scope!),
                "shining_collection" => ShiningTradeRequestState.WriteRequestsAsync(files, [shiningRequest]),
                "shining_replace" => ShiningTradeRequestState.TryReplaceRequestsSnapshotAsync(files, expected, []),
                "shining_health" => ShiningTradeRequestState.EnsureHealthyAsync(files, "Mortal World"),
                "journal_append" => GuardianPowerEventState.AppendJournalEntriesAsync(files, [entry]),
                _ => GuardianPowerEventState.RepairJournalAsync(files)
            };
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            (original, actualOwnerState) = InspectOriginalNestedOwningLease(operation, boundary);
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe; cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
        }
        finally
        {
            allowAcquire.TrySetResult(); allow.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(() => operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
        }
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var gateAvailable = true;
        if (!mode.StartsWith("journal", StringComparison.Ordinal))
        {
            var type = mode.StartsWith("guardian", StringComparison.Ordinal) ? typeof(GuardianTradeRequestState) : typeof(ShiningTradeRequestState);
            var gate = (SemaphoreSlim)type.GetField("RequestWriteGate", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            gateAvailable = await gate.WaitAsync(0); if (gateAvailable) gate.Release();
        }
        var targetAfter = CleanupPublicationCut.ReadOptional(target);
        var generationAfter = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        var afterFiles = ReadCanonicalFiles();
        output.WriteLine(JsonSerializer.Serialize(new { mode, uncertain, root, boundary, actualOwnerState, acquisitionPauses, publicationPauses,
            attachments, closer.Calls, failure = failure?.ToString(), samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure), lockAvailable, gateAvailable,
            activeAfter = original!.IsActive, ambientClosed = original.AmbientRegistration == null,
            mainClosed = original.MainAdmission == null, contextClosed = original.ExternalPublicationContext == null,
            generationBefore, generationAfter, targetAfter, beforeFiles, afterFiles, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable); Assert.True(gateAvailable);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext);
        Assert.Equal(generationBefore, generationAfter);
        Assert.Equal(beforeFiles.Keys.Where(path => path != target).Order(StringComparer.Ordinal),
            afterFiles.Keys.Where(path => path != target).Order(StringComparer.Ordinal));
        foreach (var path in beforeFiles.Keys.Where(path => path != target)) Assert.Equal(beforeFiles[path], afterFiles[path]);
        if (uncertain)
        {
            Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls); Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]); cut.AssertReachedAndStopped();
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls); Assert.False(File.Exists(cut.JournalPath));
            if (mode is "guardian_scoped" or "guardian_clear" or "shining_scoped" or "shining_replace")
                Assert.True(Assert.IsAssignableFrom<Task<bool>>(operation).Result);
            if (mode is "guardian_clear" or "guardian_health" or "shining_replace" or "shining_health") Assert.Null(targetAfter);
            else
            {
                Assert.NotNull(targetAfter); var persisted = JsonNode.Parse(LocalSettingsPreparation.DecodeText(targetAfter));
                if (mode.StartsWith("guardian", StringComparison.Ordinal))
                {
                    Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(guardianRequest), persisted));
                    Assert.Equal(guardianRequest.RequestId, persisted!["requestId"]!.GetValue<string>());
                    Assert.Equal(guardianRequest.GuardianId, persisted["guardianId"]!.GetValue<string>());
                    Assert.Equal(guardianRequest.ReturnCycleId, persisted["returnCycleId"]!.GetValue<string>());
                }
                else if (mode.StartsWith("shining", StringComparison.Ordinal))
                {
                    var actual = Assert.Single(persisted!["requests"]!.AsArray());
                    Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(shiningRequest), actual));
                    Assert.Equal(shiningRequest.RequestId, actual!["requestId"]!.GetValue<string>());
                    Assert.Equal(shiningRequest.FactionId, actual["factionId"]!.GetValue<string>());
                    Assert.Equal(shiningRequest.TradeCycleId, actual["tradeCycleId"]!.GetValue<string>());
                }
                else
                {
                    var actual = Assert.Single(persisted!["entries"]!.AsArray()); Assert.Equal("event-real", actual!["eventId"]!.GetValue<string>());
                    if (mode == "journal_repair")
                    {
                        Assert.Equal("guardian-real", actual["audit"]!["projectGuardianId"]!.GetValue<string>());
                        Assert.Equal("project-real", actual["audit"]!["projectId"]!.GetValue<string>());
                        Assert.Equal("Project", actual["audit"]!["projectName"]!.GetValue<string>());
                        Assert.Equal("counter_rival_operation", actual["audit"]!["projectType"]!.GetValue<string>());
                        Assert.Equal("minor", actual["audit"]!["projectTier"]!.GetValue<string>());
                    }
                    else Assert.True(JsonNode.DeepEquals(entry, actual));
                }
            }
        }
    }
}
