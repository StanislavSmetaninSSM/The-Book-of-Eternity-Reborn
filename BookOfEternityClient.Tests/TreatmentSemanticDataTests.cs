using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Executes real treatment agreement and finalization over detached file-free game DATA.
/// </summary>
public sealed class TreatmentSemanticDataTests
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
        using (fixture.Files.EnterOperationTimingContext())
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
        using (fixture.Files.EnterOperationTimingContext())
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
        using (fixture.Files.EnterOperationTimingContext())
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
    /// Performs independent semantic computation after a prior whole operation has ended.
    /// </summary>
    [Fact]
    public void NewOperationDoesNotReusePriorOperationResult()
    {
        using var fixture = new Fixture(_output);
        using (fixture.Files.EnterOperationTimingContext())
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
            var wound = parsed.RouteSourceWound with
            {
                Display = parsed.RouteSourceWound.Display with { VisibleSymptoms = Symptoms }
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
            var nonce = Guid.NewGuid().ToString("N");
            var diagnostics = Path.Combine(Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT") ??
                @"D:\BoeDiagnostics\semantic-cache-r56-20261023", "treatment-semantic-unit");
            Directory.CreateDirectory(diagnostics);
            _diagnosticPath = Path.Combine(diagnostics, nonce + ".jsonl");
            Files = new FileSystemManager(Path.Combine(Path.GetTempPath(), "boe-semantic-unit-" + nonce),
                NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, boundary, elapsed) =>
                        _observations.Add((name, boundary, elapsed.TotalMilliseconds))
                });
        }

        /// <summary>
        /// Gets the real manager used solely for the synchronous passive timing context.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets the real detached resolution reconstructed from immutable INPUT.
        /// </summary>
        internal MortalWoundTreatmentResolution Resolution { get; }

        /// <summary>
        /// Gets the production semantic evaluator used by the source-count controls.
        /// </summary>
        internal MortalWoundTreatmentSemanticData Data { get; }

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
