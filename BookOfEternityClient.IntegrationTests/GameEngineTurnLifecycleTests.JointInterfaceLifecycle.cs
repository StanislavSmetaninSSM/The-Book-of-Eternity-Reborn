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

    // Test-only observation and public-response authoring. It never constructs a
    // result/Ready/capability, changes protocol versions or invokes a publisher.
    private sealed class JointInterfaceLifecycleProbe(GameEngineTurnLifecycleTests fixture) : IDisposable
    {
        private const string InterfacePath = "output/interface_updates.json";
        private const string TwoTags = "[NI_ACTION:first][NI_CONTROL:second]Продолжить обычный ответ.";
        private const string OneTag = "[NI_CONTROL:second]Продолжить обычный ответ.";
        private readonly List<InterfacePublicationObservation> _publications = [];
        private GameEngine? _engine;
        private GameEngineSessionFinalizationHooks? _priorHooks;
        private byte[]? _submitted;
        private byte[]? _atCapture;
        private byte[]? _finalInterface;
        private bool _completionObserved;
        private JsonObject? _firstMarker;
        private int _captureCallbacks, _interfaceAtCapture, _sequence;

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
                    if (checkpoint.ToString() != "MortalJointCustodyCaptureCompletedBeforePreparation") return;
                    _captureCallbacks++;
                    _interfaceAtCapture = _publications.Count(p => p.InterfaceChanged);
                    _atCapture = File.Exists(fixture._fs.ResolvePath(InterfacePath))
                        ? File.ReadAllBytes(fixture._fs.ResolvePath(InterfacePath)) : null;
                    fixture.WritePhysicalInjuryDiagnostic("iobs_actual_completed_capture", new
                    {
                        Callback = _captureCallbacks, InterfaceCommittedCohorts = _interfaceAtCapture,
                        InterfaceRaw = Raw(_atCapture), Trace = _publications.ToArray(),
                        Meaning = "Existing actual capture checkpoint, not NI authority or a synthetic preparation hook"
                    });
                }
            });
        }

        internal void ConfigureCorrectedOutputs(Dictionary<string, byte[]> outputs)
        {
            Assert.Null(_submitted);
            // Missing useful timestamp forces the real ordinary fallback, and an
            // object with two tags changes on each projection: late replay is visible.
            var value = new JsonObject
            {
                ["dialogueOptions"] = new JsonArray(new JsonObject { ["text"] = TwoTags })
            };
            _submitted = Encoding.UTF8.GetBytes(value.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            outputs[InterfacePath] = _submitted.ToArray();
        }

        internal void ObservePublication(TrustedLocalPublicationPhase phase, int index)
        {
            if (_submitted is null || phase != TrustedLocalPublicationPhase.Committed) return;
            var journalPath = Path.Combine(fixture._fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(journalPath));
            var root = journal.RootElement;
            var members = root.GetProperty("Members").EnumerateArray().ToArray();
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
        }

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
            if (_engine is not null) _engine.ConfigureSessionFinalizationHooksForTesting(_priorHooks);
        }
    }
}
