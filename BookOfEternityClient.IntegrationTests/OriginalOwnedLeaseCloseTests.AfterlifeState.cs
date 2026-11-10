using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    [Theory]
    [InlineData("owner_resource", false)] [InlineData("owner_resource", true)]
    [InlineData("blessing_bootstrap", false)] [InlineData("blessing_bootstrap", true)]
    [InlineData("memory_selection", false)] [InlineData("memory_selection", true)]
    [InlineData("progression", false)] [InlineData("progression", true)]
    public async Task OriginalAfterlifeStateOwnersRetainGenuinePublicationUncertaintyOnClose(string mode, bool uncertain)
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
                    target = actual.RootElement.GetProperty("Members")[0].GetProperty("Path").GetString();
                    publicationPauses++; entered.TrySetResult(); allow.Task.GetAwaiter().GetResult();
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
        var soul = new JsonObject
        {
            ["soulName"] = "Owner fixture soul", ["currentRealm"] = mode == "progression" ? "Chaos Sea" : "Mortal World",
            ["currentIncarnation"] = 4, ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
            ["afterlifeCombatProfile"] = new JsonObject { ["spiritFocusTier"] = 0 },
            ["soulRelics"] = new JsonObject { ["equipped"] = new JsonArray(), ["stored"] = new JsonArray() },
            ["livesHistory"] = new JsonArray(new JsonObject { ["incarnation"] = 3, ["summary"] = "Current memory fixture" })
        };
        var profiles = new JsonObject
        {
            [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(new JsonObject
            { ["actorType"] = "player_soul", ["actorId"] = "player_soul", ["displayName"] = "Owner fixture soul", ["realm"] = "Shining Abode" })
        };
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(files,
            new AfterlifeOwnerResourceAcceptedState(Profiles: profiles, SoulState: soul));
        AfterlifeOwnerResourceStateFilePlan? ownerPlan = null;
        ProgressionScheduleService? progression = null; ProgressionControl? control = null;
        ShiningBlessingEffectState.PendingMemorySelectionState? memory = null;
        var card = new JsonObject
        {
            ["cardId"] = "card_memory", ["dedupeKey"] = "memory:card_memory", ["sourceType"] = ShiningAbodeState.CardSourceTypeProject,
            ["sourceFactionId"] = "faction_dawn", ["displayName"] = "Current memory", ["displaySummary"] = "memory",
            ["sourceActorId"] = "guardian_dawn", ["effectFamily"] = "memory", ["rarity"] = ShiningAbodeState.RarityCommon,
            ["effectPayload"] = new JsonObject { ["type"] = "expand_memory_selection", ["options"] = 1,
                [ShiningBlessingRerollAllocationContract.PropertyName] = ShiningBlessingRerollAllocationContract.Create(1) }
        };
        var package = new JsonObject { ["preparedAtTurn"] = 42, ["selectedCardIds"] = new JsonArray("card_memory"), ["selectedCards"] = new JsonArray(card) };
        Assert.Null(ShiningAbodeState.ValidatePreparedIncarnationPackageForBootstrap(package));
        if (mode == "owner_resource")
        {
            var projected = JsonNode.Parse((await files.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            projected["inkFeathers"]!["current"] = 5; projected["inkFeathers"]!["total"] = 5;
            ownerPlan = await AfterlifeOwnerResourceStateService.BuildAsync(files, new AfterlifeOwnerResourceAcceptedState(SoulState: projected), 42);
            Assert.True(ownerPlan.IsValid, string.Join(";", ownerPlan.Issues));
        }
        else if (mode == "memory_selection")
        {
            var bootstrap = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(files, package, 4);
            Assert.True(bootstrap.Success); Assert.True(bootstrap.StateChanged);
            memory = await ShiningBlessingEffectState.ReadPendingMemorySelectionAsync(files);
            Assert.NotNull(memory); Assert.Equal(1, memory.Rerolls); Assert.Equal(3, Assert.Single(memory.Candidates).Incarnation);
        }
        else if (mode == "progression")
        {
            await files.WriteFileAtomicAsync("input/turn_request.json", "{\"sessionId\":\"session_progression_owner\",\"requestId\":\"request_progression_owner\",\"turnNumber\":1}");
            progression = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
            control = await progression.BuildControlForNextTurnAsync();
            Assert.True(control.MustEvaluateChaosSeaProgression);
            await files.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, new JsonObject { ["progressionProcessingReport"] = new JsonObject
            {
                ["sessionId"] = "session_progression_owner", ["requestId"] = "request_progression_owner", ["turnNumber"] = 1,
                ["worldCyclesProcessed"] = 0, ["factionCyclesProcessed"] = 0, ["chaosSeaCyclesProcessed"] = 1,
                ["guardianProjectCyclesProcessed"] = 1, ["residentAgencyCyclesProcessed"] = 1,
                ["newLastChaosSeaSimulationOrdinal"] = 1, ["newLastGuardianProjectCycleOrdinal"] = 1, ["newLastResidentAgencyCycleOrdinal"] = 1
            } }.ToJsonString());
        }
        Assert.Empty(await context.Validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync());
        cut.Select = (path, _) => path == target;
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        Dictionary<string, byte[]> ReadCanonicalFiles() => Directory.EnumerateFiles(Path.Combine(files.GameSessionPath, "game_state"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        var beforeFiles = ReadCanonicalFiles();
        var closeFailure = new IOException("Actual afterlife state original owner late close.");
        var closer = new ThrowingClose(closeFailure); FileSystemManager.CanonicalWriteLease? original = null; var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null && Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        var boundary = mode switch
        {
            "owner_resource" => "AfterlifeOwnerResourceStateService+<TryCommitAsync>",
            "blessing_bootstrap" => "ShiningBlessingEffectState+<MaterializeForBootstrapAsync>",
            "memory_selection" => "ShiningBlessingEffectState+<ConsumePendingMemorySelectionAsync>",
            _ => "ProgressionScheduleService+<ApplyAcceptedTurnOutcomeAsync>"
        };
        Task? operation = null; Exception? failure = null; bool? boolResult = null;
        ShiningBlessingEffectState.BootstrapMaterializationResult? bootstrapResult = null;
        Dictionary<string, byte[]>? atIntentFiles = null; string? actualOwnerState = null;
        try
        {
            launchArmed = true;
            operation = mode switch
            {
                "owner_resource" => AfterlifeOwnerResourceStateService.TryCommitAsync(files, ownerPlan!),
                "blessing_bootstrap" => ShiningBlessingEffectState.MaterializeForBootstrapAsync(files, package, 4),
                "memory_selection" => ShiningBlessingEffectState.ConsumePendingMemorySelectionAsync(files, 0, memory!.Candidates[0], 1),
                _ => progression!.ApplyAcceptedTurnOutcomeAsync(control)
            };
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            atIntentFiles = ReadCanonicalFiles();
            (original, actualOwnerState) = InspectOriginalNestedOwningLease(operation, boundary);
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe; cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
            if (failure == null && (mode is "owner_resource" or "memory_selection")) boolResult = await Assert.IsAssignableFrom<Task<bool>>(operation);
            if (failure == null && mode == "blessing_bootstrap") bootstrapResult = await Assert.IsAssignableFrom<Task<ShiningBlessingEffectState.BootstrapMaterializationResult>>(operation);
        }
        finally
        {
            allowAcquire.TrySetResult(); allow.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(() => operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
        }
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var generationAfter = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath); var afterFiles = ReadCanonicalFiles();
        output.WriteLine(JsonSerializer.Serialize(new { mode, uncertain, root, target, boundary, actualOwnerState, acquisitionPauses, publicationPauses,
            attachments, closer.Calls, boolResult, bootstrapResultPresent = bootstrapResult != null, failure = failure?.ToString(),
            samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure), lockAvailable,
            activeAfter = original!.IsActive, ambientClosed = original.AmbientRegistration == null, mainClosed = original.MainAdmission == null,
            contextClosed = original.ExternalPublicationContext == null, generationBefore, generationAfter, beforeFiles, atIntentFiles, afterFiles, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext);
        Assert.Equal(generationBefore, generationAfter);
        if (uncertain)
        {
            Assert.Null(boolResult); Assert.Null(bootstrapResult); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls); Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]); cut.AssertReachedAndStopped(); Assert.Equal(0, cut.ClosingLeases);
            Assert.NotNull(atIntentFiles); Assert.Equal(atIntentFiles.Keys.Order(StringComparer.Ordinal), afterFiles.Keys.Order(StringComparer.Ordinal));
            foreach (var path in atIntentFiles.Keys.Where(path => path != target)) Assert.Equal(atIntentFiles[path], afterFiles[path]);
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls); Assert.False(File.Exists(cut.JournalPath));
            if (mode == "owner_resource")
            {
                Assert.True(boolResult);
                foreach (var pair in ownerPlan!.OwnerAfterImages) Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)));
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ownerPlan.StateAfterImage!.ToCanonicalJson()), await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ownerPlan.HistoryAfterImage!.ToCanonicalJson()), await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
                var currentSoul = await context.ReadJsonAsync("game_state/meta/soul_state.json"); Assert.Equal(5, currentSoul!["inkFeathers"]!["current"]!.GetValue<int>());
            }
            else if (mode == "blessing_bootstrap")
            {
                Assert.NotNull(bootstrapResult); Assert.True(bootstrapResult.Success); Assert.True(bootstrapResult.StateChanged);
                var selection = await ShiningBlessingEffectState.ReadPendingMemorySelectionAsync(files); Assert.NotNull(selection); Assert.Equal(1, selection.Options); Assert.Equal(1, selection.Rerolls);
                var currentSoul = await context.ReadJsonAsync("game_state/meta/soul_state.json"); Assert.IsType<JsonObject>(currentSoul![ShiningBlessingEffectState.SoulStateProperty]!["memorySelection"]!["rerollResourceBinding"]);
            }
            else if (mode == "memory_selection")
            {
                Assert.True(boolResult);
                var currentSoul = await context.ReadJsonAsync("game_state/meta/soul_state.json"); var selection = currentSoul![ShiningBlessingEffectState.SoulStateProperty]!["memorySelection"]!;
                Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, selection["status"]!.GetValue<string>());
                Assert.Equal(3, selection["selectedLifeIncarnation"]!.GetValue<int>()); Assert.Equal("Current memory fixture", selection["selectedLifeSummary"]!.GetValue<string>());
                var allocation = await ShiningBlessingRerollResourceService.ReadAllocationAsync(files, selection.AsObject());
                output.WriteLine(JsonSerializer.Serialize(new { mode, KnownAllocationValid = allocation.IsValid, allocation.AllocationId, allocation.Remaining }));
                Assert.True(allocation.IsValid, string.Join(";", allocation.Issues)); Assert.Equal(0, allocation.Remaining);
                Assert.Null(await ShiningBlessingEffectState.ReadPendingMemorySelectionAsync(files));
            }
            else
            {
                var schedule = await context.ReadJsonAsync(ProgressionScheduleService.SchedulePath);
                Assert.Equal(1, schedule!["currentChaosSeaTurnOrdinal"]!.GetValue<int>()); Assert.Equal(1, schedule["lastChaosSeaSimulationOrdinal"]!.GetValue<int>());
                Assert.Equal(1, schedule["lastGuardianProjectCycleOrdinal"]!.GetValue<int>()); Assert.Equal(1, schedule["lastResidentAgencyCycleOrdinal"]!.GetValue<int>());
                Assert.False(File.Exists(files.ResolvePath(ProgressionScheduleService.ReportPath)));
            }
            Assert.Empty(await context.Validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync());
        }
    }
}
