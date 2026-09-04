using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData("session")]
    [InlineData("request")]
    [InlineData("snapshot")]
    public void TreatmentCommand_RecomposeRejectsDraftSidecarBindingDivergence(
        string axis)
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var command = ComposeTreatmentCommand(
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_sidecar_" + axis,
                scenario.RouteId),
            "The exact treatment draft binding remains sealed.");
        var draft = Assert.Single(command.Parsed.TreatmentCommands);
        var tampered = axis switch
        {
            "session" => draft with { SessionId = "foreign_session_1536" },
            "request" => draft with { RequestId = "foreign_request_1536" },
            "snapshot" => draft with { SnapshotToken = "foreign_snapshot_1536" },
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };

        Assert.Null(MortalWoundTreatmentCommandCodec.RecomposeRoot(
            command.Binding,
            new[] { tampered }));
    }

    [Fact]
    public void PersistedCatalog_RejectsMixedOpportunityAndTreatmentCommandFamilies()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        const string scene = "One accepted response cannot mix wound command families.";
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_mixed_family",
            scenario.RouteId);
        var treatment = ComposeTreatmentCommand(flow, scene);
        var (ordinaryBinding, opportunity) =
            CreateTreatmentRepairOpportunity(treatment.Binding);
        var decision = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["opportunityRef"] = opportunity.PublicRef,
            ["decision"] = "none"
        });
        var ordinary = WoundResponseInputComposer.Compose(
            ordinaryBinding,
            new[] { opportunity },
            new[] { decision },
            scene,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(ordinary.Success, DescribeIssues(ordinary.Issues));
        var ordinaryRoot = Assert.IsType<JsonObject>(ordinary.CommandRoot);
        var ordinaryRow = Assert.IsType<JsonObject>(Assert.Single(
            ordinaryRoot["commands"]!.AsArray()));
        var mixed = treatment.Root.DeepClone().AsObject();
        mixed["commands"]!.AsArray().Add(ordinaryRow.DeepClone());
        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(mixed));
        Assert.True(parsed.Success, DescribeIssues(parsed.Issues));
        Assert.Single(parsed.Commands);
        Assert.Single(parsed.TreatmentCommands);

        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(mixed, null, flow.History),
            "mixed opportunity/treatment command families");
    }

    [Fact]
    public void PersistedCatalog_RejectsConfusableTreatmentOperationCoordinates()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var latinOperationKey = scenario.OperationKey + "_confusable_A";
        var cyrillicOperationKey = scenario.OperationKey + "_confusable_\u0410";
        Assert.NotEqual(latinOperationKey, cyrillicOperationKey);
        Assert.Equal(
            ExactIdentifierConfusableKey.Build(latinOperationKey),
            ExactIdentifierConfusableKey.Build(cyrillicOperationKey));
        const string scene = "Confusable operation coordinates cannot share one batch.";
        var first = ResolveCurrentTreatment(
            fixture,
            "procedure",
            latinOperationKey,
            scenario.RouteId);
        var second = ResolveCurrentTreatment(
            fixture,
            "procedure",
            cyrillicOperationKey,
            scenario.RouteId);
        var firstCommand = ComposeTreatmentCommand(first, scene);
        var secondCommand = ComposeTreatmentCommand(second, scene);
        var combined = firstCommand.Root.DeepClone().AsObject();
        combined["commands"]!.AsArray().Add(
            Assert.Single(secondCommand.Root["commands"]!.AsArray())!.DeepClone());
        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(combined));
        Assert.False(parsed.Success);
        Assert.Contains(parsed.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_persisted_coordinate_collision",
            StringComparison.Ordinal));

        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(combined, null, first.History),
            "confusable persisted treatment operation coordinates");
    }

    [Fact]
    public void PersistedCatalog_RejectsDistinctTreatmentsForTheSameAcceptedEvent()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        const string scene = "One accepted event cannot advance a wound twice.";
        var first = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_event_first",
            scenario.RouteId);
        var second = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_event_second",
            scenario.RouteId);
        var firstCommand = ComposeTreatmentCommand(first, scene);
        var secondCommand = ComposeTreatmentCommand(second, scene);
        Assert.Equal(
            Assert.IsType<MortalWoundTreatmentAttemptRequest>(first.Request)
                .Coordinates.EventRef,
            Assert.IsType<MortalWoundTreatmentAttemptRequest>(second.Request)
                .Coordinates.EventRef);
        var combined = firstCommand.Root.DeepClone().AsObject();
        combined["commands"]!.AsArray().Add(
            Assert.Single(secondCommand.Root["commands"]!.AsArray())!.DeepClone());

        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(combined, null, first.History),
            "distinct treatment requests for one accepted event");
    }

    [Fact]
    public void PersistedCatalog_RejectsTreatmentRepairWaveMissingItsSubmittedCopy()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_missing_pending_copy",
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            "A treatment repair wave must retain its submitted request.");
        var pending = ComposeTreatmentRepairPendingRoot(
            command,
            CreateTreatmentRepairPackets(command.Binding, scenario.Before));
        Assert.True(pending.Remove(FindJsonPropertyName(
            pending,
            "SubmittedTreatmentRequests")));

        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(command.Root, pending, flow.History),
            "treatment repair wave missing its submitted copy");
    }

    [Fact]
    public void PersistedCatalog_RejectsOpportunityCommandWithForeignTreatmentPendingCopy()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        const string scene = "The repair wave remains bound to its command family.";
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_foreign_pending_copy",
            scenario.RouteId);
        var treatment = ComposeTreatmentCommand(flow, scene);
        var pending = ComposeTreatmentRepairPendingRoot(
            treatment,
            CreateTreatmentRepairPackets(treatment.Binding, scenario.Before));
        var (ordinaryBinding, opportunity) =
            CreateTreatmentRepairOpportunity(treatment.Binding);
        var ordinary = WoundResponseInputComposer.Compose(
            ordinaryBinding,
            new[] { opportunity },
            new[]
            {
                JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["opportunityRef"] = opportunity.PublicRef,
                    ["decision"] = "none"
                })
            },
            scene,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(ordinary.Success, DescribeIssues(ordinary.Issues));
        var ordinaryRoot = Assert.IsType<JsonObject>(ordinary.CommandRoot);

        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(ordinaryRoot, pending, flow.History),
            "opportunity command with a foreign submitted treatment copy");
    }

    [Fact]
    public void PersistedCatalog_RejectsExactTreatmentDuplicateInsideCommandOrigin()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_duplicate_command",
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            "One command origin cannot repeat a treatment request.");
        var duplicated = command.Root.DeepClone().AsObject();
        var rows = duplicated["commands"]!.AsArray();
        rows.Add(Assert.Single(rows)!.DeepClone());

        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(duplicated, null, flow.History),
            "exact treatment duplicate inside command origin");
    }

    [Fact]
    public void PersistedCatalog_RejectsExactTreatmentDuplicateInsidePendingOrigin()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_duplicate_pending",
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            "One pending origin cannot repeat a treatment request.");
        var pending = ComposeTreatmentRepairPendingRoot(
            command,
            CreateTreatmentRepairPackets(command.Binding, scenario.Before));
        var submitted = pending[FindJsonPropertyName(
            pending,
            "SubmittedTreatmentRequests")]!.AsArray();
        submitted.Add(Assert.Single(submitted)!.DeepClone());

        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(null, pending, flow.History),
            "exact treatment duplicate inside pending origin");
    }

}
