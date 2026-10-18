using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_InterfacePublicationPrecedesCommonAndLateConsumesOnce()
    {
        using var probe = new JointInterfaceLifecycleProbe(this);
        await RunJointCompletionControlAsync(nonzeroProgression: false, useLiveCaller: true,
            interfaceLifecycleProbe: probe);
        // The shared helper has already checked real Stage43/original/TREAT,
        // actual rejected-wave restore/correction/common completion and inert repeat.
        // Baseline executes those owners; only this first ordering oracle is RED.
        probe.AssertEarlyInterfaceAndSingleConsumption();
    }

    [Theory]
    [InlineData("Utf8")]
    [InlineData("Bom")]
    [InlineData("Utf16")]
    public async Task GuaranteedTreatment_InterfaceNoWriteRetainsExactBytesAndMtime(string encoding)
    {
        using var probe = new JointInterfaceLifecycleProbe(this, encoding);
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
        probe.AssertNoWriteCompletion();
    }

    [Fact]
    public async Task GuaranteedTreatment_AbsentInterfacePublishesOnlyResultAndCompletes()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "Absent");
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
        probe.AssertNoWriteCompletion();
    }

    [Fact]
    public async Task GuaranteedTreatment_InterfaceErrorFreezesPreparedWithoutPublicationOrCommon()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "Error");
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
        // The shared fixture first proves real original43/held request/repair
        // dispatch/correlated correction; its deliberate held branch runs below.
    }

    [Fact]
    public async Task GuaranteedTreatment_InterfaceWarningVectorSurvivesCommonAndOrdinaryCompletion()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "Warning");
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
        probe.AssertNoWriteCompletion();
        probe.AssertFrozenWarning();
    }

    // Test-only observation and public-response authoring. It never constructs a
    // result/Ready/capability, changes protocol versions or invokes a publisher.
    private sealed partial class JointInterfaceLifecycleProbe : IDisposable
    {
        private readonly GameEngineTurnLifecycleTests fixture;
        private readonly string scenario;
        internal JointInterfaceLifecycleProbe(GameEngineTurnLifecycleTests fixture, string scenario = "Changed")
        { this.fixture = fixture; this.scenario = scenario; }
        private const string InterfacePath = "output/interface_updates.json";
        private const string TwoTags = "[NI_ACTION:first][NI_CONTROL:second]Продолжить обычный ответ.";
        private const string OneTag = "[NI_CONTROL:second]Продолжить обычный ответ.";
        private readonly List<InterfacePublicationObservation> _publications = [];
        private readonly List<JsonObject> _resultCommits = [];
        private readonly Dictionary<string, byte[]?> _mechanicalBefore = new(StringComparer.Ordinal);
        private GameEngine? _engine;
        private GameEngineSessionFinalizationHooks? _priorHooks;
        private byte[]? _submitted;
        private byte[]? _atCapture;
        private byte[]? _finalInterface;
        private bool _completionObserved;
        private JsonObject? _firstMarker;
        private int _captureCallbacks, _interfaceAtCapture, _sequence;
        private bool _configured;
        private DateTime? _submittedMtime, _finalMtime;
        internal bool ExpectsHeld => scenario == "Error";

        internal void Attach(GameEngine engine)
        {
            Assert.Null(_engine);
            _engine = engine;
            _priorHooks = GetPrivateFieldValue<GameEngineSessionFinalizationHooks?>(engine, "_sessionFinalizationHooks");
            engine.ConfigureSessionFinalizationHooksForTesting(new GameEngineSessionFinalizationHooks
            {
                AtCheckpointAsync = async checkpoint =>
                {
                    if (_priorHooks?.AtCheckpointAsync is { } prior) await prior(checkpoint);
                    if (checkpoint == SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization &&
                        scenario.StartsWith("Selector:", StringComparison.Ordinal))
                        await ObserveActualGenericSelectorAsync(engine);
                    if (checkpoint == SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization && scenario == "Warning")
                        await ObserveFrozenWarningConsumersAsync(engine);
                    if (checkpoint.ToString() != "MortalJointCustodyCaptureCompletedBeforePreparation") return;
                    _captureCallbacks++;
                    _interfaceAtCapture = _publications.Count(p => p.InterfaceChanged);
                    _atCapture = File.Exists(fixture._fs.ResolvePath(InterfacePath))
                        ? File.ReadAllBytes(fixture._fs.ResolvePath(InterfacePath)) : null;
                    if (scenario == "LateCurrent") ObserveNewLateReadyOwner(engine);
                    fixture.WritePhysicalInjuryDiagnostic("iobs_actual_completed_capture", new
                    {
                        Callback = _captureCallbacks, InterfaceCommittedCohorts = _interfaceAtCapture,
                        InterfaceRaw = Raw(_atCapture), Trace = _publications.ToArray(),
                        Meaning = "Existing actual capture checkpoint, not NI authority or a synthetic preparation hook"
                    });
                }
            });
            AttachInitialCloseObservation();
        }

        internal void ConfigureCorrectedOutputs(Dictionary<string, byte[]> outputs)
        {
            Assert.False(_configured); _configured = true;
            if (scenario == "Absent") { outputs.Remove(InterfacePath); return; }
            // Missing useful timestamp forces the real ordinary fallback, and an
            // object with two tags changes on each projection: late replay is visible.
            var value = new JsonObject
            {
                ["dialogueOptions"] = new JsonArray(new JsonObject
                { ["text"] = ChangesInterface ? TwoTags : "Продолжить обычный ответ." })
            };
            if (!ChangesInterface) value["timestamp"] = "2026-10-18T00:00:00.0000000+00:00";
            if (scenario == "Error") value["unsupported_result_field"] = true;
            var text = value.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
            _submitted = scenario switch
            {
                "Utf16" => Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray(),
                "Bom" => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(),
                _ => Encoding.UTF8.GetBytes(text)
            };
            outputs[InterfacePath] = _submitted.ToArray();
        }

        internal void ConfigureBeforeOriginal()
        {
            if (scenario != "Warning") return;
            // A real public previous-turn image BEFORE Stage43 signs its own
            // original. No original manifest/hash is edited or re-signed here.
            var value = new JsonObject { ["dialogueOptions"] = new JsonArray(new JsonObject
                { ["text"] = "Продолжить обычный ответ." }), ["timestamp"] = "2026-10-18T00:00:00.0000000+00:00" };
            File.WriteAllText(fixture._fs.ResolvePath(InterfacePath), value.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed), new UTF8Encoding(false));
        }

        internal void ObserveCorrectedSubmissionReadback()
        {
            Assert.True(_configured);
            if (scenario == "Absent") File.Delete(fixture._fs.ResolvePath(InterfacePath)); // actual public GM absence
            Assert.Equal(_submitted, ReadPhysical(InterfacePath));
            _submittedMtime = Mtime();
            foreach (var path in new[] { ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath, WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath,
                WoundHistoryState.HistoryPath, EffectIdentityState.StatePath, EffectCarrierCatalog.PlayerPath,
                MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath,
                "game_state/world/world_time.json", "game_state/player/status_changes.json", "game_state/player/experience.json",
                "game_state/core/player_status.json", "game_state/player/stat_points.json" })
                _mechanicalBefore.Add(path, ReadPhysical(path));
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_public_correction_before_ready", new
            { scenario, Interface = Raw(_submitted), Mtime = _submittedMtime,
                Mechanical = _mechanicalBefore.ToDictionary(row => row.Key, row => Raw(row.Value)) });
        }

        internal void ObservePublication(TrustedLocalPublicationPhase phase, int index)
        {
            ObserveInitialInstallation(phase);
            if (!_configured || phase != TrustedLocalPublicationPhase.Committed) return;
            var journalPath = Path.Combine(fixture._fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(journalPath));
            var root = journal.RootElement;
            var members = root.GetProperty("Members").EnumerateArray().ToArray();
            var pendingMember = members.FirstOrDefault(member => member.GetProperty("Path").GetString() ==
                fixture._fs.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
            if (pendingMember.ValueKind != JsonValueKind.Undefined && Image(pendingMember, "After") is { } pendingBytes)
            {
                var pending = ParseJsonObjectBytes(pendingBytes);
                if (pending[MortalJointInterfacePublication.PendingField] is JsonObject result)
                {
                    // Exact actual committed member, not a fixture-created DTO.
                    _ = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(result));
                    _resultCommits.Add(result.DeepClone().AsObject());
                    fixture.WritePhysicalInjuryDiagnostic("iobs_actual_result_commit", new
                    { TransactionId = root.GetProperty("TransactionId").GetString(), Record = result,
                        Interface = Raw(ReadPhysical(InterfacePath)), Mtime = Mtime() });
                }
            }
            var interfaceMember = members.FirstOrDefault(member =>
                member.GetProperty("Path").GetString() == fixture._fs.ResolvePath(InterfacePath));
            var markerMember = members.FirstOrDefault(member =>
                member.GetProperty("Path").GetString() == fixture._fs.ResolvePath(MortalQteDecisionPublication.StatePath));
            if (interfaceMember.ValueKind == JsonValueKind.Undefined && markerMember.ValueKind == JsonValueKind.Undefined) return;
            var interfaceChanged = interfaceMember.ValueKind != JsonValueKind.Undefined &&
                !ImagesEqual(Image(interfaceMember, "Before"), Image(interfaceMember, "After"));
            var markerCommitted = markerMember.ValueKind != JsonValueKind.Undefined &&
                markerMember.GetProperty("After").GetProperty("Exists").GetBoolean();
            if (!interfaceChanged && !markerCommitted) return;
            var observation = new InterfacePublicationObservation(++_sequence,
                root.GetProperty("TransactionId").GetString()!, interfaceChanged, markerCommitted,
                interfaceMember.ValueKind == JsonValueKind.Undefined ? null : Raw(Image(interfaceMember, "Before")),
                interfaceMember.ValueKind == JsonValueKind.Undefined ? null : Raw(Image(interfaceMember, "After")));
            _publications.Add(observation);
            if (markerCommitted && _firstMarker is null)
                _firstMarker = ParseJsonObjectBytes(Image(markerMember, "After")!);
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_committed_member", observation);
        }

        internal void AssertEarlyInterfaceAndSingleConsumption()
        {
            // CleanupOwnedFixture has removed the mutable root when the shared
            // helper returns; use the original bytes captured before that close.
            var final = _finalInterface;
            fixture.WritePhysicalInjuryDiagnostic("iobs_ordering_original_outcome", new
            {
                Callback = _captureCallbacks, InterfaceAtCapture = _interfaceAtCapture,
                Submitted = Raw(_submitted), AtCapture = Raw(_atCapture), Final = Raw(final),
                Publications = _publications.ToArray(), FirstActualMarker = _firstMarker,
                ActualTurn = GetPrivateField<GameLoop>(_engine!, "_gameLoop").TurnNumber,
                CompletionObservedBeforeOwnedCleanup = _completionObserved,
                Scope = "Full existing ordinary completion/inert-repeat already returned; actual I members are distinct from marker cohorts"
            });
            // FIRST causal assertion: real old late-only I performs genuine work,
            // but has zero I commits at the actual pre-preparation checkpoint.
            Assert.Equal(1, _interfaceAtCapture);
            Assert.True(_completionObserved);
            Assert.Equal(1, _captureCallbacks);
            var write = Assert.Single(_publications.Where(p => p.InterfaceChanged));
            var common = _publications.First(p => p.MarkerCommitted);
            Assert.True(write.Sequence < common.Sequence, JsonSerializer.Serialize(_publications));
            Assert.Equal(_atCapture, final);
            var result = ParseJsonObjectBytes(Assert.IsType<byte[]>(final));
            var option = Assert.IsType<JsonObject>(Assert.Single(result["dialogueOptions"]!.AsArray()));
            Assert.Equal(OneTag, option["text"]!.GetValue<string>());
            Assert.Equal(TwoTags, option["inputValue"]!.GetValue<string>());
            Assert.False(string.IsNullOrWhiteSpace(result["timestamp"]?.GetValue<string>()));
            Assert.Equal(3, _firstMarker!["outputBinding"]!["version"]!.GetValue<int>());
            Assert.Equal("PreparedResultPublicationV1",
                _firstMarker["outputBinding"]!["publishedInterfaceResult"]!["origin"]!.GetValue<string>());
        }

        internal void ObserveCompletionBeforeCleanup()
        {
            _finalInterface = File.Exists(fixture._fs.ResolvePath(InterfacePath))
                ? File.ReadAllBytes(fixture._fs.ResolvePath(InterfacePath)) : null;
            _completionObserved = true;
            _finalMtime = Mtime();
        }

        internal void AssertNoWriteCompletion()
        {
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_no_write_completion", new
            { scenario, _completionObserved, Submitted = Raw(_submitted), Final = Raw(_finalInterface),
                SubmittedMtime = _submittedMtime, FinalMtime = _finalMtime, Records = _resultCommits, Trace = _publications });
            Assert.True(_completionObserved); Assert.Equal(1, _captureCallbacks);
            Assert.Equal(_submitted, _atCapture); Assert.Equal(_submitted, _finalInterface);
            Assert.Equal(_submittedMtime, _finalMtime);
            Assert.Empty(_publications.Where(row => row.InterfaceChanged));
            Assert.Equal(new[] { "Prepared", "Published" }, _resultCommits.Select(row => row["state"]!.GetValue<string>()));
            Assert.All(_resultCommits, row => Assert.Equal(scenario == "Absent" ? "Absent" : "NoWrite", row["normalization"]!.GetValue<string>()));
            Assert.Equal(3, _firstMarker!["outputBinding"]!["version"]!.GetValue<int>());
            Assert.True(JsonNode.DeepEquals(_resultCommits[1], _firstMarker["outputBinding"]!["publishedInterfaceResult"]!["record"]));
        }

        internal void AssertFrozenWarning()
        {
            var warnings = _resultCommits[1]["issues"]!.AsArray();
            var warning = Assert.IsType<JsonObject>(Assert.Single(warnings));
            Assert.Equal("Warning", warning["severity"]!.GetValue<string>());
            Assert.Equal("accepted_turn_stale_interface_updates", warning["code"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(_resultCommits[0]["issues"], _resultCommits[1]["issues"]));
            Assert.Equal(1, _lateWarningCount); Assert.Equal(new[] { 1, 1 }, _genericWarningCounts);
        }

        internal void AssertHeld(GameEngine engine, string owningDiagnostics)
        {
            var pendingBytes = ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            var pending = ParseJsonObjectBytes(Assert.IsType<byte[]>(pendingBytes));
            var result = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(pending[MortalJointInterfacePublication.PendingField]));
            var after = _mechanicalBefore.Keys.ToDictionary(path => path, ReadPhysical, StringComparer.Ordinal);
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_error_prepared_held", new
            { scenario, Result = result.ToJson(), Interface = Raw(ReadPhysical(InterfacePath)), Mtime = Mtime(),
                MechanicalBefore = _mechanicalBefore.ToDictionary(row => row.Key, row => Raw(row.Value)),
                MechanicalAfter = after.ToDictionary(row => row.Key, row => Raw(row.Value)), Trace = _publications,
                ActualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber, OwningDiagnostics = owningDiagnostics });
            Assert.Equal("Prepared", result.State); Assert.True(result.HasErrors);
            var error = Assert.Single(result.Issues.Where(issue => issue.Severity == IssueSeverity.Error));
            Assert.Equal("interface_updates_unknown_field", error.Code);
            foreach (var issue in result.Issues) Assert.Contains(JsonSerializer.Serialize(issue), owningDiagnostics);
            Assert.Single(_resultCommits); Assert.Empty(_publications);
            Assert.Equal(_submitted, result.BeforeBytes);
            Assert.False(Assert.IsType<byte[]>(_submitted).AsSpan().SequenceEqual(Assert.IsType<byte[]>(result.AfterBytes)));
            Assert.Equal(_submitted, ReadPhysical(InterfacePath)); Assert.Equal(_submittedMtime, Mtime());
            Assert.Equal(0, _captureCallbacks); // Prepared is positive reach evidence; capture/preparation did not begin
            Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            Assert.True(GetPrivateFieldValue<bool>(engine, "_mortalJointInterfaceFreshConsumed"));
            Assert.True(GetPrivateFieldValue<bool>(engine, "_mortalJointInterfacePreparationHeld"));
            foreach (var (path, before) in _mechanicalBefore) Assert.Equal(before, after[path]);
        }

        private byte[]? ReadPhysical(string path) => File.Exists(fixture._fs.ResolvePath(path))
            ? File.ReadAllBytes(fixture._fs.ResolvePath(path)) : null;
        private DateTime? Mtime() => File.Exists(fixture._fs.ResolvePath(InterfacePath))
            ? File.GetLastWriteTimeUtc(fixture._fs.ResolvePath(InterfacePath)) : null;

        private static byte[]? Image(JsonElement member, string side)
        {
            var image = member.GetProperty(side);
            return image.GetProperty("Exists").GetBoolean() ? image.GetProperty("Bytes").GetBytesFromBase64() : null;
        }
        private static bool ImagesEqual(byte[]? left, byte[]? right) =>
            left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
        private static object? Raw(byte[]? bytes) => bytes is null ? null : new
        { Bytes = Convert.ToBase64String(bytes), Length = bytes.LongLength, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
        private sealed record InterfacePublicationObservation(int Sequence, string TransactionId,
            bool InterfaceChanged, bool MarkerCommitted, object? Before, object? After);

        public void Dispose()
        {
            DisposeInitialCloseObservation();
            if (_engine is not null) _engine.ConfigureSessionFinalizationHooksForTesting(_priorHooks);
        }
    }
}
