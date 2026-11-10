using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class TreatmentSemanticDataTests
{
    /// <summary>The exact typed resource contour explicitly excludes held authorities.</summary>
    [Fact]
    public void TerminalFinalization_EmptyResourceGuardRejectsHeldAuthority()
    {
        var terminal = new TerminalFixture();
        Assert.True(MortalWoundTreatmentSemanticData.HasEmptyTerminalResources(terminal.Resolution.ResourceAuthority));
        var input = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "treatment-semantic-guaranteed-v1.json")))!.AsObject();
        var issues = new List<ValidationIssue>();
        Assert.True(MortalWoundTreatmentCommandCodec.TryParseRequest(input["Request"]!.AsObject(),
            "held-finalization-guard", issues, out var request), JsonSerializer.Serialize(issues));
        Assert.NotNull(request);
        Assert.Equal("held", request.ResourceAuthority.ReservationDisposition);
        Assert.NotEmpty(request.ResourceAuthority.Claims);
        Assert.False(MortalWoundTreatmentSemanticData.HasEmptyTerminalResources(request.ResourceAuthority));
    }

    /// <summary>Public and hidden request mutations invalidate checked finalization DATA.</summary>
    [Theory]
    [InlineData("symptoms")]
    [InlineData("hidden-facts")]
    [InlineData("route-provenance")]
    [InlineData("resource-policy")]
    public void TerminalFinalization_MutationAndRestorationPreserveOriginalResult(string mutation)
    {
        var fixture = new TerminalFixture();
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var route = fixture.Routes[0];
        using (fixture.EnterTimingContext())
        {
            var original = fixture.Data.ReadFinalizationFingerprint(operation);
            Assert.NotNull(original);
            Assert.Equal(original, fixture.Data.ReadFinalizationFingerprint(operation));
            Assert.Equal(1, fixture.FinalizationComputations);
            if (mutation == "symptoms") fixture.Symptoms.Add("changed finalization input");
            if (mutation == "hidden-facts") fixture.DefinitionFacts.Add(new("extra", "{}", [], false));
            if (mutation == "route-provenance") fixture.Routes[0] = route with { SourcePath = route.SourcePath + ".changed" };
            if (mutation == "resource-policy")
            {
                var policy = JsonNode.Parse(route.ResourcePolicy.GetRawText())!.AsObject();
                policy["consumeOn"] = new JsonArray();
                fixture.Routes[0] = route with { ResourcePolicy = JsonSerializer.SerializeToElement(policy) };
            }
            var expected = MortalWoundTreatmentResourceComposer.Finalize(fixture.Resolution).Finalization?.FinalizationFingerprint;
            Assert.Equal(expected, fixture.Data.ReadFinalizationFingerprint(operation));
            Assert.Equal(3, fixture.FinalizationComputations);
            if (mutation == "symptoms") fixture.Symptoms.RemoveAt(fixture.Symptoms.Count - 1);
            if (mutation == "hidden-facts") fixture.DefinitionFacts.RemoveAt(fixture.DefinitionFacts.Count - 1);
            if (mutation is "route-provenance" or "resource-policy") fixture.Routes[0] = route;
            Assert.Equal(original, fixture.Data.ReadFinalizationFingerprint(operation));
            Assert.Equal(4, fixture.FinalizationComputations);
        }
    }

    /// <summary>Unavailable aliased JSON falls back to the original result or refusal on every attempt.</summary>
    [Theory]
    [InlineData("definition")]
    [InlineData("application")]
    [InlineData("route")]
    public void TerminalFinalization_DisposedJsonKeepsOriginalRefusal(string payload)
    {
        var fixture = new TerminalFixture(payload == "route" ? null : payload);
        using var document = payload == "route"
            ? JsonDocument.Parse(fixture.Routes[0].ResourcePolicy.GetRawText()) : fixture.AliasedDocument!;
        if (payload == "route") fixture.Routes[0] = fixture.Routes[0] with { ResourcePolicy = document.RootElement };
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using (fixture.EnterTimingContext())
        {
            Assert.NotNull(fixture.Data.ReadFinalizationFingerprint(operation));
            document.Dispose();
            var expected = MortalWoundTreatmentResourceComposer.Finalize(fixture.Resolution).Finalization?.FinalizationFingerprint;
            Assert.Equal(expected, fixture.Data.ReadFinalizationFingerprint(operation));
            Assert.Equal(expected, fixture.Data.ReadFinalizationFingerprint(operation));
            Assert.Equal(4, fixture.FinalizationComputations);
        }
    }

    /// <summary>Arbitrary observers cannot be suppressed by a warmed resource fingerprint.</summary>
    [Fact]
    public void TerminalFinalization_ObserverAfterWarmingUsesOriginalSource()
    {
        var fixture = new TerminalFixture();
        using var operation = new DefaultOperationValidationService().BeginOperation();
        using (fixture.EnterTimingContext()) Assert.NotNull(fixture.Data.ReadFinalizationFingerprint(operation));
        var callbacks = 0;
        using (FileSystemManager.EnterOperationTimingContext((name, boundary, _) =>
        {
            if (name == "TreatmentResources.Finalize" && boundary == "begin")
            {
                callbacks++;
                fixture.Symptoms.Add("changed at original finalization source");
            }
        }))
        {
            Assert.Null(fixture.Data.ReadFinalizationFingerprint(operation));
            Assert.Equal(1, callbacks);
            fixture.Symptoms.RemoveAt(fixture.Symptoms.Count - 1);
            Assert.Null(MortalWoundTreatmentResourceComposer.Finalize(fixture.Resolution).Finalization);
            Assert.Equal(2, callbacks);
        }
    }

    /// <summary>Null, fresh, disposed and new operations cannot reuse a previous operation's fingerprint.</summary>
    [Fact]
    public void TerminalFinalization_OperationBoundariesRemainFresh()
    {
        var fixture = new TerminalFixture();
        using (fixture.EnterTimingContext())
        {
            Assert.NotNull(fixture.Data.ReadFinalizationFingerprint(null));
            Assert.NotNull(fixture.Data.ReadFinalizationFingerprint(null));
            var operation = new DefaultOperationValidationService().BeginOperation();
            using (operation)
            {
                Assert.NotNull(fixture.Data.ReadFinalizationFingerprint(operation));
                Assert.NotNull(fixture.Data.ReadFinalizationFingerprint(operation));
                Assert.Equal(3, fixture.FinalizationComputations);
                using (operation.ForceFresh())
                {
                    Assert.NotNull(fixture.Data.ReadFinalizationFingerprint(operation));
                    Assert.NotNull(fixture.Data.ReadFinalizationFingerprint(operation));
                }
                Assert.Equal(5, fixture.FinalizationComputations);
            }
            Assert.ThrowsAny<InvalidOperationException>(() => fixture.Data.ReadFinalizationFingerprint(operation));
            using var next = new DefaultOperationValidationService().BeginOperation();
            Assert.NotNull(fixture.Data.ReadFinalizationFingerprint(next));
            Assert.NotNull(fixture.Data.ReadFinalizationFingerprint(next));
            Assert.Equal(6, fixture.FinalizationComputations);
        }
    }

    /// <summary>A foreign terminal companion retains original computation even with value-equal DATA.</summary>
    [Fact]
    public void TerminalFinalization_ForeignCompanionUsesOriginalSource()
    {
        var first = new TerminalFixture();
        var second = new TerminalFixture();
        var data = new MortalWoundTreatmentSemanticData(first.Outcome, second.Resolution);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var expected = MortalWoundTreatmentResourceComposer.Finalize(second.Resolution).Finalization?.FinalizationFingerprint;
        using (first.EnterTimingContext())
        {
            Assert.Equal(expected, data.ReadFinalizationFingerprint(operation));
            Assert.Equal(expected, data.ReadFinalizationFingerprint(operation));
            Assert.Equal(2, first.FinalizationComputations);
        }
    }
}
