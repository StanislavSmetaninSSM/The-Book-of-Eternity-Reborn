using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundMaterializationLifecycleTests
{
    private const string SessionId = "session_resource_materialization";
    private const string RequestId = "request_resource_materialization";
    private const string EventRef = "turn_42:accepted_effect";
    private const string OpportunityId = "opportunity_creation_player_001";
    private const string OpportunityRef = "wound-opportunity-creation-player-001";
    private const string LocalWoundRef = "wound_local_creation_player_001";
    private const string AcquisitionNarration =
        "Острый край вспарывает левое предплечье, и пальцы немеют от боли.";

    [Fact]
    public async Task OptionalOpportunity_DeclineConsumesCommandWithoutCreatingWound()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var response = Response(Decision("none", proposal: null));

        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.True(composed.Success, Describe(composed.Issues));
        Assert.Empty(composed.Notifications);
        var plan = await PublishAsync(
            context,
            Assert.IsType<JsonObject>(composed.CommandRoot));
        var output = GameEngine.BindAcceptedWoundOutput(plan, response.Response!);
        Assert.True(output.Success, Describe(output.Issues));
        Assert.Empty(output.Notifications);

        var player = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath));
        Assert.Empty(player["activeWounds"]!.AsArray());
        var index = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundIdentityState.StatePath));
        Assert.Empty(index["entries"]!.AsArray());
        var history = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundHistoryState.HistoryPath));
        Assert.Empty(history["transitions"]!.AsArray());
        Assert.Null(await context.ReadJsonAsync(AcceptedMechanicsPlan.WoundCommandPath));
    }

    [Fact]
    public async Task OptionalOpportunity_LowerSeverityPublishesWoundEffectHistoryAndNotificationTogether()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var response = Response(Decision(
            "materialize",
            CreatePhysicalProposal(severity: "I", includeMechanicalRoot: true)));

        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.True(composed.Success, Describe(composed.Issues));
        var notification = Assert.Single(composed.Notifications);
        Assert.Equal(
            "Получена рана: Рваная рана предплечья (I). Подробнее: /раны",
            notification.Text.PlainText);
        Assert.DoesNotContain("wound_", notification.Text.PlainText, StringComparison.Ordinal);

        var plan = await PublishAsync(
            context,
            Assert.IsType<JsonObject>(composed.CommandRoot));

        var player = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath));
        var wound = Assert.IsType<JsonObject>(Assert.Single(
            player["activeWounds"]!.AsArray()));
        var woundId = wound["woundId"]!.GetValue<string>();
        Assert.StartsWith("wound_", woundId, StringComparison.Ordinal);
        Assert.NotEqual(LocalWoundRef, woundId);
        Assert.Equal("I", wound["severity"]!["value"]!.GetValue<string>());
        Assert.Equal("II", wound["severity"]!["maximumAtCreation"]!.GetValue<string>());

        var rootBinding = Assert.IsType<JsonObject>(Assert.Single(
            wound["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray()));
        var effectId = rootBinding["effectId"]!.GetValue<string>();
        var effects = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.PlayerPath));
        var effect = effects["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .Single(value => string.Equals(
                value["effectId"]?.GetValue<string>(),
                effectId,
                StringComparison.Ordinal));
        Assert.Equal(woundId, effect["source"]!["sourceId"]!.GetValue<string>());
        Assert.Equal("wound", effect["source"]!["kind"]!.GetValue<string>());

        var index = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundIdentityState.StatePath));
        var indexEntry = Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()));
        Assert.Equal(woundId, indexEntry["woundId"]!.GetValue<string>());
        Assert.Equal("active", indexEntry["status"]!.GetValue<string>());
        var history = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundHistoryState.HistoryPath));
        var transition = Assert.IsType<JsonObject>(Assert.Single(
            history["transitions"]!.AsArray()));
        Assert.Equal(woundId, transition["woundId"]!.GetValue<string>());
        Assert.Equal("create", transition["kind"]!.GetValue<string>());
        Assert.NotNull(plan.WoundStageBundle);
        Assert.Null(await context.ReadJsonAsync(AcceptedMechanicsPlan.WoundCommandPath));
    }

    [Fact]
    public async Task GuaranteedOpportunity_OmittedWoundFailsClosedBeforeAnyPublication()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 2,
            guaranteedSeverityRank: 2);
        var before = await context.CaptureAsync(
            WoundCarrierCatalog.PlayerPath,
            EffectCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath);
        var response = Response(Decision("none", proposal: null));

        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(composed.Success);
        Assert.Null(composed.CommandRoot);
        Assert.Contains(composed.Issues, issue =>
            issue.Code == "wound_guaranteed_result_required");
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task GuaranteedOpportunity_ExactRequiredSeverityPublishesBoundWound()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 2,
            guaranteedSeverityRank: 2);
        var response = Response(Decision(
            "materialize",
            CreatePhysicalProposal(severity: "II", includeMechanicalRoot: true)));

        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.True(composed.Success, Describe(composed.Issues));
        Assert.Equal(
            "Получена рана: Рваная рана предплечья (II). Подробнее: /раны",
            Assert.Single(composed.Notifications).Text.PlainText);

        await PublishAsync(
            context,
            Assert.IsType<JsonObject>(composed.CommandRoot));

        var player = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath));
        var wound = Assert.IsType<JsonObject>(Assert.Single(
            player["activeWounds"]!.AsArray()));
        var woundId = wound["woundId"]!.GetValue<string>();
        Assert.Equal("II", wound["severity"]!["value"]!.GetValue<string>());
        Assert.Equal(
            "guarantee_creation_player_001",
            wound["origin"]!["guaranteedTriggerId"]!.GetValue<string>());

        var index = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundIdentityState.StatePath));
        Assert.Equal(
            woundId,
            Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()))
                ["woundId"]!.GetValue<string>());
        var history = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundHistoryState.HistoryPath));
        var transition = Assert.IsType<JsonObject>(Assert.Single(
            history["transitions"]!.AsArray()));
        Assert.Equal(woundId, transition["woundId"]!.GetValue<string>());
        Assert.Equal("create", transition["kind"]!.GetValue<string>());
    }

    [Fact]
    public async Task AcceptedTurnOutputBinding_RecomputesNotificationAndRejectsChangedScene()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 2);
        var response = Response(Decision(
            "materialize",
            CreatePhysicalProposal(severity: "II", includeMechanicalRoot: true)));
        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));
        var plan = await PublishAsync(
            context,
            Assert.IsType<JsonObject>(composed.CommandRoot));

        var accepted = GameEngine.BindAcceptedWoundOutput(
            plan,
            response.Response!);

        Assert.True(accepted.Success, Describe(accepted.Issues));
        Assert.Equal(
            "Получена рана: Рваная рана предплечья (II). Подробнее: /раны",
            Assert.Single(accepted.Notifications).Text.PlainText);

        await context.WriteExactJsonAsync(
            "output/narrative_response.json",
            new JsonObject
            {
                ["response"] = response.Response,
                ["timestamp"] = "2026-08-28T22:00:00Z"
            }.ToJsonString());
        var published = await GameEngine.BindPublishedAcceptedWoundOutputAsync(
            context.FileSystem,
            plan);
        Assert.True(published.Success, Describe(published.Issues));
        Assert.Equal(
            accepted.Notifications,
            published.Notifications);

        var rejected = GameEngine.BindAcceptedWoundOutput(
            plan,
            "Вы успеваете отступить от обвала, не замечая последствий.");

        Assert.False(rejected.Success);
        Assert.Empty(rejected.Notifications);
        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "wound_acquisition_narration_missing" &&
            issue.FilePath == "output/narrative_response.json.response" &&
            issue.RepairTargetFiles.SequenceEqual(
                new[] { "output/narrative_response.json" },
                StringComparer.Ordinal));
    }

    [Fact]
    public async Task StateDistributor_TypedWoundCompositionPublishesOnlyStrictCommand()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 2);
        var response = Response(Decision(
            "materialize",
            CreatePhysicalProposal(severity: "II", includeMechanicalRoot: true)));
        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));
        var distributor = new StateDistributor(
            context.FileSystem,
            NullLogger<StateDistributor>.Instance);

        var modified = await distributor.DistributeAsync(response, composed);

        Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, modified);
        var distributedCommand = JsonNode.Parse(
            await context.FileSystem.ReadFileAsync(
                AcceptedMechanicsPlan.WoundCommandPath) ?? "null");
        Assert.True(JsonNode.DeepEquals(
            composed.CommandRoot,
            distributedCommand));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(static issue => issue.Severity != IssueSeverity.Error),
            Describe(issues));
        await using var lease =
            await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = Assert.IsType<AcceptedMechanicsPlan>(await context.Normalizer
            .BindTo(lease)
            .NormalizeAcceptedMechanicsAsync(backups: null));

        Assert.NotNull(plan.WoundStageBundle);
        Assert.False(context.FileSystem.FileExists(
            lease,
            AcceptedMechanicsPlan.WoundCommandPath));
        var player = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath));
        Assert.Single(Assert.IsType<JsonArray>(player["activeWounds"]));
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("decision")]
    [InlineData("extra_decision")]
    public async Task StateDistributor_RejectsTypedCommandDetachedFromResponse(
        string mutation)
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 2);
        var response = Response(Decision(
            "materialize",
            CreatePhysicalProposal(severity: "II", includeMechanicalRoot: true)));
        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));
        if (mutation == "scene")
        {
            response.Response = "Подменённая сцена без принятого описания раны.";
        }
        else if (mutation == "decision")
        {
            response.WoundDecisions = new[]
            {
                ToElement(Decision("none", proposal: null))
            };
        }
        else
        {
            response.WoundDecisions =
            [
                .. response.WoundDecisions!,
                ToElement(Decision("none", proposal: null))
            ];
        }
        var distributor = new StateDistributor(
            context.FileSystem,
            NullLogger<StateDistributor>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => distributor.DistributeAsync(response, composed));

        Assert.Contains(
            "wound_accepted_command_unbound",
            exception.Message,
            StringComparison.Ordinal);
        Assert.False(context.FileSystem.FileExists(
            AcceptedMechanicsPlan.WoundCommandPath));
        Assert.False(context.FileSystem.FileExists(
            WoundMaterializationTestContext.NarrativeOutputPath));
    }

    [Fact]
    public async Task StateDistributor_StrictWoundCommandFailureRestoresExactBeforeImage()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 2);
        var response = Response(Decision(
            "materialize",
            CreatePhysicalProposal(severity: "II", includeMechanicalRoot: true)));
        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));
        var originalCommandBytes = System.Text.Encoding.UTF8.GetBytes(
            "{\"sentinel\":\"exact-before-image\"}");
        await context.FileSystem.WriteFileAtomicBytesAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            originalCommandBytes);
        var distributor = new StateDistributor(
            context.FileSystem,
            NullLogger<StateDistributor>.Instance,
            new StateDistributorHooks
            {
                AfterFileMutationAppliedAsync = path =>
                    string.Equals(
                        path,
                        AcceptedMechanicsPlan.WoundCommandPath,
                        StringComparison.Ordinal)
                        ? throw new IOException("Injected strict wound command failure.")
                        : Task.CompletedTask
            });

        await Assert.ThrowsAsync<IOException>(
            () => distributor.DistributeAsync(response, composed));

        Assert.Equal(
            originalCommandBytes,
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));
        Assert.False(context.FileSystem.FileExists(
            WoundMaterializationTestContext.NarrativeOutputPath));
    }

    [Fact]
    public async Task ExistingWound_WorseningReusesIdentityReplacesEffectsAndAppendsHistory()
    {
        await using var context = await CreatePlayerContextAsync();
        var creationAuthority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 2);
        var creationResponse = Response(Decision(
            "materialize",
            CreatePhysicalProposal(severity: "II", includeMechanicalRoot: true)));
        var creation = WoundResponseInputComposer.Compose(
            creationAuthority.Binding,
            new[] { creationAuthority.Opportunity },
            creationResponse.WoundDecisions,
            creationResponse.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(creation.Success, Describe(creation.Issues));
        await PublishAsync(context, Assert.IsType<JsonObject>(creation.CommandRoot));

        var playerBefore = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath));
        var woundBeforeJson = Assert.IsType<JsonObject>(Assert.Single(
            playerBefore["activeWounds"]!.AsArray()));
        var parsedBefore = WoundMaterializationContract.Parse(
            woundBeforeJson.ToJsonString(),
            WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.True(parsedBefore.IsValid, Describe(parsedBefore.Issues));
        var woundBefore = Assert.IsType<WoundMaterializationEnvelope>(parsedBefore.Wound);
        var woundId = woundBefore.WoundId;
        var oldEffectId = Assert.Single(
            woundBefore.Consequences.OwnedEffectSources.RootBindings).EffectId;

        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            additionalTrackedPaths: WoundMaterializationValidationTests.SnapshotWoundPaths);
        var worseningAuthority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 3,
            eventRef: "turn_43:accepted_effect",
            turn: 43,
            worseningTarget: new WoundOpportunityWorseningTargetEvidence(
                woundBefore,
                "retrauma"));
        var worseningResponse = Response(Decision(
            "materialize",
            CreatePhysicalProposal(severity: "III", includeMechanicalRoot: true)));

        var worsening = WoundResponseInputComposer.Compose(
            worseningAuthority.Binding,
            new[] { worseningAuthority.Opportunity },
            worseningResponse.WoundDecisions,
            worseningResponse.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.True(worsening.Success, Describe(worsening.Issues));
        Assert.Equal(
            "Рана ухудшилась: Рваная рана предплечья (III). Подробнее: /раны",
            Assert.Single(worsening.Notifications).Text.PlainText);
        var worseningPlan = await PublishAsync(
            context,
            Assert.IsType<JsonObject>(worsening.CommandRoot));
        var worseningOutput = GameEngine.BindAcceptedWoundOutput(
            worseningPlan,
            worseningResponse.Response!);
        Assert.True(worseningOutput.Success, Describe(worseningOutput.Issues));
        Assert.Equal(
            "Рана ухудшилась: Рваная рана предплечья (III). Подробнее: /раны",
            Assert.Single(worseningOutput.Notifications).Text.PlainText);

        var playerAfter = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath));
        var woundAfter = Assert.IsType<JsonObject>(Assert.Single(
            playerAfter["activeWounds"]!.AsArray()));
        Assert.Equal(woundId, woundAfter["woundId"]!.GetValue<string>());
        Assert.Equal("III", woundAfter["severity"]!["value"]!.GetValue<string>());
        Assert.Equal("II", woundAfter["severity"]!["maximumAtCreation"]!.GetValue<string>());
        Assert.Equal("worsen", woundAfter["lastTransition"]!["kind"]!.GetValue<string>());
        Assert.Equal(2, woundAfter["lastTransition"]!["ordinal"]!.GetValue<int>());
        var newEffectId = Assert.IsType<JsonObject>(Assert.Single(
            woundAfter["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray()))
            ["effectId"]!.GetValue<string>();
        Assert.NotEqual(oldEffectId, newEffectId);

        var effectsAfter = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.PlayerPath));
        var activeEffectIds = effectsAfter["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .Select(value => value["effectId"]!.GetValue<string>())
            .ToArray();
        Assert.DoesNotContain(oldEffectId, activeEffectIds, StringComparer.Ordinal);
        Assert.Contains(newEffectId, activeEffectIds, StringComparer.Ordinal);

        var index = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundIdentityState.StatePath));
        Assert.Equal(
            woundId,
            Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()))
                ["woundId"]!.GetValue<string>());
        var history = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundHistoryState.HistoryPath));
        var transitions = history["transitions"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(2, transitions.Length);
        Assert.Equal(new[] { "create", "worsen" }, transitions
            .Select(value => value["kind"]!.GetValue<string>())
            .ToArray());
        Assert.All(transitions, transition => Assert.Equal(
            woundId,
            transition["woundId"]!.GetValue<string>()));
    }

    [Fact]
    public async Task ExistingWound_StaleWorseningTargetFailsClosedBeforeMutation()
    {
        await using var context = await CreatePlayerContextAsync();
        var creationAuthority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 2);
        var creationResponse = Response(Decision(
            "materialize",
            CreatePhysicalProposal(
                severity: "II",
                includeMechanicalRoot: true)));
        var creation = WoundResponseInputComposer.Compose(
            creationAuthority.Binding,
            new[] { creationAuthority.Opportunity },
            creationResponse.WoundDecisions,
            creationResponse.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(creation.Success, Describe(creation.Issues));
        await PublishAsync(context, Assert.IsType<JsonObject>(creation.CommandRoot));

        var player = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath));
        var parsed = WoundMaterializationContract.Parse(
            Assert.IsType<JsonObject>(Assert.Single(
                player["activeWounds"]!.AsArray())).ToJsonString(),
            WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var before = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            additionalTrackedPaths: WoundMaterializationValidationTests.SnapshotWoundPaths);
        var unchanged = await context.CaptureAsync(
            WoundCarrierCatalog.PlayerPath,
            EffectCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath);
        var staleBefore = before with
        {
            Display = before.Display with
            {
                Prognosis = "Устаревшая версия прогноза, которой нет в снимке."
            }
        };
        var authority = await CreateAuthorityAsync(
            context,
            maximumSeverityRank: 3,
            eventRef: "turn_43:accepted_effect",
            turn: 43,
            worseningTarget: new WoundOpportunityWorseningTargetEvidence(
                staleBefore,
                "retrauma"));
        var response = Response(Decision(
            "materialize",
            CreatePhysicalProposal(severity: "III", includeMechanicalRoot: true)));
        var worsening = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(worsening.Success, Describe(worsening.Issues));

        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            Assert.IsType<JsonObject>(worsening.CommandRoot).ToJsonString());
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "wound_plan_worsening_target_stale");
        await context.AssertUnchangedAsync(unchanged);
    }

    [Fact]
    public async Task ProposalAboveOpportunityMaximumFailsClosedWithoutWoundEffectOrHistory()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 1);
        var before = await context.CaptureAsync(
            WoundCarrierCatalog.PlayerPath,
            EffectCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath);
        var response = Response(Decision(
            "materialize",
            CreatePhysicalProposal(severity: "III", includeMechanicalRoot: true)));

        var composed = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(composed.Success);
        Assert.Null(composed.CommandRoot);
        Assert.Contains(composed.Issues, issue =>
            issue.Code == "wound_severity_above_opportunity" &&
            issue.Expected == "I-I" &&
            issue.Actual == "III");
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public void HealedWoundLegacy_IndependentMechanicSurvivesWhileCosmeticLegacyIsHistoryOnly()
    {
        var fixture = WoundLifecycleLegacyFixture.Create();

        var woundCatalog = WoundCarrierCatalog.Build(fixture.WoundCarriers);
        var identity = WoundIdentityState.Parse(
            fixture.WoundIdentityIndex.ToJsonString(),
            WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            fixture.WoundHistory.ToJsonString(),
            WoundHistoryState.HistoryPath);
        var effectCatalog = EffectCarrierCatalog.Build(fixture.EffectCarriers);

        Assert.Empty(woundCatalog.Issues);
        Assert.NotNull(identity.State);
        Assert.NotNull(history.State);
        Assert.Empty(identity.Issues);
        Assert.Empty(history.Issues);
        Assert.Empty(history.State!.ValidateAgreement(identity.State!, woundCatalog));
        Assert.Empty(woundCatalog.Occurrences);
        Assert.Single(effectCatalog.Occurrences);
        Assert.Equal(
            fixture.IndependentEffectId,
            Assert.Single(effectCatalog.Occurrences).EffectId);
        Assert.DoesNotContain(
            fixture.CosmeticLegacyRef,
            fixture.EffectCarriers.PlayerEffects!.ToJsonString(),
            StringComparison.Ordinal);
        Assert.Contains(
            fixture.CosmeticLegacyRef,
            fixture.WoundHistory.ToJsonString(),
            StringComparison.Ordinal);
    }

    private static async Task<ResourceMaterializationTestContext> CreatePlayerContextAsync(
        FileSystemManagerHooks? hooks = null)
    {
        var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        await WoundMaterializationValidationTests.SeedEmptyFoundationsAsync(context);
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: 41,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        Assert.True(resources.IsValid, Describe(resources.Issues));
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            resources.Definitions!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.StatePath,
            resources.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            resources.History!.ToCanonicalJson());
        var resourceOwners = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            resources.Definitions,
            context.FileSystem.ReadFileAsync,
            resources.State,
            resources.History,
            CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(resourceOwners.IsValid, Describe(resourceOwners.Issues));
        await context.WriteExactJsonAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            resourceOwners.CanonicalAuthorityJson!);
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.PlayerPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray()
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            EffectAcceptedTurnPlan.CommandPath,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot().ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            additionalTrackedPaths: WoundMaterializationValidationTests.SnapshotWoundPaths);
        return context;
    }

    private static async Task<CreationAuthority> CreateAuthorityAsync(
        ResourceMaterializationTestContext context,
        int maximumSeverityRank,
        int? guaranteedSeverityRank = null,
        string eventRef = EventRef,
        WoundOwnerCoordinate? acceptedOwner = null,
        int turn = 42,
        WoundOpportunityWorseningTargetEvidence? worseningTarget = null)
    {
        var snapshotToken = await WoundMaterializationValidationTests
            .ReadSnapshotTokenAsync(context);
        var evidence = new WoundOpportunityEventEvidence(
            "formal",
            "accepted_turn",
            $"turn_{turn}",
            "harmful",
            maximumSeverityRank,
            "Острый край ранит левое предплечье во время обвала.");
        var acceptedEvent = new WoundAcceptedEventAuthority(
            eventRef,
            evidence.AuthorityKind,
            evidence.AuthorityId,
            WoundOpportunityEventEvidenceFingerprint.Compute(evidence));
        var events = new[] { acceptedEvent };
        var binding = new WoundAcceptedTurnBinding(
            SessionId,
            RequestId,
            snapshotToken,
            "mortal_world",
            turn,
            events,
            WoundAcceptedEventSetFingerprint.Compute(events));
        var owner = acceptedOwner ?? new WoundOwnerCoordinate(
            "mortal_world",
            "player",
            "player_current",
            WoundCarrierCatalog.PlayerPath);
        var guarantee = guaranteedSeverityRank.HasValue
            ? new WoundGuaranteedTriggerEvidence(
                "guarantee_creation_player_001",
                "combat_action",
                "combat_action_creation_player_001",
                "active",
                "mortal_world",
                "physical",
                owner,
                guaranteedSeverityRank.Value,
                turn - 1,
                Fingerprint("guaranteed-source-contract"))
            : null;
        var result = WoundOpportunityAuthority.Compose(new WoundOpportunityBuildRequest(
            binding,
            OpportunityId,
            OpportunityRef,
            eventRef,
            owner,
            "physical",
            "mortal_formal_injury_v1",
            "combat_action",
            "combat_action_creation_player_001",
            "active",
            evidence,
            HardMaximumSeverityRank: 4,
            guarantee,
            new WoundOpportunitySafeContext(
                "вы",
                "острый край во время обвала",
                new[] { "anatomical", "systemic", "other" }),
            worseningTarget));
        Assert.True(result.Success, Describe(result.Issues));
        return new CreationAuthority(
            binding,
            Assert.IsType<WoundOpportunityAuthority>(result.Opportunity));
    }

    private static GameResponse Response(JsonObject decision) => new()
    {
        Response = $"{AcquisitionNarration} Вы успеваете отступить от обвала.",
        WoundDecisions = new[] { ToElement(decision) }
    };

    private static JsonObject Decision(string decision, JsonObject? proposal)
    {
        var root = new JsonObject
        {
            ["opportunityRef"] = OpportunityRef,
            ["decision"] = decision
        };
        if (proposal is not null)
        {
            root["woundRef"] = LocalWoundRef;
            root["proposal"] = proposal.DeepClone();
        }
        return root;
    }

    private static JsonObject CreatePhysicalProposal(
        string severity,
        bool includeMechanicalRoot)
    {
        var definitions = new JsonArray();
        if (includeMechanicalRoot)
        {
            var definition = EffectMaterializationTestFixture.CreateDefinition(
                "periodic_damage");
            definition["definitionKey"] = "wound_bleeding_root";
            definition["allowedRealms"] = new JsonArray("mortal_world");
            definition["allowedTargetKinds"] = new JsonArray("player");
            definition["parameterBounds"] = new JsonObject();
            definition["links"] = new JsonArray();
            definition["stacking"]!["stackKey"] = "stack_wound_bleeding_root";
            definition["stacking"]!["policy"] = "independent";
            definition["stacking"]!["maxStacks"] = 1;
            definition["stacking"]!["atMaximum"] = "no_change";
            definition["stacking"]!["refreshMode"] = null;
            definition["stacking"]!["mergeRule"] = null;
            definitions.Add(new JsonObject
            {
                ["definitionRef"] = "bleeding_root",
                ["definition"] = definition,
                ["root"] = new JsonObject
                {
                    ["ownership"] = new JsonObject
                    {
                        ["kind"] = "base_wound",
                        ["complicationRef"] = null
                    },
                    ["slots"] = new JsonArray(new JsonObject
                    {
                        ["profileKey"] = "periodic_damage",
                        ["readableSummary"] = "Свежая рана продолжает кровоточить."
                    })
                }
            });
        }

        return new JsonObject
        {
            ["classification"] = new JsonObject
            {
                ["woundType"] = "contaminated_laceration",
                ["locationProfile"] = new JsonObject
                {
                    ["kind"] = "anatomical",
                    ["readableLocus"] = "наружная сторона левого предплечья",
                    ["authorityKind"] = "body_part",
                    ["authorityRef"] = "body_part_left_forearm",
                    ["affectedSide"] = "left"
                }
            },
            ["display"] = new JsonObject
            {
                ["name"] = "Рваная рана предплечья",
                ["description"] = "Края раны расходятся при движении кисти.",
                ["visibleSymptoms"] = new JsonArray("кровотечение", "боль при хвате"),
                ["prognosis"] = "Без очистки возможно воспаление.",
                ["visibility"] = "known_to_player",
                ["acquisitionNarration"] = AcquisitionNarration
            },
            ["severity"] = severity,
            ["complications"] = new JsonArray(),
            ["consequenceDefinitions"] = definitions,
            ["treatment"] = new JsonObject
            {
                ["diagnosisPaths"] = new JsonArray(),
                ["routes"] = new JsonArray(),
                ["knownRouteIds"] = new JsonArray(),
                ["completedRouteIds"] = new JsonArray()
            },
            ["recovery"] = new JsonObject
            {
                ["mode"] = "requires_stabilization",
                ["clockKind"] = "mortal_world_time",
                ["cadence"] = 86400,
                ["currentStepProgress"] = 0,
                ["currentStepThreshold"] = 3,
                ["lastTickKey"] = null,
                ["blockers"] = new JsonArray("not_stabilized"),
                ["carryOverflow"] = true,
                ["deteriorationPolicy"] = null
            }
        };
    }

    private static async Task<AcceptedMechanicsPlan> PublishAsync(
        ResourceMaterializationTestContext context,
        JsonObject commandRoot)
    {
        await context.WriteExactJsonAsync(
            AcceptedMechanicsPlan.WoundCommandPath,
            commandRoot.ToJsonString());
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(static issue => issue.Severity != IssueSeverity.Error),
            Describe(issues));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        return Assert.IsType<AcceptedMechanicsPlan>(await context.Normalizer
            .BindTo(lease)
            .NormalizeAcceptedMechanicsAsync(backups: null));
    }

    private static JsonElement ToElement(JsonNode value) =>
        JsonSerializer.SerializeToElement(value);

    private static string Fingerprint(string value) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new[] { value });

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(static issue =>
            $"{issue} code={issue.Code}; expected={issue.Expected}; actual={issue.Actual}"));

    private sealed record CreationAuthority(
        WoundAcceptedTurnBinding Binding,
        WoundOpportunityAuthority Opportunity);
}

