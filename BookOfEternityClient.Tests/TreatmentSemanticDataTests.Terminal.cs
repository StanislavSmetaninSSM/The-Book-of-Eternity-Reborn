using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class TreatmentSemanticDataTests
{
    /// <summary>Repeated terminal agreement uses one real source within the same operation.</summary>
    [Fact]
    public void TerminalAgreement_EightRequestsExecuteActualSourceOnce()
    {
        var fixture = new TerminalFixture();
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using (fixture.EnterTimingContext())
        {
            for (var i = 0; i < 8; i++) Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(1, fixture.AgreementComputations);
            Assert.Equal(2, fixture.GraphCompilations);
        }
    }

    /// <summary>Empty-resource terminal finalization retains only its checked fingerprint DATA.</summary>
    [Fact]
    public void TerminalFinalization_EightRequestsExecuteActualSourceOnce()
    {
        var fixture = new TerminalFixture();
        var expected = MortalWoundTreatmentResourceComposer.Finalize(fixture.Resolution).Finalization?.FinalizationFingerprint;
        Assert.NotNull(expected);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using (fixture.EnterTimingContext())
        {
            for (var index = 0; index < 8; index++)
                Assert.Equal(expected, fixture.Data.ReadFinalizationFingerprint(operation));
            Assert.Equal(1, fixture.FinalizationComputations);
        }
    }

    /// <summary>All mutable public and hidden wound inputs invalidate, including restoration.</summary>
    [Theory]
    [InlineData("symptoms")]
    [InlineData("hidden-facts")]
    [InlineData("route-provenance")]
    public void TerminalAgreement_MutationAndRestorationExecuteFreshSource(string mutation)
    {
        var fixture = new TerminalFixture();
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var route = fixture.Routes[0];
        using (fixture.EnterTimingContext())
        {
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            var publicBefore = JsonSerializer.Serialize(fixture.Resolution);
            if (mutation == "symptoms") fixture.Symptoms.Add("changed terminal wound");
            if (mutation == "hidden-facts") fixture.DefinitionFacts.Add(new("extra", "{}", [], false));
            if (mutation == "route-provenance") fixture.Routes[0] = route with { SourcePath = route.SourcePath + ".changed" };
            if (mutation != "symptoms") Assert.Equal(publicBefore, JsonSerializer.Serialize(fixture.Resolution));
            var expected = fixture.Outcome.AgreesWith(fixture.Resolution);
            Assert.Equal(expected, fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(3, fixture.AgreementComputations);
            if (mutation == "symptoms") fixture.Symptoms.RemoveAt(fixture.Symptoms.Count - 1);
            if (mutation == "hidden-facts") fixture.DefinitionFacts.RemoveAt(fixture.DefinitionFacts.Count - 1);
            if (mutation == "route-provenance") fixture.Routes[0] = route;
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(4, fixture.AgreementComputations);
        }
    }

    /// <summary>An arbitrary observer after warming executes its original mutation before agreement.</summary>
    [Fact]
    public void TerminalAgreement_ObserverAfterWarmingUsesOriginalSource()
    {
        var fixture = new TerminalFixture();
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using (fixture.EnterTimingContext()) Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
        var callbacks = 0;
        using (FileSystemManager.EnterOperationTimingContext((name, boundary, _) =>
        {
            if (name == "TreatmentOutcome.AgreesWith" && boundary == "begin")
            {
                callbacks++;
                fixture.Symptoms.Add("observer changes the actual input");
            }
        }))
        {
            Assert.False(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(1, callbacks);
            fixture.Symptoms.RemoveAt(fixture.Symptoms.Count - 1);
            Assert.False(fixture.Outcome.AgreesWith(fixture.Resolution));
            Assert.Equal(2, callbacks);
        }
    }

    /// <summary>Null, force-fresh and new operations retain original source behavior.</summary>
    [Fact]
    public void TerminalAgreement_OperationBoundariesRemainFresh()
    {
        var fixture = new TerminalFixture();
        using (fixture.EnterTimingContext())
        {
            Assert.True(fixture.Data.ReadOutcomeAgreement(null));
            Assert.True(fixture.Data.ReadOutcomeAgreement(null));
            using (var operation = new DefaultOperationValidationService().BeginOperation())
            {
                Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
                Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
                Assert.Equal(3, fixture.AgreementComputations);
                using (operation.ForceFresh())
                {
                    Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
                    Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
                }
                Assert.Equal(5, fixture.AgreementComputations);
            }
            using (var operation = new DefaultOperationValidationService().BeginOperation())
            {
                Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
                Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            }
            Assert.Equal(6, fixture.AgreementComputations);
        }
    }

    /// <summary>Value-equal foreign companions cannot qualify for retained-alias reuse.</summary>
    [Fact]
    public void TerminalAgreement_ForeignPairUsesOriginalSource()
    {
        var first = new TerminalFixture();
        var second = new TerminalFixture();
        var data = new MortalWoundTreatmentSemanticData(first.Outcome, second.Resolution);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var expected = first.Outcome.AgreesWith(second.Resolution);
        using (first.EnterTimingContext())
        {
            Assert.Equal(expected, data.ReadOutcomeAgreement(operation));
            Assert.Equal(expected, data.ReadOutcomeAgreement(operation));
            Assert.Equal(2, first.AgreementComputations);
            Assert.Equal(0, operation.CaptureState().Entries);
        }
    }

    /// <summary>Terminal scalar support does not widen preparation replay eligibility.</summary>
    [Fact]
    public void TerminalAgreement_StageReplayPolicyRemainsUnsupported()
    {
        var fixture = new TerminalFixture();
        using var operation = new DefaultOperationValidationService().BeginOperation();
        Assert.False(fixture.Data.CanCapturePreparationProjection);

        var sources = 0;
        ExpectedPreparationReplayProjection? Refuse() { sources++; return null; }
        fixture.Data.ReadPreparationProjection(operation, new OperationStructuralSnapshot(["terminal"], []), Refuse);
        fixture.Data.ReadPreparationProjection(operation, new OperationStructuralSnapshot(["terminal"], []), Refuse);
        Assert.Equal(2, sources);
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>Disposal of aliased JSON preserves the original refusal or exception.</summary>
    [Fact]
    public void TerminalAgreement_DisposedRouteJsonPreservesSourceFailure()
    {
        var fixture = new TerminalFixture();
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var route = fixture.Routes[0];
        using var document = JsonDocument.Parse(route.ResourcePolicy.GetRawText());
        fixture.Routes[0] = route with { ResourcePolicy = document.RootElement };
        using (fixture.EnterTimingContext())
        {
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            document.Dispose();
            var expected = false; var actual = false;
            var sourceError = Record.Exception(() => expected = fixture.Outcome.AgreesWith(fixture.Resolution));
            var cachedError = Record.Exception(() => actual = fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(sourceError?.GetType(), cachedError?.GetType());
            Assert.Equal(sourceError?.Message, cachedError?.Message);
            Assert.Equal(expected, actual);
            Assert.Equal(3, fixture.AgreementComputations);
        }
    }

    /// <summary>Non-polymorphic legacy payload JSON remains a dependency after warming.</summary>
    [Theory]
    [InlineData("definition")]
    [InlineData("application")]
    public void TerminalAgreement_DisposedMechanicalLegacyPayloadCannotReuse(string payload)
    {
        var fixture = new TerminalFixture(payload);
        using var document = fixture.AliasedDocument!;
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using (fixture.EnterTimingContext())
        {
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.True(fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(1, fixture.AgreementComputations);
            document.Dispose();
            var expected = false; var actual = false;
            var sourceError = Record.Exception(() => expected = fixture.Outcome.AgreesWith(fixture.Resolution));
            var cachedError = Record.Exception(() => actual = fixture.Data.ReadOutcomeAgreement(operation));
            Assert.Equal(sourceError?.GetType(), cachedError?.GetType());
            Assert.Equal(sourceError?.Message, cachedError?.Message);
            Assert.Equal(expected, actual);
            Assert.Equal(3, fixture.AgreementComputations);
        }
    }

    /// <summary>Replacement operations/intents and unsupported shape retain original source computation.</summary>
    [Theory]
    [InlineData("operation")]
    [InlineData("intent")]
    [InlineData("unsupported")]
    public void TerminalAgreement_ReplacedCompanionCannotReuse(string replacement)
    {
        var fixture = new TerminalFixture();
        var original = fixture.Resolution;
        var declared = original.DeclaredResult.ToArray();
        var intents = original.OutcomeIntents;
        if (replacement == "operation") declared[0] = new MortalWoundReduceSeverityOperation(1);
        if (replacement == "unsupported") declared[0] = new MortalWoundReduceSeverityOperation(3);
        if (replacement is "intent" or "unsupported")
        {
            Assert.True(MortalWoundTreatmentOutcomeIntentComposer.TryComposeDetached(original.RequestAuthority,
                declared, "terminal-fixture-binding", "terminal-fixture", out var recomposed, out _));
            intents = recomposed;
        }
        var resolution = MortalWoundTreatmentResolution.Create(original.Mode, original.Coordinates,
            original.AttemptDisposition, original.ResultCategory, original.SelectedOutcomeIndex,
            original.Interruption, declared, intents, null, original.ConsumptionTrigger,
            original.CourseId, original.CourseMilestoneOrdinal, original.CourseDisposition,
            original.RequestAuthority, original.ModeEvidence, original.RouteFingerprint, original.RouteCompletion);
        var data = new MortalWoundTreatmentSemanticData(fixture.Outcome, resolution);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var expected = fixture.Outcome.AgreesWith(resolution);
        using (fixture.EnterTimingContext())
        {
            Assert.Equal(expected, data.ReadOutcomeAgreement(operation));
            Assert.Equal(expected, data.ReadOutcomeAgreement(operation));
            Assert.Equal(2, fixture.AgreementComputations);
            Assert.Equal(0, operation.CaptureState().Entries);
        }
    }

    /// <summary>Reconstructs only detached terminal INPUT through the real strict codec and composer.</summary>
    private sealed class TerminalFixture
    {
        internal TerminalFixture(string? aliasedPayload = null)
        {
            var input = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "treatment-semantic-terminal-v1.json")))!.AsObject();
            var issues = new List<ValidationIssue>();
            Assert.True(MortalWoundTreatmentCommandCodec.TryParseHistoryResult(
                JsonSerializer.SerializeToElement(input["Result"]), "terminal-fixture", issues,
                out var request, out var receipt, out _), JsonSerializer.Serialize(issues));
            Assert.NotNull(request); Assert.NotNull(receipt);
            Symptoms = request.RouteSourceWound.Display.VisibleSymptoms.ToList();
            Routes = request.RouteSourceWound.Treatment.Routes.ToList();
            DefinitionFacts = request.RouteSourceWound.Consequences.OwnedEffectSources.DefinitionFacts.ToList();
            var wound = request.RouteSourceWound with
            {
                Display = request.RouteSourceWound.Display with { VisibleSymptoms = Symptoms },
                Treatment = request.RouteSourceWound.Treatment with { Routes = Routes },
                Consequences = request.RouteSourceWound.Consequences with
                {
                    OwnedEffectSources = request.RouteSourceWound.Consequences.OwnedEffectSources with
                    { DefinitionFacts = DefinitionFacts }
                }
            };
            request = MortalWoundTreatmentAttemptRequest.RestoreDetached(request.Mode, request.Coordinates,
                request.MilestoneOrdinal, wound, request.RouteSourceWoundFingerprint, request.ModeAuthority,
                request.RequirementAuthority, request.ResourceAuthority, request.RequestFingerprint);
            Assert.NotNull(request);
            IReadOnlyList<MortalWoundTreatmentOperation> declared = receipt.DeclaredResult;
            if (aliasedPayload is not null)
            {
                var heal = Assert.IsType<MortalWoundHealOperation>(declared[2]);
                var ordinal = Enumerable.Range(0, heal.Legacies.Length).First(index => heal.Legacies[index] is MortalWoundMechanicalEffectLegacyDraft);
                var legacy = Assert.IsType<MortalWoundMechanicalEffectLegacyDraft>(heal.Legacies[ordinal]);
                var effect = legacy.EffectDraft;
                if (aliasedPayload == "definition")
                {
                    var definition = effect.Definitions[0];
                    AliasedDocument = JsonDocument.Parse(definition.Definition.GetRawText());
                    effect = effect with { Definitions = effect.Definitions.SetItem(0,
                        definition with { Definition = AliasedDocument.RootElement }) };
                }
                else
                {
                    var application = effect.Applications[0];
                    AliasedDocument = JsonDocument.Parse(application.Parameters.GetRawText());
                    effect = effect with { Applications = effect.Applications.SetItem(0,
                        application with { Parameters = AliasedDocument.RootElement }) };
                }
                declared = [declared[0], declared[1], heal with
                { Legacies = heal.Legacies.SetItem(ordinal, legacy with { EffectDraft = effect }) }];
            }
            Assert.True(MortalWoundTreatmentOutcomeIntentComposer.TryComposeDetached(request,
                declared, "terminal-fixture-binding", "terminal-fixture", out var intents,
                out var intentIssues), JsonSerializer.Serialize(intentIssues));
            Resolution = MortalWoundTreatmentResolution.Create(receipt.Mode, request.Coordinates,
                receipt.AttemptDisposition, receipt.ResultCategory, receipt.SelectedOutcomeIndex,
                receipt.Interruption, declared, intents, null, receipt.ConsumptionTrigger,
                receipt.CourseId, receipt.CourseMilestoneOrdinal, receipt.CourseDisposition,
                request, receipt.ModeEvidence, receipt.RouteFingerprint, receipt.RouteCompletion);
            Assert.Equal(receipt.ResultFingerprint, Resolution.ResultFingerprint);
            var prepared = MortalWoundTreatmentOutcomePublicationPlanner.RecomposeDetached(request,
                Resolution, input["CurrentGameMinute"]!.GetValue<long>());
            Assert.True(prepared.IsValid, JsonSerializer.Serialize(prepared.Issues));
            Outcome = Assert.IsType<MortalWoundTreatmentOutcomePreparation>(prepared.Preparation);
            Assert.True(Outcome.AgreesWith(Resolution));
            Data = new(Outcome, Resolution);
        }

        internal JsonDocument? AliasedDocument { get; }
        internal MortalWoundTreatmentResolution Resolution { get; }
        internal MortalWoundTreatmentOutcomePreparation Outcome { get; }
        internal MortalWoundTreatmentSemanticData Data { get; }
        internal List<string> Symptoms { get; }
        internal List<WoundTreatmentRoute> Routes { get; }
        internal List<WoundOwnedEffectDefinitionFact> DefinitionFacts { get; }
        private readonly OperationTimingAccumulator _recorder = new();
        private OperationTimingAccumulator.Window? _window;
        internal long AgreementComputations => _window?.CaptureNamedCounts("TreatmentOutcome.AgreesWith").Started ?? 0;
        internal long GraphCompilations => _window?.CaptureNamedCounts("TreatmentOutcome.CompileSelectedGraph").Started ?? 0;
        internal long FinalizationComputations => _window?.CaptureNamedCounts("TreatmentResources.Finalize").Started ?? 0;
        internal IDisposable EnterTimingContext()
        {
            _window = _recorder.StartWindow();
            return new TimingScope(FileSystemManager.EnterOperationTimingContext(null, _recorder), _window);
        }
        private sealed class TimingScope(IDisposable? context, IDisposable window) : IDisposable
        {
            public void Dispose() { context?.Dispose(); window.Dispose(); }
        }
    }
}
