using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact]
    public Task ConsoleNegotiatedFactionOfferUsesActualGrantCleanupAndConfirmedTreatment() => RunNegotiatedJourneyAsync(false, true);
    [Fact]
    public Task BrowserNegotiatedFactionOfferUsesActualGrantCleanupAndConfirmedTreatment() => RunNegotiatedJourneyAsync(true, true);
    [Fact]
    public Task ConsoleNegotiatedElyaraOfferUsesActualGrantCleanupAndConfirmedTreatment() => RunNegotiatedJourneyAsync(false, false);
    [Fact]
    public Task BrowserNegotiatedElyaraOfferUsesActualGrantCleanupAndConfirmedTreatment() => RunNegotiatedJourneyAsync(true, false);
    [Fact]
    public Task NegotiatedFactionPublicationFailureRestoresAcceptedGrantWalletAndMemory() => RunNegotiatedJourneyAsync(false, true, true);

    private async Task RunNegotiatedJourneyAsync(bool browser, bool faction, bool failPublication = false)
    {
        var diagnosticRoot = Path.GetFullPath(TestDiagnosticPaths.Root);
        var repo = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (diagnosticRoot.Equals(repo, comparison) || diagnosticRoot.StartsWith(repo + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Negotiated treatment capture requires a diagnostic root outside checkout.");
        var capture = new LifecyclePassiveTimingDiagnostics("Negotiated-" + (faction ? "faction-" : "elyara-") +
            (failPublication ? "rollback" : browser ? "browser" : "console"), output, gzipRecords: true);
        var timings = new OperationTimingAccumulator();
        var window = timings.StartWindow(captureEvents: true);
        IDisposable? journey = null; IDisposable? preparation = null;
        Exception? primary = null;
        try { await CoreAsync(); }
        catch (Exception error) { primary = error; }
        finally
        {
            preparation?.Dispose(); journey?.Dispose();
            await capture.CompleteAsync(primary);
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();

        async Task CoreAsync()
        {
            FileSystemManager? owned = null; var armed = false; var cuts = 0;
            var reads = new CanonicalReadActivityAccumulator();
            var hooks = new FileSystemManagerHooks
            {
                PassiveCanonicalReads = reads,
                LocalPublicationObserver = !failPublication ? null : (phase, _) =>
                {
                    if (!armed || phase != TrustedLocalPublicationPhase.MemberPublished ||
                        File.Exists(owned!.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath))) return;
                    armed = false; cuts++;
                    throw new InvalidOperationException("Negotiated attempt C4 cut after original command consumption.");
                }
            };
            void Allocated(string actualRoot)
            {
                capture.BindFixtureRoot(actualRoot); capture.Arm(window);
                journey = timings.Measure("Negotiated.JourneyIncludingClosing");
                preparation = timings.Measure("Negotiated.FixturePreparation");
            }
            HealingFixture fixture;
            try
            {
                fixture = faction
                    ? await AfterlifeHealingPaymentLifecycleTests.CreateFactionServiceFixtureAsync(3, 14, hooks, engine: true,
                        allocatedRootForDiagnostics: Allocated, additionalBeforeResourceBootstrap: async (files, lease, profiles) =>
                        {
                            profiles["profiles"]!.AsArray().OfType<JsonObject>()
                                .Single(p => p["actorId"]!.GetValue<string>() == AfterlifeHealingPaymentLifecycleTests.FactionHealerId)
                                .Remove("healingServiceProfile");
                            await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
                        })
                    : await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(3, 14, hooks, engine: true,
                        realm: "Shining Abode", allocatedRootForDiagnostics: Allocated,
                        additionalBeforeResourceBootstrap: async (files, profiles) =>
                        {
                            profiles["profiles"]!.AsArray().OfType<JsonObject>()
                                .Single(p => p["actorId"]!.GetValue<string>() == AfterlifeWoundTreatmentAuthority.ElyaraId)
                                ["healingServiceProfile"]!["availability"] = "unavailable";
                            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                            await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
                            // Count real opens, as in the existing public-service
                            // comparison: an absent optional roster has no open event.
                            var residents = new JsonObject();
                            GuardianAbodeResidentState.NormalizeShape(residents);
                            await files.WriteFileAtomicAsync(lease, GuardianAbodeResidentState.StatePath, residents.ToJsonString());
                        });
            }
            finally { preparation?.Dispose(); preparation = null; }
            Exception? fixtureFailure = null;
            try { await WithFixtureAsync(fixture); }
            catch (Exception error) { fixtureFailure = error; }
            finally
            {
                using var closing = timings.Measure("Negotiated.FixtureClosing");
                try { await fixture.DisposeAsync(); }
                catch (Exception error) { fixtureFailure ??= error; capture.RecordClosingFailure(error, "negotiated-fixture-close"); }
            }
            if (fixtureFailure is not null) ExceptionDispatchInfo.Capture(fixtureFailure).Throw();

            async Task WithFixtureAsync(HealingFixture activeFixture)
            {
                var files = activeFixture.Files; owned = files;
                var providerId = faction ? AfterlifeHealingPaymentLifecycleTests.FactionHealerId : AfterlifeWoundTreatmentAuthority.ElyaraId;
                var providerName = faction ? AfterlifeHealingPaymentLifecycleTests.FactionHealerName : "Элиара Последней Раны";
                var terms = faction ? "Перенести архив целителя после принятой попытки лечения." : "Передать свидетельство о пережитой духовной ране.";
                var kind = faction ? "favor" : "debt"; var family = faction ? 6 : 4; var tier = faction ? 3 : 5;
                var original = Assert.Single((await activeFixture.ReadWoundsAsync()).Occurrences).Wound;
                WoundMaterializationEnvelope currentWound = original;
                JsonObject? proposal = null; JsonObject? retained = null; TurnRequest? treatmentTurn = null;
                var offeredTurn = false; var treatmentWritten = false;
                var console = new TestExplorerConsole();
                var diagnostics = new GameEngineTurnLifecycleTests.SpiritualLifecycleTestLogger(capture);
                var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, inputSource: new NoPlayerInput(diagnostics),
                    logger: diagnostics, explorerConsole: console, finalizationHooks: new GameEngineSessionFinalizationHooks
                    {
                        AtCheckpointAsync = async checkpoint =>
                        {
                            if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
                            using var responseScope = timings.Measure("Negotiated.GmResponse");
                            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                            var turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath))!,
                                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                            if (turn.TurnNumber == 43 && !offeredTurn)
                            {
                                Assert.Equal("natural", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                                Assert.Equal(0, Assert.Single(turn.SpiritualHealing["cohort"]!.AsArray())!["healingTier"]!.GetValue<int>());
                                var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
                                var actor = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]!.GetValue<string>() == providerId);
                                var shining = JsonNode.Parse((await files.ReadFileAsync(lease, ShiningAbodeState.StatePath))!)!.AsObject();
                                var hallId = shining["currentHallId"]!.GetValue<string>();
                                proposal = faction ? AfterlifeSpiritualNaturalRecoveryTests.ResidentOfferProposal()
                                    : AfterlifeSpiritualNaturalRecoveryTests.GuardianOfferProposal();
                                proposal["woundId"] = currentWound.WoundId;
                                proposal["readableEvidence"] = "Я предлагаю помощь на прямо оговорённых условиях; отдельное решение души обязательно.";
                                proposal["compensation"] = new JsonObject { ["kind"] = kind, ["agreementRef"] = "agreement_negotiated_journey", ["readableTerms"] = terms };
                                if (faction)
                                {
                                    var roster = JsonNode.Parse((await files.ReadFileAsync(lease, GuardianAbodeResidentState.StatePath))!)!;
                                    var member = roster["entries"]!.AsArray().OfType<JsonObject>().Single(r => r["residentId"]!.GetValue<string>() == providerId);
                                    proposal["locality"] = SpiritualTreatmentParticipation.LocalityJson(SpiritualAidLocality.ForFaction(member["shiningFactionId"]!.GetValue<string>(), hallId));
                                }
                                else proposal["locality"] = SpiritualTreatmentParticipation.LocalityJson(SpiritualAidLocality.ForElyara(hallId));
                                actor[SpiritualTreatmentParticipation.ChangeProperty] = proposal.DeepClone();
                                await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
                                offeredTurn = true;
                            }
                            else
                            {
                                Assert.Equal(44, turn.TurnNumber); Assert.False(treatmentWritten);
                                Assert.Equal("aid", turn.SpiritualHealing!["mode"]!.GetValue<string>());
                                Assert.Equal(tier, turn.SpiritualHealing["healingTier"]!.GetValue<int>());
                                Assert.Equal(0, turn.SpiritualHealing["targetNaturalHealingTier"]!.GetValue<int>());
                                retained = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                                Assert.Equal(family, retained["schemaVersion"]!.GetValue<int>());
                                Assert.Equal("player_target_confirmed", retained["playerConfirmation"]!.GetValue<string>());
                                Assert.Equal(0, retained["activeSource"]!["complicationModifier"]!.GetValue<long>());
                                Assert.True(JsonNode.DeepEquals(proposal!["compensation"], retained["participation"]!["record"]!["compensation"]));
                                Assert.True(JsonNode.DeepEquals(proposal["locality"], turn.SpiritualHealing["locality"]));
                                var soul = JsonNode.Parse((await files.ReadFileAsync(lease, AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject();
                                SpiritualTreatmentWalletInvariant.Parse(retained["currencyInvariant"]).RequirePreserved(soul);
                                treatmentTurn = turn; treatmentWritten = true;
                            }
                            await WriteNativeCompletionAsync(files, lease, turn,
                                AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(currentWound, turn.SpiritualHealing!["destinationGraphs"]!.AsArray()),
                                turn.TurnNumber == 43 ? "Целитель предлагает согласованную помощь; душа ещё не принимает лечение."
                                    : "Душа отдельно принимает договорную помощь; клиент рассчитывает настоящий исход и обычный цикл без списания Перьев.");
                            await WriteNegotiatedReasoningAsync(files, lease, turn, providerId, providerName, faction, terms);
                            armed = failPublication && turn.TurnNumber == 44;
                        }
                    });
                try
                {
                    runtime.GameLoop.SetSession("session_negotiated_" + (faction ? "faction_" : "elyara_") + (browser ? "browser" : "console"), 42);
                    using (timings.Measure("Negotiated.Refresh")) await runtime.StateManager.RefreshGameStateAsync();
                    var initialSoul = JsonNode.Parse((await files.ReadFileAsync(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject();
                    var wallet = SpiritualTreatmentWalletInvariant.Capture(initialSoul);
                    using (timings.Measure("Negotiated.OfferTurn"))
                        await runtime.Engine.ProcessPlayerTurn("Спросить целителя об указанной договорной помощи; лечение я ещё не принимаю.");
                    Assert.True(offeredTurn); Assert.Equal(43, runtime.GameLoop.TurnNumber);
                    currentWound = Assert.Single((await activeFixture.ReadWoundsAsync()).Occurrences).Wound;
                    Assert.Equal(3, currentWound.Severity.Rank); Assert.Equal(1, currentWound.Recovery.CurrentStepProgress);
                    await AssertNegotiatedCleanupAsync(files);
                    var grant = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single((await activeFixture.ReadHistoryAsync()).Transitions,
                        t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
                    Assert.True(grant.IsNatural); Assert.Equal(family, grant.SchemaVersion);
                    Assert.True(JsonNode.DeepEquals(proposal!["compensation"], grant.ParticipationOutcomes![0]!["record"]!["compensation"]));
                    wallet.RequirePreserved(JsonNode.Parse((await files.ReadFileAsync(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject());
                    var grantGuardianJournal = JsonNode.Parse((await files.ReadFileAsync(GuardianThoughtJournalState.StatePath))!)!["entries"]!.AsArray();
                    var grantGuardianMemory = Assert.Single(grantGuardianJournal)!.DeepClone();
                    Assert.Equal(43, grantGuardianMemory["turn"]!.GetValue<int>());
                    Assert.Equal(AfterlifeWoundTreatmentAuthority.ElyaraId, grantGuardianMemory["guardianId"]!.GetValue<string>());
                    var before = activeFixture.CaptureMutationRoots(new[] { AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath,
                        GuardianThoughtJournalState.StatePath, GuardianAbodeResidentState.StatePath, ShiningAbodeState.StatePath,
                        EffectAcceptedTurnInputComposer.WorldTimePath, PendingTurnStateService.PendingDiceStatePath });
                    string? action = null; PendingPlayerActionService.Binding? queuedBinding = null;
                    using (timings.Measure("Negotiated.MenuAndConfirmation"))
                    {
                        var localityPaths = new[] { AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0], GuardianAbodeResidentState.StatePath, ShiningAbodeState.StatePath };
                        var readCounts = localityPaths.Select(reads.ReadCount).ToArray();
                        var opened = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
                        for (var i = 0; i < localityPaths.Length; i++) Assert.Equal(1L, reads.ReadCount(localityPaths[i]) - readCounts[i]);
                        var choice = Assert.Single(opened.WoundCommandState!.Choices, c => c.Available && c.Selection.Aid?.Compensation is not null);
                        Assert.False(choice.Selection.Aid!.PlayerConfirmed); Assert.Contains(terms, choice.Reason);
                        Assert.DoesNotContain(opened.WoundCommandState.Choices, c => c.Available && c.Selection.Service?.Quote.Provider.OwnerId == providerId);
                        activeFixture.AssertMutationRootsUnchanged(before);
                        if (browser)
                        {
                            var service = CreateCommands(files);
                            var cancel = await service.ExecuteAsync(new("/лечить", Owner));
                            await service.CancelPromptSessionAsync(new(cancel.InteractiveSession!.SessionId, Owner));
                            activeFixture.AssertMutationRootsUnchanged(before);
                            var promptResult = await service.ExecuteAsync(new("/лечить", Owner));
                            Assert.DoesNotContain(currentWound.WoundId, JsonSerializer.Serialize(promptResult));
                            Assert.DoesNotContain(providerId, JsonSerializer.Serialize(promptResult));
                            var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(promptResult.Prompts, p => p.Id == "wound_selection"));
                            var option = Assert.Single(prompt.Options, o => !o.Disabled && o.Label.Contains(providerName, StringComparison.Ordinal) && o.Description.Contains(terms, StringComparison.Ordinal));
                            var submitted = await service.SubmitPromptSessionAsync(new(promptResult.InteractiveSession!.SessionId,
                                new() { ["wound_selection"] = JsonValue.Create(option.Value), ["confirm_wound_treatment"] = JsonValue.Create(true) }, Owner));
                            Assert.DoesNotContain(submitted.Notifications, n => n.Severity == UiNotificationSeverity.Error);
                            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                            queuedBinding = Assert.IsType<PendingPlayerActionService.State>(await PendingPlayerActionService.ReadAsync(files, lease)).Binding;
                            Assert.True(queuedBinding.SpiritualSelfTreatment!.Aid!.PlayerConfirmed);
                            Assert.Equal(choice.Selection.Aid.Compensation, queuedBinding.SpiritualSelfTreatment.Aid.Compensation);
                        }
                        else
                        {
                            console.QueueAnySelection("Отмена");
                            Assert.Equal(string.Empty, await runtime.Explorer.TryProcessCommand("/лечить"));
                            activeFixture.AssertMutationRootsUnchanged(before); Assert.Empty(console.ConfirmPrompts);
                            var available = opened.WoundCommandState.Choices.Where(c => c.Available).ToArray();
                            console.QueueAnySelection((Array.IndexOf(available, choice) + 1) + ". " + choice.Name + " — " + choice.Severity);
                            console.QueueAnyConfirmResponse(true);
                            action = await runtime.Explorer.TryProcessCommand("/лечить");
                            Assert.Equal(WoundApplicationService.ActionFor(choice.Selection), action);
                            Assert.Contains(terms, Assert.Single(console.ConfirmPrompts));
                        }
                        activeFixture.AssertMutationRootsUnchanged(before);
                    }
                    using (timings.Measure("Negotiated.TreatmentTurn"))
                        await runtime.Engine.ProcessPlayerTurn(queuedBinding?.Action ?? action!, browserAction: queuedBinding);
                    using var assertions = timings.Measure("Negotiated.Assertions");
                    Assert.True(treatmentWritten); Assert.NotNull(retained);
                    if (failPublication)
                    {
                        Assert.Equal(1, cuts); Assert.Equal(43, runtime.GameLoop.TurnNumber);
                        activeFixture.AssertMutationRootsUnchanged(before);
                        Assert.DoesNotContain((await activeFixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
                        await AssertNegotiatedCleanupAsync(files);
                        activeFixture.AssertResourcesUnchanged();
                        return;
                    }
                    Assert.Equal(44, runtime.GameLoop.TurnNumber);
                    var history = await activeFixture.ReadHistoryAsync();
                    var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions, t => t.AttemptId is not null).TransitionResult);
                    Assert.Equal("aid", result.Mode); Assert.Equal(family, result.SchemaVersion);
                    Assert.True(JsonNode.DeepEquals(retained, result.ToCanonicalJson()["request"]));
                    var values = retained!["attempt"]!["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray();
                    var die = retained["attempt"]!["roll"]!["mode"]!.GetValue<string>() == "disadvantage" ? values.Min() : values.Max();
                    var calculation = SpiritualHealingOutcomeMath.Calculate(new(tier, currentWound.Severity.Rank, die, 0, 0))!;
                    var expected = SpiritualWoundRecoveryPlanner.Build(currentWound, 0, retained["attempt"]!["attemptId"]!.GetValue<string>(),
                        retained["attempt"]!["cycleKey"]!.GetValue<string>(), calculation);
                    Assert.Equal(expected.Select(v => (v.Kind, v.SeverityRank, v.Progress)),
                        result.Stages.Select(v => (v.Kind, v.After.Severity.Rank, v.After.Recovery.CurrentStepProgress)));
                    var after = result.Stages[^1].After;
                    if (after.Lifecycle == "healed") Assert.Empty((await activeFixture.ReadWoundsAsync()).Occurrences);
                    else Assert.Equal(WoundMaterializationContract.SerializeCanonical(after), WoundMaterializationContract.SerializeCanonical(
                        Assert.Single((await activeFixture.ReadWoundsAsync()).Occurrences).Wound));
                    Assert.Equal(2, history.Transitions.Count(t => t.TransitionResult is SpiritualHealingPersistedResult));
                    var schedule = JsonNode.Parse((await files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
                    var grantOrdinal = grant.ToCanonicalJson()["control"]!["nextChaosSeaTurnOrdinal"]!.GetValue<int>();
                    Assert.Equal(grantOrdinal + 1, treatmentTurn!.ProgressionControl!.NextChaosSeaTurnOrdinal);
                    Assert.Equal(treatmentTurn.ProgressionControl.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
                    wallet.RequirePreserved(JsonNode.Parse((await files.ReadFileAsync(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject());
                    await AssertNegotiatedCleanupAsync(files);
                    var settled = activeFixture.CaptureMutationRoots(new[] { AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath,
                        GuardianThoughtJournalState.StatePath, GuardianAbodeResidentState.StatePath, ShiningAbodeState.StatePath });
                    await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                        Assert.NotNull(await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(treatmentTurn!.ProgressionControl!,
                            treatmentTurn.SessionId, treatmentTurn.RequestId, treatmentTurn.TurnNumber, p => files.ReadFileBytesAsync(lease, p)));
                    activeFixture.AssertMutationRootsUnchanged(settled);
                    var guardianJournal = JsonNode.Parse((await files.ReadFileAsync(GuardianThoughtJournalState.StatePath))!)!.AsObject();
                    Assert.False(guardianJournal.ContainsKey(GuardianThoughtJournalState.UpdateProperty));
                    var guardianMemories = guardianJournal["entries"]!.AsArray();
                    Assert.Equal(new[] { 43, 44 }, guardianMemories.Select(e => e!["turn"]!.GetValue<int>()));
                    Assert.True(JsonNode.DeepEquals(grantGuardianMemory, guardianMemories[0]));
                    Assert.All(guardianMemories, e => Assert.Equal(AfterlifeWoundTreatmentAuthority.ElyaraId, e!["guardianId"]!.GetValue<string>()));
                    if (faction)
                    {
                        var journal = JsonNode.Parse((await files.ReadFileAsync(GuardianAbodeResidentState.StatePath))!)!["thoughtJournal"]!.AsArray();
                        Assert.Equal(new[] { 43, 44 }, journal.Select(e => e!["turn"]!.GetValue<int>()));
                        Assert.All(journal, e => Assert.Equal(providerId, e!["residentId"]!.GetValue<string>()));
                    }
                    activeFixture.AssertResourcesUnchanged();
                    output.WriteLine($"Negotiated {kind}/{family}: real Engine offer43/cleanup/confirmed44, {after.Lifecycle}/{after.Severity.Rank}/{after.Recovery.CurrentStepProgress}; wallet unchanged, accepted terms/draw/history and current inert replay.");
                }
                catch (Exception error) { fixtureFailure ??= error; }
                finally
                {
                    armed = false;
                    try { capture.WriteLine(diagnostics.Describe()); }
                    catch (Exception error) { fixtureFailure ??= error; capture.RecordClosingFailure(error, "negotiated-describe-forward"); }
                    try
                    {
                        using (timings.Measure("Negotiated.AudioDisposal")) await runtime.AudioService.DisposeAsync();
                    }
                    catch (Exception error) { fixtureFailure ??= error; capture.RecordClosingFailure(error, "negotiated-audio-close"); }
                }
            }
        }
    }

    private static async Task AssertNegotiatedCleanupAsync(FileSystemManager files)
    {
        foreach (var path in new[] { LiveTurnPreparationService.TurnRequestPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath, "ready/turn_complete.json", "ready/turn_error.json", ProgressionScheduleService.ReportPath,
            AcceptedMechanicsPlan.WoundCommandPath, PendingPlayerActionService.PendingPath })
            Assert.Null(await files.ReadFileBytesAsync(path));
    }

    private static async Task WriteNegotiatedReasoningAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        TurnRequest turn, string providerId, string providerName, bool faction, string terms)
    {
        const string elyara = "Элиара Последней Раны";
        var timestamp = DateTime.UtcNow.ToString("o"); var offer = turn.TurnNumber == 43;
        var thought = offer ? "Я предлагаю договорную помощь; душа ещё не принимает лечение." : "Я оказываю отдельно подтверждённую договорную помощь; исход рассчитывает клиент.";
        var consequence = offer ? "Запись предложения не создаёт нового обязательства души." : "Согласованные условия относятся к принятой попытке, включая неудачу; Перья не списываются.";
        JsonObject Memory(string id, string summary) => new()
        {
            ["entryId"] = "negotiated_thought_" + id + "_" + turn.RequestId, ["turn"] = turn.TurnNumber, ["timestamp"] = timestamp,
            ["title"] = offer ? "Предложение помощи" : "Принятая договорная попытка", ["summary"] = summary,
            ["eventType"] = "conversation", ["consequence"] = consequence, ["attitude"] = "attentive",
            ["intent"] = "Сохранить отдельный выбор души и точные согласованные условия.", ["tags"] = new JsonArray()
        };
        var guardianMemory = Memory(AfterlifeWoundTreatmentAuthority.ElyaraId, faction
            ? "Я остаюсь в текущем зале, продолжаю обычную стратегию и не заменяю выбранного фракционного целителя." : thought);
        guardianMemory["guardianId"] = AfterlifeWoundTreatmentAuthority.ElyaraId;
        var guardians = JsonNode.Parse((await files.ReadFileAsync(lease, GuardianThoughtJournalState.StatePath)) ?? "{\"entries\":[]}")!.AsObject();
        guardians[GuardianThoughtJournalState.UpdateProperty] = new JsonArray(guardianMemory);
        await files.WriteFileAtomicAsync(lease, GuardianThoughtJournalState.StatePath, guardians.ToJsonString());
        if (faction)
        {
            var response = JsonNode.Parse((await files.ReadFileAsync(lease, "output/interface_updates.json"))!)!.AsObject();
            var memory = Memory(providerId, thought); memory["residentId"] = providerId;
            response[SpiritualTreatmentParticipation.ResidentThoughtResponseProperty] = new JsonArray(memory);
            await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", response.ToJsonString());
        }
        string Brain(string name, bool provider) => string.Join("\n", "", "### " + name,
            "- Current location: Зал сопровождения; остаюсь в текущем каноническом зале.",
            "- Situation: " + (provider ? thought : "Душа обсуждает или принимает помощь другого целителя; обычное развитие Хранителя продолжается."),
            "- Profile inputs: " + (provider ? faction ? "Живой видимый вознесённый healing_support, ИсцелениеIII, точное членство текущей фракции; публичного денежного профиля нет." : "Единственная живая видимая Элиара, ИсцелениеV, канонический профиль100%, публичный приём сейчас недоступен; " +
                    (offer ? "частное предложение только выдвинуто, подтверждения души ещё нет." : "частное предложение явно принято отдельно.") : "Живая видимая Элиара в текущем зале; действующая обычная стратегия, в этой попытке не является целителем."),
            "- Thoughts: " + (provider ? thought : "Я сохраняю самостоятельный выбор души и продолжаю свою обычную стратегию."),
            "- Motivation: Поддержать целостность души без навязанного решения.",
            "- Constraints: " + terms + " Клиент владеет броском, раной, кошельком и циклом; предложение не заменяет подтверждение.",
            "- Strategy options:", "1. Сохранить явные условия и выбор души. Benefit: осознанное лечение. Risk: отказ или неудача.",
            "2. Навязать другое действие. Benefit: контроль. Risk: нарушение согласия.",
            "- Chosen strategy: Сохранить точные условия и самостоятельное подтверждение души.",
            "- Rejected alternatives: Нет основания менять условия, роль, зал или результат.",
            "- Actions: " + (provider ? thought : "Я продолжаю обычную стратегию; развитие рассчитывает клиент."),
            "- State changes: " + (provider && faction ? "spiritualResidentThoughtJournalUpdates добавляет мою точную first-person память; roster нормализует тот же владелец." : "guardianThoughtJournalUpdates добавляет мою first-person память.") +
                " Клиент публикует исход, effects/History, один цикл и автоматическое развитие; ГМ не задаёт доход или wallet.");
        var text = string.Join("\n", "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: " + elyara + (faction ? ", " + providerName : ""),
            "- Why relevant: Целитель участвует в договорной помощи; обычный цикл затрагивает развитие объявленных участников.",
            "- Actors outside scope: нет", "- Why outside scope: Другие акторы не меняются.", "", "## Reasoning", Brain(elyara, !faction));
        if (faction) text += Brain(providerName, true);
        await files.WriteFileAtomicAsync(lease, "output/debug_logs.json", new JsonObject { ["timestamp"] = timestamp, ["gm_thoughts_markdown"] = text }.ToJsonString());
    }
}