internal sealed record WoundLifecycleLegacyFixture(
    WoundCarrierCatalogInput WoundCarriers,
    JsonObject WoundIdentityIndex,
    JsonObject WoundHistory,
    EffectCarrierCatalogInput EffectCarriers,
    string IndependentEffectId,
    string CosmeticLegacyRef)
{
    internal static WoundLifecycleLegacyFixture Create()
    {
        const string woundId = "wound_healed_legacy_fixture";
        const string healTransitionId = "wound_transition_heal_legacy_fixture";
        const string independentEffectId = "effect_independent_legacy_fixture";
        const string cosmeticLegacyRef = "scar_left_forearm_cosmetic";
        var activeFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new[] { "legacy-fixture-active" });
        var healedFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new[] { "legacy-fixture-healed" });
        var sourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new[] { "legacy-fixture-source" });
        var identity = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray(new JsonObject
            {
                ["woundId"] = woundId,
                ["realm"] = "mortal_world",
                ["ownerKind"] = "player",
                ["ownerId"] = "player_current",
                ["carrierPath"] = WoundCarrierCatalog.PlayerPath,
                ["domain"] = "physical",
                ["status"] = "healed",
                ["createdAtTurn"] = 42,
                ["createdEventRef"] = "turn_42:wound_legacy_fixture",
                ["lastTransitionOrdinal"] = 3,
                ["terminalTransitionId"] = healTransitionId,
                ["semanticFingerprint"] = healedFingerprint
            })
        };
        var history = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["nextOrdinal"] = 4,
            ["transitions"] = new JsonArray(
                HistoryRow(
                    "wound_transition_create_legacy_fixture",
                    woundId,
                    ordinal: 1,
                    woundOrdinal: 1,
                    kind: "create",
                    turn: 42,
                    eventRef: "turn_42:wound_legacy_fixture",
                    operationKey: "operation_create_legacy_fixture",
                    before: WoundHistoryState.ComputeNonexistentBeforeFingerprint(woundId),
                    after: activeFingerprint,
                    source: sourceFingerprint,
                    summary: "Рана получена.",
                    terminal: false),
                HistoryRow(
                    healTransitionId,
                    woundId,
                    ordinal: 2,
                    woundOrdinal: 2,
                    kind: "heal",
                    turn: 45,
                    eventRef: "turn_45:wound_healed_legacy_fixture",
                    operationKey: "operation_heal_legacy_fixture",
                    before: activeFingerprint,
                    after: healedFingerprint,
                    source: sourceFingerprint,
                    summary: "Рана полностью исцелена.",
                    terminal: true),
                HistoryRow(
                    "wound_transition_legacy_fixture",
                    woundId,
                    ordinal: 3,
                    woundOrdinal: 3,
                    kind: "legacy",
                    turn: 45,
                    eventRef: "turn_45:wound_legacy_separated_fixture",
                    operationKey: "operation_legacy_fixture",
                    before: healedFingerprint,
                    after: healedFingerprint,
                    source: sourceFingerprint,
                    summary: $"Остался косметический след: {cosmeticLegacyRef}.",
                    terminal: false))
        };

        var independentEffect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            "player",
            "periodic_restore");
        independentEffect["effectId"] = independentEffectId;
        independentEffect["source"] = new JsonObject
        {
            ["kind"] = "fate_card",
            ["sourceId"] = "fate_card_independent_legacy_fixture",
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        independentEffect["display"]!["sourceLabel"] = "Самостоятельное наследие";

        return new WoundLifecycleLegacyFixture(
            new WoundCarrierCatalogInput(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["owner"] = new JsonObject
                    {
                        ["realm"] = "mortal_world",
                        ["ownerKind"] = "player",
                        ["ownerId"] = "player_current"
                    },
                    ["activeWounds"] = new JsonArray()
                },
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["entries"] = new JsonArray()
                },
                null,
                null,
                null),
            identity,
            history,
            new EffectCarrierCatalogInput(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["activeEffects"] = new JsonArray(independentEffect)
                },
                null,
                null,
                null,
                null,
                null),
            independentEffectId,
            cosmeticLegacyRef);
    }

    private static JsonObject HistoryRow(
        string transitionId,
        string woundId,
        int ordinal,
        int woundOrdinal,
        string kind,
        int turn,
        string eventRef,
        string operationKey,
        string before,
        string after,
        string source,
        string summary,
        bool terminal) => new()
    {
        ["transitionId"] = transitionId,
        ["woundId"] = woundId,
        ["ordinal"] = ordinal,
        ["woundTransitionOrdinal"] = woundOrdinal,
        ["kind"] = kind,
        ["turn"] = turn,
        ["eventRef"] = eventRef,
        ["operationKey"] = operationKey,
        ["beforeFingerprint"] = before,
        ["afterFingerprint"] = after,
        ["sourceFingerprint"] = source,
        ["attemptId"] = null,
        ["courseId"] = null,
        ["courseMilestoneOrdinal"] = null,
        ["cycleKey"] = null,
        ["paymentFingerprint"] = null,
        ["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(
            operationKey,
            eventRef,
            summary),
        ["readableSummary"] = summary,
        ["terminal"] = terminal
    };
}
