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

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public Task GuaranteedTreatment_AppliedBeforeStoryFreshProcessCompletesOnceAndRepeatsInertly() =>
        RunJointCompletionControlAsync(nonzeroProgression: false, pauseAppliedForCold: true);

    [Fact]
    public Task GuaranteedTreatment_UnappliedCommonFreshProcessAppliesPhaseAndCompletesExactlyOnce() =>
        RunJointCompletionControlAsync(nonzeroProgression: false, pauseUnappliedForCold: true);

    [Fact]
    public Task GuaranteedTreatment_NonzeroReportConsumedBeforeAppliedColdCompletesWithoutRecreation() =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true);

    private static async Task AssertJointColdSignedStoryInventoryAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, LiveTurnPendingSnapshotManifest manifest, byte[] originalStoryPrefix)
    {
        // ReadCurrent already authenticated this exact manifest under this lease.
        // Histories have signed TEXT hashes, not canonical snapshot-file leaves.
        var histories = Assert.IsType<Dictionary<string, string>>(manifest.ClientOwnedValidationHashes);
        Assert.Contains("game_state/history/chat_log.json", histories.Keys, StringComparer.Ordinal);
        Assert.Contains(QteStoryPath, histories.Keys, StringComparer.Ordinal);
        var signedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in histories)
        {
            Assert.True(signedNames.Add(pair.Key), "ambiguous signed history: " + pair.Key);
            Assert.True(pair.Key == "game_state/history/chat_log.json" ||
                (pair.Key.StartsWith("stories/", StringComparison.OrdinalIgnoreCase) && pair.Key.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)));
            Assert.DoesNotContain('\\', pair.Key);
            Assert.DoesNotContain(pair.Key.Split('/'), part => part is "" or "." or "..");
            Assert.NotNull(pair.Value);
            Assert.True(pair.Value.Length == 0 || (pair.Value.Length == 64 && pair.Value.All(Uri.IsHexDigit)));
            if (string.Equals(pair.Key, QteStoryPath, StringComparison.OrdinalIgnoreCase)) Assert.Equal(QteStoryPath, pair.Key);
            var image = pair.Key == QteStoryPath ? originalStoryPrefix : await files.ReadFileBytesAsync(lease, pair.Key);
            if (pair.Value.Length == 0) Assert.Null(image);
            else
            {
                Assert.NotNull(image);
                Assert.Equal(pair.Value.ToUpperInvariant(), PendingTurnSnapshotAuthority.ComputeSha256(
                    CanonicalJsonUtf8.DecodeOneOptionalBom(image!)).ToUpperInvariant());
            }
        }
        var actualNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in GameEngine.SelectOriginalStoryContinuityFiles(files.EnumerateFiles(lease, "*")))
        {
            Assert.True(actualNames.Add(name), "ambiguous physical story: " + name);
            Assert.Contains(name, histories.Keys, StringComparer.Ordinal);
        }
        foreach (var pair in histories.Where(row => row.Key != "game_state/history/chat_log.json" && row.Value.Length != 0))
            Assert.Contains(pair.Key, actualNames);
    }

    private async Task RunJointAppliedBeforeStoryColdAsync(GameEngine parent, PendingTurnSnapshotReadAuthority original,
        string requestFingerprint, IReadOnlyDictionary<string, byte[]?> originalBefore, string scene, string expectedPhase = "applied",
        string? refusalScenario = null, bool newPhysicalRoot = false, bool recoverRetainedStoryRow = false)
    {
        var originalRequest = StrictJsonAuthority.Deserialize<TurnRequest>(CanonicalJsonUtf8.DecodeOneOptionalBom(originalBefore["input/turn_request.json"]!),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual cold parent original control");
        Assert.NotNull(originalRequest); Assert.NotNull(originalRequest!.ProgressionControl);
        var expectedWorldCycles = originalRequest.ProgressionControl!.WorldCyclesAlreadyPendingBeforeTurn;
        var generationRefusal = refusalScenario is "generation_missing" or "generation_foreign";
        var malformedManifestAdmissionRefusal = refusalScenario == "manifest_malformed";
        var phaseFaultControl = refusalScenario is "phase_known_compensation" or "phase_uncertain_debt";
        var storyControl = refusalScenario?.StartsWith("story_", StringComparison.Ordinal) == true;
        var childRoot = newPhysicalRoot ? Path.Combine(Path.GetTempPath(), "boe-fr063-joint-cold-" + Guid.NewGuid().ToString("N")) : _fs.BasePath;
        using var targetOwned = newPhysicalRoot ? new CleanupOwnedFixture(childRoot, Console.WriteLine) : null;
        JointNewRootTransportEvidence? transport = null;
        Dictionary<string, byte[]?> paused;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            paused = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
            Assert.Equal(originalBefore["$sessionGeneration"], paused["$sessionGeneration"]);
            var marker = ParseJsonObjectBytes(Assert.IsType<byte[]>(paused[MortalQteDecisionPublication.StatePath]));
            Assert.Equal(2, marker["schemaVersion"]!.GetValue<int>());
            Assert.Equal(expectedPhase, marker["progression"]!["phase"]!.GetValue<string>());
            if (expectedPhase == "applied")
            {
                Assert.Null(await _fs.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath));
                if (expectedWorldCycles > 0)
                {
                    var realReport = Assert.IsType<byte[]>(originalBefore[ProgressionScheduleService.ReportPath]);
                    Assert.Equal(realReport, Convert.FromBase64String(marker["progression"]!["reportBefore"]!["bytes"]!.GetValue<string>()));
                    Assert.False(marker["progression"]!["reportAfter"]!["exists"]!.GetValue<bool>());
                }
            }
            Assert.Equal(original.RequestId, marker["requestId"]!.GetValue<string>());
            Assert.Equal(originalBefore[QteStoryPath], paused[QteStoryPath]);
            foreach (var path in new[] { "input/turn_request.json", LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                         PendingTurnSnapshotAuthority.AuthorityPath, "ready/turn_complete.json" })
                Assert.Equal(originalBefore[path], paused[path]);
            Assert.Equal(42, GetPrivateField<GameLoop>(parent, "_gameLoop").TurnNumber);
            Assert.False(GetPrivateFieldValue<bool>(parent, "_inGame"));
            lease.EnsureNoPendingLocalDecision();
            if (newPhysicalRoot)
            {
                Assert.Equal("applied", expectedPhase); Assert.Null(refusalScenario); Assert.Equal(1, expectedWorldCycles);
                transport = await CopyQuiescentJointNamespaceAsync(_fs, lease, childRoot, paused);
            }
        } // No parent input/read/write lease or participating caller survives launch.
        byte[]? acceptedPublicDraft = null;
        if (refusalScenario?.StartsWith("draft_", StringComparison.Ordinal) == true)
        {
            var acceptedDraft = ParseJsonObjectBytes(originalBefore[MortalWoundOpportunityAdapter.DecisionPath]!);
            acceptedDraft["proposal"]!["severity"] = "II";
            acceptedPublicDraft = Encoding.UTF8.GetBytes(acceptedDraft.ToJsonString());
        }
        if (refusalScenario == "draft_exact")
        {
            await DamageJointOutputBeforeChildLaunchAsync(_fs, refusalScenario, acceptedPublicDraft);
            refusalScenario = null;
            await using var exactDraftLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            paused = await CaptureConfirmedClaimReadImagesAsync(_fs, exactDraftLease);
            exactDraftLease.EnsureNoPendingLocalDecision();
        }
        var initialOutputRefusal = IsInitialJointOutputRefusal(refusalScenario);
        if (initialOutputRefusal)
        {
            await DamageJointOutputBeforeChildLaunchAsync(_fs, refusalScenario!, acceptedPublicDraft);
            paused = await CaptureJointGenerationRefusalRawImagesAsync(_fs);
        }
        var context = new JsonObject
        {
            ["parentProcessId"] = Environment.ProcessId, ["sessionId"] = original.SessionId,
            ["requestId"] = original.RequestId, ["snapshotToken"] = original.SnapshotToken,
            ["requestFingerprint"] = requestFingerprint, ["scene"] = scene, ["expectedPhase"] = expectedPhase, ["expectedWorldCycles"] = expectedWorldCycles,
            ["refusalScenario"] = refusalScenario,
            ["newPhysicalRoot"] = newPhysicalRoot, ["sourceRoot"] = _fs.BasePath, ["expectedRoot"] = childRoot,
            ["generationSha256"] = MortalArchiveHash(paused["$sessionGeneration"]!),
            ["originalStorySha256"] = MortalArchiveHash(Assert.IsType<byte[]>(originalBefore[QteStoryPath])),
            ["originalStoryLength"] = originalBefore[QteStoryPath]!.Length,
            ["binaryPins"] = JsonSerializer.SerializeToNode(MortalArchiveBinaryPins()),
            ["pausedImages"] = JsonSerializer.SerializeToNode(paused.ToDictionary(pair => pair.Key,
                pair => pair.Value is null ? null : MortalArchiveHash(pair.Value), StringComparer.Ordinal))
        };
        // Context contains only assertion scalars/digests, never private authority,
        // complete request graphs, original bytes, cached plans or phase capability.
        var contextPath = Path.Combine(_rootPath, "joint-applied-cold-context.json");
        var output = Path.Combine(_rootPath, "joint-applied-cold-result.json");
        await File.WriteAllTextAsync(contextPath, context.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        Exception? failure = null;
        try
        {
            failure = await Record.ExceptionAsync(() => RunMortalArchiveChildAsync(
                "engine-mortal-joint-applied-cold", childRoot, output, initialOutputRefusal ? 9 : storyControl ? 7 : refusalScenario == "phase_known_compensation" ? 5
                    : refusalScenario == "phase_uncertain_debt" ? 6 : refusalScenario == "generation_foreign" ? 4
                    : generationRefusal ? 3 : refusalScenario is not null ? 2 : expectedPhase == "applied" ? 0 : 1, contextPath));
        }
        finally
        {
            // Retain all available managed output/full drained log before the
            // parent SOURCE root's ordinary owned cleanup, including timeout.
            WritePhysicalInjuryDiagnostic("fr063_joint_applied_cold_parent_outcome", new
            {
                ParentProcessId = Environment.ProcessId, Context = context, Failure = failure?.ToString(),
                Transport = transport,
                Child = File.Exists(output) ? await File.ReadAllTextAsync(output) : null,
                FullChildLog = File.Exists(output + ".log") ? await File.ReadAllTextAsync(output + ".log") : null,
                PhysicalAfter = MortalArchiveDigests(generationRefusal || malformedManifestAdmissionRefusal || phaseFaultControl || storyControl || initialOutputRefusal
                    ? await CaptureJointGenerationRefusalRawImagesAsync(_fs)
                    : await CaptureMortalArchiveImagesAsync(_fs, paused.Keys.Where(path => path != "$sessionGeneration"))),
                NewRootAfter = newPhysicalRoot && Directory.Exists(childRoot)
                    ? await CaptureJointNewRootDiagnosticAsync(childRoot) : null
            });
        }
        Assert.Null(failure);
        var child = ParseJsonObjectBytes(await File.ReadAllBytesAsync(output));
        Assert.NotEqual(Environment.ProcessId, child["processId"]!.GetValue<int>());
        Assert.True(child["actualOuterCallerInvoked"]!.GetValue<bool>());
        Assert.Equal(!generationRefusal && !malformedManifestAdmissionRefusal, child["actualCallerEntered"]!.GetValue<bool>());
        Assert.Equal(refusalScenario is null, child["completed"]!.GetValue<bool>());
        Assert.Equal(refusalScenario is not null, child["refusalVerified"]!.GetValue<bool>());
        Assert.Null(child["failure"]);
        if (recoverRetainedStoryRow)
        {
            Assert.Equal("story_readback_failure", refusalScenario);
            Assert.Equal(1, expectedWorldCycles); Assert.False(newPhysicalRoot);
            // First failure remains its own raw actual outcome. Its strict
            // physical oracle has already retained a real writer-created row.
            var evidence = Assert.IsType<JsonObject>(child["storyFaultEvidence"]);
            Assert.Equal(1, evidence["storyCommits"]!.GetValue<int>());
            Assert.Equal(1, evidence["readbackFaults"]!.GetValue<int>());
            var committedStory = Convert.FromBase64String(evidence["actualCommittedStory"]!.GetValue<string>());
            var retained = await CaptureJointGenerationRefusalRawImagesAsync(_fs);
            Assert.Equal(committedStory, retained[QteStoryPath]);
            Assert.False(retained.Keys.Any(path => path.StartsWith("$runtime/trusted-local-publication-v1/", StringComparison.Ordinal)));
            Assert.Equal(paused["$sessionGeneration"], retained["$sessionGeneration"]);
            foreach (var image in paused.Where(row => row.Key != QteStoryPath))
                Assert.Equal(image.Value, retained[image.Key]);
            var resumeContext = context.DeepClone().AsObject();
            resumeContext["refusalScenario"] = null;
            resumeContext["previousChildProcessId"] = child["processId"]!.GetValue<int>();
            resumeContext["expectedRetainedStorySha256"] = MortalArchiveHash(committedStory);
            resumeContext["pausedImages"] = JsonSerializer.SerializeToNode(retained
                .Where(row => !row.Key.StartsWith('$') || row.Key == "$sessionGeneration")
                .ToDictionary(row => row.Key, row => row.Value is null ? null : MortalArchiveHash(row.Value), StringComparer.Ordinal));
            var resumeContextPath = Path.Combine(_rootPath, "joint-retained-story-resume-context.json");
            var resumeOutput = Path.Combine(_rootPath, "joint-retained-story-resume-result.json");
            await File.WriteAllTextAsync(resumeContextPath, resumeContext.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            Exception? resumeFailure = null;
            try
            {
                resumeFailure = await Record.ExceptionAsync(() => RunMortalArchiveChildAsync(
                    "engine-mortal-joint-applied-cold", childRoot, resumeOutput, 8, resumeContextPath));
            }
            finally
            {
                WritePhysicalInjuryDiagnostic("fr063_joint_genuine_retained_story_second_process", new
                {
                    ParentProcessId = Environment.ProcessId, FirstActualFailure = child, ResumeContext = resumeContext,
                    Failure = resumeFailure?.ToString(), Before = retained,
                    ManagedResult = File.Exists(resumeOutput) ? await File.ReadAllTextAsync(resumeOutput) : null,
                    FullChildLog = File.Exists(resumeOutput + ".log") ? await File.ReadAllTextAsync(resumeOutput + ".log") : null,
                    RawAfter = await CaptureJointGenerationRefusalRawImagesAsync(_fs)
                });
            }
            Assert.Null(resumeFailure);
            var resumed = ParseJsonObjectBytes(await File.ReadAllBytesAsync(resumeOutput));
            Assert.NotEqual(Environment.ProcessId, resumed["processId"]!.GetValue<int>());
            Assert.NotEqual(child["processId"]!.GetValue<int>(), resumed["processId"]!.GetValue<int>());
            Assert.True(resumed["actualOuterCallerInvoked"]!.GetValue<bool>());
            Assert.True(resumed["actualCallerEntered"]!.GetValue<bool>());
            Assert.True(resumed["completed"]!.GetValue<bool>());
            Assert.False(resumed["refusalVerified"]!.GetValue<bool>()); Assert.Null(resumed["failure"]);
            Assert.Equal(0, resumed["retainedStoryAppendAttempts"]!.GetValue<int>());
        }
        if (newPhysicalRoot)
        {
            Assert.Equal(Path.GetFullPath(childRoot), child["root"]!.GetValue<string>());
            Assert.NotEqual(Path.GetFullPath(_fs.BasePath), child["root"]!.GetValue<string>());
            await using var sourceLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            await AssertConfirmedClaimReadImagesAsync(_fs, sourceLease, paused);
        }
    }

    public static async Task WriteMortalJointAppliedColdProbeAsync(string root, string output, int scenario, string contextPath)
    {
        Assert.InRange(scenario, 0, 9);
        var context = JsonNode.Parse(await File.ReadAllTextAsync(contextPath))!.AsObject();
        using var factory = new GameEngineTurnLifecycleTests();
        using var factoryOwned = new CleanupOwnedFixture(factory._rootPath, Console.WriteLine);
        var donorRoot = scenario == 4 ? Path.Combine(Path.GetTempPath(), "boe-fr063-joint-g-donor-" + Guid.NewGuid().ToString("N")) : null;
        using var donorOwned = donorRoot is null ? null : new CleanupOwnedFixture(donorRoot, Console.WriteLine);
        JointGenerationDonor? donor = null;
        Dictionary<string, byte[]?>? donorAfter = null;
        using var phaseProbe = scenario is 5 or 6 ? new JointPhaseFaultProbe(uncertain: scenario == 6) : null;
        var storyProbe = scenario == 7 ? new JointStoryControlProbe(context["refusalScenario"]!.GetValue<string>()) : null;
        var retainedStoryAppendAttempts = 0;
        var retainedStoryHooks = scenario == 8 ? new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (path == QteStoryPath)
                {
                    retainedStoryAppendAttempts++;
                    throw new InvalidDataException("A genuine retained joint row must not be appended again.");
                }
                return Task.CompletedTask;
            }
        } : null;
        var files = phaseProbe is null && storyProbe is null && retainedStoryHooks is null ? new FileSystemManager(root, NullLogger<FileSystemManager>.Instance)
            : new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                phaseProbe?.Hooks ?? storyProbe?.Hooks ?? retainedStoryHooks!);
        phaseProbe?.Attach(files);
        storyProbe?.Attach(files);
        var storyLogger = scenario == 7 ? new SpiritualLifecycleTestLogger() : null;
        var input = new QueuedConsoleInputSource([]);
        var actualCallerEntered = false;
        var engine = factory.CreateGameEngine(input, settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            fileSystem: files, finalizationHooks: new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = checkpoint =>
                {
                    if (checkpoint == SessionFinalizationCheckpoint.LateTerminalAndIdleOperationBound) actualCallerEntered = true;
                    return Task.CompletedTask;
                }
            }, logger: storyLogger);
        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_joint_applied_child_entry", Observation = new
        { ProcessId = Environment.ProcessId, ParentProcessId = context["parentProcessId"]!.GetValue<int>(),
            BorrowedRoot = root, AuxiliaryFactoryRoot = factory._rootPath, Arguments = Environment.GetCommandLineArgs(),
            DependencyContext = MortalArchiveDependencyContext(), LoadedDependencies = MortalArchiveLoadedDependencyBindings() } }));
        Exception? failure = null;
        bool? handled = null, secondIdle = null;
        var completed = false;
        var refusalVerified = false;
        var actualOuterCallerInvoked = false;
        Exception? generationAdmissionFailure = null;
        Exception? manifestAdmissionFailure = null;
        Exception? phaseOwningFailure = null;
        Exception? storyOwningFailure = null;
        string? actualGeneration = null;
        var refusalScenario = context["refusalScenario"]?.GetValue<string>();
        var sourceExpectedFrameClassification = refusalScenario switch
        {
            "manifest_missing" or "manifest_malformed" => "null: source loader cannot read the manifest; actual classification not measured",
            "input_missing" or "input_malformed" => "signed manifest remains independently frame-eligible; actual classification not measured",
            _ => "not asserted by fixture"
        };
        Dictionary<string, byte[]?>? intactBeforeTamper = null;
        Dictionary<string, byte[]?>? before = null, after = null, repeated = null;
        Dictionary<string, byte[]?>? retainedRawBefore = null, retainedRawAfter = null;
        Dictionary<string, byte[]?>? manifestAdmissionRawBefore = null, manifestAdmissionRawAfter = null;
        PendingTurnSnapshotReadAuthority? original = null;
        byte[]? storyBefore = null, originalStoryPrefix = null;
        try
        {
            Assert.NotEqual(Environment.ProcessId, context["parentProcessId"]!.GetValue<int>());
            if (scenario == 8) Assert.NotEqual(Environment.ProcessId, context["previousChildProcessId"]!.GetValue<int>());
            if (context["newPhysicalRoot"]?.GetValue<bool>() == true)
            {
                Assert.Equal(context["expectedRoot"]!.GetValue<string>(), files.BasePath);
                Assert.NotEqual(context["sourceRoot"]!.GetValue<string>(), files.BasePath);
                Assert.NotEqual(Path.GetFullPath(factory._rootPath), files.BasePath);
                Assert.NotSame(factory._fs.CanonicalRootAuthorityIdentity, files.CanonicalRootAuthorityIdentity);
            }
            AssertMortalArchiveChildBinaries(context);
            if (scenario == 9)
            {
                // Namespace was damaged in the PARENT before this process existed.
                // Only raw reads before the first owner: no successful authority,
                // output-binding, original, phase or state preflight in this child.
                before = await CaptureJointGenerationRefusalRawImagesAsync(files);
                foreach (var pair in context["pausedImages"]!.AsObject())
                    Assert.Equal(pair.Value?.GetValue<string>(), before.TryGetValue(pair.Key, out var image) && image is not null ? MortalArchiveHash(image) : null);
                GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(context["sessionId"]!.GetValue<string>(), 42);
                actualOuterCallerInvoked = true;
                var ownerFailure = await Record.ExceptionAsync(async () =>
                    handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
                after = await CaptureJointGenerationRefusalRawImagesAsync(files);
                Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_joint_output_initially_bad_first_owner", Observation = new
                { Environment.ProcessId, actualOuterCallerInvoked, actualCallerEntered, handled, Failure = ownerFailure?.ToString(), RawBefore = before, RawAfter = after } }));
                Assert.Null(ownerFailure);
                AssertJointGenerationRefusalRawImagesEqual(before, after);
                Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
                Assert.True(actualCallerEntered); Assert.True(handled);
                Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
                refusalVerified = true;
                return;
            }
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                before = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                foreach (var pair in context["pausedImages"]!.AsObject())
                    Assert.Equal(pair.Value?.GetValue<string>(), before.TryGetValue(pair.Key, out var image) && image is not null ? MortalArchiveHash(image) : null);
                Assert.Equal(context["generationSha256"]!.GetValue<string>(), MortalArchiveHash(files.ReadLocalGenerationSnapshot(lease).Bytes));
                if (scenario is 7 or 8) actualGeneration = files.ReadLocalGenerationSnapshot(lease).Binding.Id;
                var read = PendingTurnSnapshotReader.ReadCurrent(files, lease,
                    ["game_state/meta/soul_state.json", WoundHistoryState.HistoryPath]);
                Assert.True(read.Success, DescribeValidationIssues(read.Issues));
                original = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
                Assert.Equal(context["sessionId"]!.GetValue<string>(), original.SessionId);
                Assert.Equal(context["requestId"]!.GetValue<string>(), original.RequestId);
                Assert.Equal(context["snapshotToken"]!.GetValue<string>(), original.SnapshotToken);
                Assert.Equal(43, original.TurnNumber);
                var manifest = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(
                    CanonicalJsonUtf8.DecodeOneOptionalBom(Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease,
                        LiveTurnPreparationService.PendingTurnSnapshotManifestPath))),
                    LiveTurnPreparationService.ManifestHashJsonOptions, "cold actual authenticated manifest");
                Assert.NotNull(manifest);
                Assert.Equal(original.SessionId, manifest!.SessionId);
                Assert.Equal(original.RequestId, manifest.RequestId);
                Assert.Equal(original.SnapshotToken, manifest.ManifestPayloadHash);
                Assert.Equal(original.TurnNumber, manifest.TurnNumber);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(files, lease));
                var observedMarker = ParseJsonObjectBytes(Assert.IsType<byte[]>(before[MortalQteDecisionPublication.StatePath]));
                Assert.Equal(scenario is 1 or 5 or 6 ? "unapplied" : "applied", observedMarker["progression"]!["phase"]!.GetValue<string>());
                Assert.Equal(context["expectedPhase"]!.GetValue<string>(), observedMarker["progression"]!["phase"]!.GetValue<string>());
                var actualRequest = StrictJsonAuthority.Deserialize<TurnRequest>(CanonicalJsonUtf8.DecodeOneOptionalBom(before["input/turn_request.json"]!),
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "fresh child actual original progression control");
                Assert.NotNull(actualRequest); Assert.NotNull(actualRequest!.ProgressionControl);
                var cycles = context["expectedWorldCycles"]!.GetValue<int>();
                Assert.Equal(cycles, actualRequest.ProgressionControl!.WorldCyclesAlreadyPendingBeforeTurn);
                Assert.Equal(0, actualRequest.ProgressionControl.FactionCyclesAlreadyPendingBeforeTurn);
                if (scenario is not (1 or 5 or 6))
                {
                    Assert.Null(await files.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath));
                    if (cycles > 0)
                    {
                        Assert.True(observedMarker["progression"]!["reportBefore"]!["exists"]!.GetValue<bool>());
                        var frozenReport = ParseJsonObjectBytes(Convert.FromBase64String(observedMarker["progression"]!["reportBefore"]!["bytes"]!.GetValue<string>()))["progressionProcessingReport"]!;
                        Assert.Equal(original.SessionId, frozenReport["sessionId"]!.GetValue<string>());
                        Assert.Equal(original.RequestId, frozenReport["requestId"]!.GetValue<string>());
                        Assert.Equal(original.TurnNumber, frozenReport["turnNumber"]!.GetValue<int>());
                        Assert.Equal(cycles, frozenReport["worldCyclesProcessed"]!.GetValue<int>());
                        Assert.Equal(checked(actualRequest.ProgressionControl.LastWorldSimulationTimeInMinutes + cycles * actualRequest.ProgressionControl.WorldCycleMinutes),
                            frozenReport["newLastWorldSimulationTimeInMinutes"]!.GetValue<int>());
                        Assert.False(observedMarker["progression"]!["reportAfter"]!["exists"]!.GetValue<bool>());
                    }
                }
                if (scenario is 5 or 6)
                {
                    Assert.Equal(1, cycles);
                    Assert.True(observedMarker["progression"]!["reportBefore"]!["exists"]!.GetValue<bool>());
                    Assert.False(observedMarker["progression"]!["reportAfter"]!["exists"]!.GetValue<bool>());
                    Assert.Equal(Convert.FromBase64String(observedMarker["progression"]!["reportBefore"]!["bytes"]!.GetValue<string>()),
                        await files.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath));
                }
                storyBefore = await files.ReadFileBytesAsync(lease, QteStoryPath);
                var originalStoryLength = context["originalStoryLength"]!.GetValue<int>();
                Assert.NotNull(storyBefore);
                Assert.InRange(originalStoryLength, 1, storyBefore!.Length);
                originalStoryPrefix = storyBefore.AsSpan(0, originalStoryLength).ToArray();
                Assert.Equal(context["originalStorySha256"]!.GetValue<string>(), MortalArchiveHash(originalStoryPrefix));
                await AssertJointColdSignedStoryInventoryAsync(files, lease, manifest, originalStoryPrefix);
                var expectedStoryDerivedTurn = 42;
                if (scenario == 8)
                {
                    Assert.Equal(context["expectedRetainedStorySha256"]!.GetValue<string>(), MortalArchiveHash(storyBefore));
                    var actualSuffix = Encoding.UTF8.GetString(storyBefore.AsSpan(originalStoryLength));
                    var retainedRow = StrictJsonAuthority.Deserialize<StoryEntry>(
                        Assert.Single(actualSuffix.Split('\n', StringSplitOptions.RemoveEmptyEntries)),
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual retained writer row before fresh owner");
                    Assert.NotNull(retainedRow); Assert.Equal(43, retainedRow!.Turn);
                    expectedStoryDerivedTurn = retainedRow.Turn;
                    Assert.Equal(original.RequestId, retainedRow.RequestId); Assert.Equal(actualGeneration, retainedRow.SessionGeneration);
                    Assert.Equal(observedMarker["progression"]!["preparedAtUtc"]!.GetValue<string>(), retainedRow.Timestamp);
                    Assert.Equal("Осмотреть ушиб.", retainedRow.Player); Assert.Equal(context["scene"]!.GetValue<string>(), retainedRow.Narrative);
                }
                else Assert.Equal(originalStoryPrefix, storyBefore);
                await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateReadOnlyAsync(lease);
                Assert.Equal(expectedStoryDerivedTurn, GetPrivateField<StateManager>(engine, "_stateManager").CurrentState.TurnNumber);
                GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(original.SessionId, expectedStoryDerivedTurn);
                await AssertConfirmedClaimReadImagesAsync(files, lease, before);
                if (scenario == 2)
                {
                    // All official original/control/phase and fresh-cache prerequisites
                    // above precede the corruption. No authority helper runs afterward.
                    Assert.NotNull(refusalScenario); Assert.Equal(1, cycles);
                    intactBeforeTamper = before;
                    await ApplyJointCompletionRefusalTamperAsync(files, lease, refusalScenario!);
                    before = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                    Assert.Equal(intactBeforeTamper["$sessionGeneration"], before["$sessionGeneration"]);
                }
                lease.EnsureNoPendingLocalDecision();
            }
            if (scenario is 3 or 4)
            {
                Assert.Equal(scenario == 3 ? "generation_missing" : "generation_foreign", refusalScenario);
                intactBeforeTamper = before;
                var generationPath = new TrustedLocalFileScope([files.RuntimeRootPath]).ValidateFile(files.SessionGenerationPath, allowMissing: false);
                Assert.Equal(before!["$sessionGeneration"], await File.ReadAllBytesAsync(generationPath));
                // All issuing/admission leases have closed. This is one negative
                // physical deletion/replacement, not target rotation or recovery authority.
                if (scenario == 3) File.Delete(generationPath);
                else
                {
                    donor = await InitializeJointGenerationDonorAsync(donorRoot!);
                    Assert.NotEqual(files.BasePath, donor.Root);
                    Assert.False(before["$sessionGeneration"]!.AsSpan().SequenceEqual(donor.Bytes));
                    await File.WriteAllBytesAsync(generationPath, donor.Bytes);
                }
                before = await CaptureJointGenerationRefusalRawImagesAsync(files);
                if (scenario == 3) Assert.Null(before["$sessionGeneration"]);
                else Assert.Equal(donor!.Bytes, before["$sessionGeneration"]);
            }
            if (scenario is 5 or 6)
            {
                Assert.Equal(scenario == 5 ? "phase_known_compensation" : "phase_uncertain_debt", refusalScenario);
                before = await CaptureJointGenerationRefusalRawImagesAsync(files);
                phaseProbe!.Arm(); // Real phase only; no caller-created capability or marker.
            }
            if (scenario == 7)
            {
                Assert.Equal(1, context["expectedWorldCycles"]!.GetValue<int>());
                before = await CaptureJointGenerationRefusalRawImagesAsync(files);
                // Genuine original/full union/applied phase/projection and all
                // pristine prerequisites above precede any armed story fault.
                storyProbe!.Arm(before, original!, Assert.IsType<string>(actualGeneration));
                if (refusalScenario == "story_prefix_changed")
                {
                    await storyProbe.AlterOnlyOriginalPrefixAsync();
                    before = await CaptureJointGenerationRefusalRawImagesAsync(files);
                }
            }
            if (scenario == 8)
            {
                Assert.Equal(1, context["expectedWorldCycles"]!.GetValue<int>());
                Assert.Equal(context["expectedRetainedStorySha256"]!.GetValue<string>(), MortalArchiveHash(storyBefore!));
                retainedRawBefore = await CaptureJointGenerationRefusalRawImagesAsync(files);
            }
            if (scenario == 2 && refusalScenario == "manifest_malformed")
            {
                // The genuine original/phase preflight and one-leaf corruption
                // above have finished and their lease has closed. Browser main
                // admission rejects this malformed manifest before inner entry.
                manifestAdmissionRawBefore = await CaptureJointGenerationRefusalRawImagesAsync(files);
                before = manifestAdmissionRawBefore;
            }
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_joint_applied_child_before_actual_owner", Observation = new
            { Environment.ProcessId, original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber,
                FreshCommonCacheEmpty = true, OriginalFromOfficialReader = true, RefusalScenario = refusalScenario,
                SourceExpectedFrameClassification = sourceExpectedFrameClassification,
                IntactBeforeTamper = intactBeforeTamper is null ? null : MortalArchiveDigests(intactBeforeTamper),
                Before = MortalArchiveDigests(before) } }));
            // The first owning call is the real late lifecycle. No Restore,
            // Export, correction, reducer or Compose has run in this process.
            actualOuterCallerInvoked = true;
            if (scenario == 2 && refusalScenario == "manifest_malformed")
            {
                var inGameBeforeAdmission = GetPrivateFieldValue<bool>(engine, "_inGame");
                try
                {
                    manifestAdmissionFailure = await Record.ExceptionAsync(async () =>
                    {
                        handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
                    });
                }
                finally
                {
                    // No second admission or lease may repair the damaged
                    // namespace before its complete outcome is retained.
                    manifestAdmissionRawAfter = await CaptureJointGenerationRefusalRawImagesAsync(files);
                    after = manifestAdmissionRawAfter;
                    Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_joint_manifest_admission_actual_outcome", Observation = new
                    { actualOuterCallerInvoked, actualCallerEntered, handled, Failure = manifestAdmissionFailure?.ToString(),
                        InGameBefore = inGameBeforeAdmission, InGameAfter = GetPrivateFieldValue<bool>(engine, "_inGame"),
                        RawBefore = manifestAdmissionRawBefore, RawAfter = manifestAdmissionRawAfter } }));
                }
                var refused = Assert.IsAssignableFrom<JsonException>(manifestAdmissionFailure);
                Assert.Contains("FileSystemManager.PreflightBrowserOriginalAdmission", refused.StackTrace ?? string.Empty, StringComparison.Ordinal);
                Assert.Equal(Encoding.UTF8.GetBytes("{"), manifestAdmissionRawBefore![LiveTurnPreparationService.PendingTurnSnapshotManifestPath]);
                AssertJointGenerationRefusalRawImagesEqual(manifestAdmissionRawBefore!, manifestAdmissionRawAfter!);
                Assert.Equal(storyBefore, manifestAdmissionRawAfter![QteStoryPath]);
                Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
                Assert.Equal(inGameBeforeAdmission, GetPrivateFieldValue<bool>(engine, "_inGame"));
                Assert.False(actualCallerEntered); Assert.Null(handled);
                refusalVerified = true;
                return;
            }
            if (scenario == 7)
            {
                storyOwningFailure = await Record.ExceptionAsync(async () =>
                {
                    handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
                });
                // Raw reads retain any debt/uncertainty. No second owner,
                // admission, story proof issuer or recovery runs after failure.
                after = await CaptureJointGenerationRefusalRawImagesAsync(files);
                var diagnostics = storyLogger!.Describe();
                Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_joint_story_actual_owning_outcome", Observation = new
                {
                    actualOuterCallerInvoked, actualCallerEntered, handled, Failure = storyOwningFailure?.ToString(),
                    EngineDiagnostics = diagnostics, Probe = storyProbe!.Evidence(), RawBefore = before, RawAfter = after,
                    ActualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber
                } }));
                Assert.Null(storyOwningFailure);
                storyProbe.AssertOutcome(before!, after, diagnostics);
                Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
                Assert.True(actualCallerEntered); Assert.True(handled);
                Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
                refusalVerified = true;
                return;
            }
            if (scenario is 3 or 4)
            {
                var inGameBeforeAdmission = GetPrivateFieldValue<bool>(engine, "_inGame");
                generationAdmissionFailure = await Record.ExceptionAsync(async () =>
                {
                    handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
                });
                after = await CaptureJointGenerationRefusalRawImagesAsync(files);
                Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_joint_generation_actual_admission_outcome", Observation = new
                { actualOuterCallerInvoked, actualCallerEntered, handled, Failure = generationAdmissionFailure?.ToString(),
                    Before = MortalArchiveDigests(before), After = MortalArchiveDigests(after) } }));
                AssertJointGenerationRefusalRawImagesEqual(before!, after);
                if (scenario == 3) Assert.Null(after["$sessionGeneration"]);
                else
                {
                    Assert.Equal(donor!.Bytes, after["$sessionGeneration"]);
                    donorAfter = await CaptureJointGenerationRefusalRawImagesAsync(
                        new FileSystemManager(donor.Root, NullLogger<FileSystemManager>.Instance));
                    AssertJointGenerationRefusalRawImagesEqual(donor.Before, donorAfter);
                }
                Assert.Equal(storyBefore, after[QteStoryPath]);
                Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
                Assert.Equal(inGameBeforeAdmission, GetPrivateFieldValue<bool>(engine, "_inGame"));
                var refused = Assert.IsType<InvalidDataException>(generationAdmissionFailure);
                Assert.Equal(scenario == 3 ? "Active joint continuation generation is missing; evidence retained."
                    : "Active joint continuation generation is invalid or differs; evidence retained.", refused.Message);
                Assert.False(actualCallerEntered); Assert.Null(handled);
                refusalVerified = true;
                return;
            }
            if (scenario is 5 or 6)
            {
                phaseOwningFailure = await Record.ExceptionAsync(async () =>
                {
                    handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
                });
                // Raw reads preserve debt. No second lease/owning call may
                // recover the unknown journal before its outcome is retained.
                after = await CaptureJointGenerationRefusalRawImagesAsync(files);
                Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_joint_phase_actual_owning_outcome", Observation = new
                { actualOuterCallerInvoked, actualCallerEntered, handled, Failure = phaseOwningFailure?.ToString(),
                    Probe = phaseProbe!.Evidence(), RawBefore = before, RawAfter = after } }));
                Assert.Null(phaseOwningFailure);
                phaseProbe.AssertOutcome(before!, after);
                Assert.Equal(storyBefore, after[QteStoryPath]);
                Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
                Assert.True(actualCallerEntered); Assert.True(handled);
                Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
                refusalVerified = true;
                return;
            }
            handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            if (scenario == 8)
            {
                retainedRawAfter = await CaptureJointGenerationRefusalRawImagesAsync(files);
                Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "fr063_joint_retained_row_fresh_owner_outcome", Observation = new
                {
                    actualOuterCallerInvoked, actualCallerEntered, handled, RetainedStoryAppendAttempts = retainedStoryAppendAttempts,
                    RawBefore = retainedRawBefore, RawAfter = retainedRawAfter,
                    ActualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber
                } }));
            }
            if (scenario == 2)
            {
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                {
                    after = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                    await AssertConfirmedClaimReadImagesAsync(files, lease, before!);
                    Assert.Equal(storyBefore, after[QteStoryPath]);
                    Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
                    var damagedHeader = refusalScenario switch
                    {
                        "input_control" or "input_missing" or "input_malformed" => "input/turn_request.json",
                        "authority_payload_digest" => PendingTurnSnapshotAuthority.AuthorityPath,
                        "manifest_missing" or "manifest_malformed" => LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                        _ => null
                    };
                    foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json",
                                 LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
                    {
                        var expectedRetained = path == damagedHeader
                            ? (before!.TryGetValue(path, out var damagedImage) ? damagedImage : null) : intactBeforeTamper![path];
                        Assert.Equal(expectedRetained, after.TryGetValue(path, out var currentImage) ? currentImage : null);
                    }
                }
                // The boolean means a blocked attempt, after complete preservation.
                Assert.True(actualCallerEntered); Assert.True(handled);
                Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
                refusalVerified = true;
                return;
            }
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                after = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                var history = WoundHistoryState.Parse(await files.ReadFileAsync(lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
                Assert.True(history.IsValid, DescribeValidationIssues(history.Issues));
                var prior = WoundHistoryState.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(original.ReadRequiredBytes(WoundHistoryState.HistoryPath)), WoundHistoryState.HistoryPath);
                Assert.True(prior.IsValid, DescribeValidationIssues(prior.Issues));
                var rows = history.State!.Transitions.Skip(prior.State!.Transitions.Count).ToArray();
                Assert.Equal(2, rows.Length); Assert.Equal("create", rows[0].Kind); Assert.Equal("treat", rows[1].Kind);
                Assert.Equal(prior.State.NextOrdinal, rows[0].Ordinal); Assert.Equal(prior.State.NextOrdinal + 1, rows[1].Ordinal);
                Assert.Equal(context["requestFingerprint"]!.GetValue<string>(), rows[1].TreatmentResult!.Request.RequestFingerprint);
                var definitions = ResourceDefinitionCatalog.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath), false);
                Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
                var state = ResourceStateContract.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
                var resources = ResourceHistoryState.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
                Assert.True(state.IsValid, DescribeValidationIssues(state.Issues)); Assert.True(resources.IsValid, DescribeValidationIssues(resources.Issues));
                Assert.Empty(resources.History!.ValidateStateAgreement(state.Ledger!));
                var spend = Assert.Single(resources.History.Transitions, row => row.Turn == 43 && row.Coordinate.ResourceKey == "energy" && row.Operation == ResourceTransitionOperation.Spend);
                Assert.Equal(2m, spend.AppliedAmount); Assert.Equal(2m, spend.BeforeState!.Current); Assert.Equal(0m, spend.AfterState!.Current);
                var damage = Assert.Single(resources.History.Transitions, row => row.Turn == 43 && row.Coordinate.ResourceKey == "health");
                Assert.Equal(3m, damage.AppliedAmount); Assert.Equal(5m, damage.BeforeState!.Current); Assert.Equal(2m, damage.AfterState!.Current);
                Assert.Equal(0m, Assert.Single(state.Ledger!.Entries, entry => entry.Coordinate.ResourceKey == "energy").Current);
                Assert.Equal(2m, Assert.Single(state.Ledger.Entries, entry => entry.Coordinate.ResourceKey == "health").Current);
                var marker = ParseJsonObjectBytes(Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, MortalQteDecisionPublication.StatePath)));
                Assert.Equal("applied", marker["progression"]!["phase"]!.GetValue<string>());
                foreach (var path in new[] { ResourceMaterializationContract.StatePath,
                             ResourceMaterializationContract.HistoryPath, WoundHistoryState.HistoryPath, WoundIdentityState.StatePath,
                             WoundCarrierCatalog.PlayerPath, EffectIdentityState.StatePath, EffectCarrierCatalog.PlayerPath,
                             MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath })
                    Assert.Equal(before[path], after[path]); // Neither cold contour redoes finalized mechanics.
                var previousMarker = ParseJsonObjectBytes(Assert.IsType<byte[]>(before[MortalQteDecisionPublication.StatePath]));
                var previousPhase = Assert.IsType<JsonObject>(previousMarker["progression"]);
                var appliedPhase = Assert.IsType<JsonObject>(marker["progression"]);
                Assert.True(JsonNode.DeepEquals(previousPhase["sourceGuards"], appliedPhase["sourceGuards"]));
                foreach (var image in new[] { "scheduleBefore", "reportBefore", "scheduleAfter", "reportAfter" })
                    Assert.True(JsonNode.DeepEquals(previousPhase[image], appliedPhase[image]));
                Assert.Equal(previousPhase["mutationFingerprint"]!.GetValue<string>(), appliedPhase["mutationFingerprint"]!.GetValue<string>());
                if (scenario is 0 or 8)
                {
                    foreach (var path in new[] { MortalQteDecisionPublication.StatePath, ProgressionScheduleService.SchedulePath,
                                 CharacteristicsService.JointDerivedProjectionPath }) Assert.Equal(before[path], after[path]);
                }
                else
                {
                    var originalRequest = StrictJsonAuthority.Deserialize<BookOfEternityClient.Models.TurnRequest>(
                        CanonicalJsonUtf8.DecodeOneOptionalBom(before["input/turn_request.json"]!),
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual unapplied cold original control");
                    Assert.NotNull(originalRequest); Assert.NotNull(originalRequest!.ProgressionControl);
                    var control = originalRequest.ProgressionControl!;
                    Assert.Equal(0, control.WorldCyclesAlreadyPendingBeforeTurn); Assert.Equal(0, control.FactionCyclesAlreadyPendingBeforeTurn);
                    var scheduleBefore = Convert.FromBase64String(previousPhase["scheduleBefore"]!["bytes"]!.GetValue<string>());
                    Assert.Equal(scheduleBefore, before[ProgressionScheduleService.SchedulePath]);
                    var scheduleAfter = Convert.FromBase64String(appliedPhase["scheduleAfter"]!["bytes"]!.GetValue<string>());
                    Assert.Equal(scheduleAfter, after[ProgressionScheduleService.SchedulePath]);
                    var expected = ParseJsonObjectBytes(scheduleBefore);
                    expected["currentWorldTimeInMinutes"] = control.CurrentWorldTimeInMinutes;
                    expected["lastWorldSimulationTimeInMinutes"] = control.LastWorldSimulationTimeInMinutes;
                    expected["lastFactionSimulationTimeInMinutes"] = control.LastFactionSimulationTimeInMinutes;
                    expected["pendingWorldCycles"] = 0; expected["pendingFactionCycles"] = 0;
                    expected["lastUpdatedUtc"] = appliedPhase["preparedAtUtc"]!.GetValue<string>();
                    Assert.True(JsonNode.DeepEquals(expected, ParseJsonObjectBytes(scheduleAfter)));
                    Assert.NotNull(await files.ReadFileBytesAsync(lease, CharacteristicsService.JointDerivedProjectionPath));
                }
                Assert.Null(await files.ReadFileBytesAsync(lease, ProgressionScheduleService.ReportPath));
                var story = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, QteStoryPath));
                var storyPrefix = storyBefore!;
                if (scenario == 8)
                {
                    storyPrefix = Assert.IsType<byte[]>(originalStoryPrefix);
                    Assert.Equal(storyBefore, story); Assert.Equal(0, retainedStoryAppendAttempts);
                }
                Assert.True(story.AsSpan().StartsWith(storyPrefix));
                var suffix = Encoding.UTF8.GetString(story.AsSpan(storyPrefix.Length)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var row = StrictJsonAuthority.Deserialize<StoryEntry>(Assert.Single(suffix), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "cold exact joint story");
                Assert.NotNull(row); Assert.Equal(43, row!.Turn); Assert.Equal("Осмотреть ушиб.", row.Player);
                if (scenario == 8)
                {
                    Assert.Equal(original.RequestId, row.RequestId); Assert.Equal(actualGeneration, row.SessionGeneration);
                    Assert.Equal(appliedPhase["preparedAtUtc"]!.GetValue<string>(), row.Timestamp);
                }
                Assert.Equal(context["scene"]!.GetValue<string>(), row.Narrative);
                Assert.Equal(43, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
                Assert.True(GetPrivateFieldValue<bool>(engine, "_inGame"));
                Assert.Equal(row.Narrative, GetPrivateField<GameResponse>(engine, "_lastResponse").Response);
                Assert.Equal(before["$sessionGeneration"], after["$sessionGeneration"]);
                foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                             PendingTurnSnapshotAuthority.AuthorityPath, AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                             MortalWoundOpportunityAdapter.DecisionPath, "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json" })
                    Assert.Null(await files.ReadFileBytesAsync(lease, path));
            }
            Assert.True(actualCallerEntered); Assert.False(handled);
            secondIdle = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                repeated = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                await AssertConfirmedClaimReadImagesAsync(files, lease, after);
            }
            Assert.False(secondIdle); Assert.Equal(43, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            completed = true;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
            {
                processId = Environment.ProcessId, actualOuterCallerInvoked, actualCallerEntered, handled, secondIdle, completed, refusalVerified, refusalScenario,
                GenerationAdmissionFailure = generationAdmissionFailure?.ToString(),
                ManifestAdmissionFailure = manifestAdmissionFailure?.ToString(),
                ManifestAdmissionRawBefore = manifestAdmissionRawBefore, ManifestAdmissionRawAfter = manifestAdmissionRawAfter,
                PhaseOwningFailure = phaseOwningFailure?.ToString(), PhaseFaultEvidence = phaseProbe?.Evidence(),
                PhaseFaultRawBefore = scenario is 5 or 6 ? before : null, PhaseFaultRawAfter = scenario is 5 or 6 ? after : null,
                StoryOwningFailure = storyOwningFailure?.ToString(), StoryFaultEvidence = storyProbe?.Evidence(),
                StoryRawBefore = scenario == 7 ? before : null, StoryRawAfter = scenario == 7 ? after : null,
                StoryEngineDiagnostics = storyLogger?.Describe(),
                RetainedStoryAppendAttempts = retainedStoryAppendAttempts,
                RetainedStoryRawBefore = retainedRawBefore, RetainedStoryRawAfter = retainedRawAfter,
                InitiallyBadOutputRawBefore = scenario == 9 ? before : null, InitiallyBadOutputRawAfter = scenario == 9 ? after : null,
                GenerationRefusalRawBefore = scenario is 3 or 4 ? before : null, GenerationRefusalRawAfter = scenario is 3 or 4 ? after : null,
                GenerationDonor = donor is null ? null : new { donor.Root, donor.Generation,
                    Bytes = donor.Bytes, Before = donor.Before, After = donorAfter },
                SourceExpectedFrameClassification = sourceExpectedFrameClassification,
                AuxiliaryFactoryRoot = factory._rootPath, Root = root, BinaryPins = MortalArchiveBinaryPins(),
                DependencyContext = MortalArchiveDependencyContext(), LoadedDependencies = MortalArchiveLoadedDependencyBindings(),
                Original = original is null ? null : new { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber },
                actualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber, failure = failure?.ToString(),
                IntactBeforeTamper = intactBeforeTamper is null ? null : MortalArchiveDigests(intactBeforeTamper),
                Before = before is null ? null : MortalArchiveDigests(before), After = after is null ? null : MortalArchiveDigests(after),
                Repeated = repeated is null ? null : MortalArchiveDigests(repeated)
            }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
    }
}
