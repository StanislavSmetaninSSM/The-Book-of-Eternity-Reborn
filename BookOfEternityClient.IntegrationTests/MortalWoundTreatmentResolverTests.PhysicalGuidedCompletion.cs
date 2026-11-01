using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public Task GuidedPhysicalPublicationCompletesThroughActualFrozenAndCurrentSettlement(string mode) =>
        AssertGuidedPhysicalCompletionAsync(mode, fate: false);

    [Fact]
    public Task GuidedPhysicalFatePublicationKeepsItsActualReactionAndCurrentSettlement() =>
        AssertGuidedPhysicalCompletionAsync("procedure", fate: true);

    [Fact]
    public Task GuidedTerminalReductionAndIndependentLegaciesKeepClosedCurrentSettlement() =>
        AssertGuidedPhysicalCompletionAsync("guaranteed", fate: false, terminalVariant: "reduce_legacy");

    [Fact]
    public Task GuidedTerminalComplicationRemovalKeepsClosedCurrentSettlement() =>
        AssertGuidedPhysicalCompletionAsync("guaranteed", fate: false, terminalVariant: "remove");

    private async Task AssertGuidedPhysicalCompletionAsync(string mode, bool fate, string? terminalVariant = null)
    {
        var resourceCost = mode == "guaranteed" && terminalVariant != "remove";
        OperationTimingAccumulator? timings = null;
        OperationTimingAccumulator.Window? window = null;
        LifecyclePassiveTimingDiagnostics? capture = null;
        if (terminalVariant is null)
        {
            await using var untimedFixture = await CreateFixtureAsync();
            await CoreAsync(untimedFixture);
            return;
        }

        var diagnosticRoot = Path.GetFullPath(TestDiagnosticPaths.Root);
        var repo = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (diagnosticRoot.Equals(repo, comparison) || diagnosticRoot.StartsWith(repo + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Native physical capture requires the existing external diagnostic root.");
        timings = new OperationTimingAccumulator();
        window = timings.StartWindow(captureEvents: true);
        capture = new LifecyclePassiveTimingDiagnostics("NativePhysical-" + terminalVariant, output, gzipRecords: true);
        DiagnosisOwnershipFixture? fixture = null;
        Exception? primary = null;
        try
        {
            fixture = await CreateFixtureAsync();
            await CoreAsync(fixture);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            if (fixture is not null)
                using (timings.Measure("NativePhysical.FixtureClosing"))
                {
                    try { await fixture.DisposeAsync(); }
                    catch (Exception error) { primary ??= error; }
                }
            // CoreAsync has closed its transaction and every functional scope;
            // the actual fixture has also attempted both lease and root closing.
            await capture.CompleteAsync(primary);
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();

        Task<DiagnosisOwnershipFixture> CreateFixtureAsync() => DiagnosisOwnershipFixture.CreateAsync(fate ? 1 : 20, signedInput: true,
            physicalMode: mode, seedFate: fate, resourceCost: resourceCost,
            hooks: timings is null ? null : new FileSystemManagerHooks { PassiveOperationTimings = timings },
            timingCapture: capture, timingWindow: window,
            preparePhysicalWound: terminalVariant is null ? null : wound => PrepareTerminalSettlementWound(wound, terminalVariant),
            beforeResourceBootstrap: terminalVariant == "reduce_legacy" ? PrepareThreeStepTerminalProvider : null,
            originalProducer: async (files, preparedRequest) =>
            {
                var actualHint = Assert.IsType<JsonObject>(preparedRequest.MortalTreatment);
                var copyBefore = actualHint.DeepClone();
                await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
                {
                    SessionId = preparedRequest.SessionId, RequestId = preparedRequest.RequestId, TurnNumber = preparedRequest.TurnNumber,
                    CurrentRealm = "Mortal World", PlayerAction = "Выполнить отдельно подтверждённое лечение.",
                    PreGeneratedDices1d20 = preparedRequest.PreGeneratedDices1d20, ProgressionControl = preparedRequest.ProgressionControl,
                    MortalTreatment = actualHint
                });
                actualHint["foreignCallerMutation"] = true;
                var written = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(LiveTurnPreparationService.TurnRequestPath)))!.AsObject();
                Assert.True(JsonNode.DeepEquals(copyBefore, written["mortalTreatment"]));
            });

        async Task CoreAsync(DiagnosisOwnershipFixture fixture)
        {
            using var admissionTiming = timings?.Measure("NativePhysical.Admission");
            var fs = fixture.Files;
            var input = fixture.Authority.ReadGuidedTreatmentOriginalInput();
            var selected = MortalWoundGuidedTreatmentInputCodec.Read(input);
            var requestJson = Assert.IsType<string>(await fs.ReadFileAsync(fixture.Lease, LiveTurnPreparationService.TurnRequestPath));
            var original = JsonSerializer.Deserialize<TurnRequest>(requestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            Assert.True(JsonNode.DeepEquals(original.MortalTreatment, MortalWoundGuidedTreatmentInputCodec.CreateGmContext(input)));
            var manifestJson = Assert.IsType<string>(await fs.ReadFileAsync(fixture.Lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            var manifest = JsonSerializer.Deserialize<GameEngine.PendingTurnSnapshotManifest>(manifestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            var authorityJson = Assert.IsType<string>(await fs.ReadFileAsync(fixture.Lease, PendingTurnSnapshotAuthority.AuthorityPath));
            var observed = PendingTurnSnapshotReader.ReadCurrent(fs, fixture.Lease,
                new[] { AcceptedMechanicsPlan.WoundCommandPath, ProgressionScheduleService.SchedulePath });
            Assert.True(observed.Success, DescribeIssues(observed.Issues));
            var signedInput = observed.Snapshot!.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath);
            var token = manifest.ManifestPayloadHash; var generation = fixture.Authority.SessionGeneration;
            var scheduleHash = manifest.SnapshotFileHashes[ProgressionScheduleService.SchedulePath];
            var admission = await WoundApplicationService.ResolveMortalTreatmentInputAsync(fs, fixture.Lease, selected.Frame);
            var request = admission.Request;
            var serializedRequest = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request)!.ToJsonString();
            var history = new WoundHistoryParseResult(admission.Owner.History, []);
            var reduced = mode switch
            {
                "procedure" => MortalWoundTreatmentPlanner.CreateProcedureAttempt(request, history, admission.Owner.CurrentWound, admission.Owner),
                "course" => MortalWoundTreatmentPlanner.CreateCourseMilestoneAttempt(request, history, admission.Owner.CurrentWound, admission.Owner),
                _ => MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, admission.Owner.CurrentWound, admission.Owner)
            };
            Assert.Equal("Resolved", reduced.Disposition);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(reduced.Resolution);
            Assert.Equal(fate, resolution.CriticalReactionIntent is not null);
            var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
            Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease));
            Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
            var response = resourceCost ? new GameResponse
            { RemoveActiveSkills = Array.Empty<string>(), NPCInventoryUpdates = Array.Empty<JsonElement>() } : new GameResponse();
            admissionTiming?.Dispose();
            WoundAcceptedTurnPlanner.MortalWoundTreatmentPublicationResult composed;
            using (timings?.Measure("NativePhysical.InitialCompose"))
            {
                using var context = timings is null ? null : fs.EnterOperationTimingContext();
                composed = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(fs, fixture.Lease,
                    response, admission.Owner, request, resolution);
            }
            Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
            var publication = Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(composed.Plan!.TreatmentResourcePublicationAuthority);
            var semantic = publication.SemanticFingerprint;
            var identitySeed = publication.IdentitySeed;
            var plan = composed.Plan;
            if (terminalVariant is not null)
                using (timings?.Measure("NativePhysical.TerminalAssertions"))
                {
                    using var context = fs.EnterOperationTimingContext();
                    AssertTerminalPublicationStages(plan, terminalVariant, timings);
                }
            using var normalizeTiming = timings?.Measure("NativePhysical.Normalize");
            await fixture.ReleaseLeaseAsync();
            var normalized = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(fs,
                new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance), validator,
                new Dictionary<string, string>(StringComparer.Ordinal));
            normalizeTiming?.Dispose();
            var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(normalized.TreatmentResourcePublicationTransaction);
            using (timings?.Measure("NativePhysical.Transaction"))
            {
                Exception? transactionPrimary = null;
                try
                {
                    Assert.DoesNotContain(normalized.Issues, issue => issue.Severity == IssueSeverity.Error);
                    var advanced = await transaction.AdvancePublishedAgreementWithProgressionAsync(fs,
                        new ProgressionScheduleService(fs, NullLogger<ProgressionScheduleService>.Instance), selected.Frame.Progression);
                    Assert.True(advanced.IsValid, DescribeIssues(advanced.Issues));
                    var completed = await transaction.CompleteAsync(fs);
                    Assert.True(completed.IsValid, DescribeIssues(completed.Issues));
                    Assert.Empty(completed.Issues); Assert.Equal(1, completed.ChangedCount);
                    Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Finalized, completed.Outcome);
                }
                catch (Exception error) when (capture is not null) { transactionPrimary = error; }
                finally
                {
                    try { await ((IAsyncDisposable)transaction).DisposeAsync(); }
                    catch (Exception error) when (capture is not null)
                    {
                        capture.RecordClosingFailure(error, "native-physical-transaction-close");
                        if (transactionPrimary is null) transactionPrimary = error;
                        else transactionPrimary.Data["NativePhysical.TransactionClosingFailure"] = error;
                    }
                }
                if (transactionPrimary is not null) ExceptionDispatchInfo.Capture(transactionPrimary).Throw();
            }
            using var settlementTiming = timings?.Measure("NativePhysical.CurrentSettlement");
            if (terminalVariant is not null)
            {
                var output = GameEngine.BindAcceptedWoundOutput(plan!, "Лекарь завершает назначенную процедуру.");
                Assert.True(output.Success, DescribeIssues(output.Issues));
                Assert.Contains(output.Notifications, notice => notice.Text.PlainText.StartsWith("Исцелена рана:", StringComparison.Ordinal));
                Assert.All(output.Notifications, notice => Assert.Equal("/раны", notice.DetailCommand));
            }
            await fixture.AcquireLeaseAsync();
            var unchanged = fixture.CaptureJson();
            var current = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            async Task<byte[]?> Read(string path)
            {
                counts[path] = counts.GetValueOrDefault(path) + 1;
                var bytes = await fs.ReadFileBytesAsync(fixture.Lease, path); current[path] = bytes?.ToArray(); return bytes;
            }
            var result = await MortalWoundTreatmentCompletion.RequirePublishedAsync(original, token, generation,
                selected.Selection, input, scheduleHash, Read);
            Assert.All(counts.Values, count => Assert.Equal(1, count));
            Assert.True(result.HasGuidedPublicationWitness);
            var exact = result.ReconstructGuidedPublication();
            Assert.Equal(semantic, exact.SemanticFingerprint);
            Assert.Equal(identitySeed, MortalWoundTreatmentResourcePublicationAuthority.ComputeIdentitySeed(
                request.Coordinates.AcceptedStateFingerprint, request.RequestFingerprint, resolution.ResultFingerprint,
                exact.Finalization.FinalizationFingerprint, semantic));
            Assert.Equal(serializedRequest, WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(result.Request)!.ToJsonString());
            Assert.Equal(observed.Snapshot.ReadRequiredBytes(ProgressionScheduleService.SchedulePath), result.ReadGuidedOriginalScheduleBytes());
            if (resourceCost) Assert.Equal(2, exact.Finalization.Consumptions.Count);
            if (mode == "procedure" && !fate && terminalVariant is null) Assert.NotNull(exact.Preparation.SeverityReduction);

            // Detached delivery to the actual shared Finish/classifier predicate;
            // this is not a fabricated accepted browser receipt or a terminal claim.
            var binding = new PendingPlayerActionService.Binding(original.RequestId, generation, original.PlayerAction,
                "browser-wound-action", selected.Selection.ToJson().ToJsonString(), selected.Selection);
            var staged = new PendingPlayerActionService.Staged(binding, requestJson, manifestJson, authorityJson,
                string.Empty, string.Empty, Convert.ToBase64String(signedInput));
            counts.Clear();
            await GameEngine.RequireAcceptedSpiritualResultAsync(staged, manifest, Read);
            Assert.All(counts.Values, count => Assert.Equal(1, count));
            fixture.AssertJson(unchanged);

            async Task Refuse(string label, Action<Dictionary<string, byte[]?>> fault)
            {
                var map = current.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);
                fault(map);
                var failure = await Assert.ThrowsAsync<InvalidDataException>(() => MortalWoundTreatmentCompletion.RequirePublishedAsync(
                    original, token, generation, selected.Selection, input, scheduleHash, path => Task.FromResult(map[path])));
                Assert.False(string.IsNullOrWhiteSpace(failure.Message), label);
                fixture.AssertJson(unchanged);
            }
            void ChangeResult(Dictionary<string, byte[]?> map, Action<JsonObject> fault) => Change(map, WoundHistoryState.HistoryPath, root =>
            {
                var row = root["transitions"]!.AsArray().OfType<JsonObject>().Single(entry => entry["transitionResult"]?["schemaVersion"]?.GetValue<int>() == 2);
                var resultRoot = row["transitionResult"]!.AsObject(); fault(resultRoot);
                if (resultRoot.ContainsKey("schemaVersion")) resultRoot["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(resultRoot);
            });
            await Refuse("missing v2 witness", map => ChangeResult(map, value => value.Remove("publicationInputs")));
            await Refuse("malformed witness", map => ChangeResult(map, value => value["publicationInputs"]!["itemCommands"]!["UpdateInventory"] = "foreign"));
            await Refuse("unknown version", map => ChangeResult(map, value => value["schemaVersion"] = 3));
            await Refuse("historical receipt is not publication proof", map => Change(map, WoundHistoryState.HistoryPath, root =>
            {
                var row = root["transitions"]!.AsArray().OfType<JsonObject>().Single(value => value["transitionResult"]?["schemaVersion"]?.GetValue<int>() == 2);
                row["transitionResult"] = row["transitionResult"]!["terminal"]!.DeepClone();
                Assert.True(WoundHistoryState.Parse(root.ToJsonString(), WoundHistoryState.HistoryPath).IsValid);
            }));
            await Refuse("resealed foreign original schedule", map => ChangeResult(map, value =>
            {
                var bytes = Convert.FromBase64String(value["originalScheduleBase64"]!.GetValue<string>());
                var schedule = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes))!.AsObject();
                schedule["lastUpdatedUtc"] = DateTime.UnixEpoch.ToString("O");
                value["originalScheduleBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(schedule.ToJsonString()));
            }));
            await Refuse("complete schedule other-field drift", map => Change(map, ProgressionScheduleService.SchedulePath,
                value => value["chaosSeaCycleEquivalentHours"] = 999));
            await Refuse("current item drift", map => Change(map, NpcCoreChangesContract.NpcCorePath,
                value => value["NPCsInScene"]!.AsArray()[0]!["inventory"]!.AsArray()[0]!["count"] = 999));
            if (terminalVariant is null)
                await Refuse("current wound drift", map => Change(map, WoundCarrierCatalog.PlayerPath,
                    value => value["activeWounds"]!.AsArray()[0]!["display"]!["description"] =
                        value["activeWounds"]!.AsArray()[0]!["display"]!["description"]!.GetValue<string>() + " foreign"));
            else await AssertTerminalSettlementFaultsAsync(current, result, Refuse, async map =>
            {
                var valid = await MortalWoundTreatmentCompletion.RequirePublishedAsync(original, token, generation,
                    selected.Selection, input, scheduleHash, path => Task.FromResult(map[path]));
                Assert.Equal(result.Request.RequestFingerprint, valid.Request.RequestFingerprint);
                fixture.AssertJson(unchanged);
            });
            foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                         ResourceMaterializationContract.CommandPath, EffectAcceptedTurnPlan.CommandPath, ProgressionScheduleService.ReportPath })
                await Refuse("reintroduced work " + path, map => map[path] = Encoding.UTF8.GetBytes("{}"));
            if (resourceCost)
            {
                await Refuse("resealed semantic input drift", map => ChangeResult(map,
                    value => value["publicationInputs"]!["skillCommands"]!.AsObject().Remove("removeActiveSkills")));
                await Refuse("missing spend", map => Change(map, ResourceMaterializationContract.HistoryPath, value =>
                {
                    var rows = value["entries"]!.AsArray(); rows.Remove(rows.First(row => row!["originKind"]!.GetValue<string>() == "registered_system_outcome"));
                }));
            }
            await Assert.ThrowsAsync<InvalidDataException>(() => MortalWoundTreatmentCompletion.RequirePublishedAsync(original,
                "foreign_snapshot", generation, selected.Selection, input, scheduleHash, path => Task.FromResult(current[path])));
            await Assert.ThrowsAsync<InvalidDataException>(() => MortalWoundTreatmentCompletion.RequirePublishedAsync(original,
                token, "foreign_generation", selected.Selection, input, scheduleHash, path => Task.FromResult(current[path])));
            var foreign = JsonSerializer.Deserialize<TurnRequest>(requestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            foreign.MortalTreatment!["inputFingerprint"] = "foreign";
            await Assert.ThrowsAsync<InvalidDataException>(() => MortalWoundTreatmentCompletion.RequirePublishedAsync(foreign,
                token, generation, selected.Selection, input, scheduleHash, path => Task.FromResult(current[path])));
            fixture.AssertJson(unchanged);
        }
    }
}
