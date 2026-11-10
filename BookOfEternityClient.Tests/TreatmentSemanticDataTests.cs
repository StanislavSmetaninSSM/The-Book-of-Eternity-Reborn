using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Executes real treatment agreement and finalization over detached file-free game DATA.
/// </summary>
public sealed partial class TreatmentSemanticDataTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Selects the compact references to complete local diagnostic rows.
    /// </summary>
    /// <param name="output">
    /// The actual xUnit output receiver for diagnostic references.
    /// </param>
    public TreatmentSemanticDataTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Reuses the actual outcome agreement and resource finalization for eight unchanged requests.
    /// </summary>
    [Fact]
    public void EightRequestsExecuteEachActualSemanticSourceOnce()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using (fixture.EnterTimingContext())
        {
            var expected = MortalWoundTreatmentResourceComposer.Finalize(fixture.Resolution).Finalization;
            Assert.NotNull(expected);
            var outcomeStart = fixture.Count("TreatmentOutcome.AgreesWith");
            var finalizationStart = fixture.Count("TreatmentResources.Finalize");
            for (var i = 0; i < 8; i++)
            {
                Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
                Assert.Equal(expected.FinalizationFingerprint, fixture.Data.ReadFinalizationFingerprint(operation));
            }
            Assert.Equal(1, fixture.Count("TreatmentOutcome.AgreesWith") - outcomeStart);
            Assert.Equal(1, fixture.Count("TreatmentResources.Finalize") - finalizationStart);
        }
    }

    /// <summary>
    /// Preserves the original sources when no whole-operation lifetime was selected.
    /// </summary>
    [Fact]
    public void MissingOperationPreservesUncachedSources()
    {
        using var fixture = new Fixture(_output);
        using (fixture.EnterTimingContext())
        {
            for (var i = 0; i < 3; i++) Assert.True(fixture.Data.ReadOutcomeAgreement(null));
            Assert.Equal(3, fixture.Count("TreatmentOutcome.AgreesWith"));
        }
    }

    /// <summary>
    /// Detects an aliased wound change under unchanged request identity and stored fingerprints.
    /// </summary>
    [Fact]
    public void WarmedAgreementRefusesChangedAliasedWound()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using (fixture.EnterTimingContext())
        {
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            var request = fixture.Resolution.RequestAuthority;
            var fingerprint = request.RequestFingerprint;
            fixture.Symptoms.Add("changed after semantic DATA was retained");
            Assert.False(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Same(request, fixture.Resolution.RequestAuthority);
            Assert.Equal(fingerprint, request.RequestFingerprint);
            fixture.Symptoms.RemoveAt(fixture.Symptoms.Count - 1);
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(3, fixture.Count("TreatmentOutcome.AgreesWith"));
        }
    }

    /// <summary>
    /// Invalidates resource finalization DATA when a caller-owned wound changes under the same request.
    /// </summary>
    [Fact]
    public void WarmedFinalizationRefusesChangedAliasedWound()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using (fixture.EnterTimingContext())
        {
            var expected = fixture.Data.ReadFinalizationFingerprint(operation);
            Assert.NotNull(expected);
            Assert.Equal(expected, fixture.Data.ReadFinalizationFingerprint(operation));
            fixture.Symptoms.Add("changed resource request wound");
            Assert.Null(fixture.Data.ReadFinalizationFingerprint(operation));
            fixture.Symptoms.RemoveAt(fixture.Symptoms.Count - 1);
            Assert.Equal(expected, fixture.Data.ReadFinalizationFingerprint(operation));
            Assert.Equal(3, fixture.Count("TreatmentResources.Finalize"));
        }
    }

    /// <summary>
    /// Recomputes agreement when internal route provenance changes although public JSON remains equal.
    /// </summary>
    [Fact]
    public void InternalRouteProvenanceCannotUseWarmedAgreement()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using (fixture.EnterTimingContext())
        {
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            var original = fixture.Routes[0];
            var publicBefore = JsonSerializer.Serialize(fixture.Resolution);
            fixture.Routes[0] = original with { SourcePath = original.SourcePath + ".changed" };
            Assert.Equal(publicBefore, JsonSerializer.Serialize(fixture.Resolution));
            var originalResult = fixture.Outcome.AgreesWith(fixture.Resolution);
            var sourceStart = fixture.Count("TreatmentOutcome.AgreesWith");
            Assert.Equal(originalResult, fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(sourceStart + 1, fixture.Count("TreatmentOutcome.AgreesWith"));
            fixture.Routes[0] = original;
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(4, fixture.Count("TreatmentOutcome.AgreesWith"));
        }
    }

    /// <summary>
    /// Refuses a computed result when an original source observer changes its request before cache completion.
    /// </summary>
    [Fact]
    public void SourceObserverCannotReturnChangedRequestAsCurrent()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        fixture.OnObservation = (name, boundary) =>
        {
            if (name == "TreatmentOutcome.AgreesWith" && boundary == "end")
                fixture.Symptoms.Add("changed during original source observation");
        };
        using (fixture.EnterTimingContext())
            Assert.ThrowsAny<InvalidOperationException>(() => fixture.Data.ReadOutcomeAgreement(operation));
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Preserves preparation refusal and source exceptions without retaining an unavailable expected seal.
    /// </summary>
    [Fact]
    public void PreparationRefusalIsNotRetained()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var sources = 0;
        ExpectedPreparationReplayProjection? Refuse() { sources++; return null; }
        Assert.Null(fixture.Data.ReadPreparationProjection(operation, new OperationStructuralSnapshot(["refusal"], []), Refuse));
        Assert.Null(fixture.Data.ReadPreparationProjection(operation, new OperationStructuralSnapshot(["refusal"], []), Refuse));
        Assert.Equal(2, sources);
        Assert.Equal(0, operation.CaptureState().Entries);
        var expected = new FormatException("original preparation failure");
        Assert.Same(expected, Assert.Throws<FormatException>(() =>
            fixture.Data.ReadPreparationProjection(operation, new OperationStructuralSnapshot(["refusal"], []), () => throw expected)));
    }

    /// <summary>
    /// Preserves pure final reduction refusal issues and retries the original source instead of retaining failure.
    /// </summary>
    [Fact]
    public void FinalReductionRefusalIsNotRetained()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var admittedInput = new OperationStructuralSnapshot(["refusal DATA control"], [fixture.Resolution]);
        var original = new WoundAcceptedTurnPlanningResult(null,
            [WoundAcceptedTurnPlannerCore.NewIssue("semantic_test_refusal", "original refusal", "expected", "actual")]);
        var sources = 0;
        WoundAcceptedTurnPlanningResult Refuse() { sources++; return original; }
        var first = fixture.Data.ReadFinalReduction(operation, admittedInput, Refuse);
        var second = fixture.Data.ReadFinalReduction(operation, admittedInput, Refuse);
        Assert.False(first.Success);
        Assert.False(second.Success);
        Assert.Equal(JsonSerializer.Serialize(original.Issues), JsonSerializer.Serialize(second.Issues));
        Assert.Equal(2, sources);
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>
    /// Keeps unaudited ordered intent shapes on their original semantic computation paths.
    /// </summary>
    [Fact]
    public void UnsupportedIntentShapePreservesOriginalSources()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var original = fixture.Resolution;
        var request = original.RequestAuthority;
        MortalWoundTreatmentOperation[] declared = [new MortalWoundStabilizeOperation(), new MortalWoundStabilizeOperation()];
        var intents = declared.Select((value, ordinal) =>
        {
            var fingerprint = MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(ordinal, value);
            return MortalWoundStabilizeOutcomeIntent.Create(ordinal, fingerprint,
                MortalWoundTreatmentOutcomeIntentComposer.IntentFingerprint(request.RequestFingerprint, ordinal, value, fingerprint));
        }).ToArray();
        var unsupported = MortalWoundTreatmentResolution.Create(original.Mode, original.Coordinates,
            original.AttemptDisposition, original.ResultCategory, original.SelectedOutcomeIndex, original.Interruption,
            declared, intents, null, original.ConsumptionTrigger, null, null, null, request,
            original.ModeEvidence, original.RouteFingerprint, original.RouteCompletion);
        var data = new MortalWoundTreatmentSemanticData(fixture.Outcome, unsupported);
        using (fixture.EnterTimingContext())
        {
            for (var i = 0; i < 2; i++)
            {
                Assert.False(data.ReadOutcomeAgreement(operation));
                Assert.Null(data.ReadFinalizationFingerprint(operation));
            }
            Assert.Equal(2, fixture.Count("TreatmentOutcome.AgreesWith"));
            Assert.Equal(2, fixture.Count("TreatmentResources.Finalize"));
            Assert.Equal(0, operation.CaptureState().Entries);
        }
    }

    /// <summary>
    /// Preserves original refusal or exception mapping when an aliased JSON document becomes unavailable after warming.
    /// </summary>
    [Fact]
    public void DisposedAliasedJsonCannotReusePriorAgreement()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var original = fixture.Routes[0];
        using var document = JsonDocument.Parse(original.ResourcePolicy.GetRawText());
        fixture.Routes[0] = original with { ResourcePolicy = document.RootElement };
        using (fixture.EnterTimingContext())
        {
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            document.Dispose();
            var expected = false;
            var actual = false;
            var sourceError = Record.Exception(() => expected = fixture.Outcome.AgreesWith(fixture.Resolution));
            var cachedError = Record.Exception(() => actual = fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(sourceError?.GetType(), cachedError?.GetType());
            Assert.Equal(sourceError?.Message, cachedError?.Message);
            Assert.Equal(expected, actual);
            Assert.Equal(3, fixture.Count("TreatmentOutcome.AgreesWith"));
        }
    }

    /// <summary>
    /// Invalidates agreement when hidden definition facts change while public resolution JSON remains identical.
    /// </summary>
    [Fact]
    public void HiddenDefinitionFactsCannotUseWarmedAgreement()
    {
        using var fixture = new Fixture(_output);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using (fixture.EnterTimingContext())
        {
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            var publicBefore = JsonSerializer.Serialize(fixture.Resolution);
            fixture.DefinitionFacts.Add(new("semantic_extra_fact", "{}", [], false));
            Assert.Equal(publicBefore, JsonSerializer.Serialize(fixture.Resolution));
            var expected = fixture.Outcome.AgreesWith(fixture.Resolution);
            var start = fixture.Count("TreatmentOutcome.AgreesWith");
            Assert.Equal(expected, fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(start + 1, fixture.Count("TreatmentOutcome.AgreesWith"));
            fixture.DefinitionFacts.RemoveAt(fixture.DefinitionFacts.Count - 1);
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(4, fixture.Count("TreatmentOutcome.AgreesWith"));
        }
    }

    /// <summary>
    /// Performs independent semantic computation after a prior whole operation has ended.
    /// </summary>
    [Fact]
    public void NewOperationDoesNotReusePriorOperationResult()
    {
        using var fixture = new Fixture(_output);
        using (fixture.EnterTimingContext())
        {
            using (var first = new DefaultOperationValidationService().BeginOperation())
            {
                Assert.True(fixture.Data.ReadOutcomeAgreement(first));
                Assert.True(fixture.Data.ReadOutcomeAgreement(first));
            }
            using (var second = new DefaultOperationValidationService().BeginOperation())
            {
                Assert.True(fixture.Data.ReadOutcomeAgreement(second));
                Assert.True(fixture.Data.ReadOutcomeAgreement(second));
            }
            Assert.Equal(2, fixture.Count("TreatmentOutcome.AgreesWith"));
        }
    }

    /// <summary>
    /// Reconstructs one detached guaranteed-stabilization request without a registry, world or live owner.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly List<(string Name, string Boundary, double Milliseconds)> _observations = new();
        private readonly string _diagnosticPath;
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Reads immutable fixture INPUT and derives the real resolution, outcome and source observer.
        /// </summary>
        /// <param name="output">
        /// The actual receiver for the complete local diagnostic reference.
        /// </param>
        internal Fixture(ITestOutputHelper output)
        {
            _output = output;
            var root = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "treatment-semantic-guaranteed-v1.json")))!.AsObject();
            var issues = new List<ValidationIssue>();
            Assert.True(MortalWoundTreatmentCommandCodec.TryParseRequest(root["Request"]!.AsObject(),
                "semantic-fixture.request", issues, out var parsed), JsonSerializer.Serialize(issues));
            Assert.NotNull(parsed);
            Symptoms = parsed.RouteSourceWound.Display.VisibleSymptoms.ToList();
            Routes = parsed.RouteSourceWound.Treatment.Routes.ToList();
            DefinitionFacts = parsed.RouteSourceWound.Consequences.OwnedEffectSources.DefinitionFacts.ToList();
            var wound = parsed.RouteSourceWound with
            {
                Display = parsed.RouteSourceWound.Display with { VisibleSymptoms = Symptoms },
                Treatment = parsed.RouteSourceWound.Treatment with { Routes = Routes },
                Consequences = parsed.RouteSourceWound.Consequences with
                {
                    OwnedEffectSources = parsed.RouteSourceWound.Consequences.OwnedEffectSources with
                    {
                        DefinitionFacts = DefinitionFacts
                    }
                }
            };
            var request = MortalWoundTreatmentAttemptRequest.RestoreDetached(parsed.Mode, parsed.Coordinates,
                parsed.MilestoneOrdinal, wound, parsed.RouteSourceWoundFingerprint, parsed.ModeAuthority,
                parsed.RequirementAuthority, parsed.ResourceAuthority, parsed.RequestFingerprint);
            Assert.NotNull(request);
            var result = root["Result"]!.AsObject();
            var declared = new MortalWoundStabilizeOperation();
            var declaredFingerprint = MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(0, declared);
            var intent = MortalWoundStabilizeOutcomeIntent.Create(0, declaredFingerprint,
                MortalWoundTreatmentOutcomeIntentComposer.IntentFingerprint(request.RequestFingerprint, 0,
                    declared, declaredFingerprint));
            var evidence = JsonSerializer.Deserialize<MortalWoundGuaranteedModeEvidence>(
                result["modeEvidence"]!.ToJsonString(), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            Resolution = MortalWoundTreatmentResolution.Create("guaranteed", request.Coordinates,
                "AcceptedTerminal", result["resultCategory"]!.GetValue<string>(), 0, false, [declared], [intent],
                null, result["consumptionTrigger"]!.GetValue<string>(), null, null, null, request, evidence,
                result["routeFingerprint"]!.GetValue<string>(),
                result["routeCompletion"]!.GetValue<string>() == "append_once" ? "AppendOnce" : "None");
            Assert.Equal(result["resultFingerprint"]!.GetValue<string>(), Resolution.ResultFingerprint);
            const long minute = 1000;
            var transition = MortalWoundTreatmentOutcomePublicationPlanner.CreateTransitionId(Resolution);
            var after = MortalWoundTreatmentOutcomePublicationPlanner.CreateScalarShell(wound, request,
                Resolution, transition, minute, [intent]);
            var outcome = new MortalWoundTreatmentOutcomePreparation(wound, after, transition, null,
                request.RequestFingerprint, Resolution.ResultFingerprint, Resolution.ResolutionAuthorityFingerprint,
                [intent], Resolution.RouteCompletion, Resolution.ResultCategory, 0, minute,
                MortalWoundTreatmentOutcomePublicationPlanner.ComputePreparationFingerprint(wound, after,
                    Resolution, transition, minute, null), Resolution);
            Data = new(outcome, Resolution);
            Outcome = outcome;
            var nonce = Guid.NewGuid().ToString("N");
            var diagnostics = Path.Combine(Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT") ??
                @"D:\BoeDiagnostics\semantic-cache-r56-20261023", "treatment-semantic-unit");
            Directory.CreateDirectory(diagnostics);
            _diagnosticPath = Path.Combine(diagnostics, nonce + ".jsonl");
        }

        /// <summary>
        /// Selects the complete passive observation receiver without creating a game directory or filesystem owner.
        /// </summary>
        /// <returns>
        /// A restoring synchronous timing scope that must not span an await.
        /// </returns>
        internal IDisposable? EnterTimingContext() => FileSystemManager.EnterOperationTimingContext(
            (name, boundary, elapsed) =>
            {
                _observations.Add((name, boundary, elapsed.TotalMilliseconds));
                OnObservation?.Invoke(name, boundary);
            });

        /// <summary>
        /// Gets or sets the optional typed mutation control invoked after retaining each original observation.
        /// </summary>
        internal Action<string, string>? OnObservation { get; set; }

        /// <summary>
        /// Gets the real detached resolution reconstructed from immutable INPUT.
        /// </summary>
        internal MortalWoundTreatmentResolution Resolution { get; }

        /// <summary>
        /// Gets the production semantic evaluator used by the source-count controls.
        /// </summary>
        internal MortalWoundTreatmentSemanticData Data { get; }

        /// <summary>
        /// Gets the real retained outcome for uncached refusal parity controls.
        /// </summary>
        internal MortalWoundTreatmentOutcomePreparation Outcome { get; }

        /// <summary>
        /// Gets caller-owned route entries whose hidden provenance can change independently of public JSON.
        /// </summary>
        internal List<WoundTreatmentRoute> Routes { get; }

        /// <summary>
        /// Gets caller-owned hidden definition facts omitted from the public resolution JSON.
        /// </summary>
        internal List<WoundOwnedEffectDefinitionFact> DefinitionFacts { get; }

        /// <summary>
        /// Gets the deliberately aliased caller-owned collection retained by the restored request.
        /// </summary>
        internal List<string> Symptoms { get; }

        /// <summary>
        /// Counts every actual entry into a named original source.
        /// </summary>
        /// <param name="name">
        /// The exact original operation timer name.
        /// </param>
        /// <returns>
        /// The complete number of recorded begin observations for that name.
        /// </returns>
        internal int Count(string name) => _observations.Count(row => row.Name == name && row.Boundary == "begin");

        /// <summary>
        /// Saves every recorded diagnostic row outside Git without a record or byte quota.
        /// </summary>
        public void Dispose()
        {
            File.WriteAllLines(_diagnosticPath, _observations.Select(row =>
                JsonSerializer.Serialize(new { row.Name, row.Boundary, row.Milliseconds })));
            var bytes = File.ReadAllBytes(_diagnosticPath);
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Boundary = "semantic_data_diagnostics", Path = _diagnosticPath, Bytes = bytes.Length,
                SHA256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), Complete = true
            }));
        }
    }
}
