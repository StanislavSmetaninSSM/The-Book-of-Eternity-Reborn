using System.Reflection;
using System.Runtime.ExceptionServices;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.UI;
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
    [InlineData("guardian_correction", false)] [InlineData("guardian_correction", true)]
    [InlineData("spirit_focus_training", false)] [InlineData("spirit_focus_training", true)]
    [InlineData("spiritual_upgrade", false)] [InlineData("spiritual_upgrade", true)]
    [InlineData("qte_accept", false)] [InlineData("qte_accept", true)]
    public async Task OriginalInteractionOwnersRetainGenuinePublicationUncertaintyOnClose(string mode, bool uncertain)
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
        var settings = new GameSettings { MusicEnabled = false, SoundEnabled = false, GmBridgeAutoStart = false, ImageProvider = "off" };
        var manager = new StateManager(files, settings, NullLogger<StateManager>.Instance);
        GuardianCorrectionService? guardianCorrection = null; TrainingService? training = null;
        ExplorerMode? explorer = null; MethodInfo? upgradeMethod = null; object? currency = null;
        JsonObject? expectedSoul = null; JsonObject? projectedSoul = null;
        QteSceneService? qte = null; QteSceneService.QteOffer? offer = null;
        if (mode == "guardian_correction")
        {
            await files.WriteFileAtomicAsync(ScenarioCoreService.ManifestPath, """
                { "scenarioCoreAssertions":[{"assertionId":"core_role","category":"role_status","value":"Current king role","explicit":true,"source":"structured_field"}],
                  "candidateAssertions":[], "openCorrectionSlots":[{"slotId":"slot_protection","slotType":"protection_or_omen","maxSeverity":"medium","allowsFriendly":true,"allowsHostile":true,"sourceAssertionId":"core_role"}] }
                """);
            var guardian = new JsonObject
            {
                ["guardianId"] = "guard_original_owner", ["canonicalName"] = "Owner Guardian", ["nameVariants"] = new JsonObject { ["default"] = "Owner Guardian" },
                ["manifestation"] = new JsonObject { ["currentDisplayName"] = "Owner Guardian", ["formFlexibility"] = "selective", ["currentPresentationStyle"] = "feminine", ["currentPronouns"] = "she", ["appearanceDescription"] = "Current fixture form." },
                ["manifestationHistory"] = new JsonArray(), ["relationshipData"] = new JsonObject { ["currentReputation"] = 95, ["reputationHistory"] = new JsonArray(), ["lastInteraction"] = null },
                ["abodePower"] = new JsonObject { ["currentPower"] = 80, ["tier"] = "Сияющая", ["lastUpdatedAt"] = "2026-10-09T00:00:00Z", ["history"] = new JsonArray() },
                ["guardianRelationships"] = new JsonArray(), ["gachaSystem"] = new JsonObject { ["currentReturnCycleId"] = "chaos_return_original_owner", ["gachaHistory"] = new JsonArray() }
            };
            var guardians = new JsonObject { ["guardians"] = new JsonArray(guardian), ["activeGuardian"] = guardian.DeepClone() };
            var profiles = new JsonObject { [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", "guard_original_owner", "Chaos Sea", materializedAtTurn: 1)) };
            await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(files, new AfterlifeOwnerResourceAcceptedState(Profiles: profiles, Guardians: guardians));
            guardianCorrection = new GuardianCorrectionService(files, new ScenarioCoreService(files, NullLogger<ScenarioCoreService>.Instance), NullLogger<GuardianCorrectionService>.Instance);
        }
        else if (mode is "spirit_focus_training" or "spiritual_upgrade")
        {
            var soul = JsonNode.Parse("""
                { "soulName":"Current upgrade soul", "currentRealm":"Chaos Sea", "currentIncarnation":2,
                  "inkFeathers":{"current":2500,"total":2500},
                  "afterlifeCombatProfile":{"enlightenmentRank":3,"radianceRank":0,"retainedRadianceRank":0,"spiritFocusTier":1,"artTiers":{"spiritual_resilience":0,"spiritual_healing":0},"specialArts":[]} }
                """)!.AsObject();
            var profiles = new JsonObject { [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(new JsonObject { ["actorType"] = "player_soul", ["actorId"] = "player_soul", ["displayName"] = "Current upgrade soul", ["realm"] = "Chaos Sea" }) };
            await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(files, new AfterlifeOwnerResourceAcceptedState(Profiles: profiles, SoulState: soul, SpiritualConflict: AfterlifeSpiritualConflictState.CreateDefaultRoot()));
            expectedSoul = JsonNode.Parse((await files.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
            projectedSoul = expectedSoul.DeepClone().AsObject(); projectedSoul["afterlifeCombatProfile"]!["spiritFocusTier"] = 2; projectedSoul["inkFeathers"]!["current"] = 1600;
            training = new TrainingService(files, NullLogger<TrainingService>.Instance);
            explorer = new ExplorerMode(manager, files, new LocalizationManager());
            upgradeMethod = typeof(ExplorerMode).GetMethod("SaveSpiritualArtUpgradeRootsUnderLeaseAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            currency = Enum.Parse(typeof(ExplorerMode).GetNestedType("SpiritualArtCurrency", BindingFlags.NonPublic)!, "InkFeathers");
        }
        else
        {
            await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(files);
            qte = new QteSceneService(files, settings, null!, null!, null!, null!, null!, null!, manager, NullLogger<QteSceneService>.Instance);
            offer = new QteSceneService.QteOffer { QteId = "qte_original_owner", SourceTurnNumber = 12, StartChapterId = "first", Chapters = [new QteSceneService.QteChapter { ChapterId = "first", Narrative = "Current deterministic chapter." }] };
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
            "guardian_correction" => "GuardianCorrectionService+<ApplyForNewLifeAsync>",
            "spirit_focus_training" => "TrainingService+<TryCommitSpiritFocusTrainingAsync>",
            "spiritual_upgrade" => "ExplorerMode+<SaveSpiritualArtUpgradeRootsUnderLeaseAsync>",
            _ => "QteSceneService+<BeginAcceptedSceneCoreAsync>"
        };
        Task? operation = null; Exception? failure = null; bool? boolResult = null;
        TrainingService.TrainingOperationResult? trainingResult = null; QteSceneService.QteRuntimeState? qteResult = null;
        Dictionary<string, byte[]>? atIntentFiles = null; string? actualOwnerState = null;
        try
        {
            launchArmed = true;
            operation = mode switch
            {
                "guardian_correction" => guardianCorrection!.ApplyForNewLifeAsync(2, 42),
                "spirit_focus_training" => training!.BuyTrainingAsync("self", "self_spirit_focus_tier_2", 22),
                "spiritual_upgrade" => (Task<bool>)upgradeMethod!.Invoke(explorer, [expectedSoul, null, null, projectedSoul, null, null, currency])!,
                _ => qte!.BeginAcceptedSceneAsync(offer!, 12)
            };
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            atIntentFiles = ReadCanonicalFiles();
            (original, actualOwnerState) = InspectOriginalNestedOwningLease(operation, boundary);
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe; cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
            if (failure == null && mode == "spiritual_upgrade") boolResult = await Assert.IsAssignableFrom<Task<bool>>(operation);
            if (failure == null && mode == "spirit_focus_training") trainingResult = await Assert.IsAssignableFrom<Task<TrainingService.TrainingOperationResult>>(operation);
            if (failure == null && mode == "qte_accept") qteResult = await Assert.IsAssignableFrom<Task<QteSceneService.QteRuntimeState>>(operation);
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
            attachments, closer.Calls, boolResult, trainingResultPresent = trainingResult != null, qteResultPresent = qteResult != null, failure = failure?.ToString(),
            samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure), lockAvailable,
            activeAfter = original!.IsActive, ambientClosed = original.AmbientRegistration == null, mainClosed = original.MainAdmission == null,
            contextClosed = original.ExternalPublicationContext == null, generationBefore, generationAfter, beforeFiles, atIntentFiles, afterFiles, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext);
        Assert.Equal(generationBefore, generationAfter);
        if (uncertain)
        {
            Assert.Null(boolResult); Assert.Null(trainingResult); Assert.Null(qteResult); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls); Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]); cut.AssertReachedAndStopped(); Assert.Equal(0, cut.ClosingLeases);
            Assert.NotNull(atIntentFiles); Assert.Equal(atIntentFiles.Keys.Where(path => path != target).Order(StringComparer.Ordinal), afterFiles.Keys.Where(path => path != target).Order(StringComparer.Ordinal));
            foreach (var path in atIntentFiles.Keys.Where(path => path != target)) Assert.Equal(atIntentFiles[path], afterFiles[path]);
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls); Assert.False(File.Exists(cut.JournalPath));
            if (mode == "guardian_correction")
            {
                var receipt = await context.ReadJsonAsync(GuardianCorrectionService.StatePath);
                Assert.Equal(1, receipt!["schemaVersion"]!.GetValue<int>()); Assert.Equal(2, receipt["lifeIncarnation"]!.GetValue<int>());
                Assert.Equal("guard_original_owner", receipt["guardianId"]!.GetValue<string>());
                Assert.False(string.IsNullOrWhiteSpace(receipt["receiptFingerprint"]!.GetValue<string>()));
                Assert.False(string.IsNullOrWhiteSpace(receipt["transactionAfterImageFingerprint"]!.GetValue<string>()));
                Assert.NotEmpty(receipt["transactionAfterImagePaths"]!.AsArray());
                Assert.NotEmpty(receipt["corrections"]!.AsArray());
                var powerAfter = receipt["powerAfter"]!.GetValue<int>(); Assert.True(powerAfter < receipt["powerBefore"]!.GetValue<int>());
                var guardians = await context.ReadJsonAsync("game_state/meta/guardians.json");
                Assert.Equal(powerAfter, guardians!["activeGuardian"]!["abodePower"]!["currentPower"]!.GetValue<int>());
                output.WriteLine(JsonSerializer.Serialize(new { mode, KnownCorrections = receipt["corrections"]!.AsArray().Count, PowerAfter = powerAfter }));
            }
            else if (mode is "spirit_focus_training" or "spiritual_upgrade")
            {
                if (mode == "spirit_focus_training") { Assert.NotNull(trainingResult); Assert.True(trainingResult.Success, trainingResult.Message); Assert.True(trainingResult.StateChanged); }
                else Assert.True(boolResult);
                var currentSoul = await context.ReadJsonAsync("game_state/meta/soul_state.json");
                Assert.Equal(2, currentSoul!["afterlifeCombatProfile"]!["spiritFocusTier"]!.GetValue<int>()); Assert.Equal(1600, currentSoul["inkFeathers"]!["current"]!.GetValue<int>());
                if (mode == "spirit_focus_training")
                {
                    var purchase = Assert.Single(currentSoul[TrainingRequestState.AfterlifePurchaseReceiptsProperty]!.AsArray());
                    Assert.Equal("self_spirit_focus_tier_2", purchase["offerId"]!.GetValue<string>());
                    Assert.Equal("self", purchase["sourceActorId"]!.GetValue<string>()); Assert.Equal("self_fallback", purchase["sourceActorKind"]!.GetValue<string>());
                    Assert.Equal(22, purchase["createdAtTurn"]!.GetValue<int>()); Assert.Equal(900, purchase["inkFeathersSpent"]!.GetValue<int>());
                    output.WriteLine(JsonSerializer.Serialize(new { mode, KnownTrainingReceipt = purchase }));
                }
                var definitions = ResourceDefinitionCatalog.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false).Catalog!;
                var state = ResourceStateContract.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions, allowMissingPristine: false); Assert.True(state.IsValid);
                var points = Assert.Single(state.Ledger!.Entries, entry => entry.Coordinate.Realm == "chaos_sea" && entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor && entry.Coordinate.ResourceOwnerId == "player_soul" && entry.Coordinate.ResourceKey == "spiritual_action_points");
                Assert.Equal(7m, points.Current); Assert.Equal(AfterlifeSpiritualConflictState.GetSpiritFocusMaxActionPoints(2), points.Maximum);
                var history = ResourceHistoryState.ParseCanonical(await files.ReadFileAsync(ResourceMaterializationContract.HistoryPath), definitions, allowMissingPristine: false); Assert.True(history.IsValid);
                Assert.Contains(history.History!.Transitions, transition => transition.Coordinate == points.Coordinate && transition.Operation == ResourceTransitionOperation.Reconfigure && (mode == "spiritual_upgrade" || transition.Turn == 22));
            }
            else
            {
                Assert.NotNull(qteResult); Assert.NotNull(qteResult.ActiveScene); Assert.Equal("qte_original_owner", qteResult.ActiveScene.Offer.QteId);
                Assert.Equal(12, qteResult.ActiveScene.AcceptedAtTurn); Assert.Equal("first", qteResult.ActiveScene.CurrentChapterId);
                var runtime = await context.ReadJsonAsync(QteSceneService.QteRuntimePath); var continuation = await context.ReadJsonAsync(QteDeferredEffectContinuation.StatePath);
                Assert.Equal(qteResult.ActiveScene.DeferredEffectContinuationId, runtime!["activeScene"]!["deferredEffectContinuationId"]!.GetValue<string>());
                Assert.Equal(qteResult.ActiveScene.DeferredEffectContinuationId, continuation!["continuationId"]!.GetValue<string>());
                Assert.Equal(qteResult.ActiveScene.DeferredEffectContinuationFingerprint, continuation["authorityFingerprint"]!.GetValue<string>());
            }
            Assert.Empty(await context.Validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync());
        }
    }
}
