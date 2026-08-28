using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundRepairPacketBuilderTests
{
    [Theory]
    [InlineData(
        "woundDecisions[0].proposal.owner",
        "wound_response_unknown_field",
        "proposal.owner",
        "owner omitted; the client keeps the sealed target",
        "forbidden owner field")]
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
        "woundDecisions[0].proposal.treatment.routes[0].aftercare",
        "wound_materialization_missing_field",
        "proposal.treatment.routes[0].aftercare",
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
            originalRoute["routeRef"]!.ToJsonString(),
            preservedRoute["routeRef"]!.ToJsonString());
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
        string? semanticFingerprint = null)
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
            "I",
            "II",
            new JsonObject
            {
                ["opportunityRef"] = "opportunity_safe_001",
                ["decision"] = "materialize",
                ["woundRef"] = "local_wound_ref_001",
                ["proposal"] = (proposal ?? CreateProposal()).DeepClone()
            },
            new[] { issue });
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
                ["routeRef"] = "clean_and_suture",
                ["requirements"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "cleaning_supplies"
                }),
                ["resourcePolicy"] = new JsonObject
                {
                    ["kind"] = "consume_item",
                    ["resource"] = "bandage",
                    ["amount"] = 1
                },
                ["aftercare"] = "Держать повязку сухой."
            }),
            ["failurePolicy"] = "progress_stalls"
        },
        ["recovery"] = new JsonObject
        {
            ["mode"] = "mortal_clock",
            ["currentStepProgress"] = 0
        }
    };

    private static string Fingerprint(char value) =>
        "sha256:" + new string(value, 64);
}
