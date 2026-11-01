using System.Collections.Immutable;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData("procedure", false)]
    [InlineData("course", false)]
    [InlineData("guaranteed", false)]
    [InlineData("guaranteed", true)]
    public async Task LegalTerminalHealReachesActualTreatmentPublication(string mode, bool removeThenHeal)
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, physicalMode: mode,
            preparePhysicalWound: wound => PrepareTerminalPhysicalWound(wound, mode, removeThenHeal));
        var files = fixture.Files;
        Assert.True(fixture.Authority.HasCurrentAdmissionAuthority());
        var before = fixture.Authority.CurrentWound;
        Assert.Equal("active", before.Lifecycle); Assert.Equal(1, before.Severity.Rank);
        Assert.NotEmpty(before.Consequences.OwnedEffectSources.RootBindings);
        var parsed = WoundMaterializationContract.Parse(WoundMaterializationContract.SerializeCanonical(before), "terminal.initial");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var input = fixture.Authority.ReadGuidedTreatmentOriginalInput();
        var frame = MortalWoundGuidedTreatmentInputCodec.Read(input).Frame;
        var admitted = await WoundApplicationService.ResolveMortalTreatmentInputAsync(files, fixture.Lease, frame);
        var request = admitted.Request;
        Assert.Equal(mode, request.Mode); Assert.Equal("treatment", request.Coordinates.Purpose);
        Assert.Equal(mode == "course" ? 1 : (int?)null, request.MilestoneOrdinal);
        Assert.True(MortalWoundTreatmentDetachedSealValidator.IsValid(request));
        if (request.ModeAuthority is MortalWoundProcedureCheckAuthority check)
        {
            Assert.Equal(new[] { 20 }, check.SourceRolls);
            Assert.True(admitted.Owner.HasLiveProcedureReservationAgreement(check));
        }
        var history = new WoundHistoryParseResult(admitted.Owner.History, []);
        var reduced = mode switch
        {
            "procedure" => MortalWoundTreatmentPlanner.CreateProcedureAttempt(request, history, admitted.Owner.CurrentWound, admitted.Owner),
            "course" => MortalWoundTreatmentPlanner.CreateCourseMilestoneAttempt(request, history, admitted.Owner.CurrentWound, admitted.Owner),
            _ => MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, admitted.Owner.CurrentWound, admitted.Owner)
        };
        Assert.Equal("Resolved", reduced.Disposition);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(reduced.Resolution);
        Assert.Equal("AcceptedTerminal", resolution.AttemptDisposition); Assert.Equal("success", resolution.ResultCategory);
        Assert.IsType<MortalWoundHealOperation>(resolution.DeclaredResult[^1]);
        Assert.IsType<MortalWoundHealOutcomeIntent>(resolution.OutcomeIntents[^1]);
        Assert.Equal(removeThenHeal ? 2 : 1, resolution.DeclaredResult.Count);
        if (removeThenHeal)
        {
            Assert.IsType<MortalWoundRemoveComplicationOperation>(resolution.DeclaredResult[0]);
            Assert.IsType<MortalWoundRemoveComplicationOutcomeIntent>(resolution.OutcomeIntents[0]);
        }
        if (mode == "course") Assert.Equal("completed", resolution.CourseDisposition);
        var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(before, new[] { resolution.DeclaredResult.ToImmutableArray() });
        Assert.True(simulation.IsApplicable); Assert.True(simulation.Improved);
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease));
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
        var held = fixture.CaptureJson();
        var composed = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(files, fixture.Lease,
            new GameResponse(), admitted.Owner, request, resolution);
        fixture.AssertJson(held);
        Assert.True(composed.IsValid,
            "TERMINAL_HEAL_CAUSAL: the actual publication rejected a legal, retained terminal heal after native mode/source/claim premises. " +
            DescribeIssues(composed.Issues));
        AssertTerminalPublicationStages(composed.Plan!, removeThenHeal ? "remove" : "simple");
    }

    private static void PrepareTerminalPhysicalWound(JsonObject wound, string mode, bool removeThenHeal)
    {
        if (removeThenHeal)
        {
            // Reuse only the existing pure initial DATA builder, never its old reflection resolver.
            var routeId = wound["treatment"]!["routes"]![0]!["routeId"]!.DeepClone();
            var knownRouteIds = wound["treatment"]!["knownRouteIds"]!.DeepClone();
            var existing = CreateOutcomeIntentScenario(GuaranteedHealCase()).Before.DeepClone().AsObject();
            existing["treatment"]!["routes"]![0]!["routeId"] = routeId;
            existing["treatment"]!["knownRouteIds"] = knownRouteIds;
            wound.Clear(); foreach (var pair in existing) wound[pair.Key] = pair.Value?.DeepClone();
            return;
        }
        wound["severity"]!["value"] = "I"; wound["severity"]!["rank"] = 1;
        wound["severity"]!["maximumAtCreation"] = "I";
        wound["consequences"]!["slotBudget"] = 1; wound["consequences"]!["slotsUsed"] = 1;
        var route = wound["treatment"]!["routes"]![0]!.AsObject();
        var heal = new JsonArray(new JsonObject { ["kind"] = "heal", ["legacies"] = new JsonArray() });
        if (mode == "course")
        {
            // A one-milestone course is canonical (1–32); the full UI course remains three milestones.
            route["outcomes"] = new JsonArray(CourseMilestone(1, 0, "completed", heal));
            route["resourcePolicy"]!["mutations"] = new JsonArray(CourseMutation(1));
        }
        else route["outcomes"]![0]!["result"] = heal;
    }
}
