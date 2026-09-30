using System.Text.Json.Nodes;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.Services;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises closed continuation transport, correlation and ordinary worker compatibility.
/// </summary>
public sealed class SpiritualWoundContinuationProtocolTests
{
    /// <summary>
    /// Limits the mixed afterlife and narrative scope exception to the exact explicit continuation path.
    /// </summary>
    /// <param name="continuation">
    /// Whether the task carries the closed spiritual continuation envelope.
    /// </param>
    /// <param name="narrativePath">
    /// Proposed output path, including aliases and unrelated outputs in rejection cases.
    /// </param>
    /// <param name="expectedValid">
    /// Whether task and proposal structure may proceed to genuine owner admission.
    /// </param>
    [Theory]
    [InlineData(true, "output/narrative_response.json", true)]
    [InlineData(false, "output/narrative_response.json", false)]
    [InlineData(true, "output/Narrative_Response.json", false)]
    [InlineData(true, "output/interface_options.json", false)]
    public void AfterlifeContinuationNarrativeScope_RemainsExactAndOptIn(
        bool continuation, string narrativePath, bool expectedValid)
    {
        const string conflict = "game_state/meta/afterlife_spiritual_conflict.json";
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        profile = profile with { Permissions = profile.Permissions with { ProposalWritePaths = ["game_state/**", "output/**"] } };
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        {
            ValidationIssues = [new WorkerValidationIssue { Code = "afterlife_test", Path = conflict, Message = "Test correction." }],
            SpiritualWoundContinuation = continuation ? SpiritualWoundContinuationProtocol.ReadRequest(
                JsonSerializer.Deserialize<JsonElement>(Request)) : null,
            ContextFiles = new[] { conflict, AfterlifeRealmAuthorityContract.StatePath, narrativePath }
                .Select(path => new WorkerFileReference { Path = path, Sha256 = new string('a', 64) }).ToArray(),
            AllowedProposalPaths = [conflict, narrativePath],
            AfterlifeContract = new()
            {
                RealmGate = WorkerAfterlifeRealmGate.ChaosSea,
                CurrentRealm = "Chaos Sea",
                AllowedAfterlifeSurfaces = [conflict],
                RequiredReceipts = ["Client-owned continuation."],
                RequiredReports = ["Apply result."],
                ForbiddenMortalSubstitutes = ["Mortal combat state"]
            }
        };
        var seed = GmWorkerBridgeTestFixtures.ValidationRepairProposal();
        var proposal = seed with
        {
            SpiritualWoundContinuation = continuation ? SpiritualWoundContinuationProtocol.ReadResponse(
                JsonSerializer.Deserialize<JsonElement>(Response)) : null,
            ChangedFiles = [new WorkerChangedFile
            {
                Path = narrativePath, ChangeKind = WorkerFileChangeKind.Replace,
                BeforeSha256 = new string('a', 64), AfterSha256 = new string('b', 64),
                ContentRef = $"worker_proposals/{seed.ProposalId}/{narrativePath}"
            }]
        };
        var taskResult = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        var proposalResult = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);
        Assert.True(taskResult.IsValid == expectedValid, string.Join(Environment.NewLine, taskResult.Errors));
        Assert.True(proposalResult.IsValid == expectedValid, string.Join(Environment.NewLine, proposalResult.Errors));
    }

    /// <summary>
    /// Keeps the complete dependent scope or fails before dispatch instead of silently dropping a required path.
    /// </summary>
    /// <param name="missingPath">
    /// Required proposal permission to omit, or <see langword="null"/> to retain both paths.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("output/narrative_response.json")]
    [InlineData("game_state/meta/afterlife_spiritual_conflict_state.json")]
    public void ContinuationBuilder_RequiresEveryDependentDraftPermission(string? missingPath)
    {
        const string narrative = "output/narrative_response.json";
        var conflict = AfterlifeSpiritualConflictState.StatePath;
        var request = SpiritualWoundContinuationProtocol.ReadRequest(JsonSerializer.Deserialize<JsonElement>(Request)) with
        {
            Phase = "dependent_draft", Offer = null,
            DependentDraftFields = [new() { Path = conflict, JsonPointer = "/activeConflict/exchangeLog/1/actionCostAudit/opposition/effectiveCost" }]
        };
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        profile = profile with { Permissions = profile.Permissions with
        {
            ProposalWritePaths = new[] { narrative, conflict }.Where(path => path != missingPath).ToArray()
        } };
        var seed = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var hashes = new[] { narrative, conflict, AfterlifeRealmAuthorityContract.StatePath }
            .ToDictionary(path => path, _ => new string('a', 64));
        WorkerTaskPacket Build() => GmWorkerTaskPacketBuilder.BuildValidationRepairTask(profile, seed.TaskId,
            seed.SourceTurn, [], hashes, seed.CreatedAtUtc, seed.SessionGeneration,
            new WorkerAfterlifeTaskContract
            {
                RealmGate = WorkerAfterlifeRealmGate.ChaosSea, CurrentRealm = "Chaos Sea",
                AllowedAfterlifeSurfaces = [conflict], RequiredReceipts = ["Client-owned continuation"],
                RequiredReports = ["Apply result"], ForbiddenMortalSubstitutes = ["Mortal combat state"]
            }, request);
        if (missingPath is not null)
        {
            var error = Assert.Throws<ArgumentException>(Build);
            Assert.Contains("every required continuation draft path", error.Message);
            return;
        }
        var task = Build();
        Assert.Empty(task.ValidationIssues);
        Assert.Equal(new[] { conflict, narrative }, task.AllowedProposalPaths);
        Assert.Equal(request, task.SpiritualWoundContinuation);
    }

    private const string Request = """
        {"schemaVersion":1,"continuationId":"swc_current","phase":"decision",
         "offer":{"opportunityRef":"current_offer","minimumSeverityRank":1,
           "requiredSeverityRank":null,"maximumSeverityRank":2,"target":"Душа",
           "cause":"Духовный удар","allowedLocationKinds":["spiritual_axis"],
           "allowedDecisions":["none","materialize"]},
         "sceneTextSource":{"path":"output/narrative_response.json","field":"response"},
         "dependentDraftFields":[]}
        """;

    private const string Response = """
        {"schemaVersion":1,"continuationId":"swc_current",
         "woundDecisions":[{"opportunityRef":"current_offer","decision":"none"}]}
        """;

    /// <summary>
    /// Ensures a worker receives the exact current offer after task serialization.
    /// </summary>
    [Fact]
    public void WorkerTaskRoundtrip_PreservesCurrentContinuationInsteadOfDroppingTheOffer()
    {
        var input = JsonNode.Parse(GmWorkerJson.Serialize(GmWorkerBridgeTestFixtures.ValidationRepairTask()))!;
        input["spiritualWoundContinuation"] = JsonNode.Parse(Request);
        var task = GmWorkerJson.Deserialize<WorkerTaskPacket>(input.ToJsonString());
        var output = JsonNode.Parse(GmWorkerJson.Serialize(task))!;
        Assert.True(JsonNode.DeepEquals(input["spiritualWoundContinuation"], output["spiritualWoundContinuation"]));
    }

    /// <summary>
    /// Preserves an explicit decline rather than silently losing the worker response.
    /// </summary>
    [Fact]
    public void WorkerProposalRoundtrip_PreservesExplicitNoneDecision()
    {
        var input = JsonNode.Parse(GmWorkerJson.Serialize(GmWorkerBridgeTestFixtures.ValidationRepairProposal()))!;
        input["spiritualWoundContinuation"] = JsonNode.Parse(Response);
        var proposal = GmWorkerJson.Deserialize<WorkerProposal>(input.ToJsonString());
        var output = JsonNode.Parse(GmWorkerJson.Serialize(proposal))!;
        Assert.True(JsonNode.DeepEquals(input["spiritualWoundContinuation"], output["spiritualWoundContinuation"]));
    }

    /// <summary>
    /// Rejects malformed wire fields before permissive worker conversion can discard them.
    /// </summary>
    /// <param name="before">
    /// Exact valid fragment to replace.
    /// </param>
    /// <param name="after">
    /// Invalid replacement exercising one closed-envelope constraint.
    /// </param>
    [Theory]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1")]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":2")]
    [InlineData("\"phase\":\"decision\"", "\"phase\":\"Decision\"")]
    [InlineData("\"continuationId\"", "\"ContinuationId\"")]
    [InlineData("\"offer\":", "\"privateCheckpoint\":{},\"offer\":")]
    [InlineData("\"target\":\"Душа\"", "\"target\":\"Душа\",\"target\":\"Душа\"")]
    public void WorkerTaskRead_RejectsMalformedEnvelopeBeforeLooseDeserialization(string before, string after)
    {
        var json = "{\"spiritualWoundContinuation\":" + Request.Replace(before, after, StringComparison.Ordinal) + "}";
        Assert.Throws<InvalidDataException>(() => GmWorkerJson.Deserialize<WorkerTaskPacket>(json));
    }

    /// <summary>
    /// Accepts a current explicit choice when no validation error or file correction exists.
    /// </summary>
    [Fact]
    public void CurrentDecisionResponse_AllowsCompletionWithoutInventedErrorsOrFileChanges()
    {
        var (task, proposal) = ContinuationPair();
        var result = GmWorkerContractValidator.ValidateProposal(proposal, task,
            GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile());
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    /// <summary>
    /// Rejects missing, stale, misbound or phase-incompatible choices despite otherwise valid files.
    /// </summary>
    /// <param name="mutation">
    /// Named response or task mutation breaking correlation or cardinality.
    /// </param>
    [Theory]
    [InlineData("missing")]
    [InlineData("stale")]
    [InlineData("wrong_offer")]
    [InlineData("two_decisions")]
    [InlineData("empty_decisions")]
    [InlineData("ordinary_repair")]
    public void WorkerContract_RejectsUncorrelatedContinuationEvenWithValidFileChanges(string mutation)
    {
        var taskNode = JsonNode.Parse(GmWorkerJson.Serialize(GmWorkerBridgeTestFixtures.ValidationRepairTask()))!;
        var proposalNode = JsonNode.Parse(GmWorkerJson.Serialize(GmWorkerBridgeTestFixtures.ValidationRepairProposal()))!;
        taskNode["spiritualWoundContinuation"] = JsonNode.Parse(Request);
        proposalNode["spiritualWoundContinuation"] = JsonNode.Parse(Response);
        var response = proposalNode["spiritualWoundContinuation"]!;
        switch (mutation)
        {
            case "missing": proposalNode.AsObject().Remove("spiritualWoundContinuation"); break;
            case "stale": response["continuationId"] = "swc_stale"; break;
            case "wrong_offer": response["woundDecisions"]![0]!["opportunityRef"] = "other"; break;
            case "two_decisions": response["woundDecisions"]!.AsArray().Add(response["woundDecisions"]![0]!.DeepClone()); break;
            case "empty_decisions": response["woundDecisions"] = new JsonArray(); break;
            case "ordinary_repair": taskNode.AsObject().Remove("spiritualWoundContinuation"); break;
        }
        var result = GmWorkerContractValidator.ValidateProposal(
            GmWorkerJson.Deserialize<WorkerProposal>(proposalNode.ToJsonString()),
            GmWorkerJson.Deserialize<WorkerTaskPacket>(taskNode.ToJsonString())!,
            GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile());
        Assert.False(result.IsValid);
    }

    private static (WorkerTaskPacket Task, WorkerProposal Proposal) ContinuationPair()
    {
        var task = JsonNode.Parse(GmWorkerJson.Serialize(GmWorkerBridgeTestFixtures.ValidationRepairTask()))!;
        var proposal = JsonNode.Parse(GmWorkerJson.Serialize(GmWorkerBridgeTestFixtures.ValidationRepairProposal()))!;
        task["spiritualWoundContinuation"] = JsonNode.Parse(Request);
        task["validationIssues"] = new JsonArray();
        proposal["spiritualWoundContinuation"] = JsonNode.Parse(Response);
        proposal["changedFiles"] = new JsonArray();
        return (GmWorkerJson.Deserialize<WorkerTaskPacket>(task.ToJsonString())!,
            GmWorkerJson.Deserialize<WorkerProposal>(proposal.ToJsonString())!);
    }

    /// <summary>
    /// Rejects absent structure and invalid decision payload fields during proposal reading.
    /// </summary>
    /// <param name="response">
    /// Malformed raw envelope to embed in an otherwise recognizable proposal.
    /// </param>
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1,\"continuationId\":\"swc_current\",\"woundDecisions\":null}")]
    [InlineData("{\"schemaVersion\":1,\"continuationId\":\"swc_current\",\"woundDecisions\":[{\"opportunityRef\":\"current_offer\",\"decision\":\"none\",\"proposal\":{}}]}")]
    public void WorkerProposalRead_RejectsMalformedResponse(string response)
    {
        Assert.Throws<InvalidDataException>(() => GmWorkerJson.Deserialize<WorkerProposal>(
            "{\"status\":\"completed\",\"spiritualWoundContinuation\":" + response + "}"));
    }

    /// <summary>
    /// Prevents the outer case-insensitive serializer from accepting an envelope alias.
    /// </summary>
    /// <param name="name">
    /// Incorrectly cased envelope key.
    /// </param>
    [Theory]
    [InlineData("SpiritualWoundContinuation")]
    [InlineData("spiritualwoundcontinuation")]
    public void WorkerRead_RejectsEnvelopeCaseAliases(string name)
    {
        Assert.Throws<InvalidDataException>(() => GmWorkerJson.Deserialize<WorkerTaskPacket>(
            "{\"" + name + "\":" + Request + "}"));
    }

    /// <summary>
    /// Rejects duplicate envelope keys instead of selecting one occurrence.
    /// </summary>
    [Fact]
    public void WorkerRead_RejectsDuplicateEnvelopesEvenWhenIdentical()
    {
        Assert.Throws<InvalidDataException>(() => GmWorkerJson.Deserialize<WorkerTaskPacket>(
            "{\"spiritualWoundContinuation\":" + Request + ",\"spiritualWoundContinuation\":" + Request + "}"));
    }

    /// <summary>
    /// Rejects deep duplicate proposal keys before any JSON object conversion loses them.
    /// </summary>
    [Fact]
    public void WorkerRead_RejectsDuplicateKeysInsideMaterializeProposal()
    {
        const string response = """
            {"schemaVersion":1,"continuationId":"swc_current","woundDecisions":[{
              "opportunityRef":"current_offer","decision":"materialize","woundRef":"new_wound",
              "proposal":{"classification":{"woundType":"spiritual","woundType":"spiritual"},
                "display":{},"severity":"I","complications":[],"consequenceDefinitions":[],
                "treatment":{},"recovery":{}}}]}
            """;
        Assert.Throws<InvalidDataException>(() => GmWorkerJson.Deserialize<WorkerProposal>(
            "{\"status\":\"completed\",\"spiritualWoundContinuation\":" + response + "}"));
    }

    /// <summary>
    /// Preserves a legitimate none-only offer when further worsening exceeds the rank cap.
    /// </summary>
    [Fact]
    public void ExhaustedRankOffer_PreservesExplicitNoneWithoutDemandingAnImpossibleNewRank()
    {
        var request = JsonNode.Parse(Request)!;
        request["offer"]!["minimumSeverityRank"] = 5;
        request["offer"]!["maximumSeverityRank"] = 4;
        request["offer"]!["allowedDecisions"] = new JsonArray("none");
        var parsed = SpiritualWoundContinuationProtocol.ReadRequest(JsonSerializer.SerializeToElement(request));
        using var responseDocument = JsonDocument.Parse(Response);
        var response = SpiritualWoundContinuationProtocol.ReadResponse(responseDocument.RootElement);
        Assert.Empty(SpiritualWoundContinuationProtocol.ValidateResponse(parsed, response));
    }

    /// <summary>
    /// Rejects a decline when the current owner-derived offer requires materialization.
    /// </summary>
    [Fact]
    public void GuaranteedOffer_CannotBeDeclined()
    {
        var request = JsonNode.Parse(Request)!;
        request["offer"]!["requiredSeverityRank"] = 1;
        request["offer"]!["allowedDecisions"] = new JsonArray("materialize");
        var parsed = SpiritualWoundContinuationProtocol.ReadRequest(JsonSerializer.SerializeToElement(request));
        using var responseDocument = JsonDocument.Parse(Response);
        var response = SpiritualWoundContinuationProtocol.ReadResponse(responseDocument.RootElement);
        Assert.NotEmpty(SpiritualWoundContinuationProtocol.ValidateResponse(parsed, response));
    }

    /// <summary>
    /// Keeps dependent correction distinct from submitting or replacing a wound choice.
    /// </summary>
    /// <param name="includeDecision">
    /// Whether the response illegally repeats a decision during correction.
    /// </param>
    /// <param name="noChangedFiles">
    /// Whether the proposal omits the required dependent file correction.
    /// </param>
    /// <param name="expectedValid">
    /// Expected structural contract result before genuine owner admission.
    /// </param>
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void DependentDraft_RequiresFileCorrectionAndCannotReplaceTheDecision(
        bool includeDecision, bool noChangedFiles, bool expectedValid)
    {
        var task = JsonNode.Parse(GmWorkerJson.Serialize(GmWorkerBridgeTestFixtures.ValidationRepairTask()))!;
        var request = JsonNode.Parse(Request)!;
        request["phase"] = "dependent_draft";
        request["offer"] = null;
        request["dependentDraftFields"] = new JsonArray(new JsonObject
        {
            ["path"] = "game_state/afterlife/spiritual_conflict.json",
            ["jsonPointer"] = "/exchanges/1/result"
        });
        task["spiritualWoundContinuation"] = request;
        var proposal = JsonNode.Parse(GmWorkerJson.Serialize(GmWorkerBridgeTestFixtures.ValidationRepairProposal()))!;
        var response = JsonNode.Parse(Response)!;
        if (!includeDecision) response["woundDecisions"] = new JsonArray();
        proposal["spiritualWoundContinuation"] = response;
        if (noChangedFiles) proposal["changedFiles"] = new JsonArray();
        var result = GmWorkerContractValidator.ValidateProposal(
            GmWorkerJson.Deserialize<WorkerProposal>(proposal.ToJsonString()),
            GmWorkerJson.Deserialize<WorkerTaskPacket>(task.ToJsonString())!,
            GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile());
        Assert.Equal(expectedValid, result.IsValid);
    }

    /// <summary>
    /// Rejects ambiguous relative paths and invalid JSON pointer escapes in permitted fields.
    /// </summary>
    /// <param name="path">
    /// Candidate draft path with a path or pointer defect.
    /// </param>
    /// <param name="pointer">
    /// Candidate pointer paired with the path.
    /// </param>
    [Theory]
    [InlineData("/absolute.json", "/field")]
    [InlineData("../outside.json", "/field")]
    [InlineData("game_state\\draft.json", "/field")]
    [InlineData("game_state/draft.json", "field")]
    [InlineData("game_state/draft.json", "/field~2")]
    [InlineData("game_state/draft.json", "/field~")]
    public void DependentFields_RejectNoncanonicalPathsAndMalformedPointers(string path, string pointer)
    {
        var request = JsonNode.Parse(Request)!;
        request["phase"] = "dependent_draft";
        request["offer"] = null;
        request["dependentDraftFields"] = new JsonArray(new JsonObject { ["path"] = path, ["jsonPointer"] = pointer });
        Assert.Throws<InvalidDataException>(() => SpiritualWoundContinuationProtocol.ReadRequest(
            JsonSerializer.SerializeToElement(request)));
    }
}
