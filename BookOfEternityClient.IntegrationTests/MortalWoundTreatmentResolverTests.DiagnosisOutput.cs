using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData(20, 0)]
    [InlineData(1, 30)]
    public async Task DiagnosisOutputBindsClosedSelectedResultAndCompletePrivateCohortWithoutLiveAuthority(int natural, int duration)
    {
        const string label = "[red]Осмотр <script>&[/]";
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(natural, signedInput: true,
            duration: duration, twoMembers: true, woundLabel: label);
        var fs = fixture.Files; var owner = fixture.Authority;
        Assert.Equal(label, owner.CurrentWound.Display.Name);
        var requestResult = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner,
            new(owner.History, Array.Empty<ValidationIssue>()), owner.CurrentWound,
            "diagnosis_output_selected_private_cohort", "inspect_explicit_method", fixture.EventRef);
        Assert.True(requestResult.IsValid, JsonSerializer.Serialize(requestResult.Issues));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(requestResult.Request);
        var outcome = MortalWoundDiagnosisOutcome.Resolve(owner, new(owner.History, Array.Empty<ValidationIssue>()), request);
        Assert.Equal(natural == 20 ? "success" : "failed_attempt", outcome.Resolution.ResultCategory);
        var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease));
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
        var publication = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(fs, fixture.Lease,
            new GameResponse(), owner, request);
        Assert.True(publication.IsValid, JsonSerializer.Serialize(publication.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(publication.Plan);
        var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle);
        var prepared = bundle.PreparedPlan; var effects = bundle.EffectBatchPlan; var final = bundle.FinalPlan;
        Assert.True(EffectAcceptedTurnPlanner.TryReadMortalDiagnosisExecution(prepared, effects.EffectPlan, out var execution));
        var result = EffectAcceptedTurnPlanner.ReadMortalDiagnosisExecutedResult(prepared, effects.EffectPlan);
        Assert.Equal(2, result.Recoveries.Length);
        Assert.NotEqual(request.Coordinates.WoundId, result.Recoveries[0].Original.WoundId);
        Assert.Equal(request.Coordinates.WoundId, result.Recoveries[1].Original.WoundId);
        var contributions = final.CarrierContributions;
        var mutations = contributions.SelectMany(value => value.Mutations).ToArray();
        Assert.Equal(duration == 0 ? 1 : 2, mutations.Length);
        if (duration == 0)
        {
            Assert.All(result.Recoveries, member => Assert.Empty(member.Stages));
            Assert.DoesNotContain(mutations, mutation => mutation.WoundId == result.Recoveries[0].Original.WoundId);
        }
        else Assert.All(result.Recoveries, member => Assert.NotEmpty(member.Stages));
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(outcome.Resolution);
        Assert.True(finalization.IsValid, JsonSerializer.Serialize(finalization.Issues));
        var confirmed = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(fs,
            fixture.Lease, request, finalization.Finalization!);
        Assert.True(confirmed.IsValid, JsonSerializer.Serialize(confirmed.Issues));

        // Only immutable execution DATA may be used once the genuine producing
        // lease closes. No current accepted owner is reconstructed for output.
        var continuation = Assert.IsType<WoundAcceptedTurnPlanner.MortalDiagnosisContinuation>(bundle.MortalDiagnosisContinuation);
        var otherPreparation = WoundAcceptedTurnPlanner.PrepareMortalDiagnosisContinuation(owner,
            continuation.Outcome, continuation.Working, continuation.Progression);
        Assert.True(otherPreparation.Success, JsonSerializer.Serialize(otherPreparation.Issues));
        var held = fixture.CaptureJson();
        await fixture.ReleaseLeaseAsync();
        var direct = GameEngine.BindAcceptedWoundOutput(plan, "Обычный рассказ без описания получения новой раны.");
        Assert.True(direct.Success, JsonSerializer.Serialize(direct.Issues));
        Assert.NotEmpty(direct.Notifications);
        Assert.All(direct.Notifications, notice =>
        {
            Assert.Equal("/раны", notice.DetailCommand);
            Assert.Equal(Markup.Escape(notice.Text.PlainText), notice.Text.ConsoleMarkup);
            Assert.Equal(HtmlEncoder.Default.Encode(notice.Text.PlainText), notice.Text.BrowserText);
            Assert.Contains(label, notice.Text.PlainText);
            foreach (var hidden in new[] { "ПРИВАТНАЯ_РАНА", result.Recoveries[0].Original.WoundId,
                         "diagnostic_hidden_route", request.Coordinates.OperationKey, request.RequestFingerprint })
                Assert.DoesNotContain(hidden, notice.Text.PlainText);
        });
        var diagnosis = Assert.Single(direct.Notifications, notice => notice.Text.PlainText.StartsWith("Осмотр", StringComparison.Ordinal));
        Assert.Contains(natural == 20 ? "завершён" : "не дал новых сведений", diagnosis.Text.PlainText);
        Assert.Contains("[[red]]", diagnosis.Text.ConsoleMarkup);
        Assert.Contains("&lt;script&gt;&amp;", diagnosis.Text.BrowserText);
        Assert.Equal(duration == 0 ? 1 : 2, direct.Notifications.Count);
        if (duration != 0)
            Assert.Contains(direct.Notifications, notice => notice.Text.PlainText.StartsWith("Естественное восстановление", StringComparison.Ordinal));
        var published = await GameEngine.BindPublishedAcceptedWoundOutputAsync(fs, plan);
        Assert.True(published.Success, JsonSerializer.Serialize(published.Issues));
        Assert.Equal(JsonSerializer.Serialize(direct.Notifications), JsonSerializer.Serialize(published.Notifications));
        fixture.AssertJson(held);

        // Resealed detached final DATA still must agree with the actual private
        // execution. The real bundle constructor is the first refusing boundary.
        void RejectFinal(IReadOnlyList<WoundCarrierContribution> changed, JsonObject? history = null)
        {
            var index = final.IdentityIndexAfterImage;
            var rows = history ?? final.HistoryAfterImage;
            var intents = final.TransitionIntents;
            var fingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(prepared, effects, changed, index, rows, intents);
            var damaged = new WoundAcceptedTurnPlan(final.Binding, final.BindingFingerprint, final.InputFingerprint,
                final.WoundPreparationFingerprint, final.EffectInputFingerprint, final.EffectAcceptedTurnPlanFingerprint,
                fingerprint, final.AllocatedWoundIds, final.AllocatedTransitionIds, changed, index, rows, intents);
            Assert.Throws<ArgumentException>(() => new AcceptedMechanicsWoundStageBundle(bundle.Input, prepared, effects, damaged));
        }
        var source = Assert.Single(contributions);
        RejectFinal(new[] { new WoundCarrierContribution(source.Owner, source.ExpectedWoundCollectionFingerprint, mutations.Skip(1).ToArray()) });
        RejectFinal(new[] { new WoundCarrierContribution(source.Owner, source.ExpectedWoundCollectionFingerprint, mutations.Append(mutations[0]).ToArray()) });
        var altered = mutations[0].AfterWound!;
        altered = altered with { Display = altered.Display with { Name = "Подменённая рана" } };
        var changedMutation = new WoundCarrierMutation(mutations[0].Operation, mutations[0].WoundId, mutations[0].BeforeWound, altered);
        RejectFinal(new[] { new WoundCarrierContribution(source.Owner, source.ExpectedWoundCollectionFingerprint, mutations.Skip(1).Prepend(changedMutation).ToArray()) });
        if (duration != 0)
        {
            var history = final.HistoryAfterImage;
            var rows = history["transitions"]!.AsArray();
            var indices = Enumerable.Range(0, rows.Count).Where(i => rows[i]!["turn"]!.GetValue<int>() == request.Coordinates.Turn).ToArray();
            Assert.True(indices.Length >= 2);
            var first = rows[indices[0]]!.DeepClone(); var last = rows[indices[^1]]!.DeepClone();
            Assert.False(JsonNode.DeepEquals(first, last));
            rows[indices[0]] = last; rows[indices[^1]] = first;
            RejectFinal(contributions, history);
        }
        // A genuine second preparation for the same request is still a foreign
        // private executor; public fingerprints do not substitute its identity.
        Assert.Throws<ArgumentException>(() => new AcceptedMechanicsWoundStageBundle(bundle.Input, otherPreparation.Plan!, effects, final));
        await fixture.AcquireLeaseAsync();
        var stillConfirmed = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(fs,
            fixture.Lease, request, finalization.Finalization!);
        Assert.True(stillConfirmed.IsValid, JsonSerializer.Serialize(stillConfirmed.Issues));
        Assert.Equal(confirmed.RequestFingerprint, stillConfirmed.RequestFingerprint);
        Assert.Equal(confirmed.ResourceAuthorityFingerprint, stillConfirmed.ResourceAuthorityFingerprint);
        Assert.Equal(confirmed.AgreementFingerprint, stillConfirmed.AgreementFingerprint);
        fixture.AssertJson(held);
    }
}
