using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundRepairPacketBuilderTests
{
    [Theory]
    [InlineData("exact", "exact", true)]
    [InlineData("exact", "wrong_target", false)]
    [InlineData("exact", "unknown", false)]
    [InlineData("exact", "duplicate", false)]
    [InlineData("exact", "confusable", false)]
    [InlineData("none", "exact", false)]
    [InlineData("exact", "disabled", false)]
    public void SkillScope_CanonicalPreflightPreservesOriginalSelectorCoordinateAndRejectedProposal(
        string offered, string current, bool success)
    {
        var opportunity = CreateOpportunity();
        var binding = new WoundAcceptedTurnBinding(
            opportunity.SessionId, opportunity.RequestId, opportunity.SnapshotToken,
            "mortal_world", 7, Array.Empty<WoundAcceptedEventAuthority>(), opportunity.AcceptedEventsFingerprint);
        var proposal = CreateProposal();
        var definition = EffectMaterializationTestFixture.CreateDefinition("roll_modifier");
        definition["components"]![0]!["payload"]!["operations"] = new JsonArray("skill_check");
        definition["components"]![0]!["payload"]!["scope"] = new JsonObject { ["kind"] = "skill", ["skillId"] = "skill_grip" };
        proposal["consequenceDefinitions"]![0]!["definition"] = definition.DeepClone();
        var allDefinition = definition.DeepClone().AsObject();
        allDefinition["definitionKey"] = "all_definition";
        allDefinition["components"]![0]!["payload"]!["scope"] = new JsonObject { ["kind"] = "all" };
        proposal["consequenceDefinitions"]!.AsArray().Insert(0, new JsonObject
        {
            ["definitionRef"] = "local_all", ["definition"] = allDefinition.DeepClone()
        });
        var decision = new JsonObject
        {
            ["opportunityRef"] = opportunity.PublicRef, ["decision"] = "materialize",
            ["woundRef"] = "local_wound", ["proposal"] = proposal
        };
        var commands = new[]
        {
            new WoundResponseCommandDraft(opportunity with { PublicRef = "opportunity_none" },
                JsonSerializer.SerializeToElement(new JsonObject { ["decision"] = "none" }), "scene"),
            new WoundResponseCommandDraft(opportunity, JsonSerializer.SerializeToElement(decision), "scene")
        };
        var wound = WoundMaterializationContract.Parse(WoundContractTestData.CreateActiveWound("local_wound").ToJsonString(), "test.wound").Wound!;
        var transitions = new[] { new WoundAcceptedTransitionDraft(
            "create", "operation_scope", "local_wound", "transition_local", opportunity.OpportunityId, "scope test",
            wound, new[] { new WoundAcceptedEffectDefinitionDraft("local_all", allDefinition), new WoundAcceptedEffectDefinitionDraft("local_effect_definition_001", definition) },
            new[]
            {
                new WoundAcceptedRootApplicationDraft("root_all", "local_all", "root_all_operation", WoundRootOwnershipDomain.BaseWound),
                new WoundAcceptedRootApplicationDraft("root_local", "local_effect_definition_001", "root_operation", WoundRootOwnershipDomain.BaseWound)
            },
            Array.Empty<WoundAcceptedConsequenceSlotBinding>()) };
        var authority = EffectRollSkillScopeAuthority.Build(new(
            EffectAcceptedTurnPlanCacheTests.SkillRoots(offered), EffectAcceptedTurnPlanCacheTests.SkillRoots(current)));
        var before = decision.ToJsonString();
        var issues = WoundResponseInputComposer.ValidateSkillScopes(binding, commands, transitions, authority, out var locations);
        Assert.Equal(before, decision.ToJsonString());
        Assert.Equal(success, issues.Count == 0);
        if (success)
        {
            Assert.NotNull(locations);
            Assert.True(locations.TryResolve(new EffectSourceKey("mortal_world", "wound", "local_wound",
                definition["definitionKey"]!.GetValue<string>()), out var location));
            Assert.Equal("woundDecisions[1].proposal.consequenceDefinitions[1].definition.components", location.Path);
            return;
        }
        Assert.Null(locations);
        var issue = Assert.Single(issues);
        const string path = "woundDecisions[1].proposal.consequenceDefinitions[1].definition.components[0].payload.scope.skillId";
        Assert.Equal(path, issue.FilePath);
        Assert.Equal("wound_materialization_effect_binding_invalid", issue.Code);
        Assert.Equal("wound_materialization", issue.Section);
        Assert.NotNull(issue.WoundRepairContext);
        Assert.True(JsonNode.DeepEquals(decision, issue.WoundRepairContext.RejectedDecision));
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(issues));
        Assert.True(packet.MatchesRejectedDecision(decision));
        Assert.Equal(path["woundDecisions[1].".Length..], Assert.Single(packet.Issues).Path);
        var safe = packet.SafeContext.ToJsonString();
        Assert.DoesNotContain("game_state/", safe);
        Assert.DoesNotContain("woundId", safe);
        Assert.DoesNotContain("effectId", safe);
        Assert.DoesNotContain("opportunity_internal", safe);
        Assert.DoesNotContain("\"woundId\"", packet.ToJsonObject().ToJsonString());
        Assert.DoesNotContain("\"effectId\"", packet.ToJsonObject().ToJsonString());
        Assert.Equal("skill_grip", decision["proposal"]!["consequenceDefinitions"]![1]!["definition"]!["components"]![0]!["payload"]!["scope"]!["skillId"]!.GetValue<string>());
    }

    [Fact]
    public void SkillScope_DirectSelectorCoordinateTakesPrecedenceOverLinkRepair()
    {
        var opportunity = CreateOpportunity();
        var binding = new WoundAcceptedTurnBinding(opportunity.SessionId, opportunity.RequestId, opportunity.SnapshotToken,
            "mortal_world", 7, Array.Empty<WoundAcceptedEventAuthority>(), opportunity.AcceptedEventsFingerprint);
        var proposal = CreateProposal();
        proposal["consequenceDefinitions"]![0]!["definition"]!["links"]!.AsArray().Add(new JsonObject
        {
            ["kind"] = "wound", ["targetRef"] = "local_wound", ["role"] = "source"
        });
        var decision = new JsonObject
        {
            ["opportunityRef"] = opportunity.PublicRef, ["decision"] = "materialize", ["woundRef"] = "local_wound", ["proposal"] = proposal
        };
        const string path = "woundDecisions[0].proposal.consequenceDefinitions[0].definition.components[0].payload.scope.skillId";
        var issue = new ValidationIssue(path, IssueSeverity.Error, "unavailable skill",
            code: "wound_materialization_effect_binding_invalid", section: "wound_materialization");
        var result = WoundResponseInputComposer.AttachRepairContexts(binding, new[] { opportunity },
            new[] { JsonSerializer.SerializeToElement(decision) }, "scene", new[] { issue });
        Assert.Equal(path, Assert.Single(result).FilePath);
    }

    [Fact]
    public void SkillScope_SelectorFailureHasSafeRepairPacketAndExactCoordinate()
    {
        const string path = "woundDecisions[0].proposal.consequenceDefinitions[0].definition.components[0].payload.scope.skillId";
        var proposal = CreateProposal();
        proposal["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["scope"] =
            new JsonObject { ["kind"] = "skill", ["skillId"] = "skill_grip" };
        var candidate = CreateCandidate("candidate_scope", path,
            "wound_materialization_effect_binding_invalid", "skill_grip", proposal);
        var packets = WoundRepairPacketBuilder.Build(CreateRequest(candidate));
        var packet = Assert.Single(packets);
        var issue = Assert.Single(packet.Issues);
        Assert.Equal(path["woundDecisions[0].".Length..], issue.Path);
        Assert.Equal("one exact offered and currently usable skill of the wound owner", issue.Expected);
        Assert.Equal("игрок", packet.SafeContext["target"]!.GetValue<string>());
        Assert.DoesNotContain("validator-internal", packet.SafeContext.ToJsonString());
        Assert.DoesNotContain("game_state/", packet.SafeContext.ToJsonString());
    }

    [Theory]
    [InlineData(
        "woundDecisions[0].proposal.owner",
        "wound_response_unknown_field",
        "proposal.owner",
        "owner omitted; the client keeps the sealed target",
        "forbidden owner field")]
    [InlineData(
        "woundDecisions[0].proposal.unexpectedNote",
        "wound_response_unknown_field",
        "proposal.unexpectedNote",
        "remove only this unknown GM-authored field and preserve every valid sibling",
        "unexpected note")]
    [InlineData(
        "woundDecisions[0].proposal.severity",
        "wound_severity_above_opportunity",
        "proposal.severity",
        "I-II",
        "III")]
    [InlineData(
        "woundDecisions[0].proposal.consequenceDefinitions[0].root.slots",
        "wound_consequence_slot_budget_exceeded",
        "proposal.consequenceDefinitions[0].root.slots",
        "at most 2 independently understandable consequence slots",
        "3")]
    [InlineData(
        "woundDecisions[0].proposal.consequenceDefinitions[0].definition.links",
        "wound_materialization_effect_binding_invalid",
        "proposal.consequenceDefinitions[0].definition.links",
        "one complete wound-owned effect definition with response-local links",
        "missing reciprocal link")]
    [InlineData(
        "woundDecisions[0].proposal.treatment.routes[0].displayName",
        "wound_materialization_missing_field",
        "proposal.treatment.routes[0].displayName",
        "one complete treatment route in the closed wound schema",
        "missing")]
    [InlineData(
        "woundDecisions[0].proposal.consequenceDefinitions[0].definition.components[0].payload.resource",
        "wound_consequence_resource_bound_missing",
        "proposal.consequenceDefinitions[0].definition.components[0].payload.resource",
        "one accepted resource and a bounded quantum-aligned amount",
        "unregistered resource")]
    [InlineData(
        "output/narrative_response.json.response",
        "wound_acquisition_narration_missing",
        "response",
        "the exact acquisition narration inside the final scene",
        "missing")]
    public void Build_NormalizesRepresentativeRepairIssuesToSafeSemanticEvidence(
        string rawPath,
        string code,
        string expectedPath,
        string expectedSafeRange,
        string actual)
    {
        var request = CreateRequest(CreateCandidate(
            "candidate_safe_001",
            rawPath,
            code,
            actual));

        var packet = Assert.Single(WoundRepairPacketBuilder.Build(request));

        Assert.Equal("wound_materialization_repair", packet.Kind);
        Assert.Equal("session_wound_repair", packet.SessionId);
        Assert.Equal("request_wound_repair", packet.RequestId);
        Assert.Equal("snapshot_wound_repair", packet.SnapshotToken);
        Assert.Equal("candidate_safe_001", packet.CandidateRef);
        Assert.Equal(Fingerprint('a'), packet.SemanticFingerprint);
        var issue = Assert.Single(packet.Issues);
        Assert.Equal(expectedPath, issue.Path);
        Assert.Equal(code, issue.Code);
        Assert.Equal(expectedSafeRange, issue.Expected);
        Assert.Equal(actual, issue.Actual);
        Assert.DoesNotContain("validator-internal", issue.Expected, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state/", issue.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RemovesOnlyTheOffendingLeafAndPreservesEveryValidProposalSibling()
    {
        var original = CreateProposal();
        var candidate = CreateCandidate(
            "candidate_safe_001",
            "woundDecisions[0].proposal.treatment.routes[0].resourcePolicy",
            "wound_materialization_invalid_field",
            "invalid resource policy",
            original);

        var packet = Assert.Single(WoundRepairPacketBuilder.Build(
            CreateRequest(candidate)));
        var preserved = packet.PreservedProposal;

        Assert.True(JsonNode.DeepEquals(original["classification"], preserved["classification"]));
        Assert.True(JsonNode.DeepEquals(original["display"], preserved["display"]));
        Assert.True(JsonNode.DeepEquals(original["severity"], preserved["severity"]));
        Assert.True(JsonNode.DeepEquals(original["complications"], preserved["complications"]));
        Assert.True(JsonNode.DeepEquals(
            original["consequenceDefinitions"],
            preserved["consequenceDefinitions"]));
        Assert.True(JsonNode.DeepEquals(original["recovery"], preserved["recovery"]));

        var originalRoute = original["treatment"]!["routes"]![0]!.AsObject();
        var preservedRoute = preserved["treatment"]!["routes"]![0]!.AsObject();
        Assert.Equal(
            originalRoute["routeId"]!.ToJsonString(),
            preservedRoute["routeId"]!.ToJsonString());
        Assert.Equal(
            originalRoute["requirements"]!.ToJsonString(),
            preservedRoute["requirements"]!.ToJsonString());
        Assert.False(preservedRoute.ContainsKey("resourcePolicy"));
        Assert.Equal(original.ToJsonString(), candidate.RejectedDecision["proposal"]!.ToJsonString());
    }

    [Fact]
    public void Build_RequiresOneClosedCorrectedDecisionAndCompleteNarrativeResponse()
    {
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateRequest(
            CreateCandidate(
                "candidate_safe_001",
                "woundDecisions[0].proposal.severity",
                "wound_severity_above_opportunity",
                "III"))));

        var shape = packet.RequiredResponseShape;
        Assert.Equal(new[] { "woundDecisions", "response" }, shape.Select(pair => pair.Key));
        var decisions = shape["woundDecisions"]!.AsArray();
        var decision = Assert.Single(decisions)!.AsObject();
        Assert.Equal(
            new[] { "opportunityRef", "decision", "woundRef", "proposal" },
            decision.Select(pair => pair.Key));
        Assert.Equal("opportunity_safe_001", decision["opportunityRef"]!.GetValue<string>());
        Assert.Equal("materialize", decision["decision"]!.GetValue<string>());
        Assert.Equal("local_wound_ref_001", decision["woundRef"]!.GetValue<string>());

        var proposalRequirement = decision["proposal"]!.AsObject();
        Assert.Equal("preservedProposal", proposalRequirement["base"]!.GetValue<string>());
        Assert.Equal(
            new[] { "proposal.severity" },
            proposalRequirement["correctOnly"]!.AsArray()
                .Select(node => node!.GetValue<string>()));
        Assert.Equal(
            "complete final scene containing display.acquisitionNarration verbatim",
            shape["response"]!.GetValue<string>());
        Assert.False(shape.ContainsKey("eventRef"));
        Assert.False(shape.ContainsKey("targetId"));
        Assert.False(shape.ContainsKey("ownerId"));
    }

    [Fact]
    public void Build_UsesTheCandidateSpecificSealedSeverityRange()
    {
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateRequest(
            CreateCandidate(
                "candidate_safe_001",
                "woundDecisions[0].proposal.severity",
                "wound_severity_above_opportunity",
                "IV",
                minimumSeverity: "II",
                maximumSeverity: "III"))));

        Assert.Equal("II-III", Assert.Single(packet.Issues).Expected);
    }

    [Fact]
    public void ToJsonObject_SerializesOnlyTheClosedWoundRepairContract()
    {
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateRequest(
            CreateCandidate(
                "candidate_safe_001",
                "woundDecisions[0].proposal.severity",
                "wound_severity_above_opportunity",
                "III"))));

        var json = packet.ToJsonObject();

        Assert.Equal(
            new[]
            {
                "kind", "sessionId", "requestId", "snapshotToken", "candidateRef",
                "semanticFingerprint", "issues", "safeContext", "preservedProposal",
                "requiredResponseShape"
            },
            json.Select(pair => pair.Key));
        Assert.False(json.ContainsKey("opportunityId"));
        Assert.False(json.ContainsKey("eventRef"));
        Assert.False(json.ContainsKey("owner"));
        Assert.False(json.ContainsKey("targetBinding"));
        Assert.False(json.ContainsKey("authorityFingerprint"));
    }

    [Fact]
    public void Build_AcceptsExactlySixtyFourUniqueCandidatesInStableOrder()
    {
        var candidates = Enumerable.Range(0, WoundContractTestData.PendingCandidateLimit)
            .Select(index => CreateCandidate(
                $"candidate_safe_{index:D3}",
                "woundDecisions[0].proposal.severity",
                "wound_severity_above_opportunity",
                "III",
                semanticFingerprint: Fingerprint((char)('a' + index % 6))))
            .ToArray();

        var packets = WoundRepairPacketBuilder.Build(CreateRequest(candidates));

        Assert.Equal(WoundContractTestData.PendingCandidateLimit, packets.Count);
        Assert.Equal(
            candidates.Select(candidate => candidate.CandidateRef),
            packets.Select(packet => packet.CandidateRef));
        Assert.False(WoundRepairPacketBuilder.RequiresFailClosedRollback(
            CreateRequest(candidates)));
    }

    [Fact]
    public void Build_RejectsTheWholeWaveInsteadOfTruncatingSixtyFifthCandidate()
    {
        var candidates = Enumerable.Range(0, WoundContractTestData.PendingCandidateLimit + 1)
            .Select(index => CreateCandidate(
                $"candidate_safe_{index:D3}",
                "woundDecisions[0].proposal.severity",
                "wound_severity_above_opportunity",
                "III"))
            .ToArray();
        var request = CreateRequest(candidates);

        Assert.Empty(WoundRepairPacketBuilder.Build(request));
        Assert.True(WoundRepairPacketBuilder.RequiresFailClosedRollback(request));
    }

    [Theory]
    [InlineData("candidate_safe_001", "candidate_safe_001")]
    [InlineData("candidate_safe_A", "candidate_safe_А")]
    public void Build_RejectsExactOrConfusableCandidateReferences(
        string firstRef,
        string secondRef)
    {
        var request = CreateRequest(
            CreateCandidate(
                firstRef,
                "woundDecisions[0].proposal.severity",
                "wound_severity_above_opportunity",
                "III"),
            CreateCandidate(
                secondRef,
                "woundDecisions[0].proposal.severity",
                "wound_severity_above_opportunity",
                "III"));

        Assert.Empty(WoundRepairPacketBuilder.Build(request));
        Assert.True(WoundRepairPacketBuilder.RequiresFailClosedRollback(request));
    }

    [Fact]
    public void Build_ContextualValidationIssueReconstructsTheExactSafeCandidate()
    {
        var candidate = CreateCandidate(
            "candidate_safe_001",
            "woundDecisions[0].proposal.severity",
            "wound_severity_above_opportunity",
            "III");
        var request = CreateRequest(candidate);
        var issue = Assert.Single(candidate.Issues);
        issue.WoundRepairContext = new WoundRepairContext(
            request.SessionId,
            request.RequestId,
            request.SnapshotToken,
            candidate.Kind,
            candidate.CandidateRef,
            candidate.SemanticFingerprint,
            candidate.OpportunityRef,
            candidate.SafeContext,
            candidate.AllowedDecisions,
            candidate.MinimumSeverity,
            candidate.MaximumSeverity,
            candidate.RejectedDecision);

        var direct = Assert.Single(WoundRepairPacketBuilder.Build(request));
        var contextual = Assert.Single(WoundRepairPacketBuilder.Build(
            new[] { issue }));

        Assert.True(JsonNode.DeepEquals(
            direct.ToJsonObject(),
            contextual.ToJsonObject()));
        Assert.False(WoundRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { issue }));
    }

    [Fact]
    public void Build_RepairableValidationIssueWithoutSafeContextFailsClosed()
    {
        var issue = new ValidationIssue(
            "woundDecisions[0].proposal.severity",
            IssueSeverity.Error,
            "Severity exceeds the sealed opportunity.",
            code: "wound_severity_above_opportunity",
            section: "wound_materialization",
            expected: "validator-internal range",
            actual: "III");

        Assert.Empty(WoundRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(WoundRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { issue }));
    }

    [Fact]
    public void MatchesCorrectedDecision_AcceptsOnlyTheListedLeafAndExactNarration()
    {
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateRequest(
            CreateCandidate(
                "candidate_retry_exact_001",
                "woundDecisions[0].proposal.severity",
                "wound_severity_above_opportunity",
                "III"))));
        var correctedProposal = packet.PreservedProposal;
        correctedProposal["severity"] = "II";
        var correctedDecision = new JsonObject
        {
            ["opportunityRef"] = "opportunity_safe_001",
            ["decision"] = "materialize",
            ["woundRef"] = "local_wound_ref_001",
            ["proposal"] = correctedProposal
        };
        const string narration =
            "Крюк срывается с цепи и вспарывает вам предплечье.";

        Assert.True(packet.MatchesCorrectedDecision(
            correctedDecision,
            "Пыль оседает. " + narration + " Вы отступаете к стене."));

        var changedSibling = correctedDecision.DeepClone().AsObject();
        changedSibling["proposal"]!["display"]!["name"] = "Другая рана";
        Assert.False(packet.MatchesCorrectedDecision(
            changedSibling,
            narration));
        Assert.False(packet.MatchesCorrectedDecision(
            correctedDecision,
            "Пыль оседает, но описание получения раны пропущено."));
    }

    [Fact]
    public void MatchesCorrectedDecision_RequiresForbiddenOwnerLeafToStayOmitted()
    {
        var proposal = CreateProposal();
        proposal["owner"] = new JsonObject
        {
            ["ownerId"] = "hidden_owner",
            ["carrierPath"] = "hidden_carrier"
        };
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateRequest(
            CreateCandidate(
                "candidate_retry_owner_001",
                "woundDecisions[0].proposal.owner",
                "wound_response_unknown_field",
                "owner",
                proposal))));
        var correctedDecision = new JsonObject
        {
            ["opportunityRef"] = "opportunity_safe_001",
            ["decision"] = "materialize",
            ["woundRef"] = "local_wound_ref_001",
            ["proposal"] = packet.PreservedProposal
        };
        const string narration =
            "Крюк срывается с цепи и вспарывает вам предплечье.";

        Assert.True(packet.MatchesCorrectedDecision(
            correctedDecision,
            narration));
        correctedDecision["proposal"]!["owner"] = new JsonObject();
        Assert.False(packet.MatchesCorrectedDecision(
            correctedDecision,
            narration));
    }

    [Fact]
    public void MatchesCorrectedDecision_PreservesUnknownFieldCodeAndRequiresOmission()
    {
        var proposal = CreateProposal();
        proposal["unexpectedNote"] = "remove me";
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateRequest(
            CreateCandidate(
                "candidate_retry_unknown_001",
                "woundDecisions[0].proposal.unexpectedNote",
                "wound_response_unknown_field",
                "unexpected note",
                proposal))));
        Assert.Equal(
            "wound_response_unknown_field",
            Assert.Single(packet.Issues).Code);
        Assert.False(packet.PreservedProposal.ContainsKey("unexpectedNote"));

        var correctedDecision = new JsonObject
        {
            ["opportunityRef"] = "opportunity_safe_001",
            ["decision"] = "materialize",
            ["woundRef"] = "local_wound_ref_001",
            ["proposal"] = packet.PreservedProposal
        };
        const string narration =
            "Крюк срывается с цепи и вспарывает вам предплечье.";

        Assert.True(packet.MatchesCorrectedDecision(
            correctedDecision,
            narration));
        correctedDecision["proposal"]!["unexpectedNote"] = "still present";
        Assert.False(packet.MatchesCorrectedDecision(
            correctedDecision,
            narration));
    }

    [Fact]
    public void MatchesOpportunity_RequiresTheExactUnpublishedSealedAuthority()
    {
        var opportunity = CreateOpportunity();
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateRequest(
            CreateCandidate(
                "candidate_retry_authority_001",
                "woundDecisions[0].proposal.severity",
                "wound_severity_above_opportunity",
                "III",
                opportunityAuthorityFingerprint:
                    opportunity.AuthorityFingerprint))));

        Assert.True(packet.MatchesOpportunity(opportunity));
        var changed = opportunity with { SourceId = "changed_combat_action" };
        changed = changed with
        {
            AuthorityFingerprint =
                WoundOpportunityAuthority.RecomputeAuthorityFingerprint(changed)
        };
        Assert.False(packet.MatchesOpportunity(changed));
        Assert.DoesNotContain(
            opportunity.AuthorityFingerprint,
            packet.ToJsonObject().ToJsonString(),
            StringComparison.Ordinal);
    }

    private static WoundRepairBuildRequest CreateRequest(
        params WoundRepairCandidateInput[] candidates) => new(
        "session_wound_repair",
        "request_wound_repair",
        "snapshot_wound_repair",
        candidates);

    private static WoundRepairCandidateInput CreateCandidate(
        string candidateRef,
        string rawPath,
        string code,
        string actual,
        JsonObject? proposal = null,
        string? semanticFingerprint = null,
        string minimumSeverity = "I",
        string maximumSeverity = "II",
        string? opportunityAuthorityFingerprint = null)
    {
        var issue = new ValidationIssue(
            rawPath,
            IssueSeverity.Error,
            "The rejected wound proposal violates its bounded contract.",
            code: code,
            section: "wound_materialization",
            expected: "validator-internal authority description",
            actual: actual,
            repairHint: "validator-internal implementation hint");
        return new WoundRepairCandidateInput(
            "repair_wound",
            candidateRef,
            semanticFingerprint ?? Fingerprint('a'),
            "opportunity_safe_001",
            new JsonObject
            {
                ["event"] = "осколок после обвала",
                ["target"] = "игрок",
                ["realm"] = "Смертный мир"
            },
            new[] { "none", "materialize" },
            minimumSeverity,
            maximumSeverity,
            new JsonObject
            {
                ["opportunityRef"] = "opportunity_safe_001",
                ["decision"] = "materialize",
                ["woundRef"] = "local_wound_ref_001",
                ["proposal"] = (proposal ?? CreateProposal()).DeepClone()
            },
            new[] { issue },
            opportunityAuthorityFingerprint);
    }

    private static WoundOpportunityAuthority CreateOpportunity()
    {
        var evidence = new WoundOpportunityEventEvidence(
            "narrative",
            "narrative_injury",
            "event_authority_repair_001",
            "harmful",
            2,
            "Осколок после обвала ранит цель.");
        var acceptedEvent = new WoundAcceptedEventAuthority(
            "event_repair_001",
            "narrative_injury",
            "event_authority_repair_001",
            WoundOpportunityEventEvidenceFingerprint.Compute(evidence));
        var acceptedEvents = new[] { acceptedEvent };
        var binding = new WoundAcceptedTurnBinding(
            "session_wound_repair",
            "request_wound_repair",
            "snapshot_wound_repair",
            "mortal_world",
            7,
            acceptedEvents,
            WoundAcceptedEventSetFingerprint.Compute(acceptedEvents));
        var result = WoundOpportunityAuthority.Compose(
            new WoundOpportunityBuildRequest(
                binding,
                "opportunity_internal_repair_001",
                "opportunity_safe_001",
                acceptedEvent.EventRef,
                new WoundOwnerCoordinate(
                    "mortal_world",
                    "player",
                    "player_current",
                    "game_state/player/wounds.json"),
                "physical",
                "mortal_narrative_injury_v1",
                "combat_action",
                "combat_action_repair_001",
                "active",
                evidence,
                2,
                null,
                new WoundOpportunitySafeContext(
                    "игрок",
                    "осколок после обвала",
                    new[] { "anatomical", "systemic", "other" })));
        Assert.True(result.Success);
        return Assert.IsType<WoundOpportunityAuthority>(result.Opportunity);
    }

    private static JsonObject CreateProposal() => new()
    {
        ["classification"] = new JsonObject
        {
            ["woundType"] = "laceration",
            ["locationProfile"] = new JsonObject
            {
                ["kind"] = "body_part",
                ["readableLocus"] = "левое предплечье"
            }
        },
        ["display"] = new JsonObject
        {
            ["name"] = "Рваная рана предплечья",
            ["description"] = "Края раны расходятся при движении кисти.",
            ["visibleSymptoms"] = new JsonArray("кровотечение", "боль при хвате"),
            ["prognosis"] = "Без очистки возможно воспаление.",
            ["visibility"] = "known_to_player",
            ["acquisitionNarration"] =
                "Крюк срывается с цепи и вспарывает вам предплечье."
        },
        ["severity"] = "II",
        ["complications"] = new JsonArray(),
        ["consequenceDefinitions"] = new JsonArray(new JsonObject
        {
            ["definitionRef"] = "local_effect_definition_001",
            ["definition"] = new JsonObject
            {
                ["definitionKey"] = "wound_grip_penalty",
                ["links"] = new JsonArray(),
                ["components"] = new JsonArray(new JsonObject
                {
                    ["componentId"] = "grip_penalty",
                    ["profile"] = "characteristic_modifier",
                    ["payload"] = new JsonObject
                    {
                        ["characteristic"] = "strength",
                        ["operation"] = "add",
                        ["value"] = -1
                    }
                })
            },
            ["root"] = new JsonObject
            {
                ["ownership"] = new JsonObject { ["kind"] = "base_wound" },
                ["slots"] = new JsonArray(new JsonObject
                {
                    ["profileKey"] = "characteristic_modifier",
                    ["readableSummary"] = "Боль мешает удерживать тяжёлые предметы."
                })
            }
        }),
        ["treatment"] = new JsonObject
        {
            ["diagnosisPaths"] = new JsonArray(),
            ["routes"] = new JsonArray(new JsonObject
            {
                ["routeId"] = "clean_and_suture",
                ["displayName"] = "Очистить и наложить швы",
                ["visibility"] = "known_to_player",
                ["mode"] = "procedure",
                ["requirements"] = new JsonArray(),
                ["resourcePolicy"] = new JsonObject
                {
                    ["reserveBeforeResolution"] = true,
                    ["consumeOn"] = new JsonArray("success", "partial_success"),
                    ["refundOn"] = new JsonArray("validation_failed", "rolled_back"),
                    ["mutations"] = new JsonArray()
                },
                ["resolution"] = new JsonObject
                {
                    ["formulaKey"] = "mortal_wound_procedure_v1",
                    ["difficulty"] = 12,
                    ["rollSource"] = "accepted_d20"
                },
                ["outcomes"] = new JsonArray(),
                ["interruption"] = null
            }),
            ["knownRouteIds"] = new JsonArray("clean_and_suture"),
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

    private static string Fingerprint(char value) =>
        "sha256:" + new string(value, 64);
}
